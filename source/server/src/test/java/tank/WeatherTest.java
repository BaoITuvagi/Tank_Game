package tank;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
import static tank.GameRules.*;
import static tank.WorldBattlefield.*;

class WeatherTest {
    GameMap map(int id)throws Exception {return MapCatalog.load(Path.of("../../source/client/Assets/StreamingAssets"),null).get(id);}
    Wave wave(int kind) {return new Wave(1,kind,300,390,750,List.of());}
    void inside(World w,World.Tank t,int tick) {t.x=(Math.max(0,bandLeft(w.map,w.battlefield.wave,tick))+Math.min(w.map.width,bandRight(w.map,w.battlefield.wave,tick)))/2;t.y=10;}
    World world(int kind)throws Exception {
        var w=new World(map(kind==SANDSTORM?DesertMapId:WinterMapId),1);w.add(0);w.add(1);
        w.battlefield.configure(18000);w.battlefield.nextTick=18001;w.battlefield.wave=wave(kind);return w;
    }
    @Test void mapsUseTheCorrectAlternatingPairWithoutOverlappingOrConsumingGameplayRng()throws Exception {
        for(int id:new int[]{2,3,4})for(int seed=0;seed<30;seed++) {
            World w=new World(map(id),seed),control=new World(map(id),seed);w.battlefield.configure(18000);
            int last=0,end=0;Set<Integer> kinds=new HashSet<>();
            for(int i=0;i<8;i++) {
                w.tick=w.battlefield.nextTick;w.battlefield.beginTick();var wave=w.battlefield.wave;
                assertNotNull(wave);assertNotEquals(last,wave.kind());assertTrue(wave.start()>=end);
                assertEquals(weather(wave.kind())?90:60,wave.impact()-wave.start());
                assertEquals(weather(wave.kind())?0:15,wave.targets().size());
                assertTrue(w.battlefield.nextTick-w.tick>=800&&w.battlefield.nextTick-w.tick<=1200);
                end=wave.end();last=wave.kind();kinds.add(last);
            }
            assertEquals(Set.of(id==4?SNOWSTORM:FIRE,id==3?SANDSTORM:AIRSTRIKE),kinds);
            assertEquals(control.random.nextLong(),w.random.nextLong());
        }
        var w=world(SANDSTORM);w.tick=300;w.battlefield.wave=null;w.battlefield.bag.add(SANDSTORM);w.battlefield.matchTicks=630;w.battlefield.schedule();assertNull(w.battlefield.wave);
    }
    @Test void globalWeatherDamageHitsSixTimesAcrossTheMapAtExactPulsesAndLeavesMapUnchanged()throws Exception {
        for(int kind:new int[]{SANDSTORM,SNOWSTORM}) {
            World w=world(kind);byte[] bricks=w.bricks.clone();
            for(int tick=300;tick<=751;tick++) {
                w.tanks.get(0).x=tick%2==0?2:w.map.width-2;w.tanks.get(0).y=tick%2==0?2:w.map.height-2;
                w.tanks.get(1).x=-1;w.tanks.get(1).y=10;
                w.tick=tick;w.battlefield.beginTick();w.battlefield.damage();
                int count=0;for(int hit:new int[]{420,480,540,600,660,720})if(tick>=hit)count++;
                assertEquals(100-2*count,w.tanks.get(0).hp,"tick "+tick+" kind "+kind);
                assertEquals(100,w.tanks.get(1).hp,"outside tick "+tick+" kind "+kind);
                assertArrayEquals(bricks,w.bricks);
            }
            assertNull(w.battlefield.wave);assertFalse(w.combat.events.values().stream().anyMatch(e->e.kind()==CombatEvents.BRICK_DESTROYED));
        }
    }
    @Test void weatherCanKillWithoutKillCreditAndRespawnGetsARealShield()throws Exception {
        for(int kind:new int[]{SANDSTORM,SNOWSTORM}) {
            World w=world(kind);var t=w.tanks.get(0);t.hp=1;inside(w,t,420);w.tick=419;w.step(Map.of());
            assertEquals(0,t.hp);assertEquals(1,t.deaths);assertEquals(0,w.tanks.get(1).kills);
            var death=w.combat.events.values().stream().filter(e->e.kind()==CombatEvents.DEATH).findFirst().orElseThrow();
            assertEquals(kind==SANDSTORM?SANDSTORM_CAUSE:SNOWSTORM_CAUSE,death.causeKind());assertEquals(255,death.sourceSlot());
            while(w.tick<t.respawn)w.step(Map.of());assertEquals(100,t.hp);assertEquals(w.tick+90,t.shield);
            while(w.tick<599) {inside(w,t,w.tick+1);w.step(Map.of());}assertEquals(100,t.hp);
            inside(w,t,600);w.step(Map.of());assertEquals(98,t.hp);
        }
    }
    @Test void armorInvincibilityAndShieldBlockOnlyTheirOwnDamageTicksWithoutCatchUp()throws Exception {
        var w=world(SANDSTORM);var t=w.tanks.get(0);t.armorEndTick=450;
        inside(w,t,420);w.tick=420;w.battlefield.damage();assertEquals(99,t.hp);
        t.invincibilityEndTick=500;inside(w,t,480);w.tick=480;w.battlefield.damage();assertEquals(99,t.hp);
        t.shield=541;inside(w,t,540);w.tick=540;w.battlefield.damage();assertEquals(99,t.hp);
        inside(w,t,600);w.tick=600;w.battlefield.damage();assertEquals(97,t.hp);
        t.x=-1;w.tick=660;w.battlefield.damage();assertEquals(97,t.hp);
        inside(w,t,661);w.tick=661;w.battlefield.damage();assertEquals(97,t.hp); // entering never adds a hit
        inside(w,t,720);w.tick=720;w.battlefield.damage();assertEquals(95,t.hp);
    }
    @Test void spawnProtectionBlocksAllDamageKindsAndStillConsumesBullets()throws Exception {
        var w=world(SANDSTORM);var t=w.tanks.get(0);t.x=t.y=10;assertEquals(90,t.shield);
        for(int tick:new int[]{0,1,89})for(int kind:new int[]{0,1,2}) {
            w.tick=tick;w.damage(t,1,500,kind);assertEquals(100,t.hp);assertEquals(0,t.deaths);assertEquals(0,w.tanks.get(1).kills);
        }
        assertTrue(w.combat.events.isEmpty());
        var b=new World.Bullet();b.id=1;b.owner=1;b.x=8.5f;b.y=10;b.heading=0;b.born=-20;w.bullets.put(1,b);w.advance(b);
        assertEquals(100,t.hp);assertFalse(w.bullets.containsKey(1));
        w.tick=90;w.damage(t,1,20);assertEquals(80,t.hp);
        w.spawn(t);assertEquals(180,t.shield);w.tick=179;w.damage(t,-1,500,2);assertEquals(100,t.hp);
        w.tick=180;w.damage(t,-1,500,2);assertEquals(0,t.hp);
    }
    @Test void weatherShapeAndCauseValidationAreStrict()throws Exception {
        for(int kind:new int[]{SANDSTORM,SNOWSTORM}) {
            var wave=wave(kind);assertEquals(1,wave.rounds());assertEquals(0,wave.targetsPerRound());
            assertThrows(IllegalArgumentException.class,()->new Wave(1,kind,300,360,720,List.of()));
            assertThrows(IllegalArgumentException.class,()->new Wave(1,kind,300,390,750,List.of(0)));
            assertThrows(IllegalArgumentException.class,()->new Wave(1,kind,300,390,630,List.of()));
        }
        assertThrows(IllegalArgumentException.class,()->new Wave(1,6,300,390,750,List.of()));
    }
    @Test void snowMovementProducesBoundaryTerrainShiftAndSlowInterop()throws Exception {
        List<String> rows=new ArrayList<>();
        for(boolean wet:new boolean[]{false,true})for(int buttons:new int[]{8,40})for(boolean slow:new boolean[]{false,true})for(int stacks:new int[]{0,2})for(int tick:new int[]{389,390,420,510,630,720,749,750}) {
            GameMap m=new GameMap();m.mapId=WinterMapId;m.width=m.height=100;m.walls=new GameMap.Rect[0];m.bricks=new GameMap.Rect[0];m.water=new GameMap.Rect[wet?1:0];
            if(wet) {var r=new GameMap.Rect();r.x=r.y=1;r.w=r.h=98;m.water[0]=r;}
            float left=bandLeft(m,wave(SNOWSTORM),tick),right=bandRight(m,wave(SNOWSTORM),tick);
            for(float x:new float[]{2,20,50,80,98}) {
                var t=new World.Tank();t.x=x;t.y=20;t.energy=150;t.slowEndTick=slow?800:0;t.rocketSlowEndTick=800;t.rocketSlowStacks=stacks;
                World.movement(t,new World.Input(tick,buttons,0),m,new byte[64],tick,List.of(),wave(SNOWSTORM));
                float multiplier=(wet?1.5f:1)*(buttons==40?1.5f:1)*(slow?SlowSpeedPercent/100f:1);
                for(int hit=0;hit<stacks;hit++)multiplier*=RocketSlowSpeedPercent/100f;
                multiplier*=affecting(m,x,20,wave(SNOWSTORM),tick)?.5f:1;
                assertEquals(x+9*multiplier/30,t.x,.00001f);
                rows.add((wet?1:0)+"\t"+buttons+"\t"+(slow?1:0)+"\t"+stacks+"\t"+tick+"\t"+t.x+"\t"+t.energy+"\t"+x+"\t20\t"+Float.floatToRawIntBits(x)+"\t"+Float.floatToRawIntBits(20));
            }
        }
        Path file=Path.of("target/interop/weather-movement.tsv");Files.createDirectories(file.getParent());Files.write(file,rows);
    }
    @Test void globalWeatherHasSharedTemporalAndMapBoundsAndExportsGeometryInterop()throws Exception {
        List<String> rows=new ArrayList<>();
        for(float width:new float[]{100,120})for(int kind:new int[]{SANDSTORM,SNOWSTORM}) {
            GameMap m=new GameMap();m.width=width;m.height=80;var wave=wave(kind);
            assertEquals(width,bandWidth(m),0);
            assertEquals(0,bandLeft(m,wave,390),0);assertEquals(width,bandRight(m,wave,390),0);
            assertEquals(0,bandLeft(m,wave,750),0);
            assertTrue(affecting(m,bandLeft(m,wave,510),20,wave,510));assertFalse(affecting(m,bandRight(m,wave,510),20,wave,510));
            assertFalse(affecting(m,-.01f,20,wave,420));assertFalse(affecting(m,width,20,wave,720));
            assertEquals(kind==SNOWSTORM?.5f:1,speed(m,bandLeft(m,wave,510),20,wave,510),0);
            for(int tick:new int[]{300,389,390,391,420,510,570,630,720,749,750,751}) {
                float left=bandLeft(m,wave,tick),right=bandRight(m,wave,tick);
                for(float x:new float[]{0,.001f,left-.0001f,left,left+.0001f,right-.0001f,right,right+.0001f,width})for(float y:new float[]{-.001f,0,79.999f,80})
                    rows.add(width+"\t80\t"+kind+"\t"+tick+"\t"+left+"\t"+right+"\t"+x+"\t"+y+"\t"+(affecting(m,x,y,wave,tick)?1:0)+"\t"+Float.floatToRawIntBits(left)+"\t"+Float.floatToRawIntBits(right)+"\t"+Float.floatToRawIntBits(x)+"\t"+Float.floatToRawIntBits(y));
            }
        }
        assertEquals(1728,rows.size());
        Path file=Path.of("target/interop/weather-region.tsv");Files.createDirectories(file.getParent());Files.write(file,rows);
    }
    @Test void globalSnowChangesMovementAtImpactAndReleasesItAtEndEverywhere() {
        GameMap m=new GameMap();m.mapId=WinterMapId;m.width=m.height=100;m.walls=m.bricks=m.water=new GameMap.Rect[0];
        var w=new World(m,1);w.tick=389;w.battlefield.enabled=true;w.battlefield.nextTick=18001;w.battlefield.wave=wave(SNOWSTORM);
        var a=new World.Tank();a.slot=0;a.x=2;a.y=2;w.tanks.put(0,a);
        var b=new World.Tank();b.slot=1;b.x=97;b.y=97;w.tanks.put(1,b);
        w.step(Map.of(0,new World.Input(390,8,0),1,new World.Input(390,40,0)));
        assertEquals(2.15f,a.x,.00001f);assertEquals(97.225f,b.x,.00001f);
        w.tick=749;w.step(Map.of(0,new World.Input(750,8,0),1,new World.Input(750,40,0)));
        assertEquals(2.45f,a.x,.00001f);assertEquals(97.675f,b.x,.00001f);assertNull(w.battlefield.wave);
    }
    @Test void sandHalvesHeldFireRateForNormalAndEveryNationWithoutChangingProjectilesOrSkills() {
        for(int kind:new int[]{SANDSTORM,SNOWSTORM})for(int nation=-1;nation<8;nation++) {
            var m=new RoleplayGameplayTest().open();m.mapId=kind==SANDSTORM?DesertMapId:WinterMapId;
            var w=new World(m,1);w.add(0);var t=w.tanks.get(0);t.x=t.y=20;t.nation=nation;t.shield=1000;t.qReadyTick=1200;t.qEndTick=nation==4?750:0;
            w.battlefield.enabled=true;w.battlefield.nextTick=18001;w.battlefield.wave=wave(kind);w.tick=350;
            List<Integer> shots=new ArrayList<>();
            for(int tick=351;tick<=800;tick++) {
                w.step(Map.of(0,new World.Input(tick,16,0)));
                var born=w.bullets.values().stream().filter(b->b.born==w.tick).toList();
                if(!born.isEmpty())shots.add(tick);
                boolean volley=nation==4&&tick<750;
                for(var b:born) {assertEquals(BulletSpawnOffset+BulletStep,Math.hypot(b.x-t.x,b.y-t.y),.00001);assertEquals(100,b.speedPercent);assertEquals(volley?GermanBulletDamage:BulletDamage,b.damage);assertEquals(150,b.remainingLifetime);}
                if(!born.isEmpty())assertEquals(volley?3:1,born.size());
                assertEquals(1200,t.qReadyTick);assertEquals(nation==4?750:0,t.qEndTick);assertEquals(100,t.hp,"spawn shield remains damage-only");
                w.bullets.clear();
            }
            assertEquals(kind==SANDSTORM?List.of(351,381,441,501,561,621,681,741,771):List.of(351,381,411,441,471,501,531,561,591,621,651,681,711,741,771),shots,"kind "+kind+" nation "+nation);
        }
    }
    @Test void sandCadenceAdaptsAtImpactAndEndWithoutDelayingFreshSpawnOrCatchingUp() {
        var m=new RoleplayGameplayTest().open();m.mapId=DesertMapId;
        var w=new World(m,1);w.add(0);var t=w.tanks.get(0);t.x=t.y=20;
        w.battlefield.enabled=true;w.battlefield.nextTick=18001;w.battlefield.wave=wave(SANDSTORM);
        t.lastShotTick=360;t.cooldown=390;w.tick=389;w.step(Map.of(0,new World.Input(390,16,0)));assertEquals(360,t.lastShotTick);assertTrue(w.bullets.isEmpty());
        w.tick=418;w.step(Map.of(0,new World.Input(419,16,0)));assertEquals(360,t.lastShotTick);
        w.step(Map.of(0,new World.Input(420,16,0)));assertEquals(420,t.lastShotTick);assertEquals(450,t.cooldown);assertEquals(1,w.bullets.size());
        var copy=t.copy();assertEquals(420,copy.lastShotTick);assertEquals(450,copy.cooldown);
        w.tick=650;w.bullets.clear();w.step(Map.of(0,new World.Input(651,16,0)));assertEquals(651,t.lastShotTick);assertEquals(1,w.bullets.size(),"idle time never queues catch-up shots");
        w.tick=749;t.lastShotTick=710;t.cooldown=740;w.bullets.clear();w.step(Map.of(0,new World.Input(750,16,0)));assertEquals(750,t.lastShotTick);assertEquals(1,w.bullets.size(),"end immediately restores the normal interval");
        w.tick=419;w.battlefield.wave=wave(SANDSTORM);w.spawn(t);assertEquals(-1,t.lastShotTick);assertEquals(419,t.cooldown);assertEquals(509,t.shield);
        w.bullets.clear();w.step(Map.of(0,new World.Input(420,16,0)));assertEquals(420,t.lastShotTick);assertEquals(1,w.bullets.size(),"fresh protected spawn can shoot immediately");
        t.fireLockEndTick=500;w.tick=479;w.step(Map.of(0,new World.Input(480,16,0)));assertEquals(420,t.lastShotTick,"fire lock remains independent");
    }
    @Test void fullDeltaDroppedUpdateAndRejoinProduceWeatherInterop()throws Exception {
        List<String> rows=new ArrayList<>();var hex=HexFormat.of();
        for(int kind:new int[]{SANDSTORM,SNOWSTORM}) {
            var w=world(kind);w.tick=300;byte[] base=w.encode();rows.add(kind+"\t300\tFULL\t"+hex.formatHex(base));
            for(int tick:new int[]{389,390,419,420,480,540,600,660,720,749,750}) {
                w.tick=tick;w.battlefield.beginTick();w.battlefield.damage();byte[] current=w.encode();
                byte[] delta=DeltaCodec.encode(base,current);assertArrayEquals(current,DeltaCodec.apply(base,delta));assertArrayEquals(current,World.decode(current).encode());
                rows.add(kind+"\t"+tick+"\t"+(tick==420?"DROP":"DELTA")+"\t"+hex.formatHex(delta)+"\t"+hex.formatHex(current));
                if(tick==480)rows.add(kind+"\t"+tick+"\tREJOIN\t"+hex.formatHex(current));
                if(tick!=420)base=current; // next delta repairs the deliberately dropped update
            }
        }
        Path file=Path.of("target/interop/weather-state.tsv");Files.createDirectories(file.getParent());Files.write(file,rows);
    }
}
