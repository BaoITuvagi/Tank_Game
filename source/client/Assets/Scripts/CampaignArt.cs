using System.Collections.Generic;
using UnityEngine;

namespace Tank.Campaign {
    // Owns presentation assets exclusively for campaign. Never changes shared textures.
    public static partial class CampaignArt {
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        static readonly string[] pickups={"pickup_health","pickup_damage","pickup_shield","pickup_gold"};
        public static Texture2D Texture(string name) {
            Texture2D result;
            if(!textures.TryGetValue(name,out result)) {
                result=Resources.Load<Texture2D>("CampaignArt/"+name);
                textures[name]=result;
                if(result==null)Debug.LogError("Missing campaign art: "+name);
            }
            return result;
        }
        static void Draw(Rect rect,string name,ScaleMode mode=ScaleMode.ScaleToFit) {
            var texture=Texture(name);
            if(texture!=null)GUI.DrawTexture(rect,texture,mode);
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
        public static Rect VehicleUV(int facing){return facing==3?new Rect(1,0,-1,1):new Rect(0,0,1,1);}
        public static float VehicleRotation(int facing){return facing==3?0:facing*90-90;}
        static void Vehicle(Rect rect,string name,int facing) {
            float size=Mathf.Min(rect.width,rect.height);
            Rect sprite=new Rect(rect.center.x-size/2,rect.center.y-size/2,size,size);
            Matrix4x4 previous=GUI.matrix;
            // Left uses horizontal mirroring, preserving the tracks and shadow at the bottom.
            GUIUtility.RotateAroundPivot(VehicleRotation(facing),sprite.center);
            var texture=Texture(name);
            if(texture!=null)GUI.DrawTextureWithTexCoords(sprite,texture,VehicleUV(facing));
            GUI.matrix=previous;
        }
        public static void Tank(Rect rect,int nation,int facing) {
            Vehicle(rect,nation==0?"tank_usa":nation==1?"tank_ussr":"tank_italy",facing);
        }
        public static string EnemyAsset(int boss,EnemyRole role){return boss>0?"boss_"+Mathf.Clamp(boss,1,4):role==EnemyRole.Ranged?"enemy_ranged":role==EnemyRole.Support?"enemy_support":"enemy";}
        public static void Enemy(Rect rect,int boss,int facing,EnemyRole role=EnemyRole.Melee) {
            Vehicle(rect,EnemyAsset(boss,role),facing);
        }
        public static void Pickup(Rect rect,int kind) {
            if(kind<0||kind>=pickups.Length)return;
            float size=Mathf.Min(rect.width,rect.height)*(kind==3?.9f:.75f);
            Rect icon=new Rect(rect.center.x-size/2,rect.center.y-size/2,size,size);
            if(kind==3){
                var coin=Texture(pickups[kind]);if(coin==null)return;
                int frames=Mathf.Max(1,coin.width/coin.height),frame=(int)(Time.unscaledTimeAsDouble*10)%frames;
                GUI.DrawTextureWithTexCoords(icon,coin,new Rect(frame/(float)frames,0,1f/frames,1));
            }else Draw(icon,pickups[kind]);
        }
    }
}
