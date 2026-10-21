using System;
using System.Collections.Generic;
using System.IO;
namespace Tank {
    public sealed class SkillEffect {
        public const int Rocket=1,Bomb=2,Kamikaze=3,Beam=4,Radar=5,Sabotage=6,Area=7,Activate=8;
        public uint Id;
        public int Kind,Owner,Start,End,Angle;
        public float X,Y,Radius;
        public static SkillEffect Read(WireReader r) {
            var e=new SkillEffect{Id=r.U32(),Kind=r.U8(),Owner=r.U8(),Start=r.U16(),End=r.U16(),X=r.F32(),Y=r.F32(),Radius=r.F32(),Angle=r.U16()};
            if(e.Id==0||e.Kind<1||e.Kind>8||e.Owner>7||e.End<e.Start||e.Radius<0||float.IsNaN(e.Radius)||float.IsInfinity(e.Radius)||e.Angle>=36000)throw new InvalidDataException("skill effect");
            return e;
        }
        public void Write(WireWriter w) {w.U32(Id).U8(Kind).U8(Owner).U16(Start).U16(End).F32(X).F32(Y).F32(Radius).U16(Angle);}
    }
    public sealed class SnapshotAssembler {
        const int Chunk=1100,MaxBytes=32768;
        sealed class Assembly {
            public int Op,Total,Count,Received;
            public double Since;
            public byte[][] Parts;
        }
        readonly Dictionary<uint,Assembly> pending=new Dictionary<uint,Assembly>();
        public void Clear() {pending.Clear();}
        public Packet Add(Packet packet,ulong match,uint last,double now) {
            var r=new WireReader(packet.Payload);
            ulong incoming=r.U64();uint id=r.U32();int op=r.U8(),index=r.U8(),count=r.U8(),total=r.U16();
            if(incoming!=match||last!=0&&!Protocol.Newer(id,last))return null;
            if((op!=Op.Full&&op!=Op.Delta)||total<30||total>MaxBytes||count!=(total+Chunk-1)/Chunk||index>=count||r.Remaining!=Math.Min(Chunk,total-index*Chunk))throw new InvalidDataException("snapshot fragment");
            var expired=new List<uint>();foreach(var p in pending)if(now-p.Value.Since>500000||last!=0&&!Protocol.Newer(p.Key,last))expired.Add(p.Key);
            foreach(uint key in expired)pending.Remove(key);
            Assembly a;
            if(!pending.TryGetValue(id,out a)) {
                if(pending.Count>=16) {uint oldest=0;double since=double.MaxValue;foreach(var p in pending)if(p.Value.Since<since) {oldest=p.Key;since=p.Value.Since;}pending.Remove(oldest);}
                a=new Assembly{Op=op,Total=total,Count=count,Since=now,Parts=new byte[count][]};pending.Add(id,a);
            }
            if(a.Op!=op||a.Total!=total||a.Count!=count)throw new InvalidDataException("fragment mismatch");
            byte[] part=r.Bytes(r.Remaining);r.End();
            if(a.Parts[index]!=null) {
                if(!System.Linq.Enumerable.SequenceEqual(a.Parts[index],part))throw new InvalidDataException("duplicate fragment mismatch");
                return null;
            }
            a.Parts[index]=part;a.Received++;
            if(a.Received<count)return null;
            var payload=new byte[total];for(int i=0;i<count;i++)Buffer.BlockCopy(a.Parts[i],0,payload,i*Chunk,a.Parts[i].Length);
            pending.Remove(id);
            var header=new WireReader(payload);if(header.U64()!=match||header.U32()!=id)throw new InvalidDataException("fragment header mismatch");
            return new Packet{Op=(byte)op,Sequence=packet.Sequence,Payload=payload};
        }
    }
    public static class SkillGeometry {
        // The arena is clipped to its panel; shake moves the map inside that panel.
        public static bool TryAim(float px,float py,float left,float top,float scale,float width,float height,float shakeX,float shakeY,ref float aimX,ref float aimY) {
            if(scale<=0||px<left||py<top||px>left+width*scale||py>top+height*scale)return false;
            float wx=(px-left-shakeX)/scale,wy=height-(py-top-shakeY)/scale;
            if(wx<0||wy<0||wx>width||wy>height)return false;
            aimX=ClampAim(wx,width);aimY=ClampAim(wy,height);return true;
        }
        public static float ClampAim(float value,float extent) {return Math.Max(GameRules.KatyushaRadius,Math.Min(extent-GameRules.KatyushaRadius,value));}
        public static int HealthPercent(int hp,int maxHp) {return (int)Math.Round(100.0*Math.Max(0,hp)/Math.Max(1,maxHp));}
    }
}
