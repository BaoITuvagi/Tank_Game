using UnityEngine;
namespace Tank {
    public sealed class MenuTheme {
        public Color Ink {get{return CartoonUi.Ink;}}
        public GUISkin Skin;
        public GUIStyle Title,Section,Text,Hint,Name,Header,HeaderButton,Primary,Secondary,Danger,Kick,Choice,Nation,NationSelected,NationDisabledLabel,ReadyButton,ReadyLabel;
        GUIStyle checkbox;
        Texture2D logo,background,checkOff,checkOn,shade;
        static Texture2D Art(string name){return Resources.Load<Texture2D>("MenuArt/"+name);}
        static void State(GUIStyleState state,GUIStyle surface,Color ink){state.background=surface.normal.background;state.textColor=ink;}
        public void Init() {
            if(Skin!=null)return;
            Skin=Object.Instantiate(GUI.skin);Skin.hideFlags=HideFlags.HideAndDontSave;Skin.font=CartoonUi.BodyFont;
            logo=Art("logo");background=Art("anh_nen");
            Text=CartoonUi.Label(17,CartoonUi.Text);Name=new GUIStyle(Text);
            Title=CartoonUi.Label(26,CartoonUi.Text,true);Title.wordWrap=false;
            Section=CartoonUi.Label(19,CartoonUi.Text,true);
            Hint=CartoonUi.Label(16,CartoonUi.Muted);ReadyLabel=CartoonUi.Label(16,CartoonUi.Ready);
            Header=CartoonUi.Label(23,CartoonUi.Text,true);
            Primary=CartoonUi.Button(CartoonUi.Gold);Secondary=CartoonUi.Button(CartoonUi.Cream);
            Danger=CartoonUi.Button(CartoonUi.Danger);ReadyButton=CartoonUi.Button(CartoonUi.Ready);
            Kick=new GUIStyle(Danger){font=CartoonUi.BodyFont,fontSize=20,fixedHeight=44,padding=new RectOffset(4,4,3,7)};
            Choice=new GUIStyle(Secondary){fontSize=14,wordWrap=false,padding=new RectOffset(8,8,7,12)};
            HeaderButton=CartoonUi.Button(CartoonUi.PanelColor,14,46);HeaderButton.font=CartoonUi.BodyFont;HeaderButton.padding=new RectOffset(10,10,4,8);
            foreach(var state in new[]{HeaderButton.normal,HeaderButton.hover,HeaderButton.active,HeaderButton.focused})state.textColor=CartoonUi.Text;
            Nation=new GUIStyle(Secondary){fontSize=14,fixedHeight=66,padding=new RectOffset(8,8,5,9)};
            NationSelected=CartoonUi.Button(CartoonUi.Ready,14,66);NationSelected.padding=new RectOffset(8,8,5,9);
            NationDisabledLabel=new GUIStyle(Nation);NationDisabledLabel.normal.background=null;NationDisabledLabel.normal.textColor=Ink;
            Skin.label=Text;Skin.button=Primary;Skin.settings.cursorColor=Ink;Skin.settings.cursorFlashSpeed=.7f;Skin.settings.selectionColor=new Color(.95f,.76f,.31f,.5f);
            Skin.textField=new GUIStyle(Skin.textField) {
                font=CartoonUi.BodyFont,fontSize=17,fontStyle=FontStyle.Normal,fixedHeight=48,wordWrap=false,alignment=TextAnchor.MiddleLeft,
                border=new RectOffset(12,12,12,12),padding=new RectOffset(16,16,8,10),margin=new RectOffset(4,4,6,8)
            };
            foreach(var state in new[]{Skin.textField.normal,Skin.textField.hover,Skin.textField.active})State(state,CartoonUi.SurfaceStyle(CartoonUi.Cream,14,0),Ink);
            State(Skin.textField.focused,CartoonUi.SurfaceStyle(CartoonUi.Cream,14,0,true),Ink);
            Skin.box=new GUIStyle(CartoonUi.SurfaceStyle(CartoonUi.PanelDark,10,0)) {
                padding=new RectOffset(16,16,14,14),margin=new RectOffset(4,4,7,9)
            };
            Skin.box.normal.textColor=CartoonUi.Text;
            checkbox=new GUIStyle(Name){fixedHeight=44,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(42,8,8,8),margin=new RectOffset(4,4,6,6)};
            foreach(var state in new[]{checkbox.normal,checkbox.hover,checkbox.active,checkbox.focused,checkbox.onNormal,checkbox.onHover,checkbox.onActive,checkbox.onFocused})state.textColor=CartoonUi.Text;
            checkOff=Art("white/checkbox empty");checkOn=Art("white/checkbox checked");
            Scrollbar(Skin.verticalScrollbar,Skin.verticalScrollbarThumb,true);Scrollbar(Skin.horizontalScrollbar,Skin.horizontalScrollbarThumb,false);
        }
        void Scrollbar(GUIStyle track,GUIStyle thumb,bool vertical) {
            var rail=Art("white/scrollbar "+(vertical?"vertical":"horizontal"));var slider=Art("white/scrollbar slider");
            track.border=new RectOffset(6,6,6,6);thumb.border=new RectOffset(6,6,6,6);
            foreach(var state in new[]{track.normal,track.hover,track.active}){state.background=rail;state.textColor=CartoonUi.Text;}
            foreach(var state in new[]{thumb.normal,thumb.hover,thumb.active}){state.background=slider;state.textColor=CartoonUi.Text;}
            if(vertical){track.fixedWidth=14;thumb.fixedWidth=14;thumb.fixedHeight=28;}
            else {track.fixedHeight=14;thumb.fixedHeight=14;thumb.fixedWidth=28;}
        }
        public bool Toggle(bool value,string label) {
            bool result=GUILayout.Toggle(value,label,checkbox);var rect=GUILayoutUtility.GetLastRect();
            GUI.DrawTexture(new Rect(rect.x+4,rect.center.y-14,28,28),result?checkOn:checkOff,ScaleMode.ScaleToFit,true);return result;
        }
        public void Background(float width,float height,bool illustrated) {
            ArenaRenderer.Fill(new Rect(0,0,width,height),CartoonUi.Backdrop);
            if(illustrated&&background!=null) {
                // Keep the complete supplied illustration visible at its original aspect.
                GUI.DrawTexture(new Rect(0,0,width,height),background,ScaleMode.ScaleToFit,true);
                if(shade==null) {
                    shade=new Texture2D(128,1,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
                    var pixels=new Color[128];
                    for(int i=0;i<pixels.Length;i++){float t=i/127f;pixels[i]=new Color(.035f,.065f,.045f,Mathf.Lerp(.10f,.78f,Mathf.SmoothStep(0,1,t)));}
                    shade.SetPixels(pixels);shade.Apply(false,true);
                }
                GUI.DrawTexture(new Rect(0,0,width,height),shade,ScaleMode.StretchToFill,true);
            }
            else {
                // Sparse track impressions on the depot floor. No grid behind data.
                Matrix4x4 saved=GUI.matrix;
                GuiTransforms.Rotate(-24,new Vector2(width*.85f,height*.5f));
                for(int i=-8;i<24;i++) {
                    ArenaRenderer.Fill(new Rect(width*.84f,i*42,48,17),new Color(.15f,.20f,.15f,.48f));
                    ArenaRenderer.Fill(new Rect(width*.84f+100,i*42,48,17),new Color(.15f,.20f,.15f,.48f));
                }
                GUI.matrix=saved;
            }
            ArenaRenderer.Fill(new Rect(0,0,width,76),new Color(.055f,.085f,.065f,.96f));
            ArenaRenderer.Fill(new Rect(24,75,width-48,1),new Color(.43f,.50f,.34f,.45f));
        }
        public void Logo(Rect rect){if(logo!=null)GUI.DrawTexture(rect,logo,ScaleMode.ScaleToFit,true);}
        public void Card(Rect rect,string title,string subtitle=null) {
            CartoonUi.Panel(rect,CartoonUi.PanelDark,14,0);
            ArenaRenderer.Fill(new Rect(rect.x+26,rect.y+20,32,4),CartoonUi.Gold);
            GUI.Label(new Rect(rect.x+26,rect.y+30,rect.width-52,42),title,Title);
            if(subtitle!=null)GUI.Label(new Rect(rect.x+26,rect.y+80,rect.width-52,38),subtitle,Hint);
        }
        public void Dispose(){if(Skin!=null)Object.Destroy(Skin);if(shade!=null)Object.Destroy(shade);Skin=null;shade=null;}
    }
}
