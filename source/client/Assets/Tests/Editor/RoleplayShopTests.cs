using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using Tank;

public class RoleplayShopTests {
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [Test] public void OpeningNoticeLastsTwoSecondsOncePerAuthoritativeWaveAndResetsBetweenMatches() {
        using(var c=new ClientCore(null,new byte[32])) {
            var notice=new RoleplayShopOpeningNotice();
            c.Match=7;c.Phase=ClientPhase.PLAYING;c.RoomMode=RoomMode.ROLEPLAY;
            c.Latest=new Snapshot{Tick=1199,Remaining=4201,World=new WorldState()};
            c.LastArrivalUs=ClientCore.NowUs-1e6;
            notice.Update(c,10);Assert.IsFalse(notice.Visible(10),"Wait for the server's spawn tick, not extrapolated time.");
            c.Latest.Tick=1200;c.Latest.Remaining=4200;
            notice.Update(c,11);Assert.IsTrue(notice.Visible(11));
            notice.Update(c,12.9);Assert.IsTrue(notice.Visible(12.999));Assert.IsFalse(notice.Visible(13));
            notice.Update(c,14);Assert.IsFalse(notice.Visible(14),"Repeated snapshots must not restart the notice.");
            c.Latest.Tick=1800;c.Latest.Remaining=3600;notice.Update(c,15);Assert.IsFalse(notice.Visible(15));
            c.Latest.Tick=2400;c.Latest.Remaining=3000;notice.Update(c,20);Assert.IsTrue(notice.Visible(20));
            c.Match=8;c.Latest.Tick=0;c.Latest.Remaining=5400;notice.Update(c,20.5);Assert.IsFalse(notice.Visible(20.5));
            c.Latest.Tick=1200;c.Latest.Remaining=4200;notice.Update(c,21);Assert.IsTrue(notice.Visible(21));
            c.Phase=ClientPhase.LOBBY;notice.Update(c,21.1);Assert.IsFalse(notice.Visible(21.1));
            c.Phase=ClientPhase.PLAYING;c.RoomMode=RoomMode.NORMAL;notice.Update(c,22);Assert.IsFalse(notice.Visible(22));
            c.RoomMode=RoomMode.ROLEPLAY;notice.Update(c,23);Assert.IsTrue(notice.Visible(23));
            c.Latest.Tick=1800;c.Latest.Remaining=3600;notice.Update(c,23.1);Assert.IsFalse(notice.Visible(23.1));
        }
    }
    [TestCase(60)] [TestCase(180)] [TestCase(300)] [TestCase(600)]
    public void FortySecondWavesHaveTwentySecondWindowsAtEveryDuration(int seconds) {
        int duration=seconds*30,interval=1200,window=600;
        Assert.AreEqual(0,RoleplayItemSchedule.Wave(duration,0));
        for(int wave=1;wave*interval<duration;wave++) {
            Assert.AreEqual(0,RoleplayItemSchedule.Wave(duration,(uint)(wave*interval-1)));
            Assert.AreEqual(wave,RoleplayItemSchedule.Wave(duration,(uint)(wave*interval)));
            Assert.AreEqual(wave,RoleplayItemSchedule.Wave(duration,(uint)(Math.Min(duration,wave*interval+window)-1)));
            Assert.AreEqual(0,RoleplayItemSchedule.Wave(duration,(uint)(Math.Min(duration,wave*interval+window))));
        }
        Assert.AreEqual(0,RoleplayItemSchedule.Wave(duration,(uint)duration));
    }
    static void Reply(ClientCore c,uint sequence,ulong gold,ulong match,int wave,int mask) {
        typeof(ClientCore).GetField("shopRequest",Private).SetValue(c,sequence);
        ((Dictionary<uint,byte>)typeof(ClientCore).GetField("requests",Private).GetValue(c))[sequence]=Op.Shop;
        typeof(ClientCore).GetMethod("Handle",Private).Invoke(c,new object[]{new Packet{Op=Op.ShopResult,Sequence=sequence,Payload=new WireWriter().U16(0).U8(2).U64(gold).U64(match).U8(wave).U8(mask).U32(3).Bytes(new byte[]{0,2,0,0,0,1}).Done()}});
    }
    [Test] public void WalletAndPurchaseMaskAreAuthoritativeAndResetOnAccountChange() {
        using(var c=new ClientCore(null,new byte[32])) {
            c.Account=1;c.Match=7;c.Phase=ClientPhase.PLAYING;c.RoomMode=RoomMode.ROLEPLAY;c.Slot=0;
            c.Latest=new Snapshot{Tick=1200,Remaining=4200,World=new WorldState()};c.LastArrivalUs=ClientCore.NowUs;
            c.Latest.World.Tanks[0]=new TankState{Hp=100};
            Reply(c,1,150,7,1,1<<ItemState.Mirror);
            Assert.AreEqual(1,c.ItemCount(ItemState.Mirror));Assert.AreEqual(2,c.ItemCount(ItemState.Armor));Assert.IsTrue(c.CanUseItem(ItemState.Mirror));Assert.AreEqual(150,c.WalletGold);Assert.IsTrue(c.WalletLoaded);Assert.IsTrue(c.ItemBought(ItemState.Mirror));Assert.IsFalse(c.CanBuyItem(ItemState.Mirror));Assert.IsTrue(c.CanBuyItem(ItemState.Armor));
            c.Latest.Tick=1800;c.Latest.Remaining=3600;Assert.AreEqual(0,c.ItemWave);Assert.IsFalse(c.CanBuyItem(ItemState.Armor));Assert.IsTrue(c.CanUseItem(ItemState.Mirror));
            c.Latest.Tick=2400;c.Latest.Remaining=3000;c.LastArrivalUs=ClientCore.NowUs;
            Assert.AreEqual(2,c.ItemWave);Assert.IsFalse(c.ItemBought(ItemState.Mirror));Assert.IsTrue(c.CanBuyItem(ItemState.Mirror));
            c.Latest.World.Tanks[0].Hp=0;Assert.IsFalse(c.CanBuyItem(ItemState.Armor));Assert.IsFalse(c.CanUseItem(ItemState.Armor));
            typeof(ClientCore).GetMethod("ClearAccountState",Private).Invoke(c,null);Assert.IsFalse(c.WalletLoaded);Assert.AreEqual(0,c.WalletGold);Assert.AreEqual(0,c.ItemCount(ItemState.Mirror));
        }
    }
    [Test] public void JavaShopGoldenPayloadsUseTheSameWireLayout() {
        int count=0;
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/golden.tsv"))) {
            string hex=line.Split('\t')[1];var bytes=new byte[hex.Length/2];for(int i=0;i<bytes.Length;i++)bytes[i]=Convert.ToByte(hex.Substring(i*2,2),16);
            Packet packet=Protocol.Decode(bytes);var r=new WireReader(packet.Payload);
            if(packet.Op==Op.Shop) {int command=r.U8();r.U64();if(command==1){Assert.AreEqual(1,r.U8());Assert.AreEqual(2,r.U8());}else if(command==2){Assert.AreEqual(2,r.U32());Assert.AreEqual(1,r.U8());}r.End();count++;}
            if(packet.Op==Op.ShopResult) {int status=r.U16();if(status!=0)Assert.IsNotEmpty(r.Str());else{Assert.AreEqual(2,r.U8());Assert.AreEqual(150,r.U64());Assert.AreEqual(1,r.U64());Assert.AreEqual(1,r.U8());Assert.AreEqual(3,r.U8());Assert.AreEqual(2,r.U32());CollectionAssert.AreEqual(new byte[]{1,1,0,0,0,0},r.Bytes(6));}r.End();count++;}
        }
        Assert.AreEqual(5,count);
    }
}
