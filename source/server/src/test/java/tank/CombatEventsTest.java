package tank;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;
import static tank.CombatEvents.*;

class CombatEventsTest {
    World world() {return new ItemGameplayTest().world();}
    long count(World w,int kind) {return w.combat.events.values().stream().filter(e->e.kind()==kind).count();}
    void add(World w,int kind) {var t=w.tanks.get(0);w.combat.add(w.tick,kind,t,kind==DAMAGE||kind==DEATH?t:null,1,65535,0,10,10,0,kind==DAMAGE?10:0,0,0);}
    @Test void shotIsPerSuccessfulVolleyAndNeverPerGermanPellet() {
        World w=world();var t=w.tanks.get(0);t.nation=4;t.qEndTick=100;
        w.step(Map.of(0,new World.Input(1,16,0)));assertEquals(3,w.bullets.size());assertEquals(1,count(w,SHOT));
        for(int tick=2;tick<30;tick++)w.step(Map.of(0,new World.Input(tick,16,0)));
        assertEquals(1,count(w,SHOT));t.fireLockEndTick=100;w.step(Map.of(0,new World.Input(30,16,0)));assertEquals(1,count(w,SHOT));
        t.fireLockEndTick=0;t.cooldown=0;
        for(int id=10;id<10+GameRules.MaxBullets;id++) {var b=new World.Bullet();b.id=id;b.x=20;b.y=20;w.bullets.put(id,b);}
        w.step(Map.of(0,new World.Input(31,16,0)));assertEquals(1,count(w,SHOT));
    }
    @Test void lethalDamageAndInvincibilityRecordOnlyRealResults() {
        World w=world();var t=w.tanks.get(1);t.armorEndTick=10;
        w.damage(t,0,20);assertEquals(87,t.hp);assertEquals(13,w.combat.events.lastEntry().getValue().value());
        t.invincibilityEndTick=10;w.damage(t,0,999);assertEquals(1,count(w,DAMAGE));assertEquals(0,count(w,DEATH));
        t.invincibilityEndTick=0;w.damage(t,0,999);w.damage(t,0,999);
        assertEquals(2,count(w,DAMAGE));assertEquals(1,count(w,DEATH));
        Event death=w.combat.events.lastEntry().getValue();assertEquals(t.life,death.targetLife());assertEquals(t.x,death.x());assertEquals(1,t.deaths);assertEquals(1,w.tanks.get(0).kills);
    }
    @Test void kamikazeHasOneCauseAndSeparateDeathsWithoutNationDeathEvents() {
        World w=world();var t=w.tanks.get(0);var victim=w.tanks.get(1);t.nation=6;t.x=t.y=20;victim.x=22;victim.y=20;
        assertEquals(Protocol.Error.OK,w.useSkill(0,0));while(w.tick<GameRules.KamikazeTicks)w.step(Map.of());
        var cause=w.skills.effects.stream().filter(e->e.kind==NationSkills.KAMIKAZE).toList();assertEquals(1,cause.size());assertEquals(2,count(w,DEATH));
        for(var e:w.combat.events.values())if(e.kind()==DAMAGE||e.kind()==DEATH) {assertEquals(3,e.causeKind());assertEquals(cause.getFirst().id,e.causeId());}
    }
    @Test void heDirectAndAreaDamageShareExplosionAndDoNotEmitCombatContact() {
        World w=world();var t=w.tanks.get(1);t.x=11;t.y=10;
        var b=new ItemGameplayTest().bullet(w,WorldItems.HE_BULLET,10,10,0);w.advance(b);
        assertEquals(74,t.hp);assertEquals(0,count(w,IMPACT));assertEquals(0,count(w,BOUNCE));assertEquals(3,count(w,DAMAGE)); // Includes the shooter's existing HE splash damage.
        assertEquals(2,w.combat.events.values().stream().filter(e->e.kind()==DAMAGE&&e.targetSlot()==1).count());
        int id=w.items.effects.getFirst().id;
        for(var e:w.combat.events.values()) {assertEquals(WorldItems.EXPLOSION,e.causeKind());assertEquals(id,e.causeId());}
    }
    @Test void multipleHeBouncesKeepDistinctExistingExplosionIdsWithoutExtraContactEvents() {
        World w=world();w.tanks.clear();var helper=new ItemGameplayTest();
        float extent=GameRules.BulletHalfLength*GameRules.HighExplosiveSizePercent/100f;
        w.map.walls=new GameMap.Rect[]{helper.rect(9,0,1,30,0),helper.rect(10+2*extent+.2f,0,1,30,0)};
        var b=helper.bullet(w,WorldItems.HE_BULLET,10+extent+.1f,10,0);w.advance(b);
        assertEquals(3,w.items.effects.size());assertEquals(3,w.items.effects.stream().map(e->e.id).distinct().count());
        assertEquals(2,b.bounces);assertFalse(w.bullets.containsKey(b.id));assertTrue(w.combat.events.isEmpty());
    }
    @Test void removalAndLifetimeExpiryDoNotInventContactsOrDeathAndRejoinChangesLife() {
        World w=world();int life=w.tanks.get(0).life;
        new ItemGameplayTest().bullet(w,0,25,25,0);w.remove(0);assertTrue(w.bullets.isEmpty());assertTrue(w.combat.events.isEmpty());
        w.add(0);assertNotEquals(life,w.tanks.get(0).life);
        var b=new ItemGameplayTest().bullet(w,0,25,25,0);b.born=-GameRules.BulletLifetimeTicks;w.step(Map.of());assertFalse(w.bullets.containsKey(b.id));assertTrue(w.combat.events.isEmpty());
    }
    @Test void dyingBeforeKamikazeDeadlineDoesNotCreateASkillExplosion() {
        World w=world();w.tanks.get(0).nation=6;w.useSkill(0,0);w.damage(w.tanks.get(0),1,100);
        assertEquals(1,count(w,DEATH));assertEquals(0,w.combat.events.lastEntry().getValue().causeKind());
        while(w.tick<=GameRules.KamikazeTicks)w.step(Map.of());assertTrue(w.skills.effects.stream().noneMatch(e->e.kind==NationSkills.KAMIKAZE));
    }
    @Test void mirrorSplitOwnsContactButNormalReflectionHasOneBounce() {
        for(int kind:new int[]{WorldItems.NORMAL_BULLET,WorldItems.MIRROR_BULLET}) {
            World w=world();w.tanks.clear();var wall=new ItemGameplayTest().rect(11,0,1,30,0);w.map.walls=new GameMap.Rect[]{wall};
            var b=new ItemGameplayTest().bullet(w,kind,10.5f,10,0);w.advance(b);
            assertEquals(0,count(w,IMPACT));assertEquals(kind==0?1:0,count(w,BOUNCE));assertEquals(kind==0?0:1,w.items.effects.size());
        }
    }
    @Test void brickTransitionIsRecordedOnceAfterSimulation() {
        World w=world();w.tanks.clear();var r=new ItemGameplayTest().rect(11,0,1,30,1);w.map.bricks=new GameMap.Rect[]{r};w.bricks[0]=1;
        new ItemGameplayTest().bullet(w,0,10.5f,10,0);w.step(Map.of());assertEquals(0,w.bricks[0]);assertEquals(1,count(w,BRICK_DESTROYED));
        w.step(Map.of());assertEquals(1,count(w,BRICK_DESTROYED));
    }
    @Test void retentionAndOverflowAreBoundedInBothGroups() {
        World w=world();for(int i=0;i<400;i++)add(w,SHOT);assertEquals(160,w.combat.events.size());assertEquals(240,w.combat.decorativeDropped);
        for(int i=0;i<110;i++)add(w,DEATH);assertEquals(256,w.combat.events.size());assertEquals(14,w.combat.criticalDropped);assertEquals(96,count(w,DEATH));
        w.combat.prune(89);assertEquals(256,w.combat.events.size());w.combat.prune(90);assertTrue(w.combat.events.isEmpty());assertTrue(world().combat.events.isEmpty());
    }
    @Test void lostSnapshotIsRecoveredFromAcknowledgedBaseAndMalformedDeltaIsAtomic()throws Exception {
        World w=world();byte[] base=w.encode();add(w,SHOT);byte[] dropped=w.encode();w.tick++;add(w,DEATH);byte[] current=w.encode();
        byte[] delta=DeltaCodec.encode(base,current);assertArrayEquals(current,DeltaCodec.apply(base,delta));assertEquals(2,World.decode(current).combat.events.size());
        assertThrows(RuntimeException.class,()->DeltaCodec.apply(base,Arrays.copyOf(delta,delta.length-1)));assertArrayEquals(base,World.decode(base).encode());
        Event e=w.combat.events.firstEntry().getValue();Writer record=new Writer();e.write(record);assertEquals(43,record.done().length);
        List<String> rows=new ArrayList<>();var hex=HexFormat.of();rows.add("FULL\t"+hex.formatHex(base));rows.add("DROPPED\t"+hex.formatHex(DeltaCodec.encode(base,dropped)));
        rows.add("DELTA\t"+hex.formatHex(delta)+"\t"+hex.formatHex(current));
        w.combat.prune(100);byte[] expired=w.encode();rows.add("DELTA\t"+hex.formatHex(DeltaCodec.encode(current,expired))+"\t"+hex.formatHex(expired));
        Path path=Path.of("target/interop/combat.tsv");Files.createDirectories(path.getParent());Files.write(path,rows);
    }
    @Test void invalidRecordsAndMutationAreRejected() {
        assertThrows(IllegalArgumentException.class,()->new Event(1,1,DEATH,0,1,1,2,0,65535,0,0,0,0,100,3,0));
        assertThrows(IllegalArgumentException.class,()->new Event(1,1,DEATH,255,1,1,2,0,65535,0,0,0,0,100,0,0));
        World w=world();add(w,SHOT);byte[] base=w.encode();var e=w.combat.events.firstEntry().getValue();
        w.combat.events.put(e.id(),new Event(e.id(),e.tick(),e.kind(),e.sourceSlot(),e.targetSlot(),e.sourceLife(),e.targetLife(),e.bulletId(),e.objectId(),e.surface(),11,10,0,0,0,0));
        assertThrows(IllegalArgumentException.class,()->DeltaCodec.encode(base,w.encode()));
    }
    @Test void everyKindAndCauseRoundTripsAcrossFullAndDelta()throws Exception {
        World w=world();byte[] base=w.encode();w.tick=20;
        for(int kind=SHOT;kind<=BRICK_DESTROYED;kind++)w.combat.add(w.tick,kind,w.tanks.get(0),kind==DAMAGE||kind==DEATH?w.tanks.get(1):null,kind==BRICK_DESTROYED?0:1,kind==BRICK_DESTROYED?0:65535,kind==BRICK_DESTROYED?2:0,10,10,35999,kind==DAMAGE?10:kind==DEATH?100:0,0,0);
        for(int cause:new int[]{1,2,3,4,10}) {
            int id;
            if(cause==10) {w.items.event(10,0,10,10,2,0);id=w.items.effects.getLast().id;}
            else id=w.skills.effect(cause,0,10,20,10,10,2,0).id;
            for(int kind:new int[]{DAMAGE,DEATH})w.combat.add(w.tick,kind,w.tanks.get(0),w.tanks.get(1),0,65535,0,12,10,0,kind==DAMAGE?20:100,cause,id);
        }
        byte[] current=w.encode(),delta=DeltaCodec.encode(base,current);assertArrayEquals(current,DeltaCodec.apply(base,delta));
        var hex=HexFormat.of();Path path=Path.of("target/interop/combat-all.tsv");Files.createDirectories(path.getParent());
        Files.write(path,List.of("FULL\t"+hex.formatHex(base),"DELTA\t"+hex.formatHex(delta)+"\t"+hex.formatHex(current)));
    }
    @Test void worstCaseFullAndReplacementDeltaIncludingAllExtensionsFitBudgets()throws Exception {
        World w=new RoleplayCodecTest().maximum();for(int slot=0;slot<8;slot++)w.tanks.get(slot).life=slot+1;
        for(int i=0;i<192;i++)w.skills.effect(1,i%8,1,100,10,10,3,0);
        for(int i=0;i<256;i++)w.items.event(10,i%8,10,10,2,0);
        for(int i=1;i<=6;i++)new ItemCodecTest().item(w,i,i-1);
        for(int i=0;i<160;i++)add(w,SHOT);for(int i=0;i<96;i++)add(w,DEATH);
        w.battlefield.wave=new WorldBattlefield.Wave(1,3,0,60,300,List.of(0,1,2,3,4,5,6,7,8,9,0,1,2,3,4));
        byte[] base=w.encode();assertEquals(25262,base.length);
        w.combat.events.clear();for(int i=0;i<160;i++)add(w,SHOT);for(int i=0;i<96;i++)add(w,DEATH);
        w.bullets.clear();for(int i=1000;i<1128;i++) {var b=new World.Bullet();b.id=i;w.bullets.put(i,b);}
        w.battlefield.wave=new WorldBattlefield.Wave(2,3,60,120,360,List.of(5,6,7,8,9,0,1,2,3,4,5,6,7,8,9));
        byte[] current=w.encode(),delta=DeltaCodec.encode(base,current);assertArrayEquals(current,DeltaCodec.apply(base,delta));
        List<String> rows=new ArrayList<>();
        for(int op:new int[]{FULL,DELTA}) {
            int id=op==FULL?1:2;
            byte[] payload=new Writer().u64(7).u32(id).u32(1).u64(1000).u32(1).u16(18000).bytes(op==FULL?base:new Writer().u32(1).bytes(delta).done()).done();
            assertTrue(payload.length<=32768);for(byte[] part:SnapshotFragments.split(7,id,op,payload)) {
                byte[] packet=frame(SNAPSHOT_PART,id,part);assertTrue(packet.length<=1200);
                rows.add((op==FULL?"FULL":"DELTA")+"\t"+HexFormat.of().formatHex(packet));
            }
        }
        rows.add("EXPECTED\t"+HexFormat.of().formatHex(current));Path path=Path.of("target/interop/combat-fragments.tsv");Files.createDirectories(path.getParent());Files.write(path,rows);
    }
}
