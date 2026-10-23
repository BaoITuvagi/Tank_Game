using System;
using UnityEngine;
namespace Tank  {
    // Explicit offline visual QA mode; never connects or writes game/account data.
    public static class ArenaPreview  {
        public static bool Enabled  {
            get {
                return Array.IndexOf(Environment.GetCommandLineArgs(),"--arena-preview")>=0;
            }
        }
        public static void Setup(ClientCore client)  {
            client.Phase=ClientPhase.PLAYING;
            client.Slot=0;
            var world=new WorldState();
            for(int i=0;i<6;i++)world.Bricks[i]=(byte)GameRules.BrickMaxHp;
            world.Tanks[0]=new TankState {
                Slot=0,X=19,Y=16,Body=9000,Turret=9000,Hp=100,Energy=60,Shield=200
            };
            world.Tanks[1]=new TankState {
                Slot=1,X=23,Y=24,Body=18000,Turret=23000,Hp=100,Kills=2
            };
            world.Tanks[2]=new TankState {
                Slot=2,X=38,Y=34,Body=4500,Turret=9000,Hp=100,Kills=1,Deaths=1
            };
            world.Tanks[3]=new TankState {
                Slot=3,X=31,Y=43,Body=9000,Turret=0,Hp=100,Deaths=2
            };
            world.Bullets[1]=new BulletState {
                Id=1,Owner=0,X=19,Y=18.4f,Heading=9000
            };
            world.Bullets[2]=new BulletState {
                Id=2,Owner=1,X=21,Y=25,Heading=16000
            };
            client.Latest=new Snapshot {
                Tick=100,Remaining=3720,World=world,Time=(ulong)ClientCore.NowUs
            };
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--preview-final-ten")>=0)client.Latest.Remaining=270;
            client.Snapshots.Add(client.Latest);
            client.Players.Add(new RoomPlayer {
                Slot=0,Name="Commander"
            });
            client.Players.Add(new RoomPlayer {
                Slot=1,Name="Crimson"
            });
            client.Players.Add(new RoomPlayer {
                Slot=2,Name="Viper"
            });
            client.Players.Add(new RoomPlayer {
                Slot=3,Name="Nomad"
            });
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--preview-afk")>=0) {
                client.AfkWarning=true;
                client.AfkWarnedAt=ClientCore.NowUs-5e6;
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--preview-eight")>=0) {
                world.Tanks.Clear();
                world.Bullets.Clear();
                client.Players.Clear();
                for(int i=0;i<24;i++)world.Bricks[i]=(byte)GameRules.BrickMaxHp;
                float[] px= {
                    18,60,104,110,100,60,15,8
                },py= {
                    10,12,12,40,70,68,60,30
                };
                for(int slot=0;slot<8;slot++) {
                    world.Tanks[slot]=new TankState {
                        Slot=slot,X=px[slot],Y=py[slot],Hp=100,Body=slot*4500,Turret=slot*4500,Kills=slot,Deaths=slot/2,Life=slot+1
                    };
                    client.Players.Add(new RoomPlayer {
                        Slot=slot,Name=TankPalette.Names[slot]
                    });
                }
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--preview-roleplay")>=0) {
                client.RoomMode=RoomMode.ROLEPLAY;client.LoadoutReady=true;
                foreach(var tank in world.Tanks.Values) {
                    tank.Nation=tank.Slot;
                    tank.QReadyTick=RoleplayContent.NationCooldowns[tank.Nation];
                    client.Loadouts[tank.Slot]=new MatchLoadout{Slot=tank.Slot,Nation=tank.Nation};
                }
                client.SkillNotice="Q đã được kích hoạt";
                client.Match=42;
                var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--preview-nation"),slot;
                if(index>=0&&index+1<args.Length&&int.TryParse(args[index+1],out slot)&&world.Tanks.ContainsKey(slot))client.Slot=slot;
            }
            foreach(var tank in world.Tanks.Values)tank.Life=tank.Slot+1;
        }
        public static void Animate(ClientCore client)  {
            client.LastArrivalUs=ClientCore.NowUs;
            // Show an actual change from full HP, then hold the damaged state for screenshots.
            if(Time.unscaledTime>1.5f) {
                client.Latest.World.Bricks[0]=3;
                client.Latest.World.Bricks[1]=1;
                client.Latest.World.Tanks[1].Hp=60;
                client.Latest.World.Tanks[2].Hp=25;
            }
            if(client.RoomMode==RoomMode.ROLEPLAY)AnimateSkills(client);
            client.Latest.Id=client.Latest.Tick+1;
        }
        static void AnimateSkills(ClientCore client) {
            var world=client.Latest.World;int tick=100+Mathf.FloorToInt(Time.unscaledTime*30),start=100+(tick-100)/180*180;
            uint effectBase=(uint)((start-100)/180*64);
            client.Latest.Tick=(uint)tick;client.Latest.Time=(ulong)ClientCore.NowUs;world.Effects.Clear();
            foreach(var t in world.Tanks.Values) {
                t.QStartTick=start;t.QEndTick=start+RoleplayContent.NationDurations[t.Nation];t.QReadyTick=start+RoleplayContent.NationCooldowns[t.Nation];
                t.QStage=(t.Nation==1||t.Nation==5||t.Nation==6)&&tick<t.QEndTick?1:0;
                t.AimX=SkillGeometry.ClampAim(t.X+5,client.ActiveMap.width);t.AimY=SkillGeometry.ClampAim(t.Y+5,client.ActiveMap.height);
                t.ConfusionEndTick=start+GameRules.ConfusionTicks;t.FireLockEndTick=start+GameRules.FireLockTicks;
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--preview-fog")>=0)t.FogEndTick=start+GameRules.FogEnemyTicks;
                if(t.Nation==0)world.Effects.Add(new SkillEffect{Id=effectBase+1,Kind=SkillEffect.Bomb,Owner=0,Start=start,End=start+45,X=t.X+4,Y=t.Y+6,Radius=GameRules.BombRadius});
                if(t.Nation==2||t.Nation==7)world.Effects.Add(new SkillEffect{Id=effectBase+(uint)(2+t.Nation),Kind=t.Nation==2?SkillEffect.Radar:SkillEffect.Sabotage,Owner=t.Slot,Start=start,End=start+24,X=t.X,Y=t.Y});
                if(t.Nation==5&&tick>=t.QEndTick)world.Effects.Add(new SkillEffect{Id=effectBase+11,Kind=SkillEffect.Beam,Owner=t.Slot,Start=t.QEndTick,End=t.QEndTick,X=t.X,Y=t.Y,Angle=t.Turret,Radius=GameRules.BeamWidth});
                if(t.Nation==6&&tick>=t.QEndTick)world.Effects.Add(new SkillEffect{Id=effectBase+12,Kind=SkillEffect.Kamikaze,Owner=t.Slot,Start=t.QEndTick,End=t.QEndTick,X=t.X,Y=t.Y,Radius=GameRules.KamikazeRadius});
                if(t.Nation==1&&t.QStage==0)for(int i=0;i<9;i++) {
                    float a=i*137.5f*Mathf.Deg2Rad,r=Mathf.Sqrt(i/9f)*GameRules.KatyushaRadius;
                    int impact=start+150+GameRules.KatyushaDelayTicks+Mathf.RoundToInt(i*GameRules.KatyushaRainTicks/8f);
                    world.Effects.Add(new SkillEffect{Id=effectBase+(uint)(20+i),Kind=SkillEffect.Rocket,Owner=t.Slot,Start=impact-15,End=impact,X=t.AimX+Mathf.Cos(a)*r,Y=t.AimY+Mathf.Sin(a)*r,Radius=GameRules.RocketRadius});
                }
            }
        }
    }
}
