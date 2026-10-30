using UnityEngine;
using Tank.Campaign;

namespace Tank {
    public partial class TankApp {
        bool campaignEndConfirm;
        GUIStyle campaignHudLightCompact,campaignHudHintCompact,campaignHudTextCompact,campaignHudDarkHintCompact;
        void EnsureCampaignHudStyles(){
            if(campaignHudLightCompact!=null)return;
            campaignHudLightCompact=new GUIStyle(CampaignUI.Light){fontSize=15};
            campaignHudHintCompact=new GUIStyle(CampaignUI.SmallLight){fontSize=12};
            campaignHudTextCompact=new GUIStyle(CampaignUI.Text){fontSize=15};
            campaignHudDarkHintCompact=new GUIStyle(CampaignUI.Hint){fontSize=12};
        }
        void DrawCampaignHud(RunState run,double now){
            CampaignUI.Ensure();EnsureCampaignHudStyles();var nation=CampaignNation.For(run.Nation);bool compact=viewWidth<800;
            CampaignUI.Background(new Rect(0,0,viewWidth,100));CampaignUI.Background(new Rect(0,viewHeight-96,viewWidth,96));
            CampaignUI.Rule(new Rect(0,97,viewWidth,3),CartoonUi.PanelColor);
            CampaignUI.Rule(new Rect(0,viewHeight-96,viewWidth,3),CartoonUi.PanelColor);
            float healthWidth=compact?196:Mathf.Clamp(viewWidth*.26f,260,320),journeyX=healthWidth+(compact?26:32),journeyWidth=viewWidth-journeyX-(compact?154:190);
            CampaignUI.Rule(new Rect(healthWidth+14,16,1,68),CartoonUi.PanelColor);
            CampaignTank(new Rect(compact?12:20,22,compact?48:54,compact?48:54),run.Nation,0);
            string healthTitle=nation.Name+" · Giáp "+run.Armor;
            float healthX=compact?70:85,healthTextWidth=compact?healthWidth-72:healthWidth-94;
            var titleStyle=compact?campaignHudLightCompact:CampaignUI.Light;var hintStyle=compact?campaignHudHintCompact:CampaignUI.SmallLight;
            GUI.Label(new Rect(healthX,13,healthTextWidth,CampaignLabelHeight(titleStyle,healthTitle,healthTextWidth,27)),healthTitle,titleStyle);
            CampaignHealthBar(new Rect(healthX,47,healthTextWidth-6,12),run.Hp,run.MaxHp);
            GUI.Label(new Rect(healthX,64,healthTextWidth,23),run.Hp+" / "+run.MaxHp+" HP",hintStyle);
            string region=string.IsNullOrEmpty(run.RegionName)?run.StageName:run.RegionName;
            string regionTitle=string.IsNullOrEmpty(region)?"Chiến dịch giải cứu":region;
            float regionHeight=CampaignLabelHeight(titleStyle,regionTitle,journeyWidth,27),distanceY=Mathf.Max(42,14+regionHeight);
            GUI.Label(new Rect(journeyX,11,journeyWidth,regionHeight),regionTitle,titleStyle);
            string distance=run.Distance.ToString("N0")+" / 10.000 m   ·   Trùm "+run.Bosses+"/"+CampaignClient.BossCount;
            GUI.Label(new Rect(journeyX,distanceY,journeyWidth,CampaignLabelHeight(hintStyle,distance,journeyWidth,22)),distance,hintStyle);
            CampaignUI.Meter(new Rect(journeyX,75,journeyWidth,9),run.Distance/(float)CampaignClient.GoalDistance,CartoonUi.Gold);
            bool active=!settingsOpen&&!campaignShopOpen&&!campaignEndConfirm&&!client.Campaign.Pending&&!run.Finished&&!campaignPresentation.Busy(now);
            float leaveWidth=compact?122:150,leaveX=viewWidth-leaveWidth-18;
            if(CampaignUI.Button(new Rect(leaveX,10,leaveWidth,CampaignButtonHeight("Kết thúc",leaveWidth)),"Kết thúc",active,true,false,"",true))campaignEndConfirm=true;
            DrawSettingsButton(new Rect(leaveX,60,leaveWidth,28));
            float hotbarWidth=Mathf.Min(1100,viewWidth-40),x=(viewWidth-hotbarWidth)/2,width=(hotbarWidth-20)/3;
            string waiting=run.Finished?"Đã kết thúc":client.Campaign.Pending?"Đang xử lý…":campaignShopOpen?"Đang tiếp tế":campaignEndConfirm?"Đang xác nhận":campaignPresentation.Busy(now)?"Đang hành động…":"";
            if(DrawCampaignSkill(new Rect(x,viewHeight-88,width,64),ClientPreferences.Active.Label(ControlAction.CampaignFire),"Bắn",UiIcon.Fire,run.Shot,run.ShotCooldown,run.ShotDamage+" sát thương · "+run.ShotRange+" ô",active,waiting))client.CampaignCommand(1,4);
            UiIcon skillIcon=run.Nation==0?UiIcon.Airstrike:run.Nation==1?UiIcon.Armor:UiIcon.Skill;
            if(DrawCampaignSkill(new Rect(x+width+10,viewHeight-88,width,64),ClientPreferences.Active.Label(ControlAction.CampaignSkill),nation.SkillName,skillIcon,run.Skill,run.SkillCooldown,"Sẵn sàng · hồi "+run.SkillCooldown+" lượt",active,waiting))client.CampaignCommand(1,5);
            if(DrawCampaignSkill(new Rect(x+2*(width+10),viewHeight-88,width,64),ClientPreferences.Active.Label(ControlAction.CampaignShop),"Tiếp tế",UiIcon.Supply,0,1,run.Gold+" vàng · Vật phẩm",active,waiting))ToggleCampaignShop();
            GUI.Label(new Rect(18,viewHeight-22,viewWidth-36,21),"Khiên "+run.Shields+" / "+run.MaxBuff+"   ·   Đòn x2: "+run.DoubleHits+"   ·   "+run.Kills+" địch đã hạ",hintStyle);
        }
        bool DrawCampaignSkill(Rect rect,string key,string title,UiIcon icon,int cooldown,int maximum,string readyLabel,bool active,string waiting){
            bool available=active&&cooldown==0;
            bool previous=GUI.enabled;GUI.enabled=previous&&available;
            GUI.SetNextControlName("CampaignSkill"+key);
            bool pressed=GUI.Button(rect,GUIContent.none,available?CampaignUI.Selected:CampaignUI.Disabled);
            GUI.enabled=previous;
            CampaignUI.Rule(new Rect(rect.x+2,rect.y+3,4,rect.height-6),available?CartoonUi.Gold:CartoonUi.PanelColor);
            bool compact=rect.width<240;
            CartoonUi.Icon(new Rect(rect.x+12,rect.y+(compact?7:20),compact?20:24,compact?20:24),icon,available?CartoonUi.Ink:CartoonUi.Text);
            float keyWidth=Mathf.Clamp(CampaignUI.SmallLight.CalcSize(new GUIContent(key)).x+16,26,Mathf.Min(120,rect.width*.4f));
            CartoonUi.Badge(new Rect(rect.x+(compact?10:43),rect.y+(compact?33:18),keyWidth,compact?23:27),key,CartoonUi.PanelDark);
            var titleStyle=compact?(available?campaignHudTextCompact:campaignHudLightCompact):available?CampaignUI.Text:CampaignUI.SkillTitleLight;
            float textX=rect.x+(compact?20:53)+keyWidth,textWidth=rect.xMax-textX-8,titleHeight=CampaignLabelHeight(titleStyle,title,textWidth,22);
            GUI.Label(new Rect(textX,rect.y+5,textWidth,titleHeight),title,titleStyle);
            string state=cooldown>0?"Hồi "+cooldown+" lượt":!string.IsNullOrEmpty(waiting)?waiting:readyLabel;
            var stateStyle=compact?(available?campaignHudDarkHintCompact:campaignHudHintCompact):available?CampaignUI.Hint:CampaignUI.SmallLight;
            GUI.Label(new Rect(textX,rect.y+6+titleHeight,textWidth,CampaignLabelHeight(stateStyle,state,textWidth,21)),state,stateStyle);
            if(cooldown>0)CampaignUI.Meter(new Rect(rect.x+10,rect.yMax-6,rect.width-20,4),1-cooldown/(float)Mathf.Max(1,maximum),CartoonUi.Gold);
            return pressed;
        }
        void DrawCampaignEndConfirmation(){
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(.035f,.055f,.04f,.9f));
            const string title="KẾT THÚC CHUYẾN ĐI?",message="Thành tích hiện tại sẽ được lưu. Chọn Tiếp tục để trở lại chiến trường.";
            float width=Mathf.Min(520,viewWidth-32),buttonWidth=(width-60)/2,titleHeight=CampaignLabelHeight(CampaignUI.Title,title,width-48,34),messageHeight=CampaignLabelHeight(CampaignUI.Text,message,width-48,54),buttonHeight=Mathf.Max(CampaignButtonHeight("Tiếp tục",buttonWidth),CampaignButtonHeight("Kết thúc",buttonWidth));
            float height=Mathf.Max(246,22+titleHeight+16+messageHeight+26+buttonHeight+24);Rect card=new Rect((viewWidth-width)/2,(viewHeight-height)/2,width,height);CampaignUI.Panel(card);
            GUI.Label(new Rect(card.x+24,card.y+22,width-48,titleHeight),title,CampaignUI.Title);
            GUI.Label(new Rect(card.x+24,card.y+38+titleHeight,width-48,messageHeight),message,CampaignUI.Text);
            if(CampaignUI.Button(new Rect(card.x+24,card.yMax-24-buttonHeight,buttonWidth,buttonHeight),"Tiếp tục",true,true))campaignEndConfirm=false;
            if(CampaignUI.Button(new Rect(card.x+36+buttonWidth,card.yMax-24-buttonHeight,buttonWidth,buttonHeight),"Kết thúc",!client.Campaign.Pending,false,false,"",true)){campaignEndConfirm=false;client.CampaignCommand(2);}
        }
    }
}
