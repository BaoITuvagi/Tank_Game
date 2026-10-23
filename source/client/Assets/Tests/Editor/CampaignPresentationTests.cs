using System.IO;
using NUnit.Framework;
using Tank;
using Tank.Campaign;
using UnityEngine;

public class CampaignPresentationTests {
    static RunState State(int turn,int y=10){return new RunState{Id="run",Turn=turn,X=6,Y=y,Bottom=y-7,Hp=100,Terrain=new byte[247]};}
    [Test] public void PlayerEnemiesAndCameraInterpolateWithoutMutatingAuthoritativeState() {
        var p=new CampaignPresentation();var a=State(0);a.Enemies.Add(new Actor{Id=1,X=8,Y=13,Hp=60,MaxHp=60});p.Observe(a,0);
        var b=State(1,11);b.Enemies.Add(new Actor{Id=1,X=7,Y=13,Hp=60,MaxHp=60});p.Observe(b,1);
        Assert.AreEqual(10,p.Player(1).y);Assert.AreEqual(10.5f,p.Player(1.07).y,.001);
        Assert.AreEqual(6.5f,p.CameraBottom(13,1.07),.001);Assert.AreEqual(8,p.Enemy(b.Enemies[0],1.07).x);
        Assert.AreEqual(7.5f,p.Enemy(b.Enemies[0],1.16).x,.001);Assert.AreEqual(11,p.Player(1.3).y);Assert.AreEqual(7,p.Enemy(b.Enemies[0],1.3).x);
        Assert.IsTrue(p.Busy(1.1));Assert.IsFalse(p.Busy(1.3));Assert.AreEqual(11,b.Y);Assert.AreEqual(7,b.Enemies[0].X);
    }
    [Test] public void MeleeLungeReturnsToCellAndDuplicateSaveResponsesDoNotReplayEffects() {
        var p=new CampaignPresentation();p.Observe(State(0),0);var b=State(1);
        b.Visuals.Add(new CampaignVisual{Kind=VisualKind.Melee,Source=0,Target=1,X=6,Y=10,TX=6,TY=11});
        b.Visuals.Add(new CampaignVisual{Kind=VisualKind.Hit,Source=0,Target=1,X=6,Y=10,TX=6,TY=11,Amount=20});p.Observe(b,1);
        Assert.AreEqual(10.25f,p.Player(1.07).y,.001);Assert.AreEqual(10,p.Player(1.3).y);
        Assert.AreEqual(2,p.Effects.Count);p.Observe(b,1.15);Assert.AreEqual(1,p.Started);Assert.AreEqual(2,p.Effects.Count);
        p.Observe(b,2);Assert.AreEqual(0,p.Effects.Count);Assert.AreEqual(10,b.Y);
    }
    [Test] public void ShieldDoesNotFlashDamageAndLeavingViewportDoesNotCreateFalseDeath() {
        var p=new CampaignPresentation();var a=State(0);a.Enemies.Add(new Actor{Id=99,X=0,Y=3,Hp=40});p.Observe(a,0);
        var b=State(1);b.Visuals.Add(new CampaignVisual{Kind=VisualKind.Block,Source=2,Target=0});p.Observe(b,1);
        Assert.AreEqual(Color.white,p.Tint(0,1.2));Assert.AreEqual(Vector2.zero,p.Shake(1.2));Assert.AreEqual(0,p.Actors().Count);
        b=State(2);b.Visuals.Add(new CampaignVisual{Kind=VisualKind.Hit,Source=2,Target=0,Amount=9});p.Observe(b,2);
        Assert.AreNotEqual(Color.white,p.Tint(0,2.2));Assert.AreNotEqual(Vector2.zero,p.Shake(2.2));
    }
    [Test] public void NewRunsResetAnimationAndDeathWaitsForImpactBeforeResults() {
        var p=new CampaignPresentation();p.Observe(State(0),0);var b=State(1);b.Finished=true;b.Hp=0;p.Observe(b,1);
        Assert.IsFalse(p.ResultsReady(1.2));Assert.IsTrue(p.ResultsReady(1.7));var c=State(0,400);c.Id="other";p.Observe(c,2);
        Assert.IsFalse(p.Busy(2));Assert.AreEqual(400,p.Player(2).y);Assert.AreEqual(0,p.Effects.Count);p.Observe(null,3);Assert.IsNull(p.Current);
    }
    [Test] public void JavaVisualExtensionPreservesDamageEvenWhenNetHpIncreases() {
        var packet=Protocol.Decode(File.ReadAllBytes(Path.GetFullPath("../../source/server/target/interop/campaign-motion.bin")),false);
        var campaign=new CampaignClient();campaign.Read(new WireReader(packet.Payload));var s=campaign.Run;
        Assert.AreEqual(66,s.Hp);Assert.AreEqual(1,s.Turn);Assert.IsTrue(s.Visuals.Exists(v=>v.Kind==VisualKind.Hit&&v.Target==0&&v.Amount==9));
        Assert.IsTrue(s.Visuals.Exists(v=>v.Kind==VisualKind.Heal&&v.Amount==25));Assert.IsTrue(s.Visuals.Exists(v=>v.Kind==VisualKind.Death&&v.Target==1));
    }
}
