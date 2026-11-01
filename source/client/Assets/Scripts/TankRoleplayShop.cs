using System;
using UnityEngine;
namespace Tank {
    public partial class TankApp {
        bool roleplayShopOpen;
        Vector2 roleplayShopScroll;
        readonly RoleplayShopOpeningNotice shopOpeningNotice=new RoleplayShopOpeningNotice();
        GUIStyle shopOpeningText,shopName,shopDescription,shopPrice,shopSymbol,shopBuy;
        static readonly string[] ShopItemNames={"Cứu thương","Giáp","Đạn nổ","Thanh tẩy","Bất tử","Phân tách"};
        static readonly string[] ShopItemDescriptions={"Hồi 30% máu tối đa.","Giảm 35% sát thương trong 6 giây.","Đạn gây nổ lan trong 5 giây.","Gỡ các hiệu ứng bất lợi đang có.","Chặn sát thương trong 3 giây.","3 phát bắn có khả năng tách đạn."};
        static readonly string[] ShopItemSymbols={"+","A","HE","P","I","M"};
        static readonly Color[] ShopItemColors={new Color(.2f,1,.42f),new Color(.7f,.84f,.96f),new Color(1,.42f,.08f),Color.white,new Color(1,.9f,.18f),new Color(.82f,.32f,1)};
        void InitRoleplayShopStyles() {
            if(shopName!=null)return;
            shopName=CartoonUi.Label(19,CartoonUi.Ink);
            shopDescription=CartoonUi.Label(14,CartoonUi.Ink);
            shopPrice=CartoonUi.Label(15,CartoonUi.Ink);shopPrice.alignment=TextAnchor.MiddleRight;
            shopSymbol=CartoonUi.Label(20,CartoonUi.Text);shopSymbol.alignment=TextAnchor.MiddleCenter;
            shopBuy=CartoonUi.Button(CartoonUi.Gold,15,40);shopBuy.padding=new RectOffset(6,6,3,5);
        }
        void UpdateRoleplayShop() {
            UpdateShopOpeningNotice();
            if(settingsOpen||arenaRenderer.Chat.InputBlocked)return;
            if(client.Phase!=ClientPhase.PLAYING||client.RoomMode!=RoomMode.ROLEPLAY||client.ItemWave==0){roleplayShopOpen=false;return;}
            if(!string.IsNullOrEmpty(walletNotice))return;
            if(!GameWindowFocus.IsForeground)return;
            if(roleplayShopOpen&&Input.GetKeyDown(KeyCode.Escape)){roleplayShopOpen=false;return;}
            if(ClientPreferences.Active.Pressed(ControlAction.Shop,!roleplayShopOpen&&PointerInArena())&&!leaveMatchForm&&!profileForm&&!joinForm) {
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
            if(shopOpeningText==null)shopOpeningText=CartoonUi.Label(15,CartoonUi.Ink);
            float noticeWidth=width-24;
            string text=CartoonUi.WrapText(message,shopOpeningText,noticeWidth-28);
            float height=Mathf.Ceil(shopOpeningText.CalcHeight(new GUIContent(text),noticeWidth-28))+24;
            Rect notice=new Rect(sidebarX+12,top+12,noticeWidth,height);
            CartoonUi.Panel(notice,CartoonUi.Gold,5,3);
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
            InitMenuCards();InitRoleplayShopStyles();
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(.035f,.055f,.042f,.84f));
            float width=Mathf.Min(780,viewWidth-40),height=Mathf.Min(760,viewHeight-32);
            Rect card=new Rect((viewWidth-width)/2,(viewHeight-height)/2,width,height);
            menu.Card(card,"TRẠM TIẾP TẾ");
            CartoonUi.Badge(new Rect(card.xMax-126,card.y+26,96,30),"ĐỢT "+client.ItemWave,CartoonUi.Gold);
            GUILayout.BeginArea(new Rect(card.x+24,card.y+82,card.width-48,card.height-158));
            roleplayShopScroll=GUILayout.BeginScrollView(roleplayShopScroll);
            float contentWidth=MenuScrollContentWidth(card.width-48);
            GUILayout.BeginVertical(GUILayout.Width(contentWidth));
            Rect account=GUILayoutUtility.GetRect(contentWidth,48,GUILayout.Width(contentWidth));
            DrawWalletBadge(new Rect(account.x,account.y,210,40));
            GUI.Label(new Rect(account.x+230,account.y+8,contentWidth-230,32),ItemScheduleText(),menu.Text);
            GUILayout.Label("Mỗi loại mua tối đa 1 lần/đợt. Item được cất vào túi; bấm icon ở sidebar để dùng khi cần.",menu.Hint);
            TankState own;bool alive=client.Latest!=null&&client.Latest.World.Tanks.TryGetValue(client.Slot,out own)&&own.Hp>0;
            if(!alive)GUILayout.Label("Chờ xe hồi sinh để mua vật phẩm.",menu.Hint);
            if(!string.IsNullOrEmpty(client.ShopNotice))GUILayout.Label(client.ShopNotice,menu.Hint);
            if(!string.IsNullOrEmpty(client.WalletError))GUILayout.Label(client.WalletError,menu.Hint);
            GUILayout.Space(12);
            for(int type=0;type<GameRules.ItemTypeCount;type++) {
                float textWidth=contentWidth-218;
                string name=WrapMenuText(ShopItemNames[type],shopName,textWidth),description=WrapMenuText(ShopItemDescriptions[type],shopDescription,textWidth);
                float nameHeight=MenuTextHeight(name,shopName,textWidth),descriptionHeight=MenuTextHeight(description,shopDescription,textWidth);
                Rect row=GUILayoutUtility.GetRect(contentWidth,Mathf.Max(88,28+nameHeight+descriptionHeight),GUILayout.Width(contentWidth));
                ArenaRenderer.Fill(row,CartoonUi.Cream);
                ArenaRenderer.Fill(new Rect(row.x+12,row.yMax-1,row.width-24,1),Color.Lerp(CartoonUi.Cream,CartoonUi.Ink,.23f));
                Rect insignia=new Rect(row.x+14,row.y+18,46,46);
                CartoonUi.Panel(insignia,CartoonUi.PanelDark,5);
                ArenaRenderer.Fill(new Rect(insignia.x,insignia.y+4,3,insignia.height-8),ShopItemColors[type]);
                shopSymbol.normal.textColor=ShopItemColors[type];
                GUI.Label(insignia,ShopItemSymbols[type],shopSymbol);
                GUI.Label(new Rect(row.x+76,row.y+12,textWidth,nameHeight),name,shopName);
                GUI.Label(new Rect(row.x+76,row.y+18+nameHeight,textWidth,descriptionHeight),description,shopDescription);
                GUI.Label(new Rect(row.xMax-130,row.y+7,112,24),GameRules.RoleplayItemPrice+" vàng",shopPrice);
                bool enabled=GUI.enabled;GUI.enabled=enabled&&client.CanBuyItem(type);
                string action=client.ItemBought(type)?"Đã mua":client.ShopPending?"Đang mua…":!alive?"Chờ hồi sinh":client.WalletLoaded&&client.WalletGold<GameRules.RoleplayItemPrice?"Thiếu vàng":"Mua";
                if(GUI.Button(new Rect(row.xMax-130,row.y+36,112,40),action,shopBuy))client.BuyItem(type);
                GUI.enabled=enabled;
            }
            GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.EndArea();
            GUI.Label(new Rect(card.x+28,card.yMax-57,card.width-250,28),ClientPreferences.Active.Label(ControlAction.Shop)+" / Esc · Trở lại trận đấu",menu.Hint);
            if(GUI.Button(new Rect(card.xMax-202,card.yMax-64,178,48),"Đóng",menu.Secondary))roleplayShopOpen=false;
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
