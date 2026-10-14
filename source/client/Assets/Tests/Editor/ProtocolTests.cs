using System;
using System.IO;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using Tank;
using UnityEngine.TestTools;
public class ProtocolTests {
 [Test]public void BackgroundAndModalInputStayNeutral(){foreach(int angle in new[]{0,9000,18000,35999}){var sample=GameplayInput.Resolve(false,31,angle,12345);Assert.AreEqual(0,sample.Buttons);Assert.AreEqual(12345,sample.Aim);}var focused=GameplayInput.Resolve(true,8,9000,12345);Assert.AreEqual(8,focused.Buttons);Assert.AreEqual(9000,focused.Aim);}
 [Test]public void ReturnToRoomKeepsExistingMembership(){using(var c=new ClientCore(null,new byte[32])){c.Phase=ClientPhase.RESULTS;c.RoomId=42;c.Slot=3;c.RoomPhase=RoomPhase.RESULTS;c.Message="Đã rời phòng.";c.BackToRoom();Assert.AreEqual(ClientPhase.ROOM,c.Phase);Assert.AreEqual(42,c.RoomId);Assert.AreEqual(3,c.Slot);Assert.AreEqual("",c.Message);}}
 [UnityTest]public IEnumerator NewRoomClearsOldMessageAndIdentifiesDuplicateAliases(){
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using(var c=new ClientCore(null,new byte[32]))try{
   c.Nickname="Same";var accept=listener.AcceptTcpClientAsync();c.Connect("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,8889);yield return WaitFor(c,()=>c.Phase==ClientPhase.AUTHENTICATING);
   using(var peer=accept.Result){c.Phase=ClientPhase.LOBBY;c.Message="Đã rời phòng.";WriteResponse(peer.GetStream(),Op.RoomState,0,new WireWriter().U32(2).Str("New").U8(0).U8(0).U8(2).U8(0).Str("Same").U8(0).U8(1).Str("Same").U8(0).U8(4).U16(180).U8(1).U8(0).Done());yield return WaitFor(c,()=>c.Phase==ClientPhase.ROOM);Assert.AreEqual("",c.Message);Assert.AreEqual(0,c.Slot);Assert.IsTrue(c.RoomLocked);}
  }finally{listener.Stop();}
 }
 [Test]public void AimTracksCursorAfterResizeAndOnEitherMap(){
  foreach(float uiScale in new[]{.75f,1f,1.35f})foreach(float height in new[]{60f,80f}){
   var tank=new TankState{X=height/2,Y=height/2};float arenaX=120,arenaY=108,units=640/height;
   foreach(var aim in new[]{new[]{10f,0f,0f},new[]{0f,10f,9000f},new[]{-10f,0f,18000f},new[]{0f,-10f,27000f},new[]{10f,10f,4500f}}){
    float pixelX=uiScale*(arenaX+(tank.X+aim[0])*units),pixelY=uiScale*(arenaY+(height-tank.Y-aim[1])*units);
    Assert.AreEqual((int)aim[2],GameplayAim.Angle(pixelX,pixelY,uiScale,arenaX,arenaY,units,height,tank));
   }
  }
 }
 [UnityTest] public IEnumerator RoomSettingsAndAfkNoticesUpdateClientState(){
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
  using(var client=new ClientCore(null,new byte[32]))try{
   client.Nickname="Tester";var accept=listener.AcceptTcpClientAsync();client.Connect("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,8889);
   yield return WaitFor(client,()=>client.Phase==ClientPhase.AUTHENTICATING);
   using(var peer=accept.Result){client.Nickname="Tester";var stream=peer.GetStream();
    WriteResponse(stream,Op.RoomState,0,new WireWriter().U32(24).Str("Friends").U8(0).U8(0).U8(2).U8(0).Str("Tester").U8(1).U8(1).Str("Partner").U8(0).U8(8).U16(600).Done());
    yield return WaitFor(client,()=>client.Phase==ClientPhase.ROOM);Assert.AreEqual(8,client.RoomCapacity);Assert.AreEqual(600,client.RoomSeconds);Assert.AreEqual("Friends",client.RoomName);Assert.AreEqual(0,client.Slot);
    WriteResponse(stream,Op.Notice,0,new WireWriter().U16(100).Str("AFK: hãy di chuyển, ngắm hoặc bắn").Done());yield return WaitFor(client,()=>client.AfkWarning);Assert.Greater(client.AfkWarnedAt,0);
    WriteResponse(stream,Op.Notice,0,new WireWriter().U16(101).Str("Đã tiếp tục hoạt động").Done());yield return WaitFor(client,()=>!client.AfkWarning);
    WriteResponse(stream,Op.Removed,0,new WireWriter().U32(24).U8(0).U8(3).Done());yield return WaitFor(client,()=>client.RoomId==0);StringAssert.Contains("không hoạt động trong 15 giây sau cảnh báo AFK",client.Message);
   }
  }finally{listener.Stop();}
 }
 [Test] public void UnsignedAnglesAndFraming(){foreach(int angle in new[]{0,18000,32768,35999})Assert.AreEqual(angle,new WireReader(new WireWriter().U16(angle).Done()).U16());Assert.IsTrue(Protocol.Newer(1,uint.MaxValue));var p=Protocol.Frame(Op.Register,0x01020304,new WireWriter().Str("tank").Str("password").Done());Assert.AreEqual("544202010102030400100000000474616e6b000870617373776f7264",Hex(p));Assert.Throws<InvalidDataException>(()=>Protocol.Decode(new byte[13],true));}
 [Test] public void JavaTraceReconstructs(){string path=Path.GetFullPath("../../source/server/target/interop/delta-trace.tsv");WorldState world=null;foreach(string line in File.ReadAllLines(path)){string[] fields=line.Split('\t');var r=new WireReader(Unhex(fields[1]));world=fields[0]=="FULL"?WorldState.Full(r):DeltaCodec.Apply(world,r);r.End();if(fields.Length>2)Assert.AreEqual(fields[2],Hex(world.Encode()));}}
 [Test] public void MovementAndTurret(){var map=new GameMap{walls=new MapRect[0],bricks=new MapRect[0],water=new MapRect[0]};var straight=new TankState{Hp=3,X=5,Y=5};var diagonal=straight.Copy();Movement.Step(straight,new InputRecord(1,1,0),map,new byte[64]);Movement.Step(diagonal,new InputRecord(1,9,0),map,new byte[64]);Assert.AreEqual(straight.Y-5,Math.Sqrt(Math.Pow(diagonal.X-5,2)+Math.Pow(diagonal.Y-5,2)),.00001);Assert.AreEqual(100,Movement.Turn(35900,100));Assert.AreEqual(35600,Movement.Turn(0,18000));}
 [Test] public void DeltaIsAtomic(){var state=new WorldState();var bad=new WireReader(new byte[]{1,0,0,1,0,0,0,0});Assert.Throws<InvalidDataException>(()=>DeltaCodec.Apply(state,bad));Assert.AreEqual(0,state.Tanks.Count);}
 [Test] public void InterpolationDoesNotShowFutureDeathOrBounce(){var a=new Snapshot{Time=1000,World=new WorldState()};a.World.Tanks[0]=new TankState{Slot=0,Hp=3,X=1,Turret=35900};a.World.Bullets[1]=new BulletState{Id=1,X=2,Bounces=0};var b=new Snapshot{Time=2000,World=a.World.Copy()};b.World.Tanks[0].Hp=0;b.World.Bullets[1].X=10;b.World.Bullets[1].Bounces=1;var middle=Rendering.Interpolate(new[]{a,b},1500);Assert.AreEqual(3,middle.Tanks[0].Hp);Assert.AreEqual(2,middle.Bullets[1].X);var after=Rendering.Interpolate(new[]{a,b},2000);Assert.AreEqual(0,after.Tanks[0].Hp);Assert.AreEqual(10,after.Bullets[1].X);}
 [Test] public void InterpolationUsesShortAngleArc(){var a=new Snapshot{Time=1000,World=new WorldState()};a.World.Tanks[0]=new TankState{Slot=0,Hp=3,X=1,Turret=35900};var b=new Snapshot{Time=2000,World=a.World.Copy()};b.World.Tanks[0].X=3;b.World.Tanks[0].Turret=100;var middle=Rendering.Interpolate(new[]{a,b},1500);Assert.AreEqual(2,middle.Tanks[0].X);Assert.AreEqual(0,middle.Tanks[0].Turret);}
 [UnityTest] public IEnumerator InvalidCredentialsAndRepeatedClicksDoNotSendExtraAuthRequests(){
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
  using(var client=new ClientCore(null,new byte[32]))try{
   var accept=listener.AcceptTcpClientAsync();client.Connect("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,8889);
   yield return WaitFor(client,()=>client.Phase==ClientPhase.AUTHENTICATING);
   using(var peer=accept.Result){peer.ReceiveTimeout=3000;var stream=peer.GetStream();
    foreach(var pair in new[]{new[]{"test1","short"},new[]{"tést1","password123"},new[]{"test 1","password123"},new[]{"test1\n","password123"},new[]{"test1",new string('x',65)}}){client.Register(pair[0],pair[1]);Assert.IsFalse(client.AuthenticationPending);Assert.IsNotEmpty(client.Message);}
    DrainHeartbeats(stream);
    client.Register("test1","password123");client.Register("test1","password123");client.Login("test1","password123");
    Assert.IsTrue(client.AuthenticationPending);var request=ReadRequest(stream,Op.Register);
    var credentials=new WireReader(request.Payload);Assert.AreEqual("test1",credentials.Str());Assert.AreEqual("password123",credentials.Str());credentials.End();
    DrainHeartbeats(stream);
    WriteResponse(stream,Op.RegisterResult,request.Sequence,new WireWriter().U16((int)ErrorCode.RATE_LIMITED).Str("RATE_LIMITED").Done());
    yield return WaitFor(client,()=>!client.AuthenticationPending);
    Assert.Greater(client.AuthenticationRetrySeconds,0);client.Register("test1","password123");Assert.IsFalse(client.AuthenticationPending);DrainHeartbeats(stream);
   }
  }finally{listener.Stop();}
 }
 [UnityTest] public IEnumerator RegistrationSuccessAllowsLoginWithoutDuplicateRequests(){
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
  using(var client=new ClientCore(null,new byte[32]))try{
   var accept=listener.AcceptTcpClientAsync();client.Connect("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,8889);
   yield return WaitFor(client,()=>client.Phase==ClientPhase.AUTHENTICATING);
   using(var peer=accept.Result){peer.ReceiveTimeout=3000;var stream=peer.GetStream();DrainHeartbeats(stream);
    client.Register("test1","password123");var registration=ReadRequest(stream,Op.Register);
    WriteResponse(stream,Op.RegisterResult,registration.Sequence,new WireWriter().U16(0).Done());
    yield return WaitFor(client,()=>client.Message=="Đăng ký thành công");Assert.IsFalse(client.AuthenticationPending);
    client.Login("test1","password123");client.Login("test1","password123");var login=ReadRequest(stream,Op.Login);DrainHeartbeats(stream);
    WriteResponse(stream,Op.LoginResult,login.Sequence,new WireWriter().U16(0).U64(1).Str("test1").Bytes(new byte[16]).Done());
    yield return WaitFor(client,()=>client.Phase==ClientPhase.LOBBY);Assert.AreEqual("test1",client.Nickname);Assert.IsFalse(client.AuthenticationPending);
   }
  }finally{listener.Stop();}
 }
 static Packet ReadRequest(NetworkStream stream,byte expected){while(true){var packet=Protocol.Read(stream);if(packet.Op==Op.Heartbeat)continue;Assert.AreEqual(expected,packet.Op);return packet;}}
 static IEnumerator WaitFor(ClientCore client,Func<bool> condition){double deadline=ClientCore.NowUs+5e6;while(!condition()){client.Poll();Assert.Less(ClientCore.NowUs,deadline,"Client did not reach the expected auth state: "+client.Message);yield return null;}}
 static void DrainHeartbeats(NetworkStream stream){while(stream.DataAvailable)Assert.AreEqual(Op.Heartbeat,Protocol.Read(stream).Op,"Unexpected extra authentication request");}
 static void WriteResponse(NetworkStream stream,byte op,uint sequence,byte[] payload){byte[] frame=Protocol.Frame(op,sequence,payload);stream.Write(frame,0,frame.Length);stream.Flush();}
 static byte[] Unhex(string x){byte[] b=new byte[x.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(x.Substring(i*2,2),16);return b;}static string Hex(byte[] b){return BitConverter.ToString(b).Replace("-","").ToLowerInvariant();}
}
