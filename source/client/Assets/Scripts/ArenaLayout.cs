using System;
namespace Tank {
    public readonly struct ArenaLayout {
        public const float SidebarWidth=294,Top=88;
        public readonly float X,Y,Scale,SidebarX,Height;
        ArenaLayout(float x,float y,float scale,float sidebarX,float height) {X=x;Y=y;Scale=scale;SidebarX=sidebarX;Height=height;}
        public static ArenaLayout Fit(float width,float height,float mapWidth,float mapHeight) {
            float sidebarX=Math.Max(0,width-SidebarWidth),availableWidth=Math.Max(1,sidebarX-12),availableHeight=Math.Max(1,height-112);
            float scale=Math.Min(availableWidth/Math.Max(1,mapWidth),availableHeight/Math.Max(1,mapHeight));
            return new ArenaLayout(6+Math.Max(0,(availableWidth-mapWidth*scale)/2),Top+Math.Max(0,(availableHeight-mapHeight*scale)/2),scale,sidebarX,availableHeight);
        }
    }
}
