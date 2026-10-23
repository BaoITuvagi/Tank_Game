using System;
using UnityEngine;
namespace Tank {
    public sealed partial class ArenaRenderer {
        static readonly Color[] ItemColors={new Color(.2f,1,.42f),new Color(.7f,.84f,.96f),new Color(1,.42f,.08f),Color.white,new Color(1,.9f,.18f),new Color(.82f,.32f,1)};
        static readonly string[] ItemSymbols={"+","A","HE","P","I","M"};
        static readonly string[] ItemNames={"Cứu thương","Giáp","Đạn nổ","Thanh tẩy","Bất tử","Phân tách"};
        void ItemIcon(Rect r,int type) {
            Color c=ItemColors[type];
            Shadow(r);Fill(r,new Color(.04f,.07f,.1f,.95f));
            Fill(new Rect(r.x,r.y,r.width,2),c);Fill(new Rect(r.x,r.yMax-2,r.width,2),c);
            Fill(new Rect(r.x,r.y,2,r.height),c);Fill(new Rect(r.xMax-2,r.y,2,r.height),c);
            GUI.color=c;GUI.Label(r,new GUIContent(ItemSymbols[type],ItemNames[type]),small);GUI.color=Color.white;
        }
        void DrawItemWorld(WorldState world,TankState observer,uint tick) {
            EffectTextures();
            foreach(var item in world.Items.Values) {
                if(item.IsExpired(tick)||!Visible(observer,item.X,item.Y,tick))continue;
                float pulse=.75f+.2f*Mathf.Sin(Time.unscaledTime*4+item.Id);
                FxSprite("pickup_ui/item_glow",item.X,item.Y,2,combatFx.Clock,new Color(1,1,1,.6f));
                Disc(item.X,item.Y,1,glow,Fade(ItemColors[item.Type],pulse*.55f));
                float side=Mathf.Max(16,GameRules.ItemHalfSize*2*scale);
                Vector2 p=Point(item.X,item.Y);ItemIcon(new Rect(p.x-side/2,p.y-side/2,side,side),item.Type);
            }
            foreach(var tank in world.Tanks.Values) {
                if(tank.Hp<=0||!Visible(observer,tank.X,tank.Y,tick))continue;
                if(tick<tank.ArmorEndTick) {
                    Disc(tank.X,tank.Y,TankSize*.82f,ring,Fade(ItemColors[1],.8f));
                    ShieldArc(Point(tank.X,tank.Y),TankSize*.7f*scale,0,360,.8f);
                }
                if(tick<tank.InvincibilityEndTick) {
                    float blink=tank.InvincibilityEndTick-tick<=15?.45f+.45f*Mathf.Sin(Time.unscaledTime*35):.85f;
                    Disc(tank.X,tank.Y,TankSize,glow,Fade(ItemColors[4],blink));
                    Disc(tank.X,tank.Y,TankSize*.85f,ring,Fade(ItemColors[4],blink));
                }
            }
        }
        void ItemHud(TankState t,uint tick,float left,float top,float width) {
            string text="";
            if(t.Hp>0) {
                if(tick<t.ArmorEndTick)text+="Giáp "+((t.ArmorEndTick-tick)/30f).ToString("0.0")+"s  ";
                if(tick<t.InvincibilityEndTick)text+="Bất tử "+((t.InvincibilityEndTick-tick)/30f).ToString("0.0")+"s  ";
                if(tick<t.HeEndTick)text+="HE "+((t.HeEndTick-tick)/30f).ToString("0.0")+"s  ";
                if(t.MirrorShots>0)text+="Mirror ×"+t.MirrorShots;
            }
            GUI.Label(new Rect(left,top,width,43),text.Length>0?text:"Vật phẩm: chạm để nhặt",body);
        }
    }
}
