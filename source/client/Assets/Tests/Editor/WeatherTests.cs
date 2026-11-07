using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Tank;

public class WeatherTests {
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    BattlefieldWave Wave(int kind=BattlefieldWave.Snowstorm) {return new BattlefieldWave(1,kind,300,390,750,new int[0]);}
    GameMap Map(bool wet=false) {return new GameMap{mapId=4,width=100,height=100,walls=new MapRect[0],bricks=new MapRect[0],water=wet?new[]{new MapRect{x=1,y=1,w=98,h=98}}:new MapRect[0]};}
    byte[] Bytes(string hex) {var b=new byte[hex.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(hex.Substring(i*2,2),16);return b;}
    float SingleBits(string bits) {return BitConverter.ToSingle(BitConverter.GetBytes(int.Parse(bits,CultureInfo.InvariantCulture)),0);}
    int SingleBits(float value) {return BitConverter.ToInt32(BitConverter.GetBytes(value),0);}
    [Test] public void JavaSnowMovementMatchesAcrossTheMapOnIceWithShiftAndAllSlows() {
        int count=0;
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/weather-movement.tsv"))) {
            var f=line.Split('\t');var t=new TankState{Hp=100,X=SingleBits(f[9]),Y=SingleBits(f[10]),Energy=150,SlowEndTick=f[2]=="1"?800:0,RocketSlowEndTick=800,RocketSlowStacks=int.Parse(f[3])};
            Movement.Step(t,new InputRecord(uint.Parse(f[4]),int.Parse(f[1]),0),Map(f[0]=="1"),new byte[64],wave:Wave());
            Assert.AreEqual(float.Parse(f[5],CultureInfo.InvariantCulture),t.X,.00001,line);Assert.AreEqual(int.Parse(f[6]),t.Energy);count++;
        }
        Assert.AreEqual(640,count);
    }
    [Test] public void JavaFullMapWeatherMatchesWarningActiveEdgesAndMapBounds() {
        int count=0;
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/weather-region.tsv"))) {
            var f=line.Split('\t');var map=Map();map.width=float.Parse(f[0],CultureInfo.InvariantCulture);map.height=float.Parse(f[1],CultureInfo.InvariantCulture);
            var wave=Wave(int.Parse(f[2]));float tick=float.Parse(f[3],CultureInfo.InvariantCulture),left,right;
            bool active=WeatherRegion.Bounds(map,wave,tick,out left,out right);
            Assert.AreEqual(tick>=390&&tick<750,active,line);
            if(active) {Assert.AreEqual(int.Parse(f[9]),SingleBits(left),line);Assert.AreEqual(int.Parse(f[10]),SingleBits(right),line);}
            Assert.AreEqual(f[8]=="1",WeatherRegion.Contains(map,wave,tick,SingleBits(f[11]),SingleBits(f[12])),line);count++;
        }
        Assert.AreEqual(1728,count);
    }
    [Test] public void BothStormsCoverEveryMapCornerForTheirEntireActiveLifetime() {
        var map=Map();float left,right;
        foreach(int kind in new[]{4,5})foreach(uint tick in new uint[]{389,390,391,570,749,750}) {
            var wave=Wave(kind);bool active=tick>=390&&tick<750;
            Assert.AreEqual(active,WeatherRegion.Bounds(map,wave,tick,out left,out right));
            if(active) {Assert.AreEqual(0,left);Assert.AreEqual(map.width,right);}
            float maxX=BitConverter.ToSingle(BitConverter.GetBytes(SingleBits(map.width)-1),0),maxY=BitConverter.ToSingle(BitConverter.GetBytes(SingleBits(map.height)-1),0);
            foreach(float x in new[]{0,map.width/2,maxX})foreach(float y in new[]{0,map.height/2,maxY})
                Assert.AreEqual(active,WeatherRegion.Contains(map,wave,tick,x,y),kind+" / "+tick+" / "+x+","+y);
            Assert.IsFalse(WeatherRegion.Contains(map,wave,tick,-float.Epsilon,0));
            Assert.IsFalse(WeatherRegion.Contains(map,wave,tick,0,-float.Epsilon));
            Assert.IsFalse(WeatherRegion.Contains(map,wave,tick,map.width,0));
            Assert.IsFalse(WeatherRegion.Contains(map,wave,tick,0,map.height));
        }
    }
    [Test] public void SnowSlowsEveryPositionOnlyWhileActiveAndCombinesWithIce() {
        var map=Map();var wave=Wave();TankState t=null;
        foreach(float x in new[]{2,50,98})foreach(float y in new[]{2,50,98}) {
            t=new TankState{Hp=100,X=x,Y=y};
            Movement.Step(t,new InputRecord(389,4,18000),map,new byte[64],wave:wave);Assert.AreEqual(x-.3f,t.X,.00001);
            Movement.Step(t,new InputRecord(390,4,18000),map,new byte[64],wave:wave);Assert.AreEqual(x-.45f,t.X,.00001);
            Movement.Step(t,new InputRecord(749,4,18000),map,new byte[64],wave:wave);Assert.AreEqual(x-.6f,t.X,.00001);
            Movement.Step(t,new InputRecord(750,4,18000),map,new byte[64],wave:wave);Assert.AreEqual(x-.9f,t.X,.00001);
        }
        var ice=Map(true);t.X=50;
        Movement.Step(t,new InputRecord(570,8,0),ice,new byte[64],wave:wave);Assert.AreEqual(50.225,t.X,.00001);
        Assert.AreEqual(1,WeatherRegion.Speed(map,Wave(4),570,50,20));
    }
    [Test] public void SnowReconciliationReplaysGlobalStartAndExpiryBoundaries() {
        foreach(uint tick in new uint[]{388,389,569,748,749,750})using(var c=new ClientCore(Map(),new byte[32])) {
            c.Slot=0;c.Predicted=new TankState{Hp=100,X=99,Y=99};
            var world=new WorldState{Battlefield=tick<750?Wave():null};world.Tanks[0]=new TankState{Slot=0,Hp=100,X=tick==569?33.6f:20,Y=20};
            var history=(SortedDictionary<uint,InputRecord>)typeof(ClientCore).GetField("history",Private).GetValue(c);
            int buttons=tick==569?4:8;
            history[tick+1]=new InputRecord(tick+1,buttons,0);history[tick+2]=new InputRecord(tick+2,buttons,0);
            typeof(ClientCore).GetField("targetTick",Private).SetValue(c,tick+2);
            typeof(ClientCore).GetMethod("Reconcile",Private).Invoke(c,new object[]{new Snapshot{Tick=tick,Resolved=tick,World=world}});
            Assert.AreEqual(tick==569?33.3f:tick==388||tick==748?20.45f:tick==389?20.3f:20.6f,c.Predicted.X,.00001,"snapshot "+tick);
        }
    }
    [Test] public void InputGenerationUsesTheAuthoritativeSnowWave() {
        using(var c=new ClientCore(Map(),new byte[32])) {
            typeof(ClientCore).GetField("token",Private).SetValue(c,new byte[16]);
            c.Phase=ClientPhase.PLAYING;c.Slot=0;c.LastArrivalUs=ClientCore.NowUs;
            c.Latest=new Snapshot{Tick=569,Resolved=569,World=new WorldState{Battlefield=Wave()}};
            c.Predicted=new TankState{Slot=0,Hp=100,X=50,Y=20};c.Latest.World.Tanks[0]=c.Predicted.Copy();
            Assert.IsTrue(c.GenerateInput(8,0));Assert.AreEqual(50.15,c.Predicted.X,.00001);
        }
    }
    [Test] public void JavaWeatherFullDeltaLossAndRejoinPreserveBytesCausesAndZeroTargets() {
        MapCatalog.Load(Application.streamingAssetsPath);WorldState baseline=null;int records=0;
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/weather-state.tsv"))) {
            var f=line.Split('\t');int kind=int.Parse(f[0]);uint tick=uint.Parse(f[1]);var reader=new WireReader(Bytes(f[3]));
            var actual=f[2]=="FULL"||f[2]=="REJOIN"?WorldState.Full(reader):DeltaCodec.Apply(baseline,reader);reader.End();
            if(f.Length==5)CollectionAssert.AreEqual(Bytes(f[4]),actual.Encode());
            if(actual.Battlefield!=null) {Assert.AreEqual(0,actual.Battlefield.Targets.Count);actual.Battlefield.Validate(MapCatalog.Find(kind==4?3:4).Map,tick);}
            foreach(var e in actual.Combat.Values)Assert.IsTrue(e.CauseKind==13||e.CauseKind==14);
            if(f[2]!="DROP")baseline=actual;records++;
        }
        Assert.AreEqual(26,records);
    }
    [Test] public void SandNeverHidesObjectsAndFogRemainsIndependent() {
        var t=new TankState{Hp=100,X=30,Y=30};var wave=Wave(BattlefieldWave.Sandstorm);
        foreach(int tick in new[]{389,390,570,749,750}) {
            Assert.IsTrue(WeatherVisibility.Visible(t,100,30,tick,wave));
            Assert.IsTrue(WeatherVisibility.SegmentVisible(t,1,1,98,0,tick,wave));
            Assert.IsTrue(WeatherVisibility.AreaVisible(t,100,80,2,tick,wave));
            Assert.IsTrue(float.IsPositiveInfinity(WeatherVisibility.Radius(t,wave,tick)));
        }
        t.FogEndTick=600;Assert.AreEqual(GameRules.FogRadius,WeatherVisibility.Radius(t,wave,420));
        Assert.IsFalse(WeatherVisibility.Visible(t,36,30,420,wave));Assert.IsTrue(float.IsPositiveInfinity(WeatherVisibility.Radius(t,wave,600)));
        Assert.AreEqual(600,t.FogEndTick);t.Hp=0;Assert.IsTrue(WeatherVisibility.Visible(t,100,30,420,wave));
    }
    [Test] public void BeamCrossingVisionRemainsVisibleEvenWithHiddenOrigin() {
        var t=new TankState{Hp=100,X=50,Y=50,FogEndTick=600};var wave=Wave(4);
        Assert.IsFalse(WeatherVisibility.Visible(t,1,50,420,wave));
        Assert.IsTrue(WeatherVisibility.SegmentVisible(t,1,50,98,0,420,wave));
        Assert.IsFalse(WeatherVisibility.SegmentVisible(t,1,1,98,0,420,wave));
        Assert.IsFalse(WeatherVisibility.SegmentVisible(t,1,50,-98,0,420,wave));
        Assert.IsFalse(WeatherVisibility.Visible(t,70,50,420,wave));
        Assert.IsTrue(WeatherVisibility.AreaVisible(t,60,50,5.1f,420,wave));
        Assert.IsFalse(WeatherVisibility.AreaVisible(t,80,50,5.1f,420,wave));
    }
    [Test] public void HiddenCombatDoesNotCreateFxOrDirectionalAudioAndNeverReplays() {
        var map=Map();map.mapId=3;var w=new WorldState{Battlefield=Wave(4)};w.Tanks[0]=new TankState{Slot=0,Hp=100,X=50,Y=50,Life=1,FogEndTick=600};w.Tanks[1]=new TankState{Slot=1,Hp=100,X=80,Y=50,Life=2};
        var p=new CombatPresentation();p.Accept(9,new Snapshot{Tick=400,World=w},0,map);
        w.Combat[1]=new CombatEvent(1,401,CombatEvent.Shot,1,255,2,0,1,65535,0,80,50,0,0,0,0);
        p.Accept(9,new Snapshot{Tick=401,World=w},0,map);Assert.AreEqual(0,p.ActiveCount);CombatPresentation.Sound sound;Assert.IsFalse(p.NextSound(out sound));
        w.Battlefield=null;p.Accept(9,new Snapshot{Tick=750,World=w},0,map);Assert.AreEqual(0,p.ActiveCount);
        Assert.IsTrue(p.NextSound(out sound));Assert.AreEqual(20,sound.Clip,"Only the genuine Fog expiry cue is allowed");Assert.IsFalse(p.NextSound(out sound));
    }
    [Test] public void SandDoesNotCullFarAwayCombatPresentation() {
        var map=Map();map.mapId=3;var w=new WorldState{Battlefield=Wave(4)};
        w.Tanks[0]=new TankState{Slot=0,Hp=100,X=20,Y=50,Life=1};w.Tanks[1]=new TankState{Slot=1,Hp=100,X=80,Y=50,Life=2};
        var p=new CombatPresentation();p.Accept(9,new Snapshot{Tick=570,World=w},0,map);
        w.Combat[1]=new CombatEvent(1,571,CombatEvent.Shot,1,255,2,0,1,65535,0,80,50,0,0,0,0);
        p.Accept(9,new Snapshot{Tick=571,World=w},0,map);Assert.Greater(p.ActiveCount,0);
    }
    [Test] public void VisibleBeamIsRetainedWithoutRevealingItsHiddenSource() {
        var w=new WorldState{Battlefield=Wave(4)};w.Tanks[0]=new TankState{Slot=0,Hp=100,X=50,Y=50,Life=1,FogEndTick=600};
        var p=new CombatPresentation();p.Accept(9,new Snapshot{Tick=400,World=w},0,Map());
        w.Effects.Add(new SkillEffect{Id=1,Kind=SkillEffect.Beam,Owner=1,Start=401,End=401,X=1,Y=50,Angle=0,Radius=GameRules.BeamWidth});
        p.Accept(9,new Snapshot{Tick=401,World=w},0,Map());Assert.IsTrue(Array.Exists(p.Transients,f=>f.Active&&f.Clip=="@beam"));
        p.Update(.1f,w,Map(),0);Assert.IsTrue(Array.Exists(p.Transients,f=>f.Active&&f.Clip=="@beam"));
        CombatPresentation.Sound sound;Assert.IsFalse(p.NextSound(out sound));
    }
    [Test] public void WeatherValidationRejectsWrongMapsTargetsTimingAndUnknownKinds() {
        MapCatalog.Load(Application.streamingAssetsPath);
        Assert.AreEqual(12,BattlefieldWave.AllowedKinds(2));Assert.AreEqual(20,BattlefieldWave.AllowedKinds(3));Assert.AreEqual(40,BattlefieldWave.AllowedKinds(4));
        Wave(4).Validate(MapCatalog.Find(3).Map,300);Wave(5).Validate(MapCatalog.Find(4).Map,749);
        Assert.Throws<InvalidDataException>(()=>Wave(4).Validate(MapCatalog.Find(4).Map,400));
        Assert.Throws<InvalidDataException>(()=>Wave(5).Validate(MapCatalog.Find(2).Map,400));
        Assert.Throws<InvalidDataException>(()=>Wave().Validate(MapCatalog.Find(4).Map,750));
        Assert.Throws<InvalidDataException>(()=>new BattlefieldWave(1,4,300,360,600,new int[0]));
        Assert.Throws<InvalidDataException>(()=>new BattlefieldWave(1,5,300,390,750,new[]{0}));
        Assert.Throws<InvalidDataException>(()=>new BattlefieldWave(1,6,300,390,750,new int[0]));
        Assert.Throws<InvalidDataException>(()=>new BattlefieldWave(1,4,300,390,630,new int[0])); // v19 duration
        var bad=new WireReader(new WireWriter().U32(1).U8(4).U32(300).U32(390).U32(750).U8(1).U8(0).Done());
        Assert.Throws<InvalidDataException>(()=>BattlefieldWave.Read(bad));
    }
    [Test] public void WeatherNeverCreatesBombAttackFxButWeatherDeathsKeepTheirExplosion() {
        foreach(int kind in new[]{4,5}) {
            var w=new WorldState{Battlefield=Wave(kind)};w.Tanks[0]=new TankState{Slot=0,Hp=100,X=20,Y=20,Life=1};w.Tanks[1]=new TankState{Slot=1,Hp=100,X=22,Y=20,Life=2};
            var p=new CombatPresentation();p.Accept(9,new Snapshot{Tick=300,World=w},0,Map());p.Accept(9,new Snapshot{Tick=390,World=w},0,Map());Assert.AreEqual(0,p.ActiveCount);
            w.Tanks[1].Hp=0;var death=new CombatEvent(1,420,CombatEvent.Death,255,1,0,2,0,65535,0,22,20,0,100,kind==4?13:14,1);Assert.IsFalse(death.CauseOwnsExplosion);
            w.Combat[1]=death;p.Accept(9,new Snapshot{Tick=420,World=w},0,Map());Assert.IsTrue(Array.Exists(p.Transients,f=>f.Active&&f.Clip=="combat/explosion_top"));
        }
        Assert.AreEqual("Bão cát",BattlefieldPresentation.Name(4));Assert.AreEqual("Bão tuyết",BattlefieldPresentation.Name(5));
    }
}
