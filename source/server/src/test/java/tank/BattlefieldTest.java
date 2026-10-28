package tank;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
import static tank.GameRules.*;
import static tank.WorldBattlefield.*;
import static tank.Protocol.*;

class BattlefieldTest {
    GameMap map()throws Exception {return GameMap.load(Path.of("../../source/client/Assets/StreamingAssets/map-large.json"));}
    World world()throws Exception {World w=new World(map(),31);w.battlefield.configure(18000);w.battlefield.nextTick=18001;w.add(0);w.add(1);return w;}
    Wave wave(int kind,int start,int anchor)throws Exception {
        int impact=start+(kind==FIRE?BattlefieldFireWarningTicks:BattlefieldWarningTicks);List<Integer> targets=new ArrayList<>();
        if(kind==FIRE)for(int round=0;round<BattlefieldRounds;round++)for(int i=0;i<BattlefieldTargetsPerRound;i++)targets.add(round==1?6+i:i);
        else {
            var m=map();var scheduler=new WorldBattlefield(new World(m,31),31);targets.add(anchor);targets.addAll(scheduler.pickTargets(m.battlefield,AIRSTRIKE,2*BattlefieldTargetsPerRound-1,List.of(anchor)));
            assertEquals(2*BattlefieldTargetsPerRound,targets.size());targets.addAll(new ArrayList<>(targets.subList(0,BattlefieldTargetsPerRound)));
        }
        return new Wave(1,kind,start,impact,impact+BattlefieldEventTicks,targets);
    }
    void force(World w,int kind)throws Exception {
        w.battlefield.wave=wave(kind,0,0);w.tick=w.battlefield.wave.impact()-1;
        var z=w.map.battlefield.zone(kind,0);var t=w.tanks.get(0);t.x=z.x;t.y=z.y;
    }
    @Test void shippedMapsHaveSafeDryRegionsAndEscapePaths()throws Exception {
        for(String name:new String[]{"map.json","map-large.json"}) {
            GameMap m=GameMap.load(Path.of("../../source/client/Assets/StreamingAssets/"+name));
            assertTrue(m.battlefield.fireZones.length>=6);assertTrue(m.battlefield.airstrikeZones.length>=6);
            byte[] hp=new byte[64];Arrays.fill(hp,(byte)BrickMaxHp);
            for(var z:m.battlefield.fireZones) {
                assertFalse(m.blocked(z.x,z.y,hp));
                // Search a traversable tank-centre grid, not just a visually empty circle.
                Set<String> seen=new HashSet<>();ArrayDeque<float[]> queue=new ArrayDeque<>();queue.add(new float[]{z.x,z.y});boolean escaped=false;
                while(!queue.isEmpty()) {
                    var p=queue.removeFirst();if(!seen.add(p[0]+":"+p[1])||m.blocked(p[0],p[1],4500,hp))continue;
                    if(Math.hypot(p[0]-z.x,p[1]-z.y)>BattlefieldRadius+TankHalfSize*Math.sqrt(2)) {escaped=true;break;}
                    for(float[] d:new float[][]{{.5f,0},{-.5f,0},{0,.5f},{0,-.5f}})queue.add(new float[]{p[0]+d[0],p[1]+d[1]});
                }
                assertTrue(escaped,"escape zone "+z.id+" in "+name);
            }
            m.battlefield.fireZones[0].x=m.spawns[0].x;m.battlefield.fireZones[0].y=m.spawns[0].y;
            assertThrows(IllegalArgumentException.class,m::validate);
        }
    }
    @Test void invalidMapReferencesDuplicatesAndWetRegionsAreRejected()throws Exception {
        var m=map();m.battlefield.airstrikeZones=null;assertThrows(IllegalArgumentException.class,m::validate);
        m=map();m.battlefield.fireZones[1].id=m.battlefield.fireZones[0].id;assertThrows(IllegalArgumentException.class,m::validate);
        m=map();m.battlefield.fireZones[0].x=43;m.battlefield.fireZones[0].y=16;assertThrows(IllegalArgumentException.class,m::validate);
    }
    @Test void firstEventAndSeededIntervalsAreDeterministicWithoutConsumingWorldRng()throws Exception {
        World a=world(),b=world(),control=world();a.battlefield.nextTick=b.battlefield.nextTick=BattlefieldFirstTicks;
        for(int tick=1;tick<8500;tick++) {
            a.tick=b.tick=tick;a.battlefield.beginTick();b.battlefield.beginTick();
            assertEquals(a.battlefield.wave,b.battlefield.wave);assertEquals(a.battlefield.nextTick,b.battlefield.nextTick);
            if(tick<BattlefieldFirstTicks)assertNull(a.battlefield.wave);
            if(a.battlefield.wave!=null&&a.battlefield.wave.start()==tick) {
                assertTrue(a.battlefield.nextTick-tick>=800&&a.battlefield.nextTick-tick<=1200);
            }
        }
        assertEquals(control.random.nextLong(),a.random.nextLong());
        World c=world();c.tick=BattlefieldFirstTicks;c.battlefield.nextTick=BattlefieldFirstTicks;c.battlefield.beginTick();Set<Integer> kinds=new HashSet<>();
        for(int i=0;i<2;i++) {kinds.add(c.battlefield.wave.kind());c.tick=c.battlefield.nextTick;c.battlefield.beginTick();}
        assertEquals(Set.of(FIRE,AIRSTRIKE),kinds);
    }
    @Test void exhaustedBricksShortMatchesAndClearAreSafe()throws Exception {
        World w=world();Arrays.fill(w.bricks,(byte)0);w.tick=w.battlefield.nextTick=BattlefieldFirstTicks;w.battlefield.beginTick();assertNotNull(w.battlefield.wave);assertTrue(Set.of(FIRE,AIRSTRIKE).contains(w.battlefield.wave.kind()));
        w=world();w.battlefield.matchTicks=BattlefieldFirstTicks+BattlefieldFireWarningTicks+BattlefieldEventTicks;w.battlefield.bag.add(FIRE);w.tick=w.battlefield.nextTick=BattlefieldFirstTicks;w.battlefield.beginTick();assertNull(w.battlefield.wave);
        w.battlefield.clear();w.tick+=5000;w.battlefield.beginTick();assertNull(w.battlefield.wave);assertFalse(w.battlefield.enabled);
    }
    @Test void slowerScheduleKeepsEachShorterEventUntilItFinishes()throws Exception {
        World w=new World(map(),31);w.battlefield.configure(18000);Wave previous=null;int count=0;
        for(int tick=1;tick<=3600;tick++) {
            w.tick=tick;w.battlefield.beginTick();Wave current=w.battlefield.wave;
            if(tick<300)assertNull(current);
            if(tick==300)assertNotNull(current);
            if(current!=null&&(previous==null||current.id()!=previous.id())) {
                if(previous!=null) {
                    assertTrue(current.start()-previous.start()>=800&&current.start()-previous.start()<=1200);
                    assertTrue(current.start()>=previous.end(),"a new event must not replace an unfinished hazard");
                }
                assertEquals(60,current.impact()-current.start());assertEquals(300,current.end()-current.start());
                previous=current;count++;
            }
            if(previous!=null&&tick<previous.end())assertEquals(previous,current);
        }
        assertTrue(count>=3,"at least three waves should start within two minutes");
    }
    @Test void bothEventsDamageTanksAndLeaveTerrainCollisionAndVisibilityIntact()throws Exception {
        for(int kind:new int[]{FIRE,AIRSTRIKE}) {
            World w=world();w.battlefield.wave=wave(kind,0,kind==FIRE?0:6);w.tick=w.battlefield.wave.impact()-1;
            var target=w.map.battlefield.zone(kind,w.battlefield.wave.target(0,0));var tank=w.tanks.get(0);tank.x=target.x;tank.y=target.y;tank.hp=tank.maxHp=500;
            byte[] terrain=w.bricks.clone();var r=w.map.bricks[1];float x=r.x+r.w/2,y=r.y+r.h/2;int end=w.battlefield.wave.end();
            while(w.tick<end)w.step(Map.of());
            assertTrue(tank.hp<500);assertArrayEquals(terrain,w.bricks);assertTrue(w.map.blocked(x,y,w.bricks));assertFalse(w.visible(x-7,y,x+7,y));
            assertFalse(w.combat.events.values().stream().anyMatch(e->e.kind()==CombatEvents.BRICK_DESTROYED));
        }
    }
    @Test void fireWarnsThenDamagesExactlyEighteenTimesAndStopsAtBoundary()throws Exception {
        World w=world();force(w,FIRE);var t=w.tanks.get(0);t.hp=t.maxHp=500;
        w.battlefield.damage();assertEquals(500,t.hp);
        int end=w.battlefield.wave.end();while(w.tick<end) {
            int round=Math.min(2,Math.max(0,(w.tick+1-w.battlefield.wave.impact())/BattlefieldRoundIntervalTicks));
            var z=w.map.battlefield.zone(FIRE,w.battlefield.wave.target(round,0));t.x=z.x;t.y=z.y;w.step(Map.of());
        }
        assertEquals(410,t.hp);assertNull(w.battlefield.wave);
        w.step(Map.of());assertEquals(410,t.hp);
    }
    @Test void enteringLeavingArmorInvincibilityAndRotatedHitboxUseExistingDamageRules()throws Exception {
        World w=world();force(w,FIRE);var t=w.tanks.get(0);var z=w.map.battlefield.zone(FIRE,0);t.armorEndTick=1000;
        w.step(Map.of());assertEquals(97,t.hp); // round(5 * .65)
        t.invincibilityEndTick=1000;for(int i=0;i<15;i++)w.step(Map.of());assertEquals(97,t.hp);
        t.invincibilityEndTick=0;t.armorEndTick=0;t.x=z.x+BattlefieldRadius+TankHalfSize*(float)Math.sqrt(2)-.01f;t.body=4500;
        for(int i=0;i<15;i++)w.step(Map.of());assertEquals(92,t.hp);
        t.x+=.1f;for(int i=0;i<15;i++)w.step(Map.of());assertEquals(92,t.hp);
        t.x=z.x;for(int i=0;i<15;i++)w.step(Map.of());assertEquals(87,t.hp);
    }
    @Test void environmentDeathHasNoKillerAndRespawnsOutsideHazard()throws Exception {
        World w=world();force(w,FIRE);var t=w.tanks.get(0);t.hp=5;int life=t.life;w.step(Map.of());
        assertEquals(0,t.hp);assertEquals(1,t.deaths);assertEquals(0,w.tanks.get(1).kills);
        var death=w.combat.events.values().stream().filter(e->e.kind()==CombatEvents.DEATH).findFirst().orElseThrow();assertEquals(255,death.sourceSlot());assertEquals(FIRE_CAUSE,death.causeKind());
        while(w.tick<t.respawn)w.step(Map.of());assertTrue(t.hp>0);assertNotEquals(life,t.life);
        var z=w.map.battlefield.zone(FIRE,0);assertFalse(NationSkills.circle(t,z.x,z.y,BattlefieldRadius));
    }
    @Test void airstrikeOnlyDamagesTanksAndDoesNotChangeQCooldown()throws Exception {
        World w=world();int id=6; // first authored air target centred on an interior brick group
        w.battlefield.wave=wave(AIRSTRIKE,0,id);w.tick=w.battlefield.wave.impact()-1;var z=w.map.battlefield.zone(AIRSTRIKE,id);var t=w.tanks.get(0);t.x=z.x;t.y=z.y;t.qReadyTick=777;
        byte[] terrain=w.bricks.clone();w.step(Map.of());assertEquals(65,t.hp);assertEquals(777,t.qReadyTick);assertArrayEquals(terrain,w.bricks);
        w.step(Map.of());assertEquals(65,t.hp);
        assertTrue(w.combat.events.values().stream().anyMatch(e->e.causeKind()==AIRSTRIKE_CAUSE));
    }
    @Test void removedCollapseKindIsRejectedOnConstructionAndDecode() {
        assertThrows(IllegalArgumentException.class,()->new Wave(1,1,0,60,300,List.of(0)));
        byte[] legacy=new Writer().u8(1).u32(1).u8(1).u32(0).u32(60).u32(300).u8(1).u8(0).done();
        assertThrows(IllegalArgumentException.class,()->new WorldBattlefield(null,31).read(new Reader(legacy)));
    }
    @Test void airstrikeHitsThreeTimesWithoutDamageBetweenRoundsOrAfterTheDeadline()throws Exception {
        World w=world();w.battlefield.wave=wave(AIRSTRIKE,0,6);var t=w.tanks.get(0);t.hp=t.maxHp=500;
        for(int round=0;round<3;round++) {
            var z=w.map.battlefield.zone(AIRSTRIKE,w.battlefield.wave.target(round,0));t.x=z.x;t.y=z.y;
            if(round>0) {var old=w.map.battlefield.zone(AIRSTRIKE,w.battlefield.wave.target(round-1,0));w.tanks.get(1).x=old.x;w.tanks.get(1).y=old.y;}
            w.tick=w.battlefield.wave.impact(round)-1;w.step(Map.of());assertEquals(500-35*(round+1),t.hp);
            if(round>0)assertEquals(100,w.tanks.get(1).hp,"previous region must not be bombed again on the next run");
            w.step(Map.of());assertEquals(500-35*(round+1),t.hp);
        }
        int end=w.battlefield.wave.end();while(w.tick<=end)w.step(Map.of());assertEquals(395,t.hp);assertNull(w.battlefield.wave);
    }
    @Test void bothEventsAlwaysChooseFiveRandomRegionsPerRoundAndEndAtTheDeadline()throws Exception {
        Set<List<Integer>> schedules=new HashSet<>();
        for(String name:new String[]{"map.json","map-large.json"}) {
            var m=GameMap.load(Path.of("../../source/client/Assets/StreamingAssets/"+name));
            for(int kind:new int[]{FIRE,AIRSTRIKE})for(int seed=0;seed<200;seed++) {
                World w=new World(m,seed);w.battlefield.configure(18000);w.battlefield.bag.add(kind);w.tick=BattlefieldFirstTicks;w.battlefield.beginTick();var wave=w.battlefield.wave;
                assertNotNull(wave,name+" kind="+kind+" seed="+seed);assertEquals(5,wave.targetsPerRound());assertEquals(15,wave.targets().size());schedules.add(wave.targets());
                assertEquals(wave.end(),wave.impact(wave.rounds()-1)+(kind==FIRE?BattlefieldFireTicks:BattlefieldAirstrikeTicks));
                for(int round=0;round<wave.rounds();round++)for(int i=0;i<wave.targetsPerRound();i++) {
                    assertEquals(5,new HashSet<>(wave.targets().subList(round*5,round*5+5)).size());
                    var a=m.battlefield.zone(kind,wave.target(round,i));
                    for(int previous=kind==AIRSTRIKE?Math.max(0,round-1):round;previous<=round;previous++)for(int j=0;j<(previous==round?i:wave.targetsPerRound());j++) {
                        var b=m.battlefield.zone(kind,wave.target(previous,j));
                        assertTrue(Math.hypot(a.x-b.x,a.y-b.y)>=BattlefieldAirstrikeSpacing,"runs must use separate regions");
                    }
                }
                assertEquals(wave,World.decode(w.encode()).battlefield.wave);
            }
        }
        assertTrue(schedules.size()>20,"independent seeds should generate varied bombing runs");
        for(int kind:new int[]{FIRE,AIRSTRIKE})for(int count:new int[]{4,6})assertThrows(IllegalArgumentException.class,()->new Wave(1,kind,0,60,300,java.util.stream.IntStream.range(0,count*3).boxed().toList()));
    }
    @Test void insufficientCustomMapsNeverStartPartialWaves()throws Exception {
        World w=world();w.map.battlefield.airstrikeZones=Arrays.copyOf(w.map.battlefield.airstrikeZones,2);
        for(int seed=0;seed<30;seed++) {
            var scheduler=new WorldBattlefield(w,seed);var targets=scheduler.airstrikeTargets(w.map.battlefield);
            assertTrue(targets.isEmpty());
        }
        w.map.battlefield.airstrikeZones=Arrays.copyOf(w.map.battlefield.airstrikeZones,1);
        assertTrue(w.battlefield.airstrikeTargets(w.map.battlefield).isEmpty());
        w.battlefield.bag.add(AIRSTRIKE);w.tick=w.battlefield.nextTick=300;w.battlefield.beginTick();assertNull(w.battlefield.wave);
        w.map.battlefield.fireZones=Arrays.copyOf(w.map.battlefield.fireZones,4);w.battlefield.bag.clear();w.battlefield.bag.add(FIRE);w.battlefield.nextTick=300;w.battlefield.beginTick();assertNull(w.battlefield.wave);
    }
    @Test void fiveTargetBombingRunsProduceFullDeltaAndReconnectInteropFixture()throws Exception {
        World w=world();byte[] previous=w.encode();List<String> trace=new ArrayList<>();var hex=HexFormat.of();trace.add("FULL\t"+hex.formatHex(previous));
        w.battlefield.wave=wave(AIRSTRIKE,0,0);var wave=w.battlefield.wave;
        for(int round=0;round<wave.rounds();round++)for(int offset:new int[]{-1,0,1}) {
            w.tick=wave.impact(round)+offset;byte[] current=w.encode(),delta=DeltaCodec.encode(previous,current);
            assertArrayEquals(current,DeltaCodec.apply(previous,delta));assertArrayEquals(current,World.decode(current).encode());
            trace.add("DELTA\t"+hex.formatHex(delta)+"\t"+hex.formatHex(current));
            if(round==1&&offset==1)trace.add("REJOIN\t"+hex.formatHex(current));
            previous=current;
        }
        w.tick=wave.end();w.battlefield.beginTick();assertNull(w.battlefield.wave);byte[] ended=w.encode();
        trace.add("DELTA\t"+hex.formatHex(DeltaCodec.encode(previous,ended))+"\t"+hex.formatHex(ended));
        Path path=Path.of("target/interop/battlefield-bombstorm.tsv");Files.createDirectories(path.getParent());Files.write(path,trace);
    }
    @Test void fireChangesRegionsWithoutLeavingDamageInTheOldRegions()throws Exception {
        World w=world();force(w,FIRE);var t=w.tanks.get(0);var first=w.map.battlefield.zone(FIRE,0);t.x=first.x;t.y=first.y;
        int change=w.battlefield.wave.impact(1);while(w.tick<change)w.step(Map.of());assertEquals(70,t.hp);
        var next=w.map.battlefield.zone(FIRE,6);var other=w.tanks.get(1);other.x=next.x;other.y=next.y;
        w.step(Map.of());assertEquals(70,t.hp);assertEquals(100,other.hp);
        while(w.tick<change+BattlefieldFireDamageTicks)w.step(Map.of());assertEquals(70,t.hp);assertEquals(95,other.hp);
    }
    @Test void qCanRunDuringFireAndDestroyBricksWithoutDuplicateDebris()throws Exception {
        World w=world();force(w,FIRE);var t=w.tanks.get(0);t.nation=0;t.hp=t.maxHp=500;
        assertEquals(Protocol.Error.OK,w.useSkill(0,0));int ready=t.qReadyTick;var wave=w.battlefield.wave;
        w.step(Map.of());assertEquals(495,t.hp);assertEquals(ready,t.qReadyTick);assertEquals(wave,w.battlefield.wave);assertFalse(w.skills.effects.isEmpty());
        w=world();force(w,FIRE);var brick=w.map.bricks[1];
        World warned=w;w.tick--;w.skillIntents.add(()->warned.skills.breakCircle(brick.x+brick.w/2,brick.y+brick.h/2,10));w.step(Map.of());w.step(Map.of());
        assertEquals(3,w.combat.events.values().stream().filter(e->e.kind()==CombatEvents.BRICK_DESTROYED).count());
        for(int id:new int[]{0,1,2})assertEquals(0,w.bricks[id]);
    }
    @Test void malformedReplacementIsRejectedWithoutMutatingBase()throws Exception {
        World w=world();force(w,FIRE);byte[] current=w.encode(),before=current.clone();
        byte[] delta=DeltaCodec.encode(current,current);delta[delta.length-34]=2;
        assertThrows(IllegalArgumentException.class,()->DeltaCodec.apply(current,delta));assertArrayEquals(before,current);
        for(int count:new int[]{2,255})assertThrows(IllegalArgumentException.class,()->w.battlefield.read(new Reader(new byte[]{(byte)count})));
    }
    @Test void fullDeltaLostPacketsAndRejoinRoundTripAndProduceUnityFixture()throws Exception {
        World w=world();byte[] base=w.encode();List<String> trace=new ArrayList<>();var hex=HexFormat.of();trace.add("FULL\t"+hex.formatHex(base));
        force(w,FIRE);byte[] warning=w.encode();trace.add("DELTA\t"+hex.formatHex(DeltaCodec.encode(base,warning))+"\t"+hex.formatHex(warning));assertArrayEquals(warning,DeltaCodec.apply(base,DeltaCodec.encode(base,warning)));
        w.step(Map.of());byte[] dropped=w.encode();trace.add("DROPPED\t"+hex.formatHex(DeltaCodec.encode(warning,dropped)));
        for(int i=0;i<17;i++)w.step(Map.of());byte[] active=w.encode();trace.add("DELTA\t"+hex.formatHex(DeltaCodec.encode(warning,active))+"\t"+hex.formatHex(active));trace.add("REJOIN\t"+hex.formatHex(active));
        assertArrayEquals(active,DeltaCodec.apply(warning,DeltaCodec.encode(warning,active)));assertArrayEquals(active,World.decode(active).encode());
        w.battlefield.clear();byte[] ended=w.encode();trace.add("DELTA\t"+hex.formatHex(DeltaCodec.encode(active,ended))+"\t"+hex.formatHex(ended));assertArrayEquals(ended,DeltaCodec.apply(active,DeltaCodec.encode(active,ended)));
        Path path=Path.of("target/interop/battlefield.tsv");Files.createDirectories(path.getParent());Files.write(path,trace);
        assertThrows(IllegalArgumentException.class,()->new Wave(1,AIRSTRIKE,0,60,300,List.of(0,0)));
        assertThrows(IllegalArgumentException.class,()->new Wave(1,4,0,60,300,List.of(0)));
        assertThrows(IllegalArgumentException.class,()->new Wave(1,FIRE,0,61,301,List.of(0)));
    }
}
