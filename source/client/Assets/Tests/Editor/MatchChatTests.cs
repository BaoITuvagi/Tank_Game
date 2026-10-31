using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Tank;
public class MatchChatTests {
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static byte[] Batch(uint first,int count=1,ulong match=7,int palette=2) {
        var w=new WireWriter().U64(match).U8(count);
        for(uint id=first;id<first+count;id++)w.U32(id).U8((int)(id%8)).U8(palette).Str("Tên trùng <color=red>").Str("Xin chào <b>!</b>");
        return w.Done();
    }
    static void Handle(ClientCore c,byte op,byte[] payload,uint sequence=0) {typeof(ClientCore).GetMethod("Handle",Private).Invoke(c,new object[]{new Packet{Op=op,Payload=payload,Sequence=sequence}});}
    [Test] public void UnicodeLimitsNormalizationAndLiteralMarkup() {
        string text;
        Assert.IsTrue(MatchChatState.TryNormalize(" \tXin chào\r\n\tbạn \n",out text));Assert.AreEqual("Xin chào   bạn",text);
        Assert.IsTrue(MatchChatState.TryNormalize(string.Concat(Enumerable.Repeat("😀",200)),out text));
        foreach(string raw in new[]{"", " \r\n\t",new string('x',201),string.Concat(Enumerable.Repeat("😀",201)),"hi\0there","hi\u0085there","\ud800"})Assert.IsFalse(MatchChatState.TryNormalize(raw,out text),raw);
        Assert.IsTrue(MatchChatState.TryNormalize("<b>literal</b>",out text));Assert.AreEqual("<b>literal</b>",text);
    }
    [Test] public void ReplayIsBoundedDeduplicatedAndRejectsOtherMatchesOrMalformedBatchesAtomically() {
        var state=new MatchChatState();state.Reset(7);
        for(uint first=1;first<=101;first+=10)state.Receive(new WireReader(Batch(first,10)),7,true);
        Assert.AreEqual(100,state.Messages.Count);Assert.AreEqual(11,state.Messages[0].Id);Assert.AreEqual(110,state.LastId);
        state.Receive(new WireReader(Batch(101,10)),7,true);Assert.AreEqual(100,state.Messages.Count);
        state.Receive(new WireReader(Batch(111,1,8)),7,true);state.Receive(new WireReader(Batch(111)),7,false);Assert.AreEqual(110,state.LastId);
        var invalid=Batch(111,2);invalid[invalid.Length-1]=0;
        Assert.Throws<InvalidDataException>(()=>state.Receive(new WireReader(invalid),7,true));Assert.AreEqual(110,state.LastId);
        Assert.Throws<InvalidDataException>(()=>state.Receive(new WireReader(Batch(111,1,7,8)),7,true));
        state.Reset(8);Assert.IsEmpty(state.Messages);Assert.AreEqual(0,state.LastId);
    }
    [Test] public void GoldenChatFramesRoundTripAndPreserveServerChosenColor() {
        int count=0;
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/golden.tsv"))) {
            string hex=line.Split('\t')[1];var bytes=Enumerable.Range(0,hex.Length/2).Select(i=>Convert.ToByte(hex.Substring(i*2,2),16)).ToArray();
            var packet=Protocol.Decode(bytes);CollectionAssert.AreEqual(bytes,Protocol.Frame(packet.Op,packet.Sequence,packet.Payload));
            if(packet.Op==Op.ChatSend){var r=new WireReader(packet.Payload);Assert.AreEqual(1,r.U64());Assert.AreEqual("Xin chào <b>!</b>",r.Str(800));r.End();count++;}
            if(packet.Op==Op.ChatMessages){var state=new MatchChatState();state.Reset(1);state.Receive(new WireReader(packet.Payload),1,true);Assert.AreEqual(7,state.Messages[0].Slot);Assert.AreEqual(2,state.Messages[0].Palette);Assert.AreEqual("Xe tăng Việt Nam",state.Messages[0].Name);count++;}
        }
        Assert.AreEqual(2,count);
    }
    [Test] public void ErrorsStayInChatAndLifecycleClearsHistoryAndPendingRequest() {
        using(var c=new ClientCore(null,new byte[32])) {
            c.Match=7;c.Chat.Reset(7);c.Phase=ClientPhase.PLAYING;c.Message="gameplay notice";
            Handle(c,Op.ChatMessages,Batch(1));Assert.AreEqual(1,c.Chat.Messages.Count);
            var requests=(Dictionary<uint,byte>)typeof(ClientCore).GetField("requests",Private).GetValue(c);
            requests[20]=Op.ChatSend;typeof(ClientCore).GetField("chatRequest",Private).SetValue(c,20u);
            Handle(c,Op.ActionResult,new WireWriter().U16((int)ErrorCode.RATE_LIMITED).Str("RATE_LIMITED").Done(),20);
            Assert.IsFalse(c.ChatPending);StringAssert.Contains("quá nhanh",c.ChatError);Assert.AreEqual("gameplay notice",c.Message);
            c.RoomId=1;c.Slot=0;Handle(c,Op.Removed,new WireWriter().U32(1).U8(0).U8(0).Done());Assert.IsEmpty(c.Chat.Messages);Assert.IsEmpty(c.ChatError);
            c.Chat.Reset(7);c.Chat.Receive(new WireReader(Batch(1)),7,true);c.Dispose();Assert.IsEmpty(c.Chat.Messages);Assert.AreEqual(0,c.Chat.Match);
        }
    }
    [Test] public void LowWindowsStartCollapsedAndUnreadSurvivesUntilViewed() {
        using(var c=new ClientCore(null,new byte[32])) {
            c.Phase=ClientPhase.PLAYING;c.Chat.Reset(7);var hud=new MatchChatHud();hud.Sync(c,600);Assert.IsFalse(hud.Expanded);
            c.Chat.Receive(new WireReader(Batch(1)),7,true);Assert.AreEqual(1,hud.Unread(c.Chat));Assert.IsFalse(hud.Typing);
            c.Chat.Reset(8);hud.Sync(c,800);Assert.IsTrue(hud.Expanded);Assert.IsFalse(hud.InputBlocked);
        }
    }
}
