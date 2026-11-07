using NUnit.Framework;
using System.Reflection;
using Tank;
using Tank.Campaign;
using UnityEngine;

public class CampaignHudLayoutTests {
    [TestCase(960,600)] [TestCase(1280,800)] [TestCase(1600,900)] [TestCase(1920,1080)]
    public void ThirteenByThirteenMapRetainsItsAspectBesidePinnedCombatActions(int width,int height){
        var layout=new CampaignHudLayout(width,height);
        Assert.AreEqual(layout.Map.width,layout.Map.height);
        Assert.AreEqual(13*ArenaLayout.Fit(width,height,13,13).Scale,layout.Map.width);
        Assert.GreaterOrEqual(layout.Map.x,0);Assert.GreaterOrEqual(layout.Map.y,ArenaLayout.Top);
        Assert.LessOrEqual(layout.Map.xMax,layout.Sidebar.x);Assert.LessOrEqual(layout.Map.yMax,height);
        Assert.AreEqual(width-ArenaLayout.SidebarWidth,layout.Sidebar.x);
        Assert.GreaterOrEqual(layout.Details.height,28,"Errors retain a usable notice viewport in a short window.");
        AssertCombatActionsFit(layout,height);
    }
    [Test] public void ActualNationTitlesAndLongCustomBindingsLeaveProgressAboveTheActionsAtMinimumSize(){
        const string customKey="Shift phải / Chuột phải";
        foreach(int nation in new[]{0,1,5})foreach(string state in new[]{"Sẵn sàng","Còn 24 lượt","Đang xử lý…","Nhả phím để tiếp tục"}){
            float fire=BattleHudUi.ActionHeight(270,customKey,"Bắn",state),skill=BattleHudUi.ActionHeight(270,customKey,CampaignNation.For(nation).SkillName,state),supply=BattleHudUi.ActionHeight(270,customKey,"Tiếp tế","Mua và dùng ngay");
            var layout=new CampaignHudLayout(960,600,fire,skill,supply);
            AssertCombatActionsFit(layout,600);
            Assert.GreaterOrEqual(layout.Effects.y-layout.Objective.y-8,58,"Distance, progress and boss/gold stay above the effect counters.");
            Assert.GreaterOrEqual(layout.Details.height,28,"Custom bindings must leave a usable error viewport.");
        }
    }
    [Test] public void CampaignAndMultiplayerKeepCooldownUnitsAndBlockingReasonsExplicit(){
        Assert.AreEqual("Còn 3 lượt",BattleHudUi.State(false,3,"lượt"));
        Assert.AreEqual("Còn 2 giây",BattleHudUi.State(false,1.2f,"giây"));
        Assert.AreEqual("Đang xử lý…",BattleHudUi.State(false,3,"lượt",blockedReason:"Đang xử lý…"));
        Assert.AreEqual("Xác nhận mục tiêu",BattleHudUi.State(true,0,"giây","Xác nhận mục tiêu"));
    }
    static void AssertCombatActionsFit(CampaignHudLayout layout,float height){
        Assert.LessOrEqual(layout.Vitals.yMax,layout.Objective.y);
        Assert.LessOrEqual(layout.Objective.yMax,layout.Fire.y);
        Assert.LessOrEqual(layout.Objective.yMax,layout.Effects.y);
        Assert.AreEqual(44,layout.Effects.height);
        Assert.LessOrEqual(layout.Effects.yMax,layout.Fire.y-6);
        Assert.LessOrEqual(layout.Fire.yMax,layout.Skill.y);
        Assert.LessOrEqual(layout.Skill.yMax,layout.Supply.y);
        Assert.LessOrEqual(layout.Supply.yMax,layout.End.y);
        Assert.AreEqual(52,layout.End.height);Assert.LessOrEqual(layout.End.yMax,height);
        foreach(Rect action in new[]{layout.Fire,layout.Skill,layout.Supply,layout.End}){
            Assert.GreaterOrEqual(action.x,layout.Sidebar.x);Assert.LessOrEqual(action.xMax,layout.Sidebar.xMax);
            Assert.GreaterOrEqual(action.y,ArenaLayout.Top);Assert.GreaterOrEqual(action.height,52);
        }
    }
}
