using System.Collections.Generic;
using UnityEngine;
namespace Tank {
    public sealed partial class ArenaRenderer {
        sealed class BiomeLayout {
            public Rect[] Routes;
            public ArenaBiomeGeometry.Prop[] Props;
        }
        readonly Dictionary<GameMap,BiomeLayout> biomeLayouts=new Dictionary<GameMap,BiomeLayout>();
        readonly Texture2D[,] biomeBricks=new Texture2D[2,GameRules.BrickMaxHp];
        Texture2D snowGround;
        BiomeLayout Layout(GameMap map) {
            BiomeLayout layout;
            if(!biomeLayouts.TryGetValue(map,out layout)) {
                var routes=ArenaBiomeGeometry.Routes(map);
                layout=new BiomeLayout {Routes=routes,Props=ArenaBiomeGeometry.Props(map,routes)};
                biomeLayouts.Add(map,layout);
            }
            return layout;
        }
        static Color BiomeBorder(ArenaBiome biome) {return biome==ArenaBiome.Desert?new Color(.42f,.29f,.17f):new Color(.31f,.43f,.51f);}
        static Texture2D BiomeTexture(Color[] pixels) {
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false) {filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        public static Color[] SnowGroundPixels() {
            var pixels=new Color[64*64];
            for(int py=0;py<64;py++)for(int px=0;px<64;px++) {
                float grain=((px*13+py*7)%19)/1400f;
                // Quiet wind-carved contour; no grass flecks, sand strip or ring road.
                float ridge=Mathf.Sin((px+Mathf.Sin(py*.12f)*5)*.12f)*Mathf.Sin(py*.1f)*.009f;
                pixels[py*64+px]=new Color(.86f+grain+ridge,.92f+grain+ridge,.95f+grain+ridge);
            }
            return pixels;
        }
        void DrawBiomeGround(GameMap map,ArenaBiome biome) {
            bool desert=biome==ArenaBiome.Desert;
            var layout=Layout(map);
            if(!desert&&snowGround==null)snowGround=BiomeTexture(SnowGroundPixels());
            int rows=Mathf.CeilToInt(map.height/4),cols=Mathf.CeilToInt(map.width/4);
            for(int row=0;row<rows;row++)for(int col=0;col<cols;col++) {
                Rect tile=World(col*4,row*4,Mathf.Min(4,map.width-col*4),Mathf.Min(4,map.height-row*4));
                if(desert)Image(tile,(row*7+col*3)%5==0?"tileSand2":"tileSand1",new Color(.99f,.87f,.66f));
                else GUI.DrawTexture(Rect.MinMaxRect(Mathf.Floor(tile.x),Mathf.Floor(tile.y),Mathf.Ceil(tile.xMax),Mathf.Ceil(tile.yMax)),snowGround);
            }
            if(desert) {
                // Earthen parade ground is local to the fortress, rather than a shared stripe.
                Fill(World(map.width*38/120,map.height*20/80,map.width*44/120,map.height*40/80),new Color(.61f,.40f,.22f,.23f));
                for(int i=0;i<10;i++) {
                    float wx=5+i*11,wy=5+(i*17)%65;
                    GroundContour(wx,wy,7,2,desert,map);
                }
            } else for(int i=0;i<12;i++)GroundContour(5+i*9,5+(i*19)%67,7.5f,2.2f,false,map);
            Color route=desert?new Color(.68f,.45f,.25f,.36f):new Color(.57f,.73f,.81f,.30f);
            foreach(var path in layout.Routes) {
                Rect r=World(path.x,path.y,path.width,path.height);Fill(r,route);
                // Fine parallel track impressions distinguish packed snow and sand from ground.
                if(path.width>path.height) {
                    Fill(new Rect(r.x,r.y+r.height*.27f,r.width,Mathf.Max(.5f,scale*.08f)),new Color(route.r,route.g,route.b,.20f));
                    Fill(new Rect(r.x,r.y+r.height*.70f,r.width,Mathf.Max(.5f,scale*.08f)),new Color(route.r,route.g,route.b,.20f));
                } else {
                    Fill(new Rect(r.x+r.width*.27f,r.y,Mathf.Max(.5f,scale*.08f),r.height),new Color(route.r,route.g,route.b,.20f));
                    Fill(new Rect(r.x+r.width*.70f,r.y,Mathf.Max(.5f,scale*.08f),r.height),new Color(route.r,route.g,route.b,.20f));
                }
            }
            foreach(var prop in layout.Props)if(!prop.OnObstacle)DrawBiomeProp(prop);
            foreach(var spawn in map.spawns)Image(World(spawn.x-1.1f,spawn.y-1.1f,2.2f,2.2f),"tracksDouble",desert?new Color(.44f,.29f,.16f,.25f):new Color(.31f,.46f,.58f,.24f));
        }
        void GroundContour(float wx,float wy,float width,float height,bool desert,GameMap map) {
            // All these low-contrast contours are flat ground marks beneath combat entities.
            float sx=map.width/120,sy=map.height/80;
            for(int i=0;i<3;i++) {
                var r=World((wx+i*.3f)*sx,(wy+i*.27f)*sy,(width-i*.6f)*sx,.10f*sy);
                Fill(r,desert?new Color(.72f,.48f,.25f,.13f):new Color(.55f,.72f,.83f,.10f));
            }
        }
        void DrawBiomeProp(ArenaBiomeGeometry.Prop prop) {var r=prop.Bounds;ArenaBiomeArt.Draw(World(r.x,r.y,r.width,r.height),prop.Key);}
        void DrawBiomeObstacleProps(GameMap map) {foreach(var prop in Layout(map).Props)if(prop.OnObstacle&&!OverlapsEnvironmentScenery(map,prop.Bounds))DrawBiomeProp(prop);}
        Texture2D BiomeBrickTexture(ArenaBiome biome,int hp) {
            int index=biome==ArenaBiome.Desert?0:1;
            if(biomeBricks[index,hp-1]!=null)return biomeBricks[index,hp-1];
            var pixels=BrickPixels(hp);
            for(int i=0;i<pixels.Length;i++) {
                var original=pixels[i];if(original.a<=0)continue;
                float value=(original.r+original.g+original.b)/3;
                pixels[i]=index==0?new Color(value*1.35f,value*.78f,value*.43f,original.a):new Color(value*.83f,value*1.08f,value*1.25f,original.a);
                // Snow remains only on the top surviving lip of each destructible section.
                int px=i%64,py=i/64,edge=hp==GameRules.BrickMaxHp?64:64*hp/GameRules.BrickMaxHp+(px/8%3)-1;
                if(index==1&&py>=edge-4)pixels[i]=new Color(.89f,.97f,1,original.a);
            }
            return biomeBricks[index,hp-1]=BiomeTexture(pixels);
        }
        void DrawBiomeBrick(MapRect wall,int hp,ArenaBiome biome) {
            Rect r=World(wall.x,wall.y,wall.w,wall.h);
            var texture=BiomeBrickTexture(biome,hp);
            if(hp==GameRules.BrickMaxHp) {Shadow(r);Fill(r,biome==ArenaBiome.Desert?new Color(.36f,.21f,.12f):new Color(.22f,.33f,.41f));}
            else {GUI.color=new Color(0,0,0,.28f);GUI.DrawTexture(Inset(new Rect(r.x+2,r.y+3,r.width,r.height),.5f),texture);GUI.color=Color.white;}
            GUI.DrawTexture(Inset(r,Mathf.Min(.5f,Mathf.Min(r.width,r.height)*.1f)),texture);
        }
        void DisposeBiomeTextures() {
            if(snowGround!=null)UnityEngine.Object.Destroy(snowGround);
            foreach(var texture in biomeBricks)if(texture!=null)UnityEngine.Object.Destroy(texture);
            biomeLayouts.Clear();
        }
    }
}
