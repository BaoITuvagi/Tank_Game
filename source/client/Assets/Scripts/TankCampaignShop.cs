using UnityEngine;
using Tank.Campaign;

namespace Tank {
    public partial class TankApp {
        bool campaignShopOpen;
        Vector2 campaignShopScroll;
        void ToggleCampaignShop(){
            campaignShopOpen=!campaignShopOpen;queuedCampaignAction=-1;
            if(campaignShopOpen){campaignShopScroll=Vector2.zero;client.Campaign.Error="";}
            nextCampaignRepeat=Time.unscaledTimeAsDouble+.18;
        }
        void DrawCampaignShop(RunState run){
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(0,0,0,.76f));
            float width=Mathf.Min(680,viewWidth-32),height=Mathf.Min(600,viewHeight-32);
            Rect card=new Rect((viewWidth-width)/2,(viewHeight-height)/2,width,height);
            menu.Card(card,"Cửa hàng tiếp tế");
            GUILayout.BeginArea(new Rect(card.x+24,card.y+78,card.width-48,card.height-98));
            campaignShopScroll=GUILayout.BeginScrollView(campaignShopScroll);
            GUILayout.Label("Vàng chuyến đi: "+run.Gold+"  ·  Quãng đường: "+run.Distance+" m",menu.Name);
            GUILayout.Label("Giá tăng mỗi 250 m. Mua không tốn lượt.\nHP "+run.Hp+"/100  ·  Khiên "+run.Shields+"/"+run.MaxBuff+"  ·  Đòn x2 "+run.DoubleHits+"/"+run.MaxBuff,menu.Hint);
            if(!run.ShopAvailable)GUILayout.Label("Cần cập nhật server để dùng cửa hàng.",menu.Hint);
            string[] titles={"Hồi máu","Khiên bảo vệ","Tăng sát thương"};
            string[] descriptions={"Hồi ngay 25 HP, tối đa 100 HP.","Thêm 3 khiên. Mỗi khiên chặn trọn một đòn.","Thêm 3 đòn x2 sát thương cận chiến hoặc I.\nChỉ trừ lượt khi đánh trúng; không áp dụng cho J."};
            int[] icons={0,2,1};
            for(int item=0;item<3;item++){
                GUILayout.Space(8);GUILayout.BeginVertical(GUI.skin.box);GUILayout.BeginHorizontal();
                Rect icon=GUILayoutUtility.GetRect(54,64,GUILayout.Width(54));CampaignArt.Pickup(icon,icons[item]);
                GUILayout.BeginVertical();GUILayout.Label(titles[item],menu.Name);GUILayout.Label(descriptions[item],menu.Hint);GUILayout.EndVertical();
                GUILayout.BeginVertical(GUILayout.Width(140));GUILayout.Space(12);
                bool enabled=GUI.enabled;GUI.enabled=enabled&&!client.Campaign.Pending&&!campaignPresentation.Busy(Time.unscaledTimeAsDouble)&&run.CanBuy(item);
                if(GUILayout.Button("Mua "+run.Prices[item]+" vàng",menu.Primary,GUILayout.Height(38)))client.CampaignCommand(5,item);
                GUI.enabled=enabled;GUILayout.EndVertical();GUILayout.EndHorizontal();GUILayout.EndVertical();
            }
            GUILayout.Space(10);
            string feedback=client.Campaign.Pending?"Đang mua…":!string.IsNullOrEmpty(client.Campaign.Error)?client.Campaign.Error:run.Message;
            GUILayout.Label(feedback,menu.Hint);
            if(GUILayout.Button("Đóng cửa hàng (P / Esc)",menu.Secondary,GUILayout.Height(40)))ToggleCampaignShop();
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
    }
}
