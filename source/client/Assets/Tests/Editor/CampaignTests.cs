using System.IO;
using NUnit.Framework;
using Tank;
using Tank.Campaign;

public class CampaignTests {
    [Test] public void JavaCampaignPacketDecodesWithoutTouchingMultiplayerState() {
        var packet=Protocol.Decode(File.ReadAllBytes(Path.GetFullPath("../../source/server/target/interop/campaign-state.bin")),false);
        Assert.AreEqual(91,packet.Op);Assert.AreEqual(123,packet.Sequence);
        var campaign=new CampaignClient();campaign.Read(new WireReader(packet.Payload));var run=campaign.Run;
        Assert.AreEqual(1,run.Nation);Assert.AreEqual(7,run.X);Assert.AreEqual(12,run.Y);Assert.AreEqual(15,run.Distance);Assert.AreEqual(75,run.Hp);Assert.AreEqual(42,run.Gold);Assert.AreEqual(2,run.Shields);
        Assert.AreEqual(247,run.Terrain.Length);Assert.AreEqual(5,run.Bottom);Assert.AreEqual(1,run.Enemies.Count);Assert.AreEqual(33,run.Enemies[0].Id);Assert.AreEqual(2,run.Enemies[0].Boss);Assert.AreEqual(160,run.Enemies[0].MaxHp);Assert.AreEqual(1,run.Loot.Count);Assert.AreEqual(3,run.Loot[0].Kind);
    }
    [Test] public void CampaignRankingPreservesUnicodeAndClearsPendingErrors() {
        var campaign=new CampaignClient{Pending=true,Error="old"};
        campaign.Read(new WireReader(new WireWriter().U16(0).U8(1).U8(1).U64(1234).U8(2).Str("Chỉ huy Việt").U32(10000).Str("Người chơi").U32(500).Done()));
        Assert.AreEqual(1234,campaign.TotalGold);Assert.AreEqual("Chỉ huy Việt",campaign.Rankings[0].Name);Assert.AreEqual(10000,campaign.Rankings[0].Score);Assert.IsFalse(campaign.Pending);Assert.AreEqual("",campaign.Error);
    }
    [Test] public void CampaignErrorsAndAccountDisposalAreIsolated() {
        using(var client=new ClientCore(null,new byte[32])) {
            client.Campaign.Run=new RunState{Gold=99};client.Campaign.Pending=true;
            client.Campaign.Read(new WireReader(new WireWriter().U16(1).Str("Chưa đăng nhập").Done()));
            Assert.IsFalse(client.Campaign.Pending);Assert.AreEqual("Chưa đăng nhập",client.Campaign.Error);
            client.Dispose();Assert.IsNull(client.Campaign.Run);Assert.IsEmpty(client.Campaign.Rankings);
        }
    }
}
