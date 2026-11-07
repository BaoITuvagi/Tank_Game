using NUnit.Framework;
using Tank.Campaign;
using UnityEngine;

public class CampaignAudioTests {
    static RunState State(int turn,int gold=0,string id="trip"){
        return new RunState{Id=id,Turn=turn,Gold=gold,X=6,Y=17};
    }
    static RunState BeforeCoin(){
        var state=State(4);state.Y=16;state.Loot.Add(new Pickup{Kind=3,X=6,Y=17});return state;
    }
    [Test] public void AuthoritativeCoinPickupPlaysOnceAfterMovementAndSameTurnRefreshDoesNotReplay(){
        var timeline=new CampaignAudioTimeline();timeline.Observe(BeforeCoin(),10,true);
        var collected=State(5,25);timeline.Observe(collected,11,true);
        Assert.IsEmpty(timeline.TakeDue(11.1),"The pickup cue follows the brief player move.");
        var cues=timeline.TakeDue(11.15);Assert.AreEqual(1,cues.Count);Assert.AreEqual("gold",cues[0].Clip);
        timeline.Observe(collected,11.2,true);timeline.Observe(State(5,25),11.3,true);
        Assert.IsEmpty(timeline.TakeDue(11.5));
    }
    [Test] public void ShopSpendingWalletCreditsAndRescueBonusAreNotCoinPickups(){
        var timeline=new CampaignAudioTimeline();var before=State(4,100);timeline.Observe(before,10,true);
        var purchase=State(4,70);purchase.ShopRevision=1;timeline.Observe(purchase,11,true);
        var cues=timeline.TakeDue(11);Assert.AreEqual(1,cues.Count);Assert.AreEqual("purchase",cues[0].Clip);
        var wallet=State(4,70);wallet.ShopRevision=1;wallet.TotalGold=500;timeline.Observe(wallet,12,true);
        Assert.IsEmpty(timeline.TakeDue(12.2));
        var victory=State(5,570);victory.Finished=victory.Victory=true;timeline.Observe(victory,13,true);
        cues=timeline.TakeDue(13.31);Assert.AreEqual(1,cues.Count);Assert.AreEqual("victory",cues[0].Clip);
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void NonGoldPickupsNeverEmitCoinSound(int kind){
        var timeline=new CampaignAudioTimeline();var before=BeforeCoin();before.Loot[0].Kind=kind;
        timeline.Observe(before,0,true);timeline.Observe(State(5,25),1,true);
        Assert.IsEmpty(timeline.TakeDue(1.2));
    }
    [Test] public void UncollectedCoinAndUnchangedBalanceAreSilent(){
        var timeline=new CampaignAudioTimeline();timeline.Observe(BeforeCoin(),0,true);
        var stillPresent=State(5,25);stillPresent.Loot.Add(new Pickup{Kind=3,X=6,Y=17});
        timeline.Observe(stillPresent,1,true);Assert.IsEmpty(timeline.TakeDue(1.2));
        timeline.Clear();timeline.Observe(BeforeCoin(),2,true);timeline.Observe(State(5),3,true);
        Assert.IsEmpty(timeline.TakeDue(3.2));
    }
    [Test] public void MutingResumingTurnGapsAndDifferentRunsDoNotReplayCoinPickups(){
        var timeline=new CampaignAudioTimeline();timeline.Observe(BeforeCoin(),0,true);
        timeline.Observe(State(5,25),1,false);Assert.IsEmpty(timeline.TakeDue(1.2));
        timeline.Observe(State(5,25),2,true);Assert.IsEmpty(timeline.TakeDue(2.2));
        timeline.Clear();timeline.Observe(BeforeCoin(),3,true);timeline.Observe(State(7,25),4,true);
        Assert.IsEmpty(timeline.TakeDue(4.2));
        timeline.Clear();timeline.Observe(BeforeCoin(),5,true);timeline.Observe(State(5,25,"other"),6,true);
        Assert.IsEmpty(timeline.TakeDue(6.2));
    }
    [Test] public void EveryCueResolvesToAnExistingAudioAssetIncludingTheCoinConfirmation(){
        foreach(string name in CampaignAudio.ClipNames){
            var clip=Resources.Load<AudioClip>(CampaignAudio.ClipResource(name));
            Assert.IsNotNull(clip,name);Assert.Greater(clip.samples,0,name);
        }
    }
}
