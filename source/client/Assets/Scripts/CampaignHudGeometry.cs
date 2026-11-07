using UnityEngine;

namespace Tank.Campaign {
    public static class CampaignHudGeometry {
        // Coordinates are local to the clipped 13 x 13 map, including at its edges.
        public static Rect PlayerHealthBounds(Rect player,Rect viewport){
            float width=Mathf.Min(viewport.width-4,Mathf.Clamp(player.width*.82f,30,60)),height=8;
            return new Rect(Mathf.Clamp(player.center.x-width/2,viewport.x+2,viewport.xMax-width-2),Mathf.Clamp(player.y-11,viewport.y+2,viewport.yMax-height-2),width,height);
        }
        public static Rect EffectBounds(Rect cluster,int index){return new Rect(cluster.x+index*50,cluster.y,44,44);}
    }
}
