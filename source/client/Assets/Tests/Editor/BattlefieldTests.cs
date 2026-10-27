using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tank;
using UnityEngine;

public class BattlefieldTests {
    GameMap Map(string name="map-large.json") {return JsonUtility.FromJson<GameMap>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,name)));}
    int[] Targets(int kind=BattlefieldWave.Fire,int target=0) {
        int first=kind==BattlefieldWave.Fire?0:16;
        int count=GameRules.BattlefieldTargetsPerRound;
        var ids=Enumerable.Range(first,2*count).Concat(Enumerable.Range(first,count)).ToArray();
        if(kind==BattlefieldWave.Fire)for(int i=0;i<count;i++)ids[i]=target<count?(target+i)%count:i==0?target:i;
        return ids;
    }
    BattlefieldWave Wave(int kind=BattlefieldWave.Fire,uint id=1,uint start=0,int target=0) {
        uint impact=start+(uint)(kind==BattlefieldWave.Fire?GameRules.BattlefieldFireWarningTicks:GameRules.BattlefieldWarningTicks);return new BattlefieldWave(id,kind,start,impact,impact+(uint)GameRules.BattlefieldEventTicks,Targets(kind,target));
    }
    byte[] Bytes(string hex) {return Enumerable.Range(0,hex.Length/2).Select(i=>Convert.ToByte(hex.Substring(i*2,2),16)).ToArray();}
    [Test] public void BothMapsValidateAndRejectSpawnWaterDuplicateAndMissingTargets() {
        foreach(string name in new[]{"map.json","map-large.json"}) {
            var map=Map(name);Assert.DoesNotThrow(()=>map.battlefield.Validate(map));Assert.GreaterOrEqual(map.battlefield.fireZones.Length,6);
            map.battlefield.fireZones[0].x=map.spawns[0].x;map.battlefield.fireZones[0].y=map.spawns[0].y;Assert.Throws<InvalidDataException>(()=>map.battlefield.Validate(map));
        }
        var m=Map();m.battlefield.airstrikeZones=null;Assert.Throws<InvalidDataException>(()=>m.battlefield.Validate(m));
        m=Map();m.battlefield.fireZones[1].id=0;Assert.Throws<InvalidDataException>(()=>m.battlefield.Validate(m));
        m=Map();m.battlefield.fireZones[0].x=43;m.battlefield.fireZones[0].y=16;Assert.Throws<InvalidDataException>(()=>m.battlefield.Validate(m));
    }
    [TestCase("battlefield.tsv")]
    [TestCase("battlefield-bombstorm.tsv")]
    public void JavaFullDeltaDroppedUpdateAndRejoinPreserveEveryByte(string fixture) {
        WorldState world=null;bool sawFire=false;
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/"+fixture))) {
            var f=line.Split('\t');if(f[0]=="DROPPED")continue;
            var r=new WireReader(Bytes(f[1]));var previous=world;byte[] before=previous==null?null:previous.Encode();
            world=f[0]=="FULL"||f[0]=="REJOIN"?WorldState.Full(r):DeltaCodec.Apply(world,r);r.End();
            CollectionAssert.AreEqual(Bytes(f[0]=="DELTA"?f[2]:f[1]),world.Encode());
            if(world.Battlefield!=null) {sawFire=true;Assert.AreEqual(5,world.Battlefield.TargetsPerRound);world.Battlefield.Validate(Map(),world.Battlefield.Impact);Assert.AreSame(world.Battlefield,world.Copy().Battlefield);}
            if(previous!=null)CollectionAssert.AreEqual(before,previous.Encode());
        }
        Assert.IsTrue(sawFire);Assert.IsNull(world.Battlefield);
    }
    [Test] public void WarningFireAndEndFollowAuthoritativeTickAndRestoreMidFire() {
        var wave=Wave();Assert.IsTrue(BattlefieldPresentation.Warning(wave,wave.Impact-1));Assert.IsFalse(BattlefieldPresentation.Burning(wave,wave.Impact-1));
        Assert.IsFalse(BattlefieldPresentation.Warning(wave,wave.Impact));Assert.IsTrue(BattlefieldPresentation.Burning(wave,wave.Impact));Assert.IsTrue(BattlefieldPresentation.Burning(wave,wave.End-1));Assert.IsFalse(BattlefieldPresentation.Burning(wave,wave.End));
        wave.Validate(Map(),100);Assert.Throws<InvalidDataException>(()=>wave.Validate(Map(),wave.End));
    }
    [Test] public void LaterRoundWarningsAndFireRestoreAtTheirOwnPositionsAndStopOnTime() {
        var wave=Wave();Assert.AreEqual(3,wave.Rounds);
        Assert.IsFalse(BattlefieldPresentation.Warning(wave,wave.RoundStart(1)-1,1));Assert.IsTrue(BattlefieldPresentation.Warning(wave,wave.RoundStart(1),1));
        Assert.IsTrue(BattlefieldPresentation.Warning(wave,wave.RoundImpact(1)-1,1));Assert.IsFalse(BattlefieldPresentation.Warning(wave,wave.RoundImpact(1),1));
        Assert.IsTrue(BattlefieldPresentation.Burning(wave,wave.RoundImpact(1)-1,0));Assert.IsFalse(BattlefieldPresentation.Burning(wave,wave.RoundImpact(1),0));
        Assert.IsTrue(BattlefieldPresentation.Burning(wave,wave.RoundImpact(1),1));Assert.IsTrue(BattlefieldPresentation.Burning(wave,wave.RoundImpact(2)-1,1));
        Assert.IsFalse(BattlefieldPresentation.Burning(wave,wave.RoundImpact(2),1));Assert.IsTrue(BattlefieldPresentation.Burning(wave,wave.RoundImpact(2),2));
        Assert.IsFalse(BattlefieldPresentation.Burning(wave,wave.End));wave.Validate(Map(),wave.Impact);
    }
    [Test] public void RenamedEventsAndTheirNoticesExpireWithTheLastEffectEvenWithAnOldSnapshot() {
        Assert.AreEqual("Red Zone",BattlefieldPresentation.Name(BattlefieldWave.Fire));Assert.AreEqual("Bombstorm",BattlefieldPresentation.Name(BattlefieldWave.Airstrike));
        var fire=Wave();Assert.AreEqual(300,fire.End-fire.Start);Assert.IsTrue(BattlefieldPresentation.Active(fire,fire.End-.1f));Assert.IsTrue(BattlefieldPresentation.Burning(fire,fire.End-.1f));
        Assert.IsFalse(BattlefieldPresentation.Active(fire,fire.End));Assert.IsFalse(BattlefieldPresentation.Burning(fire,fire.End));Assert.IsFalse(BattlefieldPresentation.Warning(fire,fire.End));
        var air=Wave(BattlefieldWave.Airstrike);Assert.AreEqual(air.End,air.RoundImpact(air.Rounds-1)+GameRules.BattlefieldAirstrikeTicks);
        Assert.IsTrue(BattlefieldPresentation.Warning(air,air.RoundStart(1),1));Assert.IsTrue(BattlefieldPresentation.Warning(air,air.RoundImpact(1)-.1f,1));Assert.IsFalse(BattlefieldPresentation.Warning(air,air.RoundImpact(1),1));
        Assert.IsTrue(BattlefieldPresentation.Active(air,air.End-.1f));Assert.IsFalse(BattlefieldPresentation.Active(air,air.End));
        var map=Map();var p=new CombatPresentation();p.Accept(9,new Snapshot{Tick=1,World=new WorldState()},0,map);
        var world=new WorldState{Battlefield=air};p.Accept(9,new Snapshot{Tick=air.RoundImpact(2),World=world},0,map);
        p.Update(GameRules.BattlefieldAirstrikeTicks/30f-.01f,world,map,0);Assert.AreEqual(5,p.Transients.Count(f=>f.Active&&f.Clip=="combat/explosion_top"));
        p.Update(.02f,world,map,0);Assert.AreEqual(0,p.ActiveCount);
    }
    [Test] public void BombstormSeparatesTargetsByRoundAndRejectsRepeatedOrOverlappingAdjacentRegions() {
        var wave=Wave(BattlefieldWave.Airstrike);wave.Validate(Map(),1);
        Assert.AreEqual(5,wave.TargetsPerRound);Assert.AreEqual(21,wave.Target(1,0));Assert.AreEqual(16,wave.Target(2,0));
        var repeated=Targets(BattlefieldWave.Airstrike);repeated[5]=repeated[0];Assert.Throws<InvalidDataException>(()=>new BattlefieldWave(1,3,0,60,300,repeated));
        var duplicate=Targets(BattlefieldWave.Airstrike);duplicate[1]=duplicate[0];Assert.Throws<InvalidDataException>(()=>new BattlefieldWave(1,3,0,60,300,duplicate));
        var close=Targets(BattlefieldWave.Airstrike);close[5]=10;var overlapping=new BattlefieldWave(1,3,0,60,300,close);Assert.Throws<InvalidDataException>(()=>overlapping.Validate(Map(),1));
    }
    [Test] public void BothEventsRejectAnythingOtherThanFiveUniqueRegionsPerRound() {
        foreach(int kind in new[]{BattlefieldWave.Fire,BattlefieldWave.Airstrike}) {
            var wave=Wave(kind);Assert.AreEqual(15,wave.Targets.Count);Assert.AreEqual(5,wave.TargetsPerRound);wave.Validate(Map(),1);
            foreach(int count in new[]{4,6})Assert.Throws<InvalidDataException>(()=>new BattlefieldWave(1,kind,0,60,300,Enumerable.Range(0,count*3).ToArray()));
            var duplicate=Targets(kind);duplicate[1]=duplicate[0];Assert.Throws<InvalidDataException>(()=>new BattlefieldWave(1,kind,0,60,300,duplicate));
        }
    }
    [Test] public void InvalidShapesAndReferencesAreRejectedAndTargetsCannotBeMutated() {
        Assert.Throws<InvalidDataException>(()=>new BattlefieldWave(1,1,0,60,300,new[]{0}));
        var legacy=new WireWriter().U8(1).U32(1).U8(1).U32(0).U32(60).U32(300).U8(1).U8(0).Done();
        Assert.Throws<InvalidDataException>(()=>BattlefieldCodec.Read(new WorldState(),new WireReader(legacy)));
        Assert.Throws<InvalidDataException>(()=>new BattlefieldWave(1,3,0,60,300,new[]{0,0}));
        Assert.Throws<InvalidDataException>(()=>new BattlefieldWave(1,2,0,60,300,new[]{0}));
        Assert.Throws<InvalidDataException>(()=>new BattlefieldWave(1,4,0,60,300,new[]{0}));
        Assert.Throws<InvalidDataException>(()=>new BattlefieldWave(1,2,0,61,301,new[]{0}));
        var ids=Targets();var wave=new BattlefieldWave(1,2,0,60,300,ids);ids[0]=63;Assert.AreEqual(0,wave.Targets[0]);
        Assert.Throws<InvalidDataException>(()=>Wave(target:63).Validate(Map(),1));
        Assert.Throws<InvalidDataException>(()=>Wave(start:10).Validate(Map(),1));
    }
    [Test] public void MalformedDeltaLeavesBaseUnchanged() {
        var world=new WorldState{Battlefield=Wave()};byte[] before=world.Encode();
        // Use the Java-produced valid delta, then corrupt only the final Battlefield presence byte.
        var lines=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/battlefield.tsv"));
        var initial=new WireReader(Bytes(lines[0].Split('\t')[1]));world=WorldState.Full(initial);before=world.Encode();
        var bad=Bytes(lines[1].Split('\t')[1]);bad[bad.Length-34]=2; // presence immediately before the fifteen-target wave
        Assert.Throws<InvalidDataException>(()=>DeltaCodec.Apply(world,new WireReader(bad)));CollectionAssert.AreEqual(before,world.Encode());
    }
    [Test] public void AirstrikeUsesItsOwnExplosionOnceAndReconnectDoesNotReplayIt() {
        var map=Map();var world=new WorldState();var p=new CombatPresentation();
        p.Accept(9,new Snapshot{Tick=1,World=world},0,map);world.Battlefield=Wave(BattlefieldWave.Airstrike);
        p.Accept(9,new Snapshot{Tick=world.Battlefield.Impact,World=world},0,map);Assert.AreEqual(5,p.Transients.Count(f=>f.Active&&f.Clip=="combat/explosion_top"));
        var fx=p.Transients.First(f=>f.Active&&f.Clip=="combat/explosion_top");Assert.IsTrue(fx.OriginalSize);Assert.AreEqual(10,fx.Width);
        p.Accept(9,new Snapshot{Tick=world.Battlefield.Impact+1,World=world},0,map);Assert.AreEqual(5,p.Transients.Count(f=>f.Active&&f.Clip=="combat/explosion_top"));
        var rejoin=new CombatPresentation();rejoin.Accept(9,new Snapshot{Tick=world.Battlefield.Impact+1,World=world},0,map);Assert.AreEqual(0,rejoin.ActiveCount);
        var death=new CombatEvent(1,90,CombatEvent.Death,255,0,0,1,0,65535,0,10,10,0,100,BattlefieldWave.AirstrikeCause,1);Assert.IsTrue(death.CauseOwnsExplosion);
        var fire=new CombatEvent(2,90,CombatEvent.Death,255,0,0,1,0,65535,0,10,10,0,100,BattlefieldWave.FireCause,1);Assert.IsFalse(fire.CauseOwnsExplosion);
        p.Clear();Assert.AreEqual(0,p.ActiveCount);Assert.AreEqual(0,p.LedgerCount);
    }
    [Test] public void FogSuppressesUnseenExplosionWithoutReplayingWhenVisionReturns() {
        var map=Map();var world=new WorldState();world.Tanks[0]=new TankState{Slot=0,Life=1,Hp=100,X=9,Y=6,FogEndTick=100};
        var p=new CombatPresentation();p.Accept(9,new Snapshot{Tick=1,World=world},0,map);world.Battlefield=Wave(BattlefieldWave.Airstrike);
        p.Accept(9,new Snapshot{Tick=world.Battlefield.Impact,World=world},0,map);Assert.AreEqual(0,p.ActiveCount);
        world.Tanks[0].FogEndTick=0;p.Accept(9,new Snapshot{Tick=world.Battlefield.Impact+1,World=world},0,map);Assert.AreEqual(0,p.ActiveCount);
    }
    [Test] public void LaterAirstrikesPlayOnceAndReconnectOnlyPlaysFutureRounds() {
        var map=Map();var world=new WorldState();var p=new CombatPresentation();p.Accept(9,new Snapshot{Tick=1,World=world},0,map);
        world.Battlefield=Wave(BattlefieldWave.Airstrike);
        foreach(int round in new[]{0,1,2}) {
            uint tick=world.Battlefield.RoundImpact(round);
            p.Accept(9,new Snapshot{Tick=tick,World=world},0,map);Assert.AreEqual(5,p.Transients.Count(f=>f.Active&&f.Clip=="combat/explosion_top"));
            var zone=map.battlefield.Zone(BattlefieldWave.Airstrike,world.Battlefield.Target(round,0));
            var blast=p.Transients.First(f=>f.Active&&f.Clip=="combat/explosion_top");Assert.AreEqual(zone.x,blast.X);Assert.AreEqual(zone.y,blast.Y);
            p.Accept(9,new Snapshot{Tick=tick+1,World=world},0,map);Assert.AreEqual(5,p.Transients.Count(f=>f.Active&&f.Clip=="combat/explosion_top"));p.Update(1,world,map,0);
        }
        var rejoin=new CombatPresentation();rejoin.Accept(9,new Snapshot{Tick=world.Battlefield.RoundImpact(1)+20,World=world},0,map);Assert.AreEqual(0,rejoin.ActiveCount);
        rejoin.Accept(9,new Snapshot{Tick=world.Battlefield.RoundImpact(2),World=world},0,map);Assert.AreEqual(5,rejoin.Transients.Count(f=>f.Active&&f.Clip=="combat/explosion_top"));
        uint end=world.Battlefield.End;rejoin.Update(1,world,map,0);world.Battlefield=null;rejoin.Accept(9,new Snapshot{Tick=end,World=world},0,map);Assert.AreEqual(0,rejoin.ActiveCount);
    }
    [Test] public void InvalidBattlefieldSnapshotCannotChangeLatestOrAck() {
        using(var client=new ClientCore(Map(),new byte[32])) {
            client.Match=9;client.Slot=0;client.Phase=ClientPhase.PLAYING;
            var handle=typeof(ClientCore).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic);
            Action<WorldState,uint,uint> send=(w,id,tick)=> {
                byte[] payload=new WireWriter().U64(9).U32(id).U32(tick).U64(tick*33333).U32(tick).U16(100).Bytes(w.Encode()).Done();
                handle.Invoke(client,new object[]{new Packet{Op=Op.Full,Sequence=id,Payload=payload}});
            };
            var world=new WorldState{Battlefield=Wave()};send(world,1,1);var before=client.Latest;
            world.Battlefield=Wave(target:63);Assert.Throws<TargetInvocationException>(()=>send(world,2,2));Assert.AreSame(before,client.Latest);Assert.AreEqual(1,client.LastSnapshot);
            world.Battlefield=Wave(target:1);Assert.Throws<TargetInvocationException>(()=>send(world,2,2));Assert.AreSame(before,client.Latest);
        }
    }
}
