using System;
using System.Collections.Generic;
using UnityEngine;
namespace Tank  {
    // Presentation only: all positions, HP and obstacle bounds come from the existing world.
    public sealed partial class ArenaRenderer  {
        public const float TankSize = CombatGeometry.TankSize;
        static readonly Color Ink = new Color(.055f,.085f,.105f), Green = new Color(.24f,.9f,.46f);
        readonly Dictionary<string,Texture2D> art = new Dictionary<string,Texture2D>();
        readonly Dictionary<int,float> health = new Dictionary<int,float>();
        readonly Texture2D[] brick = new Texture2D[GameRules.BrickMaxHp];
        GUIStyle small, hpText, heading, body, timer, section, controls, skillButton, skillWaitingButton,skillUnavailableText,leaveButton,network,rankName,battlefieldNotice;
        float x,y,scale,mapHeight;
        float VisualSize  {
            get {
                return TankSize;
            }
        }
        Texture2D Art(string name)  {
            Texture2D texture;
            if(!art.TryGetValue(name,out texture))  {
                texture=Resources.Load<Texture2D>("ArenaArt/"+name);
                if(texture==null)Debug.LogError("Missing arena asset: "+name);
                art[name]=texture;
            }
            return texture;
        }
        void Styles()  {
            if(small!=null)return;
            // Small map labels keep their footprint and the skin's glyph fallback for skill symbols.
            small=new GUIStyle(CartoonUi.Label(11,CartoonUi.Text));
            small.font=ArenaTheme.GlyphFont??GUI.skin.font;
            small.fontStyle=FontStyle.Bold;
            small.alignment=TextAnchor.MiddleCenter;
            hpText=new GUIStyle(small) {
                fontSize=10,wordWrap=false,clipping=TextClipping.Clip,padding=new RectOffset(0,0,0,0),margin=new RectOffset(0,0,0,0)
            };
            hpText.normal.textColor=Color.white;
            heading=CartoonUi.Label(18,CartoonUi.Text,true);
            body=CartoonUi.Label(13,CartoonUi.Text);
            timer=new GUIStyle(CartoonUi.Label(34,CartoonUi.Text,true));
            section=CartoonUi.Label(11,CartoonUi.Muted);
            controls=new GUIStyle(section) {font=ArenaTheme.GlyphFont??GUI.skin.font};
            battlefieldNotice=new GUIStyle(CartoonUi.Label(13,CartoonUi.Text,true));
            battlefieldNotice.alignment=TextAnchor.MiddleCenter;
            rankName=new GUIStyle(body) {wordWrap=false,clipping=TextClipping.Clip};
            network=new GUIStyle(CartoonUi.Label(12,CartoonUi.Muted)) {wordWrap=false,alignment=TextAnchor.MiddleLeft};
            skillButton=new GUIStyle(CartoonUi.Button(CartoonUi.Ready,13,54));
            skillButton.font=CartoonUi.BodyFont;
            skillButton.wordWrap=true;
            skillButton.padding=new RectOffset(8,8,4,7);
            skillWaitingButton=new GUIStyle(CartoonUi.Button(CartoonUi.PanelDark,13,54));
            skillWaitingButton.font=skillButton.font;
            skillWaitingButton.wordWrap=skillButton.wordWrap;
            skillWaitingButton.padding=new RectOffset(8,8,4,7);
            foreach(var state in new[]{skillWaitingButton.normal,skillWaitingButton.hover,skillWaitingButton.active,skillWaitingButton.focused})state.textColor=CartoonUi.Text;
            skillUnavailableText=CartoonUi.Label(13,CartoonUi.Text);skillUnavailableText.alignment=TextAnchor.MiddleCenter;skillUnavailableText.padding=new RectOffset(8,8,4,7);
            leaveButton=CartoonUi.Button(CartoonUi.Danger,14,34);
            leaveButton.padding=new RectOffset(12,12,2,4);
        }
        public static void Fill(Rect r,Color color)  {
            GUI.color=color;
            GUI.DrawTexture(r,Texture2D.whiteTexture);
            GUI.color=Color.white;
        }
        static Rect Inset(Rect r,float n) {
            return new Rect(r.x+n,r.y+n,r.width-2*n,r.height-2*n);
        }
        Rect World(float wx,float wy,float w,float h) {
            return new Rect(x+wx*scale,y+(mapHeight-wy-h)*scale,w*scale,h*scale);
        }
        void Image(Rect r,string name,Color tint) {
            GUI.color=tint;
            var texture=Art(name);
            if(texture!=null) {
                Rect pixels=Rect.MinMaxRect(Mathf.Floor(r.x),Mathf.Floor(r.y),Mathf.Ceil(r.xMax),Mathf.Ceil(r.yMax));
                GUI.DrawTexture(pixels,texture,ScaleMode.StretchToFill);
            }
            GUI.color=Color.white;
        }
        void Shadow(Rect r,float alpha=1) {
            Fill(new Rect(r.x+2,r.y+3,r.width,r.height),new Color(0,0,0,.28f*alpha));
        }
        static Color Fade(Color color,float alpha) {color.a*=alpha;return color;}
        public void Draw(GameMap map,WorldState rendered,WorldState latest,uint tick,int localSlot,float arenaX,float arenaY,float unitScale)  {
            Styles();
            GUI.BeginGroup(new Rect(arenaX,arenaY,map.width*unitScale,map.height*unitScale));
            try {DrawContents(map,rendered,latest,tick,localSlot,Shake.x,Shake.y,unitScale);}
            finally {GUI.EndGroup();}
        }
        void DrawContents(GameMap map,WorldState rendered,WorldState latest,uint tick,int localSlot,float arenaX,float arenaY,float unitScale) {
            x=arenaX;
            y=arenaY;
            scale=unitScale;
            mapHeight=map.height;
            TankState observer=null;
            if(rendered!=null)rendered.Tanks.TryGetValue(localSlot,out observer);
            var bounds=World(0,0,map.width,map.height);
            Shadow(new Rect(bounds.x-7,bounds.y-7,bounds.width+14,bounds.height+14));
            Fill(new Rect(bounds.x-6,bounds.y-6,bounds.width+12,bounds.height+12),new Color(.2f,.27f,.28f));
            int rows=Mathf.CeilToInt(map.height/4),cols=Mathf.CeilToInt(map.width/4),middle=cols/2;
            for(int row=0;row<rows;row++)for(int col=0;col<cols;col++)  {
                bool sand=col>=middle-1&&col<=middle+1;
                string ground=(sand?"tileSand":"tileGrass")+((row*7+col*3)%5==0?"2":"1");
                if(col==middle-2)ground="tileGrass_transitionE";
                if(col==middle+2)ground="tileGrass_transitionW";
                Image(World(col*4,row*4,4,4),ground,sand?new Color(.88f,.89f,.79f):new Color(.69f,.76f,.62f));
            }
            // Decorative perimeter service road; terrain speed still uses map.water only.
            for(int row=1;row<=rows-2;row++)for(int col=1;col<=cols-2;col++)  {
                bool horizontal=row==1||row==rows-2,vertical=col==1||col==cols-2;
                if(!horizontal&&!vertical)continue;
                string tile=(col>=middle-1&&col<=middle+1?"tileSand":"tileGrass")+"_road";
                if(horizontal&&vertical)tile+="Corner"+(row==1?"L":"U")+(col==1?"L":"R");
                else tile+=horizontal?"East":"North";
                Image(World(col*4,row*4,4,4),tile,new Color(.77f,.83f,.73f));
            }
            // Low-contrast ground dressing never introduces obstacles or changes the map.
            foreach(var spawn in map.spawns)  {
                Image(World(spawn.x-1.2f,spawn.y-1.2f,2.4f,2.4f),"tracksDouble",new Color(.3f,.36f,.27f,.32f));
            }
            foreach(var water in map.water)DrawWater(water);
            DrawWrecks(observer,tick);
            if(rendered!=null)DrawFxPool(combatFx.Dust,map,rendered,observer,tick);
            foreach(var wall in map.walls)DrawSteel(wall);
            foreach(var wall in map.bricks)  {
                int hp=latest==null?GameRules.BrickMaxHp:latest.Bricks[wall.id];
                if(hp>0)DrawBrick(wall,hp);
            }
            DrawBattlefield(map,latest,tick);
            if(rendered!=null)  {
                DrawSkillWorld(map,rendered,tick);
                DrawItemWorld(rendered,observer,tick);
                foreach(var tank in rendered.Tanks.Values)if(CanDrawTank(tank,latest))DrawTank(tank,tick,localSlot);
                foreach(var bullet in rendered.Bullets.Values)DrawBullet(bullet,rendered);
                DrawCombatOverlays(map,rendered,observer,tick);
            }
            // Draw tank labels last so sprites cannot obscure the health bars.
            if(rendered!=null)foreach(var tank in rendered.Tanks.Values)if(tank.Hp>0)  {
                if(!Visible(observer,tank.X,tank.Y,tick))continue;
                float size=VisualSize;
                Rect r=World(tank.X-size/2,tank.Y-size/2,size,size);
                int hp=tank.Hp;
                TankState current=null;
                if(latest!=null&&latest.Tanks.TryGetValue(tank.Slot,out current))hp=current.Hp;
                if(hp<=0)continue;
                TankState bars=tank.Slot==localSlot?tank:current??tank;
                float alpha=1;
                Health(new Rect(r.center.x-26,r.y-18,52,6),hp,bars.MaxHp,tank.Slot,alpha);
                Energy(new Rect(r.center.x-26,r.y-10,52,6),bars.Energy,alpha);
                DrawSkillLabels(tank,tick,rendered);
                if(tank.Slot==localSlot)GUI.Label(new Rect(r.center.x-30,r.yMax+1,60,16),"BẠN",small);
            }
            DrawFog(map,rendered,localSlot,tick);
            DrawBattlefieldNotice(latest,tick,map.width*unitScale);
        }
        void DrawWater(MapRect water)  {
            Rect r=World(water.x,water.y,water.w,water.h);
            Shadow(r);
            Fill(r,new Color(.11f,.25f,.25f));
            Fill(Inset(r,2),new Color(.13f,.48f,.62f));
            Fill(new Rect(r.x+3,r.y+3,r.width-6,2),new Color(.4f,.78f,.8f));
            GUI.BeginGroup(Inset(r,3));
            float phase=Mathf.Sin(Time.unscaledTime*1.2f)*2;
            for(float wy=7;wy<r.height;wy+=13)for(float wx=-5;wx<r.width;wx+=24)  {
                Fill(new Rect(wx+phase+(int)(wy/13)%2*9,wy,11,1),new Color(.64f,.9f,.91f,.28f));
                Fill(new Rect(wx+phase+3,wy+3,6,1),new Color(.06f,.31f,.46f,.3f));
            }
            GUI.EndGroup();
        }
        void DrawSteel(MapRect wall)  {
            Rect r=World(wall.x,wall.y,wall.w,wall.h);
            Shadow(r);
            Fill(r,new Color(.17f,.23f,.25f));
            float step=2;
            for(float wy=0;wy<wall.h;wy+=step)for(float wx=0;wx<wall.w;wx+=step)  {
                Rect tile=World(wall.x+wx,wall.y+wy,Mathf.Min(step,wall.w-wx),Mathf.Min(step,wall.h-wy));
                Image(Inset(tile,.5f),"crateMetal",new Color(.78f,.86f,.88f));
            }
            Fill(new Rect(r.x,r.y,r.width,1),new Color(.7f,.8f,.79f,.75f));
        }
        Texture2D BrickTexture(int hp)  {
            if(brick[hp-1]!=null)return brick[hp-1];
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false) {
                filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave
            };
            texture.SetPixels(BrickPixels(hp));
            texture.Apply(false,true);
            brick[hp-1]=texture;
            return texture;
        }
        static Color[] BrickPixels(int hp) {
            var pixels=new Color[64*64];
            for(int py=0;py<64;py++)for(int px=0;px<64;px++)  {
                int row=py/16,by=py%16,bx=(px+(row%2)*16)%32;
                float variation=((px*13+py*7)%9)/180f;
                Color c=new Color(.66f+variation,.33f+variation,.19f+variation);
                if(by<2||bx<2)c=new Color(.24f,.18f,.14f);
                else if(by==15||bx==3)c=new Color(.87f,.54f,.32f);
                else if(by==2||bx==31)c=new Color(.4f,.22f,.14f);
                // Remove the upper third(s) with a chipped edge; collision keeps the full footprint.
                if(hp<GameRules.BrickMaxHp) {
                    int edge=64*hp/GameRules.BrickMaxHp+(px/8%3)-1;
                    if(py>=edge)c=Color.clear;
                    else if(py>=edge-2)c=new Color(.32f,.2f,.13f);
                    else if(py>5&&Mathf.Abs(px-(29+(py%13)/3))<=1)c=new Color(.19f,.14f,.1f);
                }
                pixels[py*64+px]=c;
            }
            return pixels;
        }
        void DrawBrick(MapRect wall,int hp)  {
            Rect r=World(wall.x,wall.y,wall.w,wall.h);
            var texture=BrickTexture(hp);
            if(hp==GameRules.BrickMaxHp) {Shadow(r);Fill(r,new Color(.2f,.16f,.12f));}
            else {
                GUI.color=new Color(0,0,0,.28f);
                GUI.DrawTexture(Inset(new Rect(r.x+2,r.y+3,r.width,r.height),.5f),texture);GUI.color=Color.white;
            }
            GUI.DrawTexture(Inset(r,.5f),texture);
        }
        void Rotated(Rect r,string name,float angle,Vector2 pivot,Color tint)  {
            Matrix4x4 saved=GUI.matrix;
            GuiTransforms.Rotate(angle,pivot);
            Image(r,name,tint);
            GUI.matrix=saved;
        }
        void RotatedTexture(Rect r,Texture2D texture,float angle,Vector2 pivot,Color tint) {
            Matrix4x4 saved=GUI.matrix;
            GuiTransforms.Rotate(angle,pivot);
            GUI.color=tint;
            GUI.DrawTexture(r,texture);
            GUI.matrix=saved;
            GUI.color=Color.white;
        }
        void DrawTank(TankState tank,uint tick,int localSlot)  {
            float size=VisualSize,multiplier=size/1.7f;
            Rect r=World(tank.X-size/2,tank.Y-size/2,size,size);
            Vector2 center=r.center;
            bool shield=tick<tank.Shield;
            int palette=tank.Nation>=0?tank.Nation:tank.Slot;
            float alpha=1;
            var shadow=new Rect(r.x+2,r.y+3,r.width,r.height);
            RotatedTexture(shadow,TankPalette.Body(palette),90-tank.Body/100f,shadow.center,new Color(0,0,0,.38f*alpha));
            Color tint=shield?Color.Lerp(Color.white,new Color(.65f,.94f,1),(.5f+.5f*Mathf.Sin(Time.unscaledTime*8))*.4f):Color.white;
            if(tick<tank.InvincibilityEndTick)tint=Color.Lerp(tint,ItemColors[4],tank.InvincibilityEndTick-tick<=15?.5f+.4f*Mathf.Sin(Time.unscaledTime*35):.65f);
            tint=Color.Lerp(tint,Color.white,combatFx.Flash(tank));
            tint.a=alpha;
            RotatedTexture(r,TankPalette.Body(palette),90-tank.Body/100f,center,tint);
            // Source barrel faces up; its rear end stays at the vehicle center.
            Rect barrel=new Rect(center.x-.19f*scale*multiplier,center.y-1.05f*scale*multiplier,.38f*scale*multiplier,1.18f*scale*multiplier);
            barrel.y+=combatFx.Recoil(tank)*scale;
            RotatedTexture(barrel,TankPalette.Barrel(palette),90-tank.Turret/100f,center,tint);
            float flash=combatFx.Flash(tank);
            if(flash>0) {
                Color white=new Color(1,1,1,flash*.85f);
                var mask=TankPalette.Flash(false);if(mask!=null)RotatedTexture(r,mask,90-tank.Body/100f,center,white);
                mask=TankPalette.Flash(true);if(mask!=null)RotatedTexture(barrel,mask,90-tank.Turret/100f,center,white);
            }

        }
        static bool CanDrawTank(TankState tank,WorldState latest) {
            if(tank.Hp<=0)return false;TankState current;
            return latest==null||latest.Tanks.TryGetValue(tank.Slot,out current)&&current.Hp>0&&current.Life==tank.Life;
        }
        void ShieldArc(Vector2 center,float radius,float start,float span,float alpha) {
            int segments=24;Vector2 previous=center+new Vector2(Mathf.Cos(start*Mathf.Deg2Rad),-Mathf.Sin(start*Mathf.Deg2Rad))*radius;
            for(int i=1;i<=segments;i++) {
                float angle=(start+span*i/segments)*Mathf.Deg2Rad;
                Vector2 next=center+new Vector2(Mathf.Cos(angle),-Mathf.Sin(angle))*radius,delta=next-previous;
                Matrix4x4 saved=GUI.matrix;GuiTransforms.Rotate(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,previous);
                Fill(new Rect(previous.x,previous.y,delta.magnitude,2),new Color(.4f,.85f,1,alpha));GUI.matrix=saved;previous=next;
            }
        }
        void DrawBullet(BulletState bullet,WorldState world)  {
            float size=ItemCodec.BulletSize(bullet.Kind);
            Rect r=World(bullet.X-CombatGeometry.BulletHalfWidth*size,bullet.Y-CombatGeometry.BulletHalfLength*size,2*CombatGeometry.BulletHalfWidth*size,2*CombatGeometry.BulletHalfLength*size);
            if(bullet.Kind>0) {
                EffectTextures();bool he=ItemCodec.HasHe(bullet.Kind),mirror=ItemCodec.HasMirror(bullet.Kind);
                Color color=he?ItemColors[2]:ItemColors[5];
                if(he&&mirror)Disc(bullet.X,bullet.Y,.8f*size,glow,Fade(ItemColors[5],.65f));
                Disc(bullet.X,bullet.Y,.55f*size,glow,Fade(color,.7f));
                if(!ItemCodec.IsGerman(bullet)) {
                    var saved=GUI.matrix;GuiTransforms.Rotate(90-bullet.Heading/100f,r.center);Fill(r,color);GUI.matrix=saved;return;
                }
            }
            if(ItemCodec.IsGerman(bullet)) {
                TankState owner;int palette=world.Tanks.TryGetValue(bullet.Owner,out owner)&&owner.Nation>=0?owner.Nation:4;
                var saved=GUI.matrix;GuiTransforms.Rotate(90-bullet.Heading/100f,r.center);
                Fill(new Rect(r.x-1,r.y-1,r.width+2,r.height+2),new Color(1,.08f,.1f));
                Fill(r,TankPalette.Colors[palette]);GUI.matrix=saved;
            }
            else Rotated(r,"bulletSand1_outline",90-bullet.Heading/100f,r.center,Color.white);
        }
        void Health(Rect r,int hp,int max,int key,float alpha=1)  {
            float target=Mathf.Clamp01((float)hp/max),shown;
            if(!health.TryGetValue(key,out shown)||target>shown)shown=target;
            if(Event.current.type==EventType.Repaint)shown=Mathf.MoveTowards(shown,target,Time.unscaledDeltaTime*2.5f);
            health[key]=shown;
            Shadow(r,alpha);
            Fill(r,Fade(Ink,alpha));
            Fill(Inset(r,1),new Color(.18f,.25f,.23f,alpha));
            bool vehicle=key<8;
            float inset=vehicle?1:2,numberWidth=vehicle?0:hp>=10?18:12,fillWidth=vehicle?r.width-2*inset:r.width-numberWidth-3;
            Fill(new Rect(r.x+inset,r.y+inset,fillWidth*shown,r.height-2*inset),new Color(.7f,.84f,.35f,alpha));
            Fill(new Rect(r.x+inset,r.y+inset,fillWidth*target,r.height-2*inset),Fade(Green,alpha));
            Fill(new Rect(r.x+inset,r.y+inset,fillWidth*target,1),new Color(.66f,1,.72f,alpha));
            GUI.color=new Color(1,1,1,alpha);
            if(!vehicle)GUI.Label(new Rect(r.xMax-numberWidth-1,r.y,numberWidth,r.height),hp.ToString(),hpText);
            GUI.color=Color.white;
        }
        void Energy(Rect r,int energy,float alpha=1) {
            Shadow(r,alpha);
            Fill(r,Fade(Ink,alpha));
            Fill(Inset(r,1),new Color(.09f,.19f,.28f,alpha));
            Fill(new Rect(r.x+1,r.y+1,(r.width-2)*Mathf.Clamp01((float)energy/Movement.MaxEnergy),r.height-2),new Color(.12f,.58f,1f,alpha));
        }
        Vector2 rankingScroll,skillStatusScroll;
        public bool Panel(ClientCore client,float panelX,float top,float width,float height,bool stale,bool autoFire=false)  {
            Styles();
            var r=new Rect(panelX,top,width,height);
            // One instrument rail: the arena, aiming bounds and world presentation stay untouched.
            CartoonUi.Panel(new Rect(panelX+3,top,width-6,height),CartoonUi.PanelDark,5);
            Fill(new Rect(panelX+3,top+8,3,80),CartoonUi.Gold);
            string mode=client.RoomMode==RoomMode.ROLEPLAY?"NHẬP VAI":"ĐẤU TỰ DO";
            GUI.Label(new Rect(panelX+20,top+11,width-40,26),mode,heading);
            var latest=client.Latest;
            if(latest==null)return false;
            int seconds=(int)Math.Ceiling(latest.Remaining/30.0);
            bool urgent=latest.Remaining>0&&latest.Remaining<=300;
            string clock=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
            float lineHeight=Mathf.Ceil(body.CalcHeight(new GUIContent("Ag"),width-42));
            if(urgent) {
                float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*Mathf.PI*2);
                timer.normal.textColor=Color.Lerp(CartoonUi.Text,CartoonUi.Danger,.25f+.4f*pulse);
            }
            else timer.normal.textColor=CartoonUi.Gold;
            CartoonUi.Icon(new Rect(panelX+21,top+46,21,21),UiIcon.Clock,urgent?CartoonUi.Danger:CartoonUi.Gold);
            GUI.Label(new Rect(panelX+52,top+31,width-72,48),clock,timer);
            MatchLoadout ownLoadout;
            string nation=client.LoadoutReady&&client.Loadouts.TryGetValue(client.Slot,out ownLoadout)?RoleplayContent.NationNames[ownLoadout.Nation]:"";
            GUI.Label(new Rect(panelX+21,top+79,width-42,lineHeight),nation.Length>0?"PHE · "+nation:"THỜI GIAN CÒN LẠI",section);
            Fill(new Rect(panelX+18,top+102,width-36,1),CartoonUi.PanelColor);
            TankState local;
            latest.World.Tanks.TryGetValue(client.Slot,out local);
            bool roleplay=client.RoomMode==RoomMode.ROLEPLAY;
            float itemTop=110,itemHeight=roleplay?60:local==null?32:Mathf.Max(32,body.CalcHeight(new GUIContent(ItemHudText(local,latest.Tick)),width-40)+10);
            if(roleplay) {
                GUI.Label(new Rect(panelX+20,top+itemTop,width-40,18),"TÚI VẬT PHẨM · BẤM ĐỂ DÙNG",section);
                InventoryHud(client,panelX+20,top+itemTop+20,width-40);
            }
            else if(local!=null)ItemHud(local,latest.Tick,panelX+20,top+itemTop+4,width-40,itemHeight-8);
            float rankTop=itemTop+itemHeight+26;
            GUI.Label(new Rect(panelX+20,top+rankTop-23,width-110,18),"XẾP HẠNG",section);
            GUI.Label(new Rect(panelX+width-72,top+rankTop-23,58,18),"HẠ / TỬ",section);
            var ranked=LiveRanking.Sort(latest.World);
            bool hasSkill=client.RoomMode==RoomMode.ROLEPLAY&&local!=null&&local.Nation>=0&&local.Nation<8;
            string skillLabel=hasSkill?SkillButtonLabel(client,local):"",skillNotice=hasSkill?SkillStatus(client,local):"";
            float skillHeight=hasSkill?Mathf.Max(54,skillUnavailableText.CalcHeight(new GUIContent(skillLabel),width-32)):0;
            float noticeHeight=string.IsNullOrEmpty(skillNotice)?0:Mathf.Ceil(body.CalcHeight(new GUIContent(skillNotice),width-48));
            float noticeSpace=Mathf.Max(lineHeight,Mathf.Floor((height-rankTop-112-skillHeight-14-64)/lineHeight)*lineHeight);
            float statusHeight=Mathf.Min(noticeHeight,noticeSpace),skillBlockHeight=skillHeight+(statusHeight>0?6+statusHeight:0);
            float rankHeight=Mathf.Max(32,height-rankTop-(hasSkill?112+skillBlockHeight+8:110));
            float rowWidth=width-38,statsWidth=52,totalHeight=8;
            foreach(var tank in ranked)statsWidth=Mathf.Max(statsWidth,Mathf.Ceil(rankName.CalcSize(new GUIContent(tank.Kills+" / "+tank.Deaths)).x)+4);
            float nameWidth=rowWidth-64-statsWidth;
            foreach(var tank in ranked) {
                string label=CartoonUi.WrapText(RankingLabel(client,tank),body,nameWidth);
                totalHeight+=Mathf.Max(32,Mathf.Ceil(body.CalcHeight(new GUIContent(label),nameWidth))+12)+4;
            }
            rankingScroll=GUI.BeginScrollView(new Rect(panelX+10,top+rankTop,width-20,rankHeight),rankingScroll,new Rect(0,0,rowWidth,totalHeight),false,false);
            float py=0;
            int rank=0;
            foreach(var tank in ranked)  {
                rank++;
                string label=CartoonUi.WrapText(RankingLabel(client,tank),body,nameWidth);
                float nameHeight=Mathf.Ceil(body.CalcHeight(new GUIContent(label),nameWidth)),rowHeight=Mathf.Max(32,nameHeight+12);
                if(tank.Slot==client.Slot) {
                    CartoonUi.Panel(new Rect(0,py,rowWidth,rowHeight),Color.Lerp(CartoonUi.PanelDark,CartoonUi.Ready,.22f),4);
                    Fill(new Rect(0,py+5,3,rowHeight-10),CartoonUi.Ready);
                }
                else Fill(new Rect(8,py+rowHeight+1,rowWidth-12,1),Color.Lerp(CartoonUi.PanelDark,CartoonUi.PanelColor,.65f));
                GUI.Label(new Rect(6,py+6,24,lineHeight),"#"+rank,section);
                GUI.DrawTexture(new Rect(32,py+7,18,18),TankPalette.Body(tank.Nation>=0?tank.Nation:tank.Slot));
                GUI.Label(new Rect(56,py+6,nameWidth,nameHeight),label,body);
                GUI.Label(new Rect(rowWidth-statsWidth-2,py+6,statsWidth,lineHeight),tank.Kills+" / "+tank.Deaths,rankName);
                py+=rowHeight+4;
            }
            GUI.EndScrollView();
            if(hasSkill)SkillHud(client,local,panelX+16,r.yMax-112-skillBlockHeight,width-32,skillLabel,skillNotice,skillHeight,statusHeight);
            Fill(new Rect(panelX+18,r.yMax-108,width-36,1),CartoonUi.PanelColor);
            GUI.Label(new Rect(panelX+18,r.yMax-101,width-36,18),"WASD / mũi tên · Shift: tăng tốc",body);
            GUI.Label(new Rect(panelX+18,r.yMax-83,width-36,18),"Chuột phải: tự bắn "+(autoFire?"BẬT":"TẮT"),body);
            string warning=!GameWindowFocus.IsForeground&&!ArenaPreview.Enabled?"Bấm vào game để điều khiển":stale?"Mạng chậm — chờ trạng thái":"";
            if(warning.Length>0)GUI.Label(new Rect(panelX+18,r.yMax-64,width-36,22),warning,body);
            else if(roleplay) {
                int next=((int)client.EstimatedSkillTick/GameRules.RoleplayItemIntervalTicks+1)*GameRules.RoleplayItemIntervalTicks;
                string shop=client.ItemWave>0?"E: Mở shop · còn "+Math.Max(0,(int)Math.Ceiling((Math.Min(client.ItemMatchTicks,client.ItemWave*GameRules.RoleplayItemIntervalTicks+GameRules.RoleplayItemWindowTicks)-(double)client.EstimatedSkillTick)/30))+"s":next<client.ItemMatchTicks?"E: Mở shop · sau "+Math.Max(0,(int)Math.Ceiling((next-(double)client.EstimatedSkillTick)/30))+"s":"E: Mở shop · đã hết đợt";
                GUI.Label(new Rect(panelX+18,r.yMax-64,width-36,22),shop,body);
            }
            return GUI.Button(new Rect(panelX+16,r.yMax-42,width-32,34),"Rời trận",leaveButton);
        }
        static string RankingLabel(ClientCore client,TankState tank) {
            string name="P"+(tank.Slot+1);
            foreach(var player in client.Players)if(player.Slot==tank.Slot)name=player.Name;
            return (tank.Slot==client.Slot?"Bạn · ":"")+name;
        }
        public void NetworkStatus(ClientCore client,float left,float top,float width) {
            Styles();
            if(width<80)return;
            string ping=client.PingFresh?Math.Ceiling(client.LastRttUs/1000)+" ms":client.LastPongUs>0?"mất phản hồi":"đang đo…";
            network.normal.textColor=!client.PingFresh||client.LastRttUs>200000?CartoonUi.Danger:client.LastRttUs>100000?CartoonUi.Gold:CartoonUi.Ready;
            Fill(new Rect(left,top+9,5,5),network.normal.textColor);
            GUI.Label(new Rect(left+12,top,width-12,22),"Ping "+ping+"  ·  FPS "+Mathf.RoundToInt(1/Mathf.Max(.001f,Time.smoothDeltaTime)),network);
        }
        static string SkillButtonLabel(ClientCore client,TankState tank) {
            int index=tank.Nation;
            int remaining=Math.Max(0,client.SkillReadyTick(0)-(int)client.EstimatedSkillTick);
            bool aiming=tank.Nation==1&&tank.QStage==1;
            string state=client.SkillPending(0)?"Đang chờ server":aiming?"Đang ngắm · Q để chốt":remaining>0?"Đang hồi "+Math.Ceiling(remaining/30.0)+"s":"Sẵn sàng";
            int active=Math.Max(0,tank.QEndTick-(int)client.EstimatedSkillTick);
            if(active>0&&!client.SkillPending(0))state=(aiming?"Ngắm "+Math.Ceiling(active/30.0)+"s · Q chốt":"Hiệu lực "+Math.Ceiling(active/30.0)+"s")+(remaining>0?" · Hồi "+Math.Ceiling(remaining/30.0)+"s":"");
            return "Q · "+RoleplayContent.QNames[index]+"\n"+state;
        }
        void SkillHud(ClientCore client,TankState tank,float left,float top,float width,string label,string notice,float buttonHeight,float statusHeight) {
            int index=tank.Nation,remaining=Math.Max(0,client.SkillReadyTick(0)-(int)client.EstimatedSkillTick);
            bool aiming=tank.Nation==1&&tank.QStage==1;
            if(Time.unscaledTime<qResetUntil)CartoonUi.Panel(new Rect(left-3,top-3,width+6,buttonHeight+6),Purple,13,0);
            bool enabled=GUI.enabled;
            GUI.enabled=enabled&&GameWindowFocus.IsForeground&&tank.Hp>0&&!client.SkillPending(0)&&(remaining==0||aiming);
            bool available=GUI.enabled;
            var buttonRect=new Rect(left,top,width,buttonHeight);var content=new GUIContent(label);
            if(GUI.Button(buttonRect,available?content:GUIContent.none,available?skillButton:skillWaitingButton))client.UseSkill(0);
            GUI.enabled=enabled;
            if(!available)GUI.Label(buttonRect,content,skillUnavailableText);
            if(remaining>0) {
                float cooldown=Mathf.Clamp01((float)remaining/RoleplayContent.NationCooldowns[index]);
                Fill(new Rect(left+9,top+buttonHeight-7,(width-18)*cooldown,3),Cyan);
            }
            if(statusHeight>0) {
                float contentHeight=Mathf.Ceil(body.CalcHeight(new GUIContent(notice),width-16));
                skillStatusScroll=GUI.BeginScrollView(new Rect(left,top+buttonHeight+6,width,statusHeight),skillStatusScroll,new Rect(0,0,width-16,contentHeight),false,false);
                GUI.Label(new Rect(0,0,width-16,contentHeight),notice,body);GUI.EndScrollView();
            }
        }
        static string SkillStatus(ClientCore client,TankState tank) {
            string statuses="";
            if(client.Latest.Tick<tank.SlowEndTick)statuses+="Chậm "+(100-GameRules.SlowSpeedPercent)+"% "+Math.Ceiling(Math.Max(0,tank.SlowEndTick-client.EstimatedSkillTick)/30.0)+"s  ";
            if(tank.RocketSlowStacks>0&&client.Latest.Tick<tank.RocketSlowEndTick)statuses+="Chậm tên lửa "+Math.Round((1-Math.Pow(GameRules.RocketSlowSpeedPercent/100.0,tank.RocketSlowStacks))*100,1)+"% · "+tank.RocketSlowStacks+" lần trúng · "+Math.Ceiling(Math.Max(0,tank.RocketSlowEndTick-client.EstimatedSkillTick)/30.0)+"s  ";
            int areaSlowEnd=SkillGeometry.KatyushaSlowEnd(tank,client.Latest.Tick,client.Latest.World.Effects);
            if(areaSlowEnd>client.Latest.Tick)statuses+="Chậm vùng "+RoleplayContent.NationNames[1]+" "+(100-GameRules.KatyushaSlowSpeedPercent)+"% "+Math.Ceiling(Math.Max(0,areaSlowEnd-client.EstimatedSkillTick)/30.0)+"s  ";
            if(client.Latest.Tick<tank.ConfusionEndTick)statuses+="Đảo WASD/mũi tên / khóa nòng "+Math.Ceiling(Math.Max(0,tank.ConfusionEndTick-client.EstimatedSkillTick)/30.0)+"s  ";
            if(client.Latest.Tick<tank.FireLockEndTick)statuses+="Khóa bắn "+Math.Ceiling(Math.Max(0,tank.FireLockEndTick-client.EstimatedSkillTick)/30.0)+"s  ";
            if(client.Latest.Tick<tank.FogEndTick)statuses+="Sương mù "+Math.Ceiling(Math.Max(0,tank.FogEndTick-client.EstimatedSkillTick)/30.0)+"s";
            return statuses.Length>0?statuses:client.SkillNotice=="Q đã được kích hoạt"?"":client.SkillNotice;
        }
        static string SkillSymbol(int nation) {return new[]{"◆","⊕","⇄","◉","≋","↗","!","ϟ"}[nation];}
        public void Dispose() {
            ResetEffects();
            foreach(var texture in brick)if(texture!=null)UnityEngine.Object.Destroy(texture);
            foreach(var texture in new[]{glow,ring,fog,energyField})if(texture!=null)UnityEngine.Object.Destroy(texture);
        }
    }
}
