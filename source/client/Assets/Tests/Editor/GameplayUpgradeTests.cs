using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;
using Tank;

public class GameplayUpgradeTests {
 GameMap Open(){return new GameMap{walls=new MapRect[0],bricks=new MapRect[0],water=new MapRect[0]};}
 [Test] public void ShiftPredictionRunsForFiveSecondsThenRecoversAfterRelease(){
  var map=Open();var t=new TankState{Hp=3,X=10,Y=10};
  for(uint i=1;i<=150;i++)Movement.Step(t,new InputRecord(i,40,0),map,new byte[64]);
  Assert.AreEqual(0,t.Energy);Assert.AreEqual(77.5,t.X,.001);
  float exhausted=t.X;for(uint i=151;i<=180;i++)Movement.Step(t,new InputRecord(i,40,0),map,new byte[64]);
  Assert.AreEqual(0,t.Energy);Assert.AreEqual(exhausted+9,t.X,.001);
  for(uint i=181;i<=195;i++)Movement.Step(t,new InputRecord(i,0,0),map,new byte[64]);Assert.AreEqual(0,t.Energy);
  for(uint i=196;i<=345;i++)Movement.Step(t,new InputRecord(i,0,0),map,new byte[64]);Assert.AreEqual(150,t.Energy);
  Movement.Step(t,new InputRecord(346,40,0),map,new byte[64]);Assert.AreEqual(149,t.Energy);
 }
 [Test] public void SprintRemainsNeutralWhenUnfocusedAndDoesNotDrainWhileDead(){
  var sample=GameplayInput.Resolve(false,63,9000,12345);Assert.AreEqual(0,sample.Buttons);Assert.AreEqual(12345,sample.Aim);
  var t=new TankState{Hp=0,X=10,Y=10,Energy=70};Movement.Step(t,new InputRecord(1,40,0),Open(),new byte[64]);Assert.AreEqual(70,t.Energy);Assert.AreEqual(10,t.X);
 }
 [Test] public void LiveRankingUpdatesKillsAndMatchesResultTieBreakers(){
  var w=new WorldState();w.Tanks[0]=new TankState{Slot=0,Kills=1,LastKill=100};w.Tanks[1]=new TankState{Slot=1,Kills=3,LastKill=90};w.Tanks[2]=new TankState{Slot=2,Kills=3,LastKill=80};w.Tanks[3]=new TankState{Slot=3,Kills=3,LastKill=80,Hp=0};
  var ranks=LiveRanking.Sort(w);CollectionAssert.AreEqual(new[]{2,3,1,0},ranks.ConvertAll(t=>t.Slot));
  w.Tanks[0].Kills=4;ranks=LiveRanking.Sort(w);Assert.AreEqual(0,ranks[0].Slot);
  Assert.AreEqual(4,w.Tanks.Count);Assert.AreEqual(0,w.Tanks[3].Hp);
 }
 [Test] public void ExtendedFullStatePreservesHealthEnergyAndTieBreaker(){
  var w=new WorldState();w.Tanks[0]=new TankState{Slot=0,Hp=25,MaxHp=30,Energy=17,EnergyRecovery=13,LastKill=77};
  var bytes=w.Encode();var r=new WireReader(bytes);var decoded=WorldState.Full(r);r.End();CollectionAssert.AreEqual(bytes,decoded.Encode());
  Assert.AreEqual(30,decoded.Tanks[0].MaxHp);Assert.AreEqual(17,decoded.Tanks[0].Energy);Assert.AreEqual(77,decoded.Tanks[0].LastKill);
 }
 [Test] public void OldProtocolVersionIsRejected(){var frame=Protocol.Frame(Op.Heartbeat,1,new byte[0]);frame[2]=1;Assert.Throws<InvalidDataException>(()=>Protocol.Decode(frame));}
 [UnityTest] public IEnumerator RoomHealthIsSavedSentAndReadWithOwnSlot(){
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using(var c=new ClientCore(null,new byte[32]))try{
   var accept=listener.AcceptTcpClientAsync();c.Connect("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,8889);yield return WaitFor(c,()=>c.Phase==ClientPhase.AUTHENTICATING);
   using(var peer=accept.Result){peer.ReceiveTimeout=3000;var stream=peer.GetStream();Drain(stream);
    c.Create("Health",4,180,"",12);var request=ReadRequest(stream,Op.Create);var r=new WireReader(request.Payload);Assert.AreEqual("Health",r.Str());Assert.AreEqual(4,r.U8());Assert.AreEqual(180,r.U16());Assert.AreEqual("",r.Str());Assert.AreEqual(12,r.U16());r.End();
    byte[] frame=Protocol.Frame(Op.RoomState,0,new WireWriter().U32(1).Str("Health").U8(0).U8(0).U8(1).U8(0).Str("Tank").U8(0).U8(4).U16(180).U8(0).U8(0).U16(12).Done());stream.Write(frame,0,frame.Length);stream.Flush();yield return WaitFor(c,()=>c.Phase==ClientPhase.ROOM);Assert.AreEqual(12,c.RoomStartingHp);Assert.AreEqual(0,c.Slot);
    c.ConfigureRoom("Health",4,180,0,"",20);request=ReadRequest(stream,Op.RoomSettings);r=new WireReader(request.Payload);r.Str();r.U8();r.U16();Assert.AreEqual(0,r.U8());Assert.AreEqual(20,r.U16());r.End();
   }
  }finally{listener.Stop();}
 }
 static Packet ReadRequest(NetworkStream stream,byte expected){while(true){var packet=Protocol.Read(stream);if(packet.Op==Op.Heartbeat)continue;Assert.AreEqual(expected,packet.Op);return packet;}}
 static IEnumerator WaitFor(ClientCore c,Func<bool> condition){double deadline=ClientCore.NowUs+5e6;while(!condition()){c.Poll();Assert.Less(ClientCore.NowUs,deadline,c.Message);yield return null;}}
 static void Drain(NetworkStream stream){while(stream.DataAvailable)Assert.AreEqual(Op.Heartbeat,Protocol.Read(stream).Op);}
}
