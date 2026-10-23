using UnityEngine;
using System.Collections.Generic;

namespace Tank.Campaign {
    // All styles and textures belong to campaign; never mutates the shared menu skin.
    public static class CampaignUI {
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        public static GUIStyle Header,Title,Text,Hint,Light,SmallLight,Card,Dark,Slot,Banner,Primary,Quiet,Selected,Disabled,Focus;
        public static RectOffset PanelBorder {get{return new RectOffset(6,5,6,5);}}
        public static Texture2D Texture(string name){
            Texture2D result;if(!textures.TryGetValue(name,out result)){result=Resources.Load<Texture2D>("CampaignUI/"+name);textures[name]=result;}
            return result;
        }
        static GUIStyle Label(int size,Color color,bool bold=false){var s=new GUIStyle(GUI.skin.label){fontSize=size,wordWrap=true,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,padding=new RectOffset(0,0,0,0)};s.normal.textColor=color;return s;}
        static GUIStyle Box(string name,int border){return new GUIStyle(GUI.skin.box){normal={background=Texture(name)},border=new RectOffset(border,border,border,border),padding=new RectOffset(16,16,16,16)};}
        public static void Ensure(){
            if(Header!=null)return;
            Color ink=new Color(.20f,.23f,.18f),muted=new Color(.39f,.39f,.29f),cream=new Color(1,.95f,.81f);
            Header=Label(28,cream,true);Title=Label(22,ink,true);Text=Label(16,ink);Hint=Label(14,muted);Light=Label(17,cream,true);SmallLight=Label(13,cream);
            Card=Box("panel",5);Card.border=PanelBorder;Dark=Box("background_dark",4);Slot=Box("slot",4);Banner=Box("sprout_dialog",12);if(Texture("sprout_dialog")==null)Banner=Card;
            Primary=new GUIStyle(GUI.skin.button){fontSize=16,fontStyle=FontStyle.Bold,wordWrap=true,fixedHeight=0,fixedWidth=0,border=new RectOffset(4,4,3,3),padding=new RectOffset(12,12,6,6),alignment=TextAnchor.MiddleCenter};
            Primary.normal.background=Texture("button");Primary.hover.background=Texture("button_hover");Primary.active.background=Texture("button_pressed");Primary.focused.background=Texture("button_hover");
            Primary.normal.textColor=Primary.hover.textColor=Primary.focused.textColor=ink;Primary.active.textColor=cream;
            Quiet=new GUIStyle(Primary);Quiet.normal.background=Texture("background_dark");Quiet.normal.textColor=cream;
            Selected=new GUIStyle(Primary);Selected.normal.background=Texture("panel");Selected.border=PanelBorder;
            Disabled=new GUIStyle(Primary);Disabled.normal.background=Texture("button_disabled");Disabled.normal.textColor=muted;
            Focus=Box("focus",3);
        }
        public static void Background(Rect rect){Ensure();GUI.Box(rect,GUIContent.none,Dark);}
        public static void Panel(Rect rect,bool selected=false){Ensure();GUI.Box(rect,GUIContent.none,Card);if(selected)GUI.Box(rect,GUIContent.none,Focus);}
        public static void Notice(Rect rect,string text){Ensure();GUI.Box(rect,GUIContent.none,Banner);GUI.Label(new Rect(rect.x+18,rect.y+10,rect.width-36,rect.height-16),text,Hint);}
        public static bool Button(Rect rect,string label,bool enabled=true,bool quiet=false,bool selected=false,string tooltip=""){
            Ensure();bool previous=GUI.enabled;GUI.enabled=previous&&enabled;
            bool pressed=GUI.Button(rect,new GUIContent(label,tooltip),!enabled?Disabled:selected?Selected:quiet?Quiet:Primary);GUI.enabled=previous;return pressed;
        }
        public static void Meter(Rect rect,float fraction,Color color){
            Ensure();GUI.Box(rect,GUIContent.none,Dark);fraction=Mathf.Clamp01(fraction);if(fraction<=0)return;
            Color previous=GUI.color;GUI.color=color;GUI.DrawTextureWithTexCoords(new Rect(rect.x+2,rect.y+2,(rect.width-4)*fraction,Mathf.Max(1,rect.height-4)),Texture("panel"),new Rect(6f/16,5f/16,5f/16,5f/16));GUI.color=previous;
        }
    }
}
