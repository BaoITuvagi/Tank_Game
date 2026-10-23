using UnityEngine;
using Tank.Campaign;

namespace Tank {
    public partial class TankApp {
        void DrawCampaignHud(RunState run,double now){
            CampaignUI.Ensure();var nation=CampaignNation.For(run.Nation);
            CampaignUI.Background(new Rect(0,0,viewWidth,100));CampaignUI.Background(new Rect(0,viewHeight-96,viewWidth,96));
            float healthWidth=Mathf.Clamp(viewWidth*.24f,210,280),journeyX=healthWidth+30,journeyWidth=viewWidth-journeyX-184;
            GUI.Box(new Rect(12,10,healthWidth,80),GUIContent.none,CampaignUI.Dark);
            CampaignTank(new Rect(22,24,48,48),run.Nation,0);
            GUI.Label(new Rect(82,17,healthWidth-82,26),nation.Name+" · Giáp "+run.Armor,CampaignUI.Light);
            CampaignHealthBar(new Rect(82,48,healthWidth-96,14),run.Hp,run.MaxHp);
            GUI.Label(new Rect(82,66,healthWidth-82,20),run.Hp+" / "+run.MaxHp+" HP",CampaignUI.SmallLight);
            string region=string.IsNullOrEmpty(run.RegionName)?run.StageName:run.RegionName;
            GUI.Label(new Rect(journeyX,14,journeyWidth,28),string.IsNullOrEmpty(region)?"Chiến dịch giải cứu":region,CampaignUI.Light);
            GUI.Label(new Rect(journeyX,43,journeyWidth,23),run.Distance+" / 10.000 m · Trùm "+run.Bosses+"/20",CampaignUI.SmallLight);
            CampaignUI.Meter(new Rect(journeyX,73,journeyWidth,10),run.Distance/10000f,new Color(1,.85f,.5f));
            if(CampaignUI.Button(new Rect(viewWidth-160,14,144,38),"Kết thúc & lưu",!campaignShopOpen&&!client.Campaign.Pending&&!run.Finished&&!campaignPresentation.Busy(now),true))client.CampaignCommand(2);
            GUI.Label(new Rect(viewWidth-158,59,142,28),"Hạ địch: "+run.Kills,CampaignUI.SmallLight);
            float footerWidth=Mathf.Min(1000,viewWidth-32),x=(viewWidth-footerWidth)/2,width=(footerWidth-24)/3;
            bool active=!campaignShopOpen&&!client.Campaign.Pending&&!run.Finished&&!campaignPresentation.Busy(now);
            string shot=run.Shot>0?"Còn "+run.Shot+" lượt":"Sẵn sàng · "+run.ShotDamage+" sát thương";
            string skill=run.Skill>0?"Còn "+run.Skill+" lượt":"Sẵn sàng · hồi "+run.SkillCooldown+" lượt";
            if(CampaignUI.Button(new Rect(x,viewHeight-84,width,54),"I · Bắn\n"+shot,active&&run.Shot==0,false,false,"Bắn theo hướng xe, tầm "+run.ShotRange+" ô. Hồi 5 lượt."))client.CampaignCommand(1,4);
            if(CampaignUI.Button(new Rect(x+width+12,viewHeight-84,width,54),"O · "+nation.SkillName+"\n"+skill,active&&run.Skill==0,false,false,nation.SkillDescription))client.CampaignCommand(1,5);
            string stock=viewWidth>=1000?run.Gold+" vàng · "+run.Shields+" khiên · x2: "+run.DoubleHits:run.Gold+" vàng · khiên "+run.Shields;
            if(CampaignUI.Button(new Rect(x+2*(width+12),viewHeight-84,width,54),"P · Tiếp tế\n"+stock,active,true))ToggleCampaignShop();
            GUI.Label(new Rect(18,viewHeight-24,viewWidth-36,22),"WASD / Mũi tên: di chuyển   •   I: bắn   •   O: kỹ năng   •   P: cửa hàng   •   Khiên "+run.Shields+" / "+run.MaxBuff+"   •   Đòn x2: "+run.DoubleHits,CampaignUI.SmallLight);
        }
    }
}
