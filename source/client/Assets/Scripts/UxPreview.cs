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
            if(settingsDraft!=null&&Array.IndexOf(args,"--preview-arrow-bindings")>=0){
                var arrows=new[]{KeyCode.UpArrow,KeyCode.DownArrow,KeyCode.LeftArrow,KeyCode.RightArrow};
                for(int i=0;i<arrows.Length;i++)settingsDraft.Bindings[i].Secondary=arrows[i];
                settingsScroll=new Vector2(0,230);
            }
            if(settingsDraft!=null&&Array.IndexOf(args,"--preview-custom-bindings")>=0){
                string ignored;
                settingsDraft.TryBind(ControlAction.Up,false,KeyCode.Keypad8,out ignored);
                settingsDraft.TryBind(ControlAction.Sprint,false,KeyCode.RightControl,out ignored);
                settingsDraft.TryBind(ControlAction.Sprint,true,KeyCode.LeftControl,out ignored);
                settingsDraft.TryBind(ControlAction.Fire,true,KeyCode.KeypadEnter,out ignored);
                settingsDraft.TryBind(ControlAction.CampaignSkill,false,KeyCode.PageDown,out ignored);
            }
            if(settingsDraft!=null&&Array.IndexOf(args,"--preview-binding-capture")>=0)fieldFocusRequest=BindingControlName((int)ControlAction.Sprint,false);
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
                settingsScroll=new Vector2(0,10000);
                menuViewport=new Vector2(10000,10000);
            }
        }
        void SetupMenuPreview()  {
            if(!IsMenuPreview)return;
            var args=Environment.GetCommandLineArgs();
            int previewMap=UxTestDriver.PreviewMapId;
            client.SelectPreviewMap(previewMap);
            int index=Array.IndexOf(args,"--menu-preview");
            string page=index+1<args.Length?args[index+1]:"login";
            bool preparing=page=="roleplay-preparing";
            if(preparing)page="roleplay-room";
            if(page=="login")return;
            if(page=="settings"){settingsDraft=ClientPreferences.Active.Copy();settingsOpen=true;return;}
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
            if(page=="mode-select"||page=="mode-select-resume") {
                client.Phase=ClientPhase.LOBBY;
                modeNavigation.ShowSelection();
                if(page=="mode-select-resume")client.Campaign.Run=new Tank.Campaign.RunState {
                    Id="preview",Nation=0,X=6,Y=506,Distance=506,Hp=75,Facing=0,Shot=4,Skill=12,Gold=185,Kills=28,Shields=2,DoubleHits=3,Bosses=1,Bottom=499,
                    TotalGold=1240,ShopAvailable=true,Prices=new[]{30,46,55},Terrain=new byte[247],SaveStatus=""
                };
                return;
            }
            // Existing lobby previews continue directly to the room browser.
            modeNavigation.ShowMultiplayer();
            if(page=="create") {
                client.Phase=ClientPhase.LOBBY;
                OpenRoomForm(false);
                roomMapId=previewMap;
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
                    Id=24,Name="Đấu trường bạn bè",Players=3,Capacity=4,Phase=RoomPhase.WAITING,MapId=previewMap
                });
                client.Rooms.Add(new RoomInfo {
                    Id=25,Name="Biên giới 8 người",Players=6,Capacity=8,Phase=RoomPhase.WAITING,Locked=true,Mode=RoomMode.ROLEPLAY,MapId=3
                });
                client.Rooms.Add(new RoomInfo {
                    Id=26,Name="Cuộc đấu tối nay",Players=4,Capacity=4,Phase=RoomPhase.RUNNING,MapId=4
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
                        Match=14,MapId=previewMap,Started=end-180000,Ended=end,Reason=0,OwnSlot=2
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
        public static int PreviewMapId {
            get {int id;return int.TryParse(Arg("--preview-map",MapCatalog.DefaultId.ToString()),out id)&&MapCatalog.IsSelectable(id)?id:MapCatalog.DefaultId;}
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
                c.Create(roomName,Array.IndexOf(Environment.GetCommandLineArgs(),"--players")>=0?players:4,int.Parse(Arg("--seconds","180")),"",int.Parse(Arg("--hp","3")),roleplay?RoomMode.ROLEPLAY:RoomMode.NORMAL,int.Parse(Arg("--map",MapCatalog.DefaultId.ToString())));
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
                var campaign=c.Campaign.Run;
                System.IO.File.WriteAllText(report,JsonUtility.ToJson(new UxObservation {
                    phase=c.Phase.ToString(),width=Screen.width,height=Screen.height,fullscreen=Screen.fullScreen,focused=GameWindowFocus.IsForeground,field=App.FocusedControl,turret=t==null?0:t.Turret,capacity=c.RoomCapacity,seconds=c.RoomSeconds,players=c.Players.Count,map=c.ActiveMap.mapId,roomMap=c.RoomMapId,selectedRoomMap=App.SelectedRoomMapId,tick=c.Latest==null?0:(int)c.Latest.Tick,x=t==null?0:t.X,y=t==null?0:t.Y,decoded=c.Decoded,invalid=c.Invalid,message=c.Message,afk=c.AfkWarning,
                    chatTyping=App.ChatTyping,chatExpanded=App.ChatExpanded,autoFire=App.AutoFireEnabled,inputButtons=App.LastInputButtons,chatCount=c.Chat.Messages.Count,chatError=c.ChatError,
                    chatLastText=c.Chat.Messages.Count==0?"":c.Chat.Messages[c.Chat.Messages.Count-1].Text,
                    chatLastName=c.Chat.Messages.Count==0?"":c.Chat.Messages[c.Chat.Messages.Count-1].Name,
                    chatLastPalette=c.Chat.Messages.Count==0?-1:c.Chat.Messages[c.Chat.Messages.Count-1].Palette,
                    mode=c.RoomMode.ToString(),stage=c.PrepareStage.ToString(),loadout=c.LoadoutReady,skillPress=c.SkillPressRevision,skillFeedback=c.SkillFeedbackRevision,skillNotice=c.SkillNotice,qReady=c.SkillReadyTick(0),qPending=c.SkillPending(0),
                    selectedMode=App.SelectedMode,campaignPage=App.CampaignPage,campaignRunId=campaign==null?"":campaign.Id,campaignTurn=campaign==null?0:campaign.Turn,
                    campaignHp=campaign==null?0:campaign.Hp,campaignGold=campaign==null?0:campaign.Gold,campaignPending=c.Campaign.Pending,
                    campaignFinished=campaign!=null&&campaign.Finished,campaignSaveStatus=campaign==null?"":campaign.SaveStatus,
                    skillDetailsOpen=SkillDetailsUi.IsOpen,skillDetailsBlocked=SkillDetailsUi.InputBlocked,skillDetailsTitle=SkillDetailsUi.Title,
                    campaignShields=campaign==null?0:campaign.Shields,campaignDoubleHits=campaign==null?0:campaign.DoubleHits,
                    campaignShot=campaign==null?0:campaign.Shot,campaignSkill=campaign==null?0:campaign.Skill
                }));
            }
        }
        [Serializable] class UxObservation {
            public string phase,message,field,mode,stage,skillNotice;
            public int width,height,capacity,seconds,players,map,roomMap,selectedRoomMap,tick,turret,skillPress,skillFeedback,qReady;
            public bool fullscreen,afk,focused,loadout,qPending;
            public float x,y;
            public long decoded,invalid;
            public bool chatTyping,chatExpanded,autoFire;
            public int inputButtons,chatCount,chatLastPalette;
            public string chatError,chatLastText,chatLastName;
            public string selectedMode,campaignRunId,campaignSaveStatus;
            public int campaignPage,campaignTurn,campaignHp,campaignGold;
            public bool campaignPending,campaignFinished;
            public bool skillDetailsOpen,skillDetailsBlocked;
            public string skillDetailsTitle;
            public int campaignShields,campaignDoubleHits,campaignShot,campaignSkill;
        }
    }
}
