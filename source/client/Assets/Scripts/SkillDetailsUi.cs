using UnityEngine;

namespace Tank {
    // Explicit inspection only. The anchor stays with the skill, never the mouse.
    public static class SkillDetailsUi {
        static string title="",description="",context="";
        static Rect anchor,card;
        static Vector2 scroll;
        static bool releaseInput;
        static GUIStyle heading,body,close;
        public static bool IsOpen {get;private set;}
        public static bool InputBlocked {get{return IsOpen||releaseInput;}}
        public static string Title {get{return title;}}
        public static void Sync(string value){
            if(context==value)return;
            context=value;Close();
        }
        public static void BeforeInput(bool foreground){
            BeforeInput(foreground,!ControlsReleased(),Input.GetKeyDown(KeyCode.Escape));
        }
        public static void BeforeInput(bool foreground,bool controlsHeld,bool escapePressed){
            if(!foreground){if(IsOpen)Close();return;}
            if(IsOpen&&escapePressed)Close();
            if(releaseInput&&!controlsHeld)releaseInput=false;
        }
        static bool ControlsReleased(){
            foreach(var binding in ClientPreferences.Active.Bindings)
                if(ClientPreferences.Active.Held(binding.Action))return false;
            return !Input.GetMouseButton(0)&&!Input.GetMouseButton(1)&&!Input.GetKey(KeyCode.Escape)&&!Input.GetKey(KeyCode.Return)&&!Input.GetKey(KeyCode.KeypadEnter);
        }
        public static void Open(Rect source,string name,string text){
            if(string.IsNullOrEmpty(text))return;
            anchor=source;title=name??"";description=text;scroll=Vector2.zero;IsOpen=true;releaseInput=true;
        }
        public static void Close(){
            if(IsOpen)releaseInput=true;
            IsOpen=false;
        }
        public static void Reset(){IsOpen=releaseInput=false;title=description=context="";scroll=Vector2.zero;}
        public static bool Inspect(Rect source,string name,string text){
            if(string.IsNullOrEmpty(text))return false;
            if(IsOpen&&title==name)anchor=source;
            var e=Event.current;
            if(!GUI.enabled||e.type!=EventType.MouseDown||e.button!=1||!source.Contains(e.mousePosition))return false;
            if(IsOpen&&title==name)Close();else Open(source,name,text);
            GUI.FocusControl(null);
            e.Use();return true;
        }
        static void Styles(){
            if(body!=null)return;
            heading=CartoonUi.Label(21,CartoonUi.Text,true);
            body=CartoonUi.Label(16,CartoonUi.Text);body.richText=false;
            close=CartoonUi.Button(CartoonUi.Cream,15,38);
        }
        public static Rect Place(Rect source,float width,float height,float screenWidth,float screenHeight){
            width=Mathf.Min(width,Mathf.Max(1,screenWidth-32));height=Mathf.Min(height,Mathf.Max(1,screenHeight-32));
            float left=source.x-width-12;
            if(left<16)left=source.xMax+12;
            left=Mathf.Clamp(left,16,Mathf.Max(16,screenWidth-width-16));
            float top=Mathf.Clamp(source.center.y-height/2,16,Mathf.Max(16,screenHeight-height-16));
            return new Rect(left,top,width,height);
        }
        public static void Draw(float width,float height){
            if(!IsOpen)return;
            Styles();bool enabled=GUI.enabled;GUI.enabled=true;
            float contentWidth=Mathf.Min(360,width-64),titleHeight=Mathf.Max(30,heading.CalcHeight(new GUIContent(title),contentWidth));
            float textHeight=body.CalcHeight(new GUIContent(description),contentWidth-18);
            float desiredHeight=titleHeight+textHeight+104;
            card=Place(anchor,contentWidth+40,desiredHeight,width,height);
            var e=Event.current;
            if(e.type==EventType.MouseDown&&(!card.Contains(e.mousePosition)||e.button==1&&anchor.Contains(e.mousePosition))){Close();e.Use();GUI.enabled=enabled;return;}
            CartoonUi.Panel(new Rect(0,0,width,height),new Color(0,0,0,.18f),0,0);
            CartoonUi.Panel(card,CartoonUi.PanelDark,8,0);
            ArenaRenderer.Fill(new Rect(card.x+16,card.y,card.width-32,3),CartoonUi.Ready);
            GUI.Label(new Rect(card.x+20,card.y+15,card.width-40,titleHeight),title,heading);
            Rect viewport=new Rect(card.x+20,card.y+25+titleHeight,card.width-40,Mathf.Max(20,card.height-titleHeight-100));
            scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,viewport.width-18,Mathf.Max(viewport.height,textHeight)));
            GUI.Label(new Rect(0,0,viewport.width-18,textHeight),description,body);
            GUI.EndScrollView();
            if(GUI.Button(new Rect(card.x+20,card.yMax-54,card.width-40,38),"Đóng",close)){Close();GUI.FocusControl(null);}
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){Close();e.Use();}
            GUI.enabled=enabled;
        }
    }
}
