package tank;
import java.nio.file.*;
import java.sql.*;
import java.util.*;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
class RoleplayDatabaseTest {
    @TempDir Path tmp;
    Path legacy() throws Exception {
        Path path=tmp.resolve("legacy.db");
        try(Connection db=DriverManager.getConnection("jdbc:sqlite:"+path);Statement s=db.createStatement()) {
            s.execute("CREATE TABLE accounts(id INTEGER PRIMARY KEY,username TEXT UNIQUE NOT NULL,salt BLOB NOT NULL,hash BLOB NOT NULL,iterations INTEGER NOT NULL,created INTEGER NOT NULL)");
            s.execute("CREATE TABLE matches(id INTEGER PRIMARY KEY,map_id INTEGER,map_hash TEXT,mode TEXT,started INTEGER,ended INTEGER,reason INTEGER,seed INTEGER)");
            s.execute("CREATE TABLE match_players(match_id INTEGER,account_id INTEGER,nickname TEXT,slot INTEGER,kills INTEGER,deaths INTEGER,rank INTEGER,disconnected INTEGER,PRIMARY KEY(match_id,account_id))");
            s.execute("INSERT INTO accounts VALUES(1,'legacy',X'00',X'00',1,0)");
            s.execute("INSERT INTO matches VALUES(1,1,'map','COMBINED',1000,2000,0,7)");
            s.execute("INSERT INTO match_players VALUES(1,1,'Legacy',0,2,1,1,0)");
        }
        return path;
    }
    @Test void migrationIsIdempotentAndDoesNotTurnLegacyNullsIntoUsa() throws Exception {
        Path path=legacy(),spool=tmp.resolve("spool");
        for(int pass=0;pass<2;pass++)try(Database db=new Database(path.toString(),spool.toString())) {
            var old=db.submit(()->db.historyDetail(1,1)).get();assertNull(old.roomMode());assertNull(old.contentVersion());assertNull(old.durationTicks());assertNull(old.players().get(0).nationId());assertEquals("COMBINED",old.mode());
        }
        try(Database db=new Database(path.toString(),spool.toString())) {
            var player=new Database.ResultPlayer(1,"Legacy",0,3,1,1,false,0);
            var record=new Database.Result(2,1,"map","DELTA",1000,61000,0,7,List.of(player),1800,"ROLEPLAY",RoleplayContent.Version+"@"+RoleplayContent.Hash);
            assertTrue(db.save(record).get());var loaded=db.submit(()->db.historyDetail(1,2)).get();
            assertEquals("DELTA",loaded.mode());assertEquals("ROLEPLAY",loaded.roomMode());assertEquals(record.contentVersion(),loaded.contentVersion());assertEquals(0,loaded.players().get(0).nationId());
        }
    }
    @Test void oldSpoolWithoutRoleplayMetadataStillPersists() throws Exception {
        Path path=legacy(),spool=tmp.resolve("spool");Files.createDirectories(spool);
        Files.writeString(spool.resolve("3.json"),"{\"id\":3,\"mapId\":1,\"mapHash\":\"map\",\"mode\":\"BASELINE\",\"started\":1000,\"ended\":2000,\"reason\":0,\"seed\":7,\"players\":[{\"account\":1,\"nickname\":\"Legacy\",\"slot\":0,\"kills\":1,\"deaths\":0,\"rank\":1,\"disconnected\":false}]}");
        try(Database db=new Database(path.toString(),spool.toString())) {
            long deadline=System.nanoTime()+3_000_000_000L;
            while(Files.exists(spool.resolve("3.json"))&&System.nanoTime()<deadline)Thread.sleep(20);
            var loaded=db.submit(()->db.historyDetail(1,3)).get();assertNull(loaded.roomMode());assertNull(loaded.contentVersion());assertNull(loaded.players().get(0).nationId());assertEquals("BASELINE",loaded.mode());
        }
    }
    @Test void removedMetadataInExistingDatabaseAndSpoolDoesNotLoseHistory() throws Exception {
        Path path=legacy(),spool=tmp.resolve("spool");Files.createDirectories(spool);
        try(Connection db=DriverManager.getConnection("jdbc:sqlite:"+path);Statement s=db.createStatement()) {
            s.execute("ALTER TABLE match_players ADD COLUMN card_skill_id INTEGER");
            s.execute("UPDATE match_players SET card_skill_id=111 WHERE match_id=1");
        }
        Files.writeString(spool.resolve("4.json"),"{\"id\":4,\"mapId\":1,\"mapHash\":\"map\",\"mode\":\"DELTA\",\"started\":1000,\"ended\":61000,\"reason\":0,\"seed\":7,\"durationTicks\":1800,\"roomMode\":\"ROLEPLAY\",\"contentVersion\":\"legacy\",\"players\":[{\"account\":1,\"nickname\":\"Legacy\",\"slot\":0,\"kills\":3,\"deaths\":1,\"rank\":1,\"disconnected\":false,\"nationId\":2,\"cardSkillId\":110}]}");
        try(Database db=new Database(path.toString(),spool.toString())) {
            long deadline=System.nanoTime()+3_000_000_000L;
            while(Files.exists(spool.resolve("4.json"))&&System.nanoTime()<deadline)Thread.sleep(20);
            var old=db.submit(()->db.historyDetail(1,1)).get();assertEquals(2,old.players().get(0).kills());
            var recovered=db.submit(()->db.historyDetail(1,4)).get();assertEquals("ROLEPLAY",recovered.roomMode());assertEquals(2,recovered.players().get(0).nationId());assertEquals(3,recovered.players().get(0).kills());
            var player=new Database.ResultPlayer(1,"Legacy",0,4,1,1,false,3);
            assertTrue(db.save(new Database.Result(5,1,"map","DELTA",1000,61000,0,7,List.of(player),1800,"ROLEPLAY","current")).get());
        }
        try(Connection db=DriverManager.getConnection("jdbc:sqlite:"+path);Statement s=db.createStatement();ResultSet rows=s.executeQuery("SELECT match_id,card_skill_id FROM match_players ORDER BY match_id")) {
            assertTrue(rows.next());assertEquals(1,rows.getInt(1));assertEquals(111,rows.getInt(2));
            for(int match:new int[]{4,5}) {assertTrue(rows.next());assertEquals(match,rows.getInt(1));assertNull(rows.getObject(2));}
            assertFalse(rows.next());
        }
    }
}
