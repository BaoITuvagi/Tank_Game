using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tank {
    // Square material samples are taken inside the opaque swatches, away from
    // their outlines. The renderer repeats them without creating pixel copies.
    public static class ArenaEnvironmentArt {
        public const string Root="ArenaEnvironment/";
        public const int Width=1536,Height=1024,Columns=3,Rows=2,MaxTextureBytes=16*1024*1024;
        public static readonly string[] Required={"wall_sandstone","wall_snow","water_green","water_desert","water_snow"};
        [Serializable] public sealed class Material {
            public string key;
            public int cell,x,y,width,height,sampleX,sampleY,sampleSize;
        }
        [Serializable] public sealed class Manifest {
            public string sheet;
            public int width,height,columns,rows;
            public Material[] materials;
        }
        public sealed class Image {
            public readonly Texture2D Texture;
            public readonly Rect Uv;
            public readonly float Aspect;
            public Image(Material material,Texture2D texture) {
                Texture=texture;Aspect=1;
                Uv=new Rect((material.sampleX+.5f)/texture.width,1-(material.sampleY+material.sampleSize-.5f)/texture.height,(material.sampleSize-1f)/texture.width,(material.sampleSize-1f)/texture.height);
            }
        }
        static readonly Dictionary<string,Image> images=new Dictionary<string,Image>();
        static bool loaded;
        static string error;
        public static string Validate(Manifest manifest,Func<string,Texture2D> load) {
            if(manifest==null||manifest.sheet!="materials"||manifest.width!=Width||manifest.height!=Height||manifest.columns!=Columns||manifest.rows!=Rows||manifest.materials==null||manifest.materials.Length!=Required.Length)return "catalog.json";
            Texture2D texture=load==null?null:load(manifest.sheet);
            if(texture==null||texture.width!=Width||texture.height!=Height||texture.format!=TextureFormat.RGBA32)return "materials (PNG/RGBA32 dimensions)";
            if((long)texture.width*texture.height*4>MaxTextureBytes)return "materials (texture budget)";
            var keys=new HashSet<string>();var cells=new HashSet<int>();
            for(int i=0;i<Required.Length;i++) {
                Material material=manifest.materials[i];
                if(material==null||material.key!=Required[i]||!keys.Add(material.key))return Required[i]+" (mapping)";
                if(material.cell!=i||!cells.Add(material.cell)||material.width<2||material.height<2)return material.key+" (cell)";
                float left=(material.cell%Columns)*Width/(float)Columns,top=(material.cell/Columns)*Height/(float)Rows;
                float right=left+Width/(float)Columns,bottom=top+Height/(float)Rows;
                if(material.x<left+3||material.y<top+3||material.x+material.width>right-3||material.y+material.height>bottom-3)return material.key+" (crop padding)";
                if(material.sampleSize<32||material.sampleX<=material.x||material.sampleY<=material.y||material.sampleX+material.sampleSize>=material.x+material.width||material.sampleY+material.sampleSize>=material.y+material.height)return material.key+" (inner material sample)";
            }
            return null;
        }
        public static string Preload() {
            if(loaded)return error;loaded=true;images.Clear();
            var metadata=Resources.Load<TextAsset>(Root+"catalog");Manifest manifest=null;
            try{if(metadata!=null)manifest=JsonUtility.FromJson<Manifest>(metadata.text);}catch(Exception){}
            Texture2D texture=Resources.Load<Texture2D>(Root+"materials");
            error=Validate(manifest,key=>texture);
            if(error==null)foreach(var material in manifest.materials)images.Add(material.key,new Image(material,texture));
            else Debug.LogError("Missing/invalid arena environment art: "+error);
            return error;
        }
        public static Image Get(string key){Preload();Image image;return key!=null&&images.TryGetValue(key,out image)?image:null;}
        public static void Draw(Rect bounds,string key) {
            if(Event.current==null||Event.current.type!=EventType.Repaint)return;
            Image image=Get(key);if(image==null)return;
            GUI.DrawTextureWithTexCoords(ArenaBiomeArt.Fit(bounds,image.Aspect),image.Texture,image.Uv);
        }
        public static void Reset(){images.Clear();loaded=false;error=null;}
    }
}
