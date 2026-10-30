using System;
using UnityEngine;
namespace Tank  {
    public partial class TankApp  {
        bool IsMenuPreview  {
            get {
                return Array.IndexOf(Environment.GetCommandLineArgs(),"--menu-preview")>=0;
            }
        }
        void SetupMenuPreviewQa() {
            if(!IsMenuPreview)return;
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--preview-long-text")>=0) {
                username="tank_battle_commander_123";
                password=roomPassword=joinPassword="menu-preview-only-2026";
                roomName=client.RoomName="Đấu trường những người bạn";
                client.Nickname=displayName="Chỉ huy xe tăng Việt Nam";
                foreach(var player in client.Players)player.Name="Chỉ huy xe tăng Việt Nam";
                foreach(var room in client.Rooms)room.Name="Đấu trường những người bạn";
                foreach(var score in client.Scores)score.Name="Chỉ huy xe tăng Việt Nam";
            }
            if(Array.IndexOf(args,"--preview-error")>=0)client.Message="Tên tài khoản hoặc mật khẩu không đúng. Vui lòng kiểm tra lại thông tin và thử lại.";
            if(Array.IndexOf(args,"--preview-wide-names")>=0){
                client.Nickname=displayName=new string('M',24);client.RoomName=roomName=new string('M',24);
                foreach(var player in client.Players)player.Name=client.Nickname;
                foreach(var room in client.Rooms)room.Name=client.RoomName;
                foreach(var score in client.Scores)score.Name=client.Nickname;
                if(client.HistoryDetail!=null)foreach(var score in client.HistoryDetail.Players)score.Name=client.Nickname;
            }
            if(Array.IndexOf(args,"--preview-empty-slots")>=0&&client.Players.Count>3)client.Players.RemoveRange(3,client.Players.Count-3);
            if(Array.IndexOf(args,"--preview-two")>=0&&client.Players.Count>0){client.RoomCapacity=4;if(client.Players.Count>2)client.Players.RemoveRange(2,client.Players.Count-2);}
            if(Array.IndexOf(args,"--preview-unselected")>=0)foreach(var player in client.Players)if(player.Slot==client.Slot){player.Nation=-1;player.Ready=false;}
            if(Array.IndexOf(args,"--preview-ready")>=0)foreach(var player in client.Players)player.Ready=true;
            if(Array.IndexOf(args,"--preview-guest")>=0)client.Host=1;
            if(Array.IndexOf(args,"--preview-scroll-bottom")>=0) {
                scroll=roleplayInfoScroll=dialogScroll=lobbyActionScroll=new Vector2(0,10000);
                menuViewport=new Vector2(10000,10000);
            }
        }
        void SetupMenuPreview()  {
            if(!IsMenuPreview)return;
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"--menu-preview");
            string page=index+1<args.Length?args[index+1]:"login";
            bool preparing=page=="roleplay-preparing";
            if(preparing)page="roleplay-room";
            if(page=="login")return;
            if(page=="login-loading") {client.Phase=ClientPhase.CONNECTING;username="commander";return;}
            if(page=="register") {
                registerTab=true;
                return;
            }
            client.Nickname="Commander";
            client.Slot=0;
            client.Host=0;
            client.RoomCapacity=8;
            client.RoomSeconds=300;
            client.RoomName="Đấu trường bạn bè";
            client.RoomId=24;
            if(page=="create") {
                client.Phase=ClientPhase.LOBBY;
                OpenRoomForm(false);
                return;
            }
            if(page=="profile") {
                client.Phase=ClientPhase.LOBBY;
                displayName=client.Nickname;
                profileForm=true;
                return;
            }
            if(page=="join-password") {
                client.Phase=ClientPhase.LOBBY;
                joinForm=true;
                return;
            }
            if(page=="lobby"||page=="lobby-empty"||page=="history"||page=="history-empty"||page=="history-detail"||page=="afk-lobby") {
                client.Phase=ClientPhase.LOBBY;
                client.Rooms.Add(new RoomInfo {
                    Id=24,Name="Đấu trường bạn bè",Players=3,Capacity=4,Phase=RoomPhase.WAITING
                });
                client.Rooms.Add(new RoomInfo {
                    Id=25,Name="Biên giới 8 người",Players=6,Capacity=8,Phase=RoomPhase.WAITING,Locked=true,Mode=RoomMode.ROLEPLAY
                });
                client.Rooms.Add(new RoomInfo {
                    Id=26,Name="Cuộc đấu tối nay",Players=4,Capacity=4,Phase=RoomPhase.RUNNING
                });
                client.Rooms.Add(new RoomInfo {Id=27,Name="Biệt đội đã đủ",Players=2,Capacity=2,Phase=RoomPhase.WAITING});
                if(page=="lobby-empty")client.Rooms.Clear();
                if(page=="history-empty")history=true;
                if(page=="afk-lobby") {
                    client.Rooms.Clear();
                    client.Rooms.Add(new RoomInfo {
                        Id=24,Name="Đấu xe tăng",Players=2,Capacity=3,Phase=RoomPhase.RUNNING,CanRejoin=true
                    });
                    client.Message="Bạn đã về sảnh vì không hoạt động trong 15 giây sau cảnh báo AFK.";
                }
                if(page=="history") {
                    history=true;
                    client.History.Add(new HistoryEntry {
                        Match=14,Ended=(ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),Rank=1,Kills=8,Deaths=2
                    });
                }
                if(page=="history-detail") {
                    history=true;
                    client.SelectedHistoryMatch=14;
                    ulong end=(ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    client.HistoryDetail=new MatchDetail {
                        Match=14,MapId=1,Started=end-180000,Ended=end,Reason=0,OwnSlot=2
                    };
                    for(int slot=0;slot<3;slot++)client.HistoryDetail.Players.Add(new Score {
                        Slot=slot,Name=new[] {
                            "Vua bá khí","huy","nvbang"
                        }
                        [slot],Rank=slot+1,Kills=6-slot,Deaths=slot+1,Disconnected=slot==2
                    });
                }
                return;
            }
            string[] names= {
                "Commander","Crimson","Viper","Nomad","Nova","Blaze","Aqua","Rose"
            };
            if(page=="results") {
                client.Phase=ClientPhase.RESULTS;
                for(int slot=0;slot<8;slot++)client.Scores.Add(new Score {
                    Slot=slot,Name=names[slot],Rank=slot+1,Kills=12-slot,Deaths=slot+1
                });
                return;
            }
            client.Phase=ClientPhase.ROOM;
            client.RoomPhase=RoomPhase.WAITING;
            for(int slot=0;slot<8;slot++)client.Players.Add(new RoomPlayer {
                Slot=slot,Name=names[slot],Ready=slot<6
            });
            if(page=="roleplay-room") {
                client.RoomMode=RoomMode.ROLEPLAY;
                int choice=0,nationIndex=Array.IndexOf(args,"--preview-nation"),requested;
                if(nationIndex>=0&&nationIndex+1<args.Length&&int.TryParse(args[nationIndex+1],out requested)&&requested>=0&&requested<8)choice=requested;
                foreach(var player in client.Players) {player.Nation=(player.Slot+choice)%8;player.Ready=false;}
                if(preparing) {
                    client.Phase=ClientPhase.PREPARING;client.RoomPhase=RoomPhase.PREPARING;client.PrepareStage=PrepStage.SYNC_LOADOUT;
                    foreach(var player in client.Players)player.Ready=true;
                }
            }
            if(page=="room-settings")OpenRoomForm(true);
        }
    }
    public sealed class UxTestDriver:MonoBehaviour  {
        public TankApp App;
        bool registered,login,joined,ready,autoStarted;
        double nextNation;
        float nextReport;
        public static bool Enabled  {
            get {
                return Array.IndexOf(Environment.GetCommandLineArgs(),"--ui-test")>=0;
            }
        }
        public static string Arg(string name,string fallback) {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,name);
            return index>=0&&index+1<args.Length?args[index+1]:fallback;
        }
        void Start() {
            App.Core.Connect("127.0.0.1",int.Parse(Arg("--tcp","23888")),int.Parse(Arg("--udp","23889")));
        }
        void Update() {
            if(App==null)return;
            var c=App.Core;
            if(c.Phase==ClientPhase.AUTHENTICATING&&!registered) {
                registered=true;
                c.Register(Arg("--account","uxplayer"),"ux-test-only-2026");
            }
            if(registered&&!login&&(c.Message=="Đăng ký thành công"||c.Message=="USERNAME_EXISTS")) {
                login=true;
                c.Login(Arg("--account","uxplayer"),"ux-test-only-2026");
            }
            bool peer=Array.IndexOf(Environment.GetCommandLineArgs(),"--ui-peer")>=0;
            bool auto=Array.IndexOf(Environment.GetCommandLineArgs(),"--ui-auto")>=0;
            bool roleplay=Array.IndexOf(Environment.GetCommandLineArgs(),"--roleplay")>=0;
            int players=int.Parse(Arg("--players","2"));
            string roomName=Arg("--room","Focus Test");
            if(auto&&!peer&&c.Phase==ClientPhase.LOBBY&&!joined) {
                c.Create(roomName,Array.IndexOf(Environment.GetCommandLineArgs(),"--players")>=0?players:4,int.Parse(Arg("--seconds","180")),"",int.Parse(Arg("--hp","3")),roleplay?RoomMode.ROLEPLAY:RoomMode.NORMAL);
                joined=true;
            }
            var room=Array.IndexOf(Environment.GetCommandLineArgs(),"--room")>=0?c.Rooms.Find(r=>r.Name==roomName):c.Rooms.Count>0?c.Rooms[0]:null;
            if(peer&&c.Phase==ClientPhase.LOBBY&&!joined&&room!=null) {
                c.Join(room.Id);
                joined=true;
            }
            if((peer||auto)&&c.Phase==ClientPhase.ROOM&&!ready) {
                var local=c.Players.Find(p=>p.Slot==c.Slot);
                if(roleplay&&local!=null&&local.Nation<0) {
                    if(ClientCore.NowUs>=nextNation) {c.SelectNation((c.Slot+int.Parse(Arg("--nation-offset","0")))%8);nextNation=ClientCore.NowUs+1e6;}
                }
                else if(local!=null) {c.Ready(true);ready=true;}
            }
            if(auto&&!peer&&!autoStarted&&c.Phase==ClientPhase.ROOM&&c.Players.Count==players&&c.Players.TrueForAll(p=>p.Ready)) {
                autoStarted=true;
                c.StartMatch();
            }
            string report=Arg("--ui-output","");
            if(report!=""&&Time.unscaledTime>=nextReport) {
                nextReport=Time.unscaledTime+1;
                var t=c.Predicted;
                System.IO.File.WriteAllText(report,JsonUtility.ToJson(new UxObservation {
                    phase=c.Phase.ToString(),width=Screen.width,height=Screen.height,fullscreen=Screen.fullScreen,focused=GameWindowFocus.IsForeground,field=App.FocusedControl,turret=t==null?0:t.Turret,capacity=c.RoomCapacity,seconds=c.RoomSeconds,players=c.Players.Count,map=c.ActiveMap.mapId,tick=c.Latest==null?0:(int)c.Latest.Tick,x=t==null?0:t.X,y=t==null?0:t.Y,decoded=c.Decoded,invalid=c.Invalid,message=c.Message,afk=c.AfkWarning,
                    mode=c.RoomMode.ToString(),stage=c.PrepareStage.ToString(),loadout=c.LoadoutReady,skillPress=c.SkillPressRevision,skillFeedback=c.SkillFeedbackRevision,skillNotice=c.SkillNotice,qReady=c.SkillReadyTick(0),qPending=c.SkillPending(0)
                }));
            }
        }
        [Serializable] class UxObservation {
            public string phase,message,field,mode,stage,skillNotice;
            public int width,height,capacity,seconds,players,map,tick,turret,skillPress,skillFeedback,qReady;
            public bool fullscreen,afk,focused,loadout,qPending;
            public float x,y;
            public long decoded,invalid;
        }
    }
}
