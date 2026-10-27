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
        Assert.AreEqual(70,GameRules.SlowSpeedPercent);Assert.AreEqual(90,GameRules.SlowDurationTicks);Assert.AreEqual(300,GameRules.SabotageCooldownPenaltyTicks);
        Assert.AreEqual(90,GameRules.BombDelayTicks);Assert.AreEqual(90,RoleplayContent.NationDurations[0]);Assert.AreEqual(4.5f,GameRules.BombTrackingSpeed);
        Assert.AreEqual(GameRules.TankSpeed*.5f,GameRules.BombTrackingSpeed);
        Assert.AreEqual(7.65f,GameRules.BombRadius);Assert.AreEqual(11.475f,GameRules.KatyushaRadius);Assert.AreEqual(50,GameRules.KatyushaSlowSpeedPercent);
        Assert.AreEqual(1029,RoleplayContent.NationCooldowns[5]);Assert.AreEqual(1029,RoleplayContent.NationCooldowns[6]);
    }
    [Test] public void BombSlowReducesGroundWaterAndSprintSpeedByThirtyPercentUntilTheExactDeadline() {
        foreach(bool wet in new[]{false,true})foreach(int buttons in new[]{8,40,41})foreach(uint tick in new uint[]{174,175}) {
            var map=new GameMap{width=60,height=60,walls=new MapRect[0],bricks=new MapRect[0],water=wet?new[]{new MapRect{x=0,y=0,w=60,h=60}}:new MapRect[0]};
            var t=new TankState{Hp=100,X=20,Y=20,SlowEndTick=175};
            Movement.Step(t,new InputRecord(tick,buttons,0),map,new byte[64]);
            double distance=Math.Sqrt((t.X-20)*(t.X-20)+(t.Y-20)*(t.Y-20));
            double expected=.3*(wet?.5:1)*((buttons&32)!=0?1.5:1)*(tick<175?.7:1);
            Assert.AreEqual(expected,distance,.00001);Assert.AreEqual((buttons&32)!=0?149:150,t.Energy);
        }
    }
    [Test] public void JavaBombSlowAndSabotageRepairMatchRejoinState() {
        var lines=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/skill-debuffs.tsv"));
        var full=new WireReader(Bytes(lines[0].Split('\t')[1]));var baseline=WorldState.Full(full);full.End();
        var delta=new WireReader(Bytes(lines[1].Split('\t')[1]));var repaired=DeltaCodec.Apply(baseline,delta);delta.End();
        var rejoin=new WireReader(Bytes(lines[2].Split('\t')[1]));var restored=WorldState.Full(rejoin);rejoin.End();
        Assert.AreEqual(190,repaired.Tanks[1].SlowEndTick);Assert.AreEqual(1300,repaired.Tanks[0].QReadyTick);Assert.AreEqual(445,repaired.Tanks[0].FireLockEndTick);
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
            Assert.AreEqual(11.475f,SkillGeometry.ClampAim(-100,extent));Assert.AreEqual(extent-11.475f,SkillGeometry.ClampAim(100,extent));Assert.AreEqual(20,SkillGeometry.ClampAim(20,extent));
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
        Assert.IsTrue(SkillGeometry.TryAim(104,51,100,50,10,80,80,3,-2,ref x,ref y));Assert.AreEqual(11.475f,x);Assert.AreEqual(68.525f,y);
    }
    [Test] public void JavaKatyushaAreaSlowMatchesPredictionAcrossTerrainSprintAndBombSlow() {
        int count=0;
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/katyusha-movement.tsv"))) {
            var f=line.Split('\t');bool wet=bool.Parse(f[0]);uint tick=uint.Parse(f[1]);int buttons=int.Parse(f[2]);
            var map=new GameMap{width=60,height=60,walls=new MapRect[0],bricks=new MapRect[0],water=wet?new[]{new MapRect{x=0,y=0,w=60,h=60}}:new MapRect[0]};
            var r=new WireReader(Bytes(f[3]));var world=WorldState.Full(r);r.End();
            var tank=world.Tanks[0];Movement.Step(tank,new InputRecord(tick,buttons,0),map,world.Bricks,world.Effects);
            r=new WireReader(Bytes(f[4]));var expected=WorldState.ReadTank(r);r.End();
            Assert.AreEqual(expected.X,tank.X,.00001);Assert.AreEqual(expected.Y,tank.Y,.00001);Assert.AreEqual(expected.Energy,tank.Energy);count++;
        }
        Assert.AreEqual(32,count);
    }
    [Test] public void ReconciliationReplaysInputsUsingConfirmedKatyushaAreas() {
        var map=new GameMap{width=60,height=60,walls=new MapRect[0],bricks=new MapRect[0],water=new MapRect[0]};
        using(var client=new ClientCore(map,new byte[32])) {
            client.Slot=0;var world=new WorldState();world.Tanks[0]=new TankState{Slot=0,Hp=100,X=20,Y=20};
            world.Effects.Add(new SkillEffect{Id=1,Kind=SkillEffect.Area,Start=100,End=200,X=20,Y=20,Radius=GameRules.KatyushaRadius});
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var history=(System.Collections.Generic.SortedDictionary<uint,InputRecord>)typeof(ClientCore).GetField("history",flags).GetValue(client);
            history[101]=new InputRecord(101,8,0);
            typeof(ClientCore).GetField("targetTick",flags).SetValue(client,101u);
            var reconcile=typeof(ClientCore).GetMethod("Reconcile",flags);
            var snapshot=new Snapshot{Tick=100,Resolved=100,World=world};reconcile.Invoke(client,new object[]{snapshot});
            Assert.AreEqual(20.15,client.Predicted.X,.00001);
            world.Effects.Clear();reconcile.Invoke(client,new object[]{snapshot});Assert.AreEqual(20.3,client.Predicted.X,.00001);
        }
    }
    [Test] public void KatyushaSlowTracksRotatedBodyBoundaryAndExactDeadline() {
        var effects=new[]{new SkillEffect{Id=1,Kind=SkillEffect.Area,Start=100,End=200,X=20,Y=20,Radius=GameRules.KatyushaRadius}};
        var tank=new TankState{Hp=100,X=20+GameRules.KatyushaRadius+GameRules.TankHalfSize*(float)Math.Sqrt(2)-.01f,Y=20,Body=4500};
        Assert.AreEqual(200,SkillGeometry.KatyushaSlowEnd(tank,100,effects));tank.X+=.02f;Assert.AreEqual(0,SkillGeometry.KatyushaSlowEnd(tank,100,effects));
        tank.X=20;Assert.AreEqual(0,SkillGeometry.KatyushaSlowEnd(tank,99,effects));Assert.AreEqual(0,SkillGeometry.KatyushaSlowEnd(tank,200,effects));
        tank.Hp=0;Assert.AreEqual(0,SkillGeometry.KatyushaSlowEnd(tank,100,effects));
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
        Assert.AreEqual(3,baseline.Bricks[0]);Assert.AreEqual(3,baseline.Bricks[1]);
        // Deliberately skip DROPPED; the repair is against the last acknowledged base.
        var delta=new WireReader(Bytes(lines[2].Split('\t')[1]));var repaired=DeltaCodec.Apply(baseline,delta);delta.End();
        var rejoin=new WireReader(Bytes(lines[3].Split('\t')[1]));var restored=WorldState.Full(rejoin);rejoin.End();
        Assert.AreEqual(0,repaired.Bricks[0]);Assert.AreEqual(0,repaired.Bricks[1]);Assert.AreEqual(3,baseline.Bricks[0]);CollectionAssert.AreEqual(restored.Encode(),repaired.Encode());
    }
    [Test] public void SlowSprintKeepsThirtyPercentReductionUntilEnergyRunsOut() {
        var map=new GameMap{width=60,height=60,walls=new MapRect[0],bricks=new MapRect[0],water=new MapRect[0]};
        var t=new TankState{Hp=100,X=20,Y=20,SlowEndTick=190,Energy=1};
        Movement.Step(t,new InputRecord(101,40,0),map,new byte[64]);
        Assert.AreEqual(9.45,(t.X-20)*30,.0001);Assert.AreEqual(2.1,(t.X-20)*30/GameRules.BombTrackingSpeed,.0001);Assert.AreEqual(0,t.Energy);
        float x=t.X;Movement.Step(t,new InputRecord(102,40,0),map,new byte[64]);Assert.AreEqual(6.3,(t.X-x)*30,.0001);
    }
    [Test] public void JavaMovingBombKeepsItsIdentityAndRepairsCoordinatesAcrossLossAndRejoin() {
        var lines=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/bomb-tracking.tsv"));
        var r=new WireReader(Bytes(lines[0].Split('\t')[1]));var baseline=WorldState.Full(r);r.End();
        var original=baseline.Effects.Single(e=>e.Kind==SkillEffect.Bomb);Assert.AreEqual(14,original.X);
        r=new WireReader(Bytes(lines[2].Split('\t')[1]));var repaired=DeltaCodec.Apply(baseline,r);r.End();
        var moving=repaired.Effects.Single(e=>e.Kind==SkillEffect.Bomb);Assert.AreEqual(original.Id,moving.Id);Assert.AreEqual(100,moving.Start);Assert.AreEqual(190,moving.End);
        Assert.AreEqual(18.5,moving.X,.0001);Assert.AreEqual(10,moving.Y);Assert.AreEqual(190,repaired.Tanks[1].SlowEndTick);Assert.AreEqual(14,original.X);
        r=new WireReader(Bytes(lines[3].Split('\t')[1]));var restored=WorldState.Full(r);r.End();CollectionAssert.AreEqual(repaired.Encode(),restored.Encode());
    }
    [Test] public void JavaTargetedRocketsRepairTheirLockedPointsAfterLossAndRejoinWithoutFollowingTheTank() {
        var lines=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/katyusha-targeting.tsv"));
        var r=new WireReader(Bytes(lines[0].Split('\t')[1]));var baseline=WorldState.Full(r);r.End();
        var originals=baseline.Effects.Where(e=>e.Kind==SkillEffect.Rocket).OrderBy(e=>e.Start).ToArray();Assert.AreEqual(9,originals.Length);
        // Skip the snapshot that first published a targeted point; repair from the acknowledged base.
        r=new WireReader(Bytes(lines[2].Split('\t')[1]));var repaired=DeltaCodec.Apply(baseline,r);r.End();
        var rockets=repaired.Effects.Where(e=>e.Kind==SkillEffect.Rocket).OrderBy(e=>e.Start).ToArray();
        Assert.AreEqual(originals[0].Id,rockets[0].Id);Assert.AreEqual(109,rockets[0].Start);Assert.AreEqual(124,rockets[0].End);
        Assert.AreEqual(28,rockets[0].X);Assert.AreEqual(30,rockets[0].Y);
        Assert.AreEqual(34,repaired.Tanks[1].X);Assert.AreEqual(34,rockets[1].X);Assert.AreEqual(30,rockets[1].Y);
        Assert.AreEqual(GameRules.RocketRadius,rockets[0].Radius);
        r=new WireReader(Bytes(lines[3].Split('\t')[1]));var restored=WorldState.Full(r);r.End();CollectionAssert.AreEqual(repaired.Encode(),restored.Encode());
    }
    [Test] public void JavaRocketSlowPredictionMultipliesEveryCurrentSpeedFactorAndStopsAtTheExactDeadline() {
        var dry=new GameMap{width=60,height=60,walls=new MapRect[0],bricks=new MapRect[0],water=new MapRect[0]};
        var wet=new GameMap{width=60,height=60,walls=new MapRect[0],bricks=new MapRect[0],water=new[]{new MapRect{x=0,y=0,w=60,h=60}}};
        int count=0;
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/rocket-slow-movement.tsv"))) {
            var fields=line.Split('\t');uint tick=uint.Parse(fields[0]);int buttons=int.Parse(fields[1]);
            var r=new WireReader(Bytes(fields[2]));var before=WorldState.Full(r);r.End();
            r=new WireReader(Bytes(fields[3]));var expected=WorldState.ReadTank(r);r.End();
            var actual=before.Tanks[0].Copy();Movement.Step(actual,new InputRecord(tick,buttons,0),count<64?dry:wet,before.Bricks,before.Effects);
            Assert.AreEqual(expected.X,actual.X,.00001);Assert.AreEqual(expected.Y,actual.Y,.00001);Assert.AreEqual(expected.Energy,actual.Energy);
            count++;
        }
        Assert.AreEqual(128,count);
    }
    [Test] public void JavaRocketSlowStacksRepairAfterLossAndRejoinAndClearAtAreaExpiry() {
        var lines=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/rocket-slow-state.tsv"));
        var r=new WireReader(Bytes(lines[0].Split('\t')[1]));var baseline=WorldState.Full(r);r.End();Assert.AreEqual(1,baseline.Tanks[1].RocketSlowStacks);
        r=new WireReader(Bytes(lines[2].Split('\t')[1]));var repaired=DeltaCodec.Apply(baseline,r);r.End();
        Assert.AreEqual(4,repaired.Tanks[1].RocketSlowStacks);Assert.AreEqual(214,repaired.Tanks[1].RocketSlowEndTick);Assert.AreEqual(1,baseline.Tanks[1].RocketSlowStacks);
        r=new WireReader(Bytes(lines[3].Split('\t')[1]));var restored=WorldState.Full(r);r.End();CollectionAssert.AreEqual(repaired.Encode(),restored.Encode());
        var tank=repaired.Tanks[1].Copy();tank.X=tank.Y=50;Movement.Step(tank,new InputRecord(200,8,0),new GameMap{width=60,height=60,walls=new MapRect[0],bricks=new MapRect[0],water=new MapRect[0]},repaired.Bricks,repaired.Effects);
        Assert.AreEqual(.3*Math.Pow(.9,4),tank.X-50,.00001);
        r=new WireReader(Bytes(lines[4].Split('\t')[1]));var expired=WorldState.Full(r);r.End();Assert.AreEqual(0,expired.Tanks[1].RocketSlowStacks);
        var invalid=expired.Copy();invalid.Tanks[1].RocketSlowStacks=10;Assert.Throws<InvalidDataException>(()=>invalid.Encode());
    }
    [Test] public void JavaBrickStagesKeepCollisionAndRepairAgainstTheSameAcknowledgedBase() {
        var lines=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/brick-stages.tsv"));
        var r=new WireReader(Bytes(lines[0].Split('\t')[1]));var baseline=WorldState.Full(r);r.End();Assert.AreEqual(3,baseline.Bricks[0]);
        var wall=new MapRect{id=0,x=10,y=10,w=2,h=2};var map=new GameMap{width=60,height=60,walls=new MapRect[0],bricks=new[]{wall},water=new MapRect[0]};
        WorldState current=baseline;
        foreach(string line in lines.Where(line=>line.StartsWith("STAGE\t"))) {
            var fields=line.Split('\t');int hp=int.Parse(fields[1]);r=new WireReader(Bytes(fields[2]));current=DeltaCodec.Apply(baseline,r);r.End();
            Assert.AreEqual(hp,current.Bricks[0]);Assert.AreEqual(hp>0,map.Blocked(11,11,current.Bricks));CollectionAssert.AreEqual(Bytes(fields[3]),current.Encode());Assert.AreEqual(3,baseline.Bricks[0]);
        }
        r=new WireReader(Bytes(lines.Last().Split('\t')[1]));var restored=WorldState.Full(r);r.End();CollectionAssert.AreEqual(current.Encode(),restored.Encode());
        var invalid=new WorldState();invalid.Bricks[0]=4;Assert.Throws<InvalidDataException>(()=>WorldState.Full(new WireReader(invalid.Encode())));
        var delta=new WireWriter().U8(0).U8(0).U8(0).U8(1).U8(0).U8(4).Done();
        Assert.Throws<InvalidDataException>(()=>DeltaCodec.Apply(baseline,new WireReader(delta)));Assert.AreEqual(3,baseline.Bricks[0]);
    }
    [Test] public void BrickShapesVisiblyLoseOneThirdAtEachHit() {
        var pixels=typeof(ArenaRenderer).GetMethod("BrickPixels",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var full=(UnityEngine.Color[])pixels.Invoke(null,new object[]{3});
        var twoThirds=(UnityEngine.Color[])pixels.Invoke(null,new object[]{2});
        var oneThird=(UnityEngine.Color[])pixels.Invoke(null,new object[]{1});
        Assert.IsTrue(full.All(c=>c.a==1));
        Assert.That(twoThirds.Count(c=>c.a>0)/(double)full.Length,Is.InRange(.65,.68));
        Assert.That(oneThird.Count(c=>c.a>0)/(double)full.Length,Is.InRange(.32,.35));
        for(int i=0;i<full.Length;i++)if(twoThirds[i].a==0)Assert.AreEqual(0,oneThird[i].a);
        Assert.Greater(oneThird[0].a,0);Assert.AreEqual(0,twoThirds.Last().a);
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
