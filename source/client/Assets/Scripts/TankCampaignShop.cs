using UnityEngine;
using Tank.Campaign;

namespace Tank {
    public partial class TankApp {
        bool campaignShopOpen,campaignShopAttempted;
        Vector2 campaignShopScroll;
        void ToggleCampaignShop(){
            campaignShopOpen=!campaignShopOpen;campaignAudio?.ShopToggle(campaignShopOpen);queuedCampaignAction=-1;
            if(campaignShopOpen){campaignShopAttempted=false;campaignShopScroll=Vector2.zero;client.Campaign.Error="";}
            nextCampaignRepeat=Time.unscaledTimeAsDouble+.18;
        }
        void DrawCampaignShop(RunState run){
            CampaignUI.Ensure();ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(.035f,.055f,.04f,.9f));
            float width=Mathf.Min(960,viewWidth-32),height=Mathf.Min(700,viewHeight-32);
            Rect card=new Rect((viewWidth-width)/2,(viewHeight-height)/2,width,height);CartoonUi.Panel(card,CartoonUi.PanelDark,10,0);
            CampaignUI.Rule(new Rect(card.x+20,card.y+1,width-40,4),CartoonUi.Gold);
            const string title="CỬA HÀNG TIẾP TẾ";
            string close="Trở lại chiến trường · "+ClientPreferences.Active.Label(ControlAction.CampaignShop)+" / Esc";
            string summary=run.Gold+" vàng · "+run.Distance+" m · Mua không tốn lượt",stock="HP "+run.Hp+"/"+run.MaxHp+"   ·   Khiên "+run.Shields+"/"+run.MaxBuff+"   ·   Đòn x2 "+run.DoubleHits+"/"+run.MaxBuff;
            float titleHeight=CampaignLabelHeight(CampaignUI.Header,title,width-116,34),summaryHeight=CampaignLabelHeight(CampaignUI.SmallLight,summary,width-116,24),stockHeight=CampaignLabelHeight(CampaignUI.SmallLight,stock,width-64,24),headerHeight=titleHeight+summaryHeight+stockHeight+30;
            CartoonUi.Icon(new Rect(card.x+30,card.y+30,38,38),UiIcon.Supply,CartoonUi.Gold);
            GUI.Label(new Rect(card.x+84,card.y+23,width-116,titleHeight),title,CampaignUI.Header);
            GUI.Label(new Rect(card.x+84,card.y+32+titleHeight,width-116,summaryHeight),summary,CampaignUI.SmallLight);
            GUI.Label(new Rect(card.x+32,card.y+38+titleHeight+summaryHeight,width-64,stockHeight),stock,CampaignUI.SmallLight);
            string feedback=client.Campaign.Pending?"Đang mua…":!string.IsNullOrEmpty(client.Campaign.Error)?client.Campaign.Error:campaignShopAttempted&&!string.IsNullOrEmpty(run.Message)?run.Message:"Giá tăng mỗi 250 m. Vàng được trừ ngay khi mua.";
            float closeHeight=CampaignButtonHeight(close,width-52),closeY=card.yMax-14-closeHeight,noticeHeight=CampaignLabelHeight(CampaignUI.DialogueText,feedback,width-76,26)+26,noticeY=closeY-10-noticeHeight;
            Rect viewport=new Rect(card.x+20,card.y+30+headerHeight,width-40,Mathf.Max(60,noticeY-card.y-headerHeight-42));
            string[] titles={"Hồi máu","Khiên bảo vệ","Tăng sát thương"};
            string[] descriptions={"Hồi ngay 25 HP, tối đa "+run.MaxHp+" HP của xe.","Nhận 3 khiên. Mỗi khiên chặn trọn một đòn.","Nhận 3 đòn x2 cho "+ClientPreferences.Active.Label(ControlAction.CampaignFire)+" hoặc cận chiến.\nChỉ trừ khi đánh trúng; không áp dụng cho kỹ năng."};
            int[] icons={0,2,1};
            bool columns=width>=760;
            string[] reasons=new string[3];float[] rowHeights=new float[3],titleHeights=new float[3],descriptionHeights=new float[3],reasonHeights=new float[3],priceHeights=new float[3];float contentHeight=0,rowWidth=columns?(viewport.width-44)/3:viewport.width-22,textWidth=columns?rowWidth-32:rowWidth-266;
            for(int item=0;item<3;item++){
                reasons[item]=!run.ShopAvailable?"Cần cập nhật server":item==0&&run.Hp>=run.MaxHp?"Máu đã đầy":item==1&&run.Shields>run.MaxBuff-3||item==2&&run.DoubleHits>run.MaxBuff-3?"Đã đạt giới hạn":run.Gold<run.Prices[item]?"Thiếu "+(run.Prices[item]-run.Gold)+" vàng":"Có thể mua";
                titleHeights[item]=CampaignLabelHeight(CampaignUI.Text,titles[item],textWidth,28);descriptionHeights[item]=CampaignLabelHeight(CampaignUI.Hint,descriptions[item],textWidth);
                reasonHeights[item]=CampaignLabelHeight(CampaignUI.Hint,reasons[item],columns?textWidth:142,24);priceHeights[item]=CampaignButtonHeight(run.Prices[item]+" vàng",columns?textWidth:142,48);
                // Portrait, measured copy and purchase controls have separate space.
                rowHeights[item]=columns?120+titleHeights[item]+5+descriptionHeights[item]+20+priceHeights[item]+8+reasonHeights[item]+24:Mathf.Max(112,13+titleHeights[item]+5+descriptionHeights[item]+14,17+priceHeights[item]+8+reasonHeights[item]+14);
                contentHeight=columns?Mathf.Max(contentHeight,rowHeights[item]+10):contentHeight+rowHeights[item]+10;
            }
            campaignShopScroll=GUI.BeginScrollView(viewport,campaignShopScroll,new Rect(0,0,viewport.width-20,Mathf.Max(viewport.height,contentHeight)));
            float rowY=0;
            for(int item=0;item<3;item++){
                Rect row=new Rect(columns?item*(rowWidth+12):0,columns?0:rowY,rowWidth,columns?contentHeight-10:rowHeights[item]);CartoonUi.Panel(row,CartoonUi.Cream,8,0);
                CampaignUI.Rule(new Rect(row.x+14,row.y+1,row.width-28,4),item==0?CartoonUi.Ready:item==1?CartoonUi.Sand:CartoonUi.Danger);
                Rect icon=columns?new Rect(row.center.x-44,row.y+18,88,88):new Rect(row.x+12,row.y+20,66,66);
                CartoonUi.Panel(icon,CartoonUi.PanelDark,6,0);CampaignArt.Pickup(new Rect(icon.x+8,icon.y+8,icon.width-16,icon.height-16),icons[item]);
                float textX=columns?row.x+16:row.x+92,textY=columns?row.y+120:row.y+13;
                GUI.Label(new Rect(textX,textY,textWidth,titleHeights[item]),titles[item],CampaignUI.Text);
                GUI.Label(new Rect(textX,textY+titleHeights[item]+5,textWidth,descriptionHeights[item]),descriptions[item],CampaignUI.Hint);
                bool affordable=run.CanBuy(item),ready=!client.Campaign.Pending&&!campaignPresentation.Busy(Time.unscaledTimeAsDouble);
                float purchaseX=columns?row.x+16:row.xMax-156,purchaseY=columns?row.yMax-priceHeights[item]-reasonHeights[item]-32:row.y+17,purchaseWidth=columns?textWidth:142;
                if(CampaignUI.Button(new Rect(purchaseX,purchaseY,purchaseWidth,priceHeights[item]),run.Prices[item]+" vàng",affordable&&ready)){campaignShopAttempted=true;client.CampaignCommand(5,item);}
                GUI.Label(new Rect(purchaseX,purchaseY+priceHeights[item]+7,purchaseWidth,reasonHeights[item]),reasons[item],CampaignUI.Hint);
                rowY+=row.height+10;
            }
            GUI.EndScrollView();
            CampaignUI.Notice(new Rect(card.x+22,noticeY,width-44,noticeHeight),feedback,!string.IsNullOrEmpty(client.Campaign.Error),feedback.StartsWith("Đã mua "));
            if(CampaignUI.Button(new Rect(card.x+26,closeY,width-52,closeHeight),close,true,true))ToggleCampaignShop();
        }
    }
}
