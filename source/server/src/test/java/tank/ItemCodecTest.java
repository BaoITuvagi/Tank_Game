package tank;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;
class ItemCodecTest {
    World world() {
        World w=new World(null,0);for(int slot=0;slot<8;slot++) {var t=new World.Tank();t.slot=slot;t.life=slot+1;w.tanks.put(slot,t);}return w;
    }
    void item(World w,int id,int type) {var i=new WorldItems.Item();i.id=id;i.type=type;i.expires=450;i.x=id*5;i.y=id*7;w.items.items.put(id,i);}
    @Test void roleplayWavesSpawnAndExpireInFullAndDeltaSnapshots() throws Exception {
        World w=new World(GameMap.load(Path.of("../../source/client/Assets/StreamingAssets/map-large.json")),31);w.items.configure(true);
        w.add(0);w.add(1);w.arrangeInitialSpawns(null);var rows=new ArrayList<String>();byte[] base=null;int id=0;
        for(int tick:new int[]{1199,1200,1799,1800,2399,2400,3000}) {
            while(w.tick<tick)w.step(Map.of());
            assertEquals(Set.of(0,1),w.tanks.keySet());assertEquals(w.items.wave()==0?0:4,w.items.items.size());
            for(var i:w.items.items.values())assertEquals(w.items.wave()*1200+600,i.expires);
            byte[] full=w.encode();
            boolean delta=base!=null&&id%2==1;int baseId=id;id++;
            Writer payload=new Writer().u64(77).u32(id).u32(tick).u64(tick*1_000_000L/30).u32(tick).u16(5400-tick);
            if(delta)payload.u32(baseId).bytes(DeltaCodec.encode(base,full));else payload.bytes(full);
            rows.add(tick+"\t"+HexFormat.of().formatHex(frame(delta?DELTA:FULL,id,payload.done()))+"\t"+HexFormat.of().formatHex(full));
            base=full;
        }
        Path path=Path.of("target/interop/roleplay-item-first-spawn.tsv");Files.createDirectories(path.getParent());Files.write(path,rows);
    }
    @Test void firstSixItemSpawnKeepsBothPlayersInFullAndDeltaSnapshots() throws Exception {
        World w=new World(GameMap.load(Path.of("../../source/client/Assets/StreamingAssets/map-large.json")),31);
        w.add(0);w.add(1);w.arrangeInitialSpawns(null);var rows=new ArrayList<String>();byte[] base=null;int id=0;
        for(int tick:new int[]{299,300,301,330,600,750}) {
            while(w.tick<tick)w.step(Map.of());
            assertEquals(Set.of(0,1),w.tanks.keySet());assertEquals(tick<300?0:6,w.items.items.size());
            byte[] full=w.encode();boolean delta=base!=null&&id%2==1;int baseId=id;id++;
            Writer payload=new Writer().u64(77).u32(id).u32(tick).u64(tick*1_000_000L/30).u32(tick).u16(1800-tick);
            if(delta)payload.u32(baseId).bytes(DeltaCodec.encode(base,full));else payload.bytes(full);
            rows.add(tick+"\t"+HexFormat.of().formatHex(frame(delta?DELTA:FULL,id,payload.done()))+"\t"+HexFormat.of().formatHex(full));
            base=full;
        }
        Path path=Path.of("target/interop/item-first-spawn.tsv");Files.createDirectories(path.getParent());Files.write(path,rows);
    }
    @Test void fullDeltaAndRejoinRepairDroppedItemsBuffsVisionAndCooldown() throws Exception {
        World w=world();var t=w.tanks.get(0);t.qReadyTick=540;t.qBaseReadyTick=240;t.fogEndTick=300;t.qPenalty=300;t.armorEndTick=180;t.invincibilityEndTick=300;
        item(w,1,0);item(w,2,1);byte[] base=w.encode();var rows=new ArrayList<String>();rows.add("FULL\t"+HexFormat.of().formatHex(base));
        w.tick=90;w.items.items.remove(1);w.items.apply(t,WorldItems.MIRROR);w.skills.debuff(t,1,1,300);w.items.apply(t,WorldItems.PURIFICATION);
        byte[] missed=w.encode();rows.add("DROPPED\t"+HexFormat.of().formatHex(DeltaCodec.encode(base,missed)));
        item(w,3,4);item(w,4,5);
        for(int id=1;id<=2;id++) {var b=new World.Bullet();b.id=id;b.kind=id==1?WorldItems.HE_BULLET:WorldItems.MIRROR_CHILD;b.damage=id==1?20:10;w.bullets.put(id,b);}
        byte[] current=w.encode(),delta=DeltaCodec.encode(base,current);assertArrayEquals(current,DeltaCodec.apply(base,delta));
        rows.add("DELTA\t"+HexFormat.of().formatHex(delta)+"\t"+HexFormat.of().formatHex(current));rows.add("REJOIN\t"+HexFormat.of().formatHex(current));
        Path path=Path.of("target/interop/items.tsv");Files.createDirectories(path.getParent());Files.write(path,rows);
        var decoded=World.decode(current);assertEquals(90,decoded.tanks.get(0).fogEndTick);assertEquals(240,decoded.tanks.get(0).qReadyTick);assertEquals(3,decoded.tanks.get(0).mirrorShots);
        assertArrayEquals(current,DeltaCodec.apply(base,delta));assertArrayEquals(base,World.decode(base).encode());
    }
    @Test void worstCaseSnapshotAndReplacementDeltaFitAssemblerAndDatagramBudgets() throws Exception {
        World w=world();
        for(int id=1;id<=128;id++) {var b=new World.Bullet();b.id=id;b.owner=id%8;b.kind=3;b.damage=10;w.bullets.put(id,b);}
        for(int id=1;id<=6;id++) {item(w,id,id-1);if(id%2==0)w.items.items.get(id).expires=0;}
        for(int n=0;n<GameRules.MaxSkillEffects;n++)w.skills.effect(1,n%8,100,200,10,10,2,0);
        for(int n=0;n<GameRules.MaxItemEffects;n++)w.items.event(10,n%8,10,10,1.7f,0);
        byte[] base=w.encode();assertEquals(14221,base.length);assertTrue(base.length<32768);
        for(var t:w.tanks.values()) {t.armorEndTick=180;t.invincibilityEndTick=90;t.mirrorShots=3;}
        w.bullets.clear();for(int id=1000;id<1128;id++) {var b=new World.Bullet();b.id=id;w.bullets.put(id,b);}
        byte[] current=w.encode(),delta=DeltaCodec.encode(base,current);assertArrayEquals(current,DeltaCodec.apply(base,delta));assertTrue(delta.length<32768);
        List<String> trace=new ArrayList<>();
        for(int op:new int[]{FULL,DELTA}) {
            int id=op==FULL?1:2;
            byte[] payload=new Writer().u64(7).u32(id).u32(1).u64(1000).u32(1).u16(18000).bytes(op==FULL?base:new Writer().u32(1).bytes(delta).done()).done();
            for(byte[] part:SnapshotFragments.split(7,id,op,payload)) {
                byte[] packet=frame(SNAPSHOT_PART,id,part);assertTrue(packet.length<=1200);
                trace.add((op==FULL?"FULL":"DELTA")+"\t"+HexFormat.of().formatHex(packet));
            }
        }
        trace.add("EXPECTED\t"+HexFormat.of().formatHex(current));
        Path path=Path.of("target/interop/item-fragments.tsv");Files.createDirectories(path.getParent());Files.write(path,trace);
    }
    @Test void germanHeDamageAndKindRepairInFullDeltaAndRejoin() throws Exception {
        World w=world();var b=new World.Bullet();b.id=1;b.damage=GameRules.GermanBulletDamage;w.bullets.put(1,b);
        byte[] base=w.encode();b.kind=WorldItems.HE_BULLET;
        byte[] current=w.encode(),delta=DeltaCodec.encode(base,current);
        assertArrayEquals(current,World.decode(current).encode());assertArrayEquals(current,DeltaCodec.apply(base,delta));
        Path path=Path.of("target/interop/german-he.tsv");Files.createDirectories(path.getParent());
        Files.write(path,List.of("FULL\t"+HexFormat.of().formatHex(base),"DELTA\t"+HexFormat.of().formatHex(delta),"REJOIN\t"+HexFormat.of().formatHex(current)));
    }
    @Test void stackedProjectileKindsAndBuffsRepairDroppedDeltaAndRejoin() throws Exception {
        World w=world();byte[] base=w.encode();var t=w.tanks.get(0);t.heEndTick=150;t.mirrorShots=2;
        var first=new World.Bullet();first.id=w.nextBullet++;first.kind=WorldItems.HE_MIRROR_BULLET;first.damage=15;w.bullets.put(first.id,first);
        byte[] dropped=DeltaCodec.encode(base,w.encode());w.bullets.clear();
        for(int kind=0;kind<=5;kind++)for(boolean german:new boolean[]{false,true}) {
            var b=new World.Bullet();b.id=w.nextBullet++;b.kind=kind;b.damage=WorldItems.isMirrorChild(kind)?(german?25:10):(german?15:20);w.bullets.put(b.id,b);
        }
        w.items.event(WorldItems.EXPLOSION,0,10,10,GameRules.HighExplosiveRadius,0);w.items.event(WorldItems.SPLIT,0,10,10,1,18000);
        byte[] current=w.encode(),delta=DeltaCodec.encode(base,current);assertArrayEquals(current,World.decode(current).encode());assertArrayEquals(current,DeltaCodec.apply(base,delta));
        Path path=Path.of("target/interop/stacked-projectiles.tsv");Files.createDirectories(path.getParent());
        var hex=HexFormat.of();Files.write(path,List.of("FULL\t"+hex.formatHex(base),"DROPPED\t"+hex.formatHex(dropped),"DELTA\t"+hex.formatHex(delta),"REJOIN\t"+hex.formatHex(current)));
    }
    @Test void malformedCountsTypesDuplicatesAndBulletMasksAreRejectedAtomically() {
        World w=world();byte[] base=w.encode();
        var bad=new Writer().u8(0).u8(0).u8(1).u32(1).u16(1<<9).done();assertThrows(IllegalArgumentException.class,()->DeltaCodec.apply(base,bad));
        item(w,1,6);assertThrows(IllegalArgumentException.class,w::encode);w.items.items.get(1).type=1;
        var t=w.tanks.get(0);t.mirrorShots=4;assertThrows(IllegalArgumentException.class,w::encode);t.mirrorShots=0;
        t.armorEndTick=65536;assertThrows(IllegalArgumentException.class,w::encode);t.armorEndTick=0;
        w.items.clear();assertArrayEquals(base,w.encode());
        byte[] full=w.encode();assertThrows(RuntimeException.class,()->World.decode(Arrays.copyOf(full,full.length-1)));
        for(int kind:new int[]{-1,6}) {var b=new World.Bullet();b.kind=kind;assertThrows(IllegalArgumentException.class,()->World.validate(b));}
        var b=new World.Bullet();b.kind=3;b.damage=20;assertThrows(IllegalArgumentException.class,()->World.validate(b));
        for(int version:new int[]{5,6,7,8,9,10,11,12,13,14,15}) {byte[] old=frame(HEARTBEAT,1,new byte[0]);old[2]=(byte)version;assertThrows(IllegalArgumentException.class,()->decode(old,false));}assertEquals(16,GameRules.ProtocolVersion);
    }
}
