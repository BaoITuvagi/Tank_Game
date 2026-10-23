using UnityEngine;
namespace Tank {
    public sealed partial class ArenaRenderer {
        readonly CombatPresentation combatFx=new CombatPresentation();
        uint presentationSnapshot;
        int noticeSerial;
        internal void AcceptEffects(ClientCore client,Snapshot snapshot) {
            if(presentationMatch!=client.Match) {ResetEffects();presentationMatch=client.Match;}
            combatFx.Accept(client.Match,snapshot,client.Slot,client.ActiveMap);
            presentationSnapshot=snapshot.Id;
            initialized=true;
        }
        public static float EffectWidth(float width,bool originalSize=false) {return width*(originalSize?1:5);}
        void FxSprite(string key,float wx,float wy,float width,float age,Color tint,float angle=0,float speed=1,bool repeat=false,bool originalSize=false) {
            var clip=CombatFxCatalog.Get(key);if(clip==null||age<0)return;
            if(!repeat&&!clip.Info.loop&&age*speed>=clip.Duration)return;
            if(repeat)age%=clip.Duration;
            width=EffectWidth(width,originalSize);
            Rect r=World(wx-width/2,wy-width/2,width,width);var saved=GUI.matrix;Color old=GUI.color;
            try {
                if(clip.Info.faces=="right")GuiTransforms.Rotate(-angle,r.center);
                GUI.color=tint;GUI.DrawTextureWithTexCoords(r,clip.Texture,clip.Uv(age,speed));
            }
            finally {GUI.matrix=saved;GUI.color=old;}
        }
        void DrawFxPool(CombatPresentation.Fx[] pool,GameMap map,WorldState world,TankState observer,uint tick) {
            foreach(var f in pool) {
                if(!f.Active)continue;float wx=f.X,wy=f.Y;
                if(f.Slot>=0) {TankState t;if(!world.Tanks.TryGetValue(f.Slot,out t)||t.Life!=f.Life||t.Hp<=0)continue;wx=t.X+f.OffsetX;wy=t.Y+f.OffsetY;}
                if(!Visible(observer,wx,wy,tick))continue;
                float age=combatFx.Clock-f.Start;
                if(f.Clip=="@beam")BeamLine(map,wx,wy,Mathf.RoundToInt(f.Angle*100),false,Mathf.Clamp01(1-age/f.Duration));
                else if(f.Clip==null) {EffectTextures();Disc(wx,wy,f.Width*(.2f+age),ring,Fade(f.Tint,Mathf.Clamp01(1-age/f.Duration)));}
                else FxSprite(f.Clip,wx,wy,f.Width,age,f.Tint,f.Angle,f.Speed,originalSize:f.OriginalSize);
            }
        }
        void DrawWrecks(TankState observer,uint tick) {
            foreach(var wreck in combatFx.Wrecks)if(wreck.Active&&Visible(observer,wreck.X,wreck.Y,tick)) {
                Rect r=World(wreck.X-TankSize/2,wreck.Y-TankSize/2,TankSize,TankSize);
                Color tint=new Color(.22f,.19f,.17f,Mathf.Clamp01((2-(combatFx.Clock-wreck.Start))/.35f));
                RotatedTexture(r,TankPalette.Body(wreck.Palette),90-wreck.Body/100f,r.center,tint);
                float multiplier=TankSize/1.7f;
                Rect barrel=new Rect(r.center.x-.19f*scale*multiplier,r.center.y-1.05f*scale*multiplier,.38f*scale*multiplier,1.18f*scale*multiplier);
                RotatedTexture(barrel,TankPalette.Barrel(wreck.Palette),90-wreck.Turret/100f,r.center,tint);
            }
        }
        void DrawCombatOverlays(GameMap map,WorldState world,TankState observer,uint tick) {
            DrawFxPool(combatFx.Smoke,map,world,observer,tick);DrawFxPool(combatFx.Transients,map,world,observer,tick);
            foreach(var wreck in combatFx.Wrecks)if(wreck.Active&&Visible(observer,wreck.X,wreck.Y,tick)) {
                float age=combatFx.Clock-wreck.Start,alpha=Mathf.Clamp01((2-age)/.5f);
                FxSprite("nature/fire_top",wreck.X,wreck.Y,.95f*.75f,age,new Color(1,1,1,.7f*alpha));
                FxSprite("nature/smoke_top",wreck.X,wreck.Y+.45f,1.3f*.75f,age,new Color(.65f,.65f,.65f,.3f*alpha),repeat:true);
            }
            for(int slot=0;slot<8;slot++) {
                TankState t;if(!world.Tanks.TryGetValue(slot,out t)||t.Hp<=0||!Visible(observer,t.X,t.Y,tick))continue;
                float age=combatFx.Clock;
                if(tick<t.HeEndTick||t.MirrorShots>0)FxSprite("status/buff",t.X,t.Y,2.1f,age,new Color(1,.65f,.4f,.3f));
                if(tick<t.SlowEndTick||SkillGeometry.KatyushaSlowEnd(t,tick,world.Effects)>tick)FxSprite("status/debuff",t.X,t.Y+1.1f,.8f,age,new Color(1,1,1,.75f));
                if(tick<t.FireLockEndTick) {
                    float a=t.Turret/100f*Mathf.Deg2Rad;
                    FxSprite("magic/ice_hit_top",t.X+Mathf.Cos(a)*GameRules.TankMuzzleOffset,t.Y+Mathf.Sin(a)*GameRules.TankMuzzleOffset,TankSize*1.25f,age,Color.white,repeat:true,originalSize:true);
                }
                if(tick<t.ConfusionEndTick)FxSprite("status/stun",t.X,t.Y+1.15f,.9f,age,Color.white);
            }
            for(int i=0;i<combatFx.TrailBulletCount;i++) {
                BulletState b;if(world.Bullets.TryGetValue(combatFx.TrailBullets[i],out b)&&Visible(observer,b.X,b.Y,tick))
                    FxSprite("movement/dash",b.X,b.Y,.8f,combatFx.Clock,new Color(.8f,.4f,1,.5f),b.Heading/100f,repeat:true);
            }
        }
    }
}
