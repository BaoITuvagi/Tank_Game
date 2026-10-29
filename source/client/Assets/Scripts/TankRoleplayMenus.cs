using System;
using UnityEngine;
namespace Tank {
    public partial class TankApp {
        int roomModeChoice;
        Vector2 roleplayInfoScroll;
        GUIStyle nationCard,nationCardSelected;
        struct NationCardLayout {
            public string Name,Status,Skill;
            public float NameHeight,StatusHeight,TopHeight,SkillHeight,Height;
        }
        NationCardLayout MeasureNationCard(int nation,bool selected,bool taken,float width) {
            var layout=new NationCardLayout {
                Name=WrapMenuText(RoleplayContent.NationNames[nation],depotName,width-82),
                Status=selected?"ĐANG CHỌN":taken?"ĐÃ CÓ NGƯỜI":"",
                Skill=WrapMenuText("Q · "+RoleplayContent.QNames[nation],depotSmall,width-24)
            };
            layout.NameHeight=MenuTextHeight(layout.Name,depotName,width-82);
            layout.Status=WrapMenuText(layout.Status,depotKicker,width-82);
            layout.StatusHeight=layout.Status.Length==0?0:MenuTextHeight(layout.Status,depotKicker,width-82);
            layout.TopHeight=Mathf.Max(64,layout.NameHeight+4+layout.StatusHeight);
            layout.SkillHeight=MenuTextHeight(layout.Skill,depotSmall,width-24);
            layout.Height=10+layout.TopHeight+6+layout.SkillHeight+12;
            return layout;
        }
        void NationSelector(float width,int columns=2,bool compact=false) {
            var local=client.Players.Find(p=>p.Slot==client.Slot);
            if(local==null)return;
            InitMenuCards();
            columns=Mathf.Clamp(Mathf.FloorToInt((width+8)/210),1,columns);
            if(nationCard==null) {
                nationCard=new GUIStyle(CartoonUi.Button(CartoonUi.PanelColor,14,108)){margin=new RectOffset(0,0,0,0),padding=new RectOffset()};
                nationCardSelected=new GUIStyle(nationCard);
                foreach(var state in new[]{nationCardSelected.normal,nationCardSelected.hover,nationCardSelected.active,nationCardSelected.focused})
                    state.background=CartoonUi.SurfaceStyle(CartoonUi.PanelDark,6,0,true).normal.background;
            }
            if(!compact)GUILayout.Label(RoleplayContent.Story,menu.Text);
            bool waiting=client.RoomPhase==RoomPhase.WAITING;
            GUILayout.Label(!waiting?"Đã chốt phe · Đang chuẩn bị":local.Nation<0?"Chọn phe để sẵn sàng":local.Ready?"Đã sẵn sàng · Phe được khóa":"Đang chọn · "+RoleplayContent.NationNames[local.Nation],menu.Hint);
            bool editable=waiting&&!local.Ready;
            for(int row=0;row<(8+columns-1)/columns;row++) {
                float cellWidth=(width-(columns-1)*8)/columns,rowHeight=0;
                var cards=new NationCardLayout[columns];
                for(int column=0;column<columns&&row*columns+column<8;column++) {
                    int nation=row*columns+column;
                    bool taken=client.Players.Exists(p=>p.Slot!=local.Slot&&p.Nation==nation);
                    cards[column]=MeasureNationCard(nation,local.Nation==nation,taken,cellWidth);
                    rowHeight=Mathf.Max(rowHeight,cards[column].Height);
                }
                GUILayout.BeginHorizontal();
                for(int column=0;column<columns;column++) {
                    int nation=row*columns+column;
                    if(nation>=8)break;
                    bool taken=client.Players.Exists(p=>p.Slot!=local.Slot&&p.Nation==nation),enabled=GUI.enabled;
                    GUI.enabled=enabled&&editable&&!taken;
                    Rect choice=GUILayoutUtility.GetRect(cellWidth,rowHeight,GUILayout.Width(cellWidth),GUILayout.Height(rowHeight));
                    bool selected=local.Nation==nation;
                    if(GUI.Button(choice,"",selected?nationCardSelected:nationCard))client.SelectNation(nation);
                    GUI.enabled=enabled;
                    Color prior=GUI.color;if(taken)GUI.color=new Color(.68f,.72f,.76f,1);
                    TankBay(new Rect(choice.x+8,choice.y+8,56,58),nation,selected?CartoonUi.Gold:CartoonUi.Ready);
                    var layout=cards[column];
                    GUI.Label(new Rect(choice.x+72,choice.y+10,choice.width-82,layout.NameHeight),layout.Name,depotName);
                    if(layout.StatusHeight>0)GUI.Label(new Rect(choice.x+72,choice.y+14+layout.NameHeight,choice.width-82,layout.StatusHeight),layout.Status,depotKicker);
                    DepotRule(new Rect(choice.x+12,choice.y+10+layout.TopHeight,choice.width-24,2),selected?CartoonUi.Gold:CartoonUi.Outline);
                    GUI.Label(new Rect(choice.x+12,choice.y+16+layout.TopHeight,choice.width-24,layout.SkillHeight),layout.Skill,depotSmall);
                    GUI.color=prior;
                    if(column<columns-1)GUILayout.Space(8);
                }
                GUILayout.EndHorizontal();
                if((row+1)*columns<8)GUILayout.Space(8);
            }
            if(!compact&&local.Nation>=0)GUILayout.Label("Q · "+RoleplayContent.QNames[local.Nation]+"\n"+RoleplayContent.NationDescriptions[local.Nation],menu.Text);
            if(!compact&&local.Ready)GUILayout.Label("Hủy sẵn sàng trước khi đổi phe.",menu.Hint);
        }
        string PreparationLabel() {
            string name=client.PrepareStage==PrepStage.LOADING?"Đang tải và đồng bộ mạng":client.PrepareStage==PrepStage.SYNC_LOADOUT?"Đang xác nhận bộ kỹ năng":"Sắp bắt đầu";
            if(client.PingSamples>0&&client.PrepareDeadline>0)name+=" · "+Math.Max(0,Math.Ceiling((client.PrepareDeadline-ClientCore.NowUs-client.OffsetUs)/1e6))+"s";
            return name;
        }
    }
}
