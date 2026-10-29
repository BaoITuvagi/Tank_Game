using System.Globalization;
using UnityEngine;
namespace Tank {
    public partial class TankApp {
        string walletNotice="";
        const string WalletPurpose="Vàng dùng để mua vật phẩm trong cửa hàng ở chế độ nhập vai";
        const string WalletEarn="Vàng có thể được tìm thêm khi tham gia chế độ chiến trường. Vàng còn lại sau mỗi chuyến đi sẽ được cộng dồn vào tài khoản";
        GUIStyle walletNumber,walletPlus;
        void SetupShopUiPreview() {
            var args=System.Environment.GetCommandLineArgs();
            if(IsMenuPreview&&client.Phase>=ClientPhase.LOBBY)client.SetWalletPreview(19372,new int[GameRules.ItemTypeCount]);
            if(ArenaPreview.Enabled||IsMenuPreview) {
                if(System.Array.IndexOf(args,"--preview-wallet-info")>=0)walletNotice=WalletPurpose;
                if(System.Array.IndexOf(args,"--preview-wallet-add")>=0)walletNotice=WalletEarn;
            }
            if(!ArenaPreview.Enabled||System.Array.IndexOf(args,"--preview-shop-items")<0)return;
            client.Match=1;client.Latest.Tick=1260;client.Latest.Remaining=4140;
            client.SetWalletPreview(19372,new[]{1,2,1,1,1,1});
            if(System.Array.IndexOf(args,"--preview-open-shop")>=0)roleplayShopOpen=true;
        }
        void DrawWalletBadge(Rect rect) {
            if(walletNumber==null) {
                walletNumber=CartoonUi.Label(20,CartoonUi.Gold);
                walletNumber.alignment=TextAnchor.MiddleLeft;
                walletPlus=CartoonUi.Button(CartoonUi.Gold,24,0);
            }
            Rect plus=new Rect(rect.xMax-38,rect.y+3,35,rect.height-6);
            Rect info=new Rect(rect.x+3,rect.y+3,rect.width-44,rect.height-6);
            bool hovered=GUI.enabled&&info.Contains(Event.current.mousePosition);
            CartoonUi.Panel(rect,hovered?Color.Lerp(CartoonUi.PanelDark,CartoonUi.PanelColor,.55f):CartoonUi.PanelDark,5,1);
            if(GUI.Button(info,GUIContent.none,GUIStyle.none))walletNotice=WalletPurpose;
            Rect coin=new Rect(rect.x+9,rect.center.y-12,24,24);
            CartoonUi.Panel(coin,new Color(.82f,.47f,.1f),12,0);
            CartoonUi.Panel(new Rect(coin.x+2,coin.y+2,20,20),CartoonUi.Gold,10,0);
            ArenaRenderer.Fill(new Rect(coin.center.x-1.5f,coin.y+6,3,12),new Color(.9f,.57f,.1f));
            string amount=client.WalletLoaded?client.WalletGold.ToString("N0",CultureInfo.InvariantCulture):"…";
            Rect number=new Rect(rect.x+38,rect.y+3,rect.width-82,rect.height-6);
            walletNumber.fontSize=20;
            while(walletNumber.fontSize>12&&walletNumber.CalcSize(new GUIContent(amount)).x>number.width)walletNumber.fontSize--;
            GUI.Label(number,amount,walletNumber);
            if(GUI.Button(plus,GUIContent.none,walletPlus))walletNotice=WalletEarn;
            CartoonUi.Icon(new Rect(plus.center.x-9,plus.center.y-9,18,18),UiIcon.Health,CartoonUi.Ink);
        }
        void DrawWalletNotice() {
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(.035f,.055f,.042f,.84f));
            float width=Mathf.Min(520,viewWidth-40);
            string text=WrapMenuText(walletNotice,menu.Text,width-48);
            float body=MenuTextHeight(text,menu.Text,width-48),height=Mathf.Min(viewHeight-32,Mathf.Max(250,body+170));
            Rect card=new Rect((viewWidth-width)/2,(viewHeight-height)/2,width,height);
            menu.Card(card,"VÀNG");
            GUI.Label(new Rect(card.x+24,card.y+82,width-48,body),text,menu.Text);
            if(GUI.Button(new Rect(card.xMax-202,card.yMax-64,178,48),"Đóng",menu.Secondary))walletNotice="";
            if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){walletNotice="";Event.current.Use();}
        }
    }
}
