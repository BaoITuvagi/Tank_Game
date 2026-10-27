package tank;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
import static tank.WorldItems.*;
class ItemGameplayTest {
    GameMap.Point point(float x,float y) {var p=new GameMap.Point();p.x=x;p.y=y;return p;}
    GameMap.Rect rect(float x,float y,float width,float height,int kind) {var r=new GameMap.Rect();r.x=x;r.y=y;r.w=width;r.h=height;r.kind=kind;return r;}
    GameMap map() {
        var m=new GameMap();m.width=m.height=60;m.walls=new GameMap.Rect[0];m.bricks=new GameMap.Rect[0];m.water=new GameMap.Rect[0];
        m.spawns=new GameMap.Point[]{point(5,5),point(55,55)};m.itemSpawns=new GameMap.Point[]{point(10,10),point(30,30),point(50,50)};return m;
    }
    World world() {
        var w=new World(map(),7);w.add(0);w.add(1);w.tanks.get(0).x=w.tanks.get(0).y=10;w.tanks.get(1).x=w.tanks.get(1).y=40;return w;
    }
    WorldItems.Item item(World w,int type,float x,float y,int expires) {
        var i=new WorldItems.Item();i.id=w.items.nextId++;i.type=type;i.x=x;i.y=y;i.expires=expires;w.items.items.put(i.id,i);return i;
    }
    World.Bullet bullet(World w,int kind,float x,float y,int angle) {
        var b=new World.Bullet();b.id=w.nextBullet++;b.owner=0;b.kind=kind;b.x=x;b.y=y;b.heading=angle;b.born=w.tick;w.bullets.put(b.id,b);return b;
    }
    void until(World w,int tick) {while(w.tick<tick)w.step(Map.of());}
    World scheduledWorld(boolean roleplay) {
        World w=new World(map(),31);w.items.configure(roleplay);var points=new ArrayList<GameMap.Point>();
        for(int y:new int[]{10,30})for(int x=10;x<=50;x+=10)points.add(point(x,y));
        w.map.itemSpawns=points.toArray(GameMap.Point[]::new);return w;
    }
    WorldItems.Item ofType(World w,int type) {return w.items.items.values().stream().filter(i->i.type==type).findFirst().orElse(null);}
    void collect(World w,int type) {
        var i=ofType(w,type);assertNotNull(i);var t=w.tanks.get(0);t.x=i.x;t.y=i.y;w.step(Map.of());assertNull(ofType(w,type));
    }
    @Test void modeSpecificItemCountsAppearAtExactFirstSpawnWithoutDuplicates() {
        for(boolean rp:new boolean[]{false,true}) {
            World w=scheduledWorld(rp);int first=rp?1200:300,count=rp?4:6;
            until(w,first-1);assertTrue(w.items.items.isEmpty());w.step(Map.of());assertEquals(count,w.items.items.size());
            Set<String> positions=new HashSet<>();Set<Integer> types=new HashSet<>();
            for(var i:w.items.items.values()) {
                assertTrue(types.add(i.type));assertEquals(rp?1800:0,i.expires);assertTrue(positions.add(i.x+":"+i.y));
            }
        }
    }
    @Test void uncollectedNormalItemsRemainForTheWholeMatch() {
        World w=scheduledWorld(false);until(w,300);byte[] initial=w.encode();
        until(w,18000);assertEquals(6,w.items.items.size());assertArrayEquals(initial,w.encode());
    }
    @Test void normalCollectedTypesRespawnIndependentlyAtADifferentPosition() {
        World w=scheduledWorld(false);int first=300,delay=450;until(w,first);w.add(0);
        var selected=new ArrayList<>(w.items.items.values());var a=selected.get(0);var b=selected.get(1);
        collect(w,a.type);int aDue=w.tick+delay;
        until(w,first+30);collect(w,b.type);int bDue=w.tick+delay;w.tanks.get(0).x=w.tanks.get(0).y=5;
        until(w,aDue-1);assertNull(ofType(w,a.type));assertNull(ofType(w,b.type));assertEquals(4,w.items.items.size());
        w.step(Map.of());var replacement=ofType(w,a.type);assertNotNull(replacement);assertTrue(replacement.id>a.id);
        assertTrue(replacement.x!=a.x||replacement.y!=a.y);assertEquals(0,replacement.expires);assertNull(ofType(w,b.type));
        until(w,bDue-1);assertNull(ofType(w,b.type));w.step(Map.of());replacement=ofType(w,b.type);assertNotNull(replacement);
        assertTrue(replacement.x!=b.x||replacement.y!=b.y);assertEquals(6,w.items.items.size());
        for(var original:selected)assertNotNull(ofType(w,original.type));
    }
    @Test void roleplayUsesFortySecondCentralWavesWithTwentySecondWindows() {
        for(int duration:new int[]{1800,5400,9000,18000}) {
            World w=scheduledWorld(true);w.items.configure(true,duration);int interval=1200,window=600;
            for(int wave=1;wave*interval<duration;wave++) {
                until(w,wave*interval-1);assertTrue(w.items.items.isEmpty());w.step(Map.of());
                assertEquals(wave,w.items.wave());assertEquals(4,w.items.items.size());
                int end=Math.min(duration,wave*interval+window);
                for(var i:w.items.items.values()) {
                    assertTrue(Math.hypot(i.x-30,i.y-30)<=15);assertEquals(end,i.expires);
                }
                until(w,end-1);assertEquals(4,w.items.items.size());
                w.step(Map.of());assertTrue(w.items.items.isEmpty());assertEquals(0,w.items.wave());
            }
            until(w,duration);assertTrue(w.items.items.isEmpty());assertEquals(0,w.items.wave());
        }
    }
    @Test void roleplayPickupDoesNotRespawnUntilNextScheduledWave() {
        World w=scheduledWorld(true);until(w,1200);w.add(0);int type=w.items.items.firstEntry().getValue().type;
        collect(w,type);w.tanks.get(0).x=w.tanks.get(0).y=5;
        until(w,1799);assertEquals(3,w.items.items.size());assertNull(ofType(w,type));
        until(w,2399);assertTrue(w.items.items.isEmpty());w.step(Map.of());assertEquals(4,w.items.items.size());
    }
    @Test void roleplayNeverFallsBackOutsideBlockedCentralRegion() {
        World w=scheduledWorld(true);w.map.water=new GameMap.Rect[]{rect(14,14,32,32,0)};
        until(w,1200);assertTrue(w.items.items.isEmpty());
        w.map.water=new GameMap.Rect[0];until(w,1799);assertTrue(w.items.items.isEmpty());
        until(w,2400);assertEquals(4,w.items.items.size());
    }
    @Test void roleplayRandomSelectionCanChooseEveryFourTypeCombination() {
        Set<Set<Integer>> combinations=new HashSet<>();Random seeds=new Random(721);
        for(int n=0;n<300;n++) {
            World w=new World(map(),seeds.nextLong());w.items.configure(true);w.tick=1200;w.items.beginTick();
            Set<Integer> types=new HashSet<>();for(var i:w.items.items.values())assertTrue(types.add(i.type));
            assertEquals(4,types.size());combinations.add(types);
        }
        assertEquals(15,combinations.size());
    }
    @Test void allTypesCanUseEveryValidSpawnPoint() {
        World w=scheduledWorld(false);w.tick=300;List<Set<String>> positions=new ArrayList<>();for(int i=0;i<6;i++)positions.add(new HashSet<>());
        for(int n=0;n<100;n++) {
            w.items.clear();w.items.configure(false);w.items.beginTick();assertEquals(6,w.items.items.size());
            for(var i:w.items.items.values())positions.get(i.type).add(i.x+":"+i.y);
        }
        for(var points:positions)assertEquals(10,points.size());
    }
    @Test void blockedOccupiedWetOrTooClosePointsRetryWithoutResettingTimers() {
        World w=world();w.map.walls=new GameMap.Rect[]{rect(29,29,2,2,0)};w.map.water=new GameMap.Rect[]{rect(49,49,2,2,2)};
        until(w,300);assertTrue(w.items.items.isEmpty());
        w.map.itemSpawns=new GameMap.Point[]{point(10,10)};w.tanks.get(0).x=15.99f;w.tanks.get(0).y=10;
        w.step(Map.of());assertTrue(w.items.items.isEmpty());w.tanks.get(0).x=16;w.step(Map.of());assertEquals(1,w.items.items.size());
        w.step(Map.of());assertEquals(1,w.items.items.size()); // Other types cannot overlap the existing item.
        collect(w,MEDICAL);int due=w.tick+450;w.tanks.get(0).x=16;
        w.map.bricks=new GameMap.Rect[]{rect(9,9,2,2,1)};w.bricks[0]=3;until(w,due);assertTrue(w.items.items.isEmpty());
        w.bricks[0]=0;w.step(Map.of());assertNull(ofType(w,MEDICAL)); // Its previous location is still excluded.
        w.map.itemSpawns=new GameMap.Point[]{point(10,10),point(20,20)};w.step(Map.of());assertNotNull(ofType(w,MEDICAL));
        assertEquals(20,ofType(w,MEDICAL).x);assertEquals(20,ofType(w,MEDICAL).y);
    }
    @Test void clearingMatchCancelsPendingRespawns() {
        World w=scheduledWorld(true);until(w,1200);w.skills.clear();until(w,2400);assertTrue(w.items.items.isEmpty());
    }
    @Test void bothRealMapsSupportModeSpecificItemCountsWithEightPlayers() throws Exception {
        for(String name:List.of("map.json","map-large.json"))for(boolean rp:new boolean[]{false,true}) {
            var m=GameMap.load(Path.of("../../source/client/Assets/StreamingAssets/"+name));
            World w=new World(m,31);w.items.configure(rp);for(int slot=0;slot<8;slot++)w.add(slot);w.arrangeInitialSpawns(null);
            until(w,rp?1200:300);assertEquals(rp?4:6,w.items.items.size(),name+" roleplay="+rp);
            for(var i:w.items.items.values()) {
                assertFalse(m.blocked(i.x,i.y,w.bricks));assertFalse(m.water(i.x,i.y));
                for(var t:w.tanks.values())assertTrue(Math.hypot(t.x-i.x,t.y-i.y)>=6);
            }
        }
    }
    @Test void expiryPrecedesPickupAndDeadVehiclesCannotConsume() {
        World w=world();var t=w.tanks.get(0);t.hp=30;item(w,MEDICAL,10,10,1);w.step(Map.of());assertEquals(30,t.hp);assertTrue(w.items.items.isEmpty());
        t.hp=0;t.respawn=100;item(w,MEDICAL,10,10,90);w.step(Map.of());assertEquals(1,w.items.items.size());assertEquals(0,t.hp);
    }
    @Test void respawnPreservesTheExistingOneTickDelayBeforeShooting() {
        World w=world();var t=w.tanks.get(0);w.damage(t,1,100);w.tick=89;
        w.step(Map.of(0,new World.Input(90,16,0)));assertEquals(100,t.hp);assertTrue(w.bullets.isEmpty());
        w.step(Map.of(0,new World.Input(91,16,0)));assertEquals(1,w.bullets.size());
    }
    @Test void closestVehicleThenSmallestSlotWinsExactlyOnce() {
        for(float secondX:new float[]{10,10.2f,9.9f}) {
            World w=world();var a=w.tanks.get(0);var b=w.tanks.get(1);a.hp=b.hp=50;b.x=secondX;b.y=10;
            item(w,MEDICAL,10.2f,10,99);w.step(Map.of());assertTrue(w.items.items.isEmpty());
            assertEquals(secondX==10.2f?50:80,a.hp);assertEquals(secondX==10.2f?80:50,b.hp);assertEquals(1,w.items.effects.size());
        }
    }
    @Test void sweptDashCollectsAnItemPassedBetweenEndpoints() {
        World w=world();var t=w.tanks.get(0);t.hp=40;t.body=0;item(w,MEDICAL,11,10,200);
        w.skillIntents.add(()->w.dash(t));w.step(Map.of());assertEquals(12,t.x,.0001);assertEquals(70,t.hp);assertTrue(w.items.items.isEmpty());
    }
    @Test void pickupHappensBeforeShootingAndDamageInSameTick() {
        World w=world();var t=w.tanks.get(0);t.hp=10;item(w,INVINCIBILITY,10,10,200);
        var b=bullet(w,0,10,10,0);b.owner=1;w.step(Map.of(0,new World.Input(1,16,0)));assertEquals(10,t.hp);assertFalse(w.bullets.containsKey(b.id));
        w=world();t=w.tanks.get(0);item(w,MIRROR,10,10,200);w.step(Map.of(0,new World.Input(1,16,0)));assertEquals(2,t.mirrorShots);assertEquals(MIRROR_BULLET,w.bullets.firstEntry().getValue().kind);
    }
    @Test void medicalRoundsUpCapsAndConsumesAtFullHpWithoutReviving() {
        for(int max:new int[]{1,3,99,100,101,500}) {
            World w=world();var t=w.tanks.get(0);t.maxHp=max;t.hp=1;w.items.apply(t,MEDICAL);assertEquals(Math.min(max,1+(int)Math.ceil(max*.3)),t.hp);
            t.hp=max;item(w,MEDICAL,10,10,100);w.step(Map.of());assertTrue(w.items.items.isEmpty());assertEquals(max,t.hp);
            w.damage(t,1,max,2);w.items.apply(t,MEDICAL);assertEquals(0,t.hp);
        }
    }
    @Test void refreshReplacementAndDeathAreIndependentOfMapItemsAndFlyingBullets() {
        World w=world();var t=w.tanks.get(0);w.items.apply(t,ARMOR);w.items.apply(t,INVINCIBILITY);w.items.apply(t,HE);var fired=bullet(w,HE_BULLET,20,20,0);
        w.tick=10;w.items.apply(t,ARMOR);assertEquals(190,t.armorEndTick);assertEquals(90,t.invincibilityEndTick);assertEquals(150,t.heEndTick);
        w.items.apply(t,MIRROR);assertEquals(3,t.mirrorShots);assertEquals(150,t.heEndTick);t.mirrorShots=1;w.items.apply(t,MIRROR);assertEquals(3,t.mirrorShots);
        w.items.apply(t,HE);assertEquals(3,t.mirrorShots);assertEquals(160,t.heEndTick);item(w,ARMOR,30,30,500);
        w.damage(t,1,100,2);assertEquals(0,t.mirrorShots);assertTrue(t.armorEndTick<=w.tick&&t.invincibilityEndTick<=w.tick&&t.heEndTick<=w.tick);assertEquals(1,w.items.items.size());assertEquals(HE_BULLET,fired.kind);
        w.spawn(t);assertEquals(HE_BULLET,fired.kind);w.remove(0);assertFalse(w.bullets.containsKey(fired.id));
        w.skills.clear();assertTrue(w.items.items.isEmpty());assertTrue(w.items.effects.isEmpty());
    }
    @Test void armorRoundsOrdinaryDamageButCannotStopInstantKills() {
        for(int amount:new int[]{1,5,12,20,25,36}) {
            World w=world();var t=w.tanks.get(0);w.items.apply(t,ARMOR);w.damage(t,1,amount);assertEquals(100-Math.round(amount*.65f),t.hp);
        }
        World w=world();var t=w.tanks.get(0);w.items.apply(t,ARMOR);w.damage(t,1,100,1);assertEquals(0,t.hp);
    }
    @Test void invincibilityHasExactDeadlineDoesNotPauseArmorOrPreventCrowdControl() {
        World w=world();var t=w.tanks.get(0);w.items.apply(t,ARMOR);w.items.apply(t,INVINCIBILITY);
        w.tick=89;w.damage(t,1,100,1);assertEquals(100,t.hp);w.damage(t,1,25);assertEquals(100,t.hp);
        w.skills.debuff(t,0,1,300);w.skills.debuff(t,2,1,300);assertEquals(300,t.confusionEndTick);assertEquals(300,t.fireLockEndTick);
        w.tick=90;w.damage(t,1,20);assertEquals(87,t.hp);w.tick=180;w.damage(t,1,20);assertEquals(67,t.hp);
    }
    @Test void italianBeamAndJapaneseSacrificeRespectDamageKindsAndScore() {
        for(int nation:new int[]{5,6}) {
            World w=world();var a=w.tanks.get(0);var b=w.tanks.get(1);a.nation=nation;b.x=14;b.y=10;
            w.items.apply(b,INVINCIBILITY);b.invincibilityEndTick=1000;w.items.apply(a,INVINCIBILITY);a.invincibilityEndTick=1000;
            w.useSkill(0,0);until(w,nation==5?60:150);assertEquals(100,b.hp);assertEquals(0,b.deaths);assertEquals(0,a.kills);
            if(nation==6) {assertEquals(0,a.hp);assertEquals(1,a.deaths);}else assertEquals(100,a.hp);
        }
    }
    @Test void purificationPreservesOwnQBuffsAndSelfDebuffsOnlyRemovingOtherSources() {
        World w=world();var t=w.tanks.get(0);t.nation=5;w.useSkill(0,0);int end=t.qEndTick,ready=t.qReadyTick;
        w.items.apply(t,ARMOR);w.items.apply(t,MIRROR);w.skills.debuff(t,0,1,200);w.skills.debuff(t,2,1,250);w.skills.debuff(t,3,1,260);w.skills.debuff(t,3,0,100);
        w.items.apply(t,PURIFICATION);assertEquals(0,t.confusionEndTick);assertEquals(0,t.fireLockEndTick);assertEquals(100,t.slowEndTick);
        assertEquals(end,t.qEndTick);assertEquals(ready,t.qReadyTick);assertEquals(1,t.qStage);assertEquals(3,t.mirrorShots);assertEquals(180,t.armorEndTick);
        w.skills.debuff(t,0,1,350);assertEquals(350,t.confusionEndTick);w.items.apply(t,PURIFICATION);assertEquals(0,t.confusionEndTick);
    }
    @Test void cleansedFogAStaysGoneWhenNewFogBExpiresFirstAndOtherVictimsStayFogged() {
        World w=world();w.add(2);var france=w.tanks.get(1);france.nation=3;w.useSkill(1,0);var t=w.tanks.get(0);assertEquals(300,t.fogEndTick);
        w.tick=30;w.items.apply(t,PURIFICATION);assertEquals(30,t.fogEndTick);assertEquals(300,w.tanks.get(2).fogEndTick);
        var old=w.skills.debuffs.stream().filter(d->d.kind==1&&d.slot==0).findFirst().orElseThrow();assertTrue(old.cleansed);
        france.qReadyTick=30;w.useSkill(1,0);var newer=w.skills.debuffs.stream().filter(d->d.kind==1&&d.slot==0&&!d.cleansed).findFirst().orElseThrow();assertNotEquals(old.id,newer.id);
        newer.end=100;w.skills.refresh(t,1);assertEquals(100,t.fogEndTick);until(w,100);assertEquals(100,t.fogEndTick);assertTrue(old.cleansed);
        until(w,300);assertFalse(w.skills.debuffs.contains(old));assertTrue(t.fogEndTick<=300);
    }
    @Test void deathRespawnAndNewLifeNeverReapplyOldFog() {
        World w=world();var france=w.tanks.get(1);france.nation=3;w.useSkill(1,0);var t=w.tanks.get(0);w.items.apply(t,PURIFICATION);
        int life=t.life;w.damage(t,1,100);until(w,90);assertNotEquals(life,t.life);assertTrue(t.fogEndTick<=90);assertTrue(w.skills.debuffs.stream().noneMatch(d->d.slot==0));
        france.qReadyTick=90;w.useSkill(1,0);assertEquals(390,t.fogEndTick);
    }
    @Test void purificationRemovesOnlyAddedQDelayIncludingTheEightPlusTenExample() {
        for(int base:new int[]{0,240,1000}) {
            World w=world();var china=w.tanks.get(1);china.nation=7;var t=w.tanks.get(0);t.nation=5;t.qReadyTick=base;
            w.useSkill(1,0);assertEquals(base+300,t.qReadyTick);w.tick=90;w.items.apply(t,PURIFICATION);assertEquals(base,t.qReadyTick);
            assertTrue(t.qPenaltySources.isEmpty());
            if(base==240)assertEquals(150,t.qReadyTick-w.tick);
            china.qReadyTick=90;w.useSkill(1,0);assertEquals(Math.max(90,base)+300,t.qReadyTick);w.items.apply(t,PURIFICATION);assertEquals(base,t.qReadyTick);
        }
    }
    @Test void multipleSabotagesAndAimingQKeepOriginalCooldownAcrossDeath() {
        World w=world();var china=w.tanks.get(1);china.nation=7;var t=w.tanks.get(0);t.nation=1;w.useSkill(0,0);w.useSkill(1,0);
        w.tick=10;china.qReadyTick=10;w.useSkill(1,0);assertEquals(600,t.qReadyTick);w.useSkill(0,0);assertEquals(1577,t.qReadyTick);assertEquals(987,t.qBaseReadyTick);
        w.damage(t,1,100);w.spawn(t);assertEquals(1577,t.qReadyTick);w.items.apply(t,PURIFICATION);assertEquals(987,t.qReadyTick);assertEquals(0,t.qPenalty);
    }
    @Test void heDirectPlusExplosionIncludesShooterButStopsAtIntactTerrain() {
        World w=world();var shooter=w.tanks.get(0);var target=w.tanks.get(1);shooter.x=8.8f;target.x=11.5f;target.y=10;
        var b=bullet(w,HE_BULLET,10,10,0);w.advance(b);assertEquals(74,target.hp);assertEquals(94,shooter.hp);assertEquals(1,w.items.effects.stream().filter(e->e.kind==EXPLOSION).count());
        w=world();target=w.tanks.get(1);target.x=11.2f+GameRules.TankHalfSize+.1f;target.y=10;w.map.walls=new GameMap.Rect[]{rect(11,8,.2f,4,0)};
        b=bullet(w,HE_BULLET,10.5f,10,0);w.advance(b);assertEquals(100,target.hp);assertEquals(1,b.bounces);assertEquals(1,w.items.effects.size());
    }
    @Test void heHasRealLargerTankCollisionAndOnlyOneBrickDamagePerImpact() {
        World w=world();var target=w.tanks.get(1);target.x=11.8f;target.y=10+GameRules.TankHalfSize+GameRules.BulletHalfWidth+.03f;var normal=bullet(w,0,10,10,0);assertNull(w.tankHit(normal,.72f,0,target,0,1));normal.kind=HE_BULLET;assertNotNull(w.tankHit(normal,.72f,0,target,0,1));
        w=world();w.map.bricks=new GameMap.Rect[]{rect(11,9,1,2,1)};w.bricks[0]=3;var he=bullet(w,HE_BULLET,10.5f,10,0);w.advance(he);assertEquals(2,w.bricks[0]);assertEquals(1,w.items.effects.size());assertEquals(1,he.bounces);
    }
    @Test void embeddedHeMuzzleStillExplodesOutsideTheContactSurfaceOnlyOnce() {
        World w=world();var t=w.tanks.get(0);t.x=9.5f;t.y=10;w.map.walls=new GameMap.Rect[]{rect(11,8,1,4,0)};
        var b=bullet(w,HE_BULLET,11.2f,10,0);w.advance(b);assertEquals(94,t.hp);assertEquals(1,w.items.effects.size());assertEquals(1,b.bounces);
    }
    @Test void mirrorSplitsIntoExactlyThreeFrozenBranchesWithOneMoreBounce() {
        World w=world();w.tanks.get(0).x=w.tanks.get(0).y=5;w.map.walls=new GameMap.Rect[]{rect(11,8,1,4,0)};var b=bullet(w,MIRROR_BULLET,10.5f,10,0);b.born=-25;b.remainingLifetime=125;b.speedPercent=140;w.advance(b);
        assertFalse(w.bullets.containsKey(b.id));assertEquals(3,w.bullets.size());assertEquals(Set.of(18000,15500,20500),new HashSet<>(w.bullets.values().stream().map(c->c.heading).toList()));
        for(var child:w.bullets.values()) {assertEquals(MIRROR_CHILD,child.kind);assertEquals(10,child.damage);assertEquals(140,child.speedPercent);assertEquals(-25,child.born);assertEquals(125,child.remainingLifetime);assertEquals(.7f,WorldItems.size(child));assertEquals(1,child.bounces);}
        var c=w.bullets.firstEntry().getValue();c.x=10.5f;c.y=10;c.heading=0;w.advance(c);assertEquals(2,c.bounces);c.x=10.5f;c.heading=0;w.advance(c);assertFalse(w.bullets.containsKey(c.id));assertEquals(1,w.items.effects.size());
    }
    @Test void mirrorHitsVehiclesWithoutSplittingAndExpiresWithParentLifetime() {
        World w=world();var target=w.tanks.get(1);target.x=11.5f;target.y=10;var b=bullet(w,MIRROR_BULLET,10,10,0);w.advance(b);assertEquals(80,target.hp);assertTrue(w.bullets.isEmpty());assertTrue(w.items.effects.isEmpty());
        w=world();w.tanks.get(0).x=w.tanks.get(0).y=5;w.map.walls=new GameMap.Rect[]{rect(11,8,1,4,0)};b=bullet(w,MIRROR_BULLET,10.5f,10,0);b.born=-149;w.advance(b);w.step(Map.of());assertTrue(w.bullets.isEmpty());
    }
    @Test void mirrorReservesCapacityAndConsumesOncePerAcceptedVolley() {
        World w=world();var t=w.tanks.get(0);w.items.apply(t,MIRROR);
        for(int i=0;i<126;i++)bullet(w,0,50,50,0);
        w.step(Map.of(0,new World.Input(1,16,0)));assertEquals(3,t.mirrorShots);assertEquals(126,w.bullets.size());
        w.bullets.pollLastEntry();t.cooldown=0;w.step(Map.of(0,new World.Input(2,16,0)));assertEquals(2,t.mirrorShots);assertEquals(128,w.items.reservedBullets());
        World.Bullet parent=w.bullets.lastEntry().getValue();w.items.split(parent,18000,-1,0);assertEquals(128,w.bullets.size());assertEquals(128,w.items.reservedBullets());
        w=world();t=w.tanks.get(0);t.nation=4;w.useSkill(0,0);w.items.apply(t,MIRROR);w.step(Map.of(0,new World.Input(1,16,0)));assertEquals(2,t.mirrorShots);assertEquals(3,w.bullets.size());assertEquals(9,w.items.reservedBullets());for(var shot:w.bullets.values()) {assertEquals(MIRROR_BULLET,shot.kind);assertEquals(15,shot.damage);}
    }
    @Test void allHeMirrorGermanCombinationsStackInEveryPickupOrder() {
        for(int mask=0;mask<8;mask++)for(int[] order:new int[][]{{0,1,2},{0,2,1},{1,0,2},{1,2,0},{2,0,1},{2,1,0}}) {
            World w=world();var t=w.tanks.get(0);boolean german=(mask&1)!=0,he=(mask&2)!=0,mirror=(mask&4)!=0;
            if(german)t.nation=4;
            for(int effect:order)if((mask&(1<<effect))!=0) {if(effect==0)w.useSkill(0,0);else w.items.apply(t,effect==1?HE:MIRROR);}
            w.step(Map.of(0,new World.Input(1,16,0)));var parents=new ArrayList<>(w.bullets.values());int count=german?3:1;
            assertEquals(count,parents.size());assertEquals(mirror?2:0,t.mirrorShots);
            for(var parent:parents) {assertEquals(german?15:20,parent.damage);assertEquals(he,hasHe(parent.kind));assertEquals(mirror,isMirrorParent(parent.kind));}
            assertArrayEquals(w.encode(),World.decode(w.encode()).encode());
            w.tanks.clear();w.map.walls=new GameMap.Rect[]{rect(11,0,1,60,0)};int index=0;
            for(var parent:parents) {parent.x=10.5f;parent.y=10+index++*5;parent.heading=0;w.advance(parent);}
            assertEquals(he?count:0,w.items.effects.stream().filter(e->e.kind==EXPLOSION).count());
            assertEquals(mirror?count:0,w.items.effects.stream().filter(e->e.kind==SPLIT).count());
            assertEquals(count*(mirror?3:1),w.bullets.size());
            if(mirror)for(var child:w.bullets.values()) {
                assertTrue(isMirrorChild(child.kind));assertEquals(he,hasHe(child.kind));assertEquals(german?25:10,child.damage);
                assertEquals(he?1.05f:.7f,WorldItems.size(child),.00001);assertEquals(1,child.bounces);
            }
        }
    }
    @Test void combinedChildrenKeepHeAndGermanDamageWithoutRecursiveSplitting() {
        World w=world();var owner=w.tanks.get(0);owner.x=owner.y=5;
        var parent=bullet(w,HE_MIRROR_BULLET,10.5f,10,0);parent.damage=15;parent.born=-20;parent.remainingLifetime=130;parent.speedPercent=140;
        w.items.split(parent,18000,-1,0);assertEquals(3,w.bullets.size());
        var child=w.bullets.firstEntry().getValue();assertEquals(25,child.damage);assertEquals(HE_MIRROR_CHILD,child.kind);assertEquals(-20,child.born);assertEquals(130,child.remainingLifetime);assertEquals(140,child.speedPercent);
        child.x=10;child.y=10;child.heading=0;var target=w.tanks.get(1);target.x=11.5f;target.y=10;w.advance(child);
        assertEquals(69,target.hp);assertEquals(1,w.items.effects.stream().filter(e->e.kind==EXPLOSION).count());
        child=w.bullets.firstEntry().getValue();w.map.walls=new GameMap.Rect[]{rect(11,0,1,60,0)};child.x=10.5f;child.y=20;child.heading=0;
        w.advance(child);assertEquals(2,w.bullets.size());assertEquals(2,child.bounces);assertEquals(1,w.items.effects.stream().filter(e->e.kind==SPLIT).count());
        assertEquals(2,w.items.effects.stream().filter(e->e.kind==EXPLOSION).count());
    }
    @Test void germanHeMirrorReservesAllNineChildrenBeforeConsumingACharge() {
        World w=world();var t=w.tanks.get(0);t.nation=4;w.useSkill(0,0);w.items.apply(t,HE);w.items.apply(t,MIRROR);
        for(int i=0;i<120;i++)bullet(w,NORMAL_BULLET,50,50,0);
        w.step(Map.of(0,new World.Input(1,16,0)));assertEquals(120,w.bullets.size());assertEquals(3,t.mirrorShots);
        w.bullets.pollLastEntry();t.cooldown=0;w.step(Map.of(0,new World.Input(2,16,0)));
        assertEquals(122,w.bullets.size());assertEquals(128,w.items.reservedBullets());assertEquals(2,t.mirrorShots);
        var parents=w.bullets.values().stream().filter(b->isMirrorParent(b.kind)).toList();assertEquals(3,parents.size());
        for(var parent:parents)w.items.split(parent,18000,-1,0);
        assertEquals(128,w.bullets.size());assertEquals(128,w.items.reservedBullets());
    }
    @Test void stackedEffectsExpireIndependentlyAndFlyingPayloadsStayFrozen() {
        for(boolean heFirst:new boolean[]{true,false}) {
            World w=world();var t=w.tanks.get(0);t.nation=4;
            if(heFirst)w.items.apply(t,HE);else w.useSkill(0,0);
            w.tick=10;if(heFirst)w.useSkill(0,0);else w.items.apply(t,HE);w.items.apply(t,MIRROR);
            w.step(Map.of(0,new World.Input(11,16,0)));var flying=new ArrayList<>(w.bullets.values());
            until(w,149);t.cooldown=0;w.step(Map.of(0,new World.Input(150,16,0)));
            var next=w.bullets.values().stream().filter(b->b.born==150).toList();assertEquals(heFirst?3:1,next.size());
            for(var b:next) {assertEquals(heFirst?MIRROR_BULLET:HE_MIRROR_BULLET,b.kind);assertEquals(heFirst?15:20,b.damage);}
            for(var b:flying) {assertEquals(HE_MIRROR_BULLET,b.kind);assertEquals(15,b.damage);}
            until(w,159);t.cooldown=0;w.step(Map.of(0,new World.Input(160,16,0)));
            assertEquals(0,t.mirrorShots);assertEquals(MIRROR_BULLET,w.bullets.lastEntry().getValue().kind);assertEquals(20,w.bullets.lastEntry().getValue().damage);
        }
        World w=world();var t=w.tanks.get(0);t.nation=4;w.useSkill(0,0);w.items.apply(t,HE);w.items.apply(t,MIRROR);
        for(int tick:new int[]{1,31,61}) {until(w,tick-1);w.step(Map.of(0,new World.Input(tick,16,0)));}
        assertEquals(0,t.mirrorShots);until(w,90);w.step(Map.of(0,new World.Input(91,16,0)));
        assertEquals(3,w.bullets.values().stream().filter(b->b.born==91).count());
        assertEquals(HE_BULLET,w.bullets.lastEntry().getValue().kind);assertEquals(15,w.bullets.lastEntry().getValue().damage);
        assertTrue(w.bullets.values().stream().anyMatch(b->b.kind==HE_MIRROR_BULLET));
    }
    @Test void germanVolleyKeepsHeForBothPickupOrdersAndIndependentDeadlines() {
        for(boolean heFirst:new boolean[]{true,false}) {
            World w=world();var t=w.tanks.get(0);t.nation=4;
            if(heFirst)w.items.apply(t,HE);else w.useSkill(0,0);
            w.tick=10;
            if(heFirst)w.useSkill(0,0);else w.items.apply(t,HE);
            int heEnd=t.heEndTick,qEnd=t.qEndTick;
            w.step(Map.of(0,new World.Input(11,16,0)));
            var volley=new ArrayList<>(w.bullets.values());assertEquals(3,volley.size());
            for(var shot:volley) {assertEquals(HE_BULLET,shot.kind);assertEquals(15,shot.damage);assertEquals(1.5f,WorldItems.size(shot));}
            assertEquals(heEnd,t.heEndTick);assertEquals(qEnd,t.qEndTick);
            assertArrayEquals(w.encode(),World.decode(w.encode()).encode());
            w.bullets.clear();w.tick=Math.min(heEnd,qEnd)-1;t.cooldown=0;
            w.step(Map.of(0,new World.Input(w.tick+1,16,0)));
            assertEquals(heFirst?3:1,w.bullets.size());
            for(var shot:w.bullets.values()) {assertEquals(heFirst?NORMAL_BULLET:HE_BULLET,shot.kind);assertEquals(heFirst?15:20,shot.damage);}
            for(var shot:volley) {assertEquals(HE_BULLET,shot.kind);assertEquals(15,shot.damage);}
        }
    }
    @Test void germanHeImpactAddsSplashToDirectDamage() {
        World w=world();var t=w.tanks.get(0);t.nation=4;w.items.apply(t,HE);w.useSkill(0,0);
        w.step(Map.of(0,new World.Input(1,16,0)));var shot=w.bullets.firstEntry().getValue();
        w.bullets.clear();w.bullets.put(shot.id,shot);shot.x=10;shot.y=10;shot.heading=0;
        t.x=t.y=5;var target=w.tanks.get(1);target.x=11.5f;target.y=10;
        w.advance(shot);
        assertEquals(79,target.hp);assertEquals(100,t.hp);assertFalse(w.bullets.containsKey(shot.id));
        assertEquals(1,w.items.effects.stream().filter(e->e.kind==EXPLOSION).count());
    }
    @Test void mapItemPointsValidateOnBothMapsAndRejectMalformedOrWetCoordinates() throws Exception {
        for(String name:List.of("map.json","map-large.json")) {
            var m=GameMap.load(Path.of("../../source/client/Assets/StreamingAssets/"+name));assertTrue(m.itemSpawns.length>=10);assertEquals(32,m.hash.length);
            m.itemSpawns[1]=m.itemSpawns[0];assertThrows(IllegalArgumentException.class,m::validate);
        }
        var m=map();m.itemSpawns[0].x=Float.NaN;assertThrows(IllegalArgumentException.class,m::validate);
        m=map();m.water=new GameMap.Rect[]{rect(9,9,2,2,2)};assertThrows(IllegalArgumentException.class,m::validate);
    }
}
