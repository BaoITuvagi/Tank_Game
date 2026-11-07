using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tank.Campaign {
    // Atlas rectangles are source-pixel bounds inside fixed cells, not gameplay geometry.
    public static class CampaignCartoonCatalog {
        public const string Root="CampaignCartoon/";
        [Serializable] public sealed class Sheet {public string key;public int width,height,columns,rows;}
        [Serializable] public sealed class Sprite {public string key,sheet;public int cell,x,y,width,height;}
        [Serializable] public sealed class Manifest {public Sheet[] sheets;public Sprite[] sprites;}
        public sealed class Image {
            public readonly Texture2D Texture;public readonly Rect Uv;public readonly float Aspect;
            public Image(Sprite sprite,Texture2D texture) {
                Texture=texture;Aspect=sprite.width/(float)sprite.height;
                // Half-texel inset keeps bilinear samples within this padded sprite crop.
                Uv=new Rect((sprite.x+.5f)/texture.width,1-(sprite.y+sprite.height-.5f)/texture.height,(sprite.width-1f)/texture.width,(sprite.height-1f)/texture.height);
            }
        }
        public static readonly string[] Required={"tree","trees","rock","building_house","building_hangar","building_factory","building_tower","rubble","parked_truck","supplies","ruin_house","ruin_factory","ruin_tower","flag","warning","parked_tank","enemy","enemy_ranged","enemy_support","boss_1","boss_2","boss_3","boss_4"};
        static readonly Dictionary<string,Image> images=new Dictionary<string,Image>();
        static bool checkedAssets;static string error;
        public static string Preload() {
            if(checkedAssets)return error;checkedAssets=true;images.Clear();
            var metadata=Resources.Load<TextAsset>(Root+"catalog");Manifest manifest=null;
            try {if(metadata!=null)manifest=JsonUtility.FromJson<Manifest>(metadata.text);}catch(Exception) {}
            error=Validate(manifest,key=>Resources.Load<Texture2D>(Root+key));
            if(error==null)foreach(var sprite in manifest.sprites)images.Add(sprite.key,new Image(sprite,Resources.Load<Texture2D>(Root+sprite.sheet)));
            else Debug.LogError("Missing/invalid campaign cartoon art: "+error);
            return error;
        }
        public static string Validate(Manifest manifest,Func<string,Texture2D> load) {
            if(manifest==null||manifest.sheets==null||manifest.sheets.Length!=2||manifest.sprites==null||manifest.sprites.Length!=Required.Length)return "catalog.json";
            var sheets=new Dictionary<string,Sheet>();
            foreach(var sheet in manifest.sheets) {
                if(sheet==null||sheet.key==null||sheets.ContainsKey(sheet.key))return "sheet metadata";
                bool scenery=sheet.key=="scenery",actors=sheet.key=="actors";
                if(!scenery&&!actors||sheet.columns!=4||sheet.rows!=(scenery?4:2)||sheet.width!=(scenery?1254:1774)||sheet.height!=(scenery?1254:887))return sheet.key+" (dimensions metadata)";
                var texture=load(sheet.key);
                if(texture==null||texture.width!=sheet.width||texture.height!=sheet.height||texture.format!=TextureFormat.RGBA32)return sheet.key+" (PNG/RGBA32)";
                sheets.Add(sheet.key,sheet);
            }
            var keys=new HashSet<string>();var cells=new HashSet<string>();
            for(int i=0;i<Required.Length;i++) {
                var sprite=manifest.sprites[i];Sheet sheet;
                if(sprite==null||sprite.key!=Required[i]||!keys.Add(sprite.key)||sprite.sheet==null||!sheets.TryGetValue(sprite.sheet,out sheet))return Required[i]+" (mapping)";
                if(sprite.cell<0||sprite.cell>=sheet.columns*sheet.rows||!cells.Add(sprite.sheet+":"+sprite.cell)||sprite.width<2||sprite.height<2)return sprite.key+" (cell)";
                float left=(sprite.cell%sheet.columns)*sheet.width/(float)sheet.columns,top=(sprite.cell/sheet.columns)*sheet.height/(float)sheet.rows;
                if(sprite.x<left||sprite.y<top||sprite.x+sprite.width>left+sheet.width/(float)sheet.columns||sprite.y+sprite.height>top+sheet.height/(float)sheet.rows)return sprite.key+" (crop outside cell)";
            }
            return null;
        }
        public static Image Get(string key) {Preload();Image image;return images.TryGetValue(key,out image)?image:null;}
        public static void Draw(Rect rect,string key) {
            var image=Get(key);if(image==null)return;
            float width=Mathf.Min(rect.width,rect.height*image.Aspect),height=width/image.Aspect;
            GUI.DrawTextureWithTexCoords(new Rect(rect.center.x-width/2,rect.center.y-height/2,width,height),image.Texture,image.Uv);
        }
        public static void Reset(){images.Clear();checkedAssets=false;error=null;}
    }
}
