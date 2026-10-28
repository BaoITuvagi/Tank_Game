using UnityEngine;
namespace Tank {
    public struct MenuLayout {
        public float Width,Height;
        public Rect Content,AuthCard,AuthLogo;
        public bool Wide;
        public static MenuLayout Fit(float width,float height) {
            var layout=new MenuLayout {Width=Mathf.Max(720,width),Height=Mathf.Max(600,height)};
            float contentWidth=Mathf.Min(1320,layout.Width-48);
            layout.Content=new Rect((layout.Width-contentWidth)/2,100,contentWidth,layout.Height-124);
            layout.Wide=width>=1100;
            float cardWidth=layout.Wide?Mathf.Min(540,contentWidth*.47f):Mathf.Min(560,contentWidth);
            float cardHeight=Mathf.Min(620,layout.Content.height);
            layout.AuthCard=new Rect(layout.Wide?layout.Content.xMax-cardWidth:layout.Content.center.x-cardWidth/2,layout.Content.center.y-cardHeight/2,cardWidth,cardHeight);
            float logoSize=Mathf.Min(560,layout.Content.width-cardWidth-48,layout.Content.height);
            layout.AuthLogo=layout.Wide?new Rect(layout.Content.x,layout.Content.center.y-logoSize/2,logoSize,logoSize):new Rect((layout.Width-72)/2,2,72,72);
            return layout;
        }
    }
}
