package tank;
import java.util.*;
import static tank.Protocol.*;
import static tank.GameRules.*;

/** Presentation-only history. Never uses gameplay RNG or drives simulation. */
final class CombatEvents {
    static final int SHOT=1,IMPACT=2,BOUNCE=3,DAMAGE=4,DEATH=5,BRICK_DESTROYED=6,RECORD_BYTES=43;
    record Event(int id,int tick,int kind,int sourceSlot,int targetSlot,int sourceLife,int targetLife,
                 int bulletId,int objectId,int surface,float x,float y,int angle,int value,int causeKind,int causeId) {
        Event {
            if(id<=0||tick<0||kind<SHOT||kind>BRICK_DESTROYED||!slotLife(sourceSlot,sourceLife)||!slotLife(targetSlot,targetLife)
                ||bulletId<0||(objectId!=65535&&(objectId<0||objectId>63))||surface<0||surface>2
                ||!Float.isFinite(x)||!Float.isFinite(y)||angle<0||angle>=36000||value<0||value>65535
                ||!(causeKind==0||causeKind>=1&&causeKind<=4||causeKind==10||causeKind==11||causeKind==12)||(causeKind==0?causeId!=0:causeId<=0)
                ||kind!=DAMAGE&&kind!=DEATH&&causeKind!=0)throw new IllegalArgumentException("combat event");
        }
        static boolean slotLife(int slot,int life) {return slot==255?life==0:slot>=0&&slot<8&&life>0;}
        boolean critical() {return kind==DEATH||kind==BRICK_DESTROYED;}
        void write(Writer w) {
            w.u32(id).u32(tick).u8(kind).u8(sourceSlot).u8(targetSlot).u32(sourceLife).u32(targetLife).u32(bulletId)
                .u16(objectId).u8(surface).f32(x).f32(y).u16(angle).u16(value).u8(causeKind).u32(causeId);
        }
        static Event read(Reader r) {
            return new Event(r.u32(),r.u32(),r.u8(),r.u8(),r.u8(),r.u32(),r.u32(),r.u32(),r.u16(),r.u8(),r.f32(),r.f32(),r.u16(),r.u16(),r.u8(),r.u32());
        }
    }
    final TreeMap<Integer,Event> events=new TreeMap<>();
    int nextId=1;
    boolean enabled=true;
    long decorativeDropped,criticalDropped;
    void prune(int tick) {events.values().removeIf(e->tick-e.tick()>=CombatEventRetentionTicks);}
    void add(int tick,int kind,World.Tank source,World.Tank target,int bulletId,int objectId,int surface,float x,float y,int angle,int value,int causeKind,int causeId) {
        if(!enabled)return;
        if(source!=null&&source.life<=0)source=null;
        if(target!=null&&target.life<=0)target=null;
        Event e=new Event(nextId++,tick,kind,source==null?255:source.slot,target==null?255:target.slot,source==null?0:source.life,target==null?0:target.life,
            bulletId,objectId,surface,x,y,angle,value,causeKind,causeId);
        int count=0,oldest=0;
        for(Event old:events.values())if(old.critical()==e.critical()) {if(count++==0)oldest=old.id();}
        int budget=e.critical()?CombatCriticalReserve:MaxCombatEvents-CombatCriticalReserve;
        if(count>=budget) {events.remove(oldest);if(e.critical())criticalDropped++;else decorativeDropped++;}
        events.put(e.id(),e);
    }
    byte[] encode() {
        if(events.size()>MaxCombatEvents)throw new IllegalArgumentException("combat count");
        Writer w=new Writer().u16(events.size());for(Event e:events.values())e.write(w);return w.done();
    }
    void read(Reader r) {
        int count=r.u16();if(count>MaxCombatEvents)throw new IllegalArgumentException("combat count");
        events.clear();int last=0;
        for(int i=0;i<count;i++) {Event e=Event.read(r);if(e.id()<=last)throw new IllegalArgumentException("combat order");last=e.id();events.put(e.id(),e);}
    }
    byte[] delta(CombatEvents base) {
        Writer out=new Writer();var removed=base.events.keySet().stream().filter(id->!events.containsKey(id)).toList();
        out.u16(removed.size());removed.forEach(out::u32);
        List<Event> added=new ArrayList<>();
        for(Event e:events.values()) {
            Event old=base.events.get(e.id());
            if(old==null)added.add(e);else if(!old.equals(e))throw new IllegalArgumentException("combat mutation");
        }
        out.u16(added.size());for(Event e:added)e.write(out);return out.done();
    }
    void apply(Reader r) {
        int count=r.u16();if(count>MaxCombatEvents)throw new IllegalArgumentException("combat removals");
        Set<Integer> removed=new HashSet<>();
        for(int i=0;i<count;i++) {int id=r.u32();if(id<=0||!removed.add(id)||events.remove(id)==null)throw new IllegalArgumentException("combat removal");}
        count=r.u16();if(count>MaxCombatEvents)throw new IllegalArgumentException("combat additions");
        int last=0;
        for(int i=0;i<count;i++) {
            Event e=Event.read(r);
            if(e.id()<=last||removed.contains(e.id())||events.putIfAbsent(e.id(),e)!=null)throw new IllegalArgumentException("combat addition");last=e.id();
        }
        if(events.size()>MaxCombatEvents)throw new IllegalArgumentException("combat count");
    }
}
