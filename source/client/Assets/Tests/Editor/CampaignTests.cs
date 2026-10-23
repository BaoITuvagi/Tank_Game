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
    [Test] public void JavaTacticsPacketCarriesRolesSquadsAndGuards() {
        var packet=Protocol.Decode(File.ReadAllBytes(Path.GetFullPath("../../source/server/target/interop/campaign-tactics.bin")),false);
        var campaign=new CampaignClient();campaign.Read(new WireReader(packet.Payload));
        Assert.AreEqual(3,campaign.Run.Enemies.Count);
        Assert.AreEqual(EnemyRole.Ranged,campaign.Run.Enemies[0].Role);
        Assert.AreEqual(EnemyRole.Support,campaign.Run.Enemies[1].Role);
        Assert.AreEqual(EnemyRole.Melee,campaign.Run.Enemies[2].Role);
        Assert.AreEqual(7,campaign.Run.Enemies[0].Squad);Assert.AreEqual(10,campaign.Run.Enemies[0].Guard);
        Assert.AreEqual(0,campaign.Run.Enemies[1].Guard);Assert.AreEqual(10,campaign.Run.Enemies[2].Guard);
    }
    [Test] public void InvalidTacticsRoleAndDuplicateIdsAreRejected() {
        var packet=Protocol.Decode(File.ReadAllBytes(Path.GetFullPath("../../source/server/target/interop/campaign-tactics.bin")),false);
        var invalid=(byte[])packet.Payload.Clone();invalid[invalid.Length-30+4]=99;
        Assert.Throws<InvalidDataException>(()=>new CampaignClient().Read(new WireReader(invalid)));
        invalid=(byte[])packet.Payload.Clone();System.Array.Copy(invalid,invalid.Length-30,invalid,invalid.Length-20,4);
        Assert.Throws<InvalidDataException>(()=>new CampaignClient().Read(new WireReader(invalid)));
    }

    [Test] public void JavaShopPacketCarriesPricesRangeAndPurchaseRevision() {
        var packet=Protocol.Decode(File.ReadAllBytes(Path.GetFullPath("../../source/server/target/interop/campaign-shop.bin")),false);
        var client=new CampaignClient();client.Read(new WireReader(packet.Payload));var run=client.Run;
        Assert.IsTrue(run.ShopAvailable);Assert.AreEqual(4,run.ShotRange);Assert.AreEqual(9,run.MaxBuff);Assert.AreEqual(1,run.ShopRevision);
        Assert.AreEqual(85,run.Hp);Assert.AreEqual(150,run.Gold);CollectionAssert.AreEqual(new[]{30,46,55},run.Prices);
    }
    [Test] public void ShopDisablesUnaffordableAndUnusableItems() {
        var run=new RunState{ShopAvailable=true,Gold=100,Hp=75,Shields=6,DoubleHits=6,Prices=new[]{20,30,35}};
        for(int i=0;i<3;i++)Assert.IsTrue(run.CanBuy(i));
        run.Hp=100;run.Shields=7;run.DoubleHits=9;for(int i=0;i<3;i++)Assert.IsFalse(run.CanBuy(i));
        run.Hp=25;run.Gold=19;Assert.IsFalse(run.CanBuy(0));run.Gold=100;run.Finished=true;Assert.IsFalse(run.CanBuy(0));
        run.Finished=false;run.ShopAvailable=false;Assert.IsFalse(run.CanBuy(0));Assert.IsFalse(run.CanBuy(-1));Assert.IsFalse(run.CanBuy(3));
    }
    [Test] public void MalformedShopMetadataCannotReplaceTheCurrentState() {
        var packet=Protocol.Decode(File.ReadAllBytes(Path.GetFullPath("../../source/server/target/interop/campaign-shop.bin")),false);
        var client=new CampaignClient();client.Read(new WireReader(packet.Payload));var previous=client.Run;
        var invalid=(byte[])packet.Payload.Clone();invalid[invalid.Length-18]=0;
        Assert.Throws<InvalidDataException>(()=>client.Read(new WireReader(invalid)));Assert.AreSame(previous,client.Run);
    }

    [Test] public void JavaProgressionPacketCarriesStageBossPhaseAndDelayedDanger(){
        var packet=Protocol.Decode(File.ReadAllBytes(Path.GetFullPath("../../source/server/target/interop/campaign-progression.bin")),false);
        var client=new CampaignClient();client.Read(new WireReader(packet.Payload));var run=client.Run;
        Assert.AreEqual(3,run.Stage);Assert.IsNotEmpty(run.StageName);Assert.AreEqual(2,run.Enemies[0].BossPhase);
        Assert.AreEqual(3,run.Dangers.Count);foreach(var zone in run.Dangers){Assert.AreEqual(run.Turn+1,zone.Due);Assert.Greater(zone.Damage,0);}
        Assert.IsTrue(run.ShopAvailable);Assert.AreEqual(4,run.ShotRange);
    }
    [Test] public void InvalidHazardCoordinatesAndTimersAreRejected(){
        var packet=Protocol.Decode(File.ReadAllBytes(Path.GetFullPath("../../source/server/target/interop/campaign-progression.bin")),false);
        var invalid=(byte[])packet.Payload.Clone();invalid[invalid.Length-15+4]=13;
        Assert.Throws<InvalidDataException>(()=>new CampaignClient().Read(new WireReader(invalid)));
        invalid=(byte[])packet.Payload.Clone();for(int i=invalid.Length-6;i<invalid.Length-2;i++)invalid[i]=0;
        Assert.Throws<InvalidDataException>(()=>new CampaignClient().Read(new WireReader(invalid)));
    }

}
