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
            for(int i=0;i<6;i++)world.Bricks[i]=5;
            world.Tanks[0]=new TankState {
                Slot=0,X=19,Y=16,Body=9000,Turret=9000,Hp=3,Energy=60,Shield=200
            };
            world.Tanks[1]=new TankState {
                Slot=1,X=23,Y=24,Body=18000,Turret=23000,Hp=3,Kills=2
            };
            world.Tanks[2]=new TankState {
                Slot=2,X=38,Y=34,Body=4500,Turret=9000,Hp=3,Kills=1,Deaths=1
            };
            world.Tanks[3]=new TankState {
                Slot=3,X=31,Y=43,Body=9000,Turret=0,Hp=3,Deaths=2
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
                for(int i=0;i<24;i++)world.Bricks[i]=5;
                float[] px= {
                    20,40,62,70,56,40,12,10
                },py= {
                    10,12,12,40,70,68,60,30
                };
                for(int slot=0;slot<8;slot++) {
                    world.Tanks[slot]=new TankState {
                        Slot=slot,X=px[slot],Y=py[slot],Hp=3,Body=slot*4500,Turret=slot*4500,Kills=slot,Deaths=slot/2
                    };
                    client.Players.Add(new RoomPlayer {
                        Slot=slot,Name=TankPalette.Names[slot]
                    });
                }
            }
        }
        public static void Animate(ClientCore client)  {
            client.LastArrivalUs=ClientCore.NowUs;
            // Show an actual change from full HP, then hold the damaged state for screenshots.
            if(Time.unscaledTime>1.5f) {
                client.Latest.World.Bricks[0]=3;
                client.Latest.World.Bricks[1]=1;
                client.Latest.World.Tanks[1].Hp=2;
                client.Latest.World.Tanks[2].Hp=1;
            }
        }
    }
}
