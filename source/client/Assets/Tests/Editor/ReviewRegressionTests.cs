using System;
using System.IO;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using Tank;

public class ReviewRegressionTests {
 [Test] public void OldSocketPacketAndErrorCannotChangeNewSession(){
  using(var client=new ClientCore(null,new byte[32])){
   var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
   var packetType=typeof(ClientCore).GetNestedType("IncomingPacket",System.Reflection.BindingFlags.NonPublic);
   var errorType=typeof(ClientCore).GetNestedType("ConnectionError",System.Reflection.BindingFlags.NonPublic);
   var inbox=typeof(ClientCore).GetField("inbox",flags).GetValue(client);
   var errors=typeof(ClientCore).GetField("errors",flags).GetValue(client);
   inbox.GetType().GetMethod("Enqueue").Invoke(inbox,new[]{Activator.CreateInstance(packetType,new object[]{-1,new Packet{Op=255,Payload=new byte[0]}})});
   errors.GetType().GetMethod("Enqueue").Invoke(errors,new[]{Activator.CreateInstance(errorType,new object[]{-1,"Old socket error"})});
   client.Poll();Assert.AreEqual(ClientPhase.DISCONNECTED,client.Phase);Assert.AreEqual("",client.Message);Assert.AreEqual(0,client.Invalid);
  }
 }
 sealed class BlockedStream : MemoryStream {
  public readonly TaskCompletionSource<bool> Entered=new TaskCompletionSource<bool>();
  readonly TaskCompletionSource<bool> release=new TaskCompletionSource<bool>();
  public override Task WriteAsync(byte[] bytes,int offset,int count,CancellationToken token){Entered.TrySetResult(true);return release.Task;}
  protected override void Dispose(bool disposing){release.TrySetCanceled();base.Dispose(disposing);}
 }
 [Test] public void TcpWriterIsBoundedAndNeverBlocksCaller(){
  using(var stream=new BlockedStream())using(var writer=new TcpSendQueue(stream,_=>{},2,1000)){
   Assert.IsTrue(writer.TryEnqueue(new byte[]{1}));Assert.IsTrue(stream.Entered.Task.Wait(1000));
   double before=ClientCore.NowUs;
   Assert.IsTrue(writer.TryEnqueue(new byte[]{2}));Assert.IsTrue(writer.TryEnqueue(new byte[]{3}));Assert.IsFalse(writer.TryEnqueue(new byte[]{4}));
   Assert.Less(ClientCore.NowUs-before,100000);Assert.AreEqual(2,writer.Depth);
  }
 }
 [Test] public void TcpWriterTimeoutReportsFailure(){
  var failed=new TaskCompletionSource<string>();
  using(var stream=new BlockedStream())using(var writer=new TcpSendQueue(stream,error=>failed.TrySetResult(error),2,50)){
   Assert.IsTrue(writer.TryEnqueue(new byte[]{1}));Assert.IsTrue(failed.Task.Wait(2000));StringAssert.Contains("quá hạn",failed.Task.Result);
  }
 }
 [Test] public void SmokeRejectsEarlyEndInvalidDataAndMissingDuration(){
  using(var client=new ClientCore(null,new byte[32])){
   client.Ended=true;client.Match=7;client.EndReason=0;client.Decoded=5400;client.Latest=new Snapshot{Tick=5400};client.History.Add(new HistoryEntry{Match=7,Reason=0});
   Assert.IsTrue(SmokeAcceptance.Passed(client,5400));client.EndReason=1;Assert.IsFalse(SmokeAcceptance.Passed(client,5400));
   client.EndReason=0;client.Invalid=1;Assert.IsFalse(SmokeAcceptance.Passed(client,5400));client.Invalid=0;
   client.Latest.Tick=300;Assert.IsFalse(SmokeAcceptance.Passed(client,5400));client.Latest.Tick=5400;client.Decoded=2;Assert.IsFalse(SmokeAcceptance.Passed(client,5400));
  }
 }
 [Test] public void PredictionBodyFitsWallAndRenderingDoesNotInterpolateIntoIt(){
  var wall=new MapRect{x=10,y=0,w=1,h=60};var map=new GameMap{width=60,height=60,walls=new[]{wall},bricks=new MapRect[0],water=new MapRect[0]};
  var tank=new TankState{Hp=3,X=8,Y=10};for(uint tick=1;tick<=90;tick++){Movement.Step(tank,new InputRecord(tick,41,0),map,new byte[64]);Assert.IsFalse(map.Blocked(tank.X,tank.Y,tank.Body,new byte[64]));}
  Assert.Greater(tank.Y,20);Assert.IsTrue(map.Blocked(9,10,4500,new byte[64]));
  var a=new Snapshot{Time=1000,World=new WorldState()};a.World.Tanks[0]=new TankState{Hp=3,X=10-GameRules.TankHalfSize-.01f,Y=10,Body=0};
  var b=new Snapshot{Time=2000,World=a.World.Copy()};b.World.Tanks[0].Body=9000;
  var rendered=Rendering.Interpolate(new[]{a,b},1500,map);var shown=rendered.Tanks[0];Assert.IsFalse(map.Blocked(shown.X,shown.Y,shown.Body,rendered.Bricks));
 }
 [UnityTest] public IEnumerator LogoutClearsAccountAndIgnoresLateHistoryResponse(){
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
  using(var client=new ClientCore(null,new byte[32]))try{
   var accept=listener.AcceptTcpClientAsync();client.Connect("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,8889);yield return Wait(client,()=>client.Phase==ClientPhase.AUTHENTICATING);
   using(var peer=accept.Result){peer.ReceiveTimeout=2000;var stream=peer.GetStream();client.Login("first","password123");var login=ReadRequest(stream,Op.Login);
    Send(stream,Op.LoginResult,login.Sequence,new WireWriter().U16(0).U64(1).Str("First").Bytes(new byte[16]).Str("first").Done());yield return Wait(client,()=>client.Account==1);
    client.History.Add(new HistoryEntry{Match=7});client.Scores.Add(new Score{Name="First"});client.GetHistoryDetail(7);var detail=ReadRequest(stream,Op.HistoryDetail);
    client.GetHistory();var history=ReadRequest(stream,Op.History);client.Logout();var logout=ReadRequest(stream,Op.Logout);
    Send(stream,Op.LogoutResult,logout.Sequence,new WireWriter().U16(0).Done());yield return Wait(client,()=>client.Phase==ClientPhase.AUTHENTICATING);
    Assert.AreEqual(0,client.Account);Assert.AreEqual("",client.Nickname);Assert.IsEmpty(client.History);Assert.IsEmpty(client.Scores);Assert.AreEqual(0,client.SelectedHistoryMatch);Assert.IsFalse(client.HistoryDetailPending);
    Send(stream,Op.HistoryResult,history.Sequence,new WireWriter().U16(0).U8(1).U64(7).U64(1000).U8(0).U16(1).U16(0).U8(1).Done());
    client.Login("second","password123");login=ReadRequest(stream,Op.Login);Send(stream,Op.LoginResult,login.Sequence,new WireWriter().U16(0).U64(2).Str("Second").Bytes(new byte[16]).Str("second").Done());
    yield return Wait(client,()=>client.Account==2);Assert.IsEmpty(client.History);Assert.AreEqual(0,client.Invalid);Assert.AreEqual("Second",client.Nickname);
   }
  }finally{listener.Stop();}
 }
 static IEnumerator Wait(ClientCore client,Func<bool> condition){double deadline=ClientCore.NowUs+5e6;while(!condition()){client.Poll();Assert.Less(ClientCore.NowUs,deadline,client.Message);yield return null;}}
 static Packet ReadRequest(NetworkStream stream,byte expected){while(true){var packet=Protocol.Read(stream);if(packet.Op==Op.Shop){Send(stream,Op.ShopResult,packet.Sequence,new WireWriter().U16(0).U8(2).U64(0).U64(0).U8(0).U8(0).U32(0).Bytes(new byte[6]).Done());continue;}if(packet.Op==Op.Heartbeat||packet.Op==Op.RoomList)continue;Assert.AreEqual(expected,packet.Op);return packet;}}
 static void Send(NetworkStream stream,byte op,uint seq,byte[] bytes){var frame=Protocol.Frame(op,seq,bytes);stream.Write(frame,0,frame.Length);}
}
