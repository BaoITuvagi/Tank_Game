using UnityEngine;
using Tank.Campaign;

namespace Tank {
    public partial class TankApp {
        bool campaignEndConfirm;
        void DrawCampaignHud(RunState run,double now){
            CampaignUI.Ensure();var nation=CampaignNation.For(run.Nation);
            CampaignUI.Background(new Rect(0,0,viewWidth,100));CampaignUI.Background(new Rect(0,viewHeight-96,viewWidth,96));
            float healthWidth=Mathf.Clamp(viewWidth*.26f,280,320),journeyX=healthWidth+30,journeyWidth=viewWidth-journeyX-190;
            CartoonUi.Panel(new Rect(10,8,healthWidth,82),CartoonUi.PanelDark,14,3);
            CampaignTank(new Rect(20,22,54,54),run.Nation,0);
            string healthTitle=nation.Name+" · Giáp "+run.Armor;
            GUI.Label(new Rect(85,13,healthWidth-94,CampaignLabelHeight(CampaignUI.Light,healthTitle,healthWidth-94,27)),healthTitle,CampaignUI.Light);
            CampaignHealthBar(new Rect(85,47,healthWidth-102,12),run.Hp,run.MaxHp);
            GUI.Label(new Rect(85,64,healthWidth-94,23),run.Hp+" / "+run.MaxHp+" HP",CampaignUI.SmallLight);
            string region=string.IsNullOrEmpty(run.RegionName)?run.StageName:run.RegionName;
            string regionTitle=string.IsNullOrEmpty(region)?"Chiến dịch giải cứu":region;
            GUI.Label(new Rect(journeyX,11,journeyWidth,CampaignLabelHeight(CampaignUI.Light,regionTitle,journeyWidth,29)),regionTitle,CampaignUI.Light);
            GUI.Label(new Rect(journeyX,42,journeyWidth,24),run.Distance.ToString("N0")+" / 10.000 m   ·   Trùm "+run.Bosses+"/"+CampaignClient.BossCount,CampaignUI.SmallLight);
            CampaignUI.Meter(new Rect(journeyX,75,journeyWidth,9),run.Distance/(float)CampaignClient.GoalDistance,CartoonUi.Gold);
            bool active=!campaignShopOpen&&!campaignEndConfirm&&!client.Campaign.Pending&&!run.Finished&&!campaignPresentation.Busy(now);
            if(CampaignUI.Button(new Rect(viewWidth-168,10,150,CampaignButtonHeight("Kết thúc",150)),"Kết thúc",active,true,false,"",true))campaignEndConfirm=true;
            CartoonUi.Icon(new Rect(viewWidth-162,62,21,21),UiIcon.Damage,CartoonUi.Danger);
            GUI.Label(new Rect(viewWidth-133,60,118,25),run.Kills+" địch đã hạ",CampaignUI.SmallLight);
            float width=Mathf.Min(352,(viewWidth-48)/3),hotbarWidth=3*width+20,x=(viewWidth-hotbarWidth)/2;
            string waiting=run.Finished?"Đã kết thúc":client.Campaign.Pending?"Đang xử lý…":campaignShopOpen?"Đang tiếp tế":campaignEndConfirm?"Đang xác nhận":campaignPresentation.Busy(now)?"Đang hành động…":"";
            if(DrawCampaignSkill(new Rect(x,viewHeight-87,width,62),"I","Bắn",UiIcon.Fire,run.Shot,run.ShotCooldown,run.ShotDamage+" sát thương · "+run.ShotRange+" ô",active,waiting))client.CampaignCommand(1,4);
            UiIcon skillIcon=run.Nation==0?UiIcon.Airstrike:run.Nation==1?UiIcon.Armor:UiIcon.Skill;
            if(DrawCampaignSkill(new Rect(x+width+10,viewHeight-87,width,62),"O",nation.SkillName,skillIcon,run.Skill,run.SkillCooldown,"Sẵn sàng · hồi "+run.SkillCooldown+" lượt",active,waiting))client.CampaignCommand(1,5);
            if(DrawCampaignSkill(new Rect(x+2*(width+10),viewHeight-87,width,62),"P","Tiếp tế",UiIcon.Supply,0,1,run.Gold+" vàng · Vật phẩm",active,waiting))ToggleCampaignShop();
            GUI.Label(new Rect(18,viewHeight-22,viewWidth-36,21),"WASD / Mũi tên: di chuyển   ·   Khiên "+run.Shields+" / "+run.MaxBuff+"   ·   Đòn x2: "+run.DoubleHits,CampaignUI.SmallLight);
        }
        bool DrawCampaignSkill(Rect rect,string key,string title,UiIcon icon,int cooldown,int maximum,string readyLabel,bool active,string waiting){
            bool available=active&&cooldown==0;
            bool previous=GUI.enabled;GUI.enabled=previous&&available;
            GUI.SetNextControlName("CampaignSkill"+key);
            bool pressed=GUI.Button(rect,GUIContent.none,available?CampaignUI.Selected:CampaignUI.Disabled);
            GUI.enabled=previous;
            CartoonUi.Icon(new Rect(rect.x+12,rect.y+14,28,28),icon,available?CartoonUi.Ink:CartoonUi.Text);
            CartoonUi.Badge(new Rect(rect.x+44,rect.y+10,26,25),key,CartoonUi.PanelDark);
            var titleStyle=available?CampaignUI.Text:CampaignUI.SkillTitleLight;
            float titleHeight=CampaignLabelHeight(titleStyle,title,rect.width-86,24);
            GUI.Label(new Rect(rect.x+78,rect.y+5,rect.width-86,titleHeight),title,titleStyle);
            string state=cooldown>0?"Hồi "+cooldown+" lượt":!string.IsNullOrEmpty(waiting)?waiting:readyLabel;
            var stateStyle=available?CampaignUI.Hint:CampaignUI.SmallLight;
            GUI.Label(new Rect(rect.x+78,rect.y+6+titleHeight,rect.width-86,CampaignLabelHeight(stateStyle,state,rect.width-86,21)),state,stateStyle);
            if(cooldown>0)CampaignUI.Meter(new Rect(rect.x+10,rect.yMax-6,rect.width-20,4),1-cooldown/(float)Mathf.Max(1,maximum),CartoonUi.Gold);
            return pressed;
        }
        void DrawCampaignEndConfirmation(){
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(.04f,.08f,.14f,.8f));
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
