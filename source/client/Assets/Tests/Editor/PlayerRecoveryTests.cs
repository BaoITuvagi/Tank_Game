using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Tank;

public class PlayerRecoveryTests {
 [Test] public void InputCatchesUpAfterFrameStallAndKeepsUpAtLowFrameRate(){
  using(var c=new ClientCore(null,new byte[32])){
   var flags=BindingFlags.NonPublic|BindingFlags.Instance;
   typeof(ClientCore).GetField("token",flags).SetValue(c,new byte[16]);
   c.Phase=ClientPhase.PLAYING;c.Latest=new Snapshot{Tick=100};c.LastArrivalUs=ClientCore.NowUs;
   Assert.IsTrue(c.GenerateInput(24,0));
   // A short stall is inside the old 30-tick reset threshold. Then render at 15 FPS.
   for(uint tick=108;tick<468;tick+=2){
    c.Latest.Tick=tick;c.LastArrivalUs=ClientCore.NowUs;
    Assert.IsTrue(c.GenerateInput(24,9000));
    uint sent=(uint)typeof(ClientCore).GetField("targetTick",flags).GetValue(c);
    Assert.Greater(sent,tick,"Commands must reach the server before their simulation tick");
    Assert.LessOrEqual(sent,tick+30);
    if(tick==108)Assert.AreEqual(110,sent,"Recover immediately after the stall");
    else Assert.AreEqual(tick+1,sent,"Preserve both timely commands at 15 FPS");
    if(tick!=108)Assert.IsTrue(c.GenerateInput(24,9000));
    Assert.IsFalse(c.GenerateInput(24,9000),"Do not resend the same tick during catch-up");
   }
  }
 }
 [Test] public void PongIncreasesInputLeadWhenLatencyRisesDuringMatch(){
  using(var c=new ClientCore(null,new byte[32])){
   c.Match=7;c.Phase=ClientPhase.PLAYING;c.PingSamples=8;c.MinRttUs=1000;c.LeadTicks=2;
   ulong sent=(ulong)(ClientCore.NowUs-400000);
   var pong=new Packet{Op=Op.Pong,Payload=new WireWriter().U64(7).U64(sent).U64(1000000).U64(1000000).Done()};
   typeof(ClientCore).GetMethod("Handle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{pong});
   Assert.GreaterOrEqual(c.LeadTicks,8);Assert.LessOrEqual(c.LeadTicks,12);
   Assert.GreaterOrEqual(c.LastRttUs,400000);Assert.AreEqual(9,c.PingSamples);
   Assert.IsTrue(c.PingFresh);
   c.LastPongUs=ClientCore.NowUs-4e6;Assert.IsFalse(c.PingFresh);
   c.Dispose();Assert.AreEqual(0,c.LastRttUs);Assert.AreEqual(0,c.LastPongUs);Assert.IsFalse(c.PingFresh);
  }
 }
 [Test] public void InputStillMovesAndShootsWhenWallClockIsMinutesAheadOfServer(){
  var map=new GameMap{walls=new MapRect[0],bricks=new MapRect[0],water=new MapRect[0]};
  using(var c=new ClientCore(map,new byte[32])){
   typeof(ClientCore).GetField("token",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(c,new byte[16]);
   c.Phase=ClientPhase.PLAYING;c.Slot=2;c.StartUs=(ulong)(ClientCore.NowUs-120e6);c.LastArrivalUs=ClientCore.NowUs;
   c.Latest=new Snapshot{Tick=300,Resolved=300,World=new WorldState()};c.Latest.World.Tanks[2]=new TankState{Slot=2,Hp=3,X=20,Y=20};c.Predicted=c.Latest.World.Tanks[2].Copy();
   Assert.IsTrue(c.GenerateInput(24,0));Assert.Greater(c.Predicted.X,20);
   var history=(SortedDictionary<uint,InputRecord>)typeof(ClientCore).GetField("history",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c);
   foreach(var input in history.Values){Assert.Greater(input.Tick,300);Assert.LessOrEqual(input.Tick,330);Assert.AreEqual(24,input.Buttons);}
   c.Latest.Tick=301;c.Latest.Resolved=301;c.LastArrivalUs=ClientCore.NowUs;Assert.IsTrue(c.GenerateInput(24,0));
  }
 }
 [Test] public void ExtrapolationIsBoundedDuringLostSnapshots(){
  var s=new Snapshot{Tick=123};Assert.AreEqual(125,ClientCore.DesiredInputTick(s,1e6,1e6,2));Assert.LessOrEqual(ClientCore.DesiredInputTick(s,60e6,0,12),153);
 }
 [Test] public void AutoFireTogglesAndResetsOnFocusLossAndMatchExit(){
  var fire=new AutoFireControl();fire.Update(true,true,true);Assert.IsTrue(fire.Enabled);Assert.AreEqual(24,fire.Apply(8));
  fire.Update(true,true,false);Assert.IsTrue(fire.Enabled);fire.Update(true,true,true);Assert.IsFalse(fire.Enabled);
  fire.Update(true,true,true);fire.Update(true,false,false);Assert.IsFalse(fire.Enabled);
  fire.Update(true,true,true);fire.Update(false,true,false);Assert.IsFalse(fire.Enabled);
 }
 [Test] public void AfkWarningClockNeverMovesWhenTankIsDead(){
  using(var c=new ClientCore(null,new byte[32])){
   c.Slot=2;c.AfkWarning=true;c.AfkWarnedAt=ClientCore.NowUs-5e6;c.Latest=new Snapshot{World=new WorldState()};c.Latest.World.Tanks[2]=new TankState{Slot=2,Hp=0};
   double warned=c.AfkWarnedAt;c.Poll();System.Threading.Thread.Sleep(30);c.Poll();Assert.AreEqual(warned,c.AfkWarnedAt);Assert.IsTrue(c.AfkWarning);
  }
 }
}
