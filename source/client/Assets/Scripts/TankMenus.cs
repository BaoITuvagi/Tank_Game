using System;
using System.Text;
using UnityEngine;
namespace Tank  {
    public partial class TankApp  {
        MenuTheme menu=new MenuTheme();
        readonly ArenaTheme arenaTheme=new ArenaTheme();
        Vector2 menuViewport,dialogScroll,lobbyActionScroll,nationScroll;
        GUIStyle afkText;
        int capacityChoice=2,durationChoice=1;
        bool registerTab,roomForm,editingRoom,profileForm,joinForm,useRoomPassword;
        bool fieldSubmitRequested;
        string fieldFocusRequest;
        string startingHpText="100",roomPassword="",joinPassword="",displayName="",formError="";
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
        readonly System.Collections.Generic.Dictionary<GUIStyle,GUIStyle> flexibleButtons=new System.Collections.Generic.Dictionary<GUIStyle,GUIStyle>();
        GUIStyle cardName,cardText,cardSmall,cardHeading,plainCardButton,compactAction,centerCardHeading,centerCardText,centerSection,centerHint;
        GUIStyle depotKicker,depotTitle,depotText,depotName,depotSmall,depotNumber,depotAction;
        void InitMenuCards() {
            if(cardName!=null)return;
            cardName=CartoonUi.Label(18,CartoonUi.Ink);cardText=CartoonUi.Label(16,CartoonUi.Ink);
            cardSmall=CartoonUi.Label(14,CartoonUi.Ink);cardHeading=CartoonUi.Label(22,CartoonUi.Ink,true);
            centerCardHeading=new GUIStyle(cardHeading){alignment=TextAnchor.MiddleCenter};centerCardText=new GUIStyle(cardText){alignment=TextAnchor.MiddleCenter};
            centerCardHeading.normal.textColor=CartoonUi.Text;centerCardText.normal.textColor=CartoonUi.Text;
            centerSection=new GUIStyle(menu.Section){alignment=TextAnchor.MiddleCenter};centerHint=new GUIStyle(menu.Hint){alignment=TextAnchor.UpperCenter};
            plainCardButton=new GUIStyle(menu.Secondary){fixedHeight=0,margin=new RectOffset(0,0,0,0),padding=new RectOffset()};
            compactAction=new GUIStyle(menu.Secondary){font=CartoonUi.BodyFont,fontSize=15,fixedHeight=40,margin=new RectOffset(0,0,0,0),padding=new RectOffset(8,8,3,7)};
            depotKicker=CartoonUi.Label(12,CartoonUi.Gold);depotTitle=CartoonUi.Label(28,CartoonUi.Text,true);
            depotText=CartoonUi.Label(16,CartoonUi.Text);depotName=CartoonUi.Label(19,CartoonUi.Text);
            depotSmall=CartoonUi.Label(14,CartoonUi.Muted);depotNumber=CartoonUi.Label(28,CartoonUi.Gold,true);
            depotAction=new GUIStyle(compactAction){fixedHeight=42,padding=new RectOffset(12,12,4,6)};
        }
        void DepotRule(Rect rect,Color color) {
            ArenaRenderer.Fill(new Rect(rect.x,rect.y,Mathf.Min(36,rect.width),rect.height),color);
            ArenaRenderer.Fill(new Rect(rect.x+42,rect.y,Mathf.Max(0,rect.width-42),1),new Color(color.r,color.g,color.b,.24f));
        }
        void TankBay(Rect rect,int palette,Color accent) {
            CartoonUi.Panel(rect,CartoonUi.Backdrop,4,0);
            float inset=Mathf.Min(10,rect.width*.12f);
            ArenaRenderer.Fill(new Rect(rect.x+inset,rect.y+inset,Mathf.Max(1,rect.width-inset*2),2),accent);
            ArenaRenderer.Fill(new Rect(rect.x+inset,rect.yMax-inset-2,Mathf.Max(1,rect.width-inset*2),2),accent);
            CartoonUi.TankPortrait(new Rect(rect.x+4,rect.y+4,rect.width-8,rect.height-8),palette);
        }
        void EmptyMenuState(float width,string title,string description) {
            InitMenuCards();
            title=WrapMenuText(title,centerSection,width-40);description=WrapMenuText(description,centerHint,width-48);
            float titleHeight=MenuTextHeight(title,centerSection,width-40),descriptionHeight=MenuTextHeight(description,centerHint,width-48);
            Rect card=GUILayoutUtility.GetRect(width,Mathf.Max(220,134+titleHeight+descriptionHeight),GUILayout.Width(width));
            CartoonUi.Panel(card,CartoonUi.PanelDark);
            CartoonUi.TankPortrait(new Rect(card.center.x-45,card.y+12,90,90),0);
            GUI.Label(new Rect(card.x+20,card.y+108,card.width-40,titleHeight),title,centerSection);
            GUI.Label(new Rect(card.x+24,card.y+116+titleHeight,card.width-48,descriptionHeight),description,centerHint);
        }
        static float MenuTextHeight(string value,GUIStyle style,float width) {
            return Mathf.Ceil(Mathf.Max(style.lineHeight,style.CalcHeight(new GUIContent(value),width)))+2;
        }
        static string WrapMenuText(string value,GUIStyle style,float width) {
            return CartoonUi.WrapText(value,style,width);
        }
        void ResetMenusForAccount() {
            if(observedAccountRevision==client.AccountRevision)return;
            observedAccountRevision=client.AccountRevision;
            history=roomForm=editingRoom=profileForm=joinForm=false;
            walletNotice="";
            scroll=Vector2.zero;
            roomPassword=joinPassword=displayName=formError="";
            if(client.Phase==ClientPhase.AUTHENTICATING&&client.Account==0)password=confirmPassword="";
        }
        bool AnyDialog  {
            get {
                return settingsOpen||profileForm||joinForm||leaveMatchForm||roleplayShopOpen||!string.IsNullOrEmpty(walletNotice);
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
        void DrawHeader(bool playing) {
            InitMenuCards();
            if(playing)menu.Logo(new Rect(16,5,48,48));
            else {
                if(client.Phase<=ClientPhase.AUTHENTICATING&&Screen.width<1100)menu.Logo(new Rect((viewWidth-72)/2,2,72,72));
                else menu.Logo(new Rect(24,2,72,72));
            }
            if(!playing&&((client.Phase>=ClientPhase.LOBBY&&viewWidth>=1000)||Screen.width>=1100)) {
                GUI.Label(new Rect(112,15,240,30),"TANK BATTLE",menu.Header);
                GUI.Label(new Rect(114,46,240,18),"SẴN SÀNG XUẤT KÍCH",depotKicker);
            }
            if(client.Phase>=ClientPhase.LOBBY) {
                float profileWidth=viewWidth<1100?116:190,walletWidth=viewWidth<1100?140:180,profileX=viewWidth-138-profileWidth;
                string profileLabel=WrapMenuText(string.IsNullOrEmpty(client.Nickname)?"Tên hiển thị":client.Nickname,menu.HeaderButton,profileWidth);
                float buttonHeight=Mathf.Max(46,ButtonHeight(profileLabel,menu.HeaderButton,profileWidth));
                float buttonY=((playing?68:76)-buttonHeight)/2;
                DrawWalletBadge(new Rect(profileX-10-walletWidth,((playing?68:76)-40)/2,walletWidth,40));
                if(GUI.Button(new Rect(profileX,buttonY,profileWidth,buttonHeight),profileLabel,menu.HeaderButton)) {
                    displayName=client.Nickname;
                    profileForm=true;
                    formError="";
                    savedNameRevision=client.NameRevision;
                    GUI.FocusControl(null);
                }
            }
            DrawSettingsButton(new Rect(viewWidth-128,((playing?68:76)-42)/2,104,42));
        }
        bool Button(string label,bool enabled=true,bool secondary=false,GUIStyle style=null) {
            bool prior=GUI.enabled;
            GUI.enabled=prior&&enabled;
            var source=style??(secondary?menu.Secondary:menu.Primary);
            bool result=GUILayout.Button(label,FlexibleButtonStyle(source),GUILayout.MinHeight(source.fixedHeight));
            GUI.enabled=prior;
            return result;
        }
        bool SubmitButton(string label,bool enabled=true) {
            bool submit=fieldSubmitRequested&&GUI.enabled&&enabled;
            fieldSubmitRequested=false;
            return Button(label,enabled)||submit;
        }
        GUIStyle FlexibleButtonStyle(GUIStyle source) {
            GUIStyle flexible;
            if(!flexibleButtons.TryGetValue(source,out flexible)) {
                flexible=new GUIStyle(source){fixedHeight=0};flexibleButtons.Add(source,flexible);
            }
            return flexible;
        }
        float ButtonHeight(string label,GUIStyle style,float width) {
            return Mathf.Max(style.fixedHeight,Mathf.Ceil(FlexibleButtonStyle(style).CalcHeight(new GUIContent(label),width)));
        }
        static float MenuScrollContentWidth(float viewportWidth) {
            var scrollbar=GUI.skin.verticalScrollbar;
            // Reserve the actual scrollbar gutter plus child margins inside the clipped viewport.
            return Mathf.Max(120,viewportWidth-Mathf.Max(14,scrollbar.fixedWidth)-scrollbar.margin.horizontal-8);
        }
        void HandleFieldEnter(string key,string nextField=null) {
            var e=Event.current;
            if(GUI.enabled&&GUI.GetNameOfFocusedControl()==key&&e.type==EventType.KeyDown&&(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter)&&!e.alt&&!e.control&&!e.command&&!e.shift) {
                if(nextField==null)fieldSubmitRequested=true;else fieldFocusRequest=nextField;
                e.Use();
            }
        }
        void FocusRequestedField(string key){
            if(fieldFocusRequest==key&&Event.current.type==EventType.Repaint){GUI.FocusControl(key);fieldFocusRequest=null;}
        }
        string Field(string key,string label,string value,int limit=64,bool secret=false,string nextField=null) {
            GUILayout.Label(label,menu.Name);
            HandleFieldEnter(key,nextField);
            GUI.SetNextControlName(key);
            string result=secret?GUILayout.PasswordField(value,'•',limit):GUILayout.TextField(value,limit);
            FocusRequestedField(key);
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
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(value,menu.Hint);
            GUILayout.EndVertical();
        }
        Rect Content  {
            get {
                return MenuLayout.Fit(viewWidth,viewHeight).Content;
            }
        }
        void DrawMenus() {
            Rect area=Content;
            if(client.Phase<=ClientPhase.AUTHENTICATING) {
                var layout=MenuLayout.Fit(Screen.width,Screen.height);
                Rect card=layout.AuthCard;
                if(!registerTab) {
                    card.height=Mathf.Min(card.height,550);
                    card.y=layout.Content.center.y-card.height/2;
                }
                if(layout.Wide)menu.Logo(layout.AuthLogo);
                menu.Card(card,registerTab?"Đăng ký":"Đăng nhập");
                GUILayout.BeginArea(new Rect(card.x+24,card.y+80,card.width-48,card.height-104));
                scroll=GUILayout.BeginScrollView(scroll);
                GUILayout.BeginVertical(GUILayout.Width(MenuScrollContentWidth(card.width-48)));
                AuthenticationForm();
                GUILayout.EndVertical();
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
            if(Button("Đăng nhập",true,registerTab)) {registerTab=false;confirmPassword="";}
            if(Button("Đăng ký",true,!registerTab))registerTab=true;
            GUILayout.EndHorizontal();
            GUILayout.Space(14);
            GUILayout.Label(registerTab?"GIA NHẬP ĐỘI HÌNH":"TRỞ LẠI CHIẾN TRƯỜNG",depotKicker);
            GUILayout.Space(6);
            username=Field("account","Tên đăng nhập",username,24,nextField:"account-password");
            GUILayout.Space(12);
            password=Field("account-password","Mật khẩu",password,64,true,registerTab?"account-confirm-password":null);
            if(registerTab) {
                GUILayout.Space(12);
                confirmPassword=Field("account-confirm-password","Nhập lại mật khẩu",confirmPassword,64,true);
            }
            GUILayout.Space(18);
            bool busy=client.Phase==ClientPhase.CONNECTING||client.AuthenticationPending||pendingLogin||pendingRegister;
            if(SubmitButton(busy?"Đang xử lý…":registerTab?"Đăng ký":"Đăng nhập",!busy&&client.AuthenticationRetrySeconds<=0))BeginAuthentication(registerTab);
            if(client.Message=="Đăng ký thành công"&&Button("Đăng nhập ngay")) {
                registerTab=false;
                BeginAuthentication(false);
            }
            if(client.AuthenticationRetrySeconds>0)GUILayout.Label("Thử lại sau "+Math.Ceiling(client.AuthenticationRetrySeconds)+" giây.",menu.Hint);
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
                startingHpText="100";
                roomModeChoice=0;
            }
        }
        void DrawLobby(Rect area) {
            InitMenuCards();
            // A sortie banner and an open room browser replace the administrative side rail.
            Rect banner=new Rect(area.x,area.y,area.width,148);
            CartoonUi.Panel(banner,CartoonUi.PanelDark,8,0);
            float entryWidth=Mathf.Clamp(area.width*.34f,270,390),copyWidth=area.width-entryWidth-152;
            TankBay(new Rect(banner.x+18,banner.y+18,104,112),client.Slot,CartoonUi.Ready);
            GUI.Label(new Rect(banner.x+142,banner.y+20,copyWidth,20),"BỘ CHỈ HUY THIẾT GIÁP",depotKicker);
            string bannerTitle=history?"NHẬT KÝ\nTÁC CHIẾN":"TẬP HỢP.\nXUẤT KÍCH.";
            GUI.Label(new Rect(banner.x+140,banner.y+44,copyWidth,82),bannerTitle,depotTitle);
            Rect entry=new Rect(banner.xMax-entryWidth-18,banner.y+20,entryWidth,112);
            GUI.Label(new Rect(entry.x+4,entry.y,entry.width-8,20),"MẶT TRẬN CHIẾN DỊCH",depotKicker);
            GUI.Label(new Rect(entry.x+4,entry.y+24,entry.width-8,24),"Tiến quân · Thu vàng · Ghi danh",depotSmall);
            GUILayout.BeginArea(new Rect(entry.x,entry.y+54,entry.width,58));
            GUILayout.BeginHorizontal();CampaignEntryButtons();GUILayout.EndHorizontal();
            GUILayout.EndArea();
            Rect browser=new Rect(area.x,area.y+164,area.width,area.height-164);
            CartoonUi.Panel(browser,CartoonUi.PanelDark,8,0);
            GUI.Label(new Rect(browser.x+22,browser.y+18,browser.width-402,32),history?"LỊCH SỬ TRẬN":"PHÒNG ĐANG MỞ",menu.Section);
            float actionX=browser.xMax-370;
            if(GUI.Button(new Rect(actionX,browser.y+14,112,42),"Tạo phòng",depotAction))OpenRoomForm(false);
            if(GUI.Button(new Rect(actionX+120,browser.y+14,94,42),"Làm mới",depotAction)) {
                if(history) {
                    client.CloseHistoryDetail();
                    client.GetHistory();
                }
                else client.Refresh();
            }
            if(GUI.Button(new Rect(actionX+222,browser.y+14,128,42),history?"Danh sách phòng":"Lịch sử",depotAction)) {
                history=!history;
                client.CloseHistoryDetail();
                scroll=Vector2.zero;
                if(history)client.GetHistory();
            }
            DepotRule(new Rect(browser.x+22,browser.y+67,browser.width-44,3),CartoonUi.Gold);
            GUILayout.BeginArea(new Rect(browser.x+20,browser.y+78,browser.width-40,browser.height-92));
            scroll=GUILayout.BeginScrollView(scroll);
            float listWidth=MenuScrollContentWidth(browser.width-40);
            GUILayout.BeginVertical(GUILayout.Width(listWidth));
            if(!string.IsNullOrEmpty(client.WalletError))GUILayout.Label(client.WalletError,menu.Hint);
            if(history) {
                if(client.SelectedHistoryMatch!=0)DrawHistoryDetail(listWidth);
                else {
                    GUILayout.Label(client.History.Count+" trận gần đây",menu.Hint);
                    if(client.History.Count==0)EmptyMenuState(listWidth,"Nhật ký đang chờ bạn","Hoàn thành một trận đấu để lưu thành tích tại đây.");
                    foreach(var h in client.History)DrawHistoryCard(h,listWidth);
                }
            }
            else {
                GUILayout.Label(client.Rooms.Count+" phòng · Chọn một đội hình và vào trận",menu.Hint);
                if(client.Rooms.Count==0)EmptyMenuState(listWidth,"Đấu trường đang yên tĩnh","Tạo phòng đầu tiên, hoặc làm mới để tìm đồng đội.");
                foreach(var room in client.Rooms)DrawLobbyRoomCard(room,listWidth);
            }
            Feedback();
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        void DrawHistoryCard(HistoryEntry entry,float width) {
            float nameWidth=width-286;
            string title=WrapMenuText("Hạng "+entry.Rank+" · Trận #"+entry.Match,cardName,nameWidth);
            string date=WrapMenuText(DateTimeOffset.FromUnixTimeMilliseconds((long)entry.Ended).LocalDateTime.ToString("dd/MM/yyyy HH:mm"),cardSmall,nameWidth);
            string stats=WrapMenuText(entry.Kills+" hạ gục / "+entry.Deaths+" tử trận",cardText,160);
            float titleHeight=MenuTextHeight(title,cardName,nameWidth),dateHeight=MenuTextHeight(date,cardSmall,nameWidth),statsHeight=MenuTextHeight(stats,cardText,160);
            Rect row=GUILayoutUtility.GetRect(width,Mathf.Max(100,28+Mathf.Max(titleHeight+8+dateHeight,statsHeight+8+22)),GUILayout.Width(width));
            if(GUI.Button(row,"",plainCardButton))client.GetHistoryDetail(entry.Match);
            CartoonUi.Medal(new Rect(row.x+14,row.y+16,58,58),entry.Rank);
            GUI.Label(new Rect(row.x+88,row.y+12,nameWidth,titleHeight),title,cardName);
            GUI.Label(new Rect(row.x+88,row.y+20+titleHeight,nameWidth,dateHeight),date,cardSmall);
            GUI.Label(new Rect(row.xMax-178,row.y+12,160,statsHeight),stats,cardText);
            GUI.Label(new Rect(row.xMax-178,row.y+20+statsHeight,160,22),"Xem chi tiết →",cardSmall);
            GUILayout.Space(10);
        }
        void DrawLobbyRoomCard(RoomInfo room,float width) {
            bool returning=room.Phase==RoomPhase.RUNNING&&room.CanRejoin;
            bool available=(room.Phase==RoomPhase.WAITING||returning)&&room.Players<room.Capacity;
            bool full=room.Players>=room.Capacity;
            float detailWidth=Mathf.Max(100,width-284);
            string title=WrapMenuText(room.Name,depotName,detailWidth);
            string mode=WrapMenuText(room.Mode==RoomMode.ROLEPLAY?"NHẬP VAI · Kỹ năng Q riêng":"ĐẤU THƯỜNG · Tự do giao tranh",depotSmall,detailWidth);
            float titleHeight=MenuTextHeight(title,depotName,detailWidth),modeHeight=MenuTextHeight(mode,depotSmall,detailWidth);
            bool separateLock=room.Locked&&detailWidth<390;
            float contentHeight=12+titleHeight+8+modeHeight+10+24+(separateLock?28:0)+16;
            Rect row=GUILayoutUtility.GetRect(width,Mathf.Max(132,contentHeight),GUILayout.Width(width));
            CartoonUi.Panel(row,CartoonUi.PanelColor,6,0);
            Color accent=room.Mode==RoomMode.ROLEPLAY?CartoonUi.Gold:CartoonUi.Ready;
            ArenaRenderer.Fill(new Rect(row.x+1,row.y+12,4,row.height-24),accent);
            TankBay(new Rect(row.x+15,row.y+18,72,Mathf.Min(96,row.height-36)),(int)(room.Id%8),accent);
            GUI.Label(new Rect(row.x+102,row.y+12,detailWidth,titleHeight),title,depotName);
            GUI.Label(new Rect(row.x+102,row.y+20+titleHeight,detailWidth,modeHeight),mode,depotSmall);
            float metaTop=row.y+30+titleHeight+modeHeight;
            CartoonUi.Icon(new Rect(row.x+102,metaTop+3,18,18),UiIcon.Players,CartoonUi.Muted);
            GUI.Label(new Rect(row.x+127,metaTop,60,24),room.Players+" / "+room.Capacity,depotText);
            for(int i=0;i<room.Capacity;i++)CartoonUi.Panel(new Rect(row.x+192+i*12,metaTop+6,9,10),i<room.Players?CartoonUi.Ready:CartoonUi.Disabled,3,0);
            if(room.Locked) {
                float lockX=separateLock?row.x+102:row.x+304,lockY=separateLock?metaTop+28:metaTop;
                CartoonUi.Icon(new Rect(lockX,lockY+3,18,18),UiIcon.Lock,CartoonUi.Muted);
                GUI.Label(new Rect(lockX+24,lockY,130,24),"Có mật khẩu",depotSmall);
            }
            string state=returning?"CÓ THỂ VÀO LẠI":full&&room.Phase==RoomPhase.WAITING?"ĐÃ ĐẦY":RoomPhaseLabel(room.Phase).ToUpperInvariant();
            CartoonUi.Badge(new Rect(row.xMax-170,row.y+14,152,28),state,available?CartoonUi.Ready:room.Phase==RoomPhase.RUNNING?CartoonUi.Gold:CartoonUi.Disabled);
            bool prior=GUI.enabled;GUI.enabled=prior&&available;
            string action=returning?"Vào lại trận":"Vào phòng";
            float actionHeight=ButtonHeight(action,menu.Primary,152);
            if(GUI.Button(new Rect(row.xMax-170,row.yMax-actionHeight-16,152,actionHeight),action,menu.Primary)) {
                client.Message=formError="";
                if(room.Locked&&!returning){joinTarget=room.Id;joinPassword="";joinForm=true;}
                else client.Join(room.Id);
            }
            GUI.enabled=prior;
            GUILayout.Space(12);
        }
        static string EndReasonLabel(int reason) {
            return reason==0?"Hết thời gian":reason==1?"Không đủ người chơi":"Server dừng trận";
        }
        void DrawHistoryDetail(float width) {
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
            if(!string.IsNullOrEmpty(detail.RoomModeName))GUILayout.Label(detail.RoomModeName=="ROLEPLAY"?"Nhập vai":"Thường",menu.Hint);
            GUILayout.Space(12);
            foreach(var score in detail.Players) {
                var layout=MeasureScoreCard(score,detail.OwnSlot,width);
                Rect row=GUILayoutUtility.GetRect(width,layout.Height,GUILayout.Width(width));
                DrawScoreCard(row,score,detail.OwnSlot,layout);
                if(detail.RoomModeName=="ROLEPLAY"&&score.Rank==1)GUILayout.Label("Giành được Hộp Đen",menu.Hint);
                GUILayout.Space(8);
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
            float w=Mathf.Min(920,area.width),h=Mathf.Min(560,area.height);
            Rect card=new Rect(area.center.x-w/2,area.center.y-h/2,w,h);
            menu.Card(card,editingRoom?"Cài đặt phòng":"Tạo phòng");
            GUILayout.BeginArea(new Rect(card.x+24,card.y+88,card.width-48,card.height-188));
            scroll=GUILayout.BeginScrollView(scroll);
            float formWidth=MenuScrollContentWidth(card.width-48),columnWidth=(formWidth-24)/2;
            GUILayout.BeginVertical(GUILayout.Width(formWidth));
            bool split=card.width>=840;
            if(split){GUILayout.BeginHorizontal();GUILayout.BeginVertical(GUILayout.Width(columnWidth));}
            roomName=Field("room-name","Tên phòng",roomName,24,nextField:!editingRoom||useRoomPassword?"room-password":"starting-hp");
            GUILayout.Space(10);
            if(editingRoom) {
                useRoomPassword=menu.Toggle(useRoomPassword,"Dùng mật khẩu");
                if(useRoomPassword)roomPassword=Field("room-password",client.RoomLocked?"Mật khẩu mới (để trống để giữ nguyên)":"Mật khẩu",roomPassword,32,true,"starting-hp");
            }
            else roomPassword=Field("room-password","Mật khẩu phòng (không bắt buộc)",roomPassword,32,true,"starting-hp");
            GUILayout.Space(10);
            if(editingRoom)GUILayout.Label("Chế độ: "+(client.RoomMode==RoomMode.ROLEPLAY?"Nhập vai":"Thường")+" · cố định khi tạo phòng",menu.Hint);
            else {
                GUILayout.Label("Chế độ",menu.Name);
                roomModeChoice=GUILayout.SelectionGrid(roomModeChoice,new[]{"Thường","Nhập vai"},2,menu.Secondary);
                if(roomModeChoice==1)GUILayout.Label("Chọn phe với kỹ năng Q riêng trước trận.",menu.Hint);
            }
            if(split){GUILayout.EndVertical();GUILayout.Space(16);GUILayout.BeginVertical(GUILayout.Width(columnWidth));}
            GUILayout.Space(14);
            GUILayout.Label("Số người tối đa",menu.Name);
            capacityChoice=GUILayout.SelectionGrid(capacityChoice,new[] {
                "2","3","4","5","6","7","8"
            },7,menu.Choice);
            GUILayout.Label("Đã chọn: "+capacities[capacityChoice]+" người",menu.ReadyLabel);
            GUILayout.Space(14);
            GUILayout.Label("Thời gian trận",menu.Name);
            durationChoice=GUILayout.SelectionGrid(durationChoice,new[] {
                "1 phút","3 phút","5 phút","10 phút"
            },4,menu.Choice);
            GUILayout.Label("Đã chọn: "+durations[durationChoice]/60+" phút",menu.ReadyLabel);
            GUILayout.Space(14);
            startingHpText=Field("starting-hp","Máu ban đầu / mỗi lần hồi sinh (1–500)",startingHpText,3);
            if(split){GUILayout.EndVertical();GUILayout.EndHorizontal();}
            Feedback();
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(card.x+24,card.yMax-84,card.width-48,64));
            GUILayout.BeginHorizontal();
            if(SubmitButton("Xác nhận")) {
                formError="";
                int hp;
                int mode=!useRoomPassword?1:string.IsNullOrEmpty(roomPassword)&&client.RoomLocked?0:2;
                if(string.IsNullOrWhiteSpace(roomName)||Encoding.UTF8.GetByteCount(roomName.Trim())>48)formError="Tên phòng quá dài hoặc còn trống.";
                else if(!int.TryParse(startingHpText,out hp)||hp<1||hp>GameRules.MaxStartHp)formError="Máu ban đầu phải là số từ 1 đến 500.";
                else if(editingRoom&&capacities[capacityChoice]<client.Players.Count)formError="Số người tối đa không thể nhỏ hơn số người đang ở phòng.";
                else if(editingRoom&&mode==2&&roomPassword.Length==0)formError="Nhập mật khẩu hoặc bỏ chọn dùng mật khẩu.";
                else if(editingRoom)client.ConfigureRoom(roomName.Trim(),capacities[capacityChoice],durations[durationChoice],mode,roomPassword,hp);
                else client.Create(roomName.Trim(),capacities[capacityChoice],durations[durationChoice],roomPassword,hp,(RoomMode)roomModeChoice);
            }
            if(Button("Hủy",true,true)) {
                roomForm=false;
                formError="";
                client.Message="";
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
        static string RoomPhaseLabel(RoomPhase phase) {
            return phase==RoomPhase.WAITING?"Đang chờ":phase==RoomPhase.PREPARING?"Chuẩn bị":phase==RoomPhase.RESULTS?"Kết quả":"Đang đấu";
        }
        void DrawRoom(Rect area) {
            InitMenuCards();
            // Roster content scrolls independently when full names need taller rows.
            area.height=Mathf.Min(area.height,820);
            bool roleplay=client.RoomMode==RoomMode.ROLEPLAY;
            float sidebar=Mathf.Clamp(area.width*.28f,client.RoomMode==RoomMode.ROLEPLAY?320:260,320);
            Rect players=new Rect(area.x,area.y,area.width-sidebar-20,area.height),info=new Rect(area.xMax-sidebar,area.y,sidebar,area.height);
            Rect nations=default(Rect);
            if(roleplay&&players.width>=780) {
                float rosterWidth=players.width*.46f;
                nations=new Rect(players.x+rosterWidth+16,players.y,players.width-rosterWidth-16,players.height);
                players.width=rosterWidth;
            }
            int readyCount=client.Players.FindAll(p=>p.Ready).Count;
            menu.Card(players,"BÃI TẬP KẾT",client.Players.Count+" / "+client.RoomCapacity+" người  ·  "+readyCount+" sẵn sàng");
            int columns=players.width>=390?2:1;
            float cellWidth=(players.width-64-(columns-1)*10)/columns;
            GUILayout.BeginArea(new Rect(players.x+24,players.y+110,players.width-48,players.height-134));
            scroll=GUILayout.BeginScrollView(scroll);
            if(roleplay&&nations.width==0){NationSelector(players.width-76,2,true);GUILayout.Space(18);}
            for(int row=0;row<client.RoomCapacity;row+=columns) {
                var cards=new RoomPlayerCardLayout[columns];float rowHeight=0;
                for(int column=0;column<columns&&row+column<client.RoomCapacity;column++) {
                    int index=row+column;
                    cards[column]=MeasureRoomPlayerCard(index<client.Players.Count?client.Players[index]:null,cellWidth);
                    rowHeight=Mathf.Max(rowHeight,cards[column].Height);
                }
                GUILayout.BeginHorizontal();
                for(int column=0;column<columns&&row+column<client.RoomCapacity;column++) {
                    int index=row+column;RoomPlayer p=index<client.Players.Count?client.Players[index]:null;
                    Rect cell=GUILayoutUtility.GetRect(cellWidth,rowHeight,GUILayout.Width(cellWidth),GUILayout.Height(rowHeight));
                    DrawRoomPlayerCard(cell,p,index,cards[column]);
                    if(column<columns-1&&index+1<client.RoomCapacity)GUILayout.Space(10);
                }
                GUILayout.EndHorizontal();
                if(row+columns<client.RoomCapacity)GUILayout.Space(10);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if(nations.width>0) {
                menu.Card(nations,"CHỌN PHE","Một phe cho mỗi người · kỹ năng riêng");
                GUILayout.BeginArea(new Rect(nations.x+24,nations.y+110,nations.width-48,nations.height-134));
                nationScroll=GUILayout.BeginScrollView(nationScroll);
                NationSelector(nations.width-72,2,true);
                GUILayout.EndScrollView();GUILayout.EndArea();
            }
            menu.Card(info,"XUẤT KÍCH");
            var localPlayer=client.Players.Find(p=>p.Slot==client.Slot);
            bool localReady=localPlayer!=null&&localPlayer.Ready;
            float actionWidth=info.width-56;
            float actionHeight=ButtonHeight("Rời phòng",menu.Danger,actionWidth)+12;
            if(client.Phase==ClientPhase.ROOM) {
                actionHeight+=ButtonHeight(localReady?"Hủy sẵn sàng":"Sẵn sàng",menu.ReadyButton,actionWidth)+12;
                if(client.Slot==client.Host)actionHeight+=ButtonHeight("Bắt đầu",menu.Primary,actionWidth)+ButtonHeight("Cài đặt phòng",menu.Secondary,actionWidth)+24;
            }
            else actionHeight+=56;
            GUILayout.BeginArea(new Rect(info.x+24,info.y+80,info.width-48,Mathf.Max(60,info.height-112-actionHeight)));
            roleplayInfoScroll=GUILayout.BeginScrollView(roleplayInfoScroll);
            GUILayout.BeginVertical(GUILayout.Width(info.width-70));
            GUILayout.Label(WrapMenuText(client.RoomName,menu.Name,info.width-70),menu.Name);
            GUILayout.Label(client.RoomMode==RoomMode.ROLEPLAY?"NHẬP VAI":"ĐẤU THƯỜNG",menu.ReadyLabel);
            GUILayout.Space(8);
            RoomStat(UiIcon.Clock,client.RoomSeconds/60+" phút / trận");
            RoomStat(UiIcon.Players,client.Players.Count+" / "+client.RoomCapacity+" người");
            RoomStat(UiIcon.Health,client.RoomStartingHp+" máu / xe");
            if(client.RoomLocked)RoomStat(UiIcon.Lock,"Có mật khẩu");
            if(client.RoomMode==RoomMode.ROLEPLAY) {
                GUILayout.Space(12);
                var selected=client.Players.Find(p=>p.Slot==client.Slot);
                if(selected!=null&&selected.Nation>=0) {
                    GUILayout.Label(ClientPreferences.Active.Label(ControlAction.Skill)+" · "+RoleplayContent.QNames[selected.Nation],menu.Section);
                    GUILayout.Label(ClientPreferences.Active.NationDescription(selected.Nation),menu.Text);
                    GUILayout.Label("Hồi chiêu "+(RoleplayContent.NationCooldowns[selected.Nation]/30f).ToString("0.#")+" giây",menu.Hint);
                }
                else GUILayout.Label("Chọn thẻ phe để xem kỹ năng Q và sẵn sàng.",menu.Hint);
                if(client.RoomPhase==RoomPhase.WAITING&&selected!=null&&selected.Ready)GUILayout.Label("Hủy sẵn sàng trước khi đổi phe.",menu.Hint);
                GUILayout.Space(12);
                GUILayout.Label("NHIỆM VỤ · HỘP ĐEN",menu.Hint);
                GUILayout.Label(RoleplayContent.Story,menu.Hint);
            }
            else GUILayout.Label("Tất cả người chơi cần sẵn sàng. Chủ phòng bắt đầu khi có ít nhất 2 người.",menu.Hint);
            Feedback();
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(info.x+24,info.yMax-actionHeight-24,info.width-48,actionHeight));
            bool waiting=client.RoomPhase==RoomPhase.WAITING;
            if(client.Phase==ClientPhase.ROOM) {
                var local=localPlayer;
                bool ready=local!=null&&local.Ready;
                if(Button(ready?"Hủy sẵn sàng":"Sẵn sàng",waiting&&(ready||client.RoomMode!=RoomMode.ROLEPLAY||local!=null&&local.Nation>=0),false,menu.ReadyButton))client.Ready(!ready);
                if(client.Slot==client.Host) {
                    if(Button("Bắt đầu",waiting&&client.Players.Count>=2&&client.Players.TrueForAll(p=>p.Ready)))client.StartMatch();
                    if(Button("Cài đặt phòng",waiting,true))OpenRoomForm(true);
                }
            }
            else GUILayout.Label(client.RoomMode==RoomMode.ROLEPLAY?PreparationLabel():client.StartUs==0?"Đang chuẩn bị…":"Bắt đầu sau "+Math.Max(0,(client.StartUs-ClientCore.NowUs-client.OffsetUs)/1e6).ToString("F1"),menu.Text);
            if(Button("Rời phòng",true,true,menu.Danger))client.Leave();
            GUILayout.EndArea();
        }
        void RoomStat(UiIcon icon,string label) {
            GUILayout.BeginHorizontal();
            Rect symbol=GUILayoutUtility.GetRect(22,25,GUILayout.Width(22));CartoonUi.Icon(symbol,icon,CartoonUi.Muted);
            GUILayout.Space(8);GUILayout.Label(label,menu.Text);GUILayout.EndHorizontal();
        }
        struct RoomPlayerCardLayout {
            public string Name,Details;
            public float NameHeight,DetailHeight,BodyHeight,Height;
        }
        RoomPlayerCardLayout MeasureRoomPlayerCard(RoomPlayer player,float width) {
            if(player==null)return new RoomPlayerCardLayout{Height=154};
            bool own=player.Slot==client.Slot,host=player.Slot==client.Host;
            bool kick=client.Slot==client.Host&&!host&&client.RoomPhase==RoomPhase.WAITING;
            float nameWidth=width-(kick?56:24),detailWidth=width-88;
            string details=client.RoomMode==RoomMode.ROLEPLAY?(player.Nation<0?"Chưa chọn phe":RoleplayContent.NationNames[player.Nation]):TankPalette.Names[Mathf.Clamp(player.Slot,0,7)];
            if(host)details+=" · Chủ phòng";
            var layout=new RoomPlayerCardLayout {
                Name=WrapMenuText((own?"Bạn · ":"")+player.Name,depotName,nameWidth),
                Details=WrapMenuText(details,depotSmall,detailWidth)
            };
            layout.NameHeight=MenuTextHeight(layout.Name,depotName,nameWidth);
            layout.DetailHeight=MenuTextHeight(layout.Details,depotSmall,detailWidth);
            layout.BodyHeight=Mathf.Max(78,26+8+layout.DetailHeight);
            layout.Height=12+layout.NameHeight+8+layout.BodyHeight+12;
            return layout;
        }
        void DrawRoomPlayerCard(Rect r,RoomPlayer player,int position,RoomPlayerCardLayout layout) {
            if(player==null) {
                CartoonUi.Panel(r,CartoonUi.Backdrop,5,0);
                GUI.Label(new Rect(r.x+14,r.y+12,r.width-28,22),"BÃI XE / 0"+(position+1),depotKicker);
                CartoonUi.Icon(new Rect(r.x+18,r.y+61,30,30),UiIcon.Players,CartoonUi.Muted);
                GUI.Label(new Rect(r.x+64,r.y+56,r.width-78,50),"Đang chờ\nđồng đội…",depotSmall);
                DepotRule(new Rect(r.x+14,r.yMax-20,r.width-28,2),CartoonUi.PanelColor);
                return;
            }
            bool own=player.Slot==client.Slot,host=player.Slot==client.Host;
            bool kick=client.Slot==client.Host&&!host&&client.RoomPhase==RoomPhase.WAITING;
            CartoonUi.Panel(r,CartoonUi.PanelColor,5,0);
            ArenaRenderer.Fill(new Rect(r.x+1,r.y+10,4,r.height-20),own?CartoonUi.Gold:player.Ready?CartoonUi.Ready:CartoonUi.Outline);
            GUI.Label(new Rect(r.x+12,r.y+12,r.width-(kick?56:24),layout.NameHeight),layout.Name,depotName);
            float bodyTop=r.y+12+layout.NameHeight+8;
            TankBay(new Rect(r.x+11,bodyTop,60,78),player.Nation>=0?player.Nation:player.Slot,own?CartoonUi.Gold:CartoonUi.Ready);
            float textX=r.x+78,textWidth=r.width-88;
            CartoonUi.Badge(new Rect(textX,bodyTop,Mathf.Min(130,textWidth),26),player.Ready?"Sẵn sàng":"Đang chờ",player.Ready?CartoonUi.Ready:CartoonUi.Disabled);
            GUI.Label(new Rect(textX,bodyTop+34,textWidth,layout.DetailHeight),layout.Details,depotSmall);
            if(kick&&GUI.Button(new Rect(r.xMax-40,r.y+12,28,28),"×",compactAction))client.Kick(player.Slot);
        }
        void DrawResults(Rect area) {
            InitMenuCards();
            var own=client.Scores.Find(s=>s.Slot==client.Slot);
            CartoonUi.Panel(area,CartoonUi.PanelDark,8,0);
            GUI.Label(new Rect(area.x+26,area.y+15,area.width-52,18),"BÁO CÁO TÁC CHIẾN / KẾT QUẢ TRẬN ĐẤU",depotKicker);
            GUI.Label(new Rect(area.x+24,area.y+39,area.width-48,42),own!=null&&own.Rank==1?"CHIẾN THẮNG!":"TRẬN ĐẤU KẾT THÚC",depotTitle);
            DepotRule(new Rect(area.x+26,area.y+88,area.width-52,3),CartoonUi.Gold);
            float width=area.width-72,podiumWidth=Mathf.Min(1040,width),cellWidth=(podiumWidth-24)/3,podiumHeight=0;
            int[] order={2,1,3};
            var podiumCards=new PodiumCardLayout[3];
            for(int column=0;column<3;column++) {
                int rank=order[column];var score=client.Scores.Find(s=>s.Rank==rank);if(score==null)continue;
                podiumCards[column]=MeasurePodiumCard(score,cellWidth);
                podiumHeight=Mathf.Max(podiumHeight,podiumCards[column].Height);
            }
            GUILayout.BeginArea(new Rect(area.x+24,area.y+104,area.width-48,area.height-204));
            scroll=GUILayout.BeginScrollView(scroll);
            if(podiumHeight>0) {
                Rect podium=GUILayoutUtility.GetRect(width,podiumHeight,GUILayout.Width(width));
                float left=podium.center.x-podiumWidth/2;
                for(int column=0;column<3;column++) {
                    var score=client.Scores.Find(s=>s.Rank==order[column]);if(score==null)continue;
                    Rect card=new Rect(left+column*(cellWidth+12),podium.yMax-podiumCards[column].Height,cellWidth,podiumCards[column].Height);
                    DrawPodiumCard(card,score,podiumCards[column]);
                }
                GUILayout.Space(12);
            }
            string summary=own==null?"Thành tích toàn đội":"THÀNH TÍCH CỦA BẠN · Hạng "+own.Rank+" · "+own.Kills+" hạ gục / "+own.Deaths+" tử trận";
            if(client.RoomMode==RoomMode.ROLEPLAY) {
                var winner=client.Scores.Find(s=>s.Rank==1);
                if(winner!=null)summary+="  ·  "+winner.Name+" giành Hộp Đen";
            }
            GUILayout.Label(WrapMenuText(summary,menu.ReadyLabel,width),menu.ReadyLabel,GUILayout.Width(width));
            GUILayout.Space(12);
            if(client.Scores.Count==0)GUILayout.Label("Đang chờ kết quả từ server…",menu.Hint);
            foreach(var score in client.Scores) {
                var layout=MeasureScoreCard(score,client.Slot,width);
                Rect row=GUILayoutUtility.GetRect(width,layout.Height,GUILayout.Width(width));
                DrawScoreCard(row,score,client.Slot,layout);GUILayout.Space(8);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(area.x+24,area.yMax-84,area.width-48,64));
            GUILayout.BeginHorizontal();
            if(Button("Về phòng"))client.BackToRoom();
            if(Button("Về sảnh",true,true))client.Leave();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
        struct PodiumCardLayout {
            public string Name,Stats;
            public float NameHeight,StatsHeight,Height;
        }
        PodiumCardLayout MeasurePodiumCard(Score score,float width) {
            var layout=new PodiumCardLayout {
                Name=WrapMenuText(score.Name+(score.Slot==client.Slot?" · bạn":""),centerCardHeading,width-32),
                Stats=WrapMenuText(score.Kills+" hạ gục / "+score.Deaths+" tử trận",centerCardText,width-32)
            };
            layout.NameHeight=MenuTextHeight(layout.Name,centerCardHeading,width-32);
            layout.StatsHeight=MenuTextHeight(layout.Stats,centerCardText,width-32);
            layout.Height=12+100+8+layout.NameHeight+12+layout.StatsHeight+16+(score.Rank==1?32:0);
            return layout;
        }
        void DrawPodiumCard(Rect card,Score score,PodiumCardLayout layout) {
            Color fill=score.Rank==1?CartoonUi.PanelColor:CartoonUi.Backdrop;
            CartoonUi.Panel(card,fill,6,0);
            ArenaRenderer.Fill(new Rect(card.x+12,card.y+1,card.width-24,4),score.Rank==1?CartoonUi.Gold:CartoonUi.Outline);
            CartoonUi.Medal(new Rect(card.x+12,card.y+12,46,54),score.Rank);
            TankBay(new Rect(card.center.x-45,card.y+16,90,96),score.Nation>=0?score.Nation:score.Slot,score.Rank==1?CartoonUi.Gold:CartoonUi.Ready);
            GUI.Label(new Rect(card.x+16,card.y+120,card.width-32,layout.NameHeight),layout.Name,centerCardHeading);
            GUI.Label(new Rect(card.x+16,card.yMax-16-layout.StatsHeight,card.width-32,layout.StatsHeight),layout.Stats,centerCardText);
        }
        struct ScoreCardLayout {
            public string Name,Details,Stats;
            public float NameHeight,DetailHeight,StatsHeight,Height;
            public bool Narrow;
        }
        ScoreCardLayout MeasureScoreCard(Score score,int ownSlot,float width) {
            bool narrow=width<620;
            float nameWidth=narrow?width-148:width-380;
            string detail=score.Nation>=0?RoleplayContent.NationNames[score.Nation]:"Xe "+TankPalette.Names[Mathf.Clamp(score.Slot,0,7)];
            if(score.Disconnected)detail+=" · Rời trận sớm";
            string stats=score.Kills+" hạ gục / "+score.Deaths+" tử trận";
            var layout=new ScoreCardLayout {
                Name=WrapMenuText(score.Name+(score.Slot==ownSlot?" · bạn":""),cardName,nameWidth),
                Details=WrapMenuText(narrow?stats+" · "+detail:detail,cardSmall,nameWidth),
                Stats=WrapMenuText(stats,cardText,210),Narrow=narrow
            };
            layout.NameHeight=MenuTextHeight(layout.Name,cardName,nameWidth);
            layout.DetailHeight=MenuTextHeight(layout.Details,cardSmall,nameWidth);
            layout.StatsHeight=MenuTextHeight(layout.Stats,cardText,210);
            layout.Height=Mathf.Max(82,24+Mathf.Max(layout.NameHeight+6+layout.DetailHeight,narrow?0:layout.StatsHeight));
            return layout;
        }
        void DrawScoreCard(Rect r,Score score,int ownSlot,ScoreCardLayout layout) {
            InitMenuCards();
            bool own=score.Slot==ownSlot;
            CartoonUi.Panel(r,own?CartoonUi.Ready:CartoonUi.Cream,12,3);
            CartoonUi.Medal(new Rect(r.x+12,r.y+12,42,48),score.Rank);
            CartoonUi.TankPortrait(new Rect(r.x+64,r.y+8,54,54),score.Nation>=0?score.Nation:score.Slot);
            float nameWidth=layout.Narrow?r.width-148:r.width-380;
            GUI.Label(new Rect(r.x+132,r.y+12,nameWidth,layout.NameHeight),layout.Name,cardName);
            GUI.Label(new Rect(r.x+132,r.y+18+layout.NameHeight,nameWidth,layout.DetailHeight),layout.Details,cardSmall);
            if(!layout.Narrow)GUI.Label(new Rect(r.xMax-226,r.center.y-layout.StatsHeight/2,210,layout.StatsHeight),layout.Stats,cardText);
        }
        void DrawDialogs() {
            if(settingsOpen)return;
            if(roleplayShopOpen) {
                bool enabled=GUI.enabled;GUI.enabled=enabled&&string.IsNullOrEmpty(walletNotice);DrawRoleplayShop();GUI.enabled=enabled;
                if(string.IsNullOrEmpty(walletNotice))return;
            }
            if(!string.IsNullOrEmpty(walletNotice)){DrawWalletNotice();return;}
            if(leaveMatchForm){DrawLeaveMatchDialog();return;}
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
            float w=Mathf.Min(560,viewWidth-48),h=Mathf.Min(380,viewHeight-112);
            Rect card=new Rect((viewWidth-w)/2,Mathf.Max(88,(viewHeight-h)/2),w,h);
            menu.Card(card,profileForm?"Tên hiển thị":"Vào phòng");
            GUILayout.BeginArea(new Rect(card.x+24,card.y+80,card.width-48,card.height-180));
            dialogScroll=GUILayout.BeginScrollView(dialogScroll);
            GUILayout.BeginVertical(GUILayout.Width(MenuScrollContentWidth(card.width-48)));
            if(profileForm) {
                displayName=Field("display-name","Tên trong game",displayName,24);
                GUILayout.Label("Tên này xuất hiện trong phòng và bảng thành tích.",menu.Hint);
            }
            else {
                GUILayout.Label("Phòng có mật khẩu · Hỏi chủ phòng để nhận mã vào.",menu.Hint);
                joinPassword=Field("join-password","Mật khẩu phòng",joinPassword,32,true);
            }
            Feedback();
            GUILayout.EndVertical();
            GUILayout.EndScrollView();GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(card.x+24,card.yMax-84,card.width-48,64));
            GUILayout.BeginHorizontal();
            if(SubmitButton(profileForm?"Lưu tên":"Vào phòng")) {
                if(profileForm) {
                    formError="";
                    if(string.IsNullOrWhiteSpace(displayName)||Encoding.UTF8.GetByteCount(displayName.Trim())>96)formError="Nhập tên từ 1 đến 24 ký tự.";
                    else client.SetDisplayName(displayName);
                }
                else client.Join(joinTarget,joinPassword);
            }
            if(Button("Hủy",true,true)) {
                profileForm=joinForm=false;
                formError="";
                client.Message="";
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
