using UnityEngine;
using Tank.Campaign;

namespace Tank {
    // Local screen selection must survive room/arena/results transitions, but not account changes.
    public sealed class ModeNavigation {
        int accountRevision=-1;
        public bool MultiplayerSelected {get;private set;}
        public void Observe(int revision,ClientPhase phase) {
            if(accountRevision!=revision){accountRevision=revision;MultiplayerSelected=false;}
            if(phase>=ClientPhase.ROOM)MultiplayerSelected=true;
        }
        public void ShowSelection(){MultiplayerSelected=false;}
        public void ShowMultiplayer(){MultiplayerSelected=true;}
        public static bool CanLeaveCampaign(bool pending,RunState run) {
            return !pending&&(run==null||!run.Finished||run.SaveStatus=="Đã lưu kết quả.");
        }
        public static bool CanEnterMultiplayer(bool pending,RunState run) {
            return !pending&&(run==null||run.Finished&&run.SaveStatus=="Đã lưu kết quả.");
        }
    }

    public partial class TankApp {
        readonly ModeNavigation modeNavigation=new ModeNavigation();
        GUIStyle modeTitle,modeHeading,modeDescription,modeStatus;
        internal int CampaignPage {get{return campaignPage;}}
        internal string SelectedMode {get{return CampaignOpen?"campaign":modeNavigation.MultiplayerSelected?"multiplayer":"selection";}}
        bool CanEnterMultiplayer {get{return !client.Campaign.Pending&&(client.Campaign.Paused||ModeNavigation.CanEnterMultiplayer(false,client.Campaign.Run));}}
        string MultiplayerCampaignNotice {
            get {
                if(client.Campaign.Pending)return "Đợi yêu cầu chiến dịch hoàn tất trước khi tạo hoặc tham gia phòng.";
                var run=client.Campaign.Run;
                if(client.Campaign.Paused||run==null||run.Finished&&run.SaveStatus=="Đã lưu kết quả.")return "";
                return run.Finished?"Kết quả chiến dịch chưa lưu. Chọn chế độ → Xem kết quả để lưu lại.":"Lưu và thoát chiến dịch để tham gia Multiplayer. Chọn chế độ → Tiếp tục chuyến đi để trở lại chiến dịch.";
            }
        }
        void ShowModeSelection() {
            if(client.Phase!=ClientPhase.LOBBY||!ModeNavigation.CanLeaveCampaign(client.Campaign.Pending,client.Campaign.Run))return;
            if(client.Campaign.Run!=null&&!client.Campaign.Run.Finished&&!client.Campaign.Paused)client.CampaignCommand(6);
            campaignPage=0;history=roomForm=false;
            modeNavigation.ShowSelection();
            scroll=menuViewport=Vector2.zero;GUI.FocusControl(null);
        }
        void ShowMultiplayerLobby() {
            if(client.Phase!=ClientPhase.LOBBY||!ModeNavigation.CanLeaveCampaign(client.Campaign.Pending,client.Campaign.Run))return;
            if(client.Campaign.Run!=null&&!client.Campaign.Run.Finished&&!client.Campaign.Paused)client.CampaignCommand(6);
            campaignPage=0;history=false;
            modeNavigation.ShowMultiplayer();
            scroll=Vector2.zero;GUI.FocusControl(null);
            if(!IsMenuPreview)client.Refresh();
        }
        void DrawModeSelection(Rect area) {
            InitMenuCards();
            if(modeTitle==null) {
                modeTitle=CartoonUi.Label(28,CartoonUi.Text,true);
                modeHeading=CartoonUi.Label(23,CartoonUi.Text,true);
                modeDescription=CartoonUi.Label(16,CartoonUi.Text);
                modeStatus=CartoonUi.Label(14,CartoonUi.Muted);
                modeTitle.alignment=modeHeading.alignment=modeStatus.alignment=TextAnchor.MiddleCenter;
                modeDescription.alignment=TextAnchor.UpperCenter;
            }
            float groupHeight=Mathf.Min(634,area.height);
            area=new Rect(area.x,area.y+(area.height-groupHeight)/2,area.width,groupHeight);
            CartoonUi.Panel(new Rect(area.x,area.y,area.width,76),CartoonUi.PanelDark,8);
            GUI.Label(new Rect(area.x+16,area.y+6,area.width-32,36),"CHỌN MẶT TRẬN",modeTitle);
            GUI.Label(new Rect(area.x+16,area.y+44,area.width-32,26),"Một đội hình thiết giáp · Hai cách chinh phục chiến trường",centerHint);
            float top=area.y+84,gap=22,width=(area.width-gap)/2;
            float height=Mathf.Min(550,area.height-84);
            DrawModeCard(new Rect(area.x,top,width,height),true);
            DrawModeCard(new Rect(area.x+width+gap,top,width,height),false);
        }
        void DrawModeCard(Rect card,bool campaign) {
            CartoonUi.Panel(card,CartoonUi.PanelDark,12);
            float padding=24,textWidth=card.width-padding*2;
            Color accent=campaign?CartoonUi.Gold:CartoonUi.Ready;
            ArenaRenderer.Fill(new Rect(card.x+padding,card.y+20,textWidth,3),accent);
            var run=client.Campaign.Run;
            bool pending=client.Campaign.Pending,needsSave=run!=null&&run.Finished&&run.SaveStatus!="Đã lưu kết quả.";
            string heading=campaign?"CHIẾN DỊCH":"NHIỀU NGƯỜI CHƠI";
            string description=campaign?"Một người chơi · Tiến quân theo lượt.\nChọn chiến xa, hoàn thành chuyến giải cứu và mang vàng về tài khoản.":"2–8 người · Giao tranh thời gian thực.\nTạo hoặc tham gia phòng, chọn Đấu thường hay Nhập vai.";
            string label=campaign?(pending?"Đang xử lý…":run==null?"Chọn chiến xa":run.Finished?"Xem kết quả":"Tiếp tục chuyến đi"):"Vào sảnh phòng";
            string state=campaign?(pending?"Đợi phản hồi từ server.":run==null?"Quốc gia · Kỹ năng · Tiếp tế":run.Finished?"Thành tích và trạng thái lưu chuyến đi.":"Tiến trình chuyến đi được lưu theo tài khoản."):(needsSave?"Lưu kết quả chiến dịch trước khi chuyển chế độ.":pending?"Đợi yêu cầu chiến dịch hoàn tất.":run!=null&&!run.Finished?"Lưu và thoát chiến dịch để tham gia Multiplayer.":"Thường / Nhập vai được chọn khi tạo phòng.");
            float headingHeight=MenuTextHeight(heading,modeHeading,textWidth),descriptionHeight=MenuTextHeight(description,modeDescription,textWidth),stateHeight=MenuTextHeight(state,modeStatus,textWidth);
            float buttonHeight=Mathf.Max(48,ButtonHeight(label,menu.Primary,textWidth));
            // Text keeps its readable size; illustration gives up space in shorter windows.
            float artHeight=Mathf.Clamp(card.height-110-headingHeight-descriptionHeight-stateHeight-buttonHeight,48,136);
            Rect art=new Rect(card.x+padding,card.y+36,textWidth,artHeight);
            if(campaign) {
                TankBay(new Rect(art.center.x-artHeight/2,art.y,artHeight,artHeight),campaignNations[Mathf.Clamp(campaignChoice,0,campaignNations.Length-1)],accent);
            }
            else {
                float size=Mathf.Min(artHeight,art.width*.38f);
                CartoonUi.TankPortrait(new Rect(art.center.x-size+8,art.y,size,size),0);
                CartoonUi.TankPortrait(new Rect(art.center.x-8,art.y,size,size),1);
            }
            float headingY=art.yMax+14;
            GUI.Label(new Rect(card.x+padding,headingY,textWidth,headingHeight),heading,modeHeading);
            float descriptionY=headingY+headingHeight+10;
            GUI.Label(new Rect(card.x+padding,descriptionY,textWidth,descriptionHeight),description,modeDescription);
            float buttonY=card.yMax-24-buttonHeight;
            GUI.Label(new Rect(card.x+padding,buttonY-stateHeight-12,textWidth,stateHeight),state,modeStatus);
            bool previous=GUI.enabled;GUI.enabled=previous&&!pending&&(campaign||!needsSave);
            if(GUI.Button(new Rect(card.x+padding,buttonY,textWidth,buttonHeight),label,menu.Primary)) {
                if(campaign){campaignPage=run==null?1:3;campaignScroll=Vector2.zero;GUI.FocusControl(null);}
                else ShowMultiplayerLobby();
            }
            GUI.enabled=previous;
        }
    }
}
