using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
namespace Tank  {
    /** Opt-in command-line integration harness, absent from normal player UI. */
    public class SmokePlayer:MonoBehaviour  {
        ClientCore core;
        string username,output,roomName;
        bool auth,login,join,ready,start,roleplay,configuredRoom;
        double began,lastHistory,lastSkill,nextNation;
        int expectedTicks,players,seconds,startingHp,nationOffset,qRequests,matchPlayers;
        uint change;
        int battlefieldTypes,battlefieldWarnings,battlefieldImpacts;
        uint lastBattlefield;
        int battlefieldWaves;
        int buttons=17,angle;
        bool chatSent,chatColorsValid=true;
        int chatSlots;
        public static bool Enabled {
            get {
                return Environment.GetCommandLineArgs().Contains("--smoke");
            }
        }
        static string Arg(string name,string fallback) {
            var a=Environment.GetCommandLineArgs();
            int i=Array.IndexOf(a,name);
            return i>=0&&i+1<a.Length?a[i+1]:fallback;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot() {
            if(Enabled)new GameObject("Smoke Player").AddComponent<SmokePlayer>();
        }
        void Awake() {
            Application.runInBackground=true;
            Application.targetFrameRate=60;
            GameMap initial;
            core=MapLoader.CreateClient(out initial);
            core.SnapshotAccepted+=snapshot=> {
                var wave=snapshot.World.Battlefield;if(wave==null)return;
                int bit=1<<wave.Kind;battlefieldTypes|=bit;
                if(wave.Id!=lastBattlefield) {lastBattlefield=wave.Id;battlefieldWaves++;}
                if(snapshot.Tick<wave.Impact)battlefieldWarnings|=bit;else battlefieldImpacts|=bit;
            };
            username=Arg("--account","unity000");
            output=Arg("--output",Path.Combine(Application.persistentDataPath,"smoke-result.json"));
            core.Connect(Arg("--host","127.0.0.1"),int.Parse(Arg("--tcp","20888")),int.Parse(Arg("--udp","20889")));
            expectedTicks=int.Parse(Arg("--expected-ticks","5400"));
            roomName=Arg("--room","Unity smoke");
            roleplay=Environment.GetCommandLineArgs().Contains("--roleplay");
            players=int.Parse(Arg("--players","2"));seconds=int.Parse(Arg("--seconds","180"));startingHp=int.Parse(Arg("--hp","100"));nationOffset=int.Parse(Arg("--nation-offset","0"));
            configuredRoom=roleplay||Environment.GetCommandLineArgs().Contains("--players")||Environment.GetCommandLineArgs().Contains("--seconds")||Environment.GetCommandLineArgs().Contains("--hp");
            if(players<2||players>8||seconds<60||seconds>600||seconds%60!=0||startingHp<1||startingHp>GameRules.MaxStartHp||nationOffset<0||nationOffset>7)throw new ArgumentOutOfRangeException("smoke settings");
            if(configuredRoom&&!Environment.GetCommandLineArgs().Contains("--expected-ticks"))expectedTicks=seconds*30;
            began=ClientCore.NowUs;
        }
        void Update() {
            core.Poll();
            if(core.Phase==ClientPhase.AUTHENTICATING&&!auth) {
                auth=true;
                core.Register(username,"unity-demo-only-2026");
            }
            if(auth&&!login&&(core.Message=="Đăng ký thành công"||core.Message=="USERNAME_EXISTS")) {
                login=true;
                core.Login(username,"unity-demo-only-2026");
            }
            if(core.Phase==ClientPhase.LOBBY&&!join) {
                if(Environment.GetCommandLineArgs().Contains("--create")) {
                    if(configuredRoom)core.Create(roomName,players,seconds,"",startingHp,roleplay?RoomMode.ROLEPLAY:RoomMode.NORMAL);
                    else core.Create(roomName);
                    join=true;
                }
                else {
                    var room=core.Rooms.FirstOrDefault(r=>r.Name==roomName&&(!configuredRoom||r.Mode==(roleplay?RoomMode.ROLEPLAY:RoomMode.NORMAL)));
                    if(room!=null) {
                        core.Join(room.Id);
                        join=true;
                    }
                }
            }
            if(core.Phase==ClientPhase.ROOM&&!ready) {
                var local=core.Players.FirstOrDefault(p=>p.Slot==core.Slot);
                if(core.RoomMode==RoomMode.ROLEPLAY&&local!=null&&local.Nation<0) {
                    if(ClientCore.NowUs>=nextNation) {core.SelectNation((core.Slot+nationOffset)%8);nextNation=ClientCore.NowUs+1e6;}
                }
                else if(local!=null) {ready=true;core.Ready(true);}
            }
            if(core.Phase==ClientPhase.ROOM&&core.Slot==core.Host&&!start&&core.Players.Count>=players&&core.Players.All(p=>p.Ready)) {
                start=true;
                core.StartMatch();
            }
            if(core.Phase==ClientPhase.PLAYING) {
                if(matchPlayers==0&&core.Latest!=null)matchPlayers=core.Latest.World.Tanks.Count;
                uint tick=core.Latest==null?0:core.Latest.Tick;
                if(Environment.GetCommandLineArgs().Contains("--chat")) {
                    if(!chatSent&&tick>=5)chatSent=core.SendChat("Xin chào từ "+username+" <b>chat nguyên văn</b>");
                    foreach(var message in core.Chat.Messages) {
                        chatSlots|=1<<message.Slot;
                        MatchLoadout loadout;int expectedPalette=core.Loadouts.TryGetValue(message.Slot,out loadout)?loadout.Nation:message.Slot;
                        if(message.Palette!=expectedPalette||!message.Text.Contains("<b>chat nguyên văn</b>"))chatColorsValid=false;
                    }
                }
                if(tick>=change) {
                    change=tick+30;
                    buttons=(tick/30%2==0?1:8)|16|(roleplay?32:0);
                    angle=(int)(tick*17%36000);
                }
                for(int i=0;i<3;i++)if(!core.GenerateInput(buttons,angle))break;
                if(roleplay&&ClientCore.NowUs-lastSkill>=1e6) {
                    lastSkill=ClientCore.NowUs;
                    if(core.UseSkill(0))qRequests++;
                }
            }
            if(core.Ended&&ClientCore.NowUs-lastHistory>2e6) {
                lastHistory=ClientCore.NowUs;
                core.GetHistory();
            }
            if(core.Ended&&core.History.Any(h=>h.Match==core.Match)) {
                // Reliable END can overtake the last impaired UDP snapshots.
                if(core.Latest!=null&&core.Latest.Tick<expectedTicks-1&&ClientCore.NowUs-lastHistory<500000)return;
                bool scenario=!configuredRoom||core.RoomMode==(roleplay?RoomMode.ROLEPLAY:RoomMode.NORMAL)&&core.RoomCapacity==players&&core.RoomSeconds==seconds&&core.RoomStartingHp==startingHp&&matchPlayers==players&&core.Scores.Count==players;
                bool skills=!roleplay||core.LoadoutReady&&core.Loadouts.Count==players&&qRequests>0&&core.SkillFeedbackRevision>=1;
                int battlefieldMask=(1<<BattlefieldWave.Fire)|(1<<BattlefieldWave.Airstrike);
                bool battlefield=!Environment.GetCommandLineArgs().Contains("--battlefield")||(battlefieldTypes==battlefieldMask&&battlefieldWarnings==battlefieldMask&&battlefieldImpacts==battlefieldMask);
                bool chat=!Environment.GetCommandLineArgs().Contains("--chat")||chatSent&&chatColorsValid&&chatSlots==(1<<players)-1;
                Save(SmokeAcceptance.Passed(core,expectedTicks)&&scenario&&skills&&battlefield&&chat);
                return;
            }
            if(ClientCore.NowUs-began>(expectedTicks/30.0+60)*1e6||(auth&&core.Phase==ClientPhase.DISCONNECTED))Save(false);
        }
        [Serializable] class SmokeResult  {
            public bool success;
            public long decoded,invalid;
            public int historyRows,endReason,expectedTicks,qRequests,skillFeedback,nation,map,players,capacity,seconds,startingHp,expectedPlayers,expectedHp,matchPlayers,resultPlayers;
            public int battlefieldTypes,battlefieldWarnings,battlefieldImpacts,battlefieldWaves;
            public uint observedTick;
            public ulong match;
            public string mode,message,skillNotice;
            public bool chatSent,chatColorsValid;
            public int chatSlots;
        }
        void Save(bool success) {
            var result=new SmokeResult {
                success=success,decoded=core.Decoded,invalid=core.Invalid,historyRows=core.History.Count, endReason=core.EndReason,expectedTicks=expectedTicks,observedTick=core.Latest==null?0:core.Latest.Tick,match=core.Match,
                battlefieldTypes=battlefieldTypes,battlefieldWarnings=battlefieldWarnings,battlefieldImpacts=battlefieldImpacts,battlefieldWaves=battlefieldWaves,
                chatSent=chatSent,chatColorsValid=chatColorsValid,chatSlots=chatSlots,
                qRequests=qRequests,skillFeedback=core.SkillFeedbackRevision,mode=core.RoomMode.ToString(),message=core.Message,skillNotice=core.SkillNotice,map=core.ActiveMap.mapId,
                nation=core.Loadouts.ContainsKey(core.Slot)?core.Loadouts[core.Slot].Nation:-1,
                players=core.Players.Count,capacity=core.RoomCapacity,seconds=core.RoomSeconds,startingHp=core.RoomStartingHp,expectedPlayers=players,expectedHp=startingHp,matchPlayers=matchPlayers,resultPlayers=core.Scores.Count
            };
            File.WriteAllText(output,JsonUtility.ToJson(result,true));
            Debug.Log("Unity smoke success="+success+" tick="+result.observedTick+" invalid="+result.invalid);
            core.Dispose();
            enabled=false;
            Application.Quit(success?0:1);
        }
        void OnDestroy() {
            core?.Dispose();
        }
    }
}
