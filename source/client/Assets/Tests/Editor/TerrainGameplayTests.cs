using System;
using System.IO;
using NUnit.Framework;
using Tank;

public class TerrainGameplayTests {
 GameMap Map(int id,bool wet) {return new GameMap{mapId=id,width=80,height=80,walls=new MapRect[0],bricks=new MapRect[0],water=wet?new[]{new MapRect{w=80,h=80}}:new MapRect[0]};}
 [Test] public void JavaTerrainPredictionMatchesAcrossMapsClockParityDelaySprintAndDebuffDeadlines() {
  int count=0;
  foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/terrain-movement.tsv"))) {
   var f=line.Split('\t');var r=new WireReader(Bytes(f[4]));var actual=WorldState.ReadTank(r);r.End();actual.Nation=0;
   r=new WireReader(Bytes(f[5]));var expected=WorldState.ReadTank(r);r.End();
   Movement.Step(actual,new InputRecord(uint.Parse(f[2]),int.Parse(f[3]),12345),Map(int.Parse(f[0]),f[1]=="1"),new byte[64]);
   Assert.AreEqual(expected.X,actual.X,.00001,line);Assert.AreEqual(expected.Y,actual.Y,.00001,line);
   Assert.AreEqual(expected.Body,actual.Body);Assert.AreEqual(expected.Turret,actual.Turret);
   Assert.AreEqual(expected.Energy,actual.Energy);Assert.AreEqual(expected.EnergyRecovery,actual.EnergyRecovery);count++;
  }
  Assert.AreEqual(1152,count);
 }
 [Test] public void OasisRecoveryIsOneAndAHalfAndKeepsTheReleaseDelayAndCap() {
  var map=Map(3,true);var t=new TankState{Hp=100,X=20,Y=20,Energy=0,EnergyRecovery=15};
  for(uint tick=1;tick<=15;tick++)Movement.Step(t,new InputRecord(tick,0,0),map,new byte[64]);Assert.AreEqual(0,t.Energy);
  for(uint tick=16;tick<=115;tick++)Movement.Step(t,new InputRecord(tick,0,0),map,new byte[64]);Assert.AreEqual(150,t.Energy);
  Movement.Step(t,new InputRecord(116,40,0),map,new byte[64]);Assert.AreEqual(149,t.Energy);Assert.AreEqual(15,t.EnergyRecovery);
  Movement.Step(t,new InputRecord(117,32,0),map,new byte[64]);Assert.AreEqual(149,t.Energy);Assert.AreEqual(15,t.EnergyRecovery);
  t.Energy=149;t.EnergyRecovery=0;Movement.Step(t,new InputRecord(119,0,0),map,new byte[64]);Assert.AreEqual(150,t.Energy);
 }
 [Test] public void IceSpeedEndsAtTheShoreWithoutInertiaAndCannotCrossWalls() {
  var map=Map(4,true);map.water[0].w=20;var t=new TankState{Hp=100,X=19.9f,Y=20};
  Movement.Step(t,new InputRecord(1,8,0),map,new byte[64]);Assert.AreEqual(20.35,t.X,.00001);
  Movement.Step(t,new InputRecord(2,8,0),map,new byte[64]);Assert.AreEqual(20.65,t.X,.00001);
  map.water[0].w=80;map.walls=new[]{new MapRect{x=22,y=0,w=2,h=80}};
  for(uint tick=3;tick<20;tick++)Movement.Step(t,new InputRecord(tick,40,0),map,new byte[64]);
  Assert.IsFalse(map.Blocked(t.X,t.Y,t.Body,new byte[64]));Assert.LessOrEqual(t.X+GameRules.TankHalfSize,22+GameRules.CollisionEpsilon);
 }
 [Test] public void SharedBiomeIdsMatchTheMapCatalog() {Assert.AreEqual(MapCatalog.FortressId,GameRules.DesertMapId);Assert.AreEqual(MapCatalog.RiverId,GameRules.WinterMapId);}
 [Test] public void ReconciliationRepairsEnergyAndReplaysClockParityFromFullDeltaAndRejoin() {
  var lines=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/terrain-recovery-state.tsv"));
  var r=new WireReader(Bytes(lines[0].Split('\t')[2]));var baseline=WorldState.Full(r);r.End();
  r=new WireReader(Bytes(lines[3].Split('\t')[2]));var expected=WorldState.ReadTank(r);r.End();
  for(int state=0;state<3;state++)using(var client=new ClientCore(Map(3,true),new byte[32])) {
   var f=lines[state].Split('\t');WorldState world;
   if(state==0)world=baseline.Copy();else {r=new WireReader(Bytes(f[2]));world=state==1?DeltaCodec.Apply(baseline,r):WorldState.Full(r);r.End();}
   client.Slot=0;client.Predicted=new TankState{Hp=100,X=20,Y=20,Energy=0};
   var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
   var history=(System.Collections.Generic.SortedDictionary<uint,InputRecord>)typeof(ClientCore).GetField("history",flags).GetValue(client);
   for(uint tick=101;tick<=103;tick++)history[tick]=new InputRecord(tick,0,0);
   typeof(ClientCore).GetField("targetTick",flags).SetValue(client,103u);
   uint resolved=uint.Parse(f[1]);typeof(ClientCore).GetMethod("Reconcile",flags).Invoke(client,new object[]{new Snapshot{Tick=resolved,Resolved=resolved,World=world}});
   Assert.AreEqual(expected.Energy,client.Predicted.Energy);Assert.AreEqual(expected.EnergyRecovery,client.Predicted.EnergyRecovery);
   Assert.AreEqual(expected.X,client.Predicted.X);Assert.AreEqual(expected.Y,client.Predicted.Y);
   foreach(var tick in history.Keys)Assert.Greater(tick,resolved);
  }
 }
 static byte[] Bytes(string hex) {var bytes=new byte[hex.Length/2];for(int i=0;i<bytes.Length;i++)bytes[i]=Convert.ToByte(hex.Substring(i*2,2),16);return bytes;}
}
