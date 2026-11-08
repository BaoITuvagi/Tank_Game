using System;
using UnityEngine;
namespace Tank {
    public partial class TankApp {
        // Matches the old local UI harness so existing demo accounts still work.
        const string LocalUiPassword="ux-test-only-2026";
        enum LocalUiStage { None,Connecting,Registering,LoggingIn }
        LocalUiStage localUiStage;
        double localUiDeadline;
        string localUiAccount;
        static string LocalUiArgument(string[] args,string name,string fallback) {
            int index=Array.IndexOf(args,name);
            if(index<0)return fallback;
            if(index+1>=args.Length||args[index+1].StartsWith("-",StringComparison.Ordinal))throw new ArgumentException("Thiếu giá trị cho "+name+".");
            return args[index+1];
        }
        static int LocalUiPort(string[] args,string name,string fallback) {
            int port;
            if(!int.TryParse(LocalUiArgument(args,name,fallback),out port)||port<1||port>65535)throw new ArgumentException(name+" phải từ 1 đến 65535.");
            return port;
        }
        void SetupLocalUiTest() {
            string[] args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--ui-test")<0)return;
            try {
                localUiAccount=LocalUiArgument(args,"--account","uxplayer");
                string error=ClientCore.CredentialError(localUiAccount,LocalUiPassword);
                if(error!=null)throw new ArgumentException(error);
                int tcp=LocalUiPort(args,"--tcp","23888"),udp=LocalUiPort(args,"--udp","23889");
                username=localUiAccount;
                localUiStage=LocalUiStage.Connecting;
                localUiDeadline=ClientCore.NowUs+20e6;
                // Test credentials must only go to the local demo server.
                client.Connect("127.0.0.1",tcp,udp);
            }catch(ArgumentException e){StopLocalUiTest(e.Message);}
        }
        void StopLocalUiTest(string message) {
            localUiStage=LocalUiStage.None;
            client.Message=message;
            Debug.LogWarning("LOCAL_UI_LOGIN failed: "+message);
        }
        void UpdateLocalUiTest() {
            if(localUiStage==LocalUiStage.None)return;
            if(client.Phase>=ClientPhase.LOBBY) {
                localUiStage=LocalUiStage.None;
                Debug.Log("LOCAL_UI_LOGIN account="+client.AccountName+" phase="+client.Phase);
                return;
            }
            if(client.Phase==ClientPhase.DISCONNECTED){StopLocalUiTest(client.Message);return;}
            if(ClientCore.NowUs>=localUiDeadline){StopLocalUiTest("Tự đăng nhập local quá hạn. Kiểm tra server demo và cổng kết nối.");return;}
            if(client.Phase!=ClientPhase.AUTHENTICATING||client.AuthenticationPending)return;
            if(localUiStage==LocalUiStage.Connecting) {
                localUiStage=LocalUiStage.Registering;
                client.Register(localUiAccount,LocalUiPassword);
            }else if(localUiStage==LocalUiStage.Registering&&(client.Message=="Đăng ký thành công"||client.Message=="USERNAME_EXISTS")) {
                localUiStage=LocalUiStage.LoggingIn;
                client.Login(localUiAccount,LocalUiPassword);
            }else StopLocalUiTest(client.Message);
        }
    }
}
