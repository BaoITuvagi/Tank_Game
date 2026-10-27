using System;
using System.IO;
using NUnit.Framework;
using Tank;
public class RoleplayCodecTests {
    static byte[] Bytes(string hex) {
        var bytes=new byte[hex.Length/2];for(int i=0;i<bytes.Length;i++)bytes[i]=Convert.ToByte(hex.Substring(i*2,2),16);return bytes;
    }
    [Test] public void JavaRoleplayTracePreservesDeadlinesEarlyEndsAndProjectiles() {
        WorldState state=null;
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/roleplay-trace.tsv"))) {
            var fields=line.Split('\t');var r=new WireReader(Bytes(fields[1]));
            state=fields[0]=="FULL"?WorldState.Full(r):DeltaCodec.Apply(state,r);r.End();
            if(fields.Length>2)CollectionAssert.AreEqual(Bytes(fields[2]),state.Encode());
        }
        Assert.AreEqual(51,state.Tanks[0].QEndTick);Assert.AreEqual(0,state.Tanks[0].ProtectionFlags);
        Assert.AreEqual(19000,state.Tanks[0].QReadyTick);Assert.AreEqual(140,state.Bullets[1].SpeedPercent);Assert.AreEqual(70,state.Bullets[1].RemainingLifetime);
        Assert.AreEqual(19000,state.Tanks[0].Copy().QReadyTick);
    }
    [Test] public void JavaGoldenFramesCoverV8AndAllRoleplayOpcodes() {
        var seen=new System.Collections.Generic.HashSet<byte>();
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/golden.tsv"))) {
            byte[] bytes=Bytes(line.Split('\t')[1]);var packet=Protocol.Decode(bytes);
            CollectionAssert.AreEqual(bytes,Protocol.Frame(packet.Op,packet.Sequence,packet.Payload));seen.Add(packet.Op);
        }
        Assert.AreEqual(46,seen.Count);foreach(byte op in new byte[]{80,84,85,86,87})Assert.IsTrue(seen.Contains(op));
        var old=Protocol.Frame(Op.Heartbeat,1,new byte[0]);old[2]=3;Assert.Throws<InvalidDataException>(()=>Protocol.Decode(old));
    }
    [Test] public void InvalidEffectMaskLeavesBaseUntouched() {
        var world=new WorldState();world.Tanks[0]=new TankState();var before=world.Encode();
        Assert.Throws<InvalidDataException>(()=>DeltaCodec.Apply(world,new WireReader(new WireWriter().U8(1).U8(0).U32(1u<<29).Done())));
        CollectionAssert.AreEqual(before,world.Encode());world.Tanks[0].ProtectionFlags=4;Assert.Throws<InvalidDataException>(()=>world.Encode());
    }
    [Test] public void JavaMovementTraceMatchesBuffAndDeadlinePrediction() {
        var map=new GameMap{width=60,height=60,walls=new MapRect[0],bricks=new MapRect[0],water=new[]{new MapRect{x=24,y=12,w=8,h=8}}};
        int count=0;
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/roleplay-movement.tsv"))) {
            var f=line.Split('\t');var r=new WireReader(Bytes(f[4]));var tank=WorldState.ReadTank(r);r.End();tank.Nation=int.Parse(f[0]);
            Movement.Step(tank,new InputRecord(uint.Parse(f[1]),int.Parse(f[2]),int.Parse(f[3])),map,new byte[64]);
            var expectedReader=new WireReader(Bytes(f[5]));var expected=WorldState.ReadTank(expectedReader);expectedReader.End();
            Assert.AreEqual(expected.X,tank.X,.00001);Assert.AreEqual(expected.Y,tank.Y,.00001);Assert.AreEqual(expected.Turret,tank.Turret);Assert.AreEqual(expected.Energy,tank.Energy);Assert.AreEqual(expected.EnergyRecovery,tank.EnergyRecovery);count++;
        }
        Assert.AreEqual(72,count);
    }
}
