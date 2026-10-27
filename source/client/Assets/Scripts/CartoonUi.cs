using System;
using System.Collections.Generic;
using UnityEngine;
namespace Tank {
    public enum UiIcon { Health,Armor,Damage,Range,Clock,Players,Lock,Trophy,Skill,Fire,Airstrike,Supply }
    // Shared presentation only: the menu and HUD keep their own layout/input paths.
    public static class CartoonUi {
        public static readonly Color Ink=Hex(0x151D29),Outline=Hex(0x7A8899),Text=Hex(0xFFF7E5),Muted=Hex(0xCED8E5),
            PanelColor=Hex(0x43566F),PanelDark=Hex(0x2C3D55),Cream=Hex(0xF3EDDD),
            Gold=Hex(0xFFD65E),Ready=Hex(0x69D2AC),Danger=Hex(0xEA7C6B),Backdrop=Hex(0x19273B),
            Sand=Hex(0xE6CAA0),CardLight=Hex(0xF3EDDE),Disabled=Hex(0x8D9BAA);
        static Font displayFont,bodyFont;
        public static Font DisplayFont {get{return displayFont??(displayFont=Resources.Load<Font>("MenuArt/Fonts/Bungee-Regular"));}}
        public static Font BodyFont {get{return bodyFont??(bodyFont=Resources.Load<Font>("MenuArt/Fonts/Nunito-Bold"));}}
        static Color Hex(uint rgb){return new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f);}
        struct Key : IEquatable<Key> {
            public Color Fill;public int Radius,Depth;public bool Selected;
            public bool Equals(Key other){return Fill.Equals(other.Fill)&&Radius==other.Radius&&Depth==other.Depth&&Selected==other.Selected;}
            public override bool Equals(object other){return other is Key&&Equals((Key)other);}
            public override int GetHashCode(){return Fill.GetHashCode()^(Radius<<4)^(Depth<<12)^(Selected?1:0);}
        }
        static readonly Dictionary<Key,GUIStyle> surfaces=new Dictionary<Key,GUIStyle>();
        static readonly List<Texture2D> textures=new List<Texture2D>();
        static float RoundedDistance(float x,float y,float left,float bottom,float width,float height,float radius) {
            radius=Mathf.Clamp(radius,0,Mathf.Min(width,height)*.5f);
            float qx=Mathf.Abs(x-left-width*.5f)-(width*.5f-radius),qy=Mathf.Abs(y-bottom-height*.5f)-(height*.5f-radius);
            return Mathf.Sqrt(Mathf.Max(qx,0)*Mathf.Max(qx,0)+Mathf.Max(qy,0)*Mathf.Max(qy,0))+Mathf.Min(Mathf.Max(qx,qy),0)-radius;
        }
        static float Coverage(float x,float y,float left,float bottom,float width,float height,float radius) {
            return Mathf.Clamp01(.5f-RoundedDistance(x,y,left,bottom,width,height,radius));
        }
        static Color Over(Color under,Color color,float coverage) {
            float alpha=color.a*coverage,combined=alpha+under.a*(1-alpha);
            if(combined<=0)return Color.clear;
            var result=(color*alpha+under*under.a*(1-alpha))/combined;result.a=combined;return result;
        }
        static Texture2D Texture(Key key) {
            const int size=72;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];
            float radius=key.Radius,depth=Mathf.Clamp01(key.Depth/6f);
            const float left=1,width=size-2,outline=1;
            float bottom=key.Depth>0?2:1,height=size-bottom-1;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++) {
                float px=x+.5f,py=y+.5f;
                Color color=Color.clear;
                if(key.Depth>0) {
                    // Soft drop shadow below one face; there is no separate raised base.
                    float distance=RoundedDistance(px,py,left,bottom-(.5f+depth*1.5f),width,height,radius);
                    float coverage=Mathf.Clamp01(.5f-distance/2.4f);
                    Color shadow=Color.black;shadow.a=.10f+depth*.14f;
                    color=Over(color,shadow,coverage);
                }
                Color edge=key.Depth>0?Color.black:key.Selected?Gold:Outline;
                Color face=key.Depth>0&&key.Selected?Color.Lerp(key.Fill,Gold,.22f):key.Fill;
                color=Over(color,edge,Coverage(px,py,left,bottom,width,height,radius));
                color=Over(color,face,Coverage(px,py,left+outline,bottom+outline,width-2*outline,height-2*outline,radius-outline));
                pixels[y*size+x]=color;
            }
            texture.SetPixels(pixels);texture.Apply(false,true);textures.Add(texture);return texture;
        }
        public static GUIStyle SurfaceStyle(Color fill,int radius=16,int depth=0,bool selected=false) {
            var key=new Key{Fill=fill,Radius=radius,Depth=depth,Selected=selected};GUIStyle style;
            if(surfaces.TryGetValue(key,out style))return style;
            int border=radius+3;
            style=new GUIStyle{border=new RectOffset(border,border,border,border)};style.normal.background=Texture(key);
            surfaces.Add(key,style);return style;
        }
        public static void Panel(Rect rect,Color fill,int radius=16,int depth=0) {
            // Keep legacy depth arguments source-compatible; panels and cards stay flat.
            if(Event.current.type==EventType.Repaint)SurfaceStyle(fill,radius,0).Draw(rect,GUIContent.none,false,false,false,false);
        }
        public static GUIStyle Label(int size,Color color,bool display=false) {
            var style=new GUIStyle(GUI.skin.label){font=display?DisplayFont:BodyFont,fontSize=size,fontStyle=FontStyle.Normal,wordWrap=true,padding=new RectOffset(),clipping=TextClipping.Clip};
            style.normal.textColor=color;return style;
        }
        public static string WrapText(string value,GUIStyle style,float width) {
            var wrapped=new System.Text.StringBuilder();
            string[] lines=(value??"").Replace("\r\n","\n").Split('\n');
            for(int line=0;line<lines.Length;line++) {
                if(line>0)wrapped.Append('\n');
                string remaining=lines[line].Trim();
                while(remaining.Length>0) {
                    int fit=0,wordBreak=0;
                    var characters=System.Globalization.StringInfo.GetTextElementEnumerator(remaining);
                    while(characters.MoveNext()) {
                        string element=characters.GetTextElement();int next=characters.ElementIndex+element.Length;
                        if(fit>0&&style.CalcSize(new GUIContent(remaining.Substring(0,next))).x>width)break;
                        fit=next;if(element==" ")wordBreak=characters.ElementIndex;
                    }
                    if(fit<remaining.Length&&wordBreak>0)fit=wordBreak;
                    wrapped.Append(remaining.Substring(0,fit).TrimEnd());remaining=remaining.Substring(fit).TrimStart();
                    if(remaining.Length>0)wrapped.Append('\n');
                }
            }
            return wrapped.ToString();
        }
        static void State(GUIStyleState state,GUIStyle surface,Color ink){state.background=surface.normal.background;state.textColor=ink;}
        public static GUIStyle Button(Color fill,int fontSize=17,int height=52) {
            var style=new GUIStyle(GUI.skin.button){font=DisplayFont,fontStyle=FontStyle.Normal,fontSize=fontSize,fixedHeight=height,wordWrap=true,
                alignment=TextAnchor.MiddleCenter,border=new RectOffset(17,17,17,17),padding=new RectOffset(16,16,7,12),margin=new RectOffset(4,4,6,6)};
            State(style.normal,SurfaceStyle(fill,14,5),Ink);State(style.hover,SurfaceStyle(Color.Lerp(fill,Color.white,.17f),14,5),Ink);
            State(style.active,SurfaceStyle(Color.Lerp(fill,Ink,.08f),14,2),Ink);State(style.focused,SurfaceStyle(fill,14,5,true),Ink);
            State(style.onNormal,SurfaceStyle(Ready,14,5,true),Ink);State(style.onHover,SurfaceStyle(Color.Lerp(Ready,Color.white,.15f),14,5,true),Ink);
            State(style.onActive,SurfaceStyle(Ready,14,2,true),Ink);State(style.onFocused,SurfaceStyle(Ready,14,5,true),Ink);
            return style;
        }
        static GUIStyle badgeLight,badgeDark,medalText;
        static void BadgeStyles() {
            if(badgeLight==null) {
                badgeLight=Label(12,Text);badgeLight.alignment=TextAnchor.MiddleCenter;badgeLight.wordWrap=false;
                badgeDark=new GUIStyle(badgeLight);badgeDark.normal.textColor=Ink;
            }
        }
        public static void Badge(Rect rect,string text,Color fill) {
            BadgeStyles();
            Panel(rect,fill,8,0);
            GUI.Label(new Rect(rect.x+6,rect.y+2,rect.width-12,rect.height-4),text,fill.grayscale>.55f?badgeDark:badgeLight);
        }
        public static void Progress(Rect rect,float value,Color fill) {
            Panel(rect,Ink,Mathf.Min(7,Mathf.RoundToInt(rect.height/2)),0);
            float width=(rect.width-6)*Mathf.Clamp01(value);
            if(width>0)ArenaRenderer.Fill(new Rect(rect.x+3,rect.y+3,width,Mathf.Max(1,rect.height-6)),fill);
        }
        // Uses the exact existing player palette and sprites, including the gun barrel.
        public static void TankPortrait(Rect rect,int palette) {
            float side=Mathf.Min(rect.width,rect.height*.82f);
            var body=new Rect(rect.center.x-side*.5f,rect.center.y-side*.35f,side,side);
            GUI.DrawTexture(body,TankPalette.Body(palette),ScaleMode.ScaleToFit,true);
            GUI.DrawTexture(new Rect(body.center.x-side*.12f,body.y-side*.24f,side*.24f,side*.78f),TankPalette.Barrel(palette),ScaleMode.ScaleToFit,true);
        }
        public static void Medal(Rect rect,int rank) {
            Color color=rank==1?Gold:rank==2?Hex(0xDCE5ED):Sand;
            float side=Mathf.Min(rect.width,rect.height),cx=rect.center.x;
            ArenaRenderer.Fill(new Rect(cx-side*.26f,rect.y+side*.5f,side*.2f,side*.45f),Danger);
            ArenaRenderer.Fill(new Rect(cx+side*.06f,rect.y+side*.5f,side*.2f,side*.45f),Danger);
            Panel(new Rect(cx-side*.42f,rect.y,side*.84f,side*.74f),color,Mathf.RoundToInt(side*.3f),2);
            if(medalText==null){medalText=Label(20,Ink,true);medalText.alignment=TextAnchor.MiddleCenter;}
            GUI.Label(new Rect(cx-side*.42f,rect.y,side*.84f,side*.72f),rank.ToString(),medalText);
        }
        static void Stroke(Vector2 a,Vector2 b,float thickness,Color color) {
            var saved=GUI.matrix;var delta=b-a;
            GuiTransforms.Rotate(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);
            ArenaRenderer.Fill(new Rect(a.x,a.y-thickness*.5f,delta.magnitude,thickness),color);GUI.matrix=saved;
        }
        public static void Icon(Rect rect,UiIcon icon,Color color) {
            if(Event.current.type!=EventType.Repaint)return;
            float size=Mathf.Min(rect.width,rect.height);var origin=new Vector2(rect.center.x-size*.5f,rect.center.y-size*.5f);
            // A small, consistent silhouette vocabulary; no OS-dependent symbol glyphs.
            Action<float,float,float,float> block=(x,y,w,h)=>ArenaRenderer.Fill(new Rect(origin.x+x*size,origin.y+y*size,w*size,h*size),color);
            Action<float,float,float,float> line=(x,y,u,v)=>Stroke(origin+new Vector2(x,y)*size,origin+new Vector2(u,v)*size,Mathf.Max(2,size*.1f),color);
            switch(icon) {
                case UiIcon.Health:case UiIcon.Supply:block(.38f,.1f,.24f,.8f);block(.1f,.38f,.8f,.24f);break;
                case UiIcon.Armor:line(.18f,.15f,.82f,.15f);line(.18f,.15f,.22f,.6f);line(.82f,.15f,.78f,.6f);line(.22f,.6f,.5f,.9f);line(.78f,.6f,.5f,.9f);break;
                case UiIcon.Clock:
                    for(int i=0;i<12;i++){float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;line(.5f+Mathf.Cos(a)*.37f,.5f+Mathf.Sin(a)*.37f,.5f+Mathf.Cos(b)*.37f,.5f+Mathf.Sin(b)*.37f);}line(.5f,.25f,.5f,.5f);line(.5f,.5f,.72f,.61f);break;
                case UiIcon.Players:block(.18f,.16f,.22f,.22f);block(.6f,.16f,.22f,.22f);block(.1f,.48f,.36f,.34f);block(.54f,.48f,.36f,.34f);break;
                case UiIcon.Lock:block(.18f,.43f,.64f,.44f);line(.3f,.44f,.3f,.17f);line(.3f,.17f,.7f,.17f);line(.7f,.17f,.7f,.44f);break;
                case UiIcon.Trophy:block(.28f,.15f,.44f,.4f);block(.44f,.5f,.12f,.28f);block(.25f,.78f,.5f,.12f);line(.28f,.23f,.1f,.23f);line(.1f,.23f,.2f,.5f);line(.2f,.5f,.4f,.57f);line(.72f,.23f,.9f,.23f);line(.9f,.23f,.8f,.5f);line(.8f,.5f,.6f,.57f);break;
                case UiIcon.Range:line(.12f,.5f,.88f,.5f);line(.5f,.12f,.5f,.88f);block(.4f,.4f,.2f,.2f);break;
                case UiIcon.Airstrike:line(.5f,.1f,.5f,.9f);line(.15f,.55f,.5f,.35f);line(.5f,.35f,.85f,.55f);line(.3f,.87f,.5f,.75f);line(.5f,.75f,.7f,.87f);break;
                case UiIcon.Fire:case UiIcon.Damage:block(.34f,.35f,.32f,.5f);line(.34f,.35f,.5f,.12f);line(.5f,.12f,.66f,.35f);break;
                default:line(.63f,.1f,.3f,.53f);line(.3f,.53f,.68f,.48f);line(.68f,.48f,.38f,.9f);break;
            }
        }
        public static void Busy(Rect rect,string text) {
            BadgeStyles();
            Panel(rect,PanelDark,10,0);
            int active=Mathf.FloorToInt(Time.unscaledTime*8)%4;
            for(int i=0;i<4;i++)ArenaRenderer.Fill(new Rect(rect.x+12+i*7,rect.center.y-3,4,6),i==active?Gold:Muted);
            GUI.Label(new Rect(rect.x+48,rect.y+4,rect.width-58,rect.height-8),text,badgeLight);
        }
        public static void Dispose() {
            foreach(var texture in textures)UnityEngine.Object.Destroy(texture);
            textures.Clear();surfaces.Clear();badgeLight=badgeDark=medalText=null;
        }
    }
}
