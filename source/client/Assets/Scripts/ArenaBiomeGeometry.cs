using System.Collections.Generic;
using UnityEngine;
namespace Tank {
    public enum ArenaBiome {Green,Desert,Snow}
    /// <summary>Decorative geometry only. These surfaces never enter movement or collision.</summary>
    public static class ArenaBiomeGeometry {
        public struct Prop {
            public Rect Bounds;
            public string Key;
            public bool OnObstacle;
            public Prop(Rect bounds,string key,bool obstacle=false) {Bounds=bounds;Key=key;OnObstacle=obstacle;}
        }
        public static ArenaBiome BiomeFor(int mapId) {return mapId==MapCatalog.FortressId?ArenaBiome.Desert:mapId==MapCatalog.RiverId?ArenaBiome.Snow:ArenaBiome.Green;}
        static Rect Scaled(GameMap map,float x,float y,float w,float h) {return new Rect(x*map.width/120,y*map.height/80,w*map.width/120,h*map.height/80);}
        public static Rect[] Routes(GameMap map) {
            var biome=BiomeFor(map.mapId);
            if(biome==ArenaBiome.Green)return new Rect[0];
            var routes=new List<Rect>();
            if(biome==ArenaBiome.Desert) {
                // A cross through the four fortress gates replaces the green map's ring road.
                routes.Add(Scaled(map,14,37,92,6));
                routes.Add(Scaled(map,57,5,6,70));
                routes.Add(Scaled(map,28,10,29,5));routes.Add(Scaled(map,63,65,29,5));
            } else {
                // Short settlement trails stop at the lake shore; the ice itself is the fast route.
                routes.Add(Scaled(map,78,55,30,2.5f));routes.Add(Scaled(map,95,16,3,48));
                routes.Add(Scaled(map,75,18,22,3));routes.Add(Scaled(map,25,55,11,3));
            }
            foreach(var water in map.water)routes=Subtract(routes,new Rect(water.x,water.y,water.w,water.h));
            foreach(var wall in map.walls)routes=Subtract(routes,new Rect(wall.x,wall.y,wall.w,wall.h));
            return routes.ToArray();
        }
        public static Rect[] Bridges(GameMap map) {
            if(map==null||map.mapId!=MapCatalog.DefaultId)return new Rect[0];
            // The bridge decks mark authored dry gaps; they never create or remove terrain collision.
            return new[]{Scaled(map,54.5f,35.5f,11,9)};
        }
        static List<Rect> Subtract(List<Rect> source,Rect cut) {
            var result=new List<Rect>();
            foreach(var area in source) {
                if(!area.Overlaps(cut)) {result.Add(area);continue;}
                float left=Mathf.Max(area.xMin,cut.xMin),right=Mathf.Min(area.xMax,cut.xMax),bottom=Mathf.Max(area.yMin,cut.yMin),top=Mathf.Min(area.yMax,cut.yMax);
                Add(result,Rect.MinMaxRect(area.xMin,area.yMin,left,area.yMax));
                Add(result,Rect.MinMaxRect(right,area.yMin,area.xMax,area.yMax));
                Add(result,Rect.MinMaxRect(left,area.yMin,right,bottom));
                Add(result,Rect.MinMaxRect(left,top,right,area.yMax));
            }
            return result;
        }
        static void Add(List<Rect> destination,Rect rect) {if(rect.width>.01f&&rect.height>.01f)destination.Add(rect);}
        public static Prop[] Props(GameMap map,Rect[] routes) {
            var biome=BiomeFor(map.mapId);var props=new List<Prop>();
            if(biome==ArenaBiome.Green)return props.ToArray();
            string flat=biome==ArenaBiome.Desert?"desert_dune":"snow_drift";
            // Ground marks remain away from spawns, pickups and the main routes.
            float[,] spots={{19,8},{101,72},{105,8},{15,72},{7,52},{113,28},{36,68},{84,10},{44,5},{76,75}};
            for(int i=0;i<spots.GetLength(0);i++) {
                var bounds=Scaled(map,spots[i,0]-2.2f,spots[i,1]-1.2f,4.4f,2.4f);
                if(ClearGround(map,bounds,routes))props.Add(new Prop(bounds,flat));
            }
            if(biome==ArenaBiome.Snow)return props.ToArray();
            // Plants and rocks occupy real solid cover, with their entire silhouette inside it.
            // The decorative image can therefore never imply a new obstacle in an open lane.
            for(int i=4;i<map.walls.Length;i+=2) {
                var wall=map.walls[i];if(wall.w<2.5f||wall.h<2.5f||ArenaEnvironmentGeometry.IsSceneryWall(map,wall.id))continue;
                float size=Mathf.Min(2.4f,Mathf.Min(wall.w,wall.h)-.3f);
                var bounds=new Rect(wall.x+(wall.w-size)/2,wall.y+(wall.h-size)/2,size,size);
                string key=biome==ArenaBiome.Desert?(i%3==0?"desert_cactus":i%3==1?"desert_rock":"desert_scrub"):(i%3==0?"snow_pine":i%3==1?"snow_rock":"ice_shards");
                props.Add(new Prop(bounds,key,true));
            }
            return props.ToArray();
        }
        static bool ClearGround(GameMap map,Rect bounds,Rect[] routes) {
            if(bounds.xMin<2||bounds.yMin<2||bounds.xMax>map.width-2||bounds.yMax>map.height-2)return false;
            foreach(var r in map.water)if(bounds.Overlaps(new Rect(r.x,r.y,r.w,r.h)))return false;
            foreach(var r in map.walls)if(bounds.Overlaps(new Rect(r.x,r.y,r.w,r.h)))return false;
            foreach(var r in map.bricks)if(bounds.Overlaps(new Rect(r.x,r.y,r.w,r.h)))return false;
            foreach(var r in routes)if(bounds.Overlaps(r))return false;
            Rect clearance=new Rect(bounds.x-2,bounds.y-2,bounds.width+4,bounds.height+4);
            foreach(var p in map.spawns)if(clearance.Contains(new Vector2(p.x,p.y)))return false;
            foreach(var p in map.itemSpawns)if(clearance.Contains(new Vector2(p.x,p.y)))return false;
            return true;
        }
    }
}
