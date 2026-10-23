using System;
using System.Collections.Generic;
using System.IO;

namespace Tank.Campaign {
    public enum VisualKind { Melee=1,Shot=2,Skill=3,Hit=4,Block=5,Heal=6,Death=7 }
    public sealed class CampaignVisual {public VisualKind Kind;public int Source,Target,X,Y,TX,TY,Amount;}
    public sealed class Actor {public int Id,X,Y,Hp,MaxHp,Boss;}
    public sealed class Pickup {public int X,Y,Kind;}
    public sealed class Ranking {public string Name;public int Score;}
    public sealed class RunState {
        public string Id,Message,SaveStatus;
        public int Nation,Turn,X,Y,Distance,Hp,Facing,Shot,Skill,Gold,Kills,DoubleHits,Shields,Bosses,Bottom;
        public bool Finished,Victory;
        public ulong TotalGold;
        public byte[] Terrain;
        public List<CampaignVisual> Visuals=new List<CampaignVisual>();
        public List<Actor> Enemies=new List<Actor>();
        public List<Pickup> Loot=new List<Pickup>();
    }
    public sealed class CampaignClient {
        public const byte Request=90,Response=91;
        public RunState Run;
        public readonly List<Ranking> Rankings=new List<Ranking>();
        public ulong TotalGold;
        public string Error="";
        public bool Pending;
        public int Revision;
        public void Read(WireReader r) {
            Pending=false;
            if(r.U16()!=0){Error=r.Str();r.End();return;}
            Error="";
            if(r.U8()!=1)throw new InvalidDataException("campaign version");
            int kind=r.U8();
            if(kind==1){TotalGold=r.U64();int n=r.U8();Rankings.Clear();for(int i=0;i<n;i++)Rankings.Add(new Ranking{Name=r.Str(96),Score=(int)r.U32()});}
            else if(kind==0){
                if(r.U8()==0)Run=null;
                else {
                    var s=new RunState {Id=r.Str(),Nation=r.U8(),Turn=(int)r.U32(),X=r.U8(),Y=(int)r.U32(),Distance=(int)r.U32(),Hp=r.U16(),Facing=r.U8(),Shot=r.U8(),Skill=r.U8(),Gold=(int)r.U32(),Kills=(int)r.U32(),DoubleHits=r.U8(),Shields=r.U8(),Bosses=r.U8(),Finished=r.Bool(),Victory=r.Bool(),Message=r.Str(),SaveStatus=r.Str(),TotalGold=r.U64(),Bottom=(int)r.U32(),Terrain=r.Bytes(13*19)};
                    int n=r.U16();if(n>247)throw new InvalidDataException("campaign enemies");
                    for(int i=0;i<n;i++)s.Enemies.Add(new Actor{Id=(int)r.U32(),X=r.U8(),Y=(int)r.U32(),Hp=r.U16(),MaxHp=r.U16(),Boss=r.U8()});
                    n=r.U16();if(n>512)throw new InvalidDataException("campaign loot");
                    for(int i=0;i<n;i++)s.Loot.Add(new Pickup{X=r.U8(),Y=(int)r.U32(),Kind=r.U8()});
                    if(r.Remaining>0) {
                        if(r.U8()!=1)throw new InvalidDataException("campaign visual version");
                        n=r.U16();if(n>512)throw new InvalidDataException("campaign visual count");
                        for(int i=0;i<n;i++){
                            int visualKind=r.U8();if(visualKind<1||visualKind>7)throw new InvalidDataException("campaign visual kind");
                            s.Visuals.Add(new CampaignVisual{Kind=(VisualKind)visualKind,Source=unchecked((int)r.U32()),Target=unchecked((int)r.U32()),X=r.U8(),Y=(int)r.U32(),TX=r.U8(),TY=(int)r.U32(),Amount=r.U16()});
                        }
                    }
                    Run=s;TotalGold=s.TotalGold;
                }
            }else throw new InvalidDataException("campaign response");
            r.End();Revision++;
        }
    }
}
namespace Tank {
    public partial class ClientCore {
        public Campaign.CampaignClient Campaign=new Campaign.CampaignClient();
        public void CampaignCommand(int command,int value=0) {
            if(Phase!=ClientPhase.LOBBY||Campaign.Pending)return;
            var w=new WireWriter().U8(2).U8(command);
            if(command==0)w.U8(value);
            if(command==1){if(Campaign.Run==null)return;w.U32((uint)Campaign.Run.Turn).U8(value);}
            Campaign.Error="";Campaign.Pending=Request(Tank.Campaign.CampaignClient.Request,w.Done())!=0;
        }
    }
}
