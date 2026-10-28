using System;
using System.Reflection;
using NUnit.Framework;
using Tank;

public class AdminNoticesTests {
    [Test] public void AdminPacketsStayOutOfGameStatusAndDoNotChangeGamePhase() {
        var queue=new AdminNoticeQueue();
        Action<AdminNotice> receive=queue.Enqueue;
        AdminNoticeChannel.Received+=receive;
        try {
            using(var client=new ClientCore(null,new byte[32])) {
                var handle=typeof(ClientCore).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic);
                foreach(var phase in new[]{ClientPhase.AUTHENTICATING,ClientPhase.LOBBY,ClientPhase.PLAYING,ClientPhase.RESULTS}) {
                    client.Phase=phase;client.Message="Existing game status";
                    foreach(int code in new[]{200,201,202}) {
                        string text="Thông báo mạng <b>nguyên văn</b> "+code;
                        handle.Invoke(client,new object[]{new Packet{Op=Op.Notice,Payload=new WireWriter().U16(code).Str(text).Done()}});
                        Assert.AreEqual(phase,client.Phase);
                        Assert.AreEqual("Existing game status",client.Message);
                        Assert.AreEqual(text,client.AdminNotice);
                    }
                }
                Assert.AreEqual(11,queue.PendingCount);
                handle.Invoke(client,new object[]{new Packet{Op=Op.Notice,Payload=new WireWriter().U16(100).Str("AFK").Done()}});
                Assert.IsTrue(client.AfkWarning);Assert.AreEqual("AFK",client.Message);
                Assert.AreEqual(11,queue.PendingCount);
            }
        } finally {AdminNoticeChannel.Received-=receive;}
    }
    [Test] public void BurstsRetainVisibleNoticeAndBoundPendingMessages() {
        var queue=new AdminNoticeQueue();
        for(int i=0;i<40;i++)queue.Enqueue(new AdminNotice(200,"message "+i));
        Assert.AreEqual("message 0",queue.Current.Text);
        Assert.AreEqual(31,queue.PendingCount);Assert.AreEqual(8,queue.DroppedCount);
        queue.Dismiss();Assert.AreEqual("message 9",queue.Current.Text);
        for(int i=10;i<40;i++){queue.Dismiss();Assert.AreEqual("message "+i,queue.Current.Text);}
        queue.Dismiss();Assert.IsNull(queue.Current);Assert.AreEqual(0,queue.DroppedCount);
    }
}
