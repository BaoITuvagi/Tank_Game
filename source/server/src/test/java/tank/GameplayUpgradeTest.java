package tank;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.*;
import static org.junit.jupiter.api.Assertions.*;

class GameplayUpgradeTest {
 GameMap open(){GameMap m=new GameMap();m.width=m.height=200;m.walls=new GameMap.Rect[0];m.bricks=new GameMap.Rect[0];m.water=new GameMap.Rect[0];return m;}
 @Test void doubledGroundWaterAndBulletSpeed(){
  GameMap m=open();var t=new World.Tank();t.x=t.y=10;
  for(int tick=1;tick<=30;tick++)World.movement(t,new World.Input(tick,8,0),m,new byte[64]);
  assertEquals(19,t.x,.0001);
  var water=new GameMap.Rect();water.x=0;water.y=0;water.w=water.h=200;m.water=new GameMap.Rect[]{water};t.x=10;
  for(int tick=1;tick<=30;tick++)World.movement(t,new World.Input(tick,8,0),m,new byte[64]);
  assertEquals(14.5,t.x,.0001);
  var w=new World(m,1);var bullet=new World.Bullet();bullet.id=1;bullet.x=10;bullet.y=10;
  for(int i=0;i<30;i++)w.advance(bullet);
  assertEquals(31.6,bullet.x,.0001);
 }
 @Test void sprintExhaustsAfterFiveSecondsAndRequiresReleaseToRecover(){
  var m=open();var t=new World.Tank();t.x=t.y=10;
  for(int i=1;i<=150;i++)World.movement(t,new World.Input(i,40,0),m,new byte[64]);
  assertEquals(0,t.energy);assertEquals(77.5,t.x,.001);
  float exhausted=t.x;for(int i=0;i<30;i++)World.movement(t,new World.Input(i+151,40,0),m,new byte[64]);
  assertEquals(0,t.energy);assertEquals(exhausted+9,t.x,.001);
  for(int i=0;i<15;i++)World.movement(t,new World.Input(i+181,8,0),m,new byte[64]);
  assertEquals(0,t.energy);assertEquals(0,t.energyRecovery);
  for(int i=0;i<150;i++)World.movement(t,new World.Input(i+196,0,0),m,new byte[64]);
  assertEquals(150,t.energy);
  World.movement(t,new World.Input(346,40,0),m,new byte[64]);assertEquals(149,t.energy);
 }
 @Test void neutralShiftOpposingKeysAndDeathDoNotDrainEnergy(){
  var m=open();var t=new World.Tank();t.x=t.y=10;
  World.movement(t,new World.Input(1,32,0),m,new byte[64]);assertEquals(150,t.energy);assertEquals(10,t.x);
  World.movement(t,new World.Input(2,47,0),m,new byte[64]);assertEquals(150,t.energy);assertEquals(10,t.x);
  t.hp=0;World.movement(t,new World.Input(3,40,0),m,new byte[64]);assertEquals(150,t.energy);assertEquals(10,t.x);
  assertDoesNotThrow(()->new World.Input(1,63,35999));assertThrows(IllegalArgumentException.class,()->new World.Input(1,64,0));
 }
 @Test void sprintKeepsDiagonalNormalizationAndCannotPassWalls(){
  var m=open();var a=new World.Tank();a.x=a.y=10;var b=a.copy();
  World.movement(a,new World.Input(1,40,0),m,new byte[64]);World.movement(b,new World.Input(1,41,0),m,new byte[64]);
  assertEquals(a.x-10,Math.hypot(b.x-10,b.y-10),.00001);
  var wall=new GameMap.Rect();wall.x=11;wall.y=0;wall.w=1;wall.h=30;m.walls=new GameMap.Rect[]{wall};a.x=9.5f;
  for(int i=2;i<90;i++)World.movement(a,new World.Input(i,40,0),m,new byte[64]);
  assertTrue(a.x+GameRules.TankHalfSize<=11+GameRules.CollisionEpsilon);assertFalse(m.blocked(a.x,a.y,new byte[64]));
 }
 @Test void everyInitialPlayerUsesACornerZoneAcrossSeedsAndSlotGaps()throws Exception{
  for(int count:new int[]{2,3,4,5,8})for(int seed=0;seed<32;seed++){
   GameMap map=GameMap.load(Path.of("../client/Assets/StreamingAssets/"+(count<=4?"map.json":"map-large.json")));
   World w=new World(map,seed);Set<String> positions=new HashSet<>();
   for(int i=0;i<count;i++)w.add(i+8-count);
   for(var t:w.tanks.values()){
    assertTrue(Math.min(t.x,map.width-t.x)<=map.width/4);assertTrue(Math.min(t.y,map.height-t.y)<=map.height/4);
    assertFalse(map.blocked(t.x,t.y,w.bricks));assertTrue(positions.add(t.x+","+t.y));
    if(count<=4)assertTrue(Arrays.stream(map.spawns).limit(4).anyMatch(p->p.x==t.x&&p.y==t.y));
   }
  }
 }
 @Test void configuredHpResetsOnRespawnAndEveryHitDamagesImmediately()throws Exception{
  var map=GameMap.load(Path.of("../client/Assets/StreamingAssets/map.json"));
  for(int hp:new int[]{1,6,30}){
   var w=new World(map,1,hp);w.add(0);var t=w.tanks.get(0);assertEquals(hp,t.hp);assertEquals(hp,t.maxHp);
   w.tick=149;w.damage(t,0);assertEquals(hp-1,t.hp);
   for(int i=1;i<hp;i++)w.damage(t,0);assertEquals(0,t.hp);t.energy=0;
   for(int i=0;i<89;i++)w.step(Map.of());assertEquals(0,t.hp);
   w.step(Map.of());assertEquals(hp,t.hp);assertEquals(150,t.energy);assertEquals(w.tick,t.shield);
   w.damage(t,0);assertEquals(hp-1,t.hp);
  }
 }
 @Test void extendedStateRoundTripsThroughFullAndDeltaIncludingNewTank()throws Exception{
  var map=GameMap.load(Path.of("../client/Assets/StreamingAssets/map.json"));var w=new World(map,1,30);w.add(0);
  byte[] base=w.encode();var t=w.tanks.get(0);t.energy=17;t.energyRecovery=13;t.lastKill=77;t.kills=3;t.hp=20;
  assertArrayEquals(w.encode(),DeltaCodec.apply(base,DeltaCodec.encode(base,w.encode())));
  assertArrayEquals(w.encode(),World.decode(w.encode()).encode());
  base=w.encode();w.add(1);assertArrayEquals(w.encode(),DeltaCodec.apply(base,DeltaCodec.encode(base,w.encode())));
 }
 @Test void oldProtocolVersionIsRejected(){byte[] frame=Protocol.frame(73,1,new byte[0]);frame[2]=1;assertThrows(IllegalArgumentException.class,()->Protocol.decode(frame,false));}
}
