using System;
using System.Collections.Generic;
using System.IO;

namespace Tank.Campaign {
    public enum VisualKind { Melee=1,Shot=2,Skill=3,Hit=4,Block=5,Heal=6,Death=7 }
    public sealed class CampaignVisual {public VisualKind Kind;public int Source,Target,X,Y,TX,TY,Amount;}
    public enum EnemyRole { Melee, Ranged, Support }
    public sealed class Actor {public int Id,X,Y,Hp,MaxHp,Boss,Squad,Guard;public int BossPhase=1;public EnemyRole Role;}
    public sealed class DangerZone {public int Source,X,Y,Due,Damage;}
    public sealed class Pickup {public int X,Y,Kind;}
    public sealed class Ranking {public string Name;public int Score;}
    public sealed class RunState {
        public string Id,Message,SaveStatus;
        public int Stage;public string StageName="";
        public List<DangerZone> Dangers=new List<DangerZone>();
        public int Nation,Turn,X,Y,Distance,Hp,Facing,Shot,Skill,Gold,Kills,DoubleHits,Shields,Bosses,Bottom;
        public bool Finished,Victory,ShopAvailable;
        public int ShotRange=4,MaxBuff=9,ShopRevision;
        public int[] Prices=new int[3];
        public bool CanBuy(int item){return ShopAvailable&&!Finished&&item>=0&&item<3&&Prices[item]>0&&Gold>=Prices[item]&&(item==0?Hp<100:item==1?Shields<=MaxBuff-3:DoubleHits<=MaxBuff-3);}
        public ulong TotalGold;
        public byte[] Terrain,Ground,Decorations;
        public int Template,Biome;public string RegionName="";
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
                    if(r.Remaining>0) {
                        if(r.U8()!=1)throw new InvalidDataException("campaign tactics version");
                        n=r.U16();if(n!=s.Enemies.Count)throw new InvalidDataException("campaign tactics count");
                        var seen=new HashSet<int>();
                        for(int i=0;i<n;i++){
                            int id=(int)r.U32(),role=r.U8(),squad=(int)r.U32(),guard=r.U8();
                            var actor=s.Enemies.Find(e=>e.Id==id);
                            if(actor==null||!seen.Add(id)||role>2||squad<0||guard>10)throw new InvalidDataException("campaign tactics actor");
                            actor.Role=(EnemyRole)role;actor.Squad=squad;actor.Guard=guard;
                        }
                    }
                    if(r.Remaining>0) {
                        if(r.U8()!=1)throw new InvalidDataException("campaign shop version");
                        s.ShotRange=r.U8();s.MaxBuff=r.U8();s.ShopRevision=(int)r.U32();
                        for(int i=0;i<3;i++)s.Prices[i]=(int)r.U32();
                        if(s.ShotRange<1||s.ShotRange>12||s.MaxBuff<3||s.MaxBuff>64||s.ShopRevision<0||System.Array.Exists(s.Prices,p=>p<=0||p>1000000))throw new InvalidDataException("campaign shop metadata");
                        s.ShopAvailable=true;
                    }
                    if(r.Remaining>0){
                        if(r.U8()!=1)throw new InvalidDataException("campaign progression version");
                        s.Stage=r.U8();s.StageName=r.Str(120);if(s.Stage>19)throw new InvalidDataException("campaign stage");
                        int bosses=r.U8();if(bosses>s.Enemies.Count)throw new InvalidDataException("campaign boss count");
                        var seen=new HashSet<int>();
                        for(int i=0;i<bosses;i++){
                            int id=(int)r.U32(),phase=r.U8();var boss=s.Enemies.Find(e=>e.Id==id&&e.Boss>0);
                            if(boss==null||!seen.Add(id)||phase<1||phase>2)throw new InvalidDataException("campaign boss phase");boss.BossPhase=phase;
                        }
                        n=r.U16();if(n>81)throw new InvalidDataException("campaign danger count");
                        var cells=new HashSet<int>();
                        for(int i=0;i<n;i++){
                            var danger=new DangerZone{Source=(int)r.U32(),X=r.U8(),Y=(int)r.U32(),Due=(int)r.U32(),Damage=r.U16()};
                            if(danger.Source<=0||danger.X>=13||danger.Y<0||danger.Y>10000||danger.Due<=s.Turn||danger.Damage<=0||!cells.Add(danger.Y*13+danger.X))throw new InvalidDataException("campaign danger");
                            s.Dangers.Add(danger);
                        }
                    }
                    if(r.Remaining>0){
                        if(r.U8()!=1)throw new InvalidDataException("campaign environment version");
                        s.Template=r.U8();s.Biome=r.U8();s.RegionName=r.Str(120);s.Ground=r.Bytes(13*19);s.Decorations=r.Bytes(13*19);
                        if(s.Template>11||s.Biome>3||Array.Exists(s.Ground,g=>g>5)||Array.Exists(s.Decorations,d=>d>14))throw new InvalidDataException("campaign environment data");
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
            var w=new WireWriter().U8(6).U8(command);
            if(command==0)w.U8(value);
            if(command==1){if(Campaign.Run==null)return;w.U32((uint)Campaign.Run.Turn).U8(value);}
            if(command==5){if(Campaign.Run==null||!Campaign.Run.CanBuy(value))return;w.Str(Campaign.Run.Id).U32((uint)Campaign.Run.Turn).U32((uint)Campaign.Run.ShopRevision).U8(value);}
            Campaign.Error="";Campaign.Pending=Request(Tank.Campaign.CampaignClient.Request,w.Done())!=0;
        }
    }
}
