using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEngine.TestTools;
using Tank;

public class RejoinProtocolTests {
 [UnityTest] public IEnumerator RoomListFlagsAndRejoinPrepareAreReadAndStartUsesExistingMatch(){
  var map=new GameMap{mapId=1,walls=new MapRect[0],bricks=new MapRect[0],water=new MapRect[0]};byte[] hash=new byte[32];
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using(var c=new ClientCore(map,hash))try{
   var accept=listener.AcceptTcpClientAsync();c.Connect("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,8889);yield return WaitFor(c,()=>c.Phase==ClientPhase.AUTHENTICATING);
   using(var peer=accept.Result){var stream=peer.GetStream();
    Write(stream,Op.LoginResult,new WireWriter().U16(0).U64(3).Str("Test3").Bytes(new byte[16]).Done());yield return WaitFor(c,()=>c.Phase==ClientPhase.LOBBY);
    Write(stream,Op.RoomListResult,new WireWriter().U16(0).U16(1).U32(8).Str("AFK room").U8(2).U8(3).U8(2).U8(1).U8(1).Done());yield return WaitFor(c,()=>c.Rooms.Count==1);Assert.IsTrue(c.Rooms[0].Locked);Assert.IsTrue(c.Rooms[0].CanRejoin);
    Write(stream,Op.Prepare,new WireWriter().U64(123).U32(8).U8(2).U16(1).Bytes(hash).U8(3).U8(30).U16(8889).U8(1).Done());yield return WaitFor(c,()=>c.Phase==ClientPhase.PREPARING);Assert.AreEqual(123,c.Match);Assert.AreEqual(2,c.Slot);Assert.IsFalse(c.AfkWarning);
    // The server's original start time is intentionally far in the past.
    Write(stream,Op.Start,new WireWriter().U64(123).U64(1).Done());yield return WaitFor(c,()=>c.Phase==ClientPhase.PLAYING);Assert.AreEqual(123,c.Match);Assert.AreEqual(0,c.Invalid);
   }
  }finally{listener.Stop();}
 }
 static IEnumerator WaitFor(ClientCore c,Func<bool> condition){double deadline=ClientCore.NowUs+5e6;while(!condition()){c.Poll();Assert.Less(ClientCore.NowUs,deadline,c.Message);yield return null;}}
 static void Write(NetworkStream stream,byte op,byte[] payload){byte[] frame=Protocol.Frame(op,0,payload);stream.Write(frame,0,frame.Length);stream.Flush();}
}
