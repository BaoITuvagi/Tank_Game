using NUnit.Framework;
using System.Reflection;
using Tank;
using Tank.Campaign;
using UnityEngine;

public class SkillDetailsUiTests {
    [SetUp] public void Reset(){SkillDetailsUi.Reset();}
    [TearDown] public void Cleanup(){SkillDetailsUi.Reset();}
    [Test] public void InspectionBlocksGameplayUntilClosedAndAllHeldControlsAreReleased(){
        SkillDetailsUi.Sync("campaign:run1");
        SkillDetailsUi.Open(new Rect(700,400,200,70),"Không kích","Thông tin kỹ năng");
        Assert.IsTrue(SkillDetailsUi.IsOpen);Assert.IsTrue(SkillDetailsUi.InputBlocked);
        SkillDetailsUi.BeforeInput(true,false,false);
        Assert.IsTrue(SkillDetailsUi.InputBlocked,"Reading stays modal even after the opening click is released.");
        SkillDetailsUi.Close();
        SkillDetailsUi.BeforeInput(true,true,false);
        Assert.IsFalse(SkillDetailsUi.IsOpen);Assert.IsTrue(SkillDetailsUi.InputBlocked,"Closing cannot resume a held fire/skill/movement binding.");
        SkillDetailsUi.BeforeInput(true,false,false);
        Assert.IsFalse(SkillDetailsUi.InputBlocked);
    }
    [Test] public void EscapeFocusLossAndNewMatchCloseInspectionWithoutLeavingHeldInputActive(){
        SkillDetailsUi.Sync("multi:1");
        SkillDetailsUi.Open(new Rect(700,400,200,70),"Kỹ năng","Mô tả");
        SkillDetailsUi.BeforeInput(true,true,true);
        Assert.IsFalse(SkillDetailsUi.IsOpen);Assert.IsTrue(SkillDetailsUi.InputBlocked);
        SkillDetailsUi.Open(new Rect(700,400,200,70),"Kỹ năng","Mô tả");
        SkillDetailsUi.BeforeInput(false,true,false);
        Assert.IsFalse(SkillDetailsUi.IsOpen);Assert.IsTrue(SkillDetailsUi.InputBlocked);
        SkillDetailsUi.Open(new Rect(700,400,200,70),"Kỹ năng","Mô tả");
        SkillDetailsUi.Sync("multi:2");
        Assert.IsFalse(SkillDetailsUi.IsOpen);Assert.IsTrue(SkillDetailsUi.InputBlocked);
    }
    [TestCase(960,600)] [TestCase(1280,800)] [TestCase(1600,900)] [TestCase(1920,1080)]
    public void AnchoredInspectionFitsScreenAfterResizeAndLongDescriptions(int width,int height){
        foreach(Rect anchor in new[]{new Rect(width-294,height-200,270,70),new Rect(0,0,40,40),new Rect(1900,1000,30,30)}){
            var card=SkillDetailsUi.Place(anchor,400,2000,width,height);
            Assert.GreaterOrEqual(card.x,16);Assert.GreaterOrEqual(card.y,16);
            Assert.LessOrEqual(card.xMax,width-16);Assert.LessOrEqual(card.yMax,height-16);
        }
    }
    [TestCase(0f)] [TestCase(.25f)] [TestCase(.5f)] [TestCase(1f)]
    public void CooldownOutlineLengthMatchesRemainingFractionAndStaysOnTheTile(float fraction){
        var box=new Rect(10,20,270,70);var segments=BattleHudUi.CooldownSegments(box,fraction,3);
        float length=0;
        for(int i=0;i<segments.Length;i++){
            var segment=segments[i];length+=i%2==0?segment.width:segment.height;
            if(segment.width<=0||segment.height<=0)continue;
            Assert.GreaterOrEqual(segment.x,box.x);Assert.GreaterOrEqual(segment.y,box.y);
            Assert.LessOrEqual(segment.xMax,box.xMax);Assert.LessOrEqual(segment.yMax,box.yMax);
        }
        Assert.AreEqual(2*(box.width+box.height)*fraction,length,.001);
    }
    [Test] public void CooldownFractionClampsInvalidAndOverdueState(){
        Assert.AreEqual(1,BattleHudUi.CooldownFraction(50,24));
        Assert.AreEqual(.5f,BattleHudUi.CooldownFraction(12,24));
        Assert.AreEqual(0,BattleHudUi.CooldownFraction(-1,24));
        Assert.AreEqual(0,BattleHudUi.CooldownFraction(12,0));
        Assert.AreEqual(0,BattleHudUi.CooldownFraction(float.NaN,24));
    }
}
