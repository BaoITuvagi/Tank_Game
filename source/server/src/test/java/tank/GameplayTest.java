package tank;
import org.junit.jupiter.api.*;
import java.nio.file.*;
import java.util.*;
import static org.junit.jupiter.api.Assertions.*;
class GameplayTest {
 GameMap map;@BeforeEach void map()throws Exception{map=GameMap.load(Path.of("../../source/client/Assets/StreamingAssets/map.json"));}
 @Test void bulletSurvivesThreeSecondsAndExpiresAtFiveSeconds(){
  GameMap open=new GameMap();open.walls=new GameMap.Rect[0];open.bricks=new GameMap.Rect[0];open.water=new GameMap.Rect[0];
  World w=new World(open,1);w.tick=37;
  World.Bullet bullet=new World.Bullet();bullet.id=1;bullet.born=w.tick;bullet.x=5;bullet.y=5;w.bullets.put(1,bullet);
  for(int age=1;age<150;age++){w.step(Map.of());assertTrue(w.bullets.containsKey(1),"expired at age "+age);}
  assertEquals(0,bullet.bounces);assertEquals(5+149*.72f,bullet.x,.001f);
  w.step(Map.of());assertFalse(w.bullets.containsKey(1));assertEquals(37+150,w.tick);
 }
 @Test void movementWaterWallsTurret(){World w=new World(map,1);w.add(0);var t=w.tanks.get(0);t.x=t.y=5;World.movement(t,new World.Input(1,1,0),map,w.bricks);float straight=t.y-5;t.x=t.y=5;World.movement(t,new World.Input(1,9,0),map,w.bricks);assertEquals(straight,Math.hypot(t.x-5,t.y-5),1e-5);t.x=28;t.y=15;World.movement(t,new World.Input(1,1,0),map,w.bricks);assertEquals(.15,t.y-15,1e-5);t.x=1.85f;t.y=5;World.movement(t,new World.Input(1,4,0),map,w.bricks);assertEquals(1.85,t.x,1e-5);assertEquals(100,World.turn(35900,100));assertEquals(35600,World.turn(0,18000));}
 @Test void cooldownLifetimeDamageAndRespawn(){World w=new World(map,1);w.add(0);var t=w.tanks.get(0);t.x=t.y=5;for(int i=1;i<=30;i++)w.step(Map.of(0,new World.Input(i,16,0)));assertEquals(1,w.nextBullet-1);w.step(Map.of(0,new World.Input(31,16,0)));assertEquals(2,w.nextBullet-1);w.damage(t,0);assertEquals(80,t.hp);w.tick=151;w.damage(t,0,100);assertEquals(0,t.hp);assertEquals(1,t.deaths);assertEquals(0,t.kills);for(int i=0;i<90;i++)w.step(Map.of());assertEquals(100,t.hp);assertEquals(w.tick,t.shield);}
 @Test void sweepAndCorner(){var h=World.sweep(0,0,20,20,5,5,6,6,null,null,1);assertNotNull(h);assertEquals(.25,h.time());assertEquals(-1,h.nx());assertEquals(-1,h.ny());assertNull(World.sweep(0,0,0,10,5,5,6,6,null,null,1));}
 @Test void brickReflectAndSelfHit(){World w=new World(map,1);World.Bullet b=new World.Bullet();b.id=1;b.owner=0;b.x=19;b.y=19.8f;b.heading=9000;w.bullets.put(1,b);w.advance(b);assertEquals(2,w.bricks[0]);assertEquals(1,b.bounces);assertEquals(27000,b.heading);w.add(0);var t=w.tanks.get(0);t.x=5;t.y=5;t.shield=0;w.tick=20;b.x=4.6f;b.y=5;b.heading=0;b.born=0;w.bullets.put(1,b);w.advance(b);assertEquals(80,t.hp);assertFalse(w.bullets.containsKey(1));}
 @Test void inputDeadlines(){InputBuffer b=new InputBuffer();var in=new World.Input(3,9,35999);b.put(in,1);b.put(in,1);b.put(new World.Input(3,16,0),1);assertEquals(in,b.resolve(3,0));assertEquals(1,b.duplicate);assertEquals(1,b.conflict);b.put(in,3);assertEquals(1,b.late);assertEquals(0,b.resolve(4,18000).buttons());assertEquals(1,b.missing);}
 @Test void maxSnapshot(){World w=new World(map,1);for(int i=0;i<8;i++)w.add(i);for(int i=0;i<32;i++){var b=new World.Bullet();b.id=i;w.bullets.put(i,b);}byte[] encoded=w.encode();assertEquals(1363,encoded.length);assertEquals(1405,12+30+encoded.length);assertArrayEquals(encoded,World.decode(encoded).encode());}
 @Test void muzzleInsideWallEjectsAfterSingleBounce(){World w=new World(map,1);World.Bullet b=new World.Bullet();b.id=1;b.x=.8f;b.y=5;b.heading=18000;w.bullets.put(1,b);w.advance(b);assertEquals(1,b.bounces);assertTrue(b.x>1.08);assertTrue(w.bullets.containsKey(1));}
 @Test void threeActualShotsDestroyBrickAndSynchronizeFullAndDelta(){
  map.walls=new GameMap.Rect[0];map.bricks=new GameMap.Rect[]{map.bricks[0]};World w=new World(map,1);w.add(0);var shooter=w.tanks.get(0);shooter.hp=shooter.maxHp=500;shooter.x=19;shooter.y=15;shooter.turret=9000;
  assertTrue(map.blocked(19,21,w.bricks));byte[] original=w.encode();List<Integer> damage=new ArrayList<>();int previous=3;
  for(int tick=1;tick<=90;tick++){w.step(Map.of(0,new World.Input(tick,16,9000)));if(w.bricks[0]!=previous){previous=w.bricks[0];damage.add(previous);}}
  assertEquals(List.of(2,1,0),damage);assertEquals(3,w.nextBullet-1);assertFalse(map.blocked(19,21,w.bricks));
  assertEquals(0,World.decode(w.encode()).bricks[0]);assertEquals(0,World.decode(DeltaCodec.apply(original,DeltaCodec.encode(original,w.encode()))).bricks[0]);
 }
 @Test void repeatedActualShotsReflectFromSteelWithoutDestroyingIt(){
  World w=new World(map,1);w.add(0);var shooter=w.tanks.get(0);shooter.hp=shooter.maxHp=500;shooter.x=20;shooter.y=18;shooter.turret=18000;boolean reflected=false;
  for(int tick=1;tick<=150;tick++){w.step(Map.of(0,new World.Input(tick,16,18000)));reflected|=w.bullets.values().stream().anyMatch(b->b.bounces>0);}
  assertEquals(5,w.nextBullet-1);assertTrue(reflected);assertTrue(map.blocked(17,18,w.bricks));for(var brick:map.bricks)assertEquals(3,w.bricks[brick.id]);
 }
 @Test void brickStagesKeepTheirFullCollisionUntilDestroyedAndRepairEveryStage()throws Exception{
  map.walls=new GameMap.Rect[0];map.bricks=new GameMap.Rect[]{map.bricks[0]};World w=new World(map,1);byte[] base=w.encode();List<String> rows=new ArrayList<>();var hex=HexFormat.of();rows.add("BASE\t"+hex.formatHex(base));
  for(int hp=2;hp>=0;hp--){
   var b=new World.Bullet();b.id=w.nextBullet++;b.x=19;b.y=19.8f;b.heading=9000;w.bullets.put(b.id,b);w.advance(b);w.bullets.clear();
   assertEquals(hp,w.bricks[0]);assertEquals(hp>0,map.blocked(19,21,w.bricks));
   byte[] state=w.encode(),delta=DeltaCodec.encode(base,state);assertArrayEquals(state,DeltaCodec.apply(base,delta));assertArrayEquals(state,World.decode(state).encode());
   rows.add("STAGE\t"+hp+"\t"+hex.formatHex(delta)+"\t"+hex.formatHex(state));
  }
  rows.add("REJOIN\t"+hex.formatHex(w.encode()));Path path=Path.of("target/interop/brick-stages.tsv");Files.createDirectories(path.getParent());Files.write(path,rows);
 }
 @Test void fullAndDeltaRejectBrickHealthAboveThreeWithoutChangingTheBase(){
  World w=new World(map,1);byte[] base=w.encode();w.bricks[0]=4;
  assertThrows(IllegalArgumentException.class,()->World.decode(w.encode()));
  var bad=new Protocol.Writer().u8(0).u8(0).u8(0).u8(1).u8(0).u8(4).done();
  assertThrows(IllegalArgumentException.class,()->DeltaCodec.apply(base,bad));assertEquals(3,World.decode(base).bricks[0]);
 }
}


