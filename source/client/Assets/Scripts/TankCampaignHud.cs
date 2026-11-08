using UnityEngine;
using Tank.Campaign;

namespace Tank {
    public partial class TankApp {
        bool campaignEndConfirm;
        Vector2 campaignHudScroll;
        string campaignLastFeedback;
        GUIStyle campaignHudLightCompact,campaignHudHintCompact,campaignBuffCount,campaignRegionTitle;
        void EnsureCampaignHudStyles(){
            if(campaignHudLightCompact!=null)return;
            campaignHudLightCompact=CartoonUi.Label(15,CartoonUi.Text);
            campaignHudHintCompact=CartoonUi.Label(12,CartoonUi.Muted);
            campaignBuffCount=CartoonUi.Label(12,CartoonUi.Text);campaignBuffCount.alignment=TextAnchor.MiddleCenter;campaignBuffCount.wordWrap=false;
            campaignRegionTitle=CartoonUi.Label(18,CartoonUi.Text);campaignRegionTitle.alignment=TextAnchor.MiddleCenter;
        }
        string CampaignActionWaiting(RunState run,double now){
            return run.Finished?"Đã kết thúc":client.Campaign.Pending?"Đang xử lý…":campaignShopOpen?"Đang tiếp tế":campaignEndConfirm?"Đang xác nhận":AnyDialog?"Đang mở hộp thoại":campaignPresentation.Busy(now)?"Đang hành động…":campaignInputRelease||SkillDetailsUi.InputBlocked?"Nhả phím để tiếp tục":"";
        }
        CampaignHudLayout CampaignLayout(RunState run,double now){
            var nation=CampaignNation.For(run.Nation);string waiting=CampaignActionWaiting(run,now);
            float width=ArenaLayout.SidebarWidth-24;
            return new CampaignHudLayout(viewWidth,viewHeight,
                BattleHudUi.ActionHeight(width,ClientPreferences.Active.Label(ControlAction.CampaignFire),"Bắn",BattleHudUi.State(false,run.Shot,"lượt",blockedReason:waiting)),
                BattleHudUi.ActionHeight(width,ClientPreferences.Active.Label(ControlAction.CampaignSkill),nation.SkillName,BattleHudUi.State(false,run.Skill,"lượt",blockedReason:waiting)),
                BattleHudUi.ActionHeight(width,ClientPreferences.Active.Label(ControlAction.CampaignShop),"Tiếp tế",string.IsNullOrEmpty(waiting)?"Mua và dùng ngay":waiting));
        }
        void DrawCampaignHud(RunState run,double now){
            CampaignUI.Ensure();EnsureCampaignHudStyles();var nation=CampaignNation.For(run.Nation);var layout=CampaignLayout(run,now);
            CartoonUi.Panel(layout.Sidebar,CartoonUi.PanelDark,8,0);
            Rect heading=layout.Vitals;
            CampaignTank(new Rect(heading.x,heading.y,28,28),run.Nation,0);
            GUI.Label(new Rect(heading.x+36,heading.y+3,heading.width-36,23),"CHIẾN DỊCH · "+nation.Name,campaignHudLightCompact);
            CampaignUI.Rule(new Rect(heading.x,heading.yMax,heading.width,1),CartoonUi.PanelColor);

            string feedback=client.Campaign.Error;
            if(string.IsNullOrEmpty(feedback)&&run.Turn>0&&!run.Finished&&now-campaignPresentation.Started<3.5)feedback=CampaignPresentation.ActionFeedback(run);
            if(feedback!=campaignLastFeedback){campaignLastFeedback=feedback;campaignHudScroll=Vector2.zero;}
            Rect objective=layout.Objective;
            GUI.Label(new Rect(objective.x,objective.y,objective.width,21),run.Distance.ToString("N0")+" / 10.000 m",campaignHudLightCompact);
            CampaignUI.Meter(new Rect(objective.x,objective.y+25,objective.width,8),run.Distance/(float)CampaignClient.GoalDistance,CartoonUi.Gold);
            string bosses="Trùm "+run.Bosses+" / "+CampaignClient.BossCount;
            float goldX=objective.x+campaignHudHintCompact.CalcSize(new GUIContent(bosses)).x+12;
            GUI.Label(new Rect(objective.x,objective.y+38,goldX-objective.x-8,20),bosses,campaignHudHintCompact);
            ItemIconArt.DrawGold(new Rect(goldX,objective.y+39,17,17));
            GUI.Label(new Rect(goldX+21,objective.y+38,objective.xMax-goldX-21,20),run.Gold.ToString("N0")+" vàng chuyến đi",campaignHudHintCompact);
            Rect info=layout.Details;
            float contentWidth=info.width-18;
            if(!string.IsNullOrEmpty(feedback)&&info.height>0){
                float noticeHeight=CampaignLabelHeight(CampaignUI.DialogueText,feedback,contentWidth-32)+22;
                campaignHudScroll=GUI.BeginScrollView(info,campaignHudScroll,new Rect(0,0,contentWidth,Mathf.Max(info.height,noticeHeight)));
                CampaignUI.Notice(new Rect(0,0,contentWidth,noticeHeight),feedback,!string.IsNullOrEmpty(client.Campaign.Error));
                // A disabled background must not consume the modal's wheel events.
                GUI.EndScrollView(GUI.enabled);
            }
            int effect=0;
            if(run.Shields>0)DrawCampaignBuff(CampaignHudGeometry.EffectBounds(layout.Effects,effect++),UiIcon.Armor,"Khiên",run.Shields,"Chặn "+run.Shields+" đòn đánh còn lại. Mỗi lần chặn giảm 1; không giảm theo lượt di chuyển.");
            if(run.DoubleHits>0)DrawCampaignBuff(CampaignHudGeometry.EffectBounds(layout.Effects,effect),UiIcon.Damage,"Đòn ×2",run.DoubleHits,"Còn "+run.DoubleHits+" đòn cận chiến hoặc phát bắn trúng mục tiêu được nhân đôi sát thương.");
            bool active=!AnyDialog&&!SkillDetailsUi.InputBlocked&&!campaignShopOpen&&!campaignEndConfirm&&!campaignInputRelease&&!client.Campaign.Pending&&!run.Finished&&!campaignPresentation.Busy(now);
            string waiting=CampaignActionWaiting(run,now);
            if(BattleHudUi.Action(layout.Fire,UiIcon.Fire,ClientPreferences.Active.Label(ControlAction.CampaignFire),"Bắn",active&&run.Shot==0,run.Shot,run.ShotCooldown,"lượt",blockedReason:waiting,primary:true))client.CampaignCommand(1,4);
            UiIcon skillIcon=run.Nation==0?UiIcon.Airstrike:run.Nation==1?UiIcon.Armor:UiIcon.Skill;
            if(BattleHudUi.Action(layout.Skill,skillIcon,ClientPreferences.Active.Label(ControlAction.CampaignSkill),nation.SkillName,active&&run.Skill==0,run.Skill,run.SkillCooldown,"lượt",blockedReason:waiting,description:nation.SkillDescription,showCooldownBorder:false))client.CampaignCommand(1,5);
            if(BattleHudUi.Action(layout.Supply,UiIcon.Supply,ClientPreferences.Active.Label(ControlAction.CampaignShop),"Tiếp tế",active,0,1,"lượt","Mua và dùng ngay",waiting))ToggleCampaignShop();
            Rect saveExit=layout.End;saveExit.width=(saveExit.width-8)/2;
            Rect finish=saveExit;finish.x=saveExit.xMax+8;
            if(CampaignUI.Button(saveExit,"Lưu và thoát",active,true)){client.CampaignCommand(6);campaignPage=1;GateCampaignInputUntilReleased();}
            if(CampaignUI.Button(finish,"Kết thúc",active,true,false,"",true)){campaignEndConfirm=true;GateCampaignInputUntilReleased();}
        }
        void DrawCampaignBuff(Rect rect,UiIcon icon,string title,int count,string description){
            SkillDetailsUi.Inspect(rect,title,description);
            CartoonUi.Panel(rect,CartoonUi.PanelColor,8);
            CartoonUi.Icon(new Rect(rect.x+6,rect.y+4,29,29),icon,icon==UiIcon.Armor?new Color(.65f,.85f,1):CartoonUi.Gold);
            Rect badge=new Rect(rect.xMax-22,rect.yMax-20,22,20);
            CartoonUi.Panel(badge,CartoonUi.Ink,5);
            GUI.Label(badge,count.ToString(),campaignBuffCount);
        }
        void DrawCampaignRegionBanner(Rect viewport,double now){
            float opacity=campaignPresentation.RegionOpacity(now);if(opacity<=0)return;
            EnsureCampaignHudStyles();
            float width=Mathf.Min(360,viewport.width-28),height=Mathf.Max(44,campaignRegionTitle.CalcHeight(new GUIContent(campaignPresentation.RegionTitle),width-28)+18);
            Rect banner=new Rect(viewport.center.x-width/2,viewport.y+14,width,height);
            Color previous=GUI.color;
            try {
                GUI.color=new Color(previous.r,previous.g,previous.b,previous.a*opacity);
                CartoonUi.Panel(banner,CartoonUi.PanelDark,8);
                GUI.Label(new Rect(banner.x+14,banner.y+8,banner.width-28,banner.height-16),campaignPresentation.RegionTitle,campaignRegionTitle);
            }finally {GUI.color=previous;}
        }
        void DrawCampaignEndConfirmation(){
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(.035f,.055f,.04f,.9f));
            const string title="KẾT THÚC CHUYẾN ĐI?",message="Thành tích hiện tại sẽ được lưu. Chọn Tiếp tục để trở lại chiến trường.";
            float width=Mathf.Min(520,viewWidth-32),buttonWidth=(width-60)/2,titleHeight=CampaignLabelHeight(CampaignUI.Title,title,width-48,34),messageHeight=CampaignLabelHeight(CampaignUI.Text,message,width-48,54),buttonHeight=Mathf.Max(CampaignButtonHeight("Tiếp tục",buttonWidth),CampaignButtonHeight("Kết thúc",buttonWidth));
            float height=Mathf.Max(246,22+titleHeight+16+messageHeight+26+buttonHeight+24);Rect card=new Rect((viewWidth-width)/2,(viewHeight-height)/2,width,height);CampaignUI.Panel(card);
            GUI.Label(new Rect(card.x+24,card.y+22,width-48,titleHeight),title,CampaignUI.Title);
            GUI.Label(new Rect(card.x+24,card.y+38+titleHeight,width-48,messageHeight),message,CampaignUI.Text);
            if(CampaignUI.Button(new Rect(card.x+24,card.yMax-24-buttonHeight,buttonWidth,buttonHeight),"Tiếp tục",true,true)){campaignEndConfirm=false;GateCampaignInputUntilReleased();}
            if(CampaignUI.Button(new Rect(card.x+36+buttonWidth,card.yMax-24-buttonHeight,buttonWidth,buttonHeight),"Kết thúc",!client.Campaign.Pending,false,false,"",true)){campaignEndConfirm=false;GateCampaignInputUntilReleased();client.CampaignCommand(2);}
        }
    }
}
