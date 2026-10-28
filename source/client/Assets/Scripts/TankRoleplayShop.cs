using System;
using UnityEngine;
namespace Tank {
    public partial class TankApp {
        bool roleplayShopOpen;
        Vector2 roleplayShopScroll;
        readonly RoleplayShopOpeningNotice shopOpeningNotice=new RoleplayShopOpeningNotice();
        GUIStyle shopOpeningText;
        static readonly string[] ShopItemNames={"Cứu thương","Giáp","Đạn nổ","Thanh tẩy","Bất tử","Phân tách"};
        static readonly string[] ShopItemDescriptions={"Hồi 30% máu tối đa.","Giảm 35% sát thương trong 6 giây.","Đạn gây nổ lan trong 5 giây.","Gỡ các hiệu ứng bất lợi đang có.","Chặn sát thương trong 3 giây.","3 phát bắn có khả năng tách đạn."};
        void UpdateRoleplayShop() {
            UpdateShopOpeningNotice();
            if(client.Phase!=ClientPhase.PLAYING||client.RoomMode!=RoomMode.ROLEPLAY||client.ItemWave==0){roleplayShopOpen=false;return;}
            if(!string.IsNullOrEmpty(walletNotice))return;
            if(!GameWindowFocus.IsForeground)return;
            if(roleplayShopOpen&&Input.GetKeyDown(KeyCode.Escape)){roleplayShopOpen=false;return;}
            if(Input.GetKeyDown(KeyCode.E)&&!leaveMatchForm&&!profileForm&&!joinForm) {
                roleplayShopOpen=!roleplayShopOpen;
                if(roleplayShopOpen){client.ShopNotice="";client.RefreshWallet();roleplayShopScroll=Vector2.zero;}
            }
        }
        void UpdateShopOpeningNotice() {
            shopOpeningNotice.Update(client,Time.unscaledTimeAsDouble);
        }
        void DrawShopOpeningNotice(float sidebarX,float top,float width) {
            if(!shopOpeningNotice.Visible(Time.unscaledTimeAsDouble))return;
            const string message="Cửa hàng đã mở, bạn có thể mua thêm vật phẩm ở cửa hàng";
            if(shopOpeningText==null)shopOpeningText=CartoonUi.Label(16,CartoonUi.Ink);
            float noticeWidth=width-24;
            string text=CartoonUi.WrapText(message,shopOpeningText,noticeWidth-28);
            float height=Mathf.Ceil(shopOpeningText.CalcHeight(new GUIContent(text),noticeWidth-28))+24;
            Rect notice=new Rect(sidebarX+12,top+12,noticeWidth,height);
            CartoonUi.Panel(notice,CartoonUi.Gold,12,3);
            GUI.Label(new Rect(notice.x+14,notice.y+12,notice.width-28,height-24),text,shopOpeningText);
        }
        string ItemScheduleText() {
            int duration=client.ItemMatchTicks,interval=RoleplayItemSchedule.Interval(duration),wave=client.ItemWave;
            uint tick=client.EstimatedSkillTick;
            if(wave>0)return "Shop đóng sau "+Math.Max(0,(int)Math.Ceiling((wave*interval+RoleplayItemSchedule.Window(duration)-(double)tick)/30))+"s";
            int next=(int)(tick/(uint)interval)+1;
            return next*interval<duration?"Vật phẩm sau "+Math.Max(0,(int)Math.Ceiling((next*interval-(double)tick)/30))+"s":"Đã hết các đợt vật phẩm";
        }
        void DrawRoleplayShop() {
            if(client.ItemWave==0){roleplayShopOpen=false;return;}
            InitMenuCards();
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(.03f,.06f,.1f,.8f));
            float width=Mathf.Min(700,viewWidth-40),height=Mathf.Min(690,viewHeight-32);
            Rect card=new Rect((viewWidth-width)/2,(viewHeight-height)/2,width,height);
            menu.Card(card,"SHOP NHẬP VAI · ĐỢT "+client.ItemWave);
            GUILayout.BeginArea(new Rect(card.x+24,card.y+78,card.width-48,card.height-154));
            roleplayShopScroll=GUILayout.BeginScrollView(roleplayShopScroll);
            float contentWidth=MenuScrollContentWidth(card.width-48);
            GUILayout.BeginVertical(GUILayout.Width(contentWidth));
            DrawWalletBadge(GUILayoutUtility.GetRect(210,40,GUILayout.Width(Mathf.Min(210,contentWidth))));
            GUILayout.Label(ItemScheduleText(),menu.Section);
            GUILayout.Label("Mỗi loại mua tối đa 1 lần/đợt. Item được cất vào túi; bấm icon ở sidebar để dùng khi cần.",menu.Hint);
            TankState own;bool alive=client.Latest!=null&&client.Latest.World.Tanks.TryGetValue(client.Slot,out own)&&own.Hp>0;
            if(!alive)GUILayout.Label("Chờ xe hồi sinh để mua vật phẩm.",menu.Hint);
            if(!string.IsNullOrEmpty(client.ShopNotice))GUILayout.Label(client.ShopNotice,menu.Hint);
            if(!string.IsNullOrEmpty(client.WalletError))GUILayout.Label(client.WalletError,menu.Hint);
            for(int type=0;type<GameRules.ItemTypeCount;type++) {
                GUILayout.Space(8);
                float textWidth=contentWidth-156;
                string name=WrapMenuText(ShopItemNames[type],cardName,textWidth),description=WrapMenuText(ShopItemDescriptions[type],cardSmall,textWidth);
                float nameHeight=MenuTextHeight(name,cardName,textWidth),descriptionHeight=MenuTextHeight(description,cardSmall,textWidth);
                Rect row=GUILayoutUtility.GetRect(contentWidth,Mathf.Max(94,28+nameHeight+descriptionHeight),GUILayout.Width(contentWidth));
                CartoonUi.Panel(row,CartoonUi.Cream,10,1);
                GUI.Label(new Rect(row.x+12,row.y+12,textWidth,nameHeight),name,cardName);
                GUI.Label(new Rect(row.x+12,row.y+18+nameHeight,textWidth,descriptionHeight),description,cardSmall);
                GUI.Label(new Rect(row.xMax-132,row.y+8,120,28),GameRules.RoleplayItemPrice+" vàng",cardText);
                bool enabled=GUI.enabled;GUI.enabled=enabled&&client.CanBuyItem(type);
                if(GUI.Button(new Rect(row.xMax-132,row.y+40,120,44),client.ItemBought(type)?"Đã mua":client.ShopPending?"Đang mua…":"Mua",compactAction))client.BuyItem(type);
                GUI.enabled=enabled;
            }
            GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.EndArea();
            if(GUI.Button(new Rect(card.x+24,card.yMax-64,card.width-48,48),"Đóng",menu.Secondary))roleplayShopOpen=false;
        }
    }
    public sealed class RoleplayShopOpeningNotice {
        ulong match;
        int wave;
        double until;
        public bool Visible(double now) {return now<until;}
        public void Update(ClientCore client,double now) {
            if(client.Phase!=ClientPhase.PLAYING||client.RoomMode!=RoomMode.ROLEPLAY) {
                match=0;wave=0;until=0;return;
            }
            if(match!=client.Match) {match=client.Match;wave=0;until=0;}
            int currentWave=client.Latest==null?0:RoleplayItemSchedule.Wave(client.ItemMatchTicks,client.Latest.Tick);
            if(currentWave>wave) {wave=currentWave;until=now+2;}
            else if(currentWave==0)until=0;
        }
    }
}
