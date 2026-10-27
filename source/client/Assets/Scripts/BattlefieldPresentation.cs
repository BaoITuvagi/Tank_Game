using UnityEngine;
namespace Tank {
    public static class BattlefieldPresentation {
        public static bool Active(BattlefieldWave wave,float tick) {return wave!=null&&tick>=wave.Start&&tick<wave.End;}
        public static bool Warning(BattlefieldWave wave,float tick,int round=0) {return Active(wave,tick)&&tick>=wave.RoundStart(round)&&tick<wave.RoundImpact(round);}
        public static bool Burning(BattlefieldWave wave,float tick,int round) {return Active(wave,tick)&&wave.Kind==BattlefieldWave.Fire&&tick>=wave.RoundImpact(round)&&tick<wave.RoundImpact(round)+GameRules.BattlefieldFireTicks;}
        public static bool Burning(BattlefieldWave wave,float tick) {if(wave!=null)for(int i=0;i<wave.Rounds;i++)if(Burning(wave,tick,i))return true;return false;}
        public static string Name(int kind) {return kind==BattlefieldWave.Fire?"Red Zone":"Bombstorm";}
    }
    public sealed partial class ArenaRenderer {
        // This layer uses the latest authoritative state, like obstacle collision.
        // DrawFog clips its world pixels afterwards; the HUD never reveals locations.
        Texture2D battlefieldWarning;
        void DrawBattlefieldWarning(float wx,float wy,float radius,float age) {
            if(battlefieldWarning==null) {
                const int size=128;
                battlefieldWarning=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
                var pixels=new Color[size*size];
                for(int py=0;py<size;py++)for(int px=0;px<size;px++) {
                    float dx=(px+.5f-size/2f)/(size/2f),dy=(py+.5f-size/2f)/(size/2f),r=Mathf.Sqrt(dx*dx+dy*dy);
                    pixels[py*size+px]=r>1?Color.clear:r>.98f?new Color(.15f,.03f,.01f,1):r>.89f?new Color(1,.9f,.25f,1):new Color(1,.06f,.01f,(px+py)/10%2==0?.5f:.23f);
                }
                battlefieldWarning.SetPixels(pixels);battlefieldWarning.Apply(false,true);
            }
            // The footprint stays visible throughout warning; the pulse never fades it out.
            Disc(wx,wy,radius,battlefieldWarning,new Color(1,1,1,.9f+.1f*Mathf.Sin(age*12)));
            var centre=Point(wx,wy);float arm=Mathf.Max(6,scale*.8f);
            Line(centre-Vector2.right*arm,centre+Vector2.right*arm,4,Color.black,true);
            Line(centre-Vector2.up*arm,centre+Vector2.up*arm,4,Color.black,true);
            Line(centre-Vector2.right*arm,centre+Vector2.right*arm,2,new Color(1,.95f,.5f),true);
            Line(centre-Vector2.up*arm,centre+Vector2.up*arm,2,new Color(1,.95f,.5f),true);
            for(int i=0;i<12;i++) {
                float a=i*Mathf.PI/6;
                FxSprite("pickup_ui/item_glow",wx+Mathf.Cos(a)*radius,wy+Mathf.Sin(a)*radius,2.3f,age+i*.07f,Color.white,originalSize:true);
            }
        }
        void DrawBattlefield(GameMap map,WorldState world,uint tick) {
            var wave=world==null?null:world.Battlefield;
            float now=Mathf.Max(tick,visualTick);
            if(!BattlefieldPresentation.Active(wave,now)||map.battlefield==null)return;
            for(int round=0;round<wave.Rounds;round++) {
                bool warning=BattlefieldPresentation.Warning(wave,now,round);
                uint impact=wave.RoundImpact(round);
                for(int target=0;target<wave.TargetsPerRound;target++) {
                    var z=map.battlefield.Zone(wave.Kind,wave.Target(round,target));float radius=GameRules.BattlefieldRadius;
                    if(warning) {
                        float age=(now-wave.RoundStart(round))/30;
                        DrawBattlefieldWarning(z.x,z.y,radius,age);
                    }
                    else if(BattlefieldPresentation.Burning(wave,now,round)) {
                        float age=(now-impact)/30;
                        // Fixed-size, bounded stamps; particles never drive gameplay.
                        for(int i=0;i<9;i++) {
                            float a=(i-1)*Mathf.PI/4,r=i==0?0:radius*.62f;
                            float px=z.x+Mathf.Cos(a)*r,py=z.y+Mathf.Sin(a)*r;
                            FxSprite("nature/fire_top",px,py,5.2f,age+i*.13f,new Color(1,1,1,.9f),repeat:true,originalSize:true);
                            if(i%3==0)FxSprite("nature/smoke_top",px,py+.6f,4.2f,age+i*.13f,new Color(.6f,.6f,.6f,.3f),repeat:true,originalSize:true);
                        }
                    }
                }
            }
        }
        void DrawBattlefieldNotice(WorldState world,uint tick,float width) {
            var wave=world==null?null:world.Battlefield;
            float now=Mathf.Max(tick,visualTick);
            if(!BattlefieldPresentation.Active(wave,now))return;
            CartoonUi.Panel(new Rect(width/2-125,4,250,28),CartoonUi.PanelDark,9,2);
            Fill(new Rect(width/2-115,11,4,13),CartoonUi.Danger);
            float remaining=(wave.End-now)/30;
            GUI.Label(new Rect(width/2-108,6,223,24),BattlefieldPresentation.Name(wave.Kind)+" · "+remaining.ToString("0.0")+"s",battlefieldNotice);
        }
    }
}
