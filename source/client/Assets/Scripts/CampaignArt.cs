using System.Collections.Generic;
using UnityEngine;

namespace Tank.Campaign {
    // Campaign uses the same top-down vehicle and terrain language as multiplayer.
    public static partial class CampaignArt {
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        static readonly string[] pickups={"pickup_health","pickup_damage","pickup_shield","pickup_gold"};
        public static Texture2D Texture(string name) {
            Texture2D result;
            if(!textures.TryGetValue(name,out result)) {
                result=Resources.Load<Texture2D>("ArenaArt/"+name);
                textures[name]=result;
                if(result==null)Debug.LogError("Missing shared arena art: "+name);
            }
            return result;
        }
        static void Draw(Rect rect,string name,ScaleMode mode=ScaleMode.ScaleToFit) {
            if(name=="grass"||name=="grass_detail"||name=="grass_flowers") {
                var texture=Texture(name=="grass"?"tileGrass1":"tileGrass2");
                if(texture!=null)GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill);
            }else if(name=="crate") {
                var texture=Texture("crateMetal");if(texture!=null)GUI.DrawTexture(rect,texture,mode);
            }else CampaignCartoonCatalog.Draw(rect,name=="ruins"?"rubble":name);
        }
        static uint Noise(int x,int y){unchecked{uint h=(uint)x*374761393u+(uint)y*668265263u;h=(h^(h>>13))*1274126177u;return h^(h>>16);}}
        public static Rect GroundBounds(Rect rect){return Rect.MinMaxRect(Mathf.Floor(rect.xMin),Mathf.Floor(rect.yMin),Mathf.Ceil(rect.xMax)+1,Mathf.Ceil(rect.yMax)+1);}
        public static void Ground(Rect rect,int x,int y,int terrain) {
            uint variant=Noise(x,y);
            Draw(GroundBounds(rect),variant%43==0?"grass_flowers":variant%5==0?"grass_detail":"grass",ScaleMode.StretchToFill);
            if(terrain==1)Draw(rect,(variant&1)==0?"trees":"tree");
            else if(terrain==2)Draw(rect,"rock");
            else if(terrain==3)Draw(rect,"crate");
            else if(terrain==4)Draw(rect,"ruins");
        }
        public static void Surroundings(Rect rect,int x,int y) {
            uint variant=Noise(x,y);
            Draw(GroundBounds(rect),variant%5==0?"grass_detail":"grass",ScaleMode.StretchToFill);
            if(variant%4==0)Draw(rect,(variant&16)==0?"trees":"tree");
        }
        public static Rect VehicleUV(int facing){return new Rect(0,0,1,1);}
        public static float VehicleRotation(int facing){return (facing&3)*90;}
        static void Vehicle(Rect rect,string name,int facing) {
            float size=Mathf.Min(rect.width,rect.height);
            Rect sprite=new Rect(rect.center.x-size/2,rect.center.y-size/2,size,size);
            Matrix4x4 previous=GUI.matrix;
            try {GuiTransforms.Rotate(VehicleRotation(facing),sprite.center);CampaignCartoonCatalog.Draw(sprite,name);}
            finally {GUI.matrix=previous;}
        }
        public static void Tank(Rect rect,int nation,int facing) {
            int palette=nation==0?0:nation==1?1:5;float size=Mathf.Min(rect.width,rect.height)*.78f;
            Rect body=new Rect(rect.center.x-size/2,rect.center.y-size/2,size,size);
            Rect barrel=new Rect(rect.center.x-size*.112f,rect.center.y-size*.618f,size*.224f,size*.694f);
            var saved=GUI.matrix;Color color=GUI.color;
            try {
                GuiTransforms.Rotate(VehicleRotation(facing),rect.center);
                GUI.color=new Color(0,0,0,color.a*.28f);GUI.DrawTexture(new Rect(body.x+1.5f,body.y+2,body.width,body.height),TankPalette.Body(palette),ScaleMode.ScaleToFit);
                GUI.color=color;GUI.DrawTexture(body,TankPalette.Body(palette),ScaleMode.ScaleToFit);GUI.DrawTexture(barrel,TankPalette.Barrel(palette),ScaleMode.StretchToFill);
            }finally {GUI.matrix=saved;GUI.color=color;}
        }
        public static string EnemyAsset(int boss,EnemyRole role){return boss>0?"boss_"+Mathf.Clamp(boss,1,4):role==EnemyRole.Ranged?"enemy_ranged":role==EnemyRole.Support?"enemy_support":"enemy";}
        public static void Enemy(Rect rect,int boss,int facing,EnemyRole role=EnemyRole.Melee) {
            float inset=Mathf.Min(rect.width,rect.height)*(boss>0?.025f:.10f);
            Vehicle(new Rect(rect.x+inset,rect.y+inset,rect.width-inset*2,rect.height-inset*2),EnemyAsset(boss,role),facing);
        }
        public static void Pickup(Rect rect,int kind) {
            if(kind<0||kind>=pickups.Length)return;
            float size=Mathf.Min(rect.width,rect.height)*(kind==3?.9f:.75f);
            Rect icon=new Rect(rect.center.x-size/2,rect.center.y-size/2,size,size);
            if(kind==3){ItemIconArt.DrawGold(icon);return;}
            Color color=kind==0?CartoonUi.Ready:kind==1?CartoonUi.Danger:kind==2?new Color(.65f,.85f,1):CartoonUi.Gold;
            CartoonUi.Panel(icon,CartoonUi.Ink,6);Rect face=new Rect(icon.x+2,icon.y+2,icon.width-4,icon.height-4);CartoonUi.Panel(face,color,5);
            Rect symbol=new Rect(face.x+face.width*.18f,face.y+face.height*.18f,face.width*.64f,face.height*.64f);
            CartoonUi.Icon(symbol,kind==0?UiIcon.Health:kind==1?UiIcon.Damage:UiIcon.Armor,CartoonUi.Ink);
        }
    }
}
