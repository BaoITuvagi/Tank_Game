using UnityEngine;

namespace Tank.Campaign {
    public static partial class CampaignArt {
        static readonly string[] scenery={null,"grass_flowers","flag","building_house","building_hangar","building_factory","building_tower","rubble","parked_truck","supplies","ruin_house","ruin_factory","ruin_tower","warning","parked_tank"};
        // Whole original tiles, in N/E/S/W bitmask order. No quadrant compositing.
        static readonly string[] roads={"road_isolated","road_end_n","road_end_e","road_ne","road_end_s","road_ns","road_se","road_t_nse","road_end_w","road_nw","road_ew","road_t_new","road_sw","road_t_nsw","road_t_esw","road_cross"};
        public static string RoadAsset(int mask){return roads[mask&15];}
        public static string SurfaceAsset(int surface,int connections,int shores){
            if(surface==3)return (connections&10)!=0&&(connections&5)==0?"bridge_ew":"bridge_ns";
            if(surface==2)return WaterAsset(shores);
            if(surface==1)return RoadAsset(connections);
            return surface==4?"grass_detail":"grass";
        }
        // Shores: N/E/S/W = 1/2/4/8, diagonal land NW/NE/SE/SW = 16/32/64/128.
        // Diagonal corners matter only when both touching cardinal neighbors are water.
        public static string WaterAsset(int shores){
            int sides=shores&15;
            switch(sides){
                case 1:return "water_n";case 2:return "water_e";case 4:return "water_s";case 8:return "water_w";
                case 3:return "water_ne";case 9:return "water_nw";case 6:return "water_se";case 12:return "water_sw";
                case 5:return "water_channel_ew";case 10:return "water_channel_ns";
            }
            switch(shores&240){
                case 16:return "water_inner_nw";case 32:return "water_inner_ne";
                case 64:return "water_inner_se";case 128:return "water_inner_sw";
                case 48:return "water_inner_n";case 96:return "water_inner_e";
                case 192:return "water_inner_s";case 144:return "water_inner_w";
                default:return "water";
            }
        }
        public static string SceneryAsset(int kind){return kind>0&&kind<scenery.Length?scenery[kind]:null;}
        public static void EnvironmentGround(Rect rect,int x,int y,int surface,int roads,int shores){
            uint variant=Noise(x,y);Rect ground=GroundBounds(rect);
            Draw(ground,variant%5==0?"grass_detail":"grass",ScaleMode.StretchToFill);
            if(surface==1)DrawRoad(ground,roads);
            else if(surface==2||surface==3){DrawWater(ground,x,y,shores);if(surface==3)DrawBridge(ground,(roads&10)!=0&&(roads&5)==0);}
            else if(surface==4||surface==5){
                var texture=Texture(surface==4?(variant%5==0?"tileSand2":"tileSand1"):"tileSand2");
                Color saved=GUI.color;if(surface==5)GUI.color=saved*new Color(.68f,.73f,.72f);
                if(texture!=null)GUI.DrawTexture(ground,texture,ScaleMode.StretchToFill);GUI.color=saved;
                if(surface==5){float edge=Mathf.Max(1,rect.width*.025f);EnvironmentFill(new Rect(rect.x,rect.y,rect.width,edge),new Color(.15f,.22f,.21f,.15f));EnvironmentFill(new Rect(rect.x,rect.y,edge,rect.height),new Color(.15f,.22f,.21f,.15f));}
            }
        }
        public static void EnvironmentObject(Rect rect,int x,int y,int terrain,int surface,int decoration){
            if(surface==2)return;
            string name=SceneryAsset(decoration);
            if(decoration==1){DrawFlowers(rect,x,y);return;}
            if(name!=null){Draw(rect,name);return;}
            uint variant=Noise(x,y);
            if(terrain==1)Draw(rect,(variant&1)==0?"trees":"tree");
            else if(terrain==2)Draw(rect,"rock");else if(terrain==3)Draw(rect,"crate");else if(terrain==4)Draw(rect,"ruins");
        }
        public static void Surroundings(Rect rect,int x,int y,int biome){
            if(biome==0){Surroundings(rect,x,y);return;}
            uint variant=Noise(x,y);EnvironmentGround(rect,x,y,biome==1?4:biome==3?5:0,0,0);
            if(variant%5==0)Draw(rect,biome==1?"rock":biome==2?(variant%2==0?"building_hangar":"flag"):(variant%2==0?"ruin_factory":"rubble"));
        }
        static void EnvironmentFill(Rect rect,Color color) {
            Color saved=GUI.color;
            try {ArenaRenderer.Fill(rect,color*saved);}
            finally {GUI.color=saved;}
        }
        static void RoadArms(Rect rect,int mask,float ratio,Color color) {
            float width=rect.width*ratio,height=rect.height*ratio;
            EnvironmentFill(new Rect(rect.center.x-width/2,rect.center.y-height/2,width,height),color);
            if((mask&1)!=0)EnvironmentFill(new Rect(rect.center.x-width/2,rect.y,width,rect.height/2),color);
            if((mask&2)!=0)EnvironmentFill(new Rect(rect.center.x,rect.center.y-height/2,rect.width/2,height),color);
            if((mask&4)!=0)EnvironmentFill(new Rect(rect.center.x-width/2,rect.center.y,width,rect.height/2),color);
            if((mask&8)!=0)EnvironmentFill(new Rect(rect.x,rect.center.y-height/2,rect.width/2,height),color);
        }
        // Use the multiplayer road's outline and palette for all sixteen connectivity masks.
        static void DrawRoad(Rect rect,int mask) {
            mask&=15;RoadArms(rect,mask,.75f,new Color(.50f,.65f,.55f));RoadArms(rect,mask,.61f,new Color(.66f,.72f,.72f));
            Color stripe=new Color(.85f,.88f,.82f,.72f);float line=Mathf.Max(1,rect.width*.035f);
            if((mask&1)!=0)EnvironmentFill(new Rect(rect.center.x-line/2,rect.y+rect.height*.08f,line,rect.height*.20f),stripe);
            if((mask&2)!=0)EnvironmentFill(new Rect(rect.x+rect.width*.72f,rect.center.y-line/2,rect.width*.20f,line),stripe);
            if((mask&4)!=0)EnvironmentFill(new Rect(rect.center.x-line/2,rect.y+rect.height*.72f,line,rect.height*.20f),stripe);
            if((mask&8)!=0)EnvironmentFill(new Rect(rect.x+rect.width*.08f,rect.center.y-line/2,rect.width*.20f,line),stripe);
        }
        static void DrawWater(Rect rect,int x,int y,int shores) {
            var dark=new Color(.11f,.25f,.25f);var light=new Color(.4f,.78f,.8f);float edge=Mathf.Max(2,rect.width*.07f);
            EnvironmentFill(rect,new Color(.13f,.48f,.62f));
            if((shores&1)!=0){EnvironmentFill(new Rect(rect.x,rect.y,rect.width,edge),dark);EnvironmentFill(new Rect(rect.x,rect.y+edge,rect.width,1),light);}
            if((shores&2)!=0){EnvironmentFill(new Rect(rect.xMax-edge,rect.y,edge,rect.height),dark);EnvironmentFill(new Rect(rect.xMax-edge-1,rect.y,1,rect.height),light);}
            if((shores&4)!=0){EnvironmentFill(new Rect(rect.x,rect.yMax-edge,rect.width,edge),dark);EnvironmentFill(new Rect(rect.x,rect.yMax-edge-1,rect.width,1),light);}
            if((shores&8)!=0){EnvironmentFill(new Rect(rect.x,rect.y,edge,rect.height),dark);EnvironmentFill(new Rect(rect.x+edge,rect.y,1,rect.height),light);}
            int[] adjacent={9,3,6,12};
            for(int i=0;i<4;i++)if((shores&(16<<i))!=0&&(shores&adjacent[i])==0) {
                bool right=i==1||i==2,bottom=i>=2;
                float px=right?rect.xMax-edge*1.6f:rect.x,py=bottom?rect.yMax-edge*1.6f:rect.y;
                EnvironmentFill(new Rect(px,py,edge*1.6f,edge*1.6f),light);
                EnvironmentFill(new Rect(right?rect.xMax-edge:rect.x,bottom?rect.yMax-edge:rect.y,edge,edge),dark);
            }
            float phase=Mathf.Sin(Time.unscaledTime*1.2f)*.025f,seed=(Noise(x,y)%9)/100f;
            for(int i=0;i<2;i++) {
                float px=rect.x+rect.width*(.20f+i*.32f+phase),py=rect.y+rect.height*(.32f+i*.33f+seed);
                EnvironmentFill(new Rect(px,py,rect.width*.20f,Mathf.Max(1,rect.height*.02f)),new Color(.64f,.9f,.91f,.28f));
            }
        }
        static void DrawBridge(Rect rect,bool horizontal) {
            var saved=GUI.matrix;try {
                if(horizontal)GuiTransforms.Rotate(90,rect.center);
                EnvironmentFill(new Rect(rect.x+rect.width*.15f,rect.y,rect.width*.70f,rect.height),CartoonUi.Ink);
                for(int i=0;i<6;i++)EnvironmentFill(new Rect(rect.x+rect.width*.19f,rect.y+i*rect.height/6+1,rect.width*.62f,Mathf.Max(1,rect.height/6-2)),new Color(.68f,.58f,.39f));
                EnvironmentFill(new Rect(rect.x+rect.width*.12f,rect.y,Mathf.Max(2,rect.width*.07f),rect.height),new Color(.82f,.75f,.55f));
                EnvironmentFill(new Rect(rect.x+rect.width*.81f,rect.y,Mathf.Max(2,rect.width*.07f),rect.height),new Color(.82f,.75f,.55f));
            }finally{GUI.matrix=saved;}
        }
        static void DrawFlowers(Rect rect,int x,int y) {
            uint noise=Noise(x,y);
            for(int i=0;i<3;i++) {
                float px=rect.x+rect.width*(.23f+((noise>>(i*4))&7)*.07f),py=rect.y+rect.height*(.22f+i*.24f),size=Mathf.Max(2,rect.width*.07f);
                EnvironmentFill(new Rect(px,py,size,size),i==1?CartoonUi.Gold:CartoonUi.Cream);
            }
        }
    }
}
