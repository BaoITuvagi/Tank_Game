using System;
using System.Text;
using UnityEngine;
namespace Tank  {
    public partial class TankApp  {
        MenuTheme menu=new MenuTheme();
        int capacityChoice=2,durationChoice=1;
        bool registerTab,roomForm,editingRoom,profileForm,joinForm,useRoomPassword;
        string startingHpText="3",roomPassword="",joinPassword="",displayName="",formError="";
        uint joinTarget;
        int savedNameRevision,savedSettingsRevision;
        readonly int[] capacities= {
            2,3,4,5,6,7,8
        },durations= {
            60,180,300,600
        };
        float viewWidth,viewHeight,uiScale;
        int windowWidth=1280,windowHeight=800;
        int observedAccountRevision;
        void ResetMenusForAccount() {
            if(observedAccountRevision==client.AccountRevision)return;
            observedAccountRevision=client.AccountRevision;
            history=roomForm=editingRoom=profileForm=joinForm=false;
            scroll=Vector2.zero;
            roomPassword=joinPassword=displayName=formError="";
            if(client.Phase==ClientPhase.AUTHENTICATING&&client.Account==0)password="";
        }
        bool AnyDialog  {
            get {
                return profileForm||joinForm;
            }
        }
        public ClientCore Core  {
            get {
                return client;
            }
        }
        public void ToggleFullscreen() {
            if(Screen.fullScreen)Screen.SetResolution(windowWidth,windowHeight,FullScreenMode.Windowed);
            else {
                windowWidth=Screen.width;
                windowHeight=Screen.height;
                var r=Screen.currentResolution;
                Screen.SetResolution(r.width,r.height,FullScreenMode.FullScreenWindow);
            }
        }
        void DrawHeader() {
            GUI.Label(new Rect(24,17,360,36),"TANK BATTLE",new GUIStyle(menu.Header) {
                fontSize=24
            });
            if(client.Phase>=ClientPhase.LOBBY) {
                if(GUI.Button(new Rect(viewWidth-136,14,112,40),"Đăng xuất",menu.Secondary)) {
                    client.Logout();
                    roomForm=profileForm=joinForm=false;
                }
                if(GUI.Button(new Rect(viewWidth-342,14,190,40),string.IsNullOrEmpty(client.Nickname)?"Tên hiển thị":client.Nickname,menu.Secondary)) {
                    displayName=client.Nickname;
                    profileForm=true;
                    formError="";
                    savedNameRevision=client.NameRevision;
                    GUI.FocusControl(null);
                }
            }
        }
        bool Button(string label,bool enabled=true,bool secondary=false) {
            bool prior=GUI.enabled;
            GUI.enabled=prior&&enabled;
            bool result=GUILayout.Button(label,secondary?menu.Secondary:menu.Primary);
            GUI.enabled=prior;
            return result;
        }
        string Field(string key,string label,string value,int limit=64,bool secret=false) {
            GUILayout.Label(label,menu.Name);
            GUI.SetNextControlName(key);
            string result=secret?GUILayout.PasswordField(value,'•',limit):GUILayout.TextField(value,limit);
            Rect r=GUILayoutUtility.GetLastRect();
            bool qa=UxTestDriver.Arg("--qa-field-focus","")==key;
            if(qa)GUI.FocusControl(key);
            if(Event.current.type==EventType.Repaint&&GUI.GetNameOfFocusedControl()==key) {
                var editor=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),GUIUtility.keyboardControl);
                if(editor.cursorIndex==editor.selectIndex&&(qa||Time.unscaledTime%1f<.6f)) {
                    string visible=secret?new string('•',result.Length):result;
                    Vector2 position=GUI.skin.textField.GetCursorPixelPosition(r,new GUIContent(visible),Mathf.Clamp(editor.cursorIndex,0,result.Length));
                    float cx=Mathf.Clamp(position.x-editor.scrollOffset.x,r.x+12,r.xMax-8);
                    ArenaRenderer.Fill(new Rect(cx,r.center.y-9,2,18),menu.Ink);
                }
            }
            return result;
        }
        void Feedback() {
            string value=string.IsNullOrEmpty(formError)?DisplayMessage()+status:formError;
            if(string.IsNullOrWhiteSpace(value)||value=="Đăng nhập thành công"||value=="Kết nối thành công"||value=="Đã tiếp tục hoạt động")return;
            GUILayout.Space(8);
            GUILayout.Label(value,menu.Hint);
        }
        Rect Content  {
            get {
                return new Rect(24,88,viewWidth-48,viewHeight-112);
            }
        }
        void DrawMenus() {
            Rect area=Content;
            if(client.Phase<=ClientPhase.AUTHENTICATING) {
                bool illustration=viewWidth>=1100;
                float w=illustration?area.width*.44f:Mathf.Min(560,area.width);
                Rect card=new Rect(illustration?area.xMax-w:area.center.x-w/2,area.y,w,area.height);
                if(illustration)menu.HeroScene(new Rect(area.x+24,area.y+20,area.width-w-64,area.height));
                menu.Card(card,settings?"Kết nối":registerTab?"Đăng ký":"Đăng nhập");
                GUILayout.BeginArea(new Rect(card.x+24,card.y+80,card.width-48,card.height-104));
                scroll=GUILayout.BeginScrollView(scroll);
                if(settings)ConnectionForm();
                else AuthenticationForm();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if(roomForm) {
                DrawRoomForm(area);
                return;
            }
            if(client.Phase==ClientPhase.LOBBY) {
                DrawLobby(area);
                return;
            }
            if(client.Phase==ClientPhase.RESULTS) {
                DrawResults(area);
                return;
            }
            DrawRoom(area);
        }
        void AuthenticationForm() {
            GUILayout.BeginHorizontal();
            if(Button("Đăng nhập",true,registerTab))registerTab=false;
            if(Button("Đăng ký",true,!registerTab))registerTab=true;
            GUILayout.EndHorizontal();
            GUILayout.Space(18);
            username=Field("account","Tên đăng nhập",username,24);
            GUILayout.Space(12);
            password=Field("account-password","Mật khẩu",password,64,true);
            GUILayout.Space(18);
            bool busy=client.Phase==ClientPhase.CONNECTING||client.AuthenticationPending||pendingLogin||pendingRegister;
            if(Button(busy?"Đang xử lý…":registerTab?"Đăng ký":"Đăng nhập",!busy&&client.AuthenticationRetrySeconds<=0))BeginAuthentication(registerTab);
            if(client.Message=="Đăng ký thành công"&&Button("Đăng nhập ngay")) {
                registerTab=false;
                BeginAuthentication(false);
            }
            if(client.AuthenticationRetrySeconds>0)GUILayout.Label("Thử lại sau "+Math.Ceiling(client.AuthenticationRetrySeconds)+" giây.",menu.Hint);
            Feedback();
            GUILayout.Space(12);
            if(Button("Kết nối",!busy,true)) {
                settings=true;
                scroll=Vector2.zero;
                formError="";
            }
        }
        void ConnectionForm() {
            hostText=Field("server","Địa chỉ server",hostText);
            tcpText=Field("tcp","TCP",tcpText,5);
            udpText=Field("udp","UDP",udpText,5);
            GUILayout.Space(16);
            if(Button("Lưu")) {
                int tp,up;
                if(int.TryParse(tcpText,out tp)&&int.TryParse(udpText,out up)&&tp>0&&tp<=65535&&up>0&&up<=65535&&!string.IsNullOrWhiteSpace(hostText)) {
                    connection.host=hostText.Trim();
                    connection.tcpPort=tp;
                    connection.udpPort=up;
                    System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath,"connection.json"),JsonUtility.ToJson(connection));
                    client.Dispose();
                    settings=false;
                    status="";
                    client.Message="";
                }
                else formError="Địa chỉ hoặc cổng không hợp lệ.";
            }
            if(Button("Quay lại",true,true)) {
                settings=false;
                formError="";
            }
            Feedback();
        }
        void OpenRoomForm(bool edit) {
            editingRoom=edit;
            roomForm=true;
            roomPassword="";
            formError="";
            client.Message="";
            scroll=Vector2.zero;
            savedSettingsRevision=client.SettingsRevision;
            if(edit) {
                roomName=client.RoomName;
                capacityChoice=Mathf.Clamp(client.RoomCapacity-2,0,6);
                durationChoice=Mathf.Max(0,Array.IndexOf(durations,client.RoomSeconds));
                useRoomPassword=client.RoomLocked;
                startingHpText=client.RoomStartingHp.ToString();
            }
            else {
                roomName="Đấu xe tăng";
                capacityChoice=2;
                durationChoice=1;
                useRoomPassword=false;
                startingHpText="3";
            }
        }
        void DrawLobby(Rect area) {
            menu.Card(area,history?"Lịch sử trận":"Phòng đang mở");
            GUILayout.BeginArea(new Rect(area.x+24,area.y+84,164,area.height-108));
            if(Button("Tạo phòng"))OpenRoomForm(false);
            if(Button("Làm mới",true,true)) {
                if(history) {
                    client.CloseHistoryDetail();
                    client.GetHistory();
                }
                else client.Refresh();
            }
            if(Button(history?"Danh sách phòng":"Lịch sử",true,true)) {
                history=!history;
                client.CloseHistoryDetail();
                scroll=Vector2.zero;
                if(history)client.GetHistory();
            }
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(area.x+212,area.y+84,area.width-236,area.height-108));
            scroll=GUILayout.BeginScrollView(scroll);
            if(history) {
                if(client.SelectedHistoryMatch!=0)DrawHistoryDetail();
                else {
                    if(client.History.Count==0)GUILayout.Label("Chưa có trận đấu đã lưu.",menu.Hint);
                    foreach(var h in client.History) {
                        string label="Hạng "+h.Rank+" · "+h.Kills+" hạ gục / "+h.Deaths+" tử trận\n"+DateTimeOffset.FromUnixTimeMilliseconds((long)h.Ended).LocalDateTime.ToString("dd/MM/yyyy HH:mm")+" · Xem chi tiết";
                        if(GUILayout.Button(label,new GUIStyle(menu.Secondary) {
                            alignment=TextAnchor.MiddleLeft,wordWrap=true,fixedHeight=76
                        },GUILayout.MinHeight(76))) {
                            client.GetHistoryDetail(h.Match);
                        }
                    }
                }
            }
            else {
                if(client.Rooms.Count==0)GUILayout.Label("Chưa có phòng.",menu.Hint);
                foreach(var room in client.Rooms) {
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.BeginVertical();
                    GUILayout.Label(room.Name+(room.Locked?"  •  Có mật khẩu":""),menu.Section);
                    GUILayout.Label(room.Players+" / "+room.Capacity+" · "+RoomPhaseLabel(room.Phase),menu.Hint);
                    GUILayout.EndVertical();
                    bool returning=room.Phase==RoomPhase.RUNNING&&room.CanRejoin;
                    bool available=(room.Phase==RoomPhase.WAITING||returning)&&room.Players<room.Capacity;
                    if(Button(available?(returning?"Vào đấu":"Vào phòng"):room.Phase==RoomPhase.WAITING?"Đã đầy":RoomPhaseLabel(room.Phase),available)) {
                        client.Message="";
                        formError="";
                        if(room.Locked&&!returning) {
                            joinTarget=room.Id;
                            joinPassword="";
                            joinForm=true;
                        }
                        else client.Join(room.Id);
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            Feedback();
            GUILayout.EndArea();
        }
        static string EndReasonLabel(int reason) {
            return reason==0?"Hết thời gian":reason==1?"Không đủ người chơi":"Server dừng trận";
        }
        void DrawHistoryDetail() {
            if(Button("← Lịch sử trận",true,true))client.CloseHistoryDetail();
            var detail=client.HistoryDetail;
            if(detail==null) {
                GUILayout.Label(client.HistoryDetailPending?"Đang tải chi tiết…":"Không tải được chi tiết trận.",menu.Hint);
                if(!client.HistoryDetailPending&&client.SelectedHistoryMatch!=0&&Button("Thử lại",true,true))client.GetHistoryDetail(client.SelectedHistoryMatch);
                return;
            }
            GUILayout.Label("Trận #"+detail.Match,menu.Section);
            GUILayout.Label(DateTimeOffset.FromUnixTimeMilliseconds((long)detail.Ended).LocalDateTime.ToString("dd/MM/yyyy HH:mm")+" · "+detail.DurationSeconds+" giây · Bản đồ "+detail.MapId,menu.Hint);
            GUILayout.Label(EndReasonLabel(detail.Reason),menu.Hint);
            GUILayout.Space(12);
            foreach(var score in detail.Players) {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("#"+score.Rank+"  "+score.Name+(score.Slot==detail.OwnSlot?" • bạn":""),menu.Section);
                GUILayout.Label(score.Kills+" hạ gục / "+score.Deaths+" tử trận"+(score.Disconnected?" · Rời trận sớm":""),menu.Text);
                GUILayout.EndVertical();
            }
        }
        void DrawRoomForm(Rect area) {
            if(editingRoom&&client.SettingsRevision!=savedSettingsRevision) {
                roomForm=false;
                formError="";
                return;
            }
            if(!editingRoom&&client.Phase==ClientPhase.ROOM) {
                roomForm=false;
                formError="";
                return;
            }
            float w=Mathf.Min(760,area.width);
            Rect card=new Rect(area.center.x-w/2,area.y,w,area.height);
            menu.Card(card,editingRoom?"Cài đặt phòng":"Tạo phòng");
            GUILayout.BeginArea(new Rect(card.x+24,card.y+80,card.width-48,card.height-104));
            scroll=GUILayout.BeginScrollView(scroll);
            roomName=Field("room-name","Tên phòng",roomName,24);
            GUILayout.Space(10);
            if(editingRoom) {
                useRoomPassword=GUILayout.Toggle(useRoomPassword,"Dùng mật khẩu");
                if(useRoomPassword)roomPassword=Field("room-password",client.RoomLocked?"Mật khẩu mới (để trống để giữ nguyên)":"Mật khẩu",roomPassword,32,true);
            }
            else roomPassword=Field("room-password","Mật khẩu phòng (không bắt buộc)",roomPassword,32,true);
            GUILayout.Space(14);
            GUILayout.Label("Số người tối đa",menu.Name);
            capacityChoice=GUILayout.SelectionGrid(capacityChoice,new[] {
                "2","3","4","5","6","7","8"
            },7,menu.Secondary);
            GUILayout.Space(14);
            GUILayout.Label("Thời gian trận",menu.Name);
            durationChoice=GUILayout.SelectionGrid(durationChoice,new[] {
                "1 phút","3 phút","5 phút","10 phút"
            },4,menu.Secondary);
            GUILayout.Space(14);
            startingHpText=Field("starting-hp","Máu ban đầu / mỗi lần hồi sinh (1–30)",startingHpText,2);
            GUILayout.Space(18);
            if(Button("Xác nhận")) {
                formError="";
                int hp;
                int mode=!useRoomPassword?1:string.IsNullOrEmpty(roomPassword)&&client.RoomLocked?0:2;
                if(string.IsNullOrWhiteSpace(roomName)||Encoding.UTF8.GetByteCount(roomName.Trim())>48)formError="Tên phòng quá dài hoặc còn trống.";
                else if(!int.TryParse(startingHpText,out hp)||hp<1||hp>30)formError="Máu ban đầu phải là số từ 1 đến 30.";
                else if(editingRoom&&capacities[capacityChoice]<client.Players.Count)formError="Số người tối đa không thể nhỏ hơn số người đang ở phòng.";
                else if(editingRoom&&mode==2&&roomPassword.Length==0)formError="Nhập mật khẩu hoặc bỏ chọn dùng mật khẩu.";
                else if(editingRoom)client.ConfigureRoom(roomName.Trim(),capacities[capacityChoice],durations[durationChoice],mode,roomPassword,hp);
                else client.Create(roomName.Trim(),capacities[capacityChoice],durations[durationChoice],roomPassword,hp);
            }
            if(Button("Hủy",true,true)) {
                roomForm=false;
                formError="";
                client.Message="";
            }
            Feedback();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        static string RoomPhaseLabel(RoomPhase phase) {
            return phase==RoomPhase.WAITING?"Đang chờ":phase==RoomPhase.PREPARING?"Chuẩn bị":phase==RoomPhase.RESULTS?"Kết quả":"Đang đấu";
        }
        void DrawRoom(Rect area) {
            float sidebar=Mathf.Clamp(area.width*.28f,240,320);
            Rect players=new Rect(area.x,area.y,area.width-sidebar-20,area.height),info=new Rect(area.xMax-sidebar,area.y,sidebar,area.height);
            menu.Card(players,client.RoomName,client.Players.Count+" / "+client.RoomCapacity+" người");
            int columns=players.width>=540?2:1;
            float cellWidth=(players.width-48-(columns-1)*16)/columns;
            GUILayout.BeginArea(new Rect(players.x+24,players.y+110,players.width-48,players.height-134));
            scroll=GUILayout.BeginScrollView(scroll);
            for(int i=0;i<client.RoomCapacity;i++) {
                if(i%columns==0)GUILayout.BeginHorizontal();
                RoomPlayer p=i<client.Players.Count?client.Players[i]:null;
                GUILayout.BeginHorizontal(GUI.skin.box,GUILayout.Width(cellWidth),GUILayout.Height(76));
                if(p==null)GUILayout.Label("Trống",menu.Hint);
                else {
                    GUILayout.Label(new GUIContent(TankPalette.Body(p.Slot)),GUILayout.Width(42),GUILayout.Height(42));
                    GUILayout.BeginVertical();
                    GUILayout.Label(p.Name+(p.Slot==client.Slot?" • bạn":""),menu.Name);
                    GUILayout.Label((p.Ready?"Sẵn sàng":"Đang chờ")+(p.Slot==client.Host?" • Chủ phòng":""),menu.Hint);
                    GUILayout.EndVertical();
                    if(client.Slot==client.Host&&p.Slot!=client.Host&&client.RoomPhase==RoomPhase.WAITING&&GUILayout.Button("×",menu.Secondary,GUILayout.Width(34)))client.Kick(p.Slot);
                }
                GUILayout.EndHorizontal();
                if(i%columns==columns-1||i==client.RoomCapacity-1)GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            menu.Card(info,"Phòng");
            GUILayout.BeginArea(new Rect(info.x+24,info.y+80,info.width-48,info.height-104));
            GUILayout.Label(client.RoomSeconds/60+" phút",menu.Section);
            GUILayout.Label(client.RoomCapacity+" người",menu.Text);
            GUILayout.Label(client.RoomStartingHp+" máu / xe",menu.Text);
            if(client.RoomLocked)GUILayout.Label("Có mật khẩu",menu.Hint);
            GUILayout.Space(20);
            bool waiting=client.RoomPhase==RoomPhase.WAITING;
            if(client.Phase==ClientPhase.ROOM) {
                var local=client.Players.Find(p=>p.Slot==client.Slot);
                bool ready=local!=null&&local.Ready;
                if(Button(ready?"Hủy sẵn sàng":"Sẵn sàng",waiting))client.Ready(!ready);
                if(client.Slot==client.Host) {
                    if(Button("Bắt đầu",waiting&&client.Players.Count>=2&&client.Players.TrueForAll(p=>p.Ready)))client.StartMatch();
                    if(Button("Cài đặt phòng",waiting,true))OpenRoomForm(true);
                }
                if(client.RoomPhase==RoomPhase.RESULTS)GUILayout.Label("Trận mới sau "+Math.Max(0,Math.Ceiling((client.ResultsUntilUs-ClientCore.NowUs)/1e6))+" giây",menu.Hint);
            }
            else GUILayout.Label(client.StartUs==0?"Đang chuẩn bị…":"Bắt đầu sau "+Math.Max(0,(client.StartUs-ClientCore.NowUs-client.OffsetUs)/1e6).ToString("F1"),menu.Text);
            if(Button("Rời phòng",true,true))client.Leave();
            Feedback();
            GUILayout.EndArea();
        }
        void DrawResults(Rect area) {
            menu.Card(area,"Kết quả");
            GUILayout.BeginArea(new Rect(area.x+24,area.y+80,area.width-48,area.height-104));
            scroll=GUILayout.BeginScrollView(scroll);
            foreach(var s in client.Scores) {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label("#"+s.Rank,menu.Section,GUILayout.Width(60));
                GUILayout.Label(s.Name+(s.Slot==client.Slot?" • bạn":""),menu.Name);
                GUILayout.Label(s.Kills+" hạ gục / "+s.Deaths+" tử trận",menu.Text);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if(Button("Về phòng"))client.BackToRoom();
            if(Button("Về sảnh",true,true))client.Leave();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
        void DrawDialogs() {
            if(profileForm&&client.NameRevision!=savedNameRevision) {
                profileForm=false;
                formError="";
            }
            if(joinForm&&client.Phase==ClientPhase.ROOM) {
                joinForm=false;
                formError="";
            }
            if(!AnyDialog)return;
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(0,0,0,.55f));
            float w=Mathf.Min(520,viewWidth-48);
            Rect card=new Rect((viewWidth-w)/2,Mathf.Max(88,(viewHeight-300)/2),w,Mathf.Min(320,viewHeight-112));
            menu.Card(card,profileForm?"Tên hiển thị":"Vào phòng");
            GUILayout.BeginArea(new Rect(card.x+24,card.y+80,card.width-48,card.height-104));
            if(profileForm) {
                displayName=Field("display-name","Tên trong game",displayName,24);
                if(Button("Lưu")) {
                    formError="";
                    if(string.IsNullOrWhiteSpace(displayName)||Encoding.UTF8.GetByteCount(displayName.Trim())>96)formError="Nhập tên từ 1 đến 24 ký tự.";
                    else client.SetDisplayName(displayName);
                }
            }
            else {
                joinPassword=Field("join-password","Mật khẩu phòng",joinPassword,32,true);
                if(Button("Vào phòng"))client.Join(joinTarget,joinPassword);
            }
            if(Button("Hủy",true,true)) {
                profileForm=joinForm=false;
                formError="";
                client.Message="";
            }
            Feedback();
            GUILayout.EndArea();
        }
    }
}
