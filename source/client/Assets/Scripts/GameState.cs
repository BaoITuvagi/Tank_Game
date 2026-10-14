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
        public int Slot,Body,Turret,Hp,MaxHp=3,Energy=150,EnergyRecovery,Kills,Deaths,Status;
        public uint Respawn,Shield,LastKill=uint.MaxValue;
        public float X,Y;
        public TankState Copy() {
            return (TankState)MemberwiseClone();
        }
    }
    public class BulletState  {
        public uint Id;
        public int Owner,Heading,Bounces;
        public float X,Y;
        public BulletState Copy() {
            return (BulletState)MemberwiseClone();
        }
    }
    public class WorldState  {
        public SortedDictionary<int,TankState> Tanks=new SortedDictionary<int,TankState>();
        public SortedDictionary<uint,BulletState> Bullets=new SortedDictionary<uint,BulletState>();
        public byte[] Bricks=new byte[64];
        public WorldState Copy() {
            var w=new WorldState();
            foreach(var p in Tanks)w.Tanks.Add(p.Key,p.Value.Copy());
            foreach(var p in Bullets)w.Bullets.Add(p.Key,p.Value.Copy());
            w.Bricks=(byte[])Bricks.Clone();
            return w;
        }
        public static TankState ReadTank(WireReader r) {
            var t=new TankState {
                Slot=r.U8(),X=r.F32(),Y=r.F32(),Body=r.U16(),Turret=r.U16(),Hp=r.U8(),Kills=r.U16(),Deaths=r.U16(),Status=r.U8(),Respawn=r.U32(),Shield=r.U32(),MaxHp=r.U8(),Energy=r.U8(),EnergyRecovery=r.U8(),LastKill=r.U32()
            };
            Validate(t);
            return t;
        }
        public static void Validate(TankState t) {
            if(t.Slot>7||t.Body>=36000||t.Turret>=36000||t.MaxHp<1||t.MaxHp>30||t.Hp>t.MaxHp||t.Energy>150||t.EnergyRecovery>15||t.Status>1)throw new InvalidDataException("tank");
        }
        public static BulletState ReadBullet(WireReader r) {
            var b=new BulletState {
                Id=r.U32(),Owner=r.U8(),X=r.F32(),Y=r.F32(),Heading=r.U16(),Bounces=r.U8()
            };
            Validate(b);
            return b;
        }
        public static void Validate(BulletState b) {
            if(b.Owner>7||b.Heading>=36000||b.Bounces>2)throw new InvalidDataException("bullet");
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
            if(n>32)throw new InvalidDataException("bullets");
            for(int i=0;i<n;i++) {
                var b=ReadBullet(r);
                w.Bullets.Add(b.Id,b);
            }
            w.Bricks=r.Bytes(64);
            foreach(byte hp in w.Bricks)if(hp>5)throw new InvalidDataException("brick");
            return w;
        }
        public byte[] Encode() {
            var w=new WireWriter().U8(Tanks.Count);
            foreach(var t in Tanks.Values)w.U8(t.Slot).F32(t.X).F32(t.Y).U16(t.Body).U16(t.Turret).U8(t.Hp).U16(t.Kills).U16(t.Deaths).U8(t.Status).U32(t.Respawn).U32(t.Shield).U8(t.MaxHp).U8(t.Energy).U8(t.EnergyRecovery).U32(t.LastKill);
            w.U8(Bullets.Count);
            foreach(var b in Bullets.Values)w.U32(b.Id).U8(b.Owner).F32(b.X).F32(b.Y).U16(b.Heading).U8(b.Bounces);
            return w.Bytes(Bricks).Done();
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
            int diff=(desired-current+54000)%36000-18000;
            return (current+Math.Max(-400,Math.Min(400,diff))+36000)%36000;
        }
        public static void Step(TankState t,InputRecord input,GameMap map,byte[] bricks) {
            if(t.Hp==0)return;
            int dx=((input.Buttons&8)!=0?1:0)-((input.Buttons&4)!=0?1:0),dy=((input.Buttons&1)!=0?1:0)-((input.Buttons&2)!=0?1:0);
            bool held=(input.Buttons&32)!=0,sprinting=held&&(dx!=0||dy!=0)&&t.Energy>0;
            if(held) {
                t.EnergyRecovery=RecoveryDelay;
                if(sprinting)t.Energy--;
            }
            else if(t.EnergyRecovery>0)t.EnergyRecovery--;
            else t.Energy=Math.Min(MaxEnergy,t.Energy+1);
            float norm=dx!=0&&dy!=0?(float)(1/Math.Sqrt(2)):1;
            float speed=TankSpeed*(map.Water(t.X,t.Y)?.5f:1)*(sprinting?SprintMultiplier:1)/30;
            float x=t.X+dx*norm*speed,y=t.Y+dy*norm*speed;
            int desired=dx!=0||dy!=0?((int)Math.Round(Math.Atan2(dy,dx)*180/Math.PI*100)+36000)%36000:t.Body;
            int movingBody=map.Blocked(t.X,t.Y,desired,bricks)?t.Body:desired;
            if(!map.Blocked(x,t.Y,movingBody,bricks))t.X=x;
            if(!map.Blocked(t.X,y,movingBody,bricks))t.Y=y;
            t.Body=map.Blocked(t.X,t.Y,desired,bricks)?movingBody:desired;
            t.Turret=Turn(t.Turret,input.Angle);
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
                if(b.World.Tanks.TryGetValue(t.Slot,out next)&&t.Hp>0&&next.Hp>0&&t.Respawn==next.Respawn) {
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
