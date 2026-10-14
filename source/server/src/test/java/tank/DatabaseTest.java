package tank;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import java.nio.file.*;
import java.sql.*;
import java.util.*;
import static org.junit.jupiter.api.Assertions.*;
class DatabaseTest {
 @TempDir Path temp;
 @Test void databaseAndSpoolFailureKeepsResultUntilRecovery()throws Exception {
  Path spool=temp.resolve("blocked-spool");
  try(Database db=new Database(temp.resolve("blocked.db").toString(),spool.toString())) {
   var account=db.submit(()->db.register("recovery","password123")).get();
   var result=new Database.Result(101,1,"hash","BASELINE",0,1,0,1,List.of(new Database.ResultPlayer(account.id(),account.name(),0,2,1,1,false)));
   db.retry.shutdownNow();
   db.submit(()->{try(var statement=db.db.createStatement()){statement.execute("PRAGMA query_only=ON");}return null;}).get();
   Files.delete(spool);Files.writeString(spool,"Injected obstruction: cannot create spool files");
   assertFalse(db.save(result).get());
   assertEquals(result,db.pending.get(101L));
   assertTrue(db.submit(()->db.history(account.id(),0,20)).get().isEmpty());
   Files.delete(spool);Files.createDirectory(spool);
   db.submit(()->{try(var statement=db.db.createStatement()){statement.execute("PRAGMA query_only=OFF");}return null;}).get();
   db.retryPending();db.submit(()->null).get();
   assertFalse(db.pending.containsKey(101L));
   assertEquals(101,db.submit(()->db.history(account.id(),0,20)).get().get(0).match());
   assertTrue(db.save(result).get());
   assertEquals(1,db.submit(()->db.history(account.id(),0,20)).get().size());
  }
 }
 @Test void spoolAndRetry()throws Exception{String path=temp.resolve("test.db").toString();try(Database db=new Database(path,temp.resolve("spool").toString())){var a=db.submit(()->db.register("Tester","password123")).get();var result=new Database.Result(17,1,"hash","BASELINE",0,1,0,1,List.of(new Database.ResultPlayer(a.id(),a.name(),0,1,0,1,false)));try(Connection lock=DriverManager.getConnection("jdbc:sqlite:"+path);Statement s=lock.createStatement()){s.execute("BEGIN IMMEDIATE");assertFalse(db.save(result).get());assertTrue(Files.exists(temp.resolve("spool/17.json")));s.execute("ROLLBACK");}assertTrue(db.save(result).get());assertTrue(db.save(result).get());assertEquals(1,db.submit(()->db.history(a.id(),0,20)).get().size());assertFalse(Files.exists(temp.resolve("spool/17.json")));}}
 @Test void queueOverflowSpoolsAndStartupRecovers()throws Exception{String path=temp.resolve("overflow.db").toString();Path spool=temp.resolve("overflow-spool");var gate=new java.util.concurrent.CountDownLatch(1);var entered=new java.util.concurrent.CountDownLatch(1);long account;try(Database db=new Database(path,spool.toString())){account=db.submit(()->db.register("overflow","password123")).get().id();db.worker.execute(()->{entered.countDown();try{gate.await();}catch(InterruptedException e){Thread.currentThread().interrupt();}});entered.await();for(int i=0;i<1024;i++)db.submit(()->null);var result=new Database.Result(99,1,"hash","COMBINED",0,1,0,1,List.of(new Database.ResultPlayer(account,"overflow",0,0,0,1,false)));assertFalse(db.save(result).get());long until=System.nanoTime()+2_000_000_000L;while(!Files.exists(spool.resolve("99.json"))&&System.nanoTime()<until)Thread.sleep(10);assertTrue(Files.exists(spool.resolve("99.json")));gate.countDown();}finally{gate.countDown();}try(Database fresh=new Database(path,spool.toString())){long until=System.nanoTime()+2_000_000_000L;while(fresh.submit(()->fresh.history(account,0,20)).get().isEmpty()&&System.nanoTime()<until)Thread.sleep(10);assertEquals(1,fresh.submit(()->fresh.history(account,0,20)).get().size());}}
}
