using NUnit.Framework;
using Tank;
using Tank.Campaign;

public class ModeNavigationTests {
    [Test] public void LoginStartsAtChooserAndRoomReturnKeepsMultiplayerLobby() {
        var navigation=new ModeNavigation();
        navigation.Observe(1,ClientPhase.LOBBY);
        Assert.IsFalse(navigation.MultiplayerSelected);
        navigation.ShowMultiplayer();
        navigation.Observe(1,ClientPhase.ROOM);
        navigation.Observe(1,ClientPhase.LOBBY);
        Assert.IsTrue(navigation.MultiplayerSelected,"Leaving or being kicked from a room returns to its lobby.");
        navigation.Observe(1,ClientPhase.RESULTS);
        navigation.Observe(1,ClientPhase.LOBBY);
        Assert.IsTrue(navigation.MultiplayerSelected);
        navigation.ShowSelection();
        navigation.Observe(1,ClientPhase.LOBBY);
        Assert.IsFalse(navigation.MultiplayerSelected);
    }
    [Test] public void AccountChangeClearsLocalModeSelection() {
        var navigation=new ModeNavigation();
        navigation.Observe(1,ClientPhase.LOBBY);
        navigation.ShowMultiplayer();
        navigation.Observe(2,ClientPhase.AUTHENTICATING);
        navigation.Observe(3,ClientPhase.LOBBY);
        Assert.IsFalse(navigation.MultiplayerSelected);
    }
    [Test] public void DirectRoomEntrySelectsMultiplayerAndDoesNotResetOnRepeatedPolls() {
        var navigation=new ModeNavigation();
        navigation.Observe(1,ClientPhase.PLAYING);
        navigation.Observe(1,ClientPhase.LOBBY);
        Assert.IsTrue(navigation.MultiplayerSelected);
    }
    [Test] public void PendingRequestsAndUnsavedResultsHoldNavigationWhileUnfinishedRunRemainsResumable() {
        var run=new RunState();
        Assert.IsTrue(ModeNavigation.CanLeaveCampaign(false,run));
        Assert.IsFalse(ModeNavigation.CanLeaveCampaign(true,run));
        run.Finished=true;
        foreach(string status in new[]{"","Đang lưu kết quả…","Chưa lưu được. Bấm Lưu lại để thử lại."}) {
            run.SaveStatus=status;
            Assert.IsFalse(ModeNavigation.CanLeaveCampaign(false,run),status);
        }
        run.SaveStatus="Đã lưu kết quả.";
        Assert.IsTrue(ModeNavigation.CanLeaveCampaign(false,run));
        Assert.IsFalse(ModeNavigation.CanLeaveCampaign(true,run));
        Assert.IsTrue(ModeNavigation.CanLeaveCampaign(false,null));
    }
    [Test] public void MultiplayerRoomEntryReflectsServerCampaignConstraintWhileBrowsingIsAllowed() {
        var run=new RunState();
        Assert.IsTrue(ModeNavigation.CanLeaveCampaign(false,run),"An unfinished run may be left resumable while browsing rooms.");
        Assert.IsFalse(ModeNavigation.CanEnterMultiplayer(false,run),"The server requires ending the campaign before creating/joining a room.");
        run.Finished=true;run.SaveStatus="Đang lưu kết quả…";
        Assert.IsFalse(ModeNavigation.CanEnterMultiplayer(false,run));
        run.SaveStatus="Đã lưu kết quả.";
        Assert.IsTrue(ModeNavigation.CanEnterMultiplayer(false,run));
        Assert.IsFalse(ModeNavigation.CanEnterMultiplayer(true,run));
        Assert.IsTrue(ModeNavigation.CanEnterMultiplayer(false,null));
        Assert.IsFalse(ModeNavigation.CanEnterMultiplayer(true,null));
    }
}
