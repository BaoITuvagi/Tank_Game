using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tank {
    // The same untouched Kenney textures and flat functional marks are used on the map,
    // in the shop and in the inventory. Item IDs are still the shared gameplay contract.
    public static class ItemIconArt {
        public const string Root="ItemCartoon/";
        public const int ImportedSize=512,DecorationSize=128;
        public const string CompositionVersion="kenney-flat-v1",CoinSource="single-coin";
        public static readonly string[] Required={"medical","armor","he","purification","invincibility","mirror","gold"};
        public static readonly string[] SourceResources={"ArenaArt/crateMetal","ArenaArt/bulletSand1_outline"};
        [Serializable] public sealed class Source {public string key,resource;public int width,height;}
        [Serializable] public sealed class Icon {public string key,source;}
        [Serializable] public sealed class Manifest {public string renderer;public Source[] sources;public Icon[] icons;}
        public sealed class Image {
            public readonly Texture2D Base,Decoration,Projectile;
            public readonly string Source;
            public float Aspect {get{return 1;}}
            public Image(string key,string source,Texture2D baseTexture,Texture2D projectile) {
                Source=source;Base=baseTexture;Projectile=projectile;Decoration=MakeDecoration(key);
            }
        }
        static readonly Dictionary<string,Image> images=new Dictionary<string,Image>();
        static bool loaded;static string error;
        public static string ItemKey(int type){return type>=0&&type<GameRules.ItemTypeCount?Required[type]:null;}
        public static string CampaignPickupKey(int kind){return kind==0?"medical":kind==1?"he":kind==2?"armor":kind==3?"gold":null;}
        public static string Validate(Manifest manifest,Func<string,Texture2D> load) {
            if(manifest==null||manifest.renderer!=CompositionVersion||manifest.icons==null||manifest.icons.Length!=Required.Length||manifest.sources==null||manifest.sources.Length!=2)return "catalog.json";
            var sources=new HashSet<string>();
            for(int i=0;i<2;i++) {
                Source source=manifest.sources[i];string key=i==0?"crate":"bullet";
                if(source==null||source.key!=key||source.resource!=SourceResources[i]||!sources.Add(source.key))return key+" (source mapping)";
                int width=i==0?28:8,height=i==0?28:14;
                if(source.width!=width||source.height!=height)return key+" (source dimensions)";
                Texture2D texture=load(source.resource);
                if(texture==null||texture.width!=width||texture.height!=height)return source.resource+" (texture)";
            }
            var keys=new HashSet<string>();
            for(int i=0;i<Required.Length;i++) {
                Icon icon=manifest.icons[i];string source=i==6?CoinSource:"crate";
                if(icon==null||icon.key!=Required[i]||!keys.Add(icon.key)||icon.source!=source)return Required[i]+" (mapping)";
            }
            return null;
        }
        public static string Preload() {
            if(loaded)return error;loaded=true;images.Clear();
            var asset=Resources.Load<TextAsset>(Root+"catalog");Manifest manifest=null;
            try{if(asset!=null)manifest=JsonUtility.FromJson<Manifest>(asset.text);}catch(Exception){}
            error=Validate(manifest,key=>Resources.Load<Texture2D>(key));
            if(error==null) {
                var crate=Resources.Load<Texture2D>(SourceResources[0]);var bullet=Resources.Load<Texture2D>(SourceResources[1]);
                foreach(var icon in manifest.icons)images.Add(icon.key,new Image(icon.key,icon.source,icon.source=="crate"?crate:null,icon.key=="he"||icon.key=="mirror"?bullet:null));
            }
            else Debug.LogError("Missing/invalid item artwork: "+error);
            return error;
        }
        public static Image Get(string key){Preload();Image result;return key!=null&&images.TryGetValue(key,out result)?result:null;}
        public static Rect Fit(Rect bounds,float aspect) {
            if(aspect<=0||float.IsNaN(aspect)||float.IsInfinity(aspect))return new Rect(bounds.center,Vector2.zero);
            float width=Mathf.Min(Mathf.Max(0,bounds.width),Mathf.Max(0,bounds.height)*aspect),height=width/aspect;
            return new Rect(bounds.center.x-width/2,bounds.center.y-height/2,width,height);
        }
        static Rect Part(Rect rect,float x,float y,float width,float height) {return new Rect(rect.x+x*rect.width/128,rect.y+y*rect.height/128,width*rect.width/128,height*rect.height/128);}
        static void Texture(Rect rect,Texture2D texture,Action<Rect,Texture2D> draw) {
            if(draw!=null)draw(rect,texture);else GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill,true);
        }
        public static void Draw(Rect bounds,string key,Action<Rect,Texture2D> draw=null) {
            if(Event.current==null||Event.current.type!=EventType.Repaint)return;
            Image image=Get(key);if(image==null)return;
            Rect rect=Fit(bounds,1);
            if(image.Base!=null)Texture(Part(rect,9,9,110,110),image.Base,draw);
            Texture(rect,image.Decoration,draw);
            // Actual arena shell art, left in its original colour and top-down orientation.
            if(key=="he")Texture(Part(rect,46,19,36,63),image.Projectile,draw);
            else if(key=="mirror") {
                Texture(Part(rect,18,25,24,42),image.Projectile,draw);
                Texture(Part(rect,52,10,24,42),image.Projectile,draw);
                Texture(Part(rect,86,25,24,42),image.Projectile,draw);
            }
        }
        public static void DrawItem(Rect bounds,int type){Draw(bounds,ItemKey(type));}
        public static void DrawGold(Rect bounds){Draw(bounds,"gold");}
        public static void Reset() {
            foreach(var image in images.Values)if(image.Decoration!=null) {
                if(Application.isPlaying)UnityEngine.Object.Destroy(image.Decoration);else UnityEngine.Object.DestroyImmediate(image.Decoration);
            }
            images.Clear();loaded=false;error=null;
        }

        // Code-native vector marks: no generated illustration, image recolouring or glyph font.
        // Coordinates use one 128-unit canvas. Four subpixel samples keep the outline smooth
        // at the 24/32px pickup sizes while retaining the arena's flat material language.
        sealed class Shape {
            public readonly Color Color;public readonly Vector2[] Points;public readonly Rect Bounds;
            public Shape(Color color,Vector2[] points) {
                Color=color;Points=points;float left=float.MaxValue,top=float.MaxValue,right=float.MinValue,bottom=float.MinValue;
                foreach(var point in points){left=Mathf.Min(left,point.x);top=Mathf.Min(top,point.y);right=Mathf.Max(right,point.x);bottom=Mathf.Max(bottom,point.y);}
                Bounds=Rect.MinMaxRect(left,top,right,bottom);
            }
            public Shape(Color color,Rect ellipse){Color=color;Bounds=ellipse;}
            public bool Contains(float x,float y) {
                if(x<Bounds.xMin||x>Bounds.xMax||y<Bounds.yMin||y>Bounds.yMax)return false;
                if(Points==null) {float u=(x-Bounds.center.x)/(Bounds.width/2),v=(y-Bounds.center.y)/(Bounds.height/2);return u*u+v*v<=1;}
                bool inside=false;
                for(int i=0,j=Points.Length-1;i<Points.Length;j=i++) {
                    Vector2 a=Points[i],b=Points[j];
                    if((a.y>y)!=(b.y>y)&&x<(b.x-a.x)*(y-a.y)/(b.y-a.y)+a.x)inside=!inside;
                }
                return inside;
            }
        }
        static Color Hex(uint rgb){return new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f);}
        static readonly Color Ink=Hex(0x354440),Cream=Hex(0xF2E7C9),Red=Hex(0xC85546),Steel=Hex(0x8FA8A8),LightSteel=Hex(0xB9CDCB),Gold=Hex(0xF4C04B),Ochre=Hex(0xBA8A35),Violet=Hex(0x9A8AAF);
        static void Polygon(List<Shape> shapes,Color color,params float[] xy) {var points=new Vector2[xy.Length/2];for(int i=0;i<points.Length;i++)points[i]=new Vector2(xy[i*2],xy[i*2+1]);shapes.Add(new Shape(color,points));}
        static void Box(List<Shape> shapes,Color color,float x,float y,float width,float height){Polygon(shapes,color,x,y,x+width,y,x+width,y+height,x,y+height);}
        static void Disc(List<Shape> shapes,Color color,float x,float y,float radius){shapes.Add(new Shape(color,new Rect(x-radius,y-radius,radius*2,radius*2)));}
        static void Line(List<Shape> shapes,Color color,float x,float y,float u,float v,float width) {
            Vector2 delta=new Vector2(u-x,v-y).normalized,normal=new Vector2(-delta.y,delta.x)*(width/2);
            Polygon(shapes,color,x+normal.x,y+normal.y,u+normal.x,v+normal.y,u-normal.x,v-normal.y,x-normal.x,y-normal.y);
        }
        static void Star(List<Shape> shapes,Color color,float x,float y,float outer,float inner,int points) {
            var xy=new float[points*4];
            for(int i=0;i<points*2;i++){float angle=i*Mathf.PI/points-Mathf.PI/2,radius=i%2==0?outer:inner;xy[i*2]=x+Mathf.Cos(angle)*radius;xy[i*2+1]=y+Mathf.Sin(angle)*radius;}
            Polygon(shapes,color,xy);
        }
        static List<Shape> Decoration(string key) {
            var shapes=new List<Shape>();
            switch(key) {
                case "medical":
                    Box(shapes,Ink,25,25,78,78);Box(shapes,Cream,29,29,70,70);
                    Box(shapes,Red,53,37,22,54);Box(shapes,Red,37,53,54,22);break;
                case "armor":
                    Polygon(shapes,Ink,39,21,89,21,106,38,106,90,89,107,39,107,22,90,22,38);
                    Polygon(shapes,Steel,41,26,87,26,101,40,101,88,87,102,41,102,27,88,27,40);
                    Box(shapes,LightSteel,54,29,20,70);
                    foreach(int x in new[]{37,91})foreach(int y in new[]{40,88}){Disc(shapes,Ink,x,y,5);Disc(shapes,Cream,x,y,2);}break;
                case "he":
                    Star(shapes,Ink,64,65,52,35,8);Star(shapes,Red,64,65,46,30,8);Star(shapes,Gold,64,65,32,22,8);
                    // The shell is drawn over this burst using the original Kenney texture.
                    break;
                case "purification":
                    Polygon(shapes,Ink,44,23,84,23,96,35,96,95,84,108,44,108,32,95,32,35);
                    Polygon(shapes,LightSteel,46,28,82,28,91,37,91,93,82,103,46,103,37,93,37,37);
                    Box(shapes,Ink,44,10,40,20);Box(shapes,Steel,48,14,32,12);
                    Polygon(shapes,Ink,64,39,82,65,84,75,79,86,69,92,59,92,49,86,44,75,46,65);
                    Polygon(shapes,Hex(0x6FBDB5),64,48,77,67,79,75,75,83,67,87,60,87,53,83,49,75,51,67);
                    Line(shapes,Cream,56,73,57,79,3);break;
                case "invincibility":
                    Polygon(shapes,Ink,64,14,108,28,103,74,92,96,64,115,36,96,25,74,20,28);
                    Polygon(shapes,Gold,64,20,102,32,97,72,87,92,64,108,41,92,31,72,26,32);
                    Polygon(shapes,Ochre,64,29,92,38,88,69,80,85,64,97,48,85,40,69,36,38);
                    Star(shapes,Cream,64,61,23,10,5);break;
                case "mirror":
                    Box(shapes,Ink,30,79,68,28);Box(shapes,Violet,34,83,60,20);
                    Line(shapes,Ink,64,91,30,60,10);Line(shapes,Ink,64,91,64,45,10);Line(shapes,Ink,64,91,98,60,10);
                    Line(shapes,Violet,64,91,30,60,5);Line(shapes,Violet,64,91,64,45,5);Line(shapes,Violet,64,91,98,60,5);break;
                case "gold":
                    // One face-on coin, with one rim and a small embossed glint; no stack.
                    Disc(shapes,Ink,64,64,53);Disc(shapes,Ochre,64,64,49);Disc(shapes,Gold,62,61,46);
                    Disc(shapes,Ochre,62,61,34);Disc(shapes,Gold,62,61,29);
                    Star(shapes,Cream,61,60,21,8,4);break;
            }
            return shapes;
        }
        static Texture2D MakeDecoration(string key) {
            List<Shape> shapes=Decoration(key);var pixels=new Color[DecorationSize*DecorationSize];
            for(int y=0;y<DecorationSize;y++)for(int x=0;x<DecorationSize;x++) {
                Color premultiplied=Color.clear;float alpha=0;
                for(int sy=0;sy<2;sy++)for(int sx=0;sx<2;sx++) {
                    float u=x+(sx+.5f)/2,v=y+(sy+.5f)/2;Color sample=Color.clear;
                    foreach(Shape shape in shapes)if(shape.Contains(u,v))sample=shape.Color;
                    alpha+=sample.a*.25f;premultiplied+=sample*sample.a*.25f;
                }
                Color color=alpha>0?premultiplied/alpha:Color.clear;color.a=alpha;pixels[(DecorationSize-1-y)*DecorationSize+x]=color;
            }
            var texture=new Texture2D(DecorationSize,DecorationSize,TextureFormat.RGBA32,false){name="Item mark: "+key,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
    }
}
