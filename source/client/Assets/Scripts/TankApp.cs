using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
namespace Tank  {
    [Serializable] public class ConnectionSettings  {
        public string host="127.0.0.1";
        public int tcpPort=8888,udpPort=8889;
    }
    public partial class TankApp : MonoBehaviour  {
        ClientCore client;
        GameMap map;
        ConnectionSettings connection;
        string username="",password="",roomName="Đấu xe tăng",hostText,tcpText,udpText;
        bool settings,history;
        Vector2 scroll;
        ArenaRenderer arenaRenderer=new ArenaRenderer();
        double nextStep;
        float arenaX,arenaY,scale;
        string status="";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot() {
            if(!SmokePlayer.Enabled&&FindFirstObjectByType<TankApp>()==null)new GameObject("Tank Battle").AddComponent<TankApp>();
        }
        void Awake() {
            Directory.CreateDirectory(Application.persistentDataPath);
            Application.runInBackground=true;
            Application.targetFrameRate=60;
            client=MapLoader.CreateClient(out map);
            string local=Path.Combine(Application.persistentDataPath,"connection.json");
            connection=JsonUtility.FromJson<ConnectionSettings>(File.ReadAllText(File.Exists(local)?local:Path.Combine(Application.streamingAssetsPath,"connection.json")));
            hostText=connection.host;
            tcpText=connection.tcpPort.ToString();
            udpText=connection.udpPort.ToString();
            nextStep=ClientCore.NowUs;
            if(ArenaPreview.Enabled)ArenaPreview.Setup(client);
            SetupMenuPreview();
            if(UxTestDriver.Enabled)new GameObject("UX Test Driver").AddComponent<UxTestDriver>().App=this;
        }
        readonly AutoFireControl autoFire=new AutoFireControl();
        bool inputWasActive;
        int frozenAim,lastAim;
        SkillSounds skillAudio;
        int heardSkillPress,heardSkillFeedback;
        void SkillAudio() {
            if(heardSkillPress==client.SkillPressRevision&&heardSkillFeedback==client.SkillFeedbackRevision)return;
            if(skillAudio==null)skillAudio=new SkillSounds();
            if(heardSkillPress!=client.SkillPressRevision) {heardSkillPress=client.SkillPressRevision;skillAudio.Press();}
            if(heardSkillFeedback!=client.SkillFeedbackRevision) {heardSkillFeedback=client.SkillFeedbackRevision;skillAudio.Confirm(client.LastSkillSucceeded);}
        }
        void Update() {
            if(GameWindowFocus.IsForeground&&(Input.GetKeyDown(KeyCode.F11)||(Input.GetKey(KeyCode.LeftAlt)&&Input.GetKeyDown(KeyCode.Return))))ToggleFullscreen();
            if(IsMenuPreview)return;
            if(ArenaPreview.Enabled) {
                ArenaPreview.Animate(client);
                if(skillAudio==null)skillAudio=new SkillSounds();
                arenaRenderer.UpdateEffects(client,Time.unscaledDeltaTime,skillAudio);
                return;
            }
            client.Poll();
            SkillAudio();
            if(skillAudio==null)skillAudio=new SkillSounds();
            arenaRenderer.UpdateEffects(client,Time.unscaledDeltaTime,skillAudio);
            ResetMenusForAccount();
            client.Smooth(Time.unscaledDeltaTime);
            autoFire.Update(client.Phase==ClientPhase.PLAYING,GameWindowFocus.IsForeground&&!AnyDialog,client.Phase==ClientPhase.PLAYING&&Input.GetMouseButtonDown(1)&&PointerInArena());
            if(client.Phase!=ClientPhase.PLAYING) {
                nextStep=ClientCore.NowUs;
                inputWasActive=false;
                return;
            }
            map=client.ActiveMap;
            bool active=GameWindowFocus.IsForeground&&!AnyDialog;
            if(!active&&inputWasActive) {
                TankState authoritative;
                frozenAim=client.Latest!=null&&client.Latest.World.Tanks.TryGetValue(client.Slot,out authoritative)?authoritative.Turret:client.Predicted==null?lastAim:client.Predicted.Turret;
            }
            int buttons=0,aim=lastAim;
            if(active) {
                float mouseX=Input.mousePosition.x/Mathf.Max(.01f,uiScale),mouseY=(Screen.height-Input.mousePosition.y)/Mathf.Max(.01f,uiScale);
                SkillGeometry.TryAim(mouseX,mouseY,arenaX,arenaY,scale,map.width,map.height,arenaRenderer.Shake.x,arenaRenderer.Shake.y,ref client.SkillAimX,ref client.SkillAimY);
                if(Input.GetKeyDown(KeyCode.Q))client.UseSkill(0);
                SkillAudio();
                buttons=GameplayInput.MovementButtons(Input.GetKey(KeyCode.W),Input.GetKey(KeyCode.S),Input.GetKey(KeyCode.A),Input.GetKey(KeyCode.D),Input.GetKey(KeyCode.UpArrow),Input.GetKey(KeyCode.DownArrow),Input.GetKey(KeyCode.LeftArrow),Input.GetKey(KeyCode.RightArrow))|((Input.GetMouseButton(0)&&PointerInArena())||Input.GetKey(KeyCode.Space)?16:0);
                if(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift))buttons|=32;
                if(PointerInArena())aim=GameplayAim.Angle(Input.mousePosition.x,Screen.height-Input.mousePosition.y,uiScale,arenaX+arenaRenderer.Shake.x,arenaY+arenaRenderer.Shake.y,scale,map.height,client.Predicted);
                lastAim=aim;
            }
            buttons=autoFire.Apply(buttons);
            var sample=GameplayInput.Resolve(active,buttons,aim,frozenAim);
            inputWasActive=active;
            double now=ClientCore.NowUs;
            int steps=0;
            while(now>=nextStep&&steps++<3) {
                client.GenerateInput(sample.Buttons,sample.Aim);
                nextStep+=1e6/30;
            }
            if(now>=nextStep)nextStep=now+1e6/30;
        }
        bool PointerInArena() {
            float px=Input.mousePosition.x/Mathf.Max(.01f,uiScale),py=(Screen.height-Input.mousePosition.y)/Mathf.Max(.01f,uiScale);
            return px>=arenaX&&px<=arenaX+map.width*scale&&py>=arenaY&&py<=arenaY+map.height*scale;
        }
        void OnGUI() {
            uiScale=1;
            viewWidth=Screen.width;
            viewHeight=Screen.height;
            menu.Init();
            Matrix4x4 previous=GUI.matrix;
            GUISkin previousSkin=GUI.skin;
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(uiScale,uiScale,1));
            GUI.skin=menu.Skin;
            try {
                menu.Background(viewWidth,viewHeight);
                GUI.enabled=!AnyDialog;
                DrawHeader();
                if(client.Phase==ClientPhase.PLAYING)Arena();
                else DrawMenus();
                GUI.enabled=true;
                DrawDialogs();
                FocusedControl=GUI.GetNameOfFocusedControl();
            }
            finally {
                GUI.skin=previousSkin;
                GUI.matrix=previous;
                GUI.color=Color.white;
            }
        }
        bool pendingLogin,pendingRegister;
        string pendingUsername,pendingPassword;
        void BeginAuthentication(bool register) {
            string error=ClientCore.CredentialError(username,password);
            status="";
            if(error!=null) {
                client.Message=error;
                return;
            }
            if(client.Phase==ClientPhase.DISCONNECTED) {
                pendingUsername=username;
                pendingPassword=password;
                pendingRegister=register;
                pendingLogin=!register;
                client.Connect(connection.host,connection.tcpPort,connection.udpPort);
            }
            else if(register)client.Register(username,password);
            else client.Login(username,password);
        }
        string DisplayMessage() {
            switch(client.Message) {
                case "ROOM_PASSWORD_REQUIRED":return "Nhập mật khẩu phòng.";
                case "ROOM_PASSWORD_INCORRECT":return "Mật khẩu phòng không đúng.";
                case "ROOM_FULL":return "Phòng đã đầy. Hãy chọn phòng khác.";
                case "ROOM_BUSY":return "Phòng đang đấu. Hãy chờ trận tiếp theo.";
                case "NOT_READY":return "Cần ít nhất 2 người và tất cả đã sẵn sàng.";
                case "NOT_HOST":return "Chỉ chủ phòng có thể thực hiện thao tác này.";
                case "INVALID_STATE":return "Trạng thái phòng đã thay đổi. Vui lòng thử lại.";
                case "INVALID_REQUEST":return "Thông tin không hợp lệ. Kiểm tra lại các ô nhập.";
                case "RATE_LIMITED":return "Đã thử đăng ký/đăng nhập quá nhiều lần từ cùng IP. Vui lòng chờ rồi thử lại.";
                case "INVALID_CREDENTIALS":return "Tên tài khoản hoặc mật khẩu không đúng.";
                case "USERNAME_EXISTS":return "Tên tài khoản đã tồn tại. Bấm Đăng nhập hoặc chọn tên khác.";
                case "ALREADY_LOGGED_IN":return "Tài khoản đang đăng nhập ở cửa sổ khác. Mỗi cửa sổ cần một tài khoản riêng.";
                case "DATABASE_UNAVAILABLE":return "Server chưa xử lý được dữ liệu tài khoản. Vui lòng thử lại sau.";
                case "Đăng ký thành công":return "Đăng ký thành công. Bấm Đăng nhập để vào lobby.";
                default:return client.Message;
            }
        }
        void LateUpdate() {
            if(ArenaPreview.Enabled||IsMenuPreview)return;
            if(client.Phase==ClientPhase.DISCONNECTED) {
                pendingLogin=pendingRegister=false;
                pendingPassword=null;
            }
            if(client.Phase==ClientPhase.AUTHENTICATING) {
                if(pendingLogin) {
                    pendingLogin=false;
                    client.Login(pendingUsername,pendingPassword);
                    pendingPassword=null;
                }
                if(pendingRegister) {
                    pendingRegister=false;
                    client.Register(pendingUsername,pendingPassword);
                    pendingPassword=null;
                }
            }
        }
        void Arena() {
            map=client.ActiveMap;
            arenaRenderer.NetworkStatus(client,230,12,Mathf.Max(0,viewWidth-588));
            float panelWidth=294;
            scale=Mathf.Max(1,Math.Min((viewWidth-panelWidth-70)/map.width,(viewHeight-112)/map.height));
            arenaX=Mathf.Max(16,(viewWidth-map.width*scale-22-panelWidth)/2);
            arenaY=88;
            var latest=client.Latest;
            WorldState rendered=null;
            bool stale=ClientCore.NowUs-client.LastArrivalUs>500000;
            if(latest!=null) {
                double renderTime=ClientCore.NowUs+client.OffsetUs-100000;
                rendered=Rendering.Interpolate(client.Snapshots,renderTime,map);
                if(rendered!=null) {
                    var hidden=new System.Collections.Generic.List<int>();
                    foreach(var current in rendered.Tanks.Values)if(stale&&current.Slot!=client.Slot)hidden.Add(current.Slot);
                    foreach(int slot in hidden)rendered.Tanks.Remove(slot);
                    if(client.Predicted!=null) {
                        var local=client.Predicted.Copy();
                        float px=local.X+client.RenderOffsetX,py=local.Y+client.RenderOffsetY;
                        if(!map.Blocked(px,py,local.Body,latest.World.Bricks)) {
                            local.X=px;
                            local.Y=py;
                        }
                        rendered.Tanks[local.Slot]=local;
                    }
                    if(stale)rendered.Bullets.Clear();
                    rendered.Effects.Clear();rendered.Effects.AddRange(latest.World.Effects);
                    foreach(var tank in rendered.Tanks.Values) {
                        TankState current;
                        if(!latest.World.Tanks.TryGetValue(tank.Slot,out current))continue;
                        tank.QReadyTick=current.QReadyTick;
                        tank.QEndTick=current.QEndTick;
                        tank.SlowEndTick=current.SlowEndTick;tank.ProtectionFlags=current.ProtectionFlags;
                        tank.QStartTick=current.QStartTick;tank.QStage=current.QStage;tank.AimX=current.AimX;tank.AimY=current.AimY;
                        tank.ConfusionEndTick=current.ConfusionEndTick;tank.FireLockEndTick=current.FireLockEndTick;tank.FogEndTick=current.FogEndTick;
                    }
                }
            }
            arenaRenderer.Draw(map,rendered,latest==null?null:latest.World,latest==null?0:latest.Tick,client.Slot,arenaX,arenaY,scale);
            float panel=arenaX+map.width*scale+22;
            arenaRenderer.Panel(client,panel,arenaY,panelWidth,map.height*scale,stale,autoFire.Enabled);
            if(client.Predicted!=null&&client.Predicted.Hp==0&&latest!=null) {
                int seconds=(int)Math.Ceiling(Math.Max(0,((double)client.Predicted.Respawn-latest.Tick)/30));
                Rect respawn=new Rect(arenaX+16,arenaY+map.height*scale/2-25,map.width*scale-32,50);
                ArenaRenderer.Fill(respawn,new Color(.06f,.13f,.11f,.92f));
                GUI.Label(new Rect(respawn.x+14,respawn.y+12,respawn.width-28,30),"Xe bị phá hủy · Hồi sinh sau "+seconds+" giây",menu.White);
            }
            if(client.AfkWarning) {
                int remaining=Mathf.Max(0,ClientCore.AfkGraceSeconds-(int)((ClientCore.NowUs-client.AfkWarnedAt)/1e6));
                Rect warn=new Rect(arenaX+16,arenaY+16,map.width*scale-32,68);
                ArenaRenderer.Fill(warn,new Color(.99f,.84f,.48f,.96f));
                GUI.Label(new Rect(warn.x+14,warn.y+8,warn.width-28,52),"Bạn đang AFK · Về sảnh sau "+remaining+" giây\nDi chuyển, ngắm hoặc bắn để tiếp tục.",menu.Text);
            }
        }
        void OnDestroy() {
            arenaRenderer.Dispose();
            client?.Dispose();
            skillAudio?.Dispose();
        }
        public string FocusedControl  {
            get;
            private set;
        }
    }
}
