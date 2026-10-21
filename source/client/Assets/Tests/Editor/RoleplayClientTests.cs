using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine.TestTools;
using Tank;
public class RoleplayClientTests {
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static byte[] ContentHash() {var b=new byte[32];for(int i=0;i<32;i++)b[i]=Convert.ToByte(RoleplayContent.Hash.Substring(2*i,2),16);return b;}
    sealed class Harness : IDisposable {
        public readonly TcpListener Listener=new TcpListener(IPAddress.Loopback,0);
        public readonly UdpClient Udp=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));
        public readonly ClientCore Core;
        public TcpClient Peer;
        public Harness() {
            Core=new ClientCore(new GameMap{mapId=1,width=60,height=60,walls=new MapRect[0],bricks=new MapRect[0],water=new MapRect[0]},new byte[32]);Core.SetRoleplayAssets(null);
        }
        public IEnumerator Connect() {
            Listener.Start();var accepted=Listener.AcceptTcpClientAsync();Core.Connect("127.0.0.1",((IPEndPoint)Listener.LocalEndpoint).Port,((IPEndPoint)Udp.Client.LocalEndPoint).Port);
            yield return Wait(Core,()=>Core.Phase==ClientPhase.AUTHENTICATING);Peer=accepted.Result;Peer.ReceiveTimeout=3000;
            Core.Login("roleplay","password123");var login=Read(Op.Login);Send(Op.LoginResult,login.Sequence,new WireWriter().U16(0).U64(1).Str("Roleplayer").Bytes(new byte[16]).Str("roleplay").Done());
            yield return Wait(Core,()=>Core.Phase==ClientPhase.LOBBY);
        }
        public Packet Read(byte opcode) {
            while(true) {var p=Protocol.Read(Peer.GetStream());if(p.Op==Op.Heartbeat||p.Op==Op.RoomList)continue;Assert.AreEqual(opcode,p.Op);return p;}
        }
        public void Send(byte opcode,uint sequence,byte[] payload) {var bytes=Protocol.Frame(opcode,sequence,payload);Peer.GetStream().Write(bytes,0,bytes.Length);Peer.GetStream().Flush();}
        public void Prepare() {
            Send(Op.Prepare,0,new WireWriter().U64(77).U32(1).U8(0).U16(1).Bytes(new byte[32]).U8(3).U8(30).U16(((IPEndPoint)Udp.Client.LocalEndPoint).Port).U8(0).U8(1).U16(RoleplayContent.Version).Bytes(ContentHash()).U32(0).Done());
        }
        public void Handshake() {Send(Op.HandshakeAck,0,new WireWriter().U64(77).U64(7).U8(0).U64(0).Done());Core.PingSamples=8;Core.MinRttUs=1000;}
        public void Dispose() {Core.Dispose();Peer?.Close();Listener.Stop();Udp.Close();}
    }
    static IEnumerator Wait(ClientCore core,Func<bool> done) {
        double deadline=ClientCore.NowUs+8e6;while(!done()){core.Poll();Assert.Less(ClientCore.NowUs,deadline,"Client state: "+core.Message);yield return null;}
    }
    static byte[] Loadout(bool corrupt=false) {
        byte[] canonical=new WireWriter().U64(77).U32(1).Bytes(ContentHash()).U8(2).U8(0).U8(1).U8(1).U8(0).Done();byte[] hash;
        using(var sha=SHA256.Create())hash=sha.ComputeHash(canonical);if(corrupt)hash[0]^=1;return new WireWriter().Bytes(canonical).Bytes(hash).Done();
    }
    static byte[] Snapshot(uint id,uint tick,byte[] world,uint? baseId=null) {
        var w=new WireWriter().U64(77).U32(id).U32(tick).U64((ulong)ClientCore.NowUs).U32(tick).U16(1700);
        if(baseId.HasValue)w.U32(baseId.Value);return w.Bytes(world).Done();
    }
    [UnityTest] public IEnumerator LoadoutAckAndSnapshotsRecoverWithoutActivationEvents() {
        using(var h=new Harness()) {
            yield return h.Connect();h.Prepare();yield return Wait(h.Core,()=>h.Core.Phase==ClientPhase.PREPARING);h.Handshake();
            yield return Wait(h.Core,()=> (bool)typeof(ClientCore).GetField("prepared",Private).GetValue(h.Core));
            var loaded=h.Read(Op.Prepared);var r=new WireReader(loaded.Payload);Assert.AreEqual(77,r.U64());Assert.AreEqual(0,r.U8());Assert.AreEqual(RoleplayContent.Version,r.U32());CollectionAssert.AreEqual(ContentHash(),r.Bytes(32));r.End();
            h.Send(Op.MatchLoadout,0,Loadout());yield return Wait(h.Core,()=>h.Core.LoadoutReady);var ack=h.Read(Op.Prepared);r=new WireReader(ack.Payload);Assert.AreEqual(77,r.U64());Assert.AreEqual((int)PrepStage.SYNC_LOADOUT,r.U8());Assert.AreEqual(1,r.U32());r.Bytes(32);r.End();
            h.Send(Op.Start,0,new WireWriter().U64(77).U64((ulong)(ClientCore.NowUs-1000)).Done());yield return Wait(h.Core,()=>h.Core.Phase==ClientPhase.PLAYING);
            var world=new WorldState();world.Tanks[0]=new TankState{Slot=0,Hp=3,QReadyTick=700,QEndTick=130,ProtectionFlags=1};
            h.Send(Op.Full,0,Snapshot(1,100,world.Encode()));yield return Wait(h.Core,()=>h.Core.Latest!=null&&h.Core.Latest.Tick==100);
            Assert.AreEqual(1,h.Core.Latest.World.Tanks[0].Nation);Assert.AreEqual(0,h.Core.SkillFeedbackRevision);
            // Snapshot 2 is lost. Snapshot 3 repairs the early end against acknowledged base 1.
            byte[] delta=new WireWriter().U8(1).U8(0).U32((1u<<16)|(1u<<18)).U16(101).U8(0).U8(0).U8(0).U8(0).U16(0).Done();
            h.Send(Op.Delta,0,Snapshot(3,102,delta,1));yield return Wait(h.Core,()=>h.Core.Latest.Tick==102);
            Assert.AreEqual(101,h.Core.Latest.World.Tanks[0].QEndTick);Assert.AreEqual(0,h.Core.Latest.World.Tanks[0].ProtectionFlags);
            h.Send(Op.Full,0,Snapshot(4,99,world.Encode()));h.Send(Op.Notice,0,new WireWriter().U16(0).Str("barrier").Done());yield return Wait(h.Core,()=>h.Core.Message=="barrier");Assert.AreEqual(102,h.Core.Latest.Tick);
            h.Send(Op.Delta,0,Snapshot(5,103,new byte[0],999));yield return Wait(h.Core,()=>h.Core.MissingBase==1);Assert.AreEqual(102,h.Core.Latest.Tick);Assert.AreEqual(0,h.Core.Invalid);
        }
    }
    [UnityTest] public IEnumerator QPendingTimeoutAndDuplicateResultsPreserveFeedback() {
        using(var h=new Harness()) {
            yield return h.Connect();var c=h.Core;h.Prepare();yield return Wait(c,()=>c.Phase==ClientPhase.PREPARING);h.Handshake();
            yield return Wait(c,()=> (bool)typeof(ClientCore).GetField("prepared",Private).GetValue(c));h.Read(Op.Prepared);
            c.LoadoutReady=true;
            c.Loadouts[0]=new MatchLoadout{Slot=0,Nation=0};var initial=new WorldState();initial.Tanks[0]=new TankState{Slot=0,Hp=3};
            h.Send(Op.Full,0,Snapshot(1,100,initial.Encode()));yield return Wait(c,()=>c.Latest!=null);
            h.Send(Op.Start,0,new WireWriter().U64(77).U64((ulong)(ClientCore.NowUs-1000)).Done());yield return Wait(c,()=>c.Phase==ClientPhase.PLAYING);
            Assert.IsFalse(c.UseSkill(1));Assert.IsTrue(c.UseSkill(0));Assert.IsFalse(c.UseSkill(0));Assert.AreEqual(1,c.SkillPressRevision);
            var q=h.Read(Op.UseSkill);var qr=new WireReader(q.Payload);qr.U64();uint qid=qr.U32();Assert.AreEqual(0,qr.U8());qr.F32();qr.F32();qr.End();
            byte[] success=new WireWriter().U16(0).U64(77).U32(qid).U8(Op.UseSkill).U8(0).U32(101).U16(851).Done();h.Send(Op.ActionResult,q.Sequence,success);
            yield return Wait(c,()=>!c.SkillPending(0));Assert.AreEqual(1,c.SkillFeedbackRevision);Assert.AreEqual(851,c.SkillReadyTick(0));
            h.Send(Op.ActionResult,q.Sequence,success);h.Send(Op.Notice,0,new WireWriter().U16(0).Str("barrier").Done());yield return Wait(c,()=>c.Message=="barrier");Assert.AreEqual(1,c.SkillFeedbackRevision);
            initial.Tanks[0].QReadyTick=851;h.Send(Op.Full,0,Snapshot(2,851,initial.Encode()));yield return Wait(c,()=>c.Latest.Tick==851);
            Assert.IsTrue(c.UseSkill(0));var retry=h.Read(Op.UseSkill);var rr=new WireReader(retry.Payload);rr.U64();uint retryId=rr.U32();Assert.AreEqual(0,rr.U8());rr.F32();rr.F32();rr.End();
            while(h.Udp.Available>0) {var endpoint=new IPEndPoint(IPAddress.Any,0);h.Udp.Receive(ref endpoint);}
            var pending=(Array)typeof(ClientCore).GetField("pendingSkills",Private).GetValue(c);object slot=pending.GetValue(0);slot.GetType().GetField("Since").SetValue(slot,ClientCore.NowUs-2_100_000);
            c.Poll();Assert.IsTrue(c.SkillPending(0));Assert.IsFalse(c.UseSkill(0));Assert.AreEqual(2,c.SkillPressRevision);
            int resyncs=0;while(h.Udp.Available>0) {var endpoint=new IPEndPoint(IPAddress.Any,0);if(Protocol.Decode(h.Udp.Receive(ref endpoint),true).Op==Op.Resync)resyncs++;}Assert.AreEqual(1,resyncs);
            c.Poll();while(h.Udp.Available>0) {var endpoint=new IPEndPoint(IPAddress.Any,0);Assert.AreNotEqual(Op.Resync,Protocol.Decode(h.Udp.Receive(ref endpoint),true).Op);}
            h.Send(Op.ActionResult,retry.Sequence,new WireWriter().U16((int)ErrorCode.COOLDOWN).Str("COOLDOWN").U64(77).U32(retryId).U8(Op.UseSkill).U8(0).U32(851).U16(900).Done());
            yield return Wait(c,()=>!c.SkillPending(0));Assert.AreEqual(2,c.SkillFeedbackRevision);Assert.IsFalse(c.LastSkillSucceeded);Assert.AreEqual(0,c.Invalid);
        }
    }
    [UnityTest] public IEnumerator MissingAssetsAndCorruptLoadoutLeaveWithSpecificError() {
        for(int failure=0;failure<2;failure++)using(var h=new Harness()) {
            yield return h.Connect();if(failure==0)h.Core.SetRoleplayAssets("missing-sprite");h.Prepare();
            if(failure==1) {yield return Wait(h.Core,()=>h.Core.Phase==ClientPhase.PREPARING);h.Send(Op.MatchLoadout,0,Loadout(true));}
            yield return Wait(h.Core,()=>!string.IsNullOrEmpty(h.Core.PreparationError));h.Read(Op.Leave);Assert.IsFalse(h.Core.LoadoutReady);
            StringAssert.Contains(failure==0?"missing-sprite":"không khớp",h.Core.PreparationError);
        }
    }
    [UnityTest] public IEnumerator HistoryMetadataPreservesNullNationAndLegacyDuration() {
        using(var h=new Harness()) {
            yield return h.Connect();
            for(uint match=1;match<=2;match++) {
                h.Core.GetHistoryDetail(match);var request=h.Read(Op.HistoryDetail);
                h.Send(Op.HistoryDetailResult,request.Sequence,new WireWriter().U16(0).U64(match).U16(1).U64(1000).U64(2500).U8(0).U8(1).U8(0).Str("Legacy").U16(2).U16(0).U8(1).U8(0).U8(1).U32(uint.MaxValue).U8(1).Str("ROLEPLAY").Str("content").U8(1).U8(0).U8(match==1?255:0).Done());
                yield return Wait(h.Core,()=>h.Core.HistoryDetail!=null&&h.Core.HistoryDetail.Match==match);
                Assert.IsNull(h.Core.HistoryDetail.DurationTicks);Assert.AreEqual(2,h.Core.HistoryDetail.DurationSeconds);Assert.AreEqual(match==1?-1:0,h.Core.HistoryDetail.Players[0].Nation);
            }
        }
    }
}
