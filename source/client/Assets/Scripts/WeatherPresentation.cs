using UnityEngine;
namespace Tank {
    public sealed partial class ArenaRenderer {
        BattlefieldWave visibilityWave;
        GameMap weatherMap;
        TankState weatherObserver;
        Texture2D weatherCloud;
        GUIStyle weatherTitle,weatherDetail;
        uint weatherSoundWave;

        // Weather changes contrast, never the existence of a world sprite.
        // Fog retains its own visibility rules and final map mask.
        void WorldTexture(Rect rect,Texture2D texture,Rect uv) {
            if(texture!=null)GUI.DrawTextureWithTexCoords(rect,texture,uv);
        }
        void WorldFill(Rect rect,Color color) {Fill(rect,color);}
        void WeatherTextures() {
            if(weatherCloud!=null)return;
            const int width=128,height=64;
            weatherCloud=new Texture2D(width,height,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color[width*height];
            for(int py=0;py<height;py++)for(int px=0;px<width;px++) {
                float u=(px+.5f)/width*2-1,v=(py+.5f)/height*2-1;
                float edge=Mathf.Clamp01(1-u*u-v*v);
                float folds=.7f+.15f*Mathf.Sin(u*11+v*4)+.1f*Mathf.Cos(v*13-u*5);
                pixels[py*width+px]=new Color(1,1,1,Mathf.SmoothStep(0,1,edge)*folds);
            }
            weatherCloud.SetPixels(pixels);weatherCloud.Apply(false,true);
        }
        // Crop clouds to the map without rotating the IMGUI group clip.
        void WeatherStamp(Rect rect,Rect bounds,Texture2D texture,Color tint) {
            float left=Mathf.Max(rect.xMin,bounds.xMin),right=Mathf.Min(rect.xMax,bounds.xMax);
            float top=Mathf.Max(rect.yMin,bounds.yMin),bottom=Mathf.Min(rect.yMax,bounds.yMax);
            if(right<=left||bottom<=top)return;
            Rect uv=new Rect((left-rect.x)/rect.width,(rect.yMax-bottom)/rect.height,(right-left)/rect.width,(bottom-top)/rect.height);
            Color old=GUI.color;GUI.color=tint;
            GUI.DrawTextureWithTexCoords(new Rect(left,top,right-left,bottom-top),texture,uv);GUI.color=old;
        }
        Color WeatherAccent(bool sand) {return sand?new Color(1,.77f,.3f):new Color(.65f,.88f,1);}
        void WeatherSymbol(Vector2 p,Color color) {
            for(int row=0;row<3;row++) {
                Vector2 start=p+new Vector2(row==1?0:4,row*5-5);
                Line(start,start+Vector2.right*(row==1?18:11),1.5f,color,true);
            }
        }
        float WeatherStrength(BattlefieldWave wave,float tick) {
            if(wave==null||!wave.Affecting(tick))return 0;
            return (.3f+.7f*Mathf.Clamp01((tick-wave.Impact)/15))*Mathf.Clamp01((wave.End-tick)/15);
        }
        void DrawWeatherBackdrop(GameMap map,float tick) {
            var wave=visibilityWave;
            if(wave==null||!wave.IsWeather||!BattlefieldPresentation.Active(wave,tick))return;
            WeatherTextures();bool sand=wave.Kind==BattlefieldWave.Sandstorm;
            Color accent=WeatherAccent(sand);Rect bounds=World(0,0,map.width,map.height);
            if(tick<wave.Impact) {
                // A light, full-map lead-in warns about a global storm; there
                // is no advancing edge or suggestion that one side is safe.
                float progress=Mathf.Clamp01((tick-wave.Start)/(wave.Impact-wave.Start));
                Fill(bounds,sand?new Color(.74f,.49f,.18f,.04f+.07f*progress):new Color(.32f,.48f,.62f,.04f+.07f*progress));
                for(int i=0;i<18;i++) {
                    float wx=Mathf.Repeat(i*29.137f+tick*.12f,map.width),wy=Mathf.Repeat(i*17.719f,map.height);
                    WeatherStamp(World(wx-7,wy-3,14,6),bounds,weatherCloud,Fade(accent,.12f*progress));
                }
                return;
            }
            float age=(tick-wave.Impact)/30,strength=WeatherStrength(wave,tick);
            Fill(bounds,sand?new Color(.61f,.38f,.13f,.30f*strength):new Color(.25f,.4f,.54f,.28f*strength));
            // Broad billows cover the complete arena while vehicles and their
            // labels stay above this layer. Only the wind texture moves.
            for(int i=0;i<80;i++) {
                float wx=Mathf.Repeat(i*29.137f+age*(sand?5:3),map.width+18)-9,wy=Mathf.Repeat(i*17.719f-age*1.4f,map.height+16)-8;
                float size=16+i%5*3.2f,height=size*(.48f+(i%3)*.08f);
                Color color=sand?(i%3==0?new Color(.99f,.77f,.4f,.32f):new Color(.76f,.52f,.23f,.23f)):
                    (i%3==0?new Color(.91f,.96f,1,.37f):new Color(.43f,.59f,.73f,.27f));
                color.a*=strength;
                WeatherStamp(World(wx-size/2,wy-height/2,size,height),bounds,weatherCloud,color);
            }
            for(int i=0;i<18;i++) {
                float wx=Mathf.Repeat(i*23.71f+age*8,map.width);
                float wy=Mathf.Repeat(i*11.71f+Mathf.Sin(age+i)*.6f,map.height);
                Vector2 previous=Point(wx,wy);
                for(int step=1;step<=6;step++) {
                    Vector2 next=Point(Mathf.Min(map.width,wx+step*1.2f),wy+Mathf.Sin(step*.55f+i)*.65f);
                    Line(previous,next,Mathf.Max(1,scale*.14f),Fade(accent,.36f*strength),true);previous=next;
                }
            }
        }
        void DrawWeatherParticles(GameMap map,float tick) {
            var wave=visibilityWave;
            if(wave==null||!wave.IsWeather||!wave.Affecting(tick))return;
            bool sand=wave.Kind==BattlefieldWave.Sandstorm;float age=(tick-wave.Impact)/30,strength=WeatherStrength(wave,tick);
            int count=sand?320:480;
            for(int i=0;i<count;i++) {
                float wx=Mathf.Repeat(i*29.137f+age*(sand?24:18),map.width);
                float wy=Mathf.Repeat(i*17.719f-age*(sand?2:4),map.height);
                float alpha=strength;
                // Keep a clear pocket around the local vehicle. HP and labels
                // are drawn above particles; other vehicles never disappear.
                if(weatherObserver!=null&&weatherObserver.Hp>0) {
                    float dx=wx-weatherObserver.X,dy=wy-weatherObserver.Y;
                    alpha*=Mathf.Clamp01((Mathf.Sqrt(dx*dx+dy*dy)-2.5f)/3.5f);
                }
                Vector2 p=Point(wx,wy);
                Color color=sand?new Color(1,.87f,.57f,.63f*alpha):new Color(.95f,.98f,1,.88f*alpha);
                float length=(sand?1.2f:.55f+(i%3)*.17f)*scale;
                float end=Mathf.Min(length,(map.width-wx)*scale);
                float thickness=Mathf.Max(1,scale*(sand?.12f:.18f)),slope=sand?.12f:.35f;
                // Two bounded strips per streak, independent of resolution.
                Fill(new Rect(p.x,p.y,end*.52f,thickness),color);
                Fill(new Rect(p.x+end*.48f,p.y+slope*end*.5f,end*.52f,thickness),color);
                if(!sand&&i%3==0)Fill(new Rect(p.x,p.y,Mathf.Max(1.5f,scale*.3f),Mathf.Max(1.5f,scale*.3f)),color);
            }
        }
        void DrawWeatherNotice(BattlefieldWave wave,float now,float width) {
            bool warning=now<wave.Impact,sand=wave.Kind==BattlefieldWave.Sandstorm;
            if(weatherTitle==null) {
                weatherTitle=new GUIStyle(CartoonUi.Label(16,CartoonUi.Text,true)){alignment=TextAnchor.MiddleLeft};
                weatherDetail=new GUIStyle(CartoonUi.Label(13,CartoonUi.Muted)){alignment=TextAnchor.MiddleLeft};
            }
            float panelWidth=Mathf.Min(440,width-24),seconds=((warning?wave.Impact:wave.End)-now)/30;
            Rect panel=new Rect((width-panelWidth)/2,5,panelWidth,57);Color accent=WeatherAccent(sand);
            CartoonUi.Panel(panel,CartoonUi.PanelDark,8,2);
            Fill(new Rect(panel.x+1,panel.y+8,3,panel.height-16),accent);
            WeatherSymbol(new Vector2(panel.x+13,panel.y+19),accent);
            string title=BattlefieldPresentation.Name(wave.Kind).ToUpperInvariant()+" · "+(warning?"SẮP ĐẾN ":"TOÀN BẢN ĐỒ · ")+Mathf.CeilToInt(seconds)+"s";
            GUI.Label(new Rect(panel.x+42,panel.y+4,panel.width-52,24),title,weatherTitle);
            string effect=sand?"Tốc độ bắn −"+(100-GameRules.SandstormFireRatePercent)+"%":"Tốc độ di chuyển −"+(100-GameRules.SnowstormSpeedPercent)+"%";
            string detail=warning?"Toàn bản đồ · "+effect:effect+" · 2 HP/2s";
            GUI.Label(new Rect(panel.x+13,panel.y+29,panel.width-26,22),detail,weatherDetail);
        }
        void WeatherAudio(ClientCore client,SkillSounds audio) {
            var wave=client.Latest.World.Battlefield;float tick=client.EstimatedSkillTick;
            if(wave==null||!wave.IsWeather||!BattlefieldPresentation.Active(wave,tick)) {audio.Weather(0);return;}
            if(weatherSoundWave!=wave.Id) {
                weatherSoundWave=wave.Id;
                if(tick>=wave.Start&&tick-wave.Start<=6)audio.WeatherWarning(wave.Kind);
            }
            audio.WeatherGain(wave.Affecting(tick)?wave.Kind:0,.625f*WeatherStrength(wave,tick));
        }
        void DisposeWeatherTextures() {
            foreach(var texture in new[]{weatherCloud})if(texture!=null) {
                if(Application.isPlaying)Object.Destroy(texture);else Object.DestroyImmediate(texture);
            }
            weatherCloud=null;
        }
    }
}
