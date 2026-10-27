using System;
using System.Collections.Generic;
using System.IO;
namespace Tank  {
    public static class CombatGeometry  {
        public const float TankSize=2*GameRules.TankHalfSize,BulletHalfLength=GameRules.BulletHalfLength,BulletHalfWidth=GameRules.BulletHalfWidth;
    }
    [Serializable] public class MapPoint  {
        public float x,y;
    }
    [Serializable] public class MapRect  {
        public int id,kind;
        public float x,y,w,h;
        public bool Contains(float px,float py) {
            return px>=x&&px<x+w&&py>=y&&py<y+h;
        }
    }
    [Serializable] public class GameMap  {
        public int mapId;
        public float width,height;
        public MapRect[] walls,bricks,water;
        public MapPoint[] spawns;
        public MapPoint[] itemSpawns;
        public BattlefieldMap battlefield;
        public void ValidateItemSpawns() {
            if(battlefield!=null)battlefield.Validate(this);
            if(itemSpawns==null||itemSpawns.Length<3||itemSpawns.Length>64)throw new InvalidDataException("item spawns");
            var hp=new byte[64];for(int i=0;i<hp.Length;i++)hp[i]=(byte)GameRules.BrickMaxHp;
            var points=new HashSet<string>();
            foreach(var p in itemSpawns) {
                if(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y)||Blocked(p.x,p.y,hp)||Water(p.x,p.y)||!points.Add(p.x+":"+p.y))throw new InvalidDataException("item spawn");
                foreach(var r in water)if(p.x+GameRules.ItemHalfSize>r.x&&p.x-GameRules.ItemHalfSize<r.x+r.w&&p.y+GameRules.ItemHalfSize>r.y&&p.y-GameRules.ItemHalfSize<r.y+r.h)throw new InvalidDataException("wet item spawn");
            }
        }
        public bool Blocked(float x,float y,byte[] hp) {
            return Blocked(x,y,0,hp);
        }
        public bool Blocked(float x,float y,int body,byte[] hp) {
            double angle=body/100.0*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);
            double extent=GameRules.TankHalfSize*(Math.Abs(c)+Math.Abs(s));
            if(width>0&&height>0&&(x<extent-GameRules.CollisionEpsilon||y<extent-GameRules.CollisionEpsilon||x>width-extent+GameRules.CollisionEpsilon||y>height-extent+GameRules.CollisionEpsilon))return true;
            foreach(var r in walls)if(BodyOverlap(x,y,c,s,r))return true;
            foreach(var r in bricks)if(hp[r.id]>0&&BodyOverlap(x,y,c,s,r))return true;
            return false;
        }
        static bool BodyOverlap(float x,float y,double c,double s,MapRect r) {
            double dx=x-r.x-r.w/2.0,dy=y-r.y-r.h/2.0,h=GameRules.TankHalfSize,e=GameRules.CollisionEpsilon;
            double ac=Math.Abs(c),asine=Math.Abs(s),rw=r.w/2.0,rh=r.h/2.0;
            if(Math.Abs(dx)>=h*(ac+asine)+rw-e||Math.Abs(dy)>=h*(ac+asine)+rh-e)return false;
            return Math.Abs(dx*c+dy*s)<h+rw*ac+rh*asine-e&&Math.Abs(-dx*s+dy*c)<h+rw*asine+rh*ac-e;
        }
        public bool Water(float x,float y) {
            foreach(var r in water)if(r.Contains(x,y))return true;
            return false;
        }
    }
    public class TankState  {
        public int Slot,Body,Turret,Hp,MaxHp=GameRules.DefaultHp,Energy=150,EnergyRecovery,Kills,Deaths,Status;
        public uint Respawn,Shield,LastKill=uint.MaxValue;
        public int QReadyTick,QEndTick,SlowEndTick,ProtectionFlags;
        public int RocketSlowEndTick,RocketSlowStacks;
        public int QStartTick,ConfusionEndTick,FireLockEndTick,FogEndTick,QStage,Life;
        public int ArmorEndTick,HeEndTick,InvincibilityEndTick,MirrorShots,QBaseReadyTick;
        public float AimX,AimY;
        public int Nation=-1;
        public float X,Y;
        public TankState Copy() {
            return (TankState)MemberwiseClone();
        }
    }
    public class BulletState  {
        public uint Id;
        public int Owner,Heading,Bounces;
        public int Kind;
        public int SpeedPercent=100,RemainingLifetime=GameRules.BulletLifetimeTicks,Damage=GameRules.BulletDamage;
        public float X,Y;
        public BulletState Copy() {
            return (BulletState)MemberwiseClone();
        }
    }
    public class WorldState  {
        public SortedDictionary<int,TankState> Tanks=new SortedDictionary<int,TankState>();
        public SortedDictionary<uint,BulletState> Bullets=new SortedDictionary<uint,BulletState>();
        public byte[] Bricks=new byte[64];
        public BattlefieldWave Battlefield;
        public readonly List<SkillEffect> Effects=new List<SkillEffect>();
        public readonly SortedDictionary<uint,ItemState> Items=new SortedDictionary<uint,ItemState>();
        public readonly List<SkillEffect> ItemEffects=new List<SkillEffect>();
        public readonly SortedDictionary<uint,CombatEvent> Combat=new SortedDictionary<uint,CombatEvent>();
        public WorldState Copy() {
            var w=new WorldState();
            foreach(var p in Tanks)w.Tanks.Add(p.Key,p.Value.Copy());
            foreach(var p in Bullets)w.Bullets.Add(p.Key,p.Value.Copy());
            w.Bricks=(byte[])Bricks.Clone();
            w.Battlefield=Battlefield;
            w.Effects.AddRange(Effects);
            foreach(var p in Items)w.Items.Add(p.Key,p.Value.Copy());
            w.ItemEffects.AddRange(ItemEffects);
            foreach(var p in Combat)w.Combat.Add(p.Key,p.Value);
            return w;
        }
        public static TankState ReadTank(WireReader r) {
            var t=new TankState {
                Slot=r.U8(),X=r.F32(),Y=r.F32(),Body=r.U16(),Turret=r.U16(),Hp=r.U16(),Kills=r.U16(),Deaths=r.U16(),Status=r.U8(),Respawn=r.U32(),Shield=r.U32(),MaxHp=r.U16(),Energy=r.U8(),EnergyRecovery=r.U8(),LastKill=r.U32(),QReadyTick=r.U16(),QEndTick=r.U16(),SlowEndTick=r.U16(),ProtectionFlags=r.U8(),QStartTick=r.U16(),ConfusionEndTick=r.U16(),FireLockEndTick=r.U16(),FogEndTick=r.U16(),QStage=r.U8(),AimX=r.F32(),AimY=r.F32(),Life=(int)r.U32(),RocketSlowEndTick=r.U16(),RocketSlowStacks=r.U8()
            };
            Validate(t);
            return t;
        }
        public static void Validate(TankState t) {
            if(t.Slot<0||t.Slot>7||t.Body<0||t.Body>=36000||t.Turret<0||t.Turret>=36000||t.MaxHp<1||t.MaxHp>GameRules.MaxStartHp||t.Hp<0||t.Hp>t.MaxHp||t.Energy<0||t.Energy>GameRules.MaxEnergy||t.EnergyRecovery<0||t.EnergyRecovery>GameRules.EnergyRecoveryDelay||t.Status<0||t.Status>1||float.IsNaN(t.X)||float.IsInfinity(t.X)||float.IsNaN(t.Y)||float.IsInfinity(t.Y))throw new InvalidDataException("tank");
            if(t.QStage<0||t.QStage>1||t.Life<0||float.IsNaN(t.AimX)||float.IsInfinity(t.AimX)||float.IsNaN(t.AimY)||float.IsInfinity(t.AimY))throw new InvalidDataException("skill state");
            foreach(int deadline in new[]{t.QStartTick,t.ConfusionEndTick,t.FireLockEndTick,t.FogEndTick})if(deadline<0||deadline>65535)throw new InvalidDataException("skill deadline");
            if(t.QReadyTick<0||t.QReadyTick>65535||t.QEndTick<0||t.QEndTick>65535||t.SlowEndTick<0||t.SlowEndTick>65535||(t.ProtectionFlags&~1)!=0)throw new InvalidDataException("skill state");
            if(t.RocketSlowEndTick<0||t.RocketSlowEndTick>65535||t.RocketSlowStacks<0||t.RocketSlowStacks>9)throw new InvalidDataException("rocket slow state");
        }
        public static BulletState ReadBullet(WireReader r) {
            var b=new BulletState {
                Id=r.U32(),Owner=r.U8(),X=r.F32(),Y=r.F32(),Heading=r.U16(),Bounces=r.U8(),SpeedPercent=r.U8(),RemainingLifetime=r.U16(),Damage=r.U8(),Kind=r.U8()
            };
            Validate(b);
            return b;
        }
        public static void Validate(BulletState b) {
            if(!ItemCodec.ValidBulletDamage(b.Kind,b.Damage)||b.Owner<0||b.Owner>7||b.Heading<0||b.Heading>=36000||b.Bounces<0||b.Bounces>GameRules.DefaultBulletBounceLimit||b.SpeedPercent<100||b.SpeedPercent>GameRules.MaxBulletSpeedPercent||b.RemainingLifetime<0||b.RemainingLifetime>GameRules.BulletLifetimeTicks||float.IsNaN(b.X)||float.IsInfinity(b.X)||float.IsNaN(b.Y)||float.IsInfinity(b.Y))throw new InvalidDataException("bullet");
        }
        public static WorldState Full(WireReader r) {
            var w=new WorldState();
            int n=r.U8();
            if(n>8)throw new InvalidDataException("tanks");
            for(int i=0;i<n;i++) {
                var t=ReadTank(r);
                w.Tanks.Add(t.Slot,t);
            }
            n=r.U8();
            if(n>GameRules.MaxBullets)throw new InvalidDataException("bullets");
            for(int i=0;i<n;i++) {
                var b=ReadBullet(r);
                w.Bullets.Add(b.Id,b);
            }
            w.Bricks=r.Bytes(64);
            foreach(byte hp in w.Bricks)if(hp>GameRules.BrickMaxHp)throw new InvalidDataException("brick");
            ReadEffects(w,r);
            ItemCodec.Read(w,r);
            CombatCodec.Read(w,r);
            BattlefieldCodec.Read(w,r);
            return w;
        }
        public static void ReadEffects(WorldState w,WireReader r) {
            int count=r.U16();if(count>GameRules.MaxSkillEffects)throw new InvalidDataException("effects");
            w.Effects.Clear();var ids=new HashSet<uint>();
            for(int i=0;i<count;i++) {var e=SkillEffect.Read(r);if(e.Kind>8||!ids.Add(e.Id))throw new InvalidDataException("duplicate effect");w.Effects.Add(e);}
        }
        public byte[] Encode() {
            if(Tanks.Count>8||Bullets.Count>GameRules.MaxBullets||Effects.Count>GameRules.MaxSkillEffects)throw new InvalidDataException("world count");
            var w=new WireWriter().U8(Tanks.Count);
            foreach(var t in Tanks.Values) {
                Validate(t);
                w.U8(t.Slot).F32(t.X).F32(t.Y).U16(t.Body).U16(t.Turret).U16(t.Hp).U16(t.Kills).U16(t.Deaths).U8(t.Status).U32(t.Respawn).U32(t.Shield).U16(t.MaxHp).U8(t.Energy).U8(t.EnergyRecovery).U32(t.LastKill).U16(t.QReadyTick).U16(t.QEndTick).U16(t.SlowEndTick).U8(t.ProtectionFlags).U16(t.QStartTick).U16(t.ConfusionEndTick).U16(t.FireLockEndTick).U16(t.FogEndTick).U8(t.QStage).F32(t.AimX).F32(t.AimY).U32((uint)t.Life).U16(t.RocketSlowEndTick).U8(t.RocketSlowStacks);
            }
            w.U8(Bullets.Count);
            foreach(var b in Bullets.Values) {
                Validate(b);
                w.U32(b.Id).U8(b.Owner).F32(b.X).F32(b.Y).U16(b.Heading).U8(b.Bounces).U8(b.SpeedPercent).U16(b.RemainingLifetime).U8(b.Damage).U8(b.Kind);
            }
            w.Bytes(Bricks).U16(Effects.Count);foreach(var e in Effects)e.Write(w);
            ItemCodec.Write(this,w);
            CombatCodec.Write(this,w);
            BattlefieldCodec.Write(this,w);
            return w.Done();
        }
    }
    public static class LiveRanking  {
        public static List<TankState> Sort(WorldState world) {
            var sorted=new List<TankState>(world.Tanks.Values);
            sorted.Sort((a,b)=> {
                int order=b.Kills.CompareTo(a.Kills);if(order==0)order=a.LastKill.CompareTo(b.LastKill);return order==0?a.Slot.CompareTo(b.Slot):order;
            });
            return sorted;
        }
    }
    public class InputRecord  {
        public uint Tick;
        public int Buttons,Angle;
        public InputRecord(uint t,int b,int a) {
            Tick=t;
            Buttons=b;
            Angle=a;
        }
        public void Write(WireWriter w) {
            w.U32(Tick).U8(Buttons).U16(Angle);
        }
    }
    public static class Movement  {
        public const int MaxEnergy=GameRules.MaxEnergy,RecoveryDelay=GameRules.EnergyRecoveryDelay;
        public const float TankSpeed=GameRules.TankSpeed,SprintMultiplier=GameRules.SprintMultiplier;
        public static int Turn(int current,int desired) {
            return Turn(current,desired,GameRules.BaseTurretTurn);
        }
        public static int Turn(int current,int desired,int limit) {
            int diff=(desired-current+54000)%36000-18000;
            return (current+Math.Max(-limit,Math.Min(limit,diff))+36000)%36000;
        }
        public static void Step(TankState t,InputRecord input,GameMap map,byte[] bricks,IEnumerable<SkillEffect> effects=null) {
            if(t.Hp==0)return;
            int dx=((input.Buttons&8)!=0?1:0)-((input.Buttons&4)!=0?1:0),dy=((input.Buttons&1)!=0?1:0)-((input.Buttons&2)!=0?1:0);
            if(input.Tick<t.ConfusionEndTick) {dx=-dx;dy=-dy;}
            bool held=(input.Buttons&32)!=0,sprinting=held&&(dx!=0||dy!=0)&&t.Energy>0;
            if(held) {
                t.EnergyRecovery=RecoveryDelay;
                if(sprinting)t.Energy--;
            }
            else if(t.EnergyRecovery>0)t.EnergyRecovery--;
            else t.Energy=Math.Min(MaxEnergy,t.Energy+1);
            float norm=dx!=0&&dy!=0?(float)(1/Math.Sqrt(2)):1;
            float multiplier=sprinting?SprintMultiplier:1;
            multiplier=Math.Min(GameRules.MaxTankSpeedPercent/100f,multiplier);
            float water=map.Water(t.X,t.Y)?.5f:1;
            float slow=input.Tick<t.SlowEndTick?GameRules.SlowSpeedPercent/100f:1;
            if(SkillGeometry.KatyushaSlowEnd(t,input.Tick,effects)>input.Tick)slow*=GameRules.KatyushaSlowSpeedPercent/100f;
            if(input.Tick<t.RocketSlowEndTick)for(int hit=0;hit<t.RocketSlowStacks;hit++)slow*=GameRules.RocketSlowSpeedPercent/100f;
            float speed=TankSpeed*water*multiplier*slow/30;
            float x=t.X+dx*norm*speed,y=t.Y+dy*norm*speed;
            int desired=dx!=0||dy!=0?((int)Math.Round(Math.Atan2(dy,dx)*180/Math.PI*100)+36000)%36000:t.Body;
            int movingBody=map.Blocked(t.X,t.Y,desired,bricks)?t.Body:desired;
            if(!map.Blocked(x,t.Y,movingBody,bricks))t.X=x;
            if(!map.Blocked(t.X,y,movingBody,bricks))t.Y=y;
            t.Body=map.Blocked(t.X,t.Y,desired,bricks)?movingBody:desired;
            if(input.Tick>=t.ConfusionEndTick)t.Turret=Turn(t.Turret,input.Angle,GameRules.BaseTurretTurn);
        }
    }
    public class Snapshot  {
        public ulong Match,Time;
        public uint Id,Tick,Resolved;
        public int Remaining;
        public WorldState World;
    }
    public static class Rendering  {
        public static WorldState Interpolate(System.Collections.Generic.IReadOnlyList<Snapshot> samples,double serverTime,GameMap map=null) {
            if(samples.Count==0)return null;
            Snapshot a=samples[0],b=null;
            foreach(var s in samples) {
                if(s.Time<=serverTime)a=s;
                else {
                    b=s;
                    break;
                }
            }
            if(b==null||b==a||serverTime<=a.Time)return a.World.Copy();
            float alpha=(float)Math.Max(0,Math.Min(1,(serverTime-a.Time)/(b.Time-a.Time)));
            var rendered=a.World.Copy();
            foreach(var t in rendered.Tanks.Values) {
                TankState next;
                if(b.World.Tanks.TryGetValue(t.Slot,out next)&&t.Hp>0&&next.Hp>0&&t.Respawn==next.Respawn&&t.Life==next.Life) {
                    float px=t.X+(next.X-t.X)*alpha,py=t.Y+(next.Y-t.Y)*alpha;
                    int body=Angle(t.Body,next.Body,alpha);
                    if(map==null||!map.Blocked(px,py,body,rendered.Bricks)) {
                        t.X=px;
                        t.Y=py;
                        t.Body=body;
                    }
                    t.Turret=Angle(t.Turret,next.Turret,alpha);
                }
            }
            foreach(var bullet in rendered.Bullets.Values) {
                BulletState next;
                if(b.World.Bullets.TryGetValue(bullet.Id,out next)&&next.Bounces==bullet.Bounces) {
                    bullet.X+=(next.X-bullet.X)*alpha;
                    bullet.Y+=(next.Y-bullet.Y)*alpha;
                }
            }
            return rendered;
        }
        static int Angle(int a,int b,float t) {
            int diff=(b-a+54000)%36000-18000;
            return ((int)Math.Round(a+diff*t)+36000)%36000;
        }
    }
}
