using UnityEngine;
using System.Collections.Generic;

namespace Tank.Campaign {
    // All styles and textures belong to campaign; never mutates the shared menu skin.
    public static class CampaignUI {
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        public static GUIStyle Header,Title,Text,Hint,Light,SmallLight,Card,Dark,Slot,Banner,Primary,Quiet,Selected,Disabled,Focus,RankingRow,RankingText,RankingName,DialogueTitle,DialogueText;
        public static RectOffset PanelBorder {get{return new RectOffset(6,5,6,5);}}
        public static Texture2D Texture(string name){
            Texture2D result;if(!textures.TryGetValue(name,out result)){result=Resources.Load<Texture2D>("CampaignUI/"+name);textures[name]=result;}
            return result;
        }
        // Original Sprout sheet: 26x19 frames on a 48x32 grid. Crop at runtime
        // so UI states retain the original pixels and the complete sheet stays intact.
        static Texture2D ButtonFrame(int row,bool pressed=false){
            string key="sprout_button_"+row+"_"+pressed;Texture2D frame;
            if(textures.TryGetValue(key,out frame))return frame;
            var sheet=Texture("sprout_buttons");if(sheet==null)return Texture("button");
            frame=new Texture2D(26,19,TextureFormat.RGBA32,false){name=key,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            frame.SetPixels(sheet.GetPixels(pressed?59:11,sheet.height-(row*32+6)-19,26,19));
            frame.Apply(false,true);textures[key]=frame;return frame;
        }
        static Texture2D SheetFrame(string name,int x,int top,int width,int height){
            string key=name+"_"+x+"_"+top;Texture2D frame;if(textures.TryGetValue(key,out frame))return frame;
            var sheet=Texture(name);if(sheet==null)return null;
            frame=new Texture2D(width,height,TextureFormat.RGBA32,false){name=key,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            frame.SetPixels(sheet.GetPixels(x,sheet.height-top-height,width,height));frame.Apply(false,true);textures[key]=frame;return frame;
        }
        static GUIStyle Label(int size,Color color,bool bold=false){var s=new GUIStyle(GUI.skin.label){fontSize=size,wordWrap=true,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,padding=new RectOffset(0,0,0,0)};s.normal.textColor=color;return s;}
        static GUIStyle Box(string name,int border){return new GUIStyle(GUI.skin.box){normal={background=Texture(name)},border=new RectOffset(border,border,border,border),padding=new RectOffset(16,16,16,16)};}
        public static void Ensure(){
            if(Header!=null)return;
            Color ink=new Color(.20f,.23f,.18f),muted=new Color(.39f,.39f,.29f),cream=new Color(1,.95f,.81f);
            Header=Label(28,cream,true);Title=Label(22,ink,true);Text=Label(16,ink);Hint=Label(14,muted);Light=Label(17,cream,true);SmallLight=Label(13,cream);
            Card=Box("panel",5);Card.border=PanelBorder;Dark=Box("background_dark",4);Slot=Box("slot",4);Banner=Box("sprout_dialog",12);if(Texture("sprout_dialog")==null)Banner=Card;
            Primary=new GUIStyle(GUI.skin.button){fontSize=17,fontStyle=FontStyle.Bold,wordWrap=true,fixedHeight=0,fixedWidth=0,border=new RectOffset(6,6,6,6),padding=new RectOffset(12,12,6,6),alignment=TextAnchor.MiddleCenter};
            Primary.normal.background=ButtonFrame(2);Primary.hover.background=Primary.focused.background=ButtonFrame(1);Primary.active.background=ButtonFrame(2,true);
            Primary.normal.textColor=Primary.hover.textColor=Primary.focused.textColor=ink;Primary.active.textColor=ink;
            Quiet=new GUIStyle(Primary);Quiet.normal.background=ButtonFrame(0);Quiet.active.background=ButtonFrame(0,true);Quiet.normal.textColor=ink;
            Selected=new GUIStyle(Primary);Selected.normal.background=ButtonFrame(1);
            Disabled=new GUIStyle(Primary);Disabled.normal.background=ButtonFrame(3,true);Disabled.normal.textColor=muted;
            Focus=Box("focus",3);
            RankingRow=Box("panel",6);RankingRow.normal.background=SheetFrame("sprout_rank",3,2,90,27);RankingRow.border=new RectOffset(7,7,7,7);
            RankingText=Label(16,new Color(.20f,.16f,.10f));RankingText.alignment=TextAnchor.MiddleLeft;
            RankingName=new GUIStyle(RankingText){fontStyle=FontStyle.Bold,fontSize=18,wordWrap=false,clipping=TextClipping.Clip};
            DialogueTitle=Label(16,ink,true);DialogueText=Label(14,ink);

        }
        // Unity can keep a larger back buffer after maximizing a window. Use the
        // visible client area for campaign layout so the footer stays above the taskbar.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct WindowRect { public int left,top,right,bottom; }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct WindowPoint { public int x,y; }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct MonitorInfo { public int size;public WindowRect monitor,work;public uint flags; }
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern System.IntPtr GetActiveWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool GetClientRect(System.IntPtr window,out WindowRect rect);
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool ClientToScreen(System.IntPtr window,ref WindowPoint point);
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern System.IntPtr MonitorFromWindow(System.IntPtr window,uint flags);
        [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Auto)]static extern bool GetMonitorInfo(System.IntPtr monitor,ref MonitorInfo info);
#endif
        public static Vector2 VisibleSize(){
            Vector2 size=new Vector2(Screen.width,Screen.height);
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if(!Screen.fullScreen){
                var window=GetActiveWindow();WindowRect client;
                if(window!=System.IntPtr.Zero&&GetClientRect(window,out client)&&client.right>0&&client.bottom>0){
                    size.x=Mathf.Min(size.x,client.right);size.y=Mathf.Min(size.y,client.bottom);
                    var origin=new WindowPoint();var info=new MonitorInfo{size=System.Runtime.InteropServices.Marshal.SizeOf(typeof(MonitorInfo))};
                    if(ClientToScreen(window,ref origin)&&GetMonitorInfo(MonitorFromWindow(window,2),ref info)){
                        size.x=Mathf.Min(size.x,Mathf.Max(1,info.work.right-origin.x));
                        size.y=Mathf.Min(size.y,Mathf.Max(1,info.work.bottom-origin.y));
                    }
                }
            }
#endif
            return size;
        }
        public static void Background(Rect rect){Ensure();GUI.Box(rect,GUIContent.none,Dark);}
        public static void Panel(Rect rect,bool selected=false){Ensure();GUI.Box(rect,GUIContent.none,Card);if(selected)GUI.Box(rect,GUIContent.none,Focus);}
        public static void Notice(Rect rect,string text){Ensure();GUI.Box(rect,GUIContent.none,Banner);GUI.Label(new Rect(rect.x+18,rect.y+10,rect.width-36,rect.height-16),text,Hint);}
        public static void Dialogue(Rect rect,string title,string text,int face=2){
            Ensure();GUI.Box(rect,GUIContent.none,Banner);
            float size=44;var avatar=SheetFrame("sprout_emoji",Mathf.Clamp(face,0,4)*32,0,32,32);
            if(avatar!=null)GUI.DrawTexture(new Rect(rect.x+12,rect.y+20,size,size),avatar,ScaleMode.ScaleToFit);
            float x=rect.x+66,width=rect.width-82;
            GUI.Label(new Rect(x,rect.y+12,width,40),title,DialogueTitle);
            GUI.Label(new Rect(x,rect.y+56,width,rect.height-66),text,DialogueText);
        }
        public static string DurationLabel(ulong millis){
            if(millis==ulong.MaxValue)return "—";
            ulong seconds=millis/1000;
            return (seconds>=3600?(seconds/3600)+":"+(seconds/60%60).ToString("00"):(seconds/60).ToString("00"))+":"+(seconds%60).ToString("00")+"."+(millis%1000).ToString("000");
        }
        public static bool Button(Rect rect,string label,bool enabled=true,bool quiet=false,bool selected=false,string tooltip=""){
            Ensure();bool previous=GUI.enabled;GUI.enabled=previous&&enabled;
            Color color=GUI.backgroundColor;
            GUI.backgroundColor=new Color(.18f,.14f,.10f);
            GUI.Box(new Rect(rect.x,rect.y+3,rect.width,rect.height),GUIContent.none,Primary);
            GUI.backgroundColor=enabled?Color.white:new Color(.8f,.8f,.8f);
            GUI.SetNextControlName(label);
            bool pressed=GUI.Button(rect,new GUIContent(label,tooltip),!enabled?Disabled:selected?Selected:quiet?Quiet:Primary);
            GUI.backgroundColor=color;GUI.enabled=previous;return pressed;
        }
        public static void Meter(Rect rect,float fraction,Color color){
            Ensure();GUI.Box(rect,GUIContent.none,Dark);fraction=Mathf.Clamp01(fraction);if(fraction<=0)return;
            Color previous=GUI.color;GUI.color=color;GUI.DrawTextureWithTexCoords(new Rect(rect.x+2,rect.y+2,(rect.width-4)*fraction,Mathf.Max(1,rect.height-4)),Texture("panel"),new Rect(6f/16,5f/16,5f/16,5f/16));GUI.color=previous;
        }
    }
}
