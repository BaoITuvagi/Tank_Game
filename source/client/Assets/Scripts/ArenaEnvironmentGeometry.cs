using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tank {
    /// <summary>Cached presentation geometry only; never changes the map's collision data.</summary>
    public static class ArenaEnvironmentGeometry {
        public struct Edge {
            public Vector2 A,B;
            // Outward side in world coordinates: north (+Y), east, south, west.
            public int Side;
            public Edge(Vector2 a,Vector2 b,int side) {A=a;B=b;Side=side;}
        }
        public struct Corner {
            public Vector2 Position;
            // Occupied adjacent quadrants: NE=1, SE=2, SW=4, NW=8.
            public int Mask;
            public Corner(Vector2 position,int mask) {Position=position;Mask=mask;}
        }
        public sealed class Surface {
            public readonly Rect[] Cells;
            public readonly Edge[] Edges;
            public readonly Corner[] Corners;
            public Surface(Rect[] cells,Edge[] edges,Corner[] corners) {Cells=cells;Edges=edges;Corners=corners;}
        }
        public struct Prop {
            public Rect Bounds;
            public string Key;
            public bool Campaign;
            public bool HideWallBase;
            public int WallId;
            public Prop(Rect bounds,string key,bool campaign,int wallId,bool hideWallBase=false) {Bounds=bounds;Key=key;Campaign=campaign;WallId=wallId;HideWallBase=hideWallBase;}
        }
        // IDs follow the authored root colliders in map-large.json. Four small scenery
        // groups leave the combat lanes open; varied canopy sizes keep them from
        // reading as a row of identical, isolated props.
        static readonly string[] GreenSceneryKeys={
            "building_tower","tree","tree","rock",
            "tree","trees","rock","tree",
            "trees","tree","tree","rock",
            "tree","trees","rock","tree"
        };
        static readonly float[] GreenScenerySizes={4,4.2f,4,3.2f,4.4f,4.4f,3.2f,4,4.4f,4,4.2f,3.2f,4.2f,4.4f,3.2f,4};
        // Authored winter roots follow the forest groves and two small igloo settlements.
        static readonly string[] WinterSceneryKeys={"snow_pine","snow_pine","snow_pine","snow_pine","snow_rock","snow_pine","snow_pine","snow_pine","snow_pine","snow_rock","snow_pine","snow_pine","snow_pine","snow_pine","snow_rock","snow_pine","snow_pine","snow_pine","snow_pine","snow_rock","snow_pine","snow_pine","snow_pine","snow_pine","snow_rock","snow_pine","snow_pine","snow_pine","snow_pine","snow_rock","igloo","igloo","snow_pine","snow_pine","snow_rock","igloo","igloo","snow_pine","snow_pine","snow_rock","ice_shards","ice_shards","ice_shards","ice_shards"};
        static readonly float[] WinterScenerySizes={4.6f,5.0f,4.2f,4.6f,3.6f,4.6f,5.0f,4.2f,4.6f,3.6f,4.6f,5.0f,4.2f,4.6f,3.6f,4.6f,5.0f,4.2f,4.6f,3.6f,4.6f,5.0f,4.2f,4.6f,3.6f,4.6f,5.0f,4.2f,4.6f,3.6f,7.2f,6.8f,4.8f,4.4f,3.6f,7.2f,6.8f,4.8f,4.4f,3.6f,3.4f,3.4f,3.4f,3.4f};
        static bool Finite(float value) {return !float.IsNaN(value)&&!float.IsInfinity(value);}
        static bool Valid(MapRect r) {return r!=null&&r.w>0&&r.h>0&&Finite(r.x)&&Finite(r.y)&&Finite(r.x+r.w)&&Finite(r.y+r.h);}
        static bool Occupied(bool[,] grid,int x,int y) {return x>=0&&y>=0&&x<grid.GetLength(0)&&y<grid.GetLength(1)&&grid[x,y];}

        public static Surface Build(MapRect[] rects) {
            var source=new List<MapRect>();var xs=new SortedSet<float>();var ys=new SortedSet<float>();
            if(rects!=null)foreach(var r in rects)if(Valid(r)) {
                source.Add(r);xs.Add(r.x);xs.Add(r.x+r.w);ys.Add(r.y);ys.Add(r.y+r.h);
            }
            if(source.Count==0)return new Surface(new Rect[0],new Edge[0],new Corner[0]);
            var x=new float[xs.Count];xs.CopyTo(x);var y=new float[ys.Count];ys.CopyTo(y);
            var occupied=new bool[x.Length-1,y.Length-1];var cells=new List<Rect>();
            // Endpoints partition every overlap and T junction, including half-unit terrain.
            for(int iy=0;iy<y.Length-1;iy++)for(int ix=0;ix<x.Length-1;ix++) {
                float cx=x[ix]+(x[ix+1]-x[ix])/2,cy=y[iy]+(y[iy+1]-y[iy])/2;
                foreach(var r in source)if(r.Contains(cx,cy)) {
                    occupied[ix,iy]=true;cells.Add(Rect.MinMaxRect(x[ix],y[iy],x[ix+1],y[iy+1]));break;
                }
            }
            var edges=new List<Edge>();
            for(int iy=0;iy<y.Length-1;iy++)for(int ix=0;ix<x.Length-1;ix++)if(occupied[ix,iy]) {
                if(!Occupied(occupied,ix,iy+1))edges.Add(new Edge(new Vector2(x[ix],y[iy+1]),new Vector2(x[ix+1],y[iy+1]),0));
                if(!Occupied(occupied,ix+1,iy))edges.Add(new Edge(new Vector2(x[ix+1],y[iy]),new Vector2(x[ix+1],y[iy+1]),1));
                if(!Occupied(occupied,ix,iy-1))edges.Add(new Edge(new Vector2(x[ix],y[iy]),new Vector2(x[ix+1],y[iy]),2));
                if(!Occupied(occupied,ix-1,iy))edges.Add(new Edge(new Vector2(x[ix],y[iy]),new Vector2(x[ix],y[iy+1]),3));
            }
            edges.Sort(CompareEdges);
            var merged=new List<Edge>();
            foreach(var edge in edges) {
                int last=merged.Count-1;
                if(last>=0&&merged[last].Side==edge.Side&&merged[last].B==edge.A) {
                    var previous=merged[last];previous.B=edge.B;merged[last]=previous;
                } else merged.Add(edge);
            }
            var corners=new List<Corner>();
            for(int iy=0;iy<y.Length;iy++)for(int ix=0;ix<x.Length;ix++) {
                int mask=(Occupied(occupied,ix,iy)?1:0)|(Occupied(occupied,ix,iy-1)?2:0)
                    |(Occupied(occupied,ix-1,iy-1)?4:0)|(Occupied(occupied,ix-1,iy)?8:0);
                // Exclude straight boundary subdivisions and fully interior vertices.
                if(mask!=0&&mask!=15&&mask!=3&&mask!=6&&mask!=9&&mask!=12)corners.Add(new Corner(new Vector2(x[ix],y[iy]),mask));
            }
            return new Surface(cells.ToArray(),merged.ToArray(),corners.ToArray());
        }
        static int CompareEdges(Edge a,Edge b) {
            int order=a.Side.CompareTo(b.Side);if(order!=0)return order;
            bool horizontal=a.Side%2==0;
            order=(horizontal?a.A.y:a.A.x).CompareTo(horizontal?b.A.y:b.A.x);
            return order!=0?order:(horizontal?a.A.x:a.A.y).CompareTo(horizontal?b.A.x:b.A.y);
        }

        public static Prop[] Scenery(GameMap map) {
            var props=new List<Prop>();
            if(map==null||map.walls==null||!MapCatalog.IsSelectable(map.mapId))return props.ToArray();
            if(map.mapId==MapCatalog.DefaultId) {
                // Only roots collide. The larger canopies can overlap their neighbors
                // within a group, but never steel cover, roads, ponds or brick cover.
                for(int i=0;i<GreenSceneryKeys.Length;i++)AddStandalone(props,map,16+i,GreenSceneryKeys[i],true,GreenScenerySizes[i]);
                return props.ToArray();
            }
            if(map.mapId==MapCatalog.RiverId) {
                for(int i=0;i<WinterSceneryKeys.Length;i++)AddStandalone(props,map,16+i,WinterSceneryKeys[i],false,WinterScenerySizes[i]);
                return props.ToArray();
            }
            // Opposite quarters first keep the landmarks balanced around the perimeter.
            var anchors=new[]{new Vector2(.2f,.22f),new Vector2(.8f,.78f),new Vector2(.8f,.22f),new Vector2(.2f,.78f)};
            var used=new HashSet<int>();var biome=ArenaBiomeGeometry.BiomeFor(map.mapId);
            for(int anchor=0;anchor<anchors.Length;anchor++) {
                int selected=-1;float closest=float.PositiveInfinity;
                var target=new Vector2(anchors[anchor].x*map.width,anchors[anchor].y*map.height);
                for(int index=4;index<map.walls.Length;index++) {
                    var wall=map.walls[index];
                    if(!Valid(wall)||wall.w<3||wall.h<3||used.Contains(index)||IsSceneryWall(map,wall.id))continue;
                    var center=new Vector2(wall.x+wall.w/2,wall.y+wall.h/2);
                    float distance=(center-target).sqrMagnitude;
                    if(distance<closest) {selected=index;closest=distance;}
                }
                if(selected<0)continue;
                used.Add(selected);var chosen=map.walls[selected];
                const float size=2.8f;
                var bounds=new Rect(chosen.x+(chosen.w-size)/2,chosen.y+(chosen.h-size)/2,size,size);
                bool campaign=anchor==0||biome==ArenaBiome.Green;
                string key=anchor==0?"building_tower":anchor==1
                    ?(biome==ArenaBiome.Green?"rock":biome==ArenaBiome.Desert?"desert_rock":"snow_rock")
                    :biome==ArenaBiome.Green?(anchor==2?"tree":"trees")
                    :biome==ArenaBiome.Desert?(anchor==2?"desert_cactus":"desert_scrub"):"snow_pine";
                props.Add(new Prop(bounds,key,campaign,chosen.id));
            }
            if(map.mapId==MapCatalog.FortressId)for(int id=18;id<36;id++)AddStandalone(props,map,id,"oasis_palm",false,(id-18)%3==0?4.4f:4.2f);
            return props.ToArray();
        }
        static void AddStandalone(List<Prop> props,GameMap map,int id,string key,bool campaign,float size) {
            foreach(var wall in map.walls)if(wall.id==id&&Valid(wall)) {
                props.Add(new Prop(new Rect(wall.x+(wall.w-size)/2,wall.y+(wall.h-size)/2,size,size),key,campaign,id,true));
                return;
            }
        }
        public static bool IsSceneryWall(GameMap map,int wallId) {
            return map!=null&&(map.mapId==MapCatalog.DefaultId&&wallId>=16&&wallId<16+GreenSceneryKeys.Length
                ||map.mapId==MapCatalog.FortressId&&wallId>=18&&wallId<36
                ||map.mapId==MapCatalog.RiverId&&wallId>=16&&wallId<16+WinterSceneryKeys.Length);
        }
    }
}
