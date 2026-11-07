using NUnit.Framework;
using Tank;
using Tank.Campaign;
using UnityEngine;

public class CampaignHudPresentationTests {
    static RunState Region(string id,int turn,string name,int template=6){return new RunState{Id=id,Turn=turn,RegionName=name,Template=template,Biome=2};}
    [Test] public void AreaEntryAppearsOnceThenFadesWithoutRestartingOnTurnsOrShopUpdates(){
        var presentation=new CampaignPresentation();
        presentation.Observe(Region("trip",0,"Ngã tư tiền đồn"),10);
        Assert.AreEqual("Ngã tư tiền đồn",presentation.RegionTitle);
        Assert.AreEqual(0,presentation.RegionOpacity(10));
        Assert.AreEqual(1,presentation.RegionOpacity(10.2),.0001);
        presentation.Observe(Region("trip",1,"Ngã tư tiền đồn"),11);
        Assert.IsTrue(presentation.Busy(11.1));
        presentation.Observe(Region("trip",1,"Ngã tư tiền đồn"),12.5); // purchase/state refresh, same turn
        float faded=presentation.RegionOpacity(12.5);
        Assert.Greater(faded,0);Assert.Less(faded,1);
        Assert.AreEqual(0,presentation.RegionOpacity(12.81));
        Assert.IsFalse(presentation.Busy(12.81),"The banner must not extend the action animation or block input.");
        presentation.Observe(Region("trip",7,"Ngã tư tiền đồn"),20); // resync after a gap
        Assert.AreEqual(0,presentation.RegionOpacity(20.5),"A turn gap in the same area is not a new entry.");
    }
    [Test] public void EnteringAndReenteringAreasOrStartingAnotherTripShowsTheNewName(){
        var presentation=new CampaignPresentation();
        presentation.Observe(Region("trip",0,"Ngã tư tiền đồn"),0);
        presentation.Observe(Region("trip",1,"Kho tiếp tế",7),4);
        Assert.AreEqual("Kho tiếp tế",presentation.RegionTitle);Assert.AreEqual(1,presentation.RegionOpacity(4.2),.0001);
        presentation.Observe(Region("trip",2,"Ngã tư tiền đồn"),8);
        Assert.AreEqual("Ngã tư tiền đồn",presentation.RegionTitle);Assert.AreEqual(1,presentation.RegionOpacity(8.2),.0001);
        presentation.Observe(Region("next-trip",0,"Ngã tư tiền đồn"),12);
        Assert.AreEqual(1,presentation.RegionOpacity(12.2),.0001);
        presentation.Observe(null,13);
        Assert.IsEmpty(presentation.RegionTitle);Assert.AreEqual(0,presentation.RegionOpacity(13.2));
    }
    [Test] public void LegacyCampaignUsesStageNameButDoesNotExposeAnEmptyBanner(){
        var presentation=new CampaignPresentation();var state=new RunState{Id="legacy",Stage=2,StageName="Đường rừng"};
        presentation.Observe(state,0);Assert.AreEqual("Đường rừng",presentation.RegionTitle);
        state.Stage=3;state.StageName="Đèo đá";presentation.Observe(state,4);
        Assert.AreEqual("Đèo đá",presentation.RegionTitle);Assert.AreEqual(1,presentation.RegionOpacity(4.2),.0001);
        state.StageName="";presentation.Observe(state,5);Assert.AreEqual(0,presentation.RegionOpacity(5.2));
    }
    [TestCase(0,"Không kích: 70 sát thương trong bán kính 2 ô quanh mục tiêu gần nhất.")]
    [TestCase(1,"Pháo đài thép: +3 khiên, xung kích 45 sát thương trong 1 ô.")]
    [TestCase(5,"Pháo xuyên giáp: xuyên đội hình và khiên hỗ trợ, dừng ở vật cản.")]
    public void SkillSuccessFeedbackIsConciseButStaleVisualsDoNotHideErrorsOrBossNotices(int nation,string success){
        var state=new RunState{Nation=nation,Message=success};state.Visuals.Add(new CampaignVisual{Kind=VisualKind.Skill,Source=0});
        Assert.AreEqual("Đã dùng "+CampaignNation.For(nation).SkillName+".",CampaignPresentation.ActionFeedback(state));
        foreach(string message in new[]{"Kỹ năng đang hồi chiêu.","Ngắm theo hướng xe: cần mục tiêu trên đường thẳng trong 8 ô.","Trùm Vu-a-lo tại 200 m!","Đã mua khiên (-30 vàng)."}){
            state.Message=message;Assert.AreEqual(message,CampaignPresentation.ActionFeedback(state));
        }
        state.Message=success;state.Visuals.Clear();Assert.AreEqual(success,CampaignPresentation.ActionFeedback(state));
    }
}
