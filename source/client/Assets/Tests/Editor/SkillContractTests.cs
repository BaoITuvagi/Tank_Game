using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Tank;
public class SkillContractTests {
    static byte[] Bytes(string hex) {var b=new byte[hex.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(hex.Substring(i*2,2),16);return b;}
    [Test] public void ItalianChargeAndJapaneseWarningMatchUpdatedServerRules() {
        Assert.AreEqual(60,GameRules.BeamChargeTicks);Assert.AreEqual(GameRules.BeamChargeTicks,RoleplayContent.NationDurations[5]);
        Assert.AreEqual(9.5625f,GameRules.KamikazeRadius);Assert.AreEqual(150,GameRules.KamikazeTicks);Assert.AreEqual(GameRules.KamikazeTicks,RoleplayContent.NationDurations[6]);
        Assert.AreEqual(50,GameRules.SlowSpeedPercent);Assert.AreEqual(30,GameRules.SlowDurationTicks);Assert.AreEqual(300,GameRules.SabotageCooldownPenaltyTicks);
        Assert.AreEqual(1029,RoleplayContent.NationCooldowns[5]);Assert.AreEqual(1029,RoleplayContent.NationCooldowns[6]);
    }
    [Test] public void BombSlowHalvesGroundWaterAndSprintSpeedUntilTheExactDeadline() {
        foreach(bool wet in new[]{false,true})foreach(int buttons in new[]{8,40,41})foreach(uint tick in new uint[]{174,175}) {
            var map=new GameMap{width=60,height=60,walls=new MapRect[0],bricks=new MapRect[0],water=wet?new[]{new MapRect{x=0,y=0,w=60,h=60}}:new MapRect[0]};
            var t=new TankState{Hp=100,X=20,Y=20,SlowEndTick=175};
            Movement.Step(t,new InputRecord(tick,buttons,0),map,new byte[64]);
            double distance=Math.Sqrt((t.X-20)*(t.X-20)+(t.Y-20)*(t.Y-20));
            double expected=.3*(wet?.5:1)*((buttons&32)!=0?1.5:1)*(tick<175?.5:1);
            Assert.AreEqual(expected,distance,.00001);Assert.AreEqual((buttons&32)!=0?149:150,t.Energy);
        }
    }
    [Test] public void JavaBombSlowAndSabotageRepairMatchRejoinState() {
        var lines=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/skill-debuffs.tsv"));
        var full=new WireReader(Bytes(lines[0].Split('\t')[1]));var baseline=WorldState.Full(full);full.End();
        var delta=new WireReader(Bytes(lines[1].Split('\t')[1]));var repaired=DeltaCodec.Apply(baseline,delta);delta.End();
        var rejoin=new WireReader(Bytes(lines[2].Split('\t')[1]));var restored=WorldState.Full(rejoin);rejoin.End();
        Assert.AreEqual(175,repaired.Tanks[1].SlowEndTick);Assert.AreEqual(1300,repaired.Tanks[0].QReadyTick);Assert.AreEqual(445,repaired.Tanks[0].FireLockEndTick);
        CollectionAssert.AreEqual(restored.Encode(),repaired.Encode());Assert.AreEqual(0,baseline.Tanks[1].SlowEndTick);
    }
    [Test] public void HealthAbove255AndPercentBarsRoundTrip() {
        Assert.AreEqual(100,GameRules.DefaultHp);Assert.AreEqual(500,GameRules.MaxStartHp);
        var w=new WorldState();w.Tanks[0]=new TankState{Slot=0,Hp=300,MaxHp=500,FogEndTick=175,ConfusionEndTick=190,FireLockEndTick=175,QReadyTick=1129,Life=7};
        var r=new WireReader(w.Encode());var restored=WorldState.Full(r);r.End();CollectionAssert.AreEqual(w.Encode(),restored.Encode());
        Assert.AreEqual(60,SkillGeometry.HealthPercent(restored.Tanks[0].Hp,500));Assert.AreEqual(100,SkillGeometry.HealthPercent(500,500));Assert.AreEqual(25,SkillGeometry.HealthPercent(25,100));
        w.Tanks[0].MaxHp=501;Assert.Throws<InvalidDataException>(()=>w.Encode());
    }
    [Test] public void KatyushaCenterAlwaysStopsAtTheSameMapBounds() {
        foreach(float extent in new[]{60,80}) {
            Assert.AreEqual(5.1f,SkillGeometry.ClampAim(-100,extent));Assert.AreEqual(extent-5.1f,SkillGeometry.ClampAim(100,extent));Assert.AreEqual(20,SkillGeometry.ClampAim(20,extent));
        }
        CollectionAssert.AreEqual(new[]{900,977,900,900,900,1029,1029,1029},RoleplayContent.NationCooldowns);
    }
    [Test] public void KatyushaAimFreezesOutsideTheDrawnMapAndAccountsForShake() {
        float x=30,y=40;
        foreach(var pointer in new[]{new[]{99f,200f},new[]{901f,200f},new[]{200f,49f},new[]{200f,851f},new[]{-1f,-1f}}) {
            Assert.IsFalse(SkillGeometry.TryAim(pointer[0],pointer[1],100,50,10,80,80,3,-2,ref x,ref y));Assert.AreEqual(30,x);Assert.AreEqual(40,y);
        }
        Assert.IsTrue(SkillGeometry.TryAim(303,248,100,50,10,80,80,3,-2,ref x,ref y));Assert.AreEqual(20,x);Assert.AreEqual(60,y);
        // Inside panel, but in the empty strip exposed by screen shake.
        Assert.IsFalse(SkillGeometry.TryAim(101,248,100,50,10,80,80,3,-2,ref x,ref y));Assert.AreEqual(20,x);
        Assert.IsTrue(SkillGeometry.TryAim(104,51,100,50,10,80,80,3,-2,ref x,ref y));Assert.AreEqual(5.1f,x);Assert.AreEqual(74.9f,y);
    }
    [Test] public void WasdAndArrowsShareBitsSpeedSprintAndConfusion() {
        var map=new GameMap{width=80,height=80,walls=new MapRect[0],bricks=new MapRect[0],water=new MapRect[0]};
        for(int wasd=0;wasd<16;wasd++)for(int arrows=0;arrows<16;arrows++) {
            int buttons=GameplayInput.MovementButtons((wasd&1)!=0,(wasd&2)!=0,(wasd&4)!=0,(wasd&8)!=0,(arrows&1)!=0,(arrows&2)!=0,(arrows&4)!=0,(arrows&8)!=0);
            Assert.AreEqual(wasd|arrows,buttons);
            foreach(int confusion in new[]{0,300})foreach(int shift in new[]{0,32}) {
                var t=new TankState{Hp=100,X=40,Y=40,ConfusionEndTick=confusion,Turret=9000};
                Movement.Step(t,new InputRecord(100,buttons|shift,0),map,new byte[64]);
                double distance=Math.Sqrt((t.X-40)*(t.X-40)+(t.Y-40)*(t.Y-40));
                bool moving=((buttons&1)!=0)!=((buttons&2)!=0)||((buttons&4)!=0)!=((buttons&8)!=0);
                Assert.AreEqual(moving?(shift==0?.3:.45):0,distance,.00001);
                Assert.AreEqual(0,GameplayInput.Resolve(false,buttons|shift,0,9000).Buttons);
            }
        }
    }
    [Test] public void ResultsRemainReadableWhileRoomIsImmediatelyReadyForAnotherMatch() {
        using(var c=new ClientCore(null,new byte[32])) {
            c.Match=7;c.RoomId=1;c.Slot=0;c.Phase=ClientPhase.PLAYING;c.RoomMode=RoomMode.ROLEPLAY;
            c.LoadoutReady=true;c.Loadouts[0]=new MatchLoadout{Slot=0,Nation=5};
            var handle=typeof(ClientCore).GetMethod("Handle",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            handle.Invoke(c,new object[]{new Packet{Op=Op.End,Payload=new WireWriter().U64(7).U8(0).U8(1).U8(0).Str("Player").U16(3).U16(1).U32(100).U8(1).U8(0).Done()}});
            handle.Invoke(c,new object[]{new Packet{Op=Op.RoomState,Payload=new WireWriter().U32(1).Str("Room").U8(0).U8(0).U8(1).U8(0).Str("Player").U8(0).U8(2).U16(180).U8(0).U8(0).U16(100).U8(1).U8(0).U64(0).U64(7).U32(0).U8(5).Done()}});
            Assert.AreEqual(ClientPhase.RESULTS,c.Phase);Assert.AreEqual(RoomPhase.WAITING,c.RoomPhase);Assert.AreEqual(5,c.Scores[0].Nation);Assert.IsTrue(c.LoadoutReady);
            var finalWorld=new WorldState();finalWorld.Tanks[0]=new TankState{Slot=0};
            handle.Invoke(c,new object[]{new Packet{Op=Op.Full,Payload=new WireWriter().U64(7).U32(1).U32(1800).U64(0).U32(1800).U16(0).Bytes(finalWorld.Encode()).Done()}});
            Assert.AreEqual(1800,c.Latest.Tick);Assert.AreEqual(5,c.Latest.World.Tanks[0].Nation);Assert.AreEqual(ClientPhase.RESULTS,c.Phase);
            c.BackToRoom();Assert.AreEqual(ClientPhase.ROOM,c.Phase);Assert.AreEqual(1,c.RoomId);Assert.AreEqual(1,c.Scores.Count);Assert.IsFalse(c.LoadoutReady);
        }
    }
    [Test] public void JavaSkillBrickDeltaRepairsDroppedUpdateAndRejoinFullMatches() {
        var lines=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/skill-bricks.tsv"));
        var full=new WireReader(Bytes(lines[0].Split('\t')[1]));var baseline=WorldState.Full(full);full.End();
        Assert.AreEqual(5,baseline.Bricks[0]);Assert.AreEqual(5,baseline.Bricks[1]);
        // Deliberately skip DROPPED; the repair is against the last acknowledged base.
        var delta=new WireReader(Bytes(lines[2].Split('\t')[1]));var repaired=DeltaCodec.Apply(baseline,delta);delta.End();
        var rejoin=new WireReader(Bytes(lines[3].Split('\t')[1]));var restored=WorldState.Full(rejoin);rejoin.End();
        Assert.AreEqual(0,repaired.Bricks[0]);Assert.AreEqual(0,repaired.Bricks[1]);Assert.AreEqual(5,baseline.Bricks[0]);CollectionAssert.AreEqual(restored.Encode(),repaired.Encode());
    }
    [Test] public void ReversedPredictionLocksTurretButShiftAndFireLockDoNotAlterMotion() {
        var map=new GameMap{width=60,height=60,walls=new MapRect[0],bricks=new MapRect[0],water=new MapRect[0]};
        var t=new TankState{Hp=100,X=20,Y=20,Turret=9000,ConfusionEndTick=190,FireLockEndTick=200};
        Movement.Step(t,new InputRecord(189,41,18000),map,new byte[64]);Assert.Less(t.X,20);Assert.Less(t.Y,20);Assert.AreEqual(9000,t.Turret);Assert.AreEqual(149,t.Energy);
        float x=t.X;Movement.Step(t,new InputRecord(190,8,18000),map,new byte[64]);Assert.Greater(t.X,x);Assert.AreEqual(9400,t.Turret);
    }
    [Test] public void JavaFragmentedFullDeltaSurviveReorderingDuplicatesAndLostParts() {
        var all=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/skill-fragments.tsv"));
        var assembler=new SnapshotAssembler();WorldState state=null;
        foreach(string kind in new[]{"FULL","DELTA"}) {
            var packets=all.Where(line=>line.StartsWith(kind+"\t")).Select(line=>Protocol.Decode(Bytes(line.Split('\t')[1]),true)).ToArray();
            Assert.Greater(packets.Length,1);Packet complete=null;
            for(int i=packets.Length-1;i>=0;i--) {
                var result=assembler.Add(packets[i],7,kind=="FULL"?0u:1u,1000);
                if(i>0) {Assert.IsNull(result);Assert.IsNull(assembler.Add(packets[i],7,0,1000));}
                else complete=result;
            }
            Assert.IsNotNull(complete);var r=new WireReader(complete.Payload);r.U64();r.U32();r.U32();r.U64();r.U32();r.U16();
            if(kind=="FULL") {state=WorldState.Full(r);Assert.AreEqual(72,state.Effects.Count);Assert.AreEqual(500,state.Tanks[0].MaxHp);}
            else {Assert.AreEqual(1,r.U32());state=DeltaCodec.Apply(state,r);Assert.AreEqual(300,state.Tanks[0].Hp);Assert.AreEqual(0,state.Effects.Count);}
            r.End();Assert.AreEqual(128,state.Bullets.Count);
            // A new assembler missing its final fragment must never expose partial state.
            var loss=new SnapshotAssembler();for(int i=0;i<packets.Length-1;i++)Assert.IsNull(loss.Add(packets[i],7,0,1000));
            loss.Clear();foreach(var packet in packets)complete=loss.Add(packet,7,0,1000000);Assert.IsNotNull(complete);
            Assert.IsNull(loss.Add(packets[0],8,0,1000));Assert.IsNull(loss.Add(packets[0],7,2,1000));
        }
    }
    [Test] public void MalformedFragmentMetadataDoesNotAllocateUnboundedState() {
        var a=new SnapshotAssembler();
        var packet=new Packet{Op=Op.SnapshotPart,Payload=new WireWriter().U64(7).U32(1).U8(Op.Full).U8(0).U8(255).U16(32769).Done()};
        Assert.Throws<InvalidDataException>(()=>a.Add(packet,7,0,1000));
    }
}
