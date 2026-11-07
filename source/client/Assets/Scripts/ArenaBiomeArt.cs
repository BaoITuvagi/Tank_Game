using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tank {
    // Decorative sprites only: these crops never affect map tiles or collision.
    public static class ArenaBiomeArt {
        public const string Root="ArenaBiomes/";
        public const int Width=1774,Height=887,Columns=4,Rows=2,MaxTextureBytes=8*1024*1024;
        public static readonly string[] Required={"desert_cactus","desert_scrub","desert_rock","desert_dune","snow_pine","snow_rock","snow_drift","ice_shards"};
        [Serializable] public sealed class Sprite {public string key;public int cell,x,y,width,height;}
        [Serializable] public sealed class Manifest {public string sheet;public int width,height,columns,rows;public Sprite[] sprites;}
        public sealed class Image {
            public readonly Texture2D Texture;public readonly Rect Uv;public readonly float Aspect;
            public Image(Sprite sprite,Texture2D texture) {
                Texture=texture;Aspect=sprite.width/(float)sprite.height;
                // Half-texel inset prevents bilinear sampling from adjacent cells.
                Uv=new Rect((sprite.x+.5f)/texture.width,1-(sprite.y+sprite.height-.5f)/texture.height,(sprite.width-1f)/texture.width,(sprite.height-1f)/texture.height);
            }
        }
        static readonly Dictionary<string,Image> images=new Dictionary<string,Image>();
        static bool loaded;static string error;
        public static string Validate(Manifest manifest,Func<string,Texture2D> load) {
            if(manifest==null||manifest.sheet!="props"||manifest.width!=Width||manifest.height!=Height||manifest.columns!=Columns||manifest.rows!=Rows||manifest.sprites==null||manifest.sprites.Length!=Required.Length)return "catalog.json";
            Texture2D texture=load==null?null:load(manifest.sheet);
            if(texture==null||texture.width!=Width||texture.height!=Height||texture.format!=TextureFormat.RGBA32)return "props (PNG/RGBA32 dimensions)";
            if((long)texture.width*texture.height*4>MaxTextureBytes)return "props (texture budget)";
            var keys=new HashSet<string>();var cells=new HashSet<int>();
            for(int i=0;i<Required.Length;i++) {
                Sprite sprite=manifest.sprites[i];
                if(sprite==null||sprite.key!=Required[i]||!keys.Add(sprite.key))return Required[i]+" (mapping)";
                if(sprite.cell!=i||!cells.Add(sprite.cell)||sprite.width<2||sprite.height<2)return sprite.key+" (cell)";
                float left=(sprite.cell%Columns)*Width/(float)Columns,top=(sprite.cell/Columns)*Height/(float)Rows;
                if(sprite.x<left||sprite.y<top||sprite.x+sprite.width>left+Width/(float)Columns||sprite.y+sprite.height>top+Height/(float)Rows)return sprite.key+" (crop outside cell)";
            }
            return null;
        }
        public static string Preload() {
            if(loaded)return error;loaded=true;images.Clear();
            var metadata=Resources.Load<TextAsset>(Root+"catalog");Manifest manifest=null;
            try{if(metadata!=null)manifest=JsonUtility.FromJson<Manifest>(metadata.text);}catch(Exception){}
            Texture2D texture=Resources.Load<Texture2D>(Root+"props");
            error=Validate(manifest,key=>texture);
            if(error==null)foreach(var sprite in manifest.sprites)images.Add(sprite.key,new Image(sprite,texture));
            else Debug.LogError("Missing/invalid arena biome art: "+error);
            return error;
        }
        public static Image Get(string key){Preload();Image image;return key!=null&&images.TryGetValue(key,out image)?image:null;}
        public static Rect Fit(Rect bounds,float aspect) {
            if(aspect<=0||float.IsNaN(aspect)||float.IsInfinity(aspect))return new Rect(bounds.center,Vector2.zero);
            float width=Mathf.Min(Mathf.Max(0,bounds.width),Mathf.Max(0,bounds.height)*aspect),height=width/aspect;
            return new Rect(bounds.center.x-width/2,bounds.center.y-height/2,width,height);
        }
        public static void Draw(Rect bounds,string key) {
            if(Event.current==null||Event.current.type!=EventType.Repaint)return;
            Image image=Get(key);if(image==null)return;
            GUI.DrawTextureWithTexCoords(Fit(bounds,image.Aspect),image.Texture,image.Uv);
        }
        public static void Reset(){images.Clear();loaded=false;error=null;}
    }
}
