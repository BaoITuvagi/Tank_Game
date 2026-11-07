using UnityEngine;
namespace Tank {
    public partial class TankApp {
        Vector2 quickJoinScroll;
        string quickRoomIdText="",quickJoinError="";
        void ApplyQuickRoomId(string text) {
            quickRoomIdText=text;quickJoinError="";
            if(text.Length>0)quickModeChoice=quickMapChoice=0;
        }
        void OpenQuickJoin() {
            quickModeChoice=quickMapChoice=0;quickJoinScroll=Vector2.zero;
            quickRoomIdText=quickJoinError="";
            client.Message="";quickJoinForm=true;GUI.FocusControl(null);
        }
        void DrawQuickJoinDialog() {
            InitMenuCards();
            if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape&&!client.QuickJoinPending) {
                quickJoinForm=false;GUI.FocusControl(null);Event.current.Use();return;
            }
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(0,0,0,.7f));
            float width=Mathf.Min(960,viewWidth-48),height=Mathf.Min(780,viewHeight-64);
            Rect card=new Rect((viewWidth-width)/2,(viewHeight-height)/2,width,height);
            menu.Card(card,"Vào trận nhanh");
            GUILayout.BeginArea(new Rect(card.x+24,card.y+78,width-48,height-154));
            quickJoinScroll=GUILayout.BeginScrollView(quickJoinScroll);
            float contentWidth=MenuScrollContentWidth(width-48);
            GUILayout.BeginVertical(GUILayout.Width(contentWidth));
            GUILayout.Label("Nhập ID để vào đúng phòng, hoặc để trống để tìm ngẫu nhiên theo chế độ và bản đồ.",menu.Hint);
            GUILayout.Space(10);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&!client.QuickJoinPending;
            string idText=Field("quick-room-id","ID phòng (không bắt buộc)",quickRoomIdText,10);
            if(idText!=quickRoomIdText)ApplyQuickRoomId(idText);
            bool byId=quickRoomIdText.Length>0;
            if(byId)GUILayout.Label("Đang tìm theo ID · Chế độ và bản đồ được khóa ở Bất kỳ.",menu.Hint);
            GUILayout.Space(10);
            Color tint=GUI.color;GUI.enabled=enabled&&!client.QuickJoinPending&&!byId;
            if(byId)GUI.color=new Color(tint.r,tint.g,tint.b,tint.a*.45f);
            GUILayout.Label("Chế độ",menu.Name);
            quickModeChoice=GUILayout.SelectionGrid(quickModeChoice,new[]{"Bất kỳ","Thường","Nhập vai"},3,menu.Secondary);
            GUILayout.Space(10);
            GUILayout.Label("Bản đồ",menu.Name);
            if(GUILayout.Toggle(quickMapChoice==0,"Bất kỳ bản đồ",menu.Secondary))quickMapChoice=0;
            GUILayout.Space(8);
            var maps=MapCatalog.Entries;
            int selectedId=quickMapChoice==0?0:maps[quickMapChoice-1].id;
            int chosen=DrawMapChoiceCards(contentWidth,selectedId);
            for(int i=0;i<maps.Count;i++)if(maps[i].id==chosen)quickMapChoice=i+1;
            GUI.color=tint;
            GUILayout.Space(14);
            GUI.enabled=enabled;
            if(!string.IsNullOrEmpty(quickJoinError))GUILayout.Label(quickJoinError,menu.Hint);
            if(!string.IsNullOrEmpty(client.Message))GUILayout.Label(client.Message,menu.Hint);
            GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(card.x+24,card.yMax-62,width-48,46));GUILayout.BeginHorizontal();
            GUI.enabled=enabled&&!client.QuickJoinPending;
            if(GUILayout.Button("Hủy",menu.Secondary,GUILayout.Height(42))){quickJoinForm=false;GUI.FocusControl(null);}
            GUI.enabled=enabled&&CanEnterMultiplayer&&!client.QuickJoinPending;
            if(GUILayout.Button(client.QuickJoinPending?"Đang tìm…":"Tìm phòng",menu.Primary,GUILayout.Height(42))) {
                uint roomId=0;quickJoinError="";
                if(byId&&(!uint.TryParse(quickRoomIdText.Trim(),out roomId)||roomId==0||roomId>int.MaxValue))quickJoinError="ID phòng phải là số nguyên từ 1 đến 2147483647.";
                int mode=quickModeChoice==0?255:quickModeChoice-1;
                int mapId=quickMapChoice==0?0:maps[quickMapChoice-1].id;
                if(quickJoinError=="")client.QuickJoin(mode,mapId,roomId);
            }
            GUI.enabled=enabled;GUILayout.EndHorizontal();GUILayout.EndArea();
        }
    }
}
