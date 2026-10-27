package tank;
import java.util.*;
import static tank.GameRules.*;
import static tank.Protocol.*;

/** Server-owned schedule and hazards. Its RNG never consumes gameplay randomness. */
final class WorldBattlefield {
    // Kind 1 is reserved for the removed collapse event.
    static final int FIRE=2,AIRSTRIKE=3,FIRE_CAUSE=11,AIRSTRIKE_CAUSE=12;
    record Wave(int id,int kind,int start,int impact,int end,List<Integer> targets) {
        Wave {
            targets=List.copyOf(targets);
            int warning=kind==FIRE?BattlefieldFireWarningTicks:BattlefieldWarningTicks;
            if(id<=0||(kind!=FIRE&&kind!=AIRSTRIKE)||start<0||start>MaxMatchTicks||impact!=start+warning||end!=impact+BattlefieldEventTicks
                ||targets.size()!=BattlefieldTargetsPerRound*BattlefieldRounds
                ||targets.stream().anyMatch(t->t<0||t>63))throw new IllegalArgumentException("battlefield wave");
            int count=BattlefieldTargetsPerRound;
            for(int round=0;round<BattlefieldRounds;round++) {
                var current=targets.subList(round*count,(round+1)*count);
                if(new HashSet<>(current).size()!=count||kind==AIRSTRIKE&&round>0&&!Collections.disjoint(current,targets.subList((round-1)*count,round*count)))throw new IllegalArgumentException("battlefield targets");
            }
        }
        int rounds() {return BattlefieldRounds;}
        int targetsPerRound() {return targets.size()/rounds();}
        int target(int round,int index) {return targets.get(round*targetsPerRound()+index);}
        int impact(int round) {return impact+round*(kind==AIRSTRIKE?(BattlefieldEventTicks-BattlefieldAirstrikeTicks)/(BattlefieldRounds-1):BattlefieldRoundIntervalTicks);}
        void write(Writer out) {out.u32(id).u8(kind).u32(start).u32(impact).u32(end).u8(targets.size());targets.forEach(out::u8);}
        static Wave read(Reader r) {
            int id=r.u32(),kind=r.u8(),start=r.u32(),impact=r.u32(),end=r.u32(),n=r.u8();
            if(n!=BattlefieldTargetsPerRound*BattlefieldRounds)throw new IllegalArgumentException("battlefield count");
            List<Integer> targets=new ArrayList<>();for(int i=0;i<n;i++)targets.add(r.u8());
            return new Wave(id,kind,start,impact,end,targets);
        }
    }
    final World w;
    final Random random;
    final ArrayDeque<Integer> bag=new ArrayDeque<>();
    Wave wave;
    boolean enabled;
    int nextTick=BattlefieldFirstTicks,nextId=1,matchTicks,lastKind;
    WorldBattlefield(World world,long seed) {w=world;random=new Random(seed^0x424154544c45464cL);}
    void configure(int ticks) {enabled=w.map!=null&&w.map.battlefield!=null;matchTicks=ticks;}
    void clear() {enabled=false;wave=null;bag.clear();}
    void beginTick() {
        if(!enabled)return;
        if(wave!=null&&w.tick>=wave.end)wave=null;
        if(w.tick>=nextTick) {
            schedule();nextTick=w.tick+BattlefieldMinIntervalTicks+random.nextInt(BattlefieldMaxIntervalTicks-BattlefieldMinIntervalTicks+1);
        }
    }
    void schedule() {
        var map=w.map.battlefield;
        if(bag.isEmpty()) {
            var types=new ArrayList<>(List.of(FIRE,AIRSTRIKE));Collections.shuffle(types,random);
            if(types.getFirst()==lastKind)Collections.swap(types,0,1);bag.addAll(types);
        }
        int kind=bag.removeFirst();
        int warning=kind==FIRE?BattlefieldFireWarningTicks:BattlefieldWarningTicks;
        if(w.tick+warning+BattlefieldEventTicks>=matchTicks)return;
        List<Integer> targets=new ArrayList<>();
        if(kind==FIRE) {
            for(int i=0;i<BattlefieldRounds;i++) {
                var current=pickTargets(map,FIRE,BattlefieldTargetsPerRound,List.of());
                if(current.isEmpty())return;targets.addAll(current);
            }
        }
        else targets=airstrikeTargets(map);
        if(targets.isEmpty())return;
        wave=new Wave(nextId++,kind,w.tick,w.tick+warning,w.tick+warning+BattlefieldEventTicks,targets);
        lastKind=kind;
        w.telemetry.accept("kind=battlefield id="+wave.id+" type="+kind+" tick="+w.tick+" targets="+targets);
    }
    List<Integer> pickTargets(BattlefieldMap map,int kind,int count,List<Integer> previous) {
        var zones=new ArrayList<>(List.of(kind==FIRE?map.fireZones:map.airstrikeZones));
        zones.removeIf(z->previous.stream().anyMatch(id->{var p=map.zone(kind,id);return Math.hypot(z.x-p.x,z.y-p.y)<BattlefieldAirstrikeSpacing;}));
        Collections.shuffle(zones,random);
        long[] compatible=new long[zones.size()];
        for(int i=0;i<zones.size();i++)for(int j=0;j<zones.size();j++) {
            var a=zones.get(i);var b=zones.get(j);
            if(Math.hypot(a.x-b.x,a.y-b.y)>=BattlefieldAirstrikeSpacing)compatible[i]|=1L<<j;
        }
        List<Integer> indices=new ArrayList<>();
        // Bounded search prevents pathological custom maps from stalling the 30 Hz loop.
        long available=zones.size()==64?-1L:(1L<<zones.size())-1;
        if(!chooseTargets(available,count,compatible,indices,new int[]{20000}))return List.of();
        return indices.stream().map(i->zones.get(i).id).toList();
    }
    boolean chooseTargets(long available,int count,long[] compatible,List<Integer> chosen,int[] budget) {
        if(count==0)return true;
        while(Long.bitCount(available)>=count&&--budget[0]>=0) {
            int index=Long.numberOfTrailingZeros(available);available&=available-1;chosen.add(index);
            if(chooseTargets(available&compatible[index],count-1,compatible,chosen,budget))return true;
            chosen.removeLast();
        }
        return false;
    }
    List<Integer> airstrikeTargets(BattlefieldMap map) {
        // Reserve two complete separated runs before choosing the first regions.
        List<Integer> targets=new ArrayList<>(pickTargets(map,AIRSTRIKE,2*BattlefieldTargetsPerRound,List.of()));
        if(targets.isEmpty())return List.of();
        for(int round=2;round<BattlefieldRounds;round++) {
            var previous=targets.subList((round-1)*BattlefieldTargetsPerRound,round*BattlefieldTargetsPerRound);
            var current=pickTargets(map,AIRSTRIKE,BattlefieldTargetsPerRound,previous);
            if(current.isEmpty())return List.of();targets.addAll(current);
        }
        return targets;
    }
    void damage() {
        if(!enabled||wave==null||w.tick<wave.impact||w.tick>=wave.end)return;
        for(int round=0;round<wave.rounds();round++) {
            int impact=wave.impact(round);
            if(wave.kind==AIRSTRIKE?w.tick!=impact:w.tick<impact||w.tick>=impact+BattlefieldFireTicks||(w.tick-impact)%BattlefieldFireDamageTicks!=0)continue;
            for(int i=0;i<wave.targetsPerRound();i++)damage(wave.target(round,i));
        }
    }
    void damage(int id) {
        var z=w.map.battlefield.zone(wave.kind,id);
        for(World.Tank t:w.tanks.values())if(t.hp>0&&NationSkills.circle(t,z.x,z.y,BattlefieldRadius))
            w.damage(t,-1,wave.kind==FIRE?BattlefieldFireDamage:BattlefieldAirstrikeDamage,0,0,wave.kind==FIRE?FIRE_CAUSE:AIRSTRIKE_CAUSE,wave.id);
    }
    byte[] encode() {Writer out=new Writer().u8(wave==null?0:1);if(wave!=null)wave.write(out);return out.done();}
    void read(Reader r) {int n=r.u8();if(n>1)throw new IllegalArgumentException("battlefield count");wave=n==0?null:Wave.read(r);}
}
