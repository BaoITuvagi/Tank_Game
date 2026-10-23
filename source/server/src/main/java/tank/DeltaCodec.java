package tank;
import java.util.*;
import static tank.Protocol.*;
/** Compare encoded field bits; receiver applies transactionally to a private full-state copy. */
public final class DeltaCodec  {
    static final int[] TANK_SIZES= {
        4,4,2,2,2,2,2,1,4,4,2,1,1,4,2,2,2,1,2,2,2,2,1,4,4,4
    };
    static final int[] BULLET_SIZES= {
        1,4,4,2,1,1,2,1,1
    };
    static final int TANK_MASK=0x7ffffff,BULLET_MASK=511;
    static byte[][] split(byte[] record,int skip,int[] sizes) {
        byte[][] fields=new byte[sizes.length][];
        for(int i=0;i<sizes.length;i++) {
            fields[i]=Arrays.copyOfRange(record,skip,skip+sizes[i]);
            skip+=sizes[i];
        }
        return fields;
    }
    static byte[][] tank(World.Tank t) {
        Writer w=new Writer();
        World.writeTank(w,t);
        return split(w.done(),1,TANK_SIZES);
    }
    static byte[][] bullet(World.Bullet b) {
        Writer w=new Writer();
        World.writeBullet(w,b);
        return split(w.done(),4,BULLET_SIZES);
    }
    static int mask(byte[][] a,byte[][] b,boolean tank) {
        int mask=0;
        for(int i=0;i<a.length;i++)if(b==null||!Arrays.equals(a[i],b[i]))mask|=1<<(tank&&i>=10?i+1:i);
        return mask;
    }
    public static byte[] encode(byte[] base,byte[] current) {
        World a=World.decode(base),b=World.decode(current);
        Writer out=new Writer(),changes=new Writer();
        int count=0;
        Set<Integer> slots=new TreeSet<>(a.tanks.keySet());
        slots.addAll(b.tanks.keySet());
        for(int slot:slots) {
            var t=b.tanks.get(slot);
            var old=a.tanks.get(slot);
            if(t==null) {
                changes.u8(slot).u32(1<<10).u8(0);
                count++;
                continue;
            }
            byte[][] fields=tank(t);
            int m=mask(fields,old==null?null:tank(old),true);
            if(old==null)m|=1<<10;
            if(m==0)continue;
            changes.u8(slot).u32(m);
            for(int i=0;i<TANK_SIZES.length;i++)if((m&(1<<(i>=10?i+1:i)))!=0)changes.bytes(fields[i]);
            if((m&(1<<10))!=0)changes.u8(1);
            count++;
        }
        out.u8(count).bytes(changes.done());
        List<Integer> removed=a.bullets.keySet().stream().filter(id->!b.bullets.containsKey(id)).toList();
        out.u8(removed.size());
        removed.forEach(out::u32);
        changes=new Writer();
        count=0;
        for(var bullet:b.bullets.values()) {
            var old=a.bullets.get(bullet.id);
            byte[][] fields=bullet(bullet);
            int m=mask(fields,old==null?null:bullet(old),false);
            if(m==0)continue;
            changes.u32(bullet.id).u16(m);
            for(int i=0;i<BULLET_SIZES.length;i++)if((m&(1<<i))!=0)changes.bytes(fields[i]);
            count++;
        }
        out.u8(count).bytes(changes.done());
        changes=new Writer();
        count=0;
        for(int i=0;i<64;i++)if(a.bricks[i]!=b.bricks[i]) {
            changes.u8(i).u8(b.bricks[i]);
            count++;
        }
        return out.u8(count).bytes(changes.done()).bytes(b.skills.encode()).bytes(b.items.encode()).bytes(b.combat.delta(a.combat)).done();
    }
    public static byte[] apply(byte[] base,byte[] delta) {
        World w=World.decode(base);
        Reader r=new Reader(delta);
        int n=r.u8();
        if(n>8)throw new IllegalArgumentException("tank changes");
        Set<Integer> seen=new HashSet<>();
        for(int i=0;i<n;i++) {
            int slot=r.u8(),mask=r.u32();
            if(slot>7||(mask&~TANK_MASK)!=0||mask==0||!seen.add(slot))throw new IllegalArgumentException("tank mask");
            World.Tank old=w.tanks.get(slot);
            if(old==null&&mask!=TANK_MASK)throw new IllegalArgumentException("new tank incomplete");
            if(mask==1024) {
                if(r.bool())throw new IllegalArgumentException("presence");
                w.tanks.remove(slot);
                continue;
            }
            var t=old==null?new World.Tank():old.copy();
            t.slot=slot;
            if((mask&1)!=0)t.x=r.f32();
            if((mask&2)!=0)t.y=r.f32();
            if((mask&4)!=0)t.body=r.u16();
            if((mask&8)!=0)t.turret=r.u16();
            if((mask&16)!=0)t.hp=r.u16();
            if((mask&32)!=0)t.kills=r.u16();
            if((mask&64)!=0)t.deaths=r.u16();
            if((mask&128)!=0)t.status=r.u8();
            if((mask&256)!=0)t.respawn=r.u32();
            if((mask&512)!=0)t.shield=r.u32();
            if((mask&2048)!=0)t.maxHp=r.u16();
            if((mask&4096)!=0)t.energy=r.u8();
            if((mask&8192)!=0)t.energyRecovery=r.u8();
            if((mask&16384)!=0)t.lastKill=r.u32();
            if((mask&32768)!=0)t.qReadyTick=r.u16();
            if((mask&65536)!=0)t.qEndTick=r.u16();
            if((mask&131072)!=0)t.slowEndTick=r.u16();
            if((mask&262144)!=0)t.protectionFlags=r.u8();
            if((mask&(1<<19))!=0)t.qStartTick=r.u16();
            if((mask&(1<<20))!=0)t.confusionEndTick=r.u16();
            if((mask&(1<<21))!=0)t.fireLockEndTick=r.u16();
            if((mask&(1<<22))!=0)t.fogEndTick=r.u16();
            if((mask&(1<<23))!=0)t.qStage=r.u8();
            if((mask&(1<<24))!=0)t.aimX=r.f32();
            if((mask&(1<<25))!=0)t.aimY=r.f32();
            if((mask&(1<<26))!=0)t.life=r.u32();
            if((mask&1024)!=0&&!r.bool())throw new IllegalArgumentException("presence");
            w.tanks.put(slot,t);
        }
        n=r.u8();
        if(n>GameRules.MaxBullets)throw new IllegalArgumentException("remove count");
        Set<Integer> removed=new HashSet<>();
        for(int i=0;i<n;i++) {
            int id=r.u32();
            if(!removed.add(id))throw new IllegalArgumentException("duplicate removal");
            w.bullets.remove(id);
        }
        n=r.u8();
        if(n>GameRules.MaxBullets)throw new IllegalArgumentException("bullet count");
        Set<Integer> ids=new HashSet<>();
        for(int i=0;i<n;i++) {
            int id=r.u32(),mask=r.u16();
            if(mask==0||(mask&~BULLET_MASK)!=0||!ids.add(id))throw new IllegalArgumentException("bullet mask");
            var old=w.bullets.get(id);
            if(old==null&&mask!=BULLET_MASK)throw new IllegalArgumentException("new bullet incomplete");
            var b=old==null?new World.Bullet():old.copy();
            b.id=id;
            if((mask&1)!=0)b.owner=r.u8();
            if((mask&2)!=0)b.x=r.f32();
            if((mask&4)!=0)b.y=r.f32();
            if((mask&8)!=0)b.heading=r.u16();
            if((mask&16)!=0)b.bounces=r.u8();
            if((mask&32)!=0)b.speedPercent=r.u8();
            if((mask&64)!=0)b.remainingLifetime=r.u16();
            if((mask&128)!=0)b.damage=r.u8();
            if((mask&256)!=0)b.kind=r.u8();
            w.bullets.put(id,b);
        }
        n=r.u8();
        if(n>64)throw new IllegalArgumentException("bricks");
        seen.clear();
        for(int i=0;i<n;i++) {
            int id=r.u8(),hp=r.u8();
            if(id>63||hp>GameRules.BrickMaxHp||!seen.add(id))throw new IllegalArgumentException("brick");
            w.bricks[id]=(byte)hp;
        }
        w.skills.read(r);
        w.items.read(r);
        w.combat.apply(r);
        r.end();
        byte[] full=w.encode();
        World.decode(full);
        return full;
    }
}
