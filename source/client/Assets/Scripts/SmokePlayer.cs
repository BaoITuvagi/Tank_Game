using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
namespace Tank  {
    /** Opt-in command-line integration harness, absent from normal player UI. */
    public class SmokePlayer:MonoBehaviour  {
        ClientCore core;
        string username,output;
        bool auth,login,join,ready,start,history;
        double began,lastHistory;
        int expectedTicks;
        uint change;
        int buttons=17,angle;
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
            GameMap initial;
            core=MapLoader.CreateClient(out initial);
            username=Arg("--account","unity000");
            output=Arg("--output",Path.Combine(Application.persistentDataPath,"smoke-result.json"));
            core.Connect(Arg("--host","127.0.0.1"),int.Parse(Arg("--tcp","20888")),int.Parse(Arg("--udp","20889")));
            expectedTicks=int.Parse(Arg("--expected-ticks","5400"));
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
                    core.Create("Unity smoke");
                    join=true;
                }
                else {
                    var room=core.Rooms.FirstOrDefault(r=>r.Name=="Unity smoke");
                    if(room!=null) {
                        core.Join(room.Id);
                        join=true;
                    }
                }
            }
            if(core.Phase==ClientPhase.ROOM&&!ready) {
                ready=true;
                core.Ready(true);
            }
            if(core.Phase==ClientPhase.ROOM&&core.Slot==core.Host&&!start&&core.Players.Count>=2&&core.Players.All(p=>p.Ready)) {
                start=true;
                core.StartMatch();
            }
            if(core.Phase==ClientPhase.PLAYING) {
                uint tick=core.Latest==null?0:core.Latest.Tick;
                if(tick>=change) {
                    change=tick+30;
                    buttons=(tick/30%2==0?1:8)|16;
                    angle=(int)(tick*17%36000);
                }
                for(int i=0;i<3;i++)if(!core.GenerateInput(buttons,angle))break;
            }
            if(core.Ended&&ClientCore.NowUs-lastHistory>2e6) {
                history=true;
                lastHistory=ClientCore.NowUs;
                core.GetHistory();
            }
            if(core.Ended&&core.History.Any(h=>h.Match==core.Match)) {
                Save(SmokeAcceptance.Passed(core,expectedTicks));
                return;
            }
            if(ClientCore.NowUs-began>(expectedTicks/30.0+60)*1e6||(auth&&core.Phase==ClientPhase.DISCONNECTED))Save(false);
        }
        [Serializable] class SmokeResult  {
            public bool success;
            public long decoded,invalid;
            public int historyRows,endReason,expectedTicks;
            public uint observedTick;
            public ulong match;
        }
        void Save(bool success) {
            var result=new SmokeResult {
                success=success,decoded=core.Decoded,invalid=core.Invalid,historyRows=core.History.Count, endReason=core.EndReason,expectedTicks=expectedTicks,observedTick=core.Latest==null?0:core.Latest.Tick,match=core.Match
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
