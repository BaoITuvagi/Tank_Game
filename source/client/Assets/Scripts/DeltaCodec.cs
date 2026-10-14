using System;
using System.Collections.Generic;
using System.IO;
namespace Tank  {
    public static class DeltaCodec  {
        public static WorldState Apply(WorldState original,WireReader r) {
            var w=original.Copy();
            int n=r.U8();
            if(n>8)throw new InvalidDataException("tank count");
            var seen=new HashSet<int>();
            for(int i=0;i<n;i++) {
                int slot=r.U8(),mask=r.U16();
                if(slot>7||(mask&~32767)!=0||mask==0||!seen.Add(slot))throw new InvalidDataException("tank mask");
                TankState old;
                bool exists=w.Tanks.TryGetValue(slot,out old);
                if(!exists&&mask!=32767)throw new InvalidDataException("incomplete tank");
                if(mask==1024) {
                    if(r.Bool())throw new InvalidDataException("presence");
                    w.Tanks.Remove(slot);
                    continue;
                }
                var t=exists?old.Copy():new TankState {
                    Slot=slot
                };
                if((mask&1)!=0)t.X=r.F32();
                if((mask&2)!=0)t.Y=r.F32();
                if((mask&4)!=0)t.Body=r.U16();
                if((mask&8)!=0)t.Turret=r.U16();
                if((mask&16)!=0)t.Hp=r.U8();
                if((mask&32)!=0)t.Kills=r.U16();
                if((mask&64)!=0)t.Deaths=r.U16();
                if((mask&128)!=0)t.Status=r.U8();
                if((mask&256)!=0)t.Respawn=r.U32();
                if((mask&512)!=0)t.Shield=r.U32();
                if((mask&2048)!=0)t.MaxHp=r.U8();
                if((mask&4096)!=0)t.Energy=r.U8();
                if((mask&8192)!=0)t.EnergyRecovery=r.U8();
                if((mask&16384)!=0)t.LastKill=r.U32();
                if((mask&1024)!=0&&!r.Bool())throw new InvalidDataException("presence");
                WorldState.Validate(t);
                w.Tanks[slot]=t;
            }
            n=r.U8();
            if(n>32)throw new InvalidDataException("remove count");
            var removed=new HashSet<uint>();
            for(int i=0;i<n;i++) {
                uint id=r.U32();
                if(!removed.Add(id))throw new InvalidDataException("duplicate removal");
                w.Bullets.Remove(id);
            }
            n=r.U8();
            if(n>32)throw new InvalidDataException("bullet count");
            var ids=new HashSet<uint>();
            for(int i=0;i<n;i++) {
                uint id=r.U32();
                int mask=r.U8();
                if((mask&~31)!=0||mask==0||!ids.Add(id))throw new InvalidDataException("bullet mask");
                BulletState old;
                bool exists=w.Bullets.TryGetValue(id,out old);
                if(!exists&&mask!=31)throw new InvalidDataException("incomplete bullet");
                var b=exists?old.Copy():new BulletState {
                    Id=id
                };
                if((mask&1)!=0)b.Owner=r.U8();
                if((mask&2)!=0)b.X=r.F32();
                if((mask&4)!=0)b.Y=r.F32();
                if((mask&8)!=0)b.Heading=r.U16();
                if((mask&16)!=0)b.Bounces=r.U8();
                WorldState.Validate(b);
                w.Bullets[id]=b;
            }
            n=r.U8();
            if(n>64)throw new InvalidDataException("brick count");
            seen.Clear();
            for(int i=0;i<n;i++) {
                int id=r.U8(),hp=r.U8();
                if(id>63||hp>5||!seen.Add(id))throw new InvalidDataException("brick");
                w.Bricks[id]=(byte)hp;
            }
            if(w.Tanks.Count>8||w.Bullets.Count>32)throw new InvalidDataException("world count");
            return w;
        }
    }
}
