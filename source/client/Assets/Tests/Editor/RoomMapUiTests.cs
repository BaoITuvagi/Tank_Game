using NUnit.Framework;
using Tank;
using UnityEngine;

public class RoomMapUiTests {
    [TestCase(960,600)] [TestCase(1280,800)] [TestCase(1600,900)] [TestCase(1920,1080)]
    public void ThreeMapChoicesFitScrollableBodyAndKeepActionsSeparate(int width,int height) {
        var menu=MenuLayout.Fit(width,height);
        float cardWidth=Mathf.Min(920,menu.Content.width),cardHeight=Mathf.Min(560,menu.Content.height);
        var card=new Rect(menu.Content.center.x-cardWidth/2,menu.Content.center.y-cardHeight/2,cardWidth,cardHeight);
        var body=new Rect(card.x+24,card.y+88,card.width-48,card.height-188);
        var actions=new Rect(card.x+24,card.yMax-84,card.width-48,64);
        Assert.GreaterOrEqual(body.height,240);
        Assert.LessOrEqual(body.yMax,actions.y-12);
        Assert.LessOrEqual(actions.yMax,card.yMax-20);
        float contentWidth=body.width-30;
        var choices=new Rect(0,0,contentWidth,RoomMapUiGeometry.Height(contentWidth,3));
        Assert.AreEqual(3,RoomMapUiGeometry.Columns(contentWidth,3));
        for(int i=0;i<3;i++) {
            var choice=RoomMapUiGeometry.Card(choices,i,3);
            Assert.GreaterOrEqual(choice.width,240);
            Assert.GreaterOrEqual(choice.x,choices.x);Assert.LessOrEqual(choice.xMax,choices.xMax+.01f);
            Assert.LessOrEqual(choice.yMax,choices.yMax);
            for(int j=0;j<i;j++)Assert.IsFalse(choice.Overlaps(RoomMapUiGeometry.Card(choices,j,3)));
        }
    }
    [TestCase(128,80)] [TestCase(120,72)] [TestCase(80,128)] [TestCase(40,40)]
    public void ThumbnailKeepsMapAspectAndFitsInsideItsBounds(float width,float height) {
        var bounds=new Rect(14,22,236,112);
        var thumbnail=RoomMapUiGeometry.Thumbnail(bounds,width,height);
        Assert.AreEqual(width/height,thumbnail.width/thumbnail.height,.0001f);
        Assert.GreaterOrEqual(thumbnail.x,bounds.x-.01f);Assert.LessOrEqual(thumbnail.xMax,bounds.xMax+.01f);
        Assert.GreaterOrEqual(thumbnail.y,bounds.y-.01f);Assert.LessOrEqual(thumbnail.yMax,bounds.yMax+.01f);
        Assert.AreEqual(bounds.center.x,thumbnail.center.x,.001f);Assert.AreEqual(bounds.center.y,thumbnail.center.y,.001f);
    }
    [Test] public void NarrowScrollableMapListStacksChoicesWithoutClipping() {
        var bounds=new Rect(0,0,360,RoomMapUiGeometry.Height(360,3));
        Assert.AreEqual(1,RoomMapUiGeometry.Columns(bounds.width,3));
        Assert.AreEqual(3*RoomMapUiGeometry.CardHeight+2*RoomMapUiGeometry.Gap,bounds.height);
        Assert.AreEqual(bounds.yMax,RoomMapUiGeometry.Card(bounds,2,3).yMax);
    }
}
