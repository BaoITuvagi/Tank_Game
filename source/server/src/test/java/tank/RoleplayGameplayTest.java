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
            w.skillAim(0,x,y);assertTrue(t.aimX>=GameRules.KatyushaRadius&&t.aimX<=60-GameRules.KatyushaRadius);assertTrue(t.aimY>=GameRules.KatyushaRadius&&t.aimY<=60-GameRules.KatyushaRadius);
        }
        until(w,250);assertEquals(0,t.qStage);assertEquals(1227,t.qReadyTick);
        var cast=w.skills.casts.getFirst();assertEquals(9,cast.zones.size());assertEquals(274,cast.zones.getFirst().end);assertEquals(364,cast.zones.getLast().end);
        for(var e:cast.zones) {assertTrue(Math.hypot(e.x-t.aimX,e.y-t.aimY)<=GameRules.KatyushaRadius+.00001);assertTrue(e.x>=0&&e.y>=0&&e.x<=60&&e.y<=60);}
    }
    @Test void katyushaAreaHalvesCurrentSpeedAcrossTerrainSprintAndBombSlow() throws Exception {
        List<String> rows=new ArrayList<>();
        for(boolean wet:new boolean[]{false,true})for(boolean bomb:new boolean[]{false,true})for(int buttons:new int[]{8,40})for(int tick:new int[]{99,100,199,200}) {
            GameMap map=open();if(wet)map.water=new GameMap.Rect[]{rect(0,0,60,60,2)};
            World w=new World(map,1);w.add(0);var t=w.tanks.get(0);t.x=t.y=20;t.slowEndTick=bomb?300:0;
            w.skills.effect(NationSkills.AREA,0,100,200,20,20,GameRules.KatyushaRadius,0);
            w.skills.effect(NationSkills.AREA,1,100,200,20,20,GameRules.KatyushaRadius,0);
            byte[] before=w.encode();World.movement(t,new World.Input(tick,buttons,0),map,w.bricks,tick,w.skills.effects);
            double expected=.3*(wet?.5:1)*(buttons==40?1.5:1)*(bomb?.7:1)*(tick>=100&&tick<200?.5:1);
            assertEquals(expected,t.x-20,.00001);assertEquals(buttons==40?149:150,t.energy);
            var after=new Protocol.Writer();World.writeTank(after,t);
            rows.add(wet+"\t"+tick+"\t"+buttons+"\t"+HexFormat.of().formatHex(before)+"\t"+HexFormat.of().formatHex(after.done()));
        }
        Path path=Path.of("target/interop/katyusha-movement.tsv");Files.createDirectories(path.getParent());Files.write(path,rows);
    }
    World targetedKatyusha() {
        World w=world(500);var caller=w.tanks.get(0);caller.nation=1;caller.x=caller.y=5;
        w.tanks.get(1).x=26;w.tanks.get(1).y=30;
        w.skillAim(0,30,30);w.useSkill(0,0);w.useSkill(0,0);return w;
    }
    @Test void eachRocketHitMultipliesSpeedEvenAfterTheDamageCapAndAllStacksExpireWithTheArea() {
        World w=targetedKatyusha();var cast=w.skills.casts.getFirst();var rockets=new ArrayList<>(cast.zones);var target=w.tanks.get(1);
        for(int hit=1;hit<9;hit++) {
            until(w,rockets.get(hit-1).end);
            assertEquals(hit,target.rocketSlowStacks);assertEquals(cast.area.end,target.rocketSlowEndTick);
            assertEquals(500-Math.min(hit*GameRules.RocketDamage,GameRules.KatyushaDamageCap),target.hp);
            var moving=target.copy();float x=moving.x;
            World.movement(moving,new World.Input(w.tick+1,8,0),w.map,w.bricks,w.tick+1,w.skills.effects);
            assertEquals(.3*.5*Math.pow(.9,hit),moving.x-x,.00001);
        }
        until(w,cast.area.end);assertEquals(0,target.rocketSlowStacks);
        var moving=target.copy();float x=moving.x;
        World.movement(moving,new World.Input(w.tick,8,0),w.map,w.bricks,w.tick,w.skills.effects);
        assertEquals(.3,moving.x-x,.00001);
        until(w,cast.area.end+24);assertEquals(0,target.rocketSlowStacks,"Afterglows must not reapply hit slow.");
    }
    @Test void rocketHitSlowPersistsOutsideTheAreaAndPurificationAndDeathClearOnlyTheCurrentLife() {
        World w=targetedKatyusha();var rockets=new ArrayList<>(w.skills.casts.getFirst().zones);var target=w.tanks.get(1);
        until(w,rockets.getFirst().end);target.x=target.y=50;
        var moving=target.copy();World.movement(moving,new World.Input(w.tick+1,8,0),w.map,w.bricks,w.tick+1,w.skills.effects);
        assertEquals(.3*.9,moving.x-50,.00001);
        w.items.apply(target,WorldItems.PURIFICATION);assertEquals(0,target.rocketSlowStacks);
        target.x=26;target.y=30;until(w,rockets.get(1).end);assertEquals(1,target.rocketSlowStacks,"New hits must not restore cleansed stacks.");
        until(w,rockets.get(2).end);assertEquals(2,target.rocketSlowStacks);
        int life=target.life;w.damage(target,0,500,2);assertEquals(0,target.rocketSlowStacks);
        w.spawn(target);assertNotEquals(life,target.life);assertEquals(0,target.rocketSlowStacks);
        target.x=target.y=50;until(w,rockets.getLast().end);assertEquals(0,target.rocketSlowStacks);
    }
    @Test void rocketSlowMatchesTerrainSprintBombAndAreaFactorsUntilTheExactDeadline() throws Exception {
        List<String> rows=new ArrayList<>();
        for(boolean wet:new boolean[]{false,true})for(boolean bomb:new boolean[]{false,true})for(boolean inside:new boolean[]{false,true})for(int hits:new int[]{0,1,2,8})for(int tick:new int[]{199,200})for(int buttons:new int[]{8,40}) {
            GameMap map=open();if(wet)map.water=new GameMap.Rect[]{rect(0,0,60,60,2)};
            World w=new World(map,1,500);w.add(0);var t=w.tanks.get(0);t.x=t.y=inside?20:50;t.energy=77;
            t.slowEndTick=bomb?300:0;t.rocketSlowEndTick=200;t.rocketSlowStacks=hits;
            w.skills.effect(NationSkills.AREA,1,100,200,20,20,GameRules.KatyushaRadius,0);
            byte[] before=w.encode();float x=t.x;
            World.movement(t,new World.Input(tick,buttons,0),map,w.bricks,tick,w.skills.effects);
            double expected=.3*(wet?.5:1)*(buttons==40?1.5:1)*(bomb?.7:1)*(inside&&tick<200?.5:1)*(tick<200?Math.pow(.9,hits):1);
            assertEquals(expected,t.x-x,.00001);
            var after=new Protocol.Writer();World.writeTank(after,t);
            rows.add(tick+"\t"+buttons+"\t"+HexFormat.of().formatHex(before)+"\t"+HexFormat.of().formatHex(after.done()));
        }
        Path path=Path.of("target/interop/rocket-slow-movement.tsv");Files.createDirectories(path.getParent());Files.write(path,rows);
    }
    @Test void rocketSlowStacksRepairAfterDroppedSnapshotsAndExpireInFullState() throws Exception {
        World w=targetedKatyusha();var cast=w.skills.casts.getFirst();var rockets=new ArrayList<>(cast.zones);var target=w.tanks.get(1);
        until(w,rockets.getFirst().end);byte[] base=w.encode();assertEquals(1,target.rocketSlowStacks);
        until(w,rockets.get(1).end);byte[] dropped=DeltaCodec.encode(base,w.encode());
        until(w,rockets.get(3).end);byte[] current=w.encode(),delta=DeltaCodec.encode(base,current);assertEquals(4,target.rocketSlowStacks);
        assertArrayEquals(current,DeltaCodec.apply(base,delta));assertEquals(4,World.decode(current).tanks.get(1).rocketSlowStacks);
        until(w,cast.area.end);assertEquals(0,target.rocketSlowStacks);
        var hex=HexFormat.of();Path path=Path.of("target/interop/rocket-slow-state.tsv");Files.createDirectories(path.getParent());
        Files.write(path,List.of("BASE\t"+hex.formatHex(base),"DROPPED\t"+hex.formatHex(dropped),"DELTA\t"+hex.formatHex(delta),"REJOIN\t"+hex.formatHex(current),"EXPIRED\t"+hex.formatHex(w.encode())));
    }
    @Test void katyushaLocksEachRocketAtLaunchAndRepairsFrozenPointsAfterPacketLoss() throws Exception {
        World w=targetedKatyusha();var cast=w.skills.casts.getFirst();var target=w.tanks.get(1);
        var first=cast.zones.get(0);var second=cast.zones.get(1);byte[] base=w.encode();
        target.x=28;until(w,first.start-1);assertFalse(cast.targets.containsKey(first.id));
        until(w,first.start);assertEquals(28,first.x);assertEquals(30,first.y);assertEquals(1,cast.targets.get(first.id));
        byte[] dropped=w.encode();target.x=34;
        until(w,second.start);assertEquals(28,first.x);assertEquals(34,second.x);assertEquals(30,second.y);
        byte[] current=w.encode(),delta=DeltaCodec.encode(base,current);assertArrayEquals(current,DeltaCodec.apply(base,delta));
        var hex=HexFormat.of();Path path=Path.of("target/interop/katyusha-targeting.tsv");Files.createDirectories(path.getParent());
        Files.write(path,List.of("BASE\t"+hex.formatHex(base),"DROPPED\t"+hex.formatHex(DeltaCodec.encode(base,dropped)),"DELTA\t"+hex.formatHex(delta),"REJOIN\t"+hex.formatHex(current)));
        until(w,first.end);assertEquals(28,first.x);assertEquals(500,target.hp,"Moving away dodges the locked point.");
        until(w,second.end);assertEquals(488,target.hp,"The next rocket uses the position sampled at its own launch.");
    }
    @Test void katyushaFallsBackToRandomForEveryRemainingRocketWhenEnemyLeavesEvenAfterCallerDeath() {
        World w=targetedKatyusha();var cast=w.skills.casts.getFirst();var rockets=new ArrayList<>(cast.zones);var target=w.tanks.get(1);
        target.x=30;until(w,rockets.getFirst().start);assertEquals(30,rockets.getFirst().x);
        target.x=target.y=55;w.damage(w.tanks.get(0),1,500);
        for(var e:rockets.subList(1,rockets.size())) {
            float x=e.x,y=e.y;until(w,e.start);
            assertEquals(-1,cast.targets.get(e.id));assertEquals(x,e.x);assertEquals(y,e.y);
            assertTrue(Math.hypot(e.x-30,e.y-30)<=GameRules.KatyushaRadius+.00001);
        }
        until(w,rockets.getLast().end);assertEquals(9,cast.targets.size());assertFalse(w.skills.casts.contains(cast));assertEquals(500,target.hp);
    }
    @Test void katyushaSamplesRandomLivingEnemiesAndCanAcquireNewArrivalsOnlyOnLaterLaunches() {
        World w=targetedKatyusha();w.add(2);w.add(3);var cast=w.skills.casts.getFirst();
        w.tanks.get(0).x=w.tanks.get(0).y=30;
        w.tanks.get(2).x=34;w.tanks.get(2).y=30;
        w.tanks.get(3).x=w.tanks.get(3).y=30;w.tanks.get(3).hp=0;w.tanks.get(3).respawn=1000;
        w.random.setSeed(1234);var expected=new Random(1234);
        for(var e:new ArrayList<>(cast.zones)) {
            until(w,e.start);int slot=1+expected.nextInt(2);
            assertEquals(slot,cast.targets.get(e.id));assertEquals(w.tanks.get(slot).x,e.x);assertEquals(30,e.y);
        }
        World empty=targetedKatyusha();var emptyCast=empty.skills.casts.getFirst();var first=emptyCast.zones.getFirst();var second=emptyCast.zones.get(1);
        var target=empty.tanks.get(1);target.x=target.y=55;until(empty,first.start);assertEquals(-1,emptyCast.targets.get(first.id));
        float x=first.x,y=first.y;target.x=target.y=30;until(empty,second.start);
        assertEquals(x,first.x);assertEquals(y,first.y);assertEquals(1,emptyCast.targets.get(second.id));assertEquals(30,second.x);assertEquals(30,second.y);
    }
    @Test void katyushaSlowRequiresLockedActiveAreaAndStopsOutsideOrAfterDeath() {
        World w=world(500);var caller=w.tanks.get(0);caller.nation=1;caller.x=caller.y=20;
        w.skillAim(0,20,20);w.useSkill(0,0);
        assertEquals(0,NationSkills.katyushaSlowEnd(caller,100,w.skills.effects));
        w.useSkill(0,0);int end=caller.qEndTick;
        assertEquals(end,NationSkills.katyushaSlowEnd(caller,100,w.skills.effects));
        w.step(Map.of(0,new World.Input(999,8,0)));assertEquals(20.15,caller.x,.00001);
        var target=w.tanks.get(1);target.y=20;target.x=20+GameRules.KatyushaRadius+GameRules.TankHalfSize-.01f;
        assertEquals(end,NationSkills.katyushaSlowEnd(target,101,w.skills.effects));
        target.x+=.02f;assertEquals(0,NationSkills.katyushaSlowEnd(target,101,w.skills.effects));
        caller.hp=0;assertEquals(0,NationSkills.katyushaSlowEnd(caller,101,w.skills.effects));
        caller.hp=500;caller.x=caller.y=20;assertEquals(0,NationSkills.katyushaSlowEnd(caller,end,w.skills.effects));
    }
    @Test void katyushaCapIsPerCastIncludesSelfAndDestroysBricks() {
        World w=world(500);var t=w.tanks.get(0);var target=w.tanks.get(1);t.nation=1;target.x=t.x;
        w.useSkill(0,0);w.useSkill(0,0);var cast=w.skills.casts.getFirst();
        for(var e:cast.zones) {e.x=t.x;e.y=t.y;}
        w.map.bricks=new GameMap.Rect[]{rect(10,9,.1f,2,1)};w.bricks[0]=3;
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
    @Test void bombingUsesTrackingOverlappingZonesLifeIdentityAndCallerDeath() {
        World w=world(500);w.add(2);var t=w.tanks.get(0);t.nation=0;var a=w.tanks.get(1);var b=w.tanks.get(2);a.x=b.x=20;a.y=b.y=20;
        w.useSkill(0,0);w.damage(t,0,500);w.damage(b,0,500);w.spawn(b);b.x=b.y=20;
        a.x=20+GameRules.BombRadius+.7f;a.body=4500; // corner overlap although center is outside the bomb radius
        w.map.bricks=new GameMap.Rect[]{rect(21,0,1,60,1)};w.bricks[0]=3;
        until(w,190);assertEquals(475,a.hp);assertTrue(a.slowEndTick<=190);assertEquals(500,b.hp);assertTrue(b.slowEndTick<=w.tick);assertEquals(0,w.bricks[0]);
        World dodge=world(100);dodge.tanks.get(0).nation=0;dodge.useSkill(0,0);dodge.tanks.get(1).x=55;until(dodge,190);assertEquals(100,dodge.tanks.get(1).hp);assertTrue(dodge.tanks.get(1).slowEndTick<=190);
    }
    @Test void bombingSlowsImmediatelyForThreeSecondsWithoutRefreshingAtImpact() {
        World w=world(500);w.add(2);var caller=w.tanks.get(0);caller.nation=0;var a=w.tanks.get(1);var b=w.tanks.get(2);a.x=b.x=20;a.y=b.y=20;
        w.useSkill(0,0);assertEquals(190,a.slowEndTick);assertEquals(190,b.slowEndTick);assertTrue(caller.slowEndTick<=w.tick);
        float x=a.x;w.step(Map.of(1,new World.Input(101,8,0)));assertEquals(x+.21f,a.x,.00001);
        until(w,188);x=a.x;w.step(Map.of(1,new World.Input(189,8,0)));assertEquals(x+.21f,a.x,.00001);assertEquals(500,a.hp);
        x=a.x;w.step(Map.of(1,new World.Input(190,8,0)));assertEquals(x+.3f,a.x,.00001);
        assertEquals(475,a.hp);assertEquals(475,b.hp);assertTrue(a.slowEndTick<=190);until(w,214);assertEquals(475,a.hp);
        World lethal=world(25);lethal.tanks.get(0).nation=0;lethal.useSkill(0,0);until(lethal,190);var killed=lethal.tanks.get(1);
        assertEquals(0,killed.hp);assertTrue(killed.slowEndTick<=190);until(lethal,killed.respawn);assertTrue(killed.slowEndTick<=lethal.tick);
    }
    @Test void bombTrackingSpeedIsFixedWhileSlowSprintEnergyAndWaterStillAffectVehicles() {
        for(boolean wet:new boolean[]{false,true})for(int energy:new int[]{0,1,150})for(int buttons:new int[]{8,40,41}) {
            World w=world(500);var caller=w.tanks.get(0);caller.nation=0;var target=w.tanks.get(1);w.useSkill(0,0);
            var zone=w.skills.casts.getFirst().zones.getFirst();float zx=zone.x,zy=zone.y;target.x=target.y=40;target.energy=energy;
            if(wet)w.map.water=new GameMap.Rect[]{rect(0,0,60,60,2)};
            w.step(Map.of(1,new World.Input(101,buttons,0)));
            double distance=Math.hypot(target.x-40,target.y-40),tracking=Math.hypot(zone.x-zx,zone.y-zy);
            boolean sprint=(buttons&32)!=0&&energy>0;double expected=6.3*(wet?.5:1)*(sprint?1.5:1);
            assertEquals(expected,distance*30,.0001);assertEquals(4.5,tracking*30,.0001);
            assertEquals(GameRules.TankSpeed*.5f,GameRules.BombTrackingSpeed);
            if(!wet&&sprint)assertEquals(2.1,distance/tracking,.0001);
            if(sprint&&energy==1) {
                float x=target.x,y=target.y;w.step(Map.of(1,new World.Input(102,buttons,0)));
                assertEquals(6.3*(wet?.5:1),Math.hypot(target.x-x,target.y-y)*30,.0001);assertEquals(0,target.energy);
            }
        }
    }
    @Test void bombFollowsDirectionChangesWithoutOvershootingAndKeepsItsIdentity() {
        World w=world(500);w.tanks.get(0).nation=0;w.useSkill(0,0);var target=w.tanks.get(1);
        var zone=w.skills.casts.getFirst().zones.getFirst();int id=zone.id;float initial=zone.x;
        target.x+=.01f;w.skills.tick();assertEquals(initial,zone.x); // No movement on activation tick.
        w.step(Map.of());assertEquals(target.x,zone.x);assertEquals(target.y,zone.y);
        target.y+=10;w.step(Map.of());assertEquals(target.x,zone.x);assertEquals(10.15f,zone.y,.00001);
        target.x-=10;float x=zone.x,y=zone.y;w.step(Map.of());assertTrue(zone.x<x);assertTrue(zone.y>y);
        assertEquals(.15,Math.hypot(zone.x-x,zone.y-y),.00001);assertEquals(id,zone.id);assertEquals(100,zone.start);assertEquals(190,zone.end);
    }
    @Test void bombFreezesAfterTargetDeathDepartureOrNewLifeWithoutRetargeting() {
        for(int mode=0;mode<3;mode++) {
            World w=world(500);w.tanks.get(0).nation=0;w.useSkill(0,0);var target=w.tanks.get(1);target.x=40;
            var zone=w.skills.casts.getFirst().zones.getFirst();w.step(Map.of());float x=zone.x,y=zone.y;
            if(mode==0)w.damage(target,0,500,2);
            else if(mode==1)w.remove(1);
            else {w.damage(target,0,500,2);w.spawn(target);}
            w.step(Map.of());assertEquals(x,zone.x);assertEquals(y,zone.y);
            if(mode==0)w.spawn(target);else if(mode==1) {w.add(1);target=w.tanks.get(1);}
            target.x=x;target.y=y;until(w,189);assertEquals(500,target.hp);assertEquals(x,zone.x);assertEquals(y,zone.y);
            w.step(Map.of());assertEquals(500,target.hp);assertEquals(x,zone.x);assertEquals(y,zone.y);
        }
    }
    @Test void purificationRemovesInitialSlowButCannotCancelTrackingOrReapplySlowAtImpact() {
        World w=world(500);w.tanks.get(0).nation=0;w.useSkill(0,0);var target=w.tanks.get(1);
        w.items.apply(target,WorldItems.PURIFICATION);assertTrue(target.slowEndTick<=100);
        var zone=w.skills.casts.getFirst().zones.getFirst();float x=target.x;
        w.step(Map.of(1,new World.Input(101,40,0)));assertEquals(x+.45f,target.x,.00001);assertEquals(x+.15f,zone.x,.00001);
        until(w,190);assertEquals(475,target.hp);assertTrue(target.slowEndTick<=190);
    }
    @Test void movingBombCoordinatesAndSlowRepairDroppedSnapshotsAndRejoin() throws Exception {
        World w=world(500);w.tanks.get(0).nation=0;w.useSkill(0,0);byte[] base=w.encode();
        var zone=w.skills.casts.getFirst().zones.getFirst();var hex=HexFormat.of();List<String> rows=new ArrayList<>();rows.add("BASE\t"+hex.formatHex(base));
        while(w.tick<130) {
            w.step(Map.of(1,new World.Input(w.tick+1,40,0)));
            if(w.tick==115)rows.add("DROPPED\t"+hex.formatHex(DeltaCodec.encode(base,w.encode())));
        }
        assertEquals(18.5,zone.x,.0001);assertEquals(23.45,w.tanks.get(1).x,.0001);
        byte[] current=w.encode(),delta=DeltaCodec.encode(base,current);assertArrayEquals(current,DeltaCodec.apply(base,delta));
        rows.add("DELTA\t"+hex.formatHex(delta));rows.add("REJOIN\t"+hex.formatHex(current));
        var old=World.decode(base).skills.effects.stream().filter(e->e.kind==NationSkills.BOMB).findFirst().orElseThrow();assertEquals(14,old.x);
        Path path=Path.of("target/interop/bomb-tracking.tsv");Files.createDirectories(path.getParent());Files.write(path,rows);
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
        w.map.bricks=new GameMap.Rect[]{rect(0,15,60,1,1)};w.bricks[0]=3;t.turret=9000;
        assertEquals(160,t.qEndTick);until(w,159);assertEquals(500,a.hp);assertEquals(3,w.bricks[0]);until(w,160);
        assertEquals(0,a.hp);assertEquals(0,b.hp);assertEquals(500,t.hp);assertEquals(2,t.kills);assertEquals(0,w.bricks[0]);
        World geometry=world(500);var near=geometry.tanks.get(1);near.x=25;near.y=10+GameRules.TankHalfSize+GameRules.BeamWidth/2;
        assertTrue(geometry.skills.beam(near,11.05f,10,0));near.y+=.002f;assertFalse(geometry.skills.beam(near,11.05f,10,0));
        World cancel=world(500);cancel.tanks.get(0).nation=5;cancel.useSkill(0,0);cancel.damage(cancel.tanks.get(0),1,500);until(cancel,160);assertEquals(500,cancel.tanks.get(1).hp);
    }
    @Test void reducedJapaneseBlastKillsAndBreaksBricksOnlyInsideNewRadius() {
        World w=world(500);w.add(2);var caller=w.tanks.get(0);caller.nation=6;caller.x=caller.y=20;
        var edge=w.tanks.get(1);edge.x=20+GameRules.KamikazeRadius+GameRules.TankHalfSize-.00001f;edge.y=20;edge.body=0;
        var outside=w.tanks.get(2);outside.x=20+GameRules.KamikazeRadius+GameRules.TankHalfSize+.02f;outside.y=20;outside.body=0;
        var brick=rect(20,29.5625f,1,1,1);var missed=rect(20,29.5825f,1,1,1);missed.id=1;
        w.map.bricks=new GameMap.Rect[]{brick,missed};w.bricks[0]=w.bricks[1]=3;
        w.map.walls=new GameMap.Rect[]{rect(25,0,1,60,0)};
        w.useSkill(0,0);until(w,249);assertEquals(500,edge.hp);assertEquals(3,w.bricks[0]);until(w,250);
        assertEquals(0,edge.hp);assertEquals(500,outside.hp);assertEquals(0,w.bricks[0]);assertEquals(3,w.bricks[1]);
        assertEquals(0,caller.hp);assertEquals(1,caller.kills);assertEquals(1129,caller.qReadyTick);
        var blast=w.skills.effects.stream().filter(e->e.kind==NationSkills.KAMIKAZE).findFirst().orElseThrow();assertEquals(9.5625f,blast.radius);
    }
    @Test void circleUsesRotatedHitboxNotItsCenter() {
        var t=new World.Tank();t.x=3.4f+GameRules.TankHalfSize*(float)Math.sqrt(2)-.01f;t.body=4500;assertTrue(NationSkills.circle(t,0,0,3.4f));t.x=3.4f+GameRules.TankHalfSize*(float)Math.sqrt(2)+.01f;assertFalse(NationSkills.circle(t,0,0,3.4f));
    }
    @Test void damagingSkillsDestroyOnlyIntersectingBricksAtImpactAndNeverFromAfterglows() {
        for(int nation:new int[]{0,1,5,6}) {
            World w=world(500);var caller=w.tanks.get(0);caller.nation=nation;caller.x=caller.y=20;
            var target=w.tanks.get(1);target.x=target.y=20;
            float radius=nation==0?GameRules.BombRadius:nation==1?GameRules.RocketRadius:GameRules.KamikazeRadius;
            GameMap.Rect edge=nation==5?rect(30,20+GameRules.BeamWidth/2,1,1,1):rect(20+radius,19.5f,1,1,1);
            GameMap.Rect outside=nation==5?rect(30,20+GameRules.BeamWidth/2+.01f,1,1,1):rect(20+radius+.01f,19.5f,1,1,1);outside.id=1;
            w.map.bricks=new GameMap.Rect[]{edge,outside};w.bricks[0]=3;w.bricks[1]=2;
            w.map.walls=new GameMap.Rect[]{rect(22,0,1,60,0)};
            w.useSkill(0,0);int impact=100+(nation==0?GameRules.BombDelayTicks:nation==1?GameRules.KatyushaDelayTicks:nation==5?GameRules.BeamChargeTicks:GameRules.KamikazeTicks);
            if(nation==1) {
                w.useSkill(0,0);var zones=w.skills.casts.getFirst().zones;
                for(var e:zones){e.x=e.y=20;}
                zones.subList(1,zones.size()).clear();
            }
            until(w,impact-1);assertEquals(3,w.bricks[0]);assertEquals(2,w.bricks[1]);
            until(w,impact);assertEquals(0,w.bricks[0],"nation "+nation);assertEquals(2,w.bricks[1]);assertEquals(1,w.map.walls.length);
            w.bricks[0]=3;until(w,impact+24);assertEquals(3,w.bricks[0],"afterglow repeated damage");
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
        w.map.bricks=new GameMap.Rect[]{rect(23.3f,23.3f,1,1,1)};w.bricks[0]=3;
        w.skills.breakCircle(20,20,3.4f);assertEquals(3,w.bricks[0]);
    }
    @Test void brickDestructionFullDeltaAndRecoveryUseTheAcknowledgedBase() throws Exception {
        World w=world(500);w.map.bricks=new GameMap.Rect[]{rect(20,20,1,1,1),rect(30,30,1,1,1)};w.map.bricks[1].id=1;w.bricks[0]=w.bricks[1]=3;
        byte[] base=w.encode();w.skills.breakCircle(20,20,3.4f);byte[] first=w.encode();
        w.skills.breakCircle(30,30,3.4f);byte[] after=w.encode(),repair=DeltaCodec.encode(base,after);
        assertArrayEquals(after,DeltaCodec.apply(base,repair));assertArrayEquals(after,World.decode(after).encode());
        var hex=HexFormat.of();Path path=Path.of("target/interop/skill-bricks.tsv");Files.createDirectories(path.getParent());
        Files.write(path,List.of("BASE\t"+hex.formatHex(base),"DROPPED\t"+hex.formatHex(DeltaCodec.encode(base,first)),"REPAIR\t"+hex.formatHex(repair),"REJOIN\t"+hex.formatHex(after)));
    }
    @Test void matchCleanupStopsScheduledDamageAndTemporaryEffects() {
        World w=world(100);var t=w.tanks.get(0);t.nation=0;w.useSkill(0,0);w.skills.clear();until(w,190);assertEquals(100,w.tanks.get(1).hp);assertTrue(w.skills.effects.isEmpty());assertTrue(w.skills.casts.isEmpty());
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
        byte[] base=w.encode();w.useSkill(0,0);until(w,145);assertEquals(190,chinese.slowEndTick);w.useSkill(1,0);assertEquals(1300,american.qReadyTick);
        byte[] after=w.encode(),repair=DeltaCodec.encode(base,after);assertArrayEquals(after,DeltaCodec.apply(base,repair));assertArrayEquals(after,World.decode(after).encode());
        var hex=HexFormat.of();Path path=Path.of("target/interop/skill-debuffs.tsv");Files.createDirectories(path.getParent());
        Files.write(path,List.of("BASE\t"+hex.formatHex(base),"REPAIR\t"+hex.formatHex(repair),"REJOIN\t"+hex.formatHex(after)));
    }
}
