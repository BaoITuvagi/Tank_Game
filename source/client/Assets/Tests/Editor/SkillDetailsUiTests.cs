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
    [TestCase(false)] [TestCase(true)]
    public void CampaignConsumerRequiresReleaseWhenInspectionClosesBeforeItsNextUpdate(bool escape){
        var host=new GameObject("Campaign inspection release regression");host.SetActive(false);
        using(var client=new ClientCore(null,new byte[32]))try{
            var app=host.AddComponent<TankApp>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var run=new RunState{Id="inspection-release",Turn=42,Gold=185};client.Campaign.Run=run;client.Phase=ClientPhase.LOBBY;
            typeof(TankApp).GetField("client",flags).SetValue(app,client);
            typeof(TankApp).GetField("campaignAccountRevision",flags).SetValue(app,client.AccountRevision);
            typeof(TankApp).GetField("campaignPage",flags).SetValue(app,3);
            typeof(TankApp).GetField("campaignInputRelease",flags).SetValue(app,false);
            typeof(TankApp).GetField("queuedCampaignAction",flags).SetValue(app,4);
            SkillDetailsUi.Sync("campaign:"+run.Id);
            SkillDetailsUi.Open(new Rect(700,400,200,70),"Không kích","Thông tin kỹ năng");
            // Model the shared guard's held-control state, without simulating physical editor input.
            if(escape)SkillDetailsUi.BeforeInput(true,true,true);
            else {SkillDetailsUi.Close();SkillDetailsUi.BeforeInput(true,true,false);}
            Assert.IsFalse(SkillDetailsUi.IsOpen);Assert.IsTrue(SkillDetailsUi.InputBlocked);
            Assert.IsFalse((bool)typeof(TankApp).GetProperty("AnyDialog",flags).GetValue(app),"The popup is already closed, so the consumer must honor its release guard separately.");
            Assert.IsFalse((bool)typeof(TankApp).GetField("campaignInputRelease",flags).GetValue(app),"No campaign update has run since inspection closed.");
            Assert.AreEqual("Nhả phím để tiếp tục",typeof(TankApp).GetMethod("CampaignActionWaiting",flags).Invoke(app,new object[]{run,Time.unscaledTimeAsDouble}),"The HUD must show release pending before campaign Update, regardless of editor focus.");
            typeof(TankApp).GetMethod("UpdateCampaign",flags).Invoke(app,null);
            Assert.AreEqual(-1,typeof(TankApp).GetField("queuedCampaignAction",flags).GetValue(app),"A queued shot must not survive the closed popup's input guard.");
            Assert.IsTrue((bool)typeof(TankApp).GetField("campaignInputRelease",flags).GetValue(app));
            Assert.AreEqual(42,run.Turn);Assert.IsFalse(client.Campaign.Pending);
            SkillDetailsUi.BeforeInput(true,true,false);
            Assert.IsTrue(SkillDetailsUi.InputBlocked,"Closing by either route must keep gameplay blocked while a control remains held.");
            SkillDetailsUi.BeforeInput(true,false,false);
            Assert.IsFalse(SkillDetailsUi.InputBlocked,"The shared guard releases only after all controls are released.");
        }finally{Object.DestroyImmediate(host);}
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
