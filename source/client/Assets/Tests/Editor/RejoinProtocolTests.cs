using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;
using Tank;

public class RejoinProtocolTests {
 [UnityTest] public IEnumerator UdpHandshakeTimeoutReturnsToLobbyWithoutLoggingOutAndCanRetry(){
  var map=new GameMap{mapId=1,walls=new MapRect[0],bricks=new MapRect[0],water=new MapRect[0]};byte[] hash=new byte[32];
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using(var udp=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))using(var c=new ClientCore(map,hash))try{
   int udpPort=((IPEndPoint)udp.Client.LocalEndPoint).Port;
   var accept=listener.AcceptTcpClientAsync();c.Connect("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,udpPort);yield return WaitFor(c,()=>c.Phase==ClientPhase.AUTHENTICATING);
   using(var peer=accept.Result){peer.ReceiveTimeout=2000;var stream=peer.GetStream();
    Write(stream,Op.LoginResult,new WireWriter().U16(0).U64(3).Str("Test3").Bytes(new byte[16]).Done());yield return WaitFor(c,()=>c.Phase==ClientPhase.LOBBY);
    byte[] prepare=new WireWriter().U64(123).U32(8).U8(2).U16(1).Bytes(hash).U8(3).U8(30).U16(udpPort).U8(1).Done();
    Write(stream,Op.Prepare,prepare);yield return WaitFor(c,()=>c.Phase==ClientPhase.PREPARING);
    typeof(ClientCore).GetField("prepareAt",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(c,ClientCore.NowUs-11e6);
    c.Poll();Assert.AreEqual(3,c.Account,"A UDP timeout must preserve the TCP login");
    ReadRequest(stream,Op.Leave);
    Write(stream,Op.Removed,new WireWriter().U32(8).U8(2).U8(0).Done());yield return WaitFor(c,()=>c.Phase==ClientPhase.LOBBY);
    Assert.AreEqual(3,c.Account);Assert.AreEqual("Test3",c.Nickname);StringAssert.Contains("UDP",c.Message);
    c.Join(8);ReadRequest(stream,Op.Join);Write(stream,Op.Prepare,prepare);yield return WaitFor(c,()=>c.Phase==ClientPhase.PREPARING);
    Write(stream,Op.Start,new WireWriter().U64(123).U64(1).Done());yield return WaitFor(c,()=>c.Phase==ClientPhase.PLAYING);
    Assert.AreEqual(3,c.Account);Assert.AreEqual(0,c.Invalid);Assert.AreEqual(123,c.Match);
   }
  }finally{listener.Stop();}
 }
 [UnityTest] public IEnumerator UdpSocketFailureDoesNotCloseHealthyTcpLogin(){
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using(var c=new ClientCore(null,new byte[32]))try{
   var accept=listener.AcceptTcpClientAsync();c.Connect("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,8889);yield return WaitFor(c,()=>c.Phase==ClientPhase.AUTHENTICATING);
   using(var peer=accept.Result){peer.ReceiveTimeout=2000;var stream=peer.GetStream();
    Write(stream,Op.LoginResult,new WireWriter().U16(0).U64(3).Str("Test3").Bytes(new byte[16]).Done());yield return WaitFor(c,()=>c.Phase==ClientPhase.LOBBY);
    var flags=BindingFlags.NonPublic|BindingFlags.Instance;
    // Disable only UDP sends while leaving the TCP connection healthy.
    var send=typeof(ClientCore).GetMethod("SendUdp",flags);
    var socket=(UdpClient)typeof(ClientCore).GetField("udp",flags).GetValue(c);
    var endpoint=(IPEndPoint)typeof(ClientCore).GetField("remote",flags).GetValue(c);
    socket.Client.Connect(endpoint);socket.Client.Shutdown(SocketShutdown.Send);
    Assert.Throws<SocketException>(()=>socket.Client.SendTo(new byte[1],endpoint));
    send.Invoke(c,new object[]{Op.Ping,new byte[40]});c.Poll();
    Assert.AreEqual(ClientPhase.LOBBY,c.Phase);Assert.AreEqual(3,c.Account);
    c.GetHistory();ReadRequest(stream,Op.History);
   }
  }finally{listener.Stop();}
 }
 [UnityTest] public IEnumerator RoomListFlagsAndRejoinPrepareAreReadAndStartUsesExistingMatch(){
  var map=new GameMap{mapId=1,walls=new MapRect[0],bricks=new MapRect[0],water=new MapRect[0]};byte[] hash=new byte[32];
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using(var udp=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))using(var c=new ClientCore(map,hash))try{
   int udpPort=((IPEndPoint)udp.Client.LocalEndPoint).Port;
   var accept=listener.AcceptTcpClientAsync();c.Connect("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,udpPort);yield return WaitFor(c,()=>c.Phase==ClientPhase.AUTHENTICATING);
   using(var peer=accept.Result){var stream=peer.GetStream();
    Write(stream,Op.LoginResult,new WireWriter().U16(0).U64(3).Str("Test3").Bytes(new byte[16]).Done());yield return WaitFor(c,()=>c.Phase==ClientPhase.LOBBY);
    Write(stream,Op.RoomListResult,new WireWriter().U16(0).U16(1).U32(8).Str("AFK room").U8(2).U8(3).U8(2).U8(1).U8(1).Done());yield return WaitFor(c,()=>c.Rooms.Count==1);Assert.IsTrue(c.Rooms[0].Locked);Assert.IsTrue(c.Rooms[0].CanRejoin);
    Write(stream,Op.Prepare,new WireWriter().U64(123).U32(8).U8(2).U16(1).Bytes(hash).U8(3).U8(30).U16(udpPort).U8(1).Done());yield return WaitFor(c,()=>c.Phase==ClientPhase.PREPARING);Assert.AreEqual(123,c.Match);Assert.AreEqual(2,c.Slot);Assert.IsFalse(c.AfkWarning);
    // The server's original start time is intentionally far in the past.
    Write(stream,Op.Start,new WireWriter().U64(123).U64(1).Done());yield return WaitFor(c,()=>c.Phase==ClientPhase.PLAYING);Assert.AreEqual(123,c.Match);Assert.AreEqual(0,c.Invalid);
   }
  }finally{listener.Stop();}
 }
 static IEnumerator WaitFor(ClientCore c,Func<bool> condition){double deadline=ClientCore.NowUs+5e6;while(!condition()){c.Poll();Assert.Less(ClientCore.NowUs,deadline,c.Message);yield return null;}}
 static Packet ReadRequest(NetworkStream stream,byte expected){while(true){var packet=Protocol.Read(stream);if(packet.Op==Op.Shop){var frame=Protocol.Frame(Op.ShopResult,packet.Sequence,new WireWriter().U16(0).U8(2).U64(0).U64(0).U8(0).U8(0).U32(0).Bytes(new byte[6]).Done());stream.Write(frame,0,frame.Length);continue;}if(packet.Op==Op.Heartbeat||packet.Op==Op.RoomList&&expected!=Op.RoomList)continue;Assert.AreEqual(expected,packet.Op);return packet;}}
 static void Write(NetworkStream stream,byte op,byte[] payload){byte[] frame=Protocol.Frame(op,0,payload);stream.Write(frame,0,frame.Length);stream.Flush();}
}
