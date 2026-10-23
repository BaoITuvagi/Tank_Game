package tank;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;
class RoleplayCodecTest {
    World maximum() {
        World w=new World(null,0);
        for(int i=0;i<8;i++) {
            var t=new World.Tank();t.slot=i;t.hp=t.maxHp=500;t.qReadyTick=18900;t.qEndTick=t.slowEndTick=18180;t.protectionFlags=1;
            w.tanks.put(i,t);
        }
        for(int i=1;i<=128;i++) {
            var b=new World.Bullet();b.id=i;b.owner=i%8;b.bounces=2;b.speedPercent=150;b.remainingLifetime=150;
            w.bullets.put(i,b);
        }
        Arrays.fill(w.bricks,(byte)GameRules.BrickMaxHp);
        return w;
    }
    @Test void maximumFramedFullAndDeltaSelectionFitMtu() {
        World w=maximum();byte[] full=w.encode();assertEquals(3354,full.length);
        byte[] metadata=new Writer().u64(1).u32(1).u32(18000).u64(1000).u32(18000).u16(0).done();
        for(byte[] part:SnapshotFragments.split(1,1,FULL,new Writer().bytes(metadata).bytes(full).done()))assertTrue(frame(SNAPSHOT_PART,1,part).length<=1200);
        byte[] empty=new World(null,0).encode(),delta=DeltaCodec.encode(empty,full);
        assertArrayEquals(full,DeltaCodec.apply(empty,delta));
        assertTrue(delta.length+4>=full.length,"Full must win when adding every entity");
        w.tanks.get(0).qEndTick=18000;w.tanks.get(0).protectionFlags=0;
        byte[] changed=w.encode(),small=DeltaCodec.encode(full,changed);
        assertTrue(small.length+4<changed.length);
        assertTrue(frame(DELTA,2,new Writer().bytes(metadata).u32(1).bytes(small).done()).length<=1200);
    }
    @Test void highMaskBitsRestoreAllEffectsAndProjectileProperties() throws Exception {
        World initial=maximum();byte[] base=initial.encode();
        var t=initial.tanks.get(0);t.qReadyTick=19000;t.qEndTick=51;t.slowEndTick=51;t.protectionFlags=0;
        var b=initial.bullets.get(1);b.speedPercent=140;b.bounces=1;b.remainingLifetime=70;
        byte[] current=initial.encode(),delta=DeltaCodec.encode(base,current);
        assertArrayEquals(current,DeltaCodec.apply(base,delta));
        var decoded=World.decode(current);assertEquals(51,decoded.tanks.get(0).qEndTick);assertEquals(0,decoded.tanks.get(0).protectionFlags);assertEquals(140,decoded.bullets.get(1).speedPercent);
        assertEquals(19000,t.copy().qReadyTick);assertEquals(70,b.copy().remainingLifetime);
        Path path=Path.of("target/interop/roleplay-trace.tsv");Files.createDirectories(path.getParent());
        Files.write(path,List.of("FULL\t"+HexFormat.of().formatHex(base),"DELTA\t"+HexFormat.of().formatHex(delta)+"\t"+HexFormat.of().formatHex(current)));
    }
    @Test void invalidStatesAndMalformedDeltaDoNotEscapeValidation() {
        World w=maximum();byte[] base=w.encode();
        assertThrows(IllegalArgumentException.class,()->DeltaCodec.apply(base,new Writer().u8(1).u8(0).u32(1<<27).done()));
        assertThrows(IllegalArgumentException.class,()->DeltaCodec.apply(base,new Writer().u8(0).u8(2).u32(1).u32(1).u8(0).u8(0).done()));
        assertArrayEquals(base,w.encode());
        w.tanks.get(0).protectionFlags=4;assertThrows(IllegalArgumentException.class,w::encode);w.tanks.get(0).protectionFlags=0;
        w.tanks.get(0).qReadyTick=65536;assertThrows(IllegalArgumentException.class,w::encode);w.tanks.get(0).qReadyTick=0;
        w.bullets.get(1).speedPercent=151;assertThrows(IllegalArgumentException.class,w::encode);
    }
    @Test void versionThreeCannotBeMistakenForFour() {
        byte[] old=frame(HEARTBEAT,1,new byte[0]);old[2]=3;
        assertThrows(IllegalArgumentException.class,()->decode(old,false));
    }
    @Test void fragmentedFullAndDeltaRestoreEffectsHealthAndMaximumBullets() throws Exception {
        World w=maximum();var t=w.tanks.get(0);t.qStage=1;t.confusionEndTick=18090;t.fireLockEndTick=18075;t.fogEndTick=18150;t.aimX=6.8f;t.aimY=53.2f;
        for(int i=0;i<72;i++)w.skills.effect(NationSkills.ROCKET,i%8,18010,18025,i%60,i%60,1.7f,0);
        byte[] full=w.encode();List<String> rows=new ArrayList<>();
        byte[] metadata=new Writer().u64(7).u32(1).u32(18000).u64(1000).u32(18000).u16(0).done();
        for(byte[] part:SnapshotFragments.split(7,1,FULL,new Writer().bytes(metadata).bytes(full).done())) {
            byte[] datagram=frame(SNAPSHOT_PART,1,part);assertTrue(datagram.length<=1200);rows.add("FULL\t"+HexFormat.of().formatHex(datagram));
        }
        w.bullets.clear();for(int i=1000;i<1128;i++) {var b=new World.Bullet();b.id=i;w.bullets.put(i,b);}
        t.hp=300;t.qReadyTick=19029;t.qStage=0;w.skills.effects.clear();
        byte[] next=w.encode(),delta=DeltaCodec.encode(full,next);assertArrayEquals(next,DeltaCodec.apply(full,delta));
        metadata=new Writer().u64(7).u32(2).u32(18001).u64(2000).u32(18001).u16(0).u32(1).done();
        for(byte[] part:SnapshotFragments.split(7,2,DELTA,new Writer().bytes(metadata).bytes(delta).done())) {
            byte[] datagram=frame(SNAPSHOT_PART,2,part);assertTrue(datagram.length<=1200);rows.add("DELTA\t"+HexFormat.of().formatHex(datagram));
        }
        Path path=Path.of("target/interop/skill-fragments.tsv");Files.createDirectories(path.getParent());Files.write(path,rows);
        assertThrows(IllegalArgumentException.class,()->SnapshotFragments.split(7,3,FULL,new byte[32769]));
    }
}
