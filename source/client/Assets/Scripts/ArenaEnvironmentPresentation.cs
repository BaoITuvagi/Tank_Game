using System.Collections.Generic;
using Tank.Campaign;
using UnityEngine;

namespace Tank {
    public sealed partial class ArenaRenderer {
        struct MaterialPatch {
            public Rect Bounds,Uv;
            public MaterialPatch(Rect bounds,Rect uv) {Bounds=bounds;Uv=uv;}
        }
        struct WaterRipple {
            public Rect Bounds;
            public float Phase;
        }
        sealed class EnvironmentLayout {
            public ArenaEnvironmentGeometry.Surface Water,Walls;
            public ArenaEnvironmentGeometry.Prop[] Scenery;
            public MaterialPatch[] WaterPatches,WallPatches;
            public WaterRipple[] Ripples;
            public Rect[] Bridges;
            public ArenaEnvironmentArt.Image WaterArt,WallArt;
            public Texture2D IceArt;
        }
        readonly Dictionary<GameMap,EnvironmentLayout> environmentLayouts=new Dictionary<GameMap,EnvironmentLayout>();
        Texture2D waterRipple;

        EnvironmentLayout Environment(GameMap map) {
            EnvironmentLayout result;
            if(environmentLayouts.TryGetValue(map,out result))return result;
            var biome=ArenaBiomeGeometry.BiomeFor(map.mapId);
            var water=ArenaEnvironmentGeometry.Build(map.water);
            var visibleWalls=new List<MapRect>();
            foreach(var wall in map.walls)if(!ArenaEnvironmentGeometry.IsSceneryWall(map,wall.id))visibleWalls.Add(wall);
            var walls=ArenaEnvironmentGeometry.Build(visibleWalls.ToArray());
            result=new EnvironmentLayout {
                Water=water,Walls=walls,Scenery=ArenaEnvironmentGeometry.Scenery(map),
                WaterPatches=MaterialPatches(water.Cells,8),WallPatches=MaterialPatches(walls.Cells,5),
                Ripples=WaterRipples(water,map.width,map.height),
                Bridges=ArenaBiomeGeometry.Bridges(map),
                WaterArt=biome==ArenaBiome.Desert?ArenaEnvironmentArt.Get("water_desert"):null,
                IceArt=biome==ArenaBiome.Snow?ArenaTerrainArt.Get("ice"):null,
                WallArt=biome==ArenaBiome.Green?null:ArenaEnvironmentArt.Get(biome==ArenaBiome.Desert?"wall_sandstone":"wall_snow")
            };
            environmentLayouts.Add(map,result);return result;
        }
        static MaterialPatch[] MaterialPatches(Rect[] cells,float size) {
            var patches=new List<MaterialPatch>();
            // World anchoring maintains the same material phase across L/T joins and union cells.
            foreach(var cell in cells) {
                int left=Mathf.FloorToInt(cell.xMin/size),right=Mathf.CeilToInt(cell.xMax/size);
                int bottom=Mathf.FloorToInt(cell.yMin/size),top=Mathf.CeilToInt(cell.yMax/size);
                for(int row=bottom;row<top;row++)for(int col=left;col<right;col++) {
                    float wx=col*size,wy=row*size;
                    var clipped=Rect.MinMaxRect(Mathf.Max(wx,cell.xMin),Mathf.Max(wy,cell.yMin),Mathf.Min(wx+size,cell.xMax),Mathf.Min(wy+size,cell.yMax));
                    if(clipped.width<=0||clipped.height<=0)continue;
                    float u=(clipped.x-wx)/size,v=(clipped.y-wy)/size,uw=clipped.width/size,vh=clipped.height/size;
                    // Mirrored neighbors share the exact source edge, even for hand-painted patches.
                    if((col&1)!=0) {u=1-u;uw=-uw;}
                    if((row&1)!=0) {v=1-v;vh=-vh;}
                    patches.Add(new MaterialPatch(clipped,new Rect(u,v,uw,vh)));
                }
            }
            return patches.ToArray();
        }
        static WaterRipple[] WaterRipples(ArenaEnvironmentGeometry.Surface water,float width,float height) {
            var ripples=new List<WaterRipple>();
            for(int row=0;row<Mathf.CeilToInt(height/5);row++)for(int col=0;col<Mathf.CeilToInt(width/7);col++) {
                int seed=(row*73+col*31)%101;
                if(seed%3==0)continue;
                var bounds=new Rect(col*7+1+(seed%7)*.32f,row*5+1+(seed%5)*.4f,1.8f+(seed%4)*.35f,.55f);
                // Include the whole slow movement and bank clearance in the containment check.
                var clearance=new Rect(bounds.x-.5f,bounds.y-.5f,bounds.width+1,bounds.height+1);
                float area=0;
                foreach(var cell in water.Cells) {
                    float w=Mathf.Min(cell.xMax,clearance.xMax)-Mathf.Max(cell.xMin,clearance.xMin);
                    float h=Mathf.Min(cell.yMax,clearance.yMax)-Mathf.Max(cell.yMin,clearance.yMin);
                    if(w>0&&h>0)area+=w*h;
                }
                if(area>=clearance.width*clearance.height-.001f)ripples.Add(new WaterRipple {Bounds=bounds,Phase=seed*.31f});
            }
            return ripples.ToArray();
        }
        static Rect RoundedSurface(Rect r) {
            return Rect.MinMaxRect(Mathf.Floor(r.x),Mathf.Floor(r.y),Mathf.Ceil(r.xMax),Mathf.Ceil(r.yMax));
        }
        void DrawMaterial(MaterialPatch[] patches,ArenaEnvironmentArt.Image image,Color fallback) {
            DrawMaterial(patches,image==null?null:image.Texture,image==null?new Rect(0,0,1,1):image.Uv,fallback);
        }
        void DrawMaterial(MaterialPatch[] patches,Texture2D texture,Rect atlas,Color fallback) {
            foreach(var patch in patches) {
                var p=patch.Bounds;var rect=RoundedSurface(World(p.x,p.y,p.width,p.height));
                if(texture==null) {Fill(rect,fallback);continue;}
                var uv=patch.Uv;
                GUI.DrawTextureWithTexCoords(rect,texture,new Rect(atlas.x+uv.x*atlas.width,atlas.y+uv.y*atlas.height,uv.width*atlas.width,uv.height*atlas.height));
            }
        }
        static Rect EdgeStrip(ArenaEnvironmentGeometry.Edge edge,float start,float end) {
            if(edge.Side==0)return new Rect(edge.A.x,edge.A.y-end,edge.B.x-edge.A.x,end-start);
            if(edge.Side==1)return new Rect(edge.A.x-end,edge.A.y,end-start,edge.B.y-edge.A.y);
            if(edge.Side==2)return new Rect(edge.A.x,edge.A.y+start,edge.B.x-edge.A.x,end-start);
            return new Rect(edge.A.x+start,edge.A.y,end-start,edge.B.y-edge.A.y);
        }
        void EdgeBand(ArenaEnvironmentGeometry.Edge edge,float start,float end,Color color) {
            var r=EdgeStrip(edge,start,end);Fill(RoundedSurface(World(r.x,r.y,r.width,r.height)),color);
        }
        void DrawEnvironmentWater(GameMap map,ArenaBiome biome) {
            if(biome==ArenaBiome.Green) {
                // Restore the pre-polish water renderer, including its original
                // blue, narrow bank and short animated horizontal marks.
                foreach(var pond in map.water)DrawWater(pond);
                return;
            }
            var layout=Environment(map);
            bool sand=biome==ArenaBiome.Desert,snow=biome==ArenaBiome.Snow;
            Color baseColor=sand?new Color(.10f,.48f,.47f):snow?new Color(.52f,.75f,.84f):new Color(.20f,.57f,.76f);
            if(sand)DrawMaterial(layout.WaterPatches,layout.WaterArt,baseColor);
            else DrawMaterial(layout.WaterPatches,layout.IceArt,new Rect(0,0,1,1),baseColor);
            Color shallow=sand?new Color(.36f,.73f,.62f,.58f):snow?new Color(.75f,.90f,.95f,.7f):new Color(.37f,.74f,.85f,.65f);
            Color bank=sand?new Color(.77f,.63f,.37f):snow?new Color(.88f,.95f,.98f):new Color(.30f,.48f,.25f);
            Color lip=sand?new Color(.74f,.91f,.77f,.64f):snow?new Color(.96f,.99f,1,.92f):new Color(.45f,.65f,.33f,.9f);
            foreach(var edge in layout.Water.Edges) {
                // All bands are inside the wet footprint, and only the union perimeter gets a bank.
                float depth=sand?.82f:snow?.48f:.44f;
                EdgeBand(edge,.16f,depth,shallow);
                EdgeBand(edge,0,.12f,bank);
                EdgeBand(edge,.12f,.20f,lip);
            }
            if(snow) {
                foreach(var edge in layout.Water.Edges)DrawSnowLip(edge,.32f,false);
                return; // Frozen ponds have a still surface, never moving water ripples.
            }
            if(layout.Ripples.Length==0)return;
            if(waterRipple==null)waterRipple=RippleTexture();
            Color ripple=sand?new Color(.75f,.96f,.85f,.19f):new Color(.78f,.95f,1,.23f);
            GUI.color=ripple;
            foreach(var wave in layout.Ripples) {
                var r=wave.Bounds;r.x+=Mathf.Sin(Time.unscaledTime*.38f+wave.Phase)*.13f;
                GUI.DrawTexture(World(r.x,r.y,r.width,r.height),waterRipple);
            }
            GUI.color=Color.white;
        }
        void DrawEnvironmentBridges(GameMap map) {
            foreach(var bridge in Environment(map).Bridges) {
                var r=RoundedSurface(World(bridge.x,bridge.y,bridge.width,bridge.height));
                // Deck edges are flat timbers, not impassable raised railings.
                Fill(new Rect(r.x+1,r.y+2,r.width,r.height),new Color(.05f,.08f,.09f,.25f));
                Fill(r,new Color(.22f,.20f,.13f));
                bool vertical=bridge.height>=bridge.width;
                float length=vertical?bridge.height:bridge.width;
                for(float offset=.12f;offset<length-.12f;offset+=.58f) {
                    float thickness=Mathf.Min(.51f,length-.12f-offset);
                    var wood=vertical?new Rect(bridge.x+.12f,bridge.y+offset,bridge.width-.24f,thickness)
                        :new Rect(bridge.x+offset,bridge.y+.12f,thickness,bridge.height-.24f);
                    var plank=RoundedSurface(World(wood.x,wood.y,wood.width,wood.height));
                    int index=Mathf.FloorToInt((offset-.12f)/.58f);
                    Fill(plank,index%3==0?new Color(.70f,.62f,.41f):new Color(.63f,.56f,.36f));
                    Fill(new Rect(plank.x,plank.y,plank.width,1),new Color(.81f,.73f,.51f,.65f));
                }
                float edge=Mathf.Max(1,scale*.13f);
                Fill(vertical?new Rect(r.x,r.y,edge,r.height):new Rect(r.x,r.y,r.width,edge),new Color(.30f,.27f,.17f));
                Fill(vertical?new Rect(r.xMax-edge,r.y,edge,r.height):new Rect(r.x,r.yMax-edge,r.width,edge),new Color(.30f,.27f,.17f));
            }
        }
        static Texture2D RippleTexture() {
            var texture=new Texture2D(64,16,TextureFormat.RGBA32,false) {filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color[64*16];
            for(int py=0;py<16;py++)for(int px=0;px<64;px++) {
                float center=7+Mathf.Sin(px*.085f)*2,tip=Mathf.Pow(Mathf.Sin((px+.5f)/64*Mathf.PI),.7f);
                float alpha=Mathf.Clamp01(1.2f-Mathf.Abs(py-center))*tip;
                pixels[py*64+px]=new Color(1,1,1,alpha);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        void DrawEnvironmentWalls(GameMap map,ArenaBiome biome) {
            var layout=Environment(map);bool snow=biome==ArenaBiome.Snow;
            // Tight contact shadows follow only outside faces; no internal rectangle borders.
            foreach(var edge in layout.Walls.Edges)if(edge.Side==1||edge.Side==2) {
                var r=EdgeStrip(edge,0,Mathf.Min(.18f,2/scale));
                var screen=RoundedSurface(World(r.x,r.y,r.width,r.height));
                screen.x+=1;screen.y+=2;
                Fill(screen,new Color(.07f,.10f,.12f,.28f));
            }
            DrawMaterial(layout.WallPatches,layout.WallArt,snow?new Color(.43f,.56f,.64f):new Color(.70f,.49f,.28f));
            foreach(var edge in layout.Walls.Edges) {
                bool lit=edge.Side==0||edge.Side==3;
                EdgeBand(edge,.07f,.25f,snow?(lit?new Color(.72f,.84f,.89f,.8f):new Color(.20f,.33f,.42f,.8f)):(lit?new Color(.94f,.73f,.44f,.8f):new Color(.38f,.24f,.15f,.8f)));
                EdgeBand(edge,0,.07f,snow?new Color(.22f,.34f,.42f):new Color(.38f,.26f,.16f));
            }
            if(snow)foreach(var edge in layout.Walls.Edges)if(edge.Side==0||edge.Side==3)DrawSnowLip(edge,.39f,true);
        }
        void DrawSnowLip(ArenaEnvironmentGeometry.Edge edge,float depth,bool wall) {
            float length=edge.Side%2==0?edge.B.x-edge.A.x:edge.B.y-edge.A.y;
            float origin=edge.Side%2==0?edge.A.x:edge.A.y;
            for(float offset=0;offset<length;offset+=2.3f) {
                int phase=Mathf.FloorToInt((origin+offset)*3)+edge.Side*7;
                float segment=Mathf.Min(length-offset,1.15f+(phase%5)*.17f);
                var part=edge;
                if(edge.Side%2==0) {part.A.x+=offset;part.B.x=part.A.x+segment;}
                else {part.A.y+=offset;part.B.y=part.A.y+segment;}
                EdgeBand(part,.07f,depth*(.72f+(phase%3)*.12f),wall?new Color(.92f,.97f,.99f):new Color(.85f,.94f,.98f,.9f));
            }
        }
        bool OverlapsEnvironmentScenery(GameMap map,Rect bounds) {
            foreach(var prop in Environment(map).Scenery)if(bounds.Overlaps(prop.Bounds))return true;
            return false;
        }
        void DrawEnvironmentScenery(GameMap map,ArenaBiome biome) {
            foreach(var prop in Environment(map).Scenery) {
                var r=prop.Bounds;var screen=World(r.x,r.y,r.width,r.height);
                if(prop.Campaign) {
                    GUI.color=biome==ArenaBiome.Snow?new Color(.86f,.94f,1):biome==ArenaBiome.Desert?new Color(1,.94f,.82f):Color.white;
                    CampaignCartoonCatalog.Draw(screen,prop.Key);GUI.color=Color.white;
                } else if(prop.Key=="igloo")ArenaWinterArt.Draw(screen,prop.Key);
                else if(prop.Key=="oasis_palm")ArenaTerrainArt.Draw(screen,prop.Key);
                else ArenaBiomeArt.Draw(screen,prop.Key);
            }
        }
        void DisposeEnvironmentTextures() {
            if(waterRipple!=null)UnityEngine.Object.Destroy(waterRipple);
            environmentLayouts.Clear();
        }
    }
}
