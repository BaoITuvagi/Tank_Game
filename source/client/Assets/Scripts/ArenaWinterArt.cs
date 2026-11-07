using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tank {
    // A standalone cutout shares the existing source metadata shape. Its
    // texture is cached whole, with no cropped copy or readable runtime data.
    public static class ArenaWinterArt {
        public const string Root="ArenaWinter/";
        public const int MaxTextureBytes=8*1024*1024;
        public static readonly string[] Required={"igloo"};
        [Serializable] public sealed class Manifest {public ArenaTerrainArt.Entry[] images;}
        static readonly Dictionary<string,Texture2D> images=new Dictionary<string,Texture2D>();
        static bool loaded;
        static string error;
        public static string Validate(Manifest manifest,Func<string,Texture2D> load) {
            if(manifest==null||manifest.images==null||manifest.images.Length!=Required.Length)return "catalog.json";
            long bytes=0;
            for(int i=0;i<Required.Length;i++) {
                var entry=manifest.images[i];string key=Required[i];
                if(entry==null||entry.key!=key||entry.file!=key||entry.alpha!="cutout")return key+" (mapping/alpha intent)";
                if(entry.width<32||entry.height<32||entry.width>2048||entry.height>2048)return key+" (catalog dimensions)";
                if(entry.sourceHash==null||entry.sourceHash.Length!=64)return key+" (source hash)";
                foreach(char c in entry.sourceHash)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))return key+" (source hash)";
                var texture=load==null?null:load(entry.file);
                if(texture==null||texture.width!=entry.width||texture.height!=entry.height||texture.format!=TextureFormat.RGBA32)return key+" (PNG/RGBA32 dimensions)";
                bytes+=(long)texture.width*texture.height*4;
            }
            return bytes>MaxTextureBytes?"winter textures (budget)":null;
        }
        public static string Preload() {
            if(loaded)return error;
            loaded=true;images.Clear();Manifest manifest=null;
            var catalog=Resources.Load<TextAsset>(Root+"catalog");
            try{if(catalog!=null)manifest=JsonUtility.FromJson<Manifest>(catalog.text);}catch(Exception){}
            var textures=new Dictionary<string,Texture2D>();
            foreach(string key in Required)textures[key]=Resources.Load<Texture2D>(Root+key);
            error=Validate(manifest,key=>textures.ContainsKey(key)?textures[key]:null);
            if(error==null)foreach(var entry in manifest.images)images.Add(entry.key,textures[entry.file]);
            else Debug.LogError("Missing/invalid arena winter art: "+error);
            return error;
        }
        public static Texture2D Get(string key) {Preload();Texture2D texture;return key!=null&&images.TryGetValue(key,out texture)?texture:null;}
        public static void Draw(Rect bounds,string key) {
            if(Event.current==null||Event.current.type!=EventType.Repaint)return;
            var texture=Get(key);if(texture==null)return;
            GUI.DrawTexture(ArenaBiomeArt.Fit(bounds,texture.width/(float)texture.height),texture,ScaleMode.StretchToFill,true);
        }
        public static void Reset(){images.Clear();loaded=false;error=null;}
    }
}
