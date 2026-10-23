using System;
using System.Collections.Generic;
using UnityEngine;
namespace Tank {
    // Shared catalog for the Combat FX sheets bundled with the project.
    public static class CombatFxCatalog {
        public const string Root="CombatFx/CozyChunky/";
        [Serializable] public sealed class Description {
            public string key,view,faces;
            public int frames,fps,cell;
            public bool loop;
        }
        [Serializable] public sealed class Manifest {public Description[] clips;}
        public sealed class Clip {
            public readonly Description Info;
            public readonly Texture2D Texture;
            public Clip(Description info,Texture2D texture) {Info=info;Texture=texture;}
            public float Duration {get {return Info.frames/(float)Info.fps;}}
            public int Frame(float age,float speed=1) {
                int frame=Math.Max(0,(int)(Math.Max(0,age)*Info.fps*speed));
                return Info.loop?frame%Info.frames:Math.Min(Info.frames-1,frame);
            }
            public Rect Uv(float age,float speed=1) {return new Rect(Frame(age,speed)/(float)Info.frames,0,1f/Info.frames,1);}
        }
        static Description Spec(string key,int frames,bool loop=false,string faces=null) {
            return new Description{key=key,frames=frames,fps=24,cell=128,loop=loop,faces=faces};
        }
        public static readonly Description[] Required={
            Spec("combat/muzzle",8,false,"right"),Spec("combat/hit_top",12),Spec("combat/explosion_top",18),
            Spec("movement/dust_top",12),Spec("movement/dash",12,false,"right"),Spec("nature/smoke_top",18),
            Spec("nature/rocks_top",18),Spec("nature/fire_top",12,true),Spec("magic/magic_burst",12),
            Spec("magic/lightning_top",18),Spec("magic/heal_top",18),Spec("magic/shield",18),
            Spec("status/debuff",18,true),Spec("status/buff",18,true),Spec("status/stun",18,true),
            Spec("pickup_ui/pickup",12),Spec("pickup_ui/item_glow",24,true),Spec("pickup_ui/heart",12),
            Spec("nature/portal",18,true),Spec("combat/ground_slam_top",18),Spec("magic/ice_hit_top",18)
        };
        static readonly Dictionary<string,Clip> clips=new Dictionary<string,Clip>();
        static bool checkedAssets;
        static string error;
        public static bool Matches(Description actual,Description expected) {
            return actual!=null&&actual.frames==expected.frames&&actual.fps==expected.fps&&actual.cell==expected.cell&&actual.loop==expected.loop&&(actual.faces??"")== (expected.faces??"");
        }
        public static string Preload() {
            if(checkedAssets)return error;
            checkedAssets=true;clips.Clear();
            var metadata=Resources.Load<TextAsset>(Root+"catalog");
            Manifest manifest=null;
            try {if(metadata!=null)manifest=JsonUtility.FromJson<Manifest>(metadata.text);}catch(Exception) {}
            error=Validate(manifest,key=>Resources.Load<Texture2D>(Root+key),clips);
            if(error!=null)clips.Clear();return error;
        }
        public static string Validate(Manifest manifest,Func<string,Texture2D> load) {return Validate(manifest,load,null);}
        static string Validate(Manifest manifest,Func<string,Texture2D> load,Dictionary<string,Clip> output) {
            if(manifest==null||manifest.clips==null||manifest.clips.Length!=Required.Length)return "catalog.json";
            long bytes=0;
            for(int i=0;i<Required.Length;i++) {
                var spec=Required[i];var info=manifest.clips[i];
                if(info==null||info.key!=spec.key||!Matches(info,spec))return spec.key+" (metadata)";
                var texture=load(spec.key);
                if(texture==null||texture.width!=spec.frames*128||texture.height!=128||texture.format!=TextureFormat.RGBA32)return spec.key+" (PNG/RGBA32)";
                bytes+=(long)texture.width*texture.height*4;if(output!=null)output.Add(spec.key,new Clip(info,texture));
            }
            if(bytes>32*1024*1024)return "Bộ texture Combat FX vượt 32 MiB";
            return null;
        }
        public static Clip Get(string key) {Clip clip;return clips.TryGetValue(key,out clip)?clip:null;}
        // Used after an explicit Editor import/validation, never in the render loop.
        public static void Reset() {clips.Clear();checkedAssets=false;error=null;}
    }
}
