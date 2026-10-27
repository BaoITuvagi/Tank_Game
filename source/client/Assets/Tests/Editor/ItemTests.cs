using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Tank;
using UnityEngine;
public class ItemTests {
    static byte[] Bytes(string hex) {var b=new byte[hex.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(hex.Substring(i*2,2),16);return b;}
    [TestCase("item-first-spawn.tsv",300,6)]
    [TestCase("roleplay-item-first-spawn.tsv",1200,4)]
    public void ItemSpawnsKeepRemotePlayerAndRoomMembership(string fixture,int firstSpawn,int count) {
        using(var client=new ClientCore(null,new byte[32])) {
            client.Match=77;client.Slot=0;client.Phase=ClientPhase.PLAYING;
            client.Players.Add(new RoomPlayer{Slot=0,Name="Local"});client.Players.Add(new RoomPlayer{Slot=1,Name="Remote"});
            var handle=typeof(ClientCore).GetMethod("Handle",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/"+fixture))) {
                var fields=line.Split('\t');uint tick=uint.Parse(fields[0]);var packet=Protocol.Decode(Bytes(fields[1]),true);
                Assert.DoesNotThrow(()=>handle.Invoke(client,new object[]{packet}));
                Assert.AreEqual(ClientPhase.PLAYING,client.Phase);Assert.AreEqual(2,client.Players.Count);
                Assert.AreEqual(tick,client.Latest.Tick);CollectionAssert.AreEquivalent(new[]{0,1},client.Latest.World.Tanks.Keys);
                bool present=fixture=="roleplay-item-first-spawn.tsv"?RoleplayItemSchedule.Wave(5400,tick)>0:tick>=firstSpawn;
                Assert.AreEqual(present?count:0,client.Latest.World.Items.Count);Assert.Greater(client.Latest.World.Tanks[1].Hp,0);
                CollectionAssert.AreEqual(Bytes(fields[2]),client.Latest.World.Encode());
                var rendered=Rendering.Interpolate(client.Snapshots,client.Latest.Time);
                Assert.AreEqual(2,rendered.Tanks.Count);Assert.Greater(rendered.Tanks[1].Hp,0);
                foreach(var item in rendered.Items.Values)Assert.IsFalse(item.IsExpired(tick));
            }
        }
    }
    [Test] public void MaximumJavaItemSnapshotsSurviveFragmentReorderingAndMissingParts() {
        var lines=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/item-fragments.tsv"));
        var assembler=new SnapshotAssembler();WorldState world=null;
        foreach(string kind in new[]{"FULL","DELTA"}) {
            var packets=lines.Where(line=>line.StartsWith(kind+"\t")).Select(line=>Protocol.Decode(Bytes(line.Split('\t')[1]),true)).ToArray();
            Assert.Greater(packets.Length,10);Packet complete=null;
            for(int i=packets.Length-1;i>=0;i--) {
                var result=assembler.Add(packets[i],7,kind=="FULL"?0u:1u,1000);
                if(i>0) {Assert.IsNull(result);Assert.IsNull(assembler.Add(packets[i],7,kind=="FULL"?0u:1u,1000));}
                else complete=result;
            }
            Assert.IsNotNull(complete);var r=new WireReader(complete.Payload);
            Assert.AreEqual(7,r.U64());r.U32();r.U32();r.U64();r.U32();r.U16();
            if(kind=="FULL")world=WorldState.Full(r);
            else {Assert.AreEqual(1,r.U32());world=DeltaCodec.Apply(world,r);}
            r.End();Assert.AreEqual(8,world.Tanks.Count);Assert.AreEqual(128,world.Bullets.Count);Assert.AreEqual(6,world.Items.Count);
            CollectionAssert.AreEquivalent(new[]{0,1,2,3,4,5},world.Items.Values.Select(item=>item.Type).ToArray());
            foreach(var item in world.Items.Values) {Assert.AreEqual(item.Id%2==0?0:450,item.Expires);Assert.AreEqual(item.Expires!=0,item.IsExpired(450));}
            Assert.AreEqual(GameRules.MaxItemEffects,world.ItemEffects.Count);Assert.AreEqual(GameRules.MaxSkillEffects,world.Effects.Count);
            var missing=new SnapshotAssembler();for(int i=0;i<packets.Length-1;i++)Assert.IsNull(missing.Add(packets[i],7,0,1000));
        }
        CollectionAssert.AreEqual(Bytes(lines.Single(line=>line.StartsWith("EXPECTED\t")).Split('\t')[1]),world.Encode());
        foreach(var t in world.Tanks.Values) {Assert.AreEqual(3,t.MirrorShots);Assert.AreEqual(180,t.ArmorEndTick);Assert.AreEqual(90,t.InvincibilityEndTick);}
    }
    [Test] public void JavaItemsDeltaAndRejoinKeepPurifiedVisionCooldownAndFrozenBullets() {
        WorldState state=null;byte[] expected=null;
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/items.tsv"))) {
            var f=line.Split('\t');if(f[0]=="DROPPED")continue;
            var r=new WireReader(Bytes(f[1]));
            if(f[0]=="REJOIN") {var rejoin=WorldState.Full(r);r.End();CollectionAssert.AreEqual(state.Encode(),rejoin.Encode());continue;}
            state=f[0]=="FULL"?WorldState.Full(r):DeltaCodec.Apply(state,r);r.End();
            if(f.Length==3) {expected=Bytes(f[2]);CollectionAssert.AreEqual(expected,state.Encode());}
        }
        Assert.AreEqual(3,state.Items.Count);Assert.AreEqual(3,state.Tanks[0].MirrorShots);
        Assert.AreEqual(180,state.Tanks[0].ArmorEndTick);Assert.AreEqual(300,state.Tanks[0].InvincibilityEndTick);
        Assert.AreEqual(240,state.Tanks[0].QBaseReadyTick);Assert.AreEqual(240,state.Tanks[0].QReadyTick);Assert.AreEqual(90,state.Tanks[0].FogEndTick);
        Assert.AreEqual(1,state.Bullets[1].Kind);Assert.AreEqual(3,state.Bullets[2].Kind);Assert.AreEqual(10,state.Bullets[2].Damage);
        Assert.AreEqual(1.5f,ItemCodec.BulletSize(state.Bullets[1].Kind));Assert.AreEqual(.7f,ItemCodec.BulletSize(state.Bullets[2].Kind));
        CollectionAssert.AreEqual(expected,state.Copy().Encode());
    }
    [Test] public void CopyAndMalformedExtensionCannotMutateAcknowledgedBase() {
        var w=new WorldState();w.Tanks[0]=new TankState{Slot=0,Hp=50,ArmorEndTick=200,MirrorShots=3};
        w.Items[1]=new ItemState{Id=1,Type=4,X=10,Y=10,Expires=450};byte[] original=w.Encode();
        var copy=w.Copy();copy.Items[1].X=20;copy.Tanks[0].MirrorShots=0;
        CollectionAssert.AreEqual(original,w.Encode());
        var delta=new WireWriter().U8(0).U8(0).U8(0).U8(0).U16(0).U8(1).U8(0).U16(100).U16(100).U16(100).U8(4).U16(0).Done();
        Assert.Throws<InvalidDataException>(()=>DeltaCodec.Apply(w,new WireReader(delta)));CollectionAssert.AreEqual(original,w.Encode());
        w.Items[1].Type=6;Assert.Throws<InvalidDataException>(()=>w.Encode());w.Items[1].Type=0;
        w.Tanks[0].ArmorEndTick=65536;Assert.Throws<InvalidDataException>(()=>w.Encode());
    }
    [Test] public void PermanentItemsRemainVisibleAndTimedItemsExpireAtTheirDeadline() {
        var item=new ItemState{Id=1,Type=0,X=10,Y=10,Expires=0};
        Assert.IsFalse(item.IsExpired(0));Assert.IsFalse(item.IsExpired(18000));Assert.IsFalse(item.IsExpired(uint.MaxValue));
        item.Expires=900;Assert.IsFalse(item.IsExpired(899));Assert.IsTrue(item.IsExpired(900));Assert.IsTrue(item.IsExpired(901));
    }
    [Test] public void SixPermanentItemsRoundTripAndSeventhIsRejected() {
        var world=new WorldState();
        for(uint id=1;id<=6;id++)world.Items[id]=new ItemState{Id=id,Type=(int)id-1,X=id*5,Y=10,Expires=0};
        byte[] full=world.Encode();var reader=new WireReader(full);var decoded=WorldState.Full(reader);reader.End();
        CollectionAssert.AreEqual(full,decoded.Encode());CollectionAssert.AreEqual(full,decoded.Copy().Encode());
        Assert.AreEqual(6,decoded.Items.Count);foreach(var item in decoded.Items.Values)Assert.IsFalse(item.IsExpired(18000));
        world.Items[7]=new ItemState{Id=7,Type=0};Assert.Throws<InvalidDataException>(()=>world.Encode());
    }
    [Test] public void ProjectileKindsValidateDamageAndFullStateRestoresSizes() {
        for(int kind=0;kind<=5;kind++) {
            bool child=ItemCodec.IsMirrorChild(kind);
            var world=new WorldState();world.Bullets[1]=new BulletState{Id=1,Kind=kind,Damage=child?10:20};
            var r=new WireReader(world.Encode());var decoded=WorldState.Full(r);r.End();Assert.AreEqual(kind,decoded.Bullets[1].Kind);
            world.Bullets[1].Damage=15;if(child)Assert.Throws<InvalidDataException>(()=>world.Encode());
            else {r=new WireReader(world.Encode());decoded=WorldState.Full(r);r.End();Assert.AreEqual(15,decoded.Bullets[1].Damage);Assert.AreEqual(kind,decoded.Bullets[1].Kind);}
            world.Bullets[1].Damage=25;if(!child)Assert.Throws<InvalidDataException>(()=>world.Encode());
            else {r=new WireReader(world.Encode());decoded=WorldState.Full(r);r.End();Assert.AreEqual(25,decoded.Bullets[1].Damage);Assert.IsTrue(ItemCodec.IsGerman(decoded.Bullets[1]));}
        }
        Assert.Throws<InvalidDataException>(()=>WorldState.Validate(new BulletState{Kind=6}));
        Assert.AreEqual(1,ItemCodec.BulletSize(0));Assert.AreEqual(1,ItemCodec.BulletSize(2));
        Assert.AreEqual(1.5f,ItemCodec.BulletSize(4));Assert.AreEqual(1.05f,ItemCodec.BulletSize(5),.00001);
    }
    [Test] public void JavaStackedProjectileKindsAndBuffsRepairDroppedDeltaAndRejoin() {
        var lines=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/stacked-projectiles.tsv"));
        var r=new WireReader(Bytes(lines[0].Split('\t')[1]));var baseline=WorldState.Full(r);r.End();byte[] before=baseline.Encode();
        r=new WireReader(Bytes(lines[2].Split('\t')[1]));var repaired=DeltaCodec.Apply(baseline,r);r.End();
        r=new WireReader(Bytes(lines[3].Split('\t')[1]));var rejoin=WorldState.Full(r);r.End();
        CollectionAssert.AreEqual(rejoin.Encode(),repaired.Encode());CollectionAssert.AreEqual(before,baseline.Encode());
        Assert.AreEqual(12,repaired.Bullets.Count);Assert.AreEqual(150,repaired.Tanks[0].HeEndTick);Assert.AreEqual(2,repaired.Tanks[0].MirrorShots);
        foreach(var b in repaired.Bullets.Values)Assert.IsTrue(ItemCodec.ValidBulletDamage(b.Kind,b.Damage));
        Assert.AreEqual(6,repaired.Bullets.Values.Count(b=>ItemCodec.HasHe(b.Kind)));
        Assert.AreEqual(8,repaired.Bullets.Values.Count(b=>ItemCodec.HasMirror(b.Kind)));
        Assert.AreEqual(2,repaired.ItemEffects.Count);
    }
    [Test] public void JavaGermanHeDamageAndKindRepairInFullDeltaAndRejoin() {
        var lines=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/german-he.tsv"));
        var r=new WireReader(Bytes(lines[0].Split('\t')[1]));var baseline=WorldState.Full(r);r.End();
        r=new WireReader(Bytes(lines[1].Split('\t')[1]));var repaired=DeltaCodec.Apply(baseline,r);r.End();
        r=new WireReader(Bytes(lines[2].Split('\t')[1]));var rejoin=WorldState.Full(r);r.End();
        Assert.AreEqual(0,baseline.Bullets[1].Kind);Assert.AreEqual(1,repaired.Bullets[1].Kind);
        Assert.AreEqual(15,repaired.Bullets[1].Damage);Assert.AreEqual(1.5f,ItemCodec.BulletSize(repaired.Bullets[1].Kind));
        CollectionAssert.AreEqual(rejoin.Encode(),repaired.Encode());
    }
    [Test] public void BothRawMapsValidateDryItemSpawnsAndHashesAreDifferent() {
        byte[] first=null;
        foreach(string name in new[]{"map.json","map-large.json"}) {
            byte[] bytes=File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,name));
            var m=JsonUtility.FromJson<GameMap>(System.Text.Encoding.UTF8.GetString(bytes));Assert.DoesNotThrow(m.ValidateItemSpawns);
            Assert.GreaterOrEqual(m.itemSpawns.Length,10);
            using(var sha=System.Security.Cryptography.SHA256.Create()) {var hash=sha.ComputeHash(bytes);if(first==null)first=hash;else CollectionAssert.AreNotEqual(first,hash);}
            m.itemSpawns[1]=m.itemSpawns[0];Assert.Throws<InvalidDataException>(m.ValidateItemSpawns);
        }
    }
    [Test] public void OlderPeersAndTruncatedExtensionsAreRejected() {
        Assert.AreEqual(16,GameRules.ProtocolVersion);
        foreach(byte version in new byte[]{5,6,7,8,9,10,11,12,13,14,15}) {
            var old=Protocol.Frame(Op.Heartbeat,1,new byte[0]);old[2]=version;
            Assert.Throws<InvalidDataException>(()=>Protocol.Decode(old));
            Assert.Throws<InvalidDataException>(()=>Protocol.Read(new MemoryStream(old)));
        }
        byte[] full=new WorldState().Encode();Array.Resize(ref full,full.Length-1);
        Assert.Throws<InvalidDataException>(()=>WorldState.Full(new WireReader(full)));
    }
}
