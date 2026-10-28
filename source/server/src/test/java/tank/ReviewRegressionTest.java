package tank;
import java.io.*;
import java.nio.file.*;
import java.util.concurrent.*;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;

class ReviewRegressionTest {
 @TempDir Path tmp;
 GameMap map(){GameMap m=new GameMap();m.width=m.height=60;var wall=new GameMap.Rect();wall.x=10;wall.y=0;wall.w=1;wall.h=60;m.walls=new GameMap.Rect[]{wall};m.bricks=new GameMap.Rect[0];m.water=new GameMap.Rect[0];return m;}
 @Test void rotatedBodyStopsAtWallAndStillSlides(){
  GameMap m=map();World.Tank tank=new World.Tank();tank.x=8;tank.y=10;
  for(int tick=1;tick<=90;tick++){World.movement(tank,new World.Input(tick,41,0),m,new byte[64]);assertFalse(m.blocked(tank.x,tank.y,tank.body,new byte[64]));}
  assertTrue(tank.y>20);assertTrue(tank.x<=10-GameRules.TankHalfSize);
  assertTrue(m.blocked(9,10,4500,new byte[64]));assertFalse(m.blocked(10-GameRules.TankHalfSize*(float)Math.sqrt(2)-.01f,10,4500,new byte[64]));
 }
 @Test void intactBrickBlocksEntireBodyAndDestroyedBrickAllowsPassage(){
  GameMap m=map();m.bricks=m.walls;m.walls=new GameMap.Rect[0];byte[] hp=new byte[64];hp[0]=3;
  assertTrue(m.blocked(9.5f,10,0,hp));hp[0]=0;assertFalse(m.blocked(9.5f,10,0,hp));
 }
 @Test void bundledSchemaHasProfilesAndCanBeAppliedAgain()throws Exception{
  try(Database db=new Database(tmp.resolve("schema.db").toString(),tmp.resolve("spool").toString())){
   var account=db.submit(()->db.register("schema_user","password123")).get();
   assertEquals("Tên xe",db.submit(()->db.setDisplayName(account.id(),"Tên xe")).get());
   try(var input=Database.class.getResourceAsStream("/schema.sql")){
    String schema=new String(input.readAllBytes(),java.nio.charset.StandardCharsets.UTF_8);
    db.submit(()->{try(var statement=db.db.createStatement()){for(String sql:schema.split(";"))if(!sql.isBlank())statement.execute(sql);}return null;}).get();
   }
   assertEquals("Tên xe",db.submit(()->db.displayName(account.id(),"fallback")).get());
  }
 }
 @Test void blockedMetricsWriterDoesNotBlockRoomAndDeadlineLagIsRecorded()throws Exception{
  var helper=new RoomFeaturesTest();helper.tmp=tmp;CountDownLatch entered=new CountDownLatch(1),release=new CountDownLatch(1);
  StringWriter output=new StringWriter();Writer blocked=new Writer(){
   public void write(char[] c,int off,int len)throws IOException{entered.countDown();try{release.await();}catch(InterruptedException e){throw new IOException(e);}output.write(c,off,len);}
   public void flush(){}public void close(){}
  };
  try(Server server=helper.server();Metrics metrics=new Metrics(blocked)){
   Room room=helper.directRoom(server,2,4,180);room.prepare(room.members.get(0).session,1);
   assertTrue(entered.await(2,TimeUnit.SECONDS));
   long before=System.nanoTime();metrics.row(room,before,0,0,40_000_000L);
   assertTrue(System.nanoTime()-before<100_000_000L,"Metrics I/O blocked room");
   release.countDown();metrics.close();String[] lines=output.toString().split("\\R");
   String[] header=lines[0].split(","),row=lines[1].split(",");assertEquals(header.length,row.length);
   assertEquals("1",row[8]);assertEquals("40000",row[30]);
  }finally{release.countDown();}
 }
}
