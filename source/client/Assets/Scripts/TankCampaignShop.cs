using UnityEngine;
using Tank.Campaign;

namespace Tank {
    public partial class TankApp {
        bool campaignShopOpen,campaignShopAttempted;
        Vector2 campaignShopScroll;
        void ToggleCampaignShop(){
            campaignShopOpen=!campaignShopOpen;queuedCampaignAction=-1;
            if(campaignShopOpen){campaignShopAttempted=false;campaignShopScroll=Vector2.zero;client.Campaign.Error="";}
            nextCampaignRepeat=Time.unscaledTimeAsDouble+.18;
        }
        void DrawCampaignShop(RunState run){
            CampaignUI.Ensure();ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(0,0,0,.76f));
            float width=Mathf.Min(780,viewWidth-32),height=Mathf.Min(670,viewHeight-32);
            Rect card=new Rect((viewWidth-width)/2,(viewHeight-height)/2,width,height);CampaignUI.Panel(card);
            GUI.Label(new Rect(card.x+26,card.y+22,width-52,36),"CỬA HÀNG TIẾP TẾ",CampaignUI.Title);
            GUI.Label(new Rect(card.x+26,card.y+67,width-52,28),run.Gold+" vàng · "+run.Distance+" m · Mua không tốn lượt",CampaignUI.Text);
            GUI.Label(new Rect(card.x+26,card.y+100,width-52,26),"HP "+run.Hp+"/"+run.MaxHp+"   •   Khiên "+run.Shields+"/"+run.MaxBuff+"   •   Đòn x2 "+run.DoubleHits+"/"+run.MaxBuff,CampaignUI.Hint);
            Rect viewport=new Rect(card.x+18,card.y+140,width-36,Mathf.Max(80,height-264));
            campaignShopScroll=GUI.BeginScrollView(viewport,campaignShopScroll,new Rect(0,0,viewport.width-18,384));
            string[] titles={"Hồi máu","Khiên bảo vệ","Tăng sát thương"};
            string[] descriptions={"Hồi ngay 25 HP, tối đa "+run.MaxHp+" HP của xe.","Nhận 3 khiên. Mỗi khiên chặn trọn một đòn.","Nhận 3 đòn x2 cho I hoặc cận chiến.\nChỉ trừ khi đánh trúng; không áp dụng cho O."};
            int[] icons={0,2,1};
            for(int item=0;item<3;item++){
                Rect row=new Rect(0,item*126,viewport.width-20,116);CampaignUI.Panel(row);
                GUI.Box(new Rect(14,row.y+22,62,64),GUIContent.none,CampaignUI.Slot);CampaignArt.Pickup(new Rect(20,row.y+27,50,50),icons[item]);
                float textWidth=row.width-258;
                GUI.Label(new Rect(90,row.y+14,textWidth,29),titles[item],CampaignUI.Text);
                GUI.Label(new Rect(90,row.y+47,textWidth,62),descriptions[item],CampaignUI.Hint);
                bool affordable=run.CanBuy(item),ready=!client.Campaign.Pending&&!campaignPresentation.Busy(Time.unscaledTimeAsDouble);
                if(CampaignUI.Button(new Rect(row.width-156,row.y+24,142,40),run.Prices[item]+" vàng",affordable&&ready)){campaignShopAttempted=true;client.CampaignCommand(5,item);}
                string reason=!run.ShopAvailable?"Cần cập nhật server":item==0&&run.Hp>=run.MaxHp?"Máu đã đầy":item==1&&run.Shields>run.MaxBuff-3||item==2&&run.DoubleHits>run.MaxBuff-3?"Đã đạt giới hạn":run.Gold<run.Prices[item]?"Thiếu "+(run.Prices[item]-run.Gold)+" vàng":"Có thể mua";
                GUI.Label(new Rect(row.width-156,row.y+75,142,28),reason,CampaignUI.Hint);
            }
            GUI.EndScrollView();
            string feedback=client.Campaign.Pending?"Đang mua…":!string.IsNullOrEmpty(client.Campaign.Error)?client.Campaign.Error:campaignShopAttempted&&!string.IsNullOrEmpty(run.Message)?run.Message:"Giá tăng mỗi 250 m. Vàng được trừ ngay khi mua.";
            CampaignUI.Notice(new Rect(card.x+20,card.yMax-116,width-40,52),feedback);
            if(CampaignUI.Button(new Rect(card.x+26,card.yMax-54,width-52,38),"Đóng cửa hàng · P / Esc",true,true))ToggleCampaignShop();
        }
    }
}
