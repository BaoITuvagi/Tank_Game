package tank;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;

class TerrainGameplayTest {
 GameMap map(int id,boolean wet) {
  var m=new GameMap();m.mapId=id;m.width=m.height=80;
  m.walls=new GameMap.Rect[0];m.bricks=new GameMap.Rect[0];m.water=new GameMap.Rect[0];
  if(wet){var r=new GameMap.Rect();r.w=r.h=80;m.water=new GameMap.Rect[]{r};}
  return m;
 }
 World.Tank tank(){var t=new World.Tank();t.x=t.y=20;return t;}
 @Test void everyMapHasTheRequestedWetSpeedForNormalAndRoleplay() {
  for(int id:new int[]{1,2,3,4})for(boolean wet:new boolean[]{false,true})for(int nation:new int[]{-1,0}) {
   var m=map(id,wet);var t=tank();t.nation=nation;
   World.movement(t,new World.Input(1,8,0),m,new byte[64]);
   float factor=!wet?1:id==4?1.5f:id==3?1:.5f;
   assertEquals(20+GameRules.TankSpeed*factor/30,t.x,.00001);assertEquals(20,t.y);
  }
 }
 @Test void iceAndOasisComposeWithSprintDiagonalAndSlowWithoutChangingDrain() {
  for(int id:new int[]{2,3,4}) {
   var m=map(id,true);var t=tank();t.slowEndTick=2;t.rocketSlowEndTick=2;t.rocketSlowStacks=2;
   World.movement(t,new World.Input(1,41,0),m,new byte[64]);
   double factor=id==4?1.5:id==3?1:.5;
   double expected=GameRules.TankSpeed*factor*GameRules.SprintMultiplier*.7*.9*.9/30;
   assertEquals(expected,Math.hypot(t.x-20,t.y-20),.00001);assertEquals(149,t.energy);
  }
 }
 @Test void oasisRecoversExactlyOneAndAHalfWithoutExtraWireState() {
  for(int id:new int[]{1,2,3,4})for(boolean wet:new boolean[]{false,true}) {
   var m=map(id,wet);var t=tank();t.energy=0;
   for(int tick=1;tick<=100;tick++)World.movement(t,new World.Input(tick,0,0),m,new byte[64]);
   assertEquals(wet&&id==3?150:100,t.energy);
  }
  var m=map(3,true);var t=tank();t.energy=0;
  for(int tick=99;tick<=100;tick++)World.movement(t,new World.Input(1,0,0),m,new byte[64],tick);
  assertEquals(3,t.energy,"recovery uses simulation tick, not the delayed input tick");
  t.energy=149;World.movement(t,new World.Input(101,0,0),m,new byte[64]);assertEquals(150,t.energy);
 }
 @Test void oasisPreservesShiftReleaseDelayHeldShiftAndDeadTankRules() {
  var m=map(3,true);var t=tank();t.energy=0;
  World.movement(t,new World.Input(1,40,0),m,new byte[64]);assertEquals(0,t.energy);assertEquals(15,t.energyRecovery);
  for(int tick=2;tick<=16;tick++)World.movement(t,new World.Input(tick,0,0),m,new byte[64]);
  assertEquals(0,t.energy);assertEquals(0,t.energyRecovery);
  for(int tick=17;tick<=46;tick++)World.movement(t,new World.Input(tick,0,0),m,new byte[64]);assertEquals(45,t.energy);
  World.movement(t,new World.Input(47,40,0),m,new byte[64]);assertEquals(44,t.energy);
  World.movement(t,new World.Input(48,32,0),m,new byte[64]);assertEquals(44,t.energy);assertEquals(15,t.energyRecovery);
  t.hp=0;World.movement(t,new World.Input(49,0,0),m,new byte[64]);assertEquals(44,t.energy);assertEquals(15,t.energyRecovery);
 }
 @Test void iceEndsAtTheShoreAndStillRespectsSolidCollision() {
  var m=map(4,true);m.water[0].w=20;var t=tank();t.x=19.9f;
  World.movement(t,new World.Input(1,8,0),m,new byte[64]);assertEquals(20.35,t.x,.00001);
  World.movement(t,new World.Input(2,8,0),m,new byte[64]);assertEquals(20.65,t.x,.00001);
  var wall=new GameMap.Rect();wall.x=22;wall.y=0;wall.w=2;wall.h=80;m.walls=new GameMap.Rect[]{wall};m.water[0].w=80;
  for(int tick=3;tick<20;tick++)World.movement(t,new World.Input(tick,40,0),m,new byte[64]);
  assertFalse(m.blocked(t.x,t.y,t.body,new byte[64]));assertTrue(t.x+GameRules.TankHalfSize<=22+GameRules.CollisionEpsilon);
 }
 @Test void dashUsesTheSameTerrainBonusAndSweepsTheLongerIcePath() {
  for(int id:new int[]{1,2,3,4})for(boolean wet:new boolean[]{false,true}) {
   var w=new World(map(id,wet),1);var t=tank();w.tanks.put(0,t);
   assertTrue(w.dash(t));float factor=!wet?1:id==4?1.5f:id==3?1:.5f;
   assertEquals(20+GameRules.DashDistance*factor,t.x,.00001);
  }
  var m=map(4,true);var wall=new GameMap.Rect();wall.x=22;wall.y=0;wall.w=2;wall.h=80;m.walls=new GameMap.Rect[]{wall};
  var w=new World(m,1);var t=tank();w.tanks.put(0,t);assertTrue(w.dash(t));
  assertFalse(m.blocked(t.x,t.y,t.body,w.bricks));assertTrue(t.x+GameRules.TankHalfSize<22);
 }
 @Test void sharedTerrainTraceCoversClockParityDelaySprintAndDebuffDeadlines()throws Exception {
  var rows=new ArrayList<String>();var hex=HexFormat.of();
  for(int id:new int[]{1,2,3,4})for(boolean wet:new boolean[]{false,true})for(int buttons:new int[]{0,8,9,32,40,41})
   for(int energy:new int[]{0,77,149})for(int recovery:new int[]{0,1})for(int tick:new int[]{99,100})for(int slow:new int[]{0,1}) {
    var t=tank();t.nation=0;t.energy=energy;t.energyRecovery=recovery;
    if(slow==1){t.slowEndTick=100;t.rocketSlowEndTick=100;t.rocketSlowStacks=2;}
    var before=new Protocol.Writer();World.writeTank(before,t);
    World.movement(t,new World.Input(tick-50,buttons,12345),map(id,wet),new byte[64],tick);
    var after=new Protocol.Writer();World.writeTank(after,t);
    rows.add(id+"\t"+(wet?1:0)+"\t"+tick+"\t"+buttons+"\t"+hex.formatHex(before.done())+"\t"+hex.formatHex(after.done()));
   }
  assertEquals(1152,rows.size());Path path=Path.of("target/interop/terrain-movement.tsv");Files.createDirectories(path.getParent());Files.write(path,rows);
  var w=new World(map(3,true),1);var t=tank();t.energy=10;w.tanks.put(0,t);w.tick=100;
  byte[] base=w.encode();rows=new ArrayList<>();rows.add("FULL\t100\t"+hex.formatHex(base));
  World.movement(t,new World.Input(101,0,0),w.map,w.bricks);w.tick=101;
  rows.add("DELTA\t101\t"+hex.formatHex(DeltaCodec.encode(base,w.encode())));rows.add("REJOIN\t101\t"+hex.formatHex(w.encode()));
  for(int tick=102;tick<=103;tick++)World.movement(t,new World.Input(tick,0,0),w.map,w.bricks);
  assertEquals(15,t.energy);var finalTank=new Protocol.Writer();World.writeTank(finalTank,t);rows.add("EXPECTED\t103\t"+hex.formatHex(finalTank.done()));
  Files.write(Path.of("target/interop/terrain-recovery-state.tsv"),rows);
 }
}
