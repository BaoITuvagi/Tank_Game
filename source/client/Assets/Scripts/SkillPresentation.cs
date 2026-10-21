using System;
using System.Collections.Generic;
using UnityEngine;
namespace Tank {
    public sealed partial class ArenaRenderer {
        Texture2D glow,ring,fog,energyField;
        readonly HashSet<ulong> heard=new HashSet<ulong>();
        readonly HashSet<uint> seenBullets=new HashSet<uint>();
        ulong presentationMatch;
        bool initialized;
        float visualTick,shakeStrength;
        public Vector2 Shake {get;private set;}
        public int LiveEffectCount {get;private set;}
        static readonly Color Orange=new Color(1,.28f,.06f),Cyan=new Color(.15f,.9f,1),Purple=new Color(.9f,.18f,1);
        internal void UpdateEffects(ClientCore client,float dt,SkillSounds audio) {
            if(client.Phase!=ClientPhase.PLAYING||client.Latest==null) {ResetEffects();return;}
            if(presentationMatch!=client.Match) {ResetEffects();presentationMatch=client.Match;}
            visualTick=client.EstimatedSkillTick;
            var world=client.Latest.World;TankState local;world.Tanks.TryGetValue(client.Slot,out local);
            LiveEffectCount=world.Effects.Count;
            foreach(var e in world.Effects) {
                bool visible=Visible(local,e.X,e.Y,client.Latest.Tick);
                bool impact=e.Kind==SkillEffect.Rocket||e.Kind==SkillEffect.Bomb||e.Kind==SkillEffect.Kamikaze||e.Kind==SkillEffect.Beam;
                if(impact&&client.Latest.Tick>=e.End&&client.Latest.Tick-e.End<=6) {
                    ulong key=((ulong)e.Id<<2)|1;
                    if(heard.Add(key)&&initialized&&visible) {
                        audio.Impact(e.Kind);
                        if(e.Kind!=SkillEffect.Rocket&&local!=null)shakeStrength=Mathf.Max(shakeStrength,Mathf.Clamp01(1-Vector2.Distance(new Vector2(local.X,local.Y),new Vector2(e.X,e.Y))/35)*2.2f);
                    }
                }
                if(e.Kind==SkillEffect.Activate&&client.Latest.Tick>=e.Start&&client.Latest.Tick-e.Start<=6&&heard.Add((ulong)e.Id<<2)&&initialized&&visible)audio.Activate(e.Angle);
            }
            foreach(var t in world.Tanks.Values) {
                if(t.Hp>0&&t.QStage==1&&(t.Nation==5||t.Nation==6)&&Visible(local,t.X,t.Y,client.Latest.Tick)) {
                    int seconds=Mathf.CeilToInt((t.QEndTick-visualTick)/30);
                    ulong key=0x8000000000000000UL|((ulong)(uint)t.Life<<20)|((ulong)(uint)t.QStartTick<<3)|(uint)Mathf.Clamp(seconds,0,7);
                    if(heard.Add(key)&&initialized&&seconds>0)audio.Countdown(t.Nation,seconds);
                }
                if(t.Slot==client.Slot) {
                    ulong fogKey=0x4000000000000000UL|((ulong)(uint)t.Life<<20)|(uint)t.FogEndTick;
                    bool fogged=t.Hp>0&&client.Latest.Tick<t.FogEndTick;
                    if(fogged&&heard.Add(fogKey)&&initialized)audio.Fog(true);
                    ulong resetKey=0x2000000000000000UL|((ulong)(uint)t.Life<<20)|(uint)t.FireLockEndTick;
                    if(client.Latest.Tick<t.FireLockEndTick&&heard.Add(resetKey)&&initialized)qResetUntil=Time.unscaledTime+.55f;
                    if(wasFogged&&!fogged&&initialized)audio.Fog(false);
                    wasFogged=fogged;
                }
            }
            foreach(var b in world.Bullets.Values)if(seenBullets.Add(b.Id)&&initialized&&b.Damage==GameRules.GermanBulletDamage&&b.RemainingLifetime>=147&&Visible(local,b.X,b.Y,client.Latest.Tick)) {
                int born=(int)client.Latest.Tick-GameRules.BulletLifetimeTicks+b.RemainingLifetime;
                ulong key=0x1000000000000000UL|((ulong)(uint)b.Owner<<32)|(uint)born;
                if(heard.Add(key))audio.Barrage();
            }
            if(heard.Count>4096)heard.Clear();
            if(seenBullets.Count>2048) {seenBullets.Clear();foreach(uint id in world.Bullets.Keys)seenBullets.Add(id);}
            initialized=true;
            shakeStrength=Mathf.MoveTowards(shakeStrength,0,dt*14);
            Shake=new Vector2(Mathf.Sin(Time.unscaledTime*83),Mathf.Cos(Time.unscaledTime*101)) * shakeStrength;
            audio.Poll();
        }
        bool wasFogged;
        float qResetUntil;
        void ResetEffects() {heard.Clear();seenBullets.Clear();initialized=false;wasFogged=false;visualTick=shakeStrength=0;Shake=Vector2.zero;LiveEffectCount=0;}
        static bool Visible(TankState local,float wx,float wy,uint tick) {
            return local==null||local.Hp==0||tick>=local.FogEndTick||(wx-local.X)*(wx-local.X)+(wy-local.Y)*(wy-local.Y)<=GameRules.FogRadius*GameRules.FogRadius;
        }
        static Texture2D Radial(int kind) {
            const int n=256;
            var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color[n*n];
            for(int py=0;py<n;py++)for(int px=0;px<n;px++) {
                float dx=(px+.5f-n/2f)/(n/2f),dy=(py+.5f-n/2f)/(n/2f),r=Mathf.Sqrt(dx*dx+dy*dy);
                float alpha=kind==0?Mathf.Pow(Mathf.Max(0,1-r),2):kind==1?Mathf.Clamp01(1-Mathf.Abs(r-.975f)/.022f):kind==2?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.88f,.97f,r)):r>1?0:(.09f+.035f*Mathf.Sin(r*70+Mathf.Atan2(dy,dx)*6))*(1-Mathf.SmoothStep(.94f,1,r));
                pixels[py*n+px]=kind==2?new Color(.012f,.008f,.027f,alpha):new Color(1,1,1,alpha);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        void EffectTextures() {if(glow!=null)return;glow=Radial(0);ring=Radial(1);fog=Radial(2);energyField=Radial(3);}
        void Disc(float wx,float wy,float radius,Texture2D texture,Color color) {
            GUI.color=color;GUI.DrawTexture(World(wx-radius,wy-radius,2*radius,2*radius),texture);GUI.color=Color.white;
        }
        void Line(Vector2 a,Vector2 b,float width,Color color) {
            if(Event.current.type!=EventType.Repaint||width<=0||color.a<=0)return;
            Vector2 delta=b-a;float length=delta.magnitude;
            if(length<.001f)return;
            // Rotating a long GUI rectangle rotates its group clip too: the beam gets
            // cut short and sparks can escape the arena/fog. Rasterize bounded strips
            // in the group's unchanged coordinates; all layers share its real clip.
            bool horizontal=Mathf.Abs(delta.x)>=Mathf.Abs(delta.y);
            float extent=horizontal?Mathf.Abs(delta.x):Mathf.Abs(delta.y),step=width<2?1:width<5?2:4;
            int strips=Mathf.Max(1,Mathf.CeilToInt(extent/step));
            float thickness=width*length/extent;
            for(int i=0;i<strips;i++) {
                Vector2 p=a+delta*(i/(float)strips),q=a+delta*((i+1)/(float)strips),mid=(p+q)*.5f;
                Rect r=horizontal?new Rect(Mathf.Min(p.x,q.x),mid.y-thickness/2,Mathf.Abs(q.x-p.x)+.1f,thickness):new Rect(mid.x-thickness/2,Mathf.Min(p.y,q.y),thickness,Mathf.Abs(q.y-p.y)+.1f);
                Fill(r,color);
            }
        }
        Vector2 Point(float wx,float wy) {return new Vector2(x+wx*scale,y+(mapHeight-wy)*scale);}
        void Arc(Vector2 center,float radius,float start,float span,float width,Color color,int segments=8) {
            Vector2 previous=center+new Vector2(Mathf.Cos(start),Mathf.Sin(start))*radius;
            for(int i=1;i<=segments;i++) {
                float a=start+span*i/segments;Vector2 next=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                Line(previous,next,width,color);previous=next;
            }
        }
        // Fixed textures and deterministic particles: drawing never creates particles or events.
        void Warning(float wx,float wy,float radius,Color color,float progress,string symbol) {
            float now=visualTick,urgency=1-Mathf.Clamp01(progress),pulse=.75f+.25f*Mathf.Sin(now*(.25f+urgency*.7f));
            Color red=new Color(1,.08f,.12f,color.a);Vector2 p=Point(wx,wy);float rr=radius*scale;
            Disc(wx,wy,radius,energyField,red);
            Disc(wx,wy,radius*1.035f,ring,Fade(red,.15f*pulse));
            Disc(wx,wy,radius*1.012f,ring,Fade(red,.35f*pulse));
            // Ring texture's peak is at .975; compensate so the main rim marks the real radius.
            Disc(wx,wy,radius/.975f,ring,Fade(red,pulse));
            Arc(p,rr,-Mathf.PI/2,Mathf.PI*2*Mathf.Clamp01(progress),1,new Color(1,.72f,.64f,.9f*color.a),24);
            Disc(wx,wy,radius*.86f,ring,Fade(color,.25f));
            for(int i=0;i<8;i++) {
                float a=i*Mathf.PI/4+now*(symbol=="!"?-.012f:.009f);
                Arc(p,rr*1.07f,a,.28f,1,Fade(red,.65f),3);
                Vector2 direction=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                Line(p+direction*rr*.94f,p+direction*rr,1,new Color(1,.65f,.55f,.8f*color.a));
                if((Mathf.FloorToInt(now/3)+i)%8==0) {
                    Vector2 tangent=new Vector2(-direction.y,direction.x);
                    Vector2 a0=p+direction*rr,a1=a0+tangent*rr*.09f+direction*3,a2=a0+tangent*rr*.18f;
                    Line(a0,a1,2,Fade(red,.8f));Line(a1,a2,1,new Color(1,.8f,.5f,.9f*color.a));
                }
            }
            for(int i=0;i<24;i++) {
                float a=(i*137.5f+now*(symbol=="!"?-1.1f:symbol=="+"?.5f:.8f))*Mathf.Deg2Rad;
                float fraction=Mathf.Repeat(i*.618034f+now*(.0015f+urgency*.001f),1),r=Mathf.Sqrt(fraction)*radius*.94f;
                float alpha=(.25f+.45f*pulse)*Mathf.Sin(fraction*Mathf.PI)*color.a;
                float px=wx+Mathf.Cos(a)*r,py=wy+Mathf.Sin(a)*r;
                Disc(px,py,.17f,glow,new Color(1,.35f,.12f,alpha));
                Vector2 q=Point(px,py);Line(q,q+new Vector2(1,-3),1,new Color(1,.65f,.3f,alpha));
            }
            if(symbol=="◆") {
                for(int i=0;i<4;i++) {float a=i*Mathf.PI/2;Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Line(p+d*rr*.3f,p+d*rr*.55f,2,Fade(color,.7f));}
                Arc(p,rr*.42f,now*.012f,Mathf.PI*2,1,Fade(color,.45f),12);
            }
            else if(symbol=="!")Arc(p,rr*.65f,-Mathf.PI/2,Mathf.PI*2*urgency,2,Fade(red,.65f),16);
            GUI.Label(new Rect(p.x-14,p.y-13,28,26),symbol,small);
        }
        void BeamLine(GameMap map,float wx,float wy,int angle,bool warning,float alpha=1,float progress=0) {
            float c=Mathf.Cos(angle/100f*Mathf.Deg2Rad),s=Mathf.Sin(angle/100f*Mathf.Deg2Rad),distance=float.MaxValue;
            if(c>.00001f)distance=Mathf.Min(distance,(map.width-wx)/c);if(c<-.00001f)distance=Mathf.Min(distance,-wx/c);
            if(s>.00001f)distance=Mathf.Min(distance,(map.height-wy)/s);if(s<-.00001f)distance=Mathf.Min(distance,-wy/s);
            if(distance<=0)return;
            Vector2 a=Point(wx,wy),b=Point(wx+c*distance,wy+s*distance);
            float width=GameRules.BeamWidth*scale,now=visualTick;
            Vector2 direction=(b-a).normalized,normal=new Vector2(-direction.y,direction.x);
            Color red=new Color(1,.13f,.035f),gold=new Color(1,.78f,.18f),white=new Color(1,1,.83f);
            if(warning) {
                float pulse=.6f+.4f*Mathf.Sin(now*(.3f+progress*.6f));
                Line(a,b,width,Fade(red,.08f));Line(a,b,4,Fade(red,.15f+progress*.15f));
                Line(a,b,1,Fade(Orange,.5f+progress*.4f));
                Line(a+normal*width/2,b+normal*width/2,1,Fade(red,.5f));Line(a-normal*width/2,b-normal*width/2,1,Fade(red,.5f));
                Arc(a,(.6f+progress*.3f)*scale,now*.04f,Mathf.PI*1.7f,2,Fade(Orange,pulse),16);
                for(int i=0;i<12;i++) {
                    float f=Mathf.Repeat(i/12f+now*(.002f+progress*.003f),1);Vector2 p=Vector2.Lerp(a,b,f);
                    Line(p-direction*4+normal*3,p,1,Fade(gold,.4f+progress*.4f));Line(p-direction*4-normal*3,p,1,Fade(Orange,.7f));
                    if(i%3==0)Line(p-normal*width*.65f,p+normal*width*.65f,1,Fade(red,.65f));
                }
            }
            else {
                Line(a,b,width*4,Fade(red,.13f*alpha));Line(a,b,width*2.4f,Fade(red,.35f*alpha));
                Line(a,b,width*1.4f,Fade(Orange,.7f*alpha));Line(a,b,width,Fade(gold,alpha));Line(a,b,Mathf.Max(1,width*.48f),Fade(white,alpha));
                for(int side=-1;side<=1;side+=2) {
                    Vector2 previous=a;
                    for(int i=1;i<=20;i++) {
                        float f=i/20f,noise=Mathf.Sin(i*12.7f+Mathf.Floor(now/2)*2.3f+side);
                        Vector2 p=Vector2.Lerp(a,b,f)+normal*side*width*(.8f+noise*.65f);
                        Line(previous,p,3,Fade(Orange,.35f*alpha));Line(previous,p,1,Fade(white,.9f*alpha));previous=p;
                        if(i%3==0) {Vector2 tip=p+normal*side*scale*(.5f+noise*.25f)-direction*scale*.5f;Line(p,tip,1,Fade(gold,alpha));}
                    }
                }
                Disc(wx,wy,1.6f,glow,Fade(Orange,alpha));Disc(wx,wy,.7f,glow,Fade(white,alpha));
            }
        }
        void DrawSkillWorld(GameMap map,WorldState world,uint tick) {
            EffectTextures();float now=Mathf.Max(tick,visualTick);
            foreach(var t in world.Tanks.Values)if(t.Hp>0) {
                float remaining=Mathf.Max(0,t.QEndTick-now);
                if(t.Nation==1&&t.QStage==1) {
                    Warning(t.AimX,t.AimY,GameRules.KatyushaRadius,Orange,remaining/GameRules.KatyushaAimTicks,"+");
                    Vector2 p=Point(t.AimX,t.AimY);Line(p-new Vector2(12,0),p+new Vector2(12,0),1,Orange);Line(p-new Vector2(0,12),p+new Vector2(0,12),1,Orange);
                    GUI.Label(new Rect(p.x-30,p.y+12,60,20),Mathf.CeilToInt(remaining/30)+"s",small);
                }
                if(t.Nation==6&&t.QStage==1)Warning(t.X,t.Y,GameRules.KamikazeRadius,Orange,remaining/GameRules.KamikazeTicks,"!");
                if(t.Nation==5&&t.QStage==1) {
                    float angle=t.Turret/100f*Mathf.Deg2Rad;
                    BeamLine(map,t.X+Mathf.Cos(angle)*1.05f,t.Y+Mathf.Sin(angle)*1.05f,t.Turret,true,1,1-remaining/GameRules.BeamChargeTicks);
                }
                if((t.Nation==4&&remaining>0)||((t.Nation==5||t.Nation==6)&&t.QStage==1)) {
                    Color color=Orange;
                    float pulse=.45f+.2f*Mathf.Sin(now*(t.Nation==6?1+(1-remaining/150)*2:.35f));
                    Disc(t.X,t.Y,1.8f,glow,Fade(color,pulse));
                    float a=t.Turret/100f*Mathf.Deg2Rad;
                    float muzzleX=t.X+Mathf.Cos(a)*1.05f,muzzleY=t.Y+Mathf.Sin(a)*1.05f;
                    Disc(muzzleX,muzzleY,.8f,glow,Fade(color,.8f));
                    if(t.Nation==5)for(int i=0;i<8;i++) {
                        float orbit=(i*45+now*3)*Mathf.Deg2Rad,r=.3f+((now+i*7)%30)/30*1.3f;
                        Disc(muzzleX+Mathf.Cos(orbit)*r,muzzleY+Mathf.Sin(orbit)*r,.16f,glow,Fade(Orange,.7f));
                    }
                }
            }
            foreach(var e in world.Effects) {
                if(now<e.Start||now>e.End+24)continue;
                float after=now-e.End;
                Color color=e.Kind==SkillEffect.Radar?Cyan:e.Kind==SkillEffect.Sabotage?Purple:Orange;
                if(e.Kind==SkillEffect.Area&&after<0)Warning(e.X,e.Y,e.Radius,Orange,(e.End-now)/Mathf.Max(1,e.End-e.Start),"+");
                else if(e.Kind==SkillEffect.Bomb&&after<0)Warning(e.X,e.Y,e.Radius,new Color(1,.15f,.22f),(e.End-now)/GameRules.BombDelayTicks,"◆");
                else if(e.Kind==SkillEffect.Rocket&&after<0) {
                    float height=(e.End-now)/15*7;
                    Disc(e.X,e.Y,e.Radius,ring,Fade(Orange,.6f));
                    Vector2 tip=Point(e.X,e.Y+height);Line(tip,tip+new Vector2(-6,-scale*4),6,Fade(Orange,.65f));Disc(e.X,e.Y+height,1,glow,Color.white);
                }
                else if(e.Kind==SkillEffect.Beam&&after>=0)BeamLine(map,e.X,e.Y,e.Angle,false,Mathf.Clamp01(1-after/18));
                else if((e.Kind==SkillEffect.Radar||e.Kind==SkillEffect.Sabotage)&&now<=e.End) {
                    float progress=(now-e.Start)/Mathf.Max(1,e.End-e.Start);
                    Disc(e.X,e.Y,2+progress*Mathf.Max(map.width,map.height),ring,Fade(color,(1-progress)*.7f));
                }
                else if(after>=0&&(e.Kind==SkillEffect.Bomb||e.Kind==SkillEffect.Rocket||e.Kind==SkillEffect.Kamikaze)) {
                    float p=after/24,alpha=1-p;
                    Warning(e.X,e.Y,e.Radius,Fade(color,alpha),1-p,e.Kind==SkillEffect.Bomb?"◆":e.Kind==SkillEffect.Kamikaze?"!":"+");
                    Disc(e.X,e.Y,e.Radius,glow,Fade(color,alpha));Disc(e.X,e.Y,e.Radius*(.2f+.8f*p),ring,Fade(color,alpha));Disc(e.X,e.Y,e.Radius*.55f,glow,Fade(Color.white,alpha*alpha));
                    for(int i=0;i<12;i++) {
                        float a=(i*137.5f+e.Id%90)*Mathf.Deg2Rad,r=e.Radius*(.2f+.7f*p);
                        Disc(e.X+Mathf.Cos(a)*r,e.Y+Mathf.Sin(a)*r,.12f+.14f*alpha,glow,Fade(Color.yellow,alpha));
                    }
                    Disc(e.X+.2f*p,e.Y+.6f*p,e.Radius*(.25f+.45f*p),glow,new Color(.2f,.2f,.3f,alpha*.2f));
                }
            }
            foreach(var b in world.Bullets.Values)if(b.Damage==GameRules.GermanBulletDamage) {
                Disc(b.X,b.Y,.6f,glow,Fade(Orange,.7f));
                if(b.RemainingLifetime>=147) {
                    float a=b.Heading/100f*Mathf.Deg2Rad;Line(Point(b.X,b.Y),Point(b.X-Mathf.Cos(a)*1.1f,b.Y-Mathf.Sin(a)*1.1f),2,Fade(Orange,.8f));
                }
            }
        }
        void DrawSkillLabels(TankState t,uint tick) {
            Vector2 p=Point(t.X,t.Y);float now=Mathf.Max(tick,visualTick),top=p.y+scale*TankSize/2+20;
            if(t.QStage==1&&(t.Nation==5||t.Nation==6)) {GUI.Label(new Rect(p.x-43,top,86,20),(t.Nation==5?"TỤ LỰC ":"TỰ NỔ ")+Mathf.Max(0,(t.QEndTick-now)/30).ToString("0.0")+"s",small);top+=18;}
            if(tick<t.SlowEndTick) {GUI.Label(new Rect(p.x-56,top,112,18),"CHẬM 50% "+Mathf.Max(0,(t.SlowEndTick-now)/30).ToString("0.0")+"s",small);top+=17;}
            if(tick<t.ConfusionEndTick) {GUI.Label(new Rect(p.x-60,top,120,18),"⇄ KHÓA NÒNG "+Mathf.CeilToInt(Mathf.Max(0,t.ConfusionEndTick-now)/30)+"s",small);top+=17;}
            if(tick<t.FireLockEndTick) {
                GUI.Label(new Rect(p.x-56,top,112,18),"KHÓA BẮN "+Mathf.CeilToInt(Mathf.Max(0,t.FireLockEndTick-now)/30)+"s",small);
                float a=t.Turret/100f*Mathf.Deg2Rad,mx=t.X+Mathf.Cos(a)*1.05f,my=t.Y+Mathf.Sin(a)*1.05f;
                Disc(mx,my,.65f,glow,Fade(Purple,.55f));Disc(mx,my+.3f,.7f,glow,new Color(.06f,.02f,.09f,.5f));
                Vector2 muzzle=Point(mx,my);Line(muzzle+new Vector2(-4,2),muzzle+new Vector2(2,-3),1,Purple);Line(muzzle+new Vector2(2,-3),muzzle+new Vector2(6,1),1,Purple);
            }
        }
        void DrawFog(GameMap map,WorldState world,int localSlot,uint tick) {
            TankState t;if(world==null||!world.Tanks.TryGetValue(localSlot,out t)||t.Hp==0||tick>=t.FogEndTick)return;
            EffectTextures();
            Rect all=World(0,0,map.width,map.height),hole=World(t.X-GameRules.FogRadius,t.Y-GameRules.FogRadius,2*GameRules.FogRadius,2*GameRules.FogRadius);
            // Opaque outside; all softness is INSIDE the authoritative radius.
            bool enabled=GUI.enabled;GUI.enabled=true;
            // One clamped mask avoids subpixel seams between the four outer fills
            // and the circular texture, which exposed green terrain while moving.
            Rect cover=new Rect(all.x-6,all.y-6,all.width+12,all.height+12);
            Rect uv=new Rect((cover.x-hole.x)/hole.width,(hole.yMax-cover.yMax)/hole.height,cover.width/hole.width,cover.height/hole.height);
            GUI.color=Color.white;GUI.DrawTextureWithTexCoords(cover,fog,uv);
            Disc(t.X,t.Y,GameRules.FogRadius*.9f,ring,new Color(.55f,.35f,1,.16f+.08f*Mathf.Sin(Time.unscaledTime*2)));
            GUI.enabled=enabled;
        }
    }
}
