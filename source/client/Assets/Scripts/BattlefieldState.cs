using System;
using System.Collections.Generic;
using System.IO;
namespace Tank {
    [Serializable] public sealed class BattlefieldZone {public int id;public float x,y;}
    [Serializable] public sealed class BattlefieldMap {
        public BattlefieldZone[] fireZones,airstrikeZones;
        public BattlefieldZone Zone(int kind,int id) {foreach(var z in kind==BattlefieldWave.Fire?fireZones:airstrikeZones)if(z.id==id)return z;throw new InvalidDataException("battlefield zone");}
        public void Validate(GameMap map) {
            if(fireZones==null||airstrikeZones==null)throw new InvalidDataException("battlefield targets");
            var ids=new HashSet<int>();
            foreach(int kind in new[]{BattlefieldWave.Fire,BattlefieldWave.Airstrike}) {
                var zones=kind==BattlefieldWave.Fire?fireZones:airstrikeZones;ids.Clear();
                if(zones.Length<1||zones.Length>64)throw new InvalidDataException("battlefield zones");
                foreach(var z in zones) {
                    float r=GameRules.BattlefieldRadius;
                    if(z==null||z.id<0||z.id>63||!ids.Add(z.id)||float.IsNaN(z.x)||float.IsInfinity(z.x)||float.IsNaN(z.y)||float.IsInfinity(z.y)||z.x-r<0||z.y-r<0||z.x+r>map.width||z.y+r>map.height)throw new InvalidDataException("battlefield zone");
                    foreach(var p in map.spawns)if(Math.Sqrt((z.x-p.x)*(z.x-p.x)+(z.y-p.y)*(z.y-p.y))<r+GameRules.BattlefieldSpawnProtectionRadius+GameRules.TankHalfSize*Math.Sqrt(2))throw new InvalidDataException("battlefield spawn protection");
                    if(kind==BattlefieldWave.Fire)foreach(var rs in new[]{map.walls,map.bricks,map.water})foreach(var rect in rs)
                        if(z.x+r>rect.x&&z.x-r<rect.x+rect.w&&z.y+r>rect.y&&z.y-r<rect.y+rect.h)throw new InvalidDataException("blocked fire zone");
                }
            }
        }
    }
    // Immutable records are safe to share between transactional snapshot copies.
    public sealed class BattlefieldWave {
        // Kind 1 is reserved for the removed collapse event.
        public const int Fire=2,Airstrike=3,FireCause=11,AirstrikeCause=12;
        public readonly uint Id,Start,Impact,End;
        public readonly int Kind;
        public readonly System.Collections.ObjectModel.ReadOnlyCollection<int> Targets;
        public BattlefieldWave(uint id,int kind,uint start,uint impact,uint end,int[] targets) {
            int warning=kind==Fire?GameRules.BattlefieldFireWarningTicks:GameRules.BattlefieldWarningTicks;
            if(id==0||id>int.MaxValue||(kind!=Fire&&kind!=Airstrike)||start>GameRules.MaxMatchTicks||impact!=start+warning||end!=impact+GameRules.BattlefieldEventTicks
                ||targets==null||targets.Length!=GameRules.BattlefieldTargetsPerRound*GameRules.BattlefieldRounds)throw new InvalidDataException("battlefield wave");
            foreach(int target in targets)if(target<0||target>63)throw new InvalidDataException("battlefield target");
            int count=GameRules.BattlefieldTargetsPerRound;
            for(int round=0;round<GameRules.BattlefieldRounds;round++) {
                var seen=new HashSet<int>();
                for(int i=0;i<count;i++) {
                    int target=targets[round*count+i];if(!seen.Add(target))throw new InvalidDataException("battlefield target");
                    if(kind==Airstrike&&round>0)for(int j=0;j<count;j++)if(target==targets[(round-1)*count+j])throw new InvalidDataException("battlefield repeat");
                }
            }
            Id=id;Kind=kind;Start=start;Impact=impact;End=end;Targets=Array.AsReadOnly((int[])targets.Clone());
        }
        public int Rounds {get {return GameRules.BattlefieldRounds;}}
        public int TargetsPerRound {get {return Targets.Count/Rounds;}}
        public int Target(int round,int index) {return Targets[round*TargetsPerRound+index];}
        public uint RoundStart(int round) {return RoundImpact(round)-(Impact-Start);}
        public uint RoundImpact(int round) {return Impact+(uint)(round*(Kind==Airstrike?(GameRules.BattlefieldEventTicks-GameRules.BattlefieldAirstrikeTicks)/(GameRules.BattlefieldRounds-1):GameRules.BattlefieldRoundIntervalTicks));}
        public bool SameContent(BattlefieldWave other) {
            if(other==null||Id!=other.Id||Kind!=other.Kind||Start!=other.Start||Impact!=other.Impact||End!=other.End||Targets.Count!=other.Targets.Count)return false;
            for(int i=0;i<Targets.Count;i++)if(Targets[i]!=other.Targets[i])return false;return true;
        }
        public void Validate(GameMap map,uint tick) {
            if(map.battlefield==null||Start>tick||tick>=End)throw new InvalidDataException("battlefield tick");
            foreach(int id in Targets)map.battlefield.Zone(Kind,id);
            for(int round=0;round<Rounds;round++)for(int i=0;i<TargetsPerRound;i++) {
                var a=map.battlefield.Zone(Kind,Target(round,i));
                for(int previous=Kind==Airstrike?Math.Max(0,round-1):round;previous<=round;previous++)for(int j=0;j<(previous==round?i:TargetsPerRound);j++) {
                    var b=map.battlefield.Zone(Kind,Target(previous,j));
                    if(Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y))<GameRules.BattlefieldAirstrikeSpacing)throw new InvalidDataException("battlefield spacing");
                }
            }
        }
        public static BattlefieldWave Read(WireReader r) {
            uint id=r.U32();int kind=r.U8();uint start=r.U32(),impact=r.U32(),end=r.U32();int count=r.U8();
            if(count!=GameRules.BattlefieldTargetsPerRound*GameRules.BattlefieldRounds)throw new InvalidDataException("battlefield count");
            var targets=new int[count];for(int i=0;i<count;i++)targets[i]=r.U8();return new BattlefieldWave(id,kind,start,impact,end,targets);
        }
        public void Write(WireWriter w) {w.U32(Id).U8(Kind).U32(Start).U32(Impact).U32(End).U8(Targets.Count);foreach(int id in Targets)w.U8(id);}
    }
    public static class BattlefieldCodec {
        public static void Read(WorldState world,WireReader r) {int count=r.U8();if(count>1)throw new InvalidDataException("battlefield count");world.Battlefield=count==0?null:BattlefieldWave.Read(r);}
        public static void Write(WorldState world,WireWriter w) {w.U8(world.Battlefield==null?0:1);if(world.Battlefield!=null)world.Battlefield.Write(w);}
    }
}
