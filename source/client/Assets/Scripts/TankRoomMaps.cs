using UnityEngine;
namespace Tank {
    /// <summary>Layout shared by map selection and its bounded UI checks.</summary>
    public static class RoomMapUiGeometry {
        public const float Gap=12,CardHeight=232;
        public static int Columns(float width,int count) {return width>=600?Mathf.Min(3,count):1;}
        public static float Height(float width,int count) {
            if(count<=0)return 0;
            int columns=Columns(width,count),rows=(count+columns-1)/columns;
            return rows*CardHeight+(rows-1)*Gap;
        }
        public static Rect Card(Rect bounds,int index,int count) {
            int columns=Columns(bounds.width,count);
            float width=(bounds.width-(columns-1)*Gap)/columns;
            return new Rect(bounds.x+index%columns*(width+Gap),bounds.y+index/columns*(CardHeight+Gap),width,CardHeight);
        }
        public static Rect Thumbnail(Rect bounds,float mapWidth,float mapHeight) {
            if(mapWidth<=0||mapHeight<=0||bounds.width<=0||bounds.height<=0)return new Rect(bounds.center.x,bounds.center.y,0,0);
            float scale=Mathf.Min(bounds.width/mapWidth,bounds.height/mapHeight);
            return new Rect(bounds.center.x-mapWidth*scale/2,bounds.center.y-mapHeight*scale/2,mapWidth*scale,mapHeight*scale);
        }
    }
    public partial class TankApp {
        internal int SelectedRoomMapId {get {return roomMapId;}}
        readonly ArenaRenderer roomMapRenderer=new ArenaRenderer();
        GUIStyle roomMapName,roomMapDescription,roomMapState;
        void InitRoomMapStyles() {
            if(roomMapName!=null)return;
            roomMapName=CartoonUi.Label(18,CartoonUi.Text);
            roomMapDescription=CartoonUi.Label(14,CartoonUi.Muted);
            roomMapState=CartoonUi.Label(13,CartoonUi.Gold);
        }
        void DrawRoomMapChoices(float width,bool fixedMap) {
            InitRoomMapStyles();
            GUILayout.Label(fixedMap?"BẢN ĐỒ ĐÃ CHỌN":"CHỌN BẢN ĐỒ",menu.Section);
            GUILayout.Label(fixedMap?"Bản đồ được giữ cố định từ khi tạo phòng.":"Áp dụng cho cả Đấu thường và Nhập vai.",menu.Hint);
            GUILayout.Space(8);
            if(fixedMap) {DrawRoomMapSummary(roomMapId,width);return;}
            roomMapId=DrawMapChoiceCards(width,roomMapId,true);
        }
        int DrawMapChoiceCards(float width,int selectedId,bool showDefault=false) {
            InitRoomMapStyles();
            int count=MapCatalog.Entries.Count;
            Rect bounds=GUILayoutUtility.GetRect(width,RoomMapUiGeometry.Height(width,count),GUILayout.Width(width));
            for(int i=0;i<count;i++) {
                var entry=MapCatalog.Entries[i];
                Rect card=RoomMapUiGeometry.Card(bounds,i,count);
                bool selected=entry.id==selectedId;
                if(GUI.Button(card,"",plainCardButton)){selectedId=entry.id;GUI.FocusControl(null);}
                if(Event.current.type==EventType.Repaint) {
                    CartoonUi.Panel(card,selected?CartoonUi.PanelColor:CartoonUi.Backdrop,6,0);
                    ArenaRenderer.Fill(new Rect(card.x+8,card.y+1,card.width-16,3),selected?CartoonUi.Gold:CartoonUi.Outline);
                }
                DrawRoomMapThumbnail(new Rect(card.x+14,card.y+14,card.width-28,112),entry.Map);
                GUI.Label(new Rect(card.x+12,card.y+137,card.width-24,26),entry.name,roomMapName);
                GUI.Label(new Rect(card.x+12,card.y+166,card.width-24,36),entry.description,roomMapDescription);
                string state=selected?"ĐÃ CHỌN":showDefault&&entry.id==MapCatalog.DefaultId?"MẶC ĐỊNH":"CHỌN BẢN ĐỒ";
                if(selected&&showDefault&&entry.id==MapCatalog.DefaultId)state+=" · MẶC ĐỊNH";
                GUI.Label(new Rect(card.x+12,card.yMax-23,card.width-24,21),state,roomMapState);
            }
            return selectedId;
        }
        void DrawRoomMapSummary(int id,float width) {
            InitRoomMapStyles();
            var entry=MapCatalog.Find(id);
            GUILayout.Label(MapCatalog.Name(id),menu.Name);
            if(entry==null)return;
            Rect bounds=GUILayoutUtility.GetRect(width,Mathf.Min(132,width*.6f),GUILayout.Width(width));
            DrawRoomMapThumbnail(new Rect(bounds.x+8,bounds.y+8,bounds.width-16,bounds.height-16),entry.Map);
        }
        void DrawRoomMapThumbnail(Rect bounds,GameMap previewMap) {
            if(previewMap==null)return;
            Rect mapRect=RoomMapUiGeometry.Thumbnail(bounds,previewMap.width,previewMap.height);
            // Draw the real terrain renderer, including water and destructible cover.
            // No simulation, map mutation or synthetic illustration is involved.
            roomMapRenderer.Draw(previewMap,null,null,0,-1,mapRect.x,mapRect.y,mapRect.width/previewMap.width);
        }
    }
}
