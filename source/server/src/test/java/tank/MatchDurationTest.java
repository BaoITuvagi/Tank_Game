package tank;

import java.nio.file.*;
import java.sql.*;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;

class MatchDurationTest {
    @TempDir Path tmp;
    RoomFeaturesTest helper(){var helper=new RoomFeaturesTest();helper.tmp=tmp;return helper;}

    @Test void fullMinuteStores1800TicksRatherThanTruncatedWallSeconds()throws Exception {
        var helper=helper();
        try(Server server=helper.server()) {
            Room room=helper.directRoom(server,2,4,60);
            room.prepare(room.members.get(0).session,1);
            room.phase=Protocol.RoomState.RUNNING;
            room.startedUtc=System.currentTimeMillis()-59_976;
            room.world.tick=1800;
            long account=room.members.get(0).account.id();
            room.finish(0);
            var saved=server.db.submit(()->server.db.historyDetail(account,room.match)).get();
            assertEquals(1800,saved.durationTicks());
            assertEquals(0,saved.reason());
        }
    }

    @Test void earlyEndStoresActualPlayedTicks()throws Exception {
        var helper=helper();
        try(Server server=helper.server()) {
            Room room=helper.directRoom(server,2,4,60);
            room.prepare(room.members.get(0).session,1);
            room.phase=Protocol.RoomState.RUNNING;
            room.world.tick=345;
            long account=room.members.get(0).account.id();
            room.finish(1);
            var saved=server.db.submit(()->server.db.historyDetail(account,room.match)).get();
            assertEquals(345,saved.durationTicks());
            assertEquals(1,saved.reason());
        }
    }

    @Test void legacyDatabaseMigratesIdempotentlyWithoutChangingHistory()throws Exception {
        Path path=tmp.resolve("legacy.db"),spool=tmp.resolve("spool");
        try(Connection db=DriverManager.getConnection("jdbc:sqlite:"+path);Statement statement=db.createStatement()) {
            String schema=Files.readString(Path.of("src/main/resources/schema.sql")).replace(",duration_ticks INTEGER","");
            for(String sql:schema.split(";"))if(!sql.isBlank())statement.execute(sql);
            statement.execute("INSERT INTO accounts VALUES(1,'legacy',zeroblob(16),zeroblob(32),600000,0)");
            statement.execute("INSERT INTO matches VALUES(7,1,'hash','COMBINED',1000,60976,0,1)");
            statement.execute("INSERT INTO match_players VALUES(7,1,'Legacy',0,2,1,1,0)");
        }
        for(int attempt=0;attempt<2;attempt++)try(Database db=new Database(path.toString(),spool.toString())) {
            var detail=db.submit(()->db.historyDetail(1,7)).get();
            assertNull(detail.durationTicks());
            assertEquals(59_976,detail.ended()-detail.started());
            assertEquals(2,detail.players().get(0).kills());
            assertEquals(1,db.submit(()->db.history(1,0,20)).get().size());
        }
    }
}
