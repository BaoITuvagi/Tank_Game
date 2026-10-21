package tank;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
import tank.Protocol.Error;
class RoleplayGameplayTest {
    GameMap open() {
        GameMap m=new GameMap();m.width=m.height=60;m.walls=new GameMap.Rect[0];m.bricks=new GameMap.Rect[0];m.water=new GameMap.Rect[0];
        m.spawns=new GameMap.Point[4];for(int i=0;i<4;i++){var p=new GameMap.Point();p.x=i%2==0?10:30;p.y=i<2?10:30;m.spawns[i]=p;}return m;
    }
    GameMap.Rect rect(float x,float y,float width,float height,int kind) {var r=new GameMap.Rect();r.x=x;r.y=y;r.w=width;r.h=height;r.kind=kind;return r;}
    World world(int hp) {var w=new World(open(),1,hp);w.add(0);w.add(1);w.tick=100;w.tanks.get(0).x=w.tanks.get(0).y=10;w.tanks.get(1).x=14;w.tanks.get(1).y=10;return w;}
    void until(World w,int tick) {while(w.tick<tick)w.step(Map.of());}
    @Test void everySkillUsesTheAgreedCooldownAtEveryHp() {
        assertArrayEquals(new int[]{900,977,900,900,900,1029,1029,1029},RoleplayContent.NationCooldowns);
        for(int hp:new int[]{1,100,500})for(int nation=0;nation<8;nation++) {
            World w=world(hp);var t=w.tanks.get(0);t.nation=nation;
            assertEquals(Error.OK,w.useSkill(0,0));
            if(nation==1) {assertEquals(0,t.qReadyTick);assertEquals(Error.OK,w.useSkill(0,0));}
            assertEquals(100+RoleplayContent.NationCooldowns[nation],t.qReadyTick);
            assertEquals(Error.COOLDOWN,w.useSkill(0,0));
        }
    }
    @Test void unsupportedSkillSlotsAreRejectedWithoutChangingState() {
        World w=world(100);w.tanks.get(0).nation=0;byte[] before=w.encode();
        for(int slot:new int[]{-1,1,2,255})assertEquals(Error.INVALID_STATE,w.useSkill(0,slot));
        assertArrayEquals(before,w.encode());
    }
    @Test void germanVolleyHasThreeFrozenDamageProjectilesAndNormalCadenceBeyondOldCap() {
        World w=world(500);var t=w.tanks.get(0);t.nation=4;t.cooldown=150;w.useSkill(0,0);assertEquals(150,t.cooldown);
        for(int i=0;i<32;i++) {var b=new World.Bullet();b.id=w.nextBullet++;b.x=45;b.y=45;b.born=149;w.bullets.put(b.id,b);}
        w.tick=149;w.step(Map.of(0,new World.Input(150,16,0)));
        assertEquals(35,w.bullets.size());assertEquals(180,t.cooldown);
        var volley=w.bullets.values().stream().filter(b->b.born==150).toList();assertEquals(3,volley.size());
        assertEquals(Set.of(0,1200,34800),new HashSet<>(volley.stream().map(b->b.heading).toList()));
        for(var b:volley) {assertEquals(15,b.damage);assertEquals(100,b.speedPercent);}
        t.qEndTick=150;w.step(Map.of());for(var b:volley)assertEquals(15,b.damage);
        t.cooldown=152;w.step(Map.of(0,new World.Input(152,16,0)));assertEquals(20,w.bullets.lastEntry().getValue().damage);
    }
    @Test void damageClampsAndOnlyCreditsOneEnemyKill() {
        World w=world(100);var t=w.tanks.get(1);t.hp=5;w.damage(t,0,20);w.damage(t,0,20);
        assertEquals(0,t.hp);assertEquals(1,t.deaths);assertEquals(1,w.tanks.get(0).kills);
        w.damage(w.tanks.get(0),0,500);assertEquals(1,w.tanks.get(0).kills);
    }
    @Test void britishConfusionReversesDiagonalAndLocksOnlyTurret() {
        World w=world(500);var t=w.tanks.get(0);var target=w.tanks.get(1);t.nation=2;target.turret=9000;
        w.useSkill(0,0);float x=target.x,y=target.y;
        w.step(Map.of(1,new World.Input(101,25,18000)));assertTrue(target.x<x);assertTrue(target.y<y);assertEquals(9000,target.turret);
        assertEquals(9000,w.bullets.firstEntry().getValue().heading);w.damage(t,1,500);assertEquals(400,target.confusionEndTick);
        w.tick=399;w.step(Map.of(1,new World.Input(400,8,18000)));assertEquals(9400,target.turret);
    }
    @Test void franceUsesIndependentDeadlinesAndDeathClearsOnlyTheVictim() {
        World w=world(100);var t=w.tanks.get(0);var target=w.tanks.get(1);t.nation=3;w.useSkill(0,0);
        assertTrue(t.fogEndTick<=100);assertEquals(400,target.fogEndTick);
        w.tick=175;assertTrue(w.tick>=t.fogEndTick);assertTrue(w.tick<target.fogEndTick);
        w.damage(t,1,100);assertEquals(400,target.fogEndTick);until(w,t.respawn);assertTrue(t.fogEndTick<=w.tick);assertEquals(1000,t.qReadyTick);
    }
    @Test void chinaAddsTenSecondsToRemainingCooldownAndKeepsStartedSkills() {
        for(int nation=0;nation<8;nation++)for(int prior:new int[]{0,99,100,105,900}) {
            World w=world(500);var t=w.tanks.get(0);var target=w.tanks.get(1);t.nation=7;target.nation=nation;target.qReadyTick=prior;target.qEndTick=200;
            w.useSkill(0,0);assertEquals(Math.max(100,prior)+300,target.qReadyTick);assertEquals(400,target.fireLockEndTick);assertEquals(200,target.qEndTick);
            int ready=target.qReadyTick;until(w,399);assertEquals(ready,target.qReadyTick);
            target.cooldown=0;w.step(Map.of(1,new World.Input(400,16,0)));assertFalse(w.bullets.isEmpty());
            w.damage(target,0,500);int respawn=target.respawn;until(w,respawn);assertEquals(ready,target.qReadyTick);assertTrue(target.fireLockEndTick<=w.tick);
        }
    }
    @Test void chinaPenaltyStacksPerCastWithoutShorteningCooldownOrAffectingDeadTargets() {
        World w=world(500);var caller=w.tanks.get(0);caller.nation=7;var target=w.tanks.get(1);target.nation=5;target.qReadyTick=1500;
        w.useSkill(0,0);assertEquals(1800,target.qReadyTick);assertEquals(1129,caller.qReadyTick);
        w.tick=200;caller.qReadyTick=200;w.useSkill(0,0);assertEquals(2100,target.qReadyTick);assertEquals(500,target.fireLockEndTick);
        w.damage(target,0,500);caller.qReadyTick=200;w.useSkill(0,0);assertEquals(2100,target.qReadyTick);
        w.spawn(target);target.qReadyTick=65400;caller.qReadyTick=200;w.useSkill(0,0);assertEquals(65535,target.qReadyTick);assertDoesNotThrow(w::encode);
    }
    @Test void katyushaClampsEveryEdgeAutoLocksAndSchedulesAllNineInsideCircle() {
        World w=world(500);var t=w.tanks.get(0);t.nation=1;w.useSkill(0,0);
        for(float x:new float[]{-100,0,60,100})for(float y:new float[]{-100,0,60,100}) {
            w.skillAim(0,x,y);assertTrue(t.aimX>=5.1f&&t.aimX<=54.9f);assertTrue(t.aimY>=5.1f&&t.aimY<=54.9f);
        }
        until(w,250);assertEquals(0,t.qStage);assertEquals(1227,t.qReadyTick);
        var cast=w.skills.casts.getFirst();assertEquals(9,cast.zones.size());assertEquals(274,cast.zones.getFirst().end);assertEquals(364,cast.zones.getLast().end);
        for(var e:cast.zones) {assertTrue(Math.hypot(e.x-t.aimX,e.y-t.aimY)<=5.10001);assertTrue(e.x>=0&&e.y>=0&&e.x<=60&&e.y<=60);}
    }
    @Test void katyushaCapIsPerCastIncludesSelfAndDestroysBricks() {
        World w=world(500);var t=w.tanks.get(0);var target=w.tanks.get(1);t.nation=1;target.x=t.x;
        w.useSkill(0,0);w.useSkill(0,0);var cast=w.skills.casts.getFirst();
        for(var e:cast.zones) {e.x=t.x;e.y=t.y;}
        w.map.bricks=new GameMap.Rect[]{rect(10,9,.1f,2,1)};w.bricks[0]=5;
        until(w,214);assertEquals(464,t.hp);assertEquals(464,target.hp);assertEquals(0,w.bricks[0]);assertEquals(0,t.kills);
    }
    @Test void sovietAimCanFinishAfterChinaAndCancelledAimKeepsChinaCooldown() {
        for(boolean dies:new boolean[]{false,true}) {
            World w=world(500);var t=w.tanks.get(0);var chinese=w.tanks.get(1);t.nation=1;chinese.nation=7;w.useSkill(0,0);w.useSkill(1,0);
            assertEquals(400,t.qReadyTick);w.tick=105;
            if(dies) {w.damage(t,1,500);assertEquals(0,t.qStage);assertEquals(400,t.qReadyTick);}
            else {assertEquals(Error.OK,w.useSkill(0,0));assertEquals(1377,t.qReadyTick);}
        }
    }
    @Test void bombingUsesFixedOverlappingZonesLifeIdentityAndCallerDeath() {
        World w=world(500);w.add(2);var t=w.tanks.get(0);t.nation=0;var a=w.tanks.get(1);var b=w.tanks.get(2);a.x=b.x=20;a.y=b.y=20;
        w.useSkill(0,0);w.damage(t,0,500);w.damage(b,0,500);w.spawn(b);b.x=b.y=20;
        a.x=25.8f;a.body=4500; // corner overlap although center is outside radius 5.1
        w.map.bricks=new GameMap.Rect[]{rect(21,0,1,60,1)};w.bricks[0]=5;
        until(w,145);assertEquals(475,a.hp);assertEquals(175,a.slowEndTick);assertEquals(500,b.hp);assertTrue(b.slowEndTick<=w.tick);assertEquals(0,w.bricks[0]);
        World dodge=world(100);dodge.tanks.get(0).nation=0;dodge.useSkill(0,0);dodge.tanks.get(1).x=40;until(dodge,145);assertEquals(100,dodge.tanks.get(1).hp);assertTrue(dodge.tanks.get(1).slowEndTick<=145);
    }
    @Test void bombingSlowsEachSurvivingHitOnceForOneSecondAndDeathClearsIt() {
        World w=world(500);w.add(2);var caller=w.tanks.get(0);caller.nation=0;var a=w.tanks.get(1);var b=w.tanks.get(2);a.x=b.x=20;a.y=b.y=20;
        w.useSkill(0,0);until(w,144);assertTrue(a.slowEndTick<=w.tick);until(w,145);
        assertEquals(475,a.hp);assertEquals(475,b.hp);assertEquals(175,a.slowEndTick);assertEquals(175,b.slowEndTick);assertTrue(caller.slowEndTick<=w.tick);
        float x=a.x;w.step(Map.of(1,new World.Input(146,8,0)));assertEquals(x+.15f,a.x,.00001);
        until(w,173);x=a.x;w.step(Map.of(1,new World.Input(174,8,0)));assertEquals(x+.15f,a.x,.00001);
        x=a.x;w.step(Map.of(1,new World.Input(175,8,0)));assertEquals(x+.3f,a.x,.00001);assertEquals(475,a.hp);assertEquals(175,a.slowEndTick);
        World lethal=world(25);lethal.tanks.get(0).nation=0;lethal.useSkill(0,0);until(lethal,145);var killed=lethal.tanks.get(1);
        assertEquals(0,killed.hp);assertTrue(killed.slowEndTick<=145);until(lethal,killed.respawn);assertTrue(killed.slowEndTick<=lethal.tick);
    }
    @Test void japanKillsCornerOverlapsThroughWallsAndCancelsOnEarlyDeath() {
        World w=world(500);var t=w.tanks.get(0);t.nation=6;var target=w.tanks.get(1);target.x=19.5f;target.body=4500;
        w.map.walls=new GameMap.Rect[]{rect(16,0,1,60,0)};w.useSkill(0,0);until(w,249);assertEquals(500,target.hp);until(w,250);
        assertEquals(0,target.hp);assertEquals(0,t.hp);assertEquals(1,t.kills);assertEquals(1129,t.qReadyTick);
        World cancel=world(500);cancel.tanks.get(0).nation=6;cancel.useSkill(0,0);cancel.damage(cancel.tanks.get(0),1,500);until(cancel,250);assertEquals(500,cancel.tanks.get(1).hp);
    }
    @Test void italianBeamUsesFiringAngleAndFullWidthWithMultipleTargets() {
        World w=world(500);w.add(2);var t=w.tanks.get(0);t.nation=5;t.turret=0;w.useSkill(0,0);
        var a=w.tanks.get(1);a.x=10;a.y=20;var b=w.tanks.get(2);b.x=10;b.y=30;
        w.map.bricks=new GameMap.Rect[]{rect(0,15,60,1,1)};w.bricks[0]=5;t.turret=9000;
        assertEquals(160,t.qEndTick);until(w,159);assertEquals(500,a.hp);assertEquals(5,w.bricks[0]);until(w,160);
        assertEquals(0,a.hp);assertEquals(0,b.hp);assertEquals(500,t.hp);assertEquals(2,t.kills);assertEquals(0,w.bricks[0]);
        World geometry=world(500);var near=geometry.tanks.get(1);near.x=25;near.y=11.275f;
        assertTrue(geometry.skills.beam(near,11.05f,10,0));near.y+=.002f;assertFalse(geometry.skills.beam(near,11.05f,10,0));
        World cancel=world(500);cancel.tanks.get(0).nation=5;cancel.useSkill(0,0);cancel.damage(cancel.tanks.get(0),1,500);until(cancel,160);assertEquals(500,cancel.tanks.get(1).hp);
    }
    @Test void reducedJapaneseBlastKillsAndBreaksBricksOnlyInsideNewRadius() {
        World w=world(500);w.add(2);var caller=w.tanks.get(0);caller.nation=6;caller.x=caller.y=20;
        var edge=w.tanks.get(1);edge.x=30.41249f;edge.y=20;edge.body=0;
        var outside=w.tanks.get(2);outside.x=30.4325f;outside.y=20;outside.body=0;
        var brick=rect(20,29.5625f,1,1,1);var missed=rect(20,29.5825f,1,1,1);missed.id=1;
        w.map.bricks=new GameMap.Rect[]{brick,missed};w.bricks[0]=w.bricks[1]=5;
        w.map.walls=new GameMap.Rect[]{rect(25,0,1,60,0)};
        w.useSkill(0,0);until(w,249);assertEquals(500,edge.hp);assertEquals(5,w.bricks[0]);until(w,250);
        assertEquals(0,edge.hp);assertEquals(500,outside.hp);assertEquals(0,w.bricks[0]);assertEquals(5,w.bricks[1]);
        assertEquals(0,caller.hp);assertEquals(1,caller.kills);assertEquals(1129,caller.qReadyTick);
        var blast=w.skills.effects.stream().filter(e->e.kind==NationSkills.KAMIKAZE).findFirst().orElseThrow();assertEquals(9.5625f,blast.radius);
    }
    @Test void circleUsesRotatedHitboxNotItsCenter() {
        var t=new World.Tank();t.x=4.5f;t.body=4500;assertTrue(NationSkills.circle(t,0,0,3.4f));t.x=4.7f;assertFalse(NationSkills.circle(t,0,0,3.4f));
    }
    @Test void damagingSkillsDestroyOnlyIntersectingBricksAtImpactAndNeverFromAfterglows() {
        for(int nation:new int[]{0,1,5,6}) {
            World w=world(500);var caller=w.tanks.get(0);caller.nation=nation;caller.x=caller.y=20;
            var target=w.tanks.get(1);target.x=target.y=20;
            float radius=nation==0?GameRules.BombRadius:nation==1?GameRules.RocketRadius:GameRules.KamikazeRadius;
            GameMap.Rect edge=nation==5?rect(30,20+GameRules.BeamWidth/2,1,1,1):rect(20+radius,19.5f,1,1,1);
            GameMap.Rect outside=nation==5?rect(30,20+GameRules.BeamWidth/2+.01f,1,1,1):rect(20+radius+.01f,19.5f,1,1,1);outside.id=1;
            w.map.bricks=new GameMap.Rect[]{edge,outside};w.bricks[0]=5;w.bricks[1]=4;
            w.map.walls=new GameMap.Rect[]{rect(22,0,1,60,0)};
            w.useSkill(0,0);int impact=100+(nation==0?GameRules.BombDelayTicks:nation==1?GameRules.KatyushaDelayTicks:nation==5?GameRules.BeamChargeTicks:GameRules.KamikazeTicks);
            if(nation==1) {
                w.useSkill(0,0);var zones=w.skills.casts.getFirst().zones;
                for(var e:zones){e.x=e.y=20;}
                zones.subList(1,zones.size()).clear();
            }
            until(w,impact-1);assertEquals(5,w.bricks[0]);assertEquals(4,w.bricks[1]);
            until(w,impact);assertEquals(0,w.bricks[0],"nation "+nation);assertEquals(4,w.bricks[1]);assertEquals(1,w.map.walls.length);
            w.bricks[0]=5;until(w,impact+24);assertEquals(5,w.bricks[0],"afterglow repeated damage");
        }
    }
    @Test void beamReachesMapEdgeInEveryDirectionAndUsesExactRectangleIntersection() {
        World w=world(500);
        for(int angle:new int[]{0,4500,9000,13500,18000,22500,27000,31500}) {
            double a=Math.toRadians(angle/100.0),c=Math.cos(a),s=Math.sin(a),distance=30/Math.max(Math.abs(c),Math.abs(s));
            float x=(float)(30+c*(distance-.5)),y=(float)(30+s*(distance-.5));
            assertTrue(w.skills.beamRect(x,y,.2f,.2f,0,30,30,angle),"short beam "+angle);
            assertFalse(w.skills.beamRect(x-(float)s*2,y+(float)c*2,.2f,.2f,0,30,30,angle),"wide beam "+angle);
            assertFalse(w.skills.beamRect(30-(float)c*3,30-(float)s*3,.2f,.2f,0,30,30,angle));
        }
        // A circle's bounding box is not its damage region.
        w.map.bricks=new GameMap.Rect[]{rect(23.3f,23.3f,1,1,1)};w.bricks[0]=5;
        w.skills.breakCircle(20,20,3.4f);assertEquals(5,w.bricks[0]);
    }
    @Test void brickDestructionFullDeltaAndRecoveryUseTheAcknowledgedBase() throws Exception {
        World w=world(500);w.map.bricks=new GameMap.Rect[]{rect(20,20,1,1,1),rect(30,30,1,1,1)};w.map.bricks[1].id=1;w.bricks[0]=w.bricks[1]=5;
        byte[] base=w.encode();w.skills.breakCircle(20,20,3.4f);byte[] first=w.encode();
        w.skills.breakCircle(30,30,3.4f);byte[] after=w.encode(),repair=DeltaCodec.encode(base,after);
        assertArrayEquals(after,DeltaCodec.apply(base,repair));assertArrayEquals(after,World.decode(after).encode());
        var hex=HexFormat.of();Path path=Path.of("target/interop/skill-bricks.tsv");Files.createDirectories(path.getParent());
        Files.write(path,List.of("BASE\t"+hex.formatHex(base),"DROPPED\t"+hex.formatHex(DeltaCodec.encode(base,first)),"REPAIR\t"+hex.formatHex(repair),"REJOIN\t"+hex.formatHex(after)));
    }
    @Test void matchCleanupStopsScheduledDamageAndTemporaryEffects() {
        World w=world(100);var t=w.tanks.get(0);t.nation=0;w.useSkill(0,0);w.skills.clear();until(w,145);assertEquals(100,w.tanks.get(1).hp);assertTrue(w.skills.effects.isEmpty());assertTrue(w.skills.casts.isEmpty());
    }
    @Test void roleplayMovementTraceMatchesPredictionAcrossDeadlineAndTerrain() throws Exception {
        GameMap map=open();map.water=new GameMap.Rect[]{rect(24,12,8,8,2)};List<String> rows=new ArrayList<>();
        for(int nation:new int[]{0,5})for(int tick:new int[]{99,199,200})for(int angle:new int[]{4500,9000,18000})for(boolean wet:new boolean[]{false,true})for(int buttons:new int[]{8,40}) {
            var t=new World.Tank();t.nation=nation;t.x=wet?28:10;t.y=wet?15:10;t.qEndTick=200;t.slowEndTick=200;t.confusionEndTick=200;t.fireLockEndTick=180;t.fogEndTick=180;t.energy=77;t.energyRecovery=8;
            var before=new Protocol.Writer();World.writeTank(before,t);World.movement(t,new World.Input(tick,buttons,angle),map,new byte[64]);var after=new Protocol.Writer();World.writeTank(after,t);
            rows.add(nation+"\t"+tick+"\t"+buttons+"\t"+angle+"\t"+HexFormat.of().formatHex(before.done())+"\t"+HexFormat.of().formatHex(after.done()));
        }
        Path path=Path.of("target/interop/roleplay-movement.tsv");Files.createDirectories(path.getParent());Files.write(path,rows);assertEquals(72,rows.size());
    }
    @Test void bombSlowAndSabotageDeadlinesSurviveDeltaRepairAndRejoin() throws Exception {
        World w=world(500);var american=w.tanks.get(0);american.nation=0;var chinese=w.tanks.get(1);chinese.nation=7;
        byte[] base=w.encode();w.useSkill(0,0);until(w,145);assertEquals(175,chinese.slowEndTick);w.useSkill(1,0);assertEquals(1300,american.qReadyTick);
        byte[] after=w.encode(),repair=DeltaCodec.encode(base,after);assertArrayEquals(after,DeltaCodec.apply(base,repair));assertArrayEquals(after,World.decode(after).encode());
        var hex=HexFormat.of();Path path=Path.of("target/interop/skill-debuffs.tsv");Files.createDirectories(path.getParent());
        Files.write(path,List.of("BASE\t"+hex.formatHex(base),"REPAIR\t"+hex.formatHex(repair),"REJOIN\t"+hex.formatHex(after)));
    }
}
