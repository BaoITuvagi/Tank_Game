using UnityEngine;

namespace Tank {
    // Presentation shared by the turn-based campaign and real-time multiplayer.
    // The caller owns readiness, units, input and the resulting command.
    public static class BattleHudUi {
        static GUIStyle titleDark,titleLight,stateDark,stateLight,keyStyle,primaryButton,secondaryButton,disabledButton;
        static GUIStyle Text(int size,Color color){
            var style=new GUIStyle{font=CartoonUi.BodyFont,fontSize=size,wordWrap=true,clipping=TextClipping.Clip,padding=new RectOffset()};style.normal.textColor=color;return style;
        }
        static void EnsureText(){
            if(titleDark!=null)return;
            titleDark=Text(15,CartoonUi.Ink);titleLight=Text(15,CartoonUi.Text);
            stateDark=Text(12,CartoonUi.PanelDark);stateLight=Text(12,CartoonUi.Muted);
            keyStyle=Text(12,CartoonUi.Text);keyStyle.alignment=TextAnchor.MiddleCenter;
        }
        static void Ensure(){
            EnsureText();if(primaryButton!=null)return;
            primaryButton=CartoonUi.Button(CartoonUi.Gold,15,0);secondaryButton=CartoonUi.Button(CartoonUi.Ready,15,0);
            disabledButton=CartoonUi.Button(CartoonUi.PanelDark,15,0);
        }
        public static string State(bool enabled,float cooldown,string unit,string readyLabel="Sẵn sàng",string blockedReason=""){
            if(!enabled&&!string.IsNullOrEmpty(blockedReason))return blockedReason;
            if(cooldown>0)return "Còn "+Mathf.CeilToInt(cooldown)+" "+unit;
            return string.IsNullOrEmpty(readyLabel)?"Sẵn sàng":readyLabel;
        }
        public static float ActionHeight(float width,string key,string title,string state){
            EnsureText();float inner=Mathf.Max(1,width-24),keyWidth=KeyWidth(inner,key);
            float top=Mathf.Max(22,titleDark.CalcHeight(new GUIContent(title??""),Mathf.Max(1,inner-34)));
            float bottom=Mathf.Max(22,Mathf.Max(keyStyle.CalcHeight(new GUIContent(key??""),keyWidth-8),stateLight.CalcHeight(new GUIContent(state??""),Mathf.Max(1,inner-keyWidth-8))));
            return Mathf.Max(70,Mathf.Ceil(top+bottom+19));
        }
        static float KeyWidth(float inner,string key){return Mathf.Clamp(keyStyle.CalcSize(new GUIContent(key??"")).x+16,28,Mathf.Max(28,inner*.52f));}
        public static bool Action(Rect rect,UiIcon icon,string key,string title,bool enabled,float cooldown,float cooldownMaximum,string unit,string readyLabel="Sẵn sàng",string blockedReason="",bool primary=false,string description="",bool showCooldownBorder=true){
            Ensure();bool previous=GUI.enabled,available=previous&&enabled;
            // IMGUI buttons accept non-primary mouse events too. Inspection must
            // consume the right press before the action can claim hot control.
            bool secondaryMouse=Event.current.isMouse&&Event.current.button!=0;
            SkillDetailsUi.Inspect(rect,title,description);
            GUI.enabled=available&&!secondaryMouse&&!SkillDetailsUi.IsOpen;GUI.SetNextControlName("BattleAction"+key+title);
            bool pressed=GUI.Button(rect,GUIContent.none,enabled?(primary?primaryButton:secondaryButton):disabledButton);GUI.enabled=previous;
            float inner=Mathf.Max(1,rect.width-24),keyWidth=KeyWidth(inner,key),top=Mathf.Max(22,titleDark.CalcHeight(new GUIContent(title??""),Mathf.Max(1,inner-34)));
            Color ink=enabled?CartoonUi.Ink:CartoonUi.Text;
            CartoonUi.Icon(new Rect(rect.x+12,rect.y+9,23,23),icon,ink);
            GUI.Label(new Rect(rect.x+45,rect.y+7,inner-33,top),title,enabled?titleDark:titleLight);
            float lower=rect.y+8+top,lowerHeight=Mathf.Max(22,rect.yMax-lower-9);
            CartoonUi.Panel(new Rect(rect.x+12,lower,keyWidth,lowerHeight),CartoonUi.PanelDark,6,0);
            GUI.Label(new Rect(rect.x+16,lower,keyWidth-8,lowerHeight),key,keyStyle);
            GUI.Label(new Rect(rect.x+20+keyWidth,lower,inner-keyWidth-8,lowerHeight),State(enabled,cooldown,unit,readyLabel,blockedReason),enabled?stateDark:stateLight);
            if(showCooldownBorder&&cooldown>0)DrawCooldownBorder(rect,CooldownFraction(cooldown,cooldownMaximum));
            return pressed&&available&&!secondaryMouse&&!SkillDetailsUi.IsOpen;
        }
        public static float CooldownFraction(float remaining,float maximum){
            return maximum>0&&!float.IsNaN(remaining)&&!float.IsNaN(maximum)?Mathf.Clamp01(remaining/maximum):0;
        }
        // Clockwise from the upper-left corner. The visible length represents
        // remaining cooldown, so it recedes instead of filling toward readiness.
        public static Rect[] CooldownSegments(Rect rect,float fraction,float thickness=3){
            float width=Mathf.Max(0,rect.width),height=Mathf.Max(0,rect.height),left=(width+height)*2*Mathf.Clamp01(fraction);
            var result=new Rect[4];
            float length=Mathf.Min(left,width);result[0]=new Rect(rect.x,rect.y,length,thickness);left-=length;
            length=Mathf.Min(left,height);result[1]=new Rect(rect.xMax-thickness,rect.y,thickness,length);left-=length;
            length=Mathf.Min(left,width);result[2]=new Rect(rect.xMax-length,rect.yMax-thickness,length,thickness);left-=length;
            length=Mathf.Min(left,height);result[3]=new Rect(rect.x,rect.yMax-length,thickness,length);
            return result;
        }
        static void DrawCooldownBorder(Rect rect,float fraction){
            Color previous=GUI.color;
            foreach(var edge in CooldownSegments(new Rect(rect.x-3,rect.y-3,rect.width+6,rect.height+6),fraction))
                if(edge.width>0&&edge.height>0)ArenaRenderer.Fill(edge,new Color(.28f,.92f,.48f));
            GUI.color=previous;
        }
    }
    // Match the multiplayer arena and reserve all combat actions before optional content.
    public readonly struct CampaignHudLayout {
        public readonly Rect Map,Sidebar,Vitals,Objective,Details,Effects,Fire,Skill,Supply,End;
        public CampaignHudLayout(float width,float height,float fireHeight=70,float skillHeight=70,float supplyHeight=70){
            var arena=ArenaLayout.Fit(width,height,13,13);
            Map=new Rect(arena.X,arena.Y,13*arena.Scale,13*arena.Scale);
            Sidebar=new Rect(arena.SidebarX,ArenaLayout.Top,ArenaLayout.SidebarWidth,arena.Height);
            float x=Sidebar.x+12,w=Sidebar.width-24;
            End=new Rect(x,Sidebar.yMax-52,w,52);
            Supply=new Rect(x,End.y-8-supplyHeight,w,supplyHeight);
            Skill=new Rect(x,Supply.y-8-skillHeight,w,skillHeight);
            Fire=new Rect(x,Skill.y-8-fireHeight,w,fireHeight);
            Effects=new Rect(x,Fire.y-50,w,44);
            Vitals=new Rect(x,Sidebar.y+8,w,28);
            Objective=new Rect(x,Vitals.yMax+6,w,58);
            Details=new Rect(x,Objective.yMax+4,w,Mathf.Max(0,Effects.y-Objective.yMax-10));
        }
    }
}
