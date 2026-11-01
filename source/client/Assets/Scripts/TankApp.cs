using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
namespace Tank  {
    public partial class TankApp : MonoBehaviour  {
        const string ServerHost="13.75.2.41";
        const int ServerTcpPort=8888,ServerUdpPort=8889;
        ClientCore client;
        GameMap map;
        string username="",password="",confirmPassword="",roomName="Đấu xe tăng";
        bool history;
        Vector2 scroll;
        ArenaRenderer arenaRenderer=new ArenaRenderer();
        double nextStep;
        float arenaX,arenaY,scale;
        string status="";
        bool leaveMatchForm;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot() {
            if(!SmokePlayer.Enabled&&FindFirstObjectByType<TankApp>()==null)new GameObject("Tank Battle").AddComponent<TankApp>();
        }
        void Awake() {
            Directory.CreateDirectory(Application.persistentDataPath);
            LoadPreferences();
            Application.runInBackground=true;
            Application.targetFrameRate=60;
            client=MapLoader.CreateClient(out map);
            client.SnapshotAccepted+=snapshot=>arenaRenderer.AcceptEffects(client,snapshot);
            nextStep=ClientCore.NowUs;
            if(ArenaPreview.Enabled)ArenaPreview.Setup(client);
            SetupMenuPreview();
            SetupCampaignPreview();
            SetupMenuPreviewQa();
            SetupShopUiPreview();
            if(ArenaPreview.Enabled&&Array.IndexOf(Environment.GetCommandLineArgs(),"--preview-leave-confirm")>=0)leaveMatchForm=true;
            if(UxTestDriver.Enabled)new GameObject("UX Test Driver").AddComponent<UxTestDriver>().App=this;
        }
        readonly AutoFireControl autoFire=new AutoFireControl();
        internal bool ChatTyping {get {return arenaRenderer.Chat.Typing;}}
        internal bool AutoFireEnabled {get {return autoFire.Enabled;}}
        internal int LastInputButtons {get;private set;}
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
            if(client.Phase!=ClientPhase.PLAYING)leaveMatchForm=false;
            arenaRenderer.Chat.Sync(client,Screen.height);
            arenaRenderer.Chat.BeforeInput(GameWindowFocus.IsForeground,AnyDialog);
            if(!settingsOpen&&GameWindowFocus.IsForeground&&(Input.GetKeyDown(KeyCode.F11)||(Input.GetKey(KeyCode.LeftAlt)&&Input.GetKeyDown(KeyCode.Return))))ToggleFullscreen();
            if(IsMenuPreview)return;
            if(ArenaPreview.Enabled) {
                ArenaPreview.Animate(client);
                UpdateShopOpeningNotice();
                if(skillAudio==null)skillAudio=new SkillSounds();
                arenaRenderer.UpdateEffects(client,Time.unscaledDeltaTime,skillAudio);
                return;
            }
            client.Poll();
            SkillAudio();
            if(skillAudio==null)skillAudio=new SkillSounds();
            arenaRenderer.UpdateEffects(client,Time.unscaledDeltaTime,skillAudio);
            ResetMenusForAccount();
            UpdateRoleplayShop();
            UpdateCampaign();
            client.Smooth(Time.unscaledDeltaTime);
            autoFire.Update(client.Phase==ClientPhase.PLAYING,GameWindowFocus.IsForeground&&!AnyDialog&&!arenaRenderer.Chat.InputBlocked,client.Phase==ClientPhase.PLAYING&&ClientPreferences.Active.Pressed(ControlAction.AutoFire,PointerInArena()));
            if(client.Phase!=ClientPhase.PLAYING) {
                LastInputButtons=0;
                nextStep=ClientCore.NowUs;
                inputWasActive=false;
                return;
            }
            map=client.ActiveMap;
            bool active=GameWindowFocus.IsForeground&&!AnyDialog&&!arenaRenderer.Chat.InputBlocked;
            if(!active&&inputWasActive) {
                TankState authoritative;
                frozenAim=client.Latest!=null&&client.Latest.World.Tanks.TryGetValue(client.Slot,out authoritative)?authoritative.Turret:client.Predicted==null?lastAim:client.Predicted.Turret;
            }
            int buttons=0,aim=lastAim;
            if(active) {
                float mouseX=Input.mousePosition.x/Mathf.Max(.01f,uiScale),mouseY=(Screen.height-Input.mousePosition.y)/Mathf.Max(.01f,uiScale);
                SkillGeometry.TryAim(mouseX,mouseY,arenaX,arenaY,scale,map.width,map.height,arenaRenderer.Shake.x,arenaRenderer.Shake.y,ref client.SkillAimX,ref client.SkillAimY);
                if(ClientPreferences.Active.Pressed(ControlAction.Skill,PointerInArena()))client.UseSkill(0);
                SkillAudio();
                buttons=ClientPreferences.Active.MovementButtons(PointerInArena())|(ClientPreferences.Active.Held(ControlAction.Fire,PointerInArena())?16:0);
                if(ClientPreferences.Active.Held(ControlAction.Sprint,PointerInArena()))buttons|=32;
                if(PointerInArena())aim=GameplayAim.Angle(Input.mousePosition.x,Screen.height-Input.mousePosition.y,uiScale,arenaX+arenaRenderer.Shake.x,arenaY+arenaRenderer.Shake.y,scale,map.height,client.Predicted);
                lastAim=aim;
            }
            buttons=autoFire.Apply(buttons);
            var sample=GameplayInput.Resolve(active,buttons,aim,frozenAim);
            LastInputButtons=sample.Buttons;
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
            fieldSubmitRequested=false;
            uiScale=1;
            viewWidth=Screen.width;
            viewHeight=Screen.height;
            bool playing=client.Phase==ClientPhase.PLAYING;
            menu.Init();
            if(playing)arenaTheme.Init();
            Matrix4x4 previous=GUI.matrix;
            GUISkin previousSkin=GUI.skin;
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(uiScale,uiScale,1));
            GUI.skin=playing?arenaTheme.Skin:menu.Skin;
            try {
                bool viewport=false;
                if(playing)arenaTheme.Background(viewWidth,viewHeight);
                else {
                    menu.Background(viewWidth,viewHeight,!CampaignOpen&&(roomForm||client.Phase<=ClientPhase.AUTHENTICATING||client.Phase==ClientPhase.LOBBY));
                    if(CampaignOpen){GUI.enabled=!settingsOpen;DrawCampaign();GUI.enabled=true;DrawPreferencesDialog();return;}
                    var layout=MenuLayout.Fit(Screen.width,Screen.height);
                    viewport=layout.Width>Screen.width||layout.Height>Screen.height;
                    viewWidth=layout.Width;viewHeight=layout.Height;
                    if(viewport)menuViewport=GUI.BeginScrollView(new Rect(0,0,Screen.width,Screen.height),menuViewport,new Rect(0,0,viewWidth,viewHeight));
                }
                GUI.enabled=!AnyDialog;
                DrawHeader(playing);
                if(client.Phase==ClientPhase.PLAYING)Arena();
                else DrawMenus();
                GUI.enabled=true;
                if(playing&&AnyDialog){menu.Init();GUI.skin=menu.Skin;}
                DrawDialogs();
                if(viewport)GUI.EndScrollView();
                DrawPreferencesDialog();
            }
            finally {
                arenaRenderer.Chat.EndGUI(AnyDialog);
                FocusedControl=GUI.GetNameOfFocusedControl();
                GUI.skin=previousSkin;
                GUI.matrix=previous;
                GUI.color=Color.white;
            }
        }
        bool pendingLogin,pendingRegister;
        string pendingUsername,pendingPassword;
        void BeginAuthentication(bool register) {
            string error=ClientCore.CredentialError(username,password);
            if(error==null&&register&&!string.Equals(password,confirmPassword,StringComparison.Ordinal))error="Mật khẩu nhập lại không khớp.";
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
                client.Connect(ServerHost,ServerTcpPort,ServerUdpPort);
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
            arenaRenderer.NetworkStatus(client,80,18,Mathf.Min(260,Mathf.Max(0,viewWidth-626)));
            var layout=ArenaLayout.Fit(viewWidth,viewHeight,map.width,map.height);
            scale=layout.Scale;arenaX=layout.X;arenaY=layout.Y;
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
                    rendered.Items.Clear();foreach(var p in latest.World.Items)rendered.Items.Add(p.Key,p.Value.Copy());
                    rendered.ItemEffects.Clear();rendered.ItemEffects.AddRange(latest.World.ItemEffects);
                    foreach(var tank in rendered.Tanks.Values) {
                        TankState current;
                        if(!latest.World.Tanks.TryGetValue(tank.Slot,out current))continue;
                        tank.QReadyTick=current.QReadyTick;
                        tank.ArmorEndTick=current.ArmorEndTick;tank.HeEndTick=current.HeEndTick;tank.InvincibilityEndTick=current.InvincibilityEndTick;tank.MirrorShots=current.MirrorShots;tank.QBaseReadyTick=current.QBaseReadyTick;
                        tank.QEndTick=current.QEndTick;
                        tank.SlowEndTick=current.SlowEndTick;tank.ProtectionFlags=current.ProtectionFlags;
                        tank.RocketSlowEndTick=current.RocketSlowEndTick;tank.RocketSlowStacks=current.RocketSlowStacks;
                        tank.QStartTick=current.QStartTick;tank.QStage=current.QStage;tank.AimX=current.AimX;tank.AimY=current.AimY;
                        tank.ConfusionEndTick=current.ConfusionEndTick;tank.FireLockEndTick=current.FireLockEndTick;tank.FogEndTick=current.FogEndTick;
                    }
                }
            }
            arenaRenderer.Draw(map,rendered,latest==null?null:latest.World,latest==null?0:latest.Tick,client.Slot,arenaX,arenaY,scale);
            if(arenaRenderer.Panel(client,layout.SidebarX,ArenaLayout.Top,ArenaLayout.SidebarWidth,layout.Height,stale)) {
                leaveMatchForm=true;GUI.FocusControl(null);
            }
            DrawShopOpeningNotice(layout.SidebarX,ArenaLayout.Top,ArenaLayout.SidebarWidth);
            if(client.Predicted!=null&&client.Predicted.Hp==0&&latest!=null) {
                int seconds=(int)Math.Ceiling(Math.Max(0,((double)client.Predicted.Respawn-latest.Tick)/30));
                float noticeWidth=Mathf.Min(430,map.width*scale-32);
                Rect respawn=new Rect(arenaX+(map.width*scale-noticeWidth)/2,arenaY+map.height*scale/2-25,noticeWidth,50);
                CartoonUi.Panel(respawn,CartoonUi.PanelDark,12,3);
                GUI.Label(new Rect(respawn.x+14,respawn.y+12,respawn.width-28,30),"Xe bị phá hủy · Hồi sinh sau "+seconds+" giây",arenaTheme.White);
            }
            if(client.AfkWarning) {
                int remaining=Mathf.Max(0,ClientCore.AfkGraceSeconds-(int)((ClientCore.NowUs-client.AfkWarnedAt)/1e6));
                float noticeWidth=Mathf.Min(500,map.width*scale-32);
                Rect warn=new Rect(arenaX+(map.width*scale-noticeWidth)/2,arenaY+16,noticeWidth,64);
                CartoonUi.Panel(warn,CartoonUi.Gold,12,3);
                GUI.Label(new Rect(warn.x+14,warn.y+8,warn.width-28,52),"Bạn đang AFK · Về sảnh sau "+remaining+" giây\nDi chuyển, ngắm hoặc bắn để tiếp tục.",afkText??(afkText=CartoonUi.Label(16,CartoonUi.Ink)));
            }
        }
        void DrawLeaveMatchDialog() {
            if(!leaveMatchForm)return;
            if(client.Phase!=ClientPhase.PLAYING){leaveMatchForm=false;return;}
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(.03f,.06f,.1f,.75f));
            var rect=new Rect((viewWidth-480)/2,(viewHeight-256)/2,480,256);
            menu.Card(rect,"RỜI TRẬN ĐẤU?");
            GUI.Label(new Rect(rect.x+26,rect.y+87,rect.width-52,64),"Bạn sẽ trở về sảnh. Trận đấu của những người chơi còn lại vẫn tiếp tục.",menu.Text);
            if(GUI.Button(new Rect(rect.x+24,rect.yMax-78,204,52),"Tiếp tục chơi",menu.Secondary))leaveMatchForm=false;
            if(GUI.Button(new Rect(rect.x+244,rect.yMax-78,212,52),"Rời trận",menu.Danger)){leaveMatchForm=false;client.Leave();}
            if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){leaveMatchForm=false;Event.current.Use();}
        }
        void OnDestroy() {
            menu.Dispose();
            arenaTheme.Dispose();
            CartoonUi.Dispose();
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
