using System;
using System.Collections.Generic;
using System.IO;
namespace Tank {
    // Immutable so snapshot copies can safely share records.
    public sealed class CombatEvent {
        public const int Shot=1,Impact=2,Bounce=3,Damage=4,Death=5,BrickDestroyed=6,RecordBytes=43;
        public readonly uint Id,Tick,BulletId,CauseId;
        public readonly int Kind,SourceSlot,TargetSlot,SourceLife,TargetLife,ObjectId,Surface,Angle,Value,CauseKind;
        public readonly float X,Y;
        public CombatEvent(uint id,uint tick,int kind,int sourceSlot,int targetSlot,int sourceLife,int targetLife,uint bulletId,int objectId,int surface,float x,float y,int angle,int value,int causeKind,uint causeId) {
            if(id==0||id>int.MaxValue||tick>int.MaxValue||kind<Shot||kind>BrickDestroyed||!SlotLife(sourceSlot,sourceLife)||!SlotLife(targetSlot,targetLife)
                ||bulletId>int.MaxValue||(objectId!=65535&&(objectId<0||objectId>63))||surface<0||surface>2||float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)
                ||angle<0||angle>=36000||value<0||value>65535||!(causeKind==0||causeKind>=1&&causeKind<=4||causeKind==10||causeKind==11||causeKind==12)
                ||(causeKind==0?causeId!=0:causeId==0||causeId>int.MaxValue)||kind!=Damage&&kind!=Death&&causeKind!=0)throw new InvalidDataException("combat event");
            Id=id;Tick=tick;Kind=kind;SourceSlot=sourceSlot;TargetSlot=targetSlot;SourceLife=sourceLife;TargetLife=targetLife;BulletId=bulletId;ObjectId=objectId;Surface=surface;X=x;Y=y;Angle=angle;Value=value;CauseKind=causeKind;CauseId=causeId;
        }
        static bool SlotLife(int slot,int life) {return slot==255?life==0:slot>=0&&slot<8&&life>0;}
        public bool CauseOwnsExplosion {get {return CauseKind==1||CauseKind==2||CauseKind==3||CauseKind==10||CauseKind==BattlefieldWave.AirstrikeCause;}}
        public bool SameContent(CombatEvent e) {
            return e!=null&&Id==e.Id&&Tick==e.Tick&&Kind==e.Kind&&SourceSlot==e.SourceSlot&&TargetSlot==e.TargetSlot&&SourceLife==e.SourceLife&&TargetLife==e.TargetLife
                &&BulletId==e.BulletId&&ObjectId==e.ObjectId&&Surface==e.Surface&&X==e.X&&Y==e.Y&&Angle==e.Angle&&Value==e.Value&&CauseKind==e.CauseKind&&CauseId==e.CauseId;
        }
        public static CombatEvent Read(WireReader r) {
            return new CombatEvent(r.U32(),r.U32(),r.U8(),r.U8(),r.U8(),(int)r.U32(),(int)r.U32(),r.U32(),r.U16(),r.U8(),r.F32(),r.F32(),r.U16(),r.U16(),r.U8(),r.U32());
        }
        public void Write(WireWriter w) {
            w.U32(Id).U32(Tick).U8(Kind).U8(SourceSlot).U8(TargetSlot).U32((uint)SourceLife).U32((uint)TargetLife).U32(BulletId).U16(ObjectId).U8(Surface).F32(X).F32(Y).U16(Angle).U16(Value).U8(CauseKind).U32(CauseId);
        }
    }
    public static class CombatCodec {
        public static void Read(WorldState world,WireReader r) {
            int count=r.U16();if(count>GameRules.MaxCombatEvents)throw new InvalidDataException("combat count");
            world.Combat.Clear();uint last=0;
            for(int i=0;i<count;i++) {var e=CombatEvent.Read(r);if(e.Id<=last)throw new InvalidDataException("combat order");last=e.Id;world.Combat.Add(e.Id,e);}
        }
        public static void Write(WorldState world,WireWriter w) {
            if(world.Combat.Count>GameRules.MaxCombatEvents)throw new InvalidDataException("combat count");
            w.U16(world.Combat.Count);foreach(var e in world.Combat.Values)e.Write(w);
        }
        public static void Apply(WorldState world,WireReader r) {
            int count=r.U16();if(count>GameRules.MaxCombatEvents)throw new InvalidDataException("combat removals");
            var removed=new HashSet<uint>();
            for(int i=0;i<count;i++) {uint id=r.U32();if(id==0||id>int.MaxValue||!removed.Add(id)||!world.Combat.Remove(id))throw new InvalidDataException("combat removal");}
            count=r.U16();if(count>GameRules.MaxCombatEvents)throw new InvalidDataException("combat additions");uint last=0;
            for(int i=0;i<count;i++) {
                var e=CombatEvent.Read(r);if(e.Id<=last||removed.Contains(e.Id)||world.Combat.ContainsKey(e.Id))throw new InvalidDataException("combat addition");last=e.Id;world.Combat.Add(e.Id,e);
            }
            if(world.Combat.Count>GameRules.MaxCombatEvents)throw new InvalidDataException("combat count");
        }
    }
}
