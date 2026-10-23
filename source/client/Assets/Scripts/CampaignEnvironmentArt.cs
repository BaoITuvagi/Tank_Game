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
            Draw(ground,variant%43==0?"grass_flowers":variant%5==0?"grass_detail":"grass",ScaleMode.StretchToFill);
            if(surface==1||surface==2||surface==3){
                // Bridge artwork has its original size and rails; water is the underlay.
                if(surface==3)Draw(ground,SurfaceAsset(2,0,shores),ScaleMode.StretchToFill);
                Draw(ground,SurfaceAsset(surface,roads,shores),ScaleMode.StretchToFill);
            }else if(surface==4||surface==5)Draw(ground,SurfaceAsset(surface,0,0),ScaleMode.StretchToFill);
        }
        public static void EnvironmentObject(Rect rect,int x,int y,int terrain,int surface,int decoration){
            if(surface==2)return;
            string name=SceneryAsset(decoration);
            if(name!=null){Draw(rect,name,decoration==13?ScaleMode.StretchToFill:ScaleMode.ScaleToFit);return;}
            uint variant=Noise(x,y);
            if(terrain==1)Draw(rect,(variant&1)==0?"trees":"tree");
            else if(terrain==2)Draw(rect,"rock");else if(terrain==3)Draw(rect,"crate");else if(terrain==4)Draw(rect,"ruins");
        }
        public static void Surroundings(Rect rect,int x,int y,int biome){
            if(biome==0){Surroundings(rect,x,y);return;}
            uint variant=Noise(x,y);EnvironmentGround(rect,x,y,biome==1?4:biome==3?5:0,0,0);
            if(variant%5==0)Draw(rect,biome==1?"rock":biome==2?(variant%2==0?"building_hangar":"flag"):(variant%2==0?"ruin_factory":"rubble"));
        }
    }
}
