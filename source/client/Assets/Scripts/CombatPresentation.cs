using System;
using System.Collections.Generic;
using UnityEngine;
namespace Tank {
    // Server-confirmed presentation only. Draw never creates effects or plays sound.
    public sealed class CombatPresentation {
        public struct Fx {
            public bool Active,Critical,OriginalSize;
            public string Clip;
            public float X,Y,Width,Angle,Start,Duration,Speed,OffsetX,OffsetY;
            public uint ConfirmedTick;
            public float ConfirmedAge;
            public int Slot,Life;
            public Color Tint;
        }
        public struct Wreck {
            public bool Active;
            public int Slot,Life,Body,Turret,Palette;
            public float X,Y,Start;
            public uint Tick;
        }
        public struct Sound {
            public int Clip,Priority;
            public float Gain;
            public uint Tick;
        }
        public readonly Fx[] Transients=new Fx[256],Smoke=new Fx[16],Dust=new Fx[32];
        public readonly Wreck[] Wrecks=new Wreck[32];
        public readonly uint[] TrailBullets=new uint[GameRules.MaxBullets];
        public int TrailBulletCount {get;private set;}
        readonly Dictionary<ulong,uint> ledger=new Dictionary<ulong,uint>(4096);
        readonly List<ulong> expired=new List<ulong>(4096);
        readonly Sound[] audio=new Sound[64];
        int audioCount;
        readonly int[] lives=new int[8];
        readonly float[] recoil=new float[8],flash=new float[8],nextSmoke=new float[8],nextDust=new float[8],sprintUntil=new float[8],previousX=new float[8],previousY=new float[8];
        readonly int[] previousEnergy=new int[8];
        ulong match;
        uint watermark,latestTick;
        bool initialized,wasFogged;
        int fireLock;
        float nextBounceSound,nextDamageSound,nextExplosionSound;
        public float Clock {get;private set;}
        public float ShakeStrength {get;private set;}
        float shakeStart;
        public int NoticeSerial {get;private set;}
        public int LedgerCount {get {return ledger.Count;}}
        public int ActiveCount {
            get {int count=0;foreach(var f in Transients)if(f.Active)count++;foreach(var f in Smoke)if(f.Active)count++;foreach(var f in Dust)if(f.Active)count++;foreach(var f in Wrecks)if(f.Active)count++;return count;}
        }
        static readonly Color Orange=new Color(1,.65f,.3f),Brown=new Color(.65f,.42f,.26f);
        static ulong Key(int stream,uint id,int phase) {return ((ulong)(uint)stream<<56)|((ulong)id<<8)|(uint)phase;}
        float At(uint tick) {return Clock-(float)Math.Max(0,(latestTick-(double)tick)/30);}
        void Prune() {
            expired.Clear();foreach(var pair in ledger)if(pair.Value<=latestTick)expired.Add(pair.Key);
            foreach(ulong key in expired)ledger.Remove(key);
        }
        bool Mark(ulong key,uint expiry) {
            if(ledger.ContainsKey(key))return false;
            if(ledger.Count>=4096) {Prune();if(ledger.Count>=4096)return false;}
            ledger.Add(key,expiry);return true;
        }
        bool Take(int stream,uint id,int phase,uint tick,uint expiry,bool visible) {
            return Mark(Key(stream,id,phase),expiry)&&tick>watermark&&visible;
        }
        public static bool Visible(TankState observer,float x,float y,uint tick) {
            return observer==null||observer.Hp==0||tick>=observer.FogEndTick||(x-observer.X)*(x-observer.X)+(y-observer.Y)*(y-observer.Y)<=GameRules.FogRadius*GameRules.FogRadius;
        }
        static int Free(Fx[] pool,int end,bool critical) {
            int oldest=-1;
            for(int i=0;i<end;i++) {
                if(!pool[i].Active)return i;
                if(!pool[i].Critical&&(oldest<0||pool[i].Start<pool[oldest].Start))oldest=i;
            }
            if(oldest>=0||!critical)return oldest;
            oldest=0;for(int i=1;i<end;i++)if(pool[i].Start<pool[oldest].Start)oldest=i;return oldest;
        }
        void Spawn(Fx[] pool,string clip,float x,float y,float width,float start,float duration,Color tint,float angle=0,float speed=1,bool critical=false,int slot=-1,int life=0,float ox=0,float oy=0,bool originalSize=false) {
            if(Clock-start>=duration)return;
            int end=ReferenceEquals(pool,Transients)&&!critical?160:pool.Length;
            int index=Free(pool,end,critical);if(index<0)return;
            pool[index]=new Fx{Active=true,Critical=critical,OriginalSize=originalSize,Clip=clip,X=x,Y=y,Width=width,Start=start,Duration=duration,Tint=tint,Angle=angle,Speed=speed,Slot=slot,Life=life,OffsetX=ox,OffsetY=oy,ConfirmedTick=latestTick,ConfirmedAge=Math.Max(0,Clock-start)};
        }
        void Cue(int clip,int priority,float x,float y,uint tick,TankState observer,bool bounce=false,bool damage=false,bool explosion=false) {
            if(latestTick<tick||latestTick-tick>6)return;
            float distance=observer==null?0:(float)Math.Sqrt((observer.X-x)*(observer.X-x)+(observer.Y-y)*(observer.Y-y));
            if(distance>=30||!Visible(observer,x,y,latestTick))return;
            if(bounce) {if(Clock<nextBounceSound)return;nextBounceSound=Clock+.25f;}
            if(damage) {if(Clock<nextDamageSound)return;nextDamageSound=Clock+.125f;}
            float gain=1-distance/30;
            if(explosion&&Clock<nextExplosionSound) {
                // Voice budgeting only; causes and visual instances remain distinct.
                for(int i=0;i<audioCount;i++)if(audio[i].Clip==25||audio[i].Clip==29||audio[i].Clip>=11&&audio[i].Clip<=13) {
                    if(latestTick-audio[i].Tick>6||gain>audio[i].Gain||priority>audio[i].Priority)audio[i]=new Sound{Clip=clip,Gain=gain,Priority=priority,Tick=tick};return;
                }
                return;
            }
            if(explosion)nextExplosionSound=Clock+.1f;
            if(audioCount<audio.Length)audio[audioCount++]=new Sound{Clip=clip,Priority=priority,Gain=gain,Tick=tick};
        }
        public bool NextSound(out Sound sound) {
            sound=default(Sound);
            while(audioCount>0) {
                int best=0;for(int i=1;i<audioCount;i++)if(audio[i].Priority>audio[best].Priority)best=i;
                sound=audio[best];audio[best]=audio[--audioCount];
                if(latestTick>=sound.Tick&&latestTick-sound.Tick<=6)return true;
            }
            return false;
        }
        void Explosion(float x,float y,float width,float start,bool originalSize=false,bool smoke=true,bool fitSmoke=false,float duration=.75f) {
            float speed=.75f/duration;
            Spawn(Transients,"combat/explosion_top",x,y,width,start,duration,Color.white,speed:speed,critical:true,originalSize:originalSize);
            if(smoke)Spawn(Transients,"nature/smoke_top",x,y,width*.55f,start,duration,new Color(.6f,.6f,.6f,.25f),speed:speed,originalSize:fitSmoke);
        }
        void AddWreck(CombatEvent e,WorldState world) {
            if(latestTick>=e.Tick&&latestTick-e.Tick>=60)return;
            TankState t;if(!world.Tanks.TryGetValue(e.TargetSlot,out t)||t.Life!=e.TargetLife)return;
            int index=0;for(int i=0;i<Wrecks.Length;i++) {if(!Wrecks[i].Active) {index=i;break;}if(Wrecks[i].Start<Wrecks[index].Start)index=i;}
            Wrecks[index]=new Wreck{Active=true,Slot=t.Slot,Life=t.Life,Body=t.Body,Turret=t.Turret,Palette=t.Nation>=0?t.Nation:t.Slot,X=e.X,Y=e.Y,Start=At(e.Tick),Tick=e.Tick};
        }
        bool Lethal(CombatEvent damage,WorldState world) {
            foreach(var e in world.Combat.Values)if(e.Kind==CombatEvent.Death&&e.TargetSlot==damage.TargetSlot&&e.TargetLife==damage.TargetLife&&e.Tick==damage.Tick&&e.CauseKind==damage.CauseKind&&e.CauseId==damage.CauseId&&(damage.CauseKind!=0||e.BulletId==damage.BulletId))return true;
            return false;
        }
        public void Accept(ulong incomingMatch,Snapshot snapshot,int localSlot,GameMap map=null) {
            if(incomingMatch!=match) {Clear();match=incomingMatch;}
            latestTick=snapshot.Tick;Clock=Math.Max(Clock,latestTick/30f);Prune();
            bool first=!initialized;if(first) {watermark=latestTick;initialized=true;}
            var world=snapshot.World;TankState observer;world.Tanks.TryGetValue(localSlot,out observer);
            AcceptBattlefield(world,observer,map);
            TrailBulletCount=0;
            foreach(var bullet in world.Bullets.Values)if(ItemCodec.HasMirror(bullet.Kind)&&TrailBulletCount<TrailBullets.Length)TrailBullets[TrailBulletCount++]=bullet.Id;
            Age(Transients,world,observer);Age(Smoke,world,observer);Age(Dust,world,observer);
            foreach(var e in world.Combat.Values) {
                if(!Take(0,e.Id,0,e.Tick,e.Tick+(uint)GameRules.CombatEventRetentionTicks,Visible(observer,e.X,e.Y,latestTick)))continue;
                float start=At(e.Tick);
                if(e.Kind==CombatEvent.Shot) {
                    TankState t;
                    if(!world.Tanks.TryGetValue(e.SourceSlot,out t)||t.Life!=e.SourceLife)continue;
                    float a=e.Angle*(float)Math.PI/18000;
                    Spawn(Transients,"combat/muzzle",e.X,e.Y,1.2f,start,1f/6,Color.white,e.Angle/100f,2,false,t.Hp>0?t.Slot:-1,t.Life,(float)Math.Cos(a)*GameRules.TankMuzzleOffset,(float)Math.Sin(a)*GameRules.TankMuzzleOffset);
                    Spawn(Transients,"nature/smoke_top",e.X,e.Y,.65f,start,.5f,new Color(1,1,1,.25f));
                    recoil[t.Slot]=start;lives[t.Slot]=t.Life;
                    Cue(t.Nation==4&&e.Tick<t.QEndTick?21:24,1,e.X,e.Y,e.Tick,observer);
                }
                else if(e.Kind==CombatEvent.Impact||e.Kind==CombatEvent.Bounce) {
                    Spawn(Transients,"combat/hit_top",e.X,e.Y,e.Surface==0?1:e.Surface==1?.8f:.9f,start,.333f,e.Surface==2?Brown:Color.white,e.Angle/100f,1.5f);
                    Cue(e.Surface==1?26:27,0,e.X,e.Y,e.Tick,observer,bounce:e.Kind==CombatEvent.Bounce);
                }
                else if(e.Kind==CombatEvent.Damage) {
                    if(Lethal(e,world))continue;
                    TankState t;if(world.Tanks.TryGetValue(e.TargetSlot,out t)&&t.Life==e.TargetLife&&t.Hp>0) {flash[t.Slot]=start;lives[t.Slot]=t.Life;}
                    Cue(28,e.TargetSlot==localSlot?4:1,e.X,e.Y,e.Tick,observer,damage:true);
                }
                else if(e.Kind==CombatEvent.Death) {
                    if(!e.CauseOwnsExplosion) {Explosion(e.X,e.Y,3.4f*.75f,start);Cue(25,e.TargetSlot==localSlot?5:3,e.X,e.Y,e.Tick,observer,explosion:true);}
                    Spawn(Transients,"combat/hit_top",e.X,e.Y,1.7f*.75f,start,.1f,Color.white,speed:5,critical:true);
                    AddWreck(e,world);
                }
                else if(e.Kind==CombatEvent.BrickDestroyed) {
                    float width=1.7f;
                    if(map!=null)foreach(var brick in map.bricks)if(brick.id==e.ObjectId) {width=Math.Max(1.7f,Math.Min(5.1f,Math.Max(brick.w,brick.h)));break;}
                    width*=.75f*.5f;
                    Spawn(Transients,"nature/rocks_top",e.X,e.Y,width,start,.75f,Color.white,critical:true);
                    Spawn(Transients,"movement/dust_top",e.X,e.Y,width,start,.5f,Brown);
                    Spawn(Transients,"nature/smoke_top",e.X,e.Y,width,start,.75f,new Color(1,1,1,.2f));
                    Cue(29,2,e.X,e.Y,e.Tick,observer,explosion:true);
                }
            }
            foreach(var e in world.Effects) {
                bool visible=Visible(observer,e.X,e.Y,latestTick);
                if(e.Kind==SkillEffect.Activate&&latestTick>=e.Start&&Take(1,e.Id,0,(uint)e.Start,(uint)e.End+90,visible)) {
                    Spawn(Transients,"magic/magic_burst",e.X,e.Y,2,At((uint)e.Start),.5f,Color.white);
                    Cue(3+Math.Max(0,Math.Min(7,e.Angle)),4,e.X,e.Y,(uint)e.Start,observer);
                    if(e.Angle==3)Spawn(Transients,"nature/smoke_top",e.X,e.Y,2,At((uint)e.Start),.75f,new Color(1,1,1,.3f));
                }
                if(e.Kind==SkillEffect.Radar||e.Kind==SkillEffect.Sabotage) {
                    if(latestTick>=e.Start&&Take(1,e.Id,0,(uint)e.Start,(uint)e.End+90,visible))Spawn(Transients,e.Kind==SkillEffect.Sabotage?"magic/lightning_top":"magic/magic_burst",e.X,e.Y,3,At((uint)e.Start),.75f,Color.white);
                }
                if(e.Kind<1||e.Kind>4||latestTick<e.End||!Take(1,e.Id,1,(uint)e.End,(uint)e.End+90,visible))continue;
                if(e.Kind!=SkillEffect.Beam) {
                    Explosion(e.X,e.Y,e.Radius*2,At((uint)e.End),originalSize:true,smoke:e.Kind!=SkillEffect.Rocket,fitSmoke:true);
                    if(e.Kind==SkillEffect.Kamikaze)Spawn(Transients,"combat/ground_slam_top",e.X,e.Y,e.Radius*2,At((uint)e.End),.75f,Color.white,critical:true,originalSize:true);
                }
                else {
                    Spawn(Transients,"@beam",e.X,e.Y,e.Radius,At((uint)e.End),.6f,Color.white,e.Angle/100f,critical:true);
                    Spawn(Transients,"magic/lightning_top",e.X,e.Y,2,At((uint)e.End),.75f,Color.white);
                    Spawn(Transients,"magic/magic_burst",e.X,e.Y,2,At((uint)e.End),.5f,Color.white);
                }
                Cue(e.Kind==SkillEffect.Beam?14:e.Kind==SkillEffect.Kamikaze?13:e.Kind==SkillEffect.Bomb?12:11,4,e.X,e.Y,(uint)e.End,observer,explosion:e.Kind!=SkillEffect.Beam);
                if(observer!=null&&latestTick-e.End<=6) {float distance=(float)Math.Sqrt((observer.X-e.X)*(observer.X-e.X)+(observer.Y-e.Y)*(observer.Y-e.Y));ShakeStrength=Math.Min(3,Math.Max(ShakeStrength,(1-distance/35)*2.2f));shakeStart=Clock;}
            }
            foreach(var e in world.ItemEffects) {
                if(latestTick<e.Start||!Take(2,e.Id,0,(uint)e.Start,(uint)e.End+90,Visible(observer,e.X,e.Y,latestTick)))continue;
                float start=At((uint)e.Start);
                if(e.Kind==10) {Explosion(e.X,e.Y,e.Radius,start);Cue(11,3,e.X,e.Y,(uint)e.Start,observer,explosion:true);}
                else if(e.Kind==11) {Spawn(Transients,"movement/dash",e.X,e.Y,2,start,.5f,new Color(.8f,.4f,1),e.Angle/100f);Cue(30,1,e.X,e.Y,(uint)e.Start,observer);}
                else if(e.Kind==9) {
                    Spawn(Transients,"pickup_ui/pickup",e.X,e.Y,2,start,.5f,Color.white);Cue(31,2,e.X,e.Y,(uint)e.Start,observer);
                    string clip=e.Angle==0?"magic/heal_top":e.Angle==1||e.Angle==4?"magic/shield":e.Angle==3?"magic/magic_burst":"status/buff";
                    Spawn(Transients,clip,e.X,e.Y,2,start,.75f,Color.white);
                    if(e.Angle==0)Spawn(Transients,"pickup_ui/heart",e.X,e.Y+1,1,start,.5f,Color.white);
                }
            }
            foreach(var t in world.Tanks.Values) {
                if(t.Slot<0||t.Slot>=8)continue;int slot=t.Slot;
                if(lives[slot]!=t.Life) {recoil[slot]=flash[slot]=-100;lives[slot]=t.Life;sprintUntil[slot]=0;}
                else if(!first&&t.Hp>0&&t.Energy<previousEnergy[slot]&&Math.Abs(t.X-previousX[slot])+Math.Abs(t.Y-previousY[slot])>.0001f)sprintUntil[slot]=Clock+.15f;
                previousX[slot]=t.X;previousY[slot]=t.Y;previousEnergy[slot]=t.Energy;
                if(t.Hp>0&&t.QStage==1&&(t.Nation==5||t.Nation==6)) {
                    int seconds=(int)Math.Ceiling((t.QEndTick-(double)latestTick)/30.0);
                    ulong key=((ulong)3<<56)|((ulong)(uint)t.Life<<32)|((ulong)(uint)t.QStartTick<<8)|(uint)Math.Max(0,Math.Min(7,seconds));
                    if(seconds>0&&Mark(key,(uint)t.QEndTick+90)&&!first)Cue(t.Nation==5?16:seconds==1?17:15,4,t.X,t.Y,latestTick,observer);
                }
            }
            bool fogged=observer!=null&&observer.Hp>0&&latestTick<observer.FogEndTick;
            if(fogged!=wasFogged&&!first&&observer!=null)Cue(fogged?19:20,4,observer.X,observer.Y,latestTick,observer);
            wasFogged=fogged;
            if(observer!=null&&latestTick<observer.FireLockEndTick&&fireLock!=observer.FireLockEndTick&&!first)NoticeSerial++;
            fireLock=observer==null?0:observer.FireLockEndTick;
        }
        void Age(Fx[] pool,WorldState world,TankState observer) {
            for(int i=0;i<pool.Length;i++)if(pool[i].Active) {
                var f=pool[i];TankState owner;
                f.Start=Math.Min(f.Start,Clock-f.ConfirmedAge-(float)Math.Max(0,(latestTick-(double)f.ConfirmedTick)/30));pool[i]=f;
                if(Clock-f.Start>=f.Duration||!Visible(observer,f.X,f.Y,latestTick)||f.Slot>=0&&(!world.Tanks.TryGetValue(f.Slot,out owner)||owner.Life!=f.Life||owner.Hp<=0))pool[i].Active=false;
            }
        }
        public void Update(float dt,WorldState world,GameMap map,int localSlot) {
            if(!initialized||world==null)return;
            Clock+=Math.Max(0,dt);TankState observer;world.Tanks.TryGetValue(localSlot,out observer);
            Age(Transients,world,observer);Age(Smoke,world,observer);Age(Dust,world,observer);
            if(Clock-shakeStart>=.18f)ShakeStrength=0;
            for(int i=0;i<Wrecks.Length;i++)if(Wrecks[i].Active) {
                TankState t;var wreck=Wrecks[i];
                wreck.Start=Math.Min(wreck.Start,At(wreck.Tick));Wrecks[i]=wreck;
                if(Clock-wreck.Start>=2||!Visible(observer,wreck.X,wreck.Y,latestTick)||world.Tanks.TryGetValue(wreck.Slot,out t)&&t.Life!=wreck.Life)Wrecks[i].Active=false;
            }
            for(int slot=0;slot<8;slot++) {
                TankState t;if(!world.Tanks.TryGetValue(slot,out t))continue;
                if(t.Hp<=0||!Visible(observer,t.X,t.Y,latestTick))continue;
                if(t.Hp/(float)Math.Max(1,t.MaxHp)<=.3f&&Clock>=nextSmoke[t.Slot]) {
                    nextSmoke[t.Slot]=Clock+.5f;int index=t.Slot*2+(Smoke[t.Slot*2].Active?1:0);
                    Smoke[index]=new Fx{Active=true,Clip="nature/smoke_top",X=t.X,Y=t.Y,Width=1.2f,Start=Clock,Duration=.75f,Speed=1,Slot=t.Slot,Life=t.Life,Tint=new Color(.45f,.45f,.45f,.8f),ConfirmedTick=latestTick};
                }
                if(t.Energy>0&&Clock<sprintUntil[t.Slot]&&Clock>=nextDust[t.Slot]) {
                    nextDust[t.Slot]=Clock+1f/6;float angle=t.Body*(float)Math.PI/18000;
                    bool water=map!=null&&map.Water(t.X,t.Y);
                    Spawn(Dust,water?null:"movement/dust_top",t.X-(float)Math.Cos(angle)*.8f,t.Y-(float)Math.Sin(angle)*.8f,.9f,Clock,.5f,water?new Color(.6f,.9f,1,.5f):Brown);
                }
            }
        }
        void AcceptBattlefield(WorldState world,TankState observer,GameMap map) {
            var wave=world.Battlefield;
            if(wave==null||wave.Kind!=BattlefieldWave.Airstrike||latestTick<wave.Impact||map==null||map.battlefield==null)return;
            for(int round=0;round<wave.Rounds;round++) {
                uint impact=wave.RoundImpact(round);if(latestTick<impact)continue;
                for(int i=0;i<wave.TargetsPerRound;i++) {
                    var z=map.battlefield.Zone(wave.Kind,wave.Target(round,i));
                    int phase=round*GameRules.BattlefieldTargetsPerRound+i;
                    if(!Take(3,wave.Id,phase,impact,wave.End,Visible(observer,z.x,z.y,latestTick)))continue;
                    Explosion(z.x,z.y,GameRules.BattlefieldRadius*2,At(impact),originalSize:true,fitSmoke:true,duration:GameRules.BattlefieldAirstrikeTicks/30f);
                    Cue(25,3,z.x,z.y,impact,observer,explosion:true);
                }
            }
        }
        public float Recoil(TankState t) {return t.Slot>=0&&t.Slot<8&&lives[t.Slot]==t.Life?Math.Max(0,1-(Clock-recoil[t.Slot])/.1f)*.08f*CombatGeometry.TankSize:0;}
        public float Flash(TankState t) {return t.Slot>=0&&t.Slot<8&&lives[t.Slot]==t.Life?Math.Max(0,1-(Clock-flash[t.Slot])/.1f):0;}
        public void Clear() {
            Array.Clear(Transients,0,Transients.Length);Array.Clear(Smoke,0,Smoke.Length);Array.Clear(Dust,0,Dust.Length);Array.Clear(Wrecks,0,Wrecks.Length);
            Array.Clear(lives,0,8);Array.Clear(nextSmoke,0,8);Array.Clear(nextDust,0,8);Array.Clear(sprintUntil,0,8);
            for(int i=0;i<8;i++)recoil[i]=flash[i]=-100;
            ledger.Clear();expired.Clear();audioCount=TrailBulletCount=0;match=0;watermark=latestTick=0;Clock=ShakeStrength=0;initialized=wasFogged=false;fireLock=0;nextBounceSound=nextDamageSound=nextExplosionSound=0;
        }
    }
}
