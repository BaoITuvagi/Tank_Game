using System;
using UnityEngine;
namespace Tank {
    public partial class TankApp {
        int roomModeChoice;
        Vector2 roleplayInfoScroll;
        void NationSelector() {
            var local=client.Players.Find(p=>p.Slot==client.Slot);
            if(local==null)return;
            GUILayout.Space(10);
            GUILayout.Label(RoleplayContent.Story,menu.Text);
            GUILayout.Label(local.Nation<0?"Chọn quốc gia để sẵn sàng":RoleplayContent.NationNames[local.Nation],menu.Name);
            bool editable=client.RoomPhase==RoomPhase.WAITING&&!local.Ready;
            if(client.RoomPhase==RoomPhase.WAITING)for(int row=0;row<4;row++) {
                GUILayout.BeginHorizontal();
                for(int column=0;column<2;column++) {
                    int nation=row*2+column;
                    bool taken=client.Players.Exists(p=>p.Slot!=local.Slot&&p.Nation==nation),enabled=GUI.enabled;
                    GUI.enabled=enabled&&editable&&!taken;
                    string label=(local.Nation==nation?"✓ ":"")+RoleplayContent.NationNames[nation];
                    if(GUILayout.Button(new GUIContent(label,RoleplayContent.QNames[nation]+": "+RoleplayContent.NationDescriptions[nation]),menu.Secondary))client.SelectNation(nation);
                    GUI.enabled=enabled;
                }
                GUILayout.EndHorizontal();
            }
            if(local.Nation>=0)GUILayout.Label("Q · "+RoleplayContent.QNames[local.Nation]+"\n"+RoleplayContent.NationDescriptions[local.Nation],menu.Text);
            if(local.Ready)GUILayout.Label("Hủy sẵn sàng trước khi đổi quốc gia.",menu.Hint);
        }
        string PreparationLabel() {
            string name=client.PrepareStage==PrepStage.LOADING?"Đang tải và đồng bộ mạng":client.PrepareStage==PrepStage.SYNC_LOADOUT?"Đang xác nhận bộ kỹ năng":"Sắp bắt đầu";
            if(client.PingSamples>0&&client.PrepareDeadline>0)name+=" · "+Math.Max(0,Math.Ceiling((client.PrepareDeadline-ClientCore.NowUs-client.OffsetUs)/1e6))+"s";
            return name;
        }
    }
}
