using UnityEngine;
using System.Collections.Generic;

namespace Tank.Campaign {
    // Campaign semantics use the same palette, fonts and surfaces as multiplayer.
    // Styles are copied locally so campaign never mutates the shared menu skin.
    public static class CampaignUI {
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        public static GUIStyle Header,Title,Text,Hint,Light,SmallLight,Card,Dark,Slot,Banner,Primary,Quiet,Selected,Disabled,Focus,RankingRow,RankingText,RankingName,DialogueTitle,DialogueText,Danger,StatValue,StatCaption,SkillTitleLight,ErrorLabel;
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
        public static void Ensure(){
            if(Header!=null)return;
            Header=CartoonUi.Label(26,CartoonUi.Text,true);Title=CartoonUi.Label(21,CartoonUi.Ink,true);
            Text=CartoonUi.Label(16,CartoonUi.Ink);Hint=CartoonUi.Label(14,CartoonUi.PanelDark);
            Light=CartoonUi.Label(18,CartoonUi.Text);SmallLight=CartoonUi.Label(14,CartoonUi.Muted);
            SkillTitleLight=CartoonUi.Label(16,CartoonUi.Text);
            ErrorLabel=CartoonUi.Label(14,CartoonUi.Danger);
            Card=new GUIStyle(CartoonUi.SurfaceStyle(CartoonUi.Cream,18,0));Dark=new GUIStyle(CartoonUi.SurfaceStyle(CartoonUi.PanelDark,16,0));
            Slot=new GUIStyle(CartoonUi.SurfaceStyle(CartoonUi.PanelColor,14,0));Banner=new GUIStyle(CartoonUi.SurfaceStyle(CartoonUi.Sand,14,0));
            Primary=CartoonUi.Button(CartoonUi.Gold,16,0);Quiet=CartoonUi.Button(CartoonUi.Cream,15,0);
            Selected=CartoonUi.Button(CartoonUi.Ready,16,0);Disabled=CartoonUi.Button(CartoonUi.PanelDark,15,0);Danger=CartoonUi.Button(CartoonUi.Danger,15,0);
            Disabled.normal.textColor=Disabled.hover.textColor=Disabled.active.textColor=Disabled.focused.textColor=CartoonUi.Text;
            Focus=new GUIStyle(CartoonUi.SurfaceStyle(CartoonUi.Cream,18,0,true));
            RankingRow=new GUIStyle(CartoonUi.SurfaceStyle(CartoonUi.PanelColor,14,0));
            RankingText=CartoonUi.Label(17,CartoonUi.Text);RankingText.alignment=TextAnchor.MiddleLeft;
            RankingName=new GUIStyle(RankingText){fontSize=18,wordWrap=false};
            DialogueTitle=CartoonUi.Label(16,CartoonUi.Ink);DialogueText=CartoonUi.Label(14,CartoonUi.Ink);
            StatValue=CartoonUi.Label(19,CartoonUi.Ink);StatValue.alignment=TextAnchor.MiddleCenter;
            StatCaption=CartoonUi.Label(12,CartoonUi.PanelDark);StatCaption.alignment=TextAnchor.MiddleCenter;
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
        public static void Background(Rect rect){Ensure();ArenaRenderer.Fill(rect,CartoonUi.Backdrop);}
        public static void Panel(Rect rect,bool selected=false){Ensure();GUI.Box(rect,GUIContent.none,selected?Focus:Card);}
        public static void Notice(Rect rect,string text,bool error=false,bool success=false){
            Ensure();CartoonUi.Panel(rect,error?CartoonUi.Danger:success?CartoonUi.Ready:CartoonUi.Sand,12,3);
            GUI.Label(new Rect(rect.x+16,rect.y+10,rect.width-32,rect.height-16),text,DialogueText);
        }
        public static void Dialogue(Rect rect,string title,string text,int face=2){
            Ensure();GUI.Box(rect,GUIContent.none,Banner);
            CartoonUi.Icon(new Rect(rect.x+14,rect.y+14,24,24),UiIcon.Skill,CartoonUi.Ink);
            GUI.Label(new Rect(rect.x+46,rect.y+12,rect.width-60,26),title,DialogueTitle);
            GUI.Label(new Rect(rect.x+16,rect.y+43,rect.width-32,rect.height-51),text,DialogueText);
        }
        public static string DurationLabel(ulong millis){
            if(millis==ulong.MaxValue)return "—";
            ulong seconds=millis/1000;
            return (seconds>=3600?(seconds/3600)+":"+(seconds/60%60).ToString("00"):(seconds/60).ToString("00"))+":"+(seconds%60).ToString("00")+"."+(millis%1000).ToString("000");
        }
        public static bool Button(Rect rect,string label,bool enabled=true,bool quiet=false,bool selected=false,string tooltip="",bool danger=false){
            Ensure();bool previous=GUI.enabled;GUI.enabled=previous&&enabled;
            GUI.SetNextControlName(label);
            bool pressed=GUI.Button(rect,new GUIContent(label,tooltip),!enabled?Disabled:danger?Danger:selected?Selected:quiet?Quiet:Primary);
            GUI.enabled=previous;return pressed;
        }
        public static void Meter(Rect rect,float fraction,Color color){
            CartoonUi.Progress(rect,fraction,color);
        }
    }
}
