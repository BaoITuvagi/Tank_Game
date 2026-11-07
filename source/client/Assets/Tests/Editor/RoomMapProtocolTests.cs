using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NUnit.Framework;
using Tank;
using UnityEngine.TestTools;

public class RoomMapProtocolTests {
    static readonly MethodInfo Handle=typeof(ClientCore).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic);
    static void Receive(ClientCore core,byte opcode,byte[] payload) {
        try {Handle.Invoke(core,new object[]{new Packet{Op=opcode,Payload=payload}});}
        catch(TargetInvocationException error) {throw error.InnerException;}
    }
    static byte[] State(int mapId,RoomMode mode) {
        var w=new WireWriter().U32(42).Str("Selected map").U8(0).U8(0).U8(2).U8(0).Str("Host").U8(0).U8(1).Str("Peer").U8(1)
            .U8(4).U16(180).U8(0).U8(0).U16(200).U16(mapId).U8((int)mode);
        if(mode==RoomMode.ROLEPLAY)w.U8((int)PrepStage.LOADING).U64(0).U64(0).U32(0).U8(3).U8(5);
        return w.Done();
    }
    static GameMap Map(int id) {return new GameMap{mapId=id,width=120,height=80,walls=new MapRect[0],bricks=new MapRect[0],water=new MapRect[0]};}
    static byte[] Hash(int id) {var hash=new byte[32];hash[0]=(byte)id;return hash;}
    static byte[] Prepare(int mapId,byte[] hash) {
        return new WireWriter().U64(77).U32(42).U8(0).U16(mapId).Bytes(hash).U8(3).U8(30).U16(8889).Done();
    }
    [TestCase(2,RoomMode.NORMAL)] [TestCase(3,RoomMode.NORMAL)] [TestCase(4,RoomMode.NORMAL)]
    [TestCase(2,RoomMode.ROLEPLAY)] [TestCase(3,RoomMode.ROLEPLAY)] [TestCase(4,RoomMode.ROLEPLAY)]
    public void RoomStateHasAnUnambiguousMapAndModeBeforeTheRoleplayExtension(int id,RoomMode mode) {
        using(var core=new ClientCore(null,new byte[32])) {
            core.Phase=ClientPhase.LOBBY;Receive(core,Op.RoomState,State(id,mode));
            Assert.AreEqual(id,core.RoomMapId);Assert.AreEqual(mode,core.RoomMode);Assert.AreEqual(200,core.RoomStartingHp);Assert.AreEqual(0,core.Slot);Assert.AreEqual(ClientPhase.ROOM,core.Phase);
            if(mode==RoomMode.ROLEPLAY){Assert.AreEqual(3,core.Players[0].Nation);Assert.AreEqual(5,core.Players[1].Nation);}
            Receive(core,Op.Removed,new WireWriter().U32(42).U8(0).U8(0).Done());Assert.AreEqual(MapCatalog.DefaultId,core.RoomMapId);
        }
    }
    [Test] public void RoomListKeepsMapIdsAlignedWithRoomFlagsAndModes() {
        using(var core=new ClientCore(null,new byte[32])) {
            Receive(core,Op.RoomListResult,new WireWriter().U16(0).U16(2)
                .U32(11).Str("Fortress").U8(1).U8(4).U8(0).U32(22).Str("River").U8(2).U8(8).U8(2)
                .U8(0).U8(1).U8(0).U8(1).U8(0).U8(1).U16(3).U16(4).Done());
            Assert.AreEqual(3,core.Rooms[0].MapId);Assert.AreEqual(RoomMode.NORMAL,core.Rooms[0].Mode);Assert.IsFalse(core.Rooms[0].Locked);
            Assert.AreEqual(4,core.Rooms[1].MapId);Assert.AreEqual(RoomMode.ROLEPLAY,core.Rooms[1].Mode);Assert.IsTrue(core.Rooms[1].Locked);Assert.IsTrue(core.Rooms[1].CanRejoin);
        }
    }
    [Test] public void UnknownMapAndTruncatedOrOldRoomMetadataAreRejected() {
        using(var core=new ClientCore(null,new byte[32])) {
            foreach(int id in new[]{0,1,5,65535}) {
                Assert.Throws<InvalidDataException>(()=>Receive(core,Op.RoomState,State(id,RoomMode.NORMAL)));
                Assert.Throws<InvalidDataException>(()=>Receive(core,Op.RoomListResult,new WireWriter().U16(0).U16(1).U32(1).Str("Unknown").U8(1).U8(2).U8(0).U8(0).U8(0).U8(0).U16(id).Done()));
                Assert.Throws<ArgumentOutOfRangeException>(()=>core.Create("Unknown",2,60,"",100,RoomMode.NORMAL,id));
            }
            var old=State(3,RoomMode.NORMAL);Array.Resize(ref old,old.Length-3);
            Assert.Throws<InvalidDataException>(()=>Receive(core,Op.RoomState,old));
            Assert.Throws<InvalidDataException>(()=>Receive(core,Op.RoomListResult,new WireWriter().U16(0).U16(1).U32(1).Str("Old").U8(1).U8(2).U8(0).U8(0).U8(0).U8(0).Done()));
        }
    }
    [Test] public void PrepareUsesTheSelectedRegisteredMapAndRejectsUnknownIdsOrHashMismatch() {
        foreach(int id in new[]{2,3,4})using(var core=new ClientCore(Map(2),Hash(2))) {
            var selected=Map(id);core.AddMap(selected,Hash(id));
            typeof(ClientCore).GetField("remote",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(core,new IPEndPoint(IPAddress.Loopback,8889));
            Receive(core,Op.Prepare,Prepare(id,Hash(id)));Assert.AreSame(selected,core.ActiveMap);Assert.AreEqual(id,core.RoomMapId);Assert.AreEqual(ClientPhase.PREPARING,core.Phase);
        }
        foreach(int id in new[]{3,99})using(var core=new ClientCore(Map(2),Hash(2))) {
            var initial=core.ActiveMap;core.AddMap(Map(3),Hash(3));core.Phase=ClientPhase.ROOM;
            Receive(core,Op.Prepare,Prepare(id,new byte[32]));StringAssert.Contains("Map không khớp",core.PreparationError);Assert.AreSame(initial,core.ActiveMap);Assert.AreEqual(ClientPhase.ROOM,core.Phase);
        }
    }
    [Test] public void JavaRoomFixturesDecodeMapAndRoleplayNationUsingTheCurrentLayout() {
        bool normal=false,roleplay=false;
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/golden.tsv"))) {
            var fields=line.Split('\t');if(fields[0]!="op_40"&&fields[0]!="op_40_roleplay_map")continue;
            var raw=new byte[fields[1].Length/2];for(int i=0;i<raw.Length;i++)raw[i]=Convert.ToByte(fields[1].Substring(i*2,2),16);var packet=Protocol.Decode(raw);
            using(var core=new ClientCore(null,new byte[32])) {
                Receive(core,packet.Op,packet.Payload);
                if(fields[0]=="op_40") {normal=true;Assert.AreEqual(3,core.RoomMapId);Assert.AreEqual(RoomMode.NORMAL,core.RoomMode);}
                else {roleplay=true;Assert.AreEqual(4,core.RoomMapId);Assert.AreEqual(RoomMode.ROLEPLAY,core.RoomMode);Assert.AreEqual(5,core.Players[0].Nation);}
            }
        }
        Assert.IsTrue(normal&&roleplay);
    }
    [UnityTest] public IEnumerator CreateSendsSelectedMapAndDefaultsToTheExistingArena() {
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        using(var core=new ClientCore(null,new byte[32]))try {
            var accept=listener.AcceptTcpClientAsync();core.Connect("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,8889);
            double deadline=ClientCore.NowUs+5e6;while(core.Phase!=ClientPhase.AUTHENTICATING){core.Poll();Assert.Less(ClientCore.NowUs,deadline);yield return null;}
            using(var peer=accept.Result) {
                peer.ReceiveTimeout=3000;
                foreach(var mode in new[]{RoomMode.NORMAL,RoomMode.ROLEPLAY})foreach(int id in new[]{2,3,4}) {
                    if(id==2)core.Create("Map test",4,180,"",200,mode);else core.Create("Map test",4,180,"",200,mode,id);
                    Packet packet;do{packet=Protocol.Read(peer.GetStream());}while(packet.Op==Op.Heartbeat);
                    Assert.AreEqual(Op.Create,packet.Op);var r=new WireReader(packet.Payload);Assert.AreEqual("Map test",r.Str());Assert.AreEqual(4,r.U8());Assert.AreEqual(180,r.U16());Assert.AreEqual("",r.Str());Assert.AreEqual(200,r.U16());Assert.AreEqual((int)mode,r.U8());Assert.AreEqual(id,r.U16());r.End();
                }
                Assert.IsFalse(core.SelectPreviewMap(3),"Preview map selection must not replace an online map");
            }
        }finally{listener.Stop();}
    }
}
