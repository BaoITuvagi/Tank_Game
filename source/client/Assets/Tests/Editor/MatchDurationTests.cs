using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEngine.TestTools;
using Tank;

public class MatchDurationTests {
    [TestCase(1800u,60L)]
    [TestCase(5400u,180L)]
    [TestCase(9000u,300L)]
    [TestCase(18000u,600L)]
    [TestCase(345u,12L)]
    [TestCase(0u,0L)]
    public void SimulationDurationWinsOverUtcElapsedTime(uint ticks,long expected) {
        var detail=new MatchDetail{Started=1000,Ended=60976,DurationTicks=ticks};
        Assert.AreEqual(expected,detail.DurationSeconds);
    }
    [TestCase(59976L,60L)]
    [TestCase(179967L,180L)]
    [TestCase(12500L,13L)]
    [TestCase(10000L,10L)]
    public void LegacyDurationRoundsToNearestSecond(long elapsed,long expected) {
        Assert.AreEqual(expected,new MatchDetail{Started=1000,Ended=(ulong)(1000+elapsed)}.DurationSeconds);
    }
    [Test] public void InvalidTimestampOrderCannotUnderflow() {
        Assert.AreEqual(0,new MatchDetail{Started=2000,Ended=1000}.DurationSeconds);
    }
    [UnityTest] public IEnumerator DetailReadsBothOptionalTicksAndLegacyPayload() {
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        using(var client=new ClientCore(null,new byte[32]))try {
            var accept=listener.AcceptTcpClientAsync();
            client.Connect("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,8889);
            yield return Wait(client,()=>client.Phase==ClientPhase.AUTHENTICATING);
            using(var peer=accept.Result) {
                peer.ReceiveTimeout=3000;var stream=peer.GetStream();
                foreach(bool extended in new[]{true,false}) {
                    client.GetHistoryDetail(7);Packet request;
                    do{request=Protocol.Read(stream);}while(request.Op==Op.Heartbeat);
                    Assert.AreEqual(Op.HistoryDetail,request.Op);
                    var payload=new WireWriter().U16(0).U64(7).U16(1).U64(1000).U64(60976).U8(0).U8(1)
                        .U8(0).Str("Player").U16(2).U16(1).U8(1).U8(0).U8(1);
                    if(extended)payload.U32(1800);
                    var frame=Protocol.Frame(Op.HistoryDetailResult,request.Sequence,payload.Done());stream.Write(frame,0,frame.Length);
                    yield return Wait(client,()=>client.HistoryDetail!=null);
                    Assert.AreEqual(60,client.HistoryDetail.DurationSeconds);
                    Assert.AreEqual(extended,client.HistoryDetail.DurationTicks.HasValue);
                }
                Assert.AreEqual(0,client.Invalid);
            }
        }finally{listener.Stop();}
    }
    static IEnumerator Wait(ClientCore client,Func<bool> condition) {
        double deadline=ClientCore.NowUs+5e6;
        while(!condition()){client.Poll();Assert.Less(ClientCore.NowUs,deadline,client.Message);yield return null;}
    }
}
