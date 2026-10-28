package tank;

import java.sql.*;
import java.util.*;
import java.nio.file.*;
import java.util.concurrent.*;

/** Uses the database worker/connection, exclusively owns campaign tables. */
final class CampaignStore {
    record Result(String id,long account,String name,int nation,int distance,int gold,boolean victory,Long durationMillis){}
    final Database database;
    final Path spool;
    final Map<String,Result> pending=new ConcurrentHashMap<>();
    final Set<String> writing=ConcurrentHashMap.newKeySet();
    CampaignStore(Database database)throws Exception {
        this.database=database;spool=database.spool.resolve("campaign");
        if(!Files.isDirectory(spool))return;
        try(var files=Files.list(spool)){for(Path file:files.filter(p->p.toString().endsWith(".json")).toList()){
            try {
                Result result=database.gson.fromJson(Files.readString(file),Result.class);
                if(result==null||!file.getFileName().toString().equals(result.id()+".json"))throw new java.io.IOException("Invalid campaign spool id");
                pending.put(result.id(),result);
            }catch(Exception e){System.err.println("Invalid campaign spool file: "+file);}
        }}
    }
    CompletableFuture<Long> save(long account,String name,CampaignRun run) {
        Result result=new Result(run.id,account,name,run.nation,run.distance,run.gold,run.victory,run.elapsedMillis());
        try {
            Files.createDirectories(spool);
            Path temp=spool.resolve(result.id()+".tmp"),target=spool.resolve(result.id()+".json");
            Files.writeString(temp,database.gson.toJson(result));Files.move(temp,target,StandardCopyOption.REPLACE_EXISTING);
            pending.put(result.id(),result);
        }catch(Exception e){return CompletableFuture.failedFuture(e);}
        return persist(result);
    }
    CompletableFuture<Long> persist(Result result) {
        if(!writing.add(result.id()))return CompletableFuture.failedFuture(new IllegalStateException("Campaign save in progress"));
        return database.submit(()->save(database.db,result)).whenComplete((gold,error)->{
            writing.remove(result.id());
            if(error==null){pending.remove(result.id(),result);try{Files.deleteIfExists(spool.resolve(result.id()+".json"));}catch(Exception ignored){}}
        });
    }
    void retry(){for(Result result:pending.values())if(!writing.contains(result.id()))persist(result);}
    static void schema(Connection db)throws SQLException {
        try(var s=db.createStatement()) {
            s.execute("CREATE TABLE IF NOT EXISTS campaign_runs(id TEXT PRIMARY KEY,account_id INTEGER NOT NULL REFERENCES accounts(id),nickname TEXT NOT NULL,nation INTEGER NOT NULL,distance INTEGER NOT NULL,gold INTEGER NOT NULL,victory INTEGER NOT NULL,ended INTEGER NOT NULL)");
            boolean duration=false;
            try(var columns=s.executeQuery("PRAGMA table_info(campaign_runs)")){while(columns.next())if(columns.getString("name").equals("duration_ms"))duration=true;}
            if(!duration)s.execute("ALTER TABLE campaign_runs ADD COLUMN duration_ms INTEGER");
            s.execute("CREATE INDEX IF NOT EXISTS campaign_run_ranking ON campaign_runs(account_id,distance DESC,gold DESC,duration_ms)");
            s.execute("CREATE TABLE IF NOT EXISTS campaign_profiles(account_id INTEGER PRIMARY KEY REFERENCES accounts(id),gold INTEGER NOT NULL DEFAULT 0,best INTEGER NOT NULL DEFAULT 0)");
            s.execute("CREATE INDEX IF NOT EXISTS campaign_ranking ON campaign_profiles(best DESC,account_id)");
        }
    }
    static long save(Connection db,long account,String name,CampaignRun run)throws SQLException {
        return save(db,new Result(run.id,account,name,run.nation,run.distance,run.gold,run.victory,run.elapsedMillis()));
    }
    static long save(Connection db,Result run)throws SQLException {
        db.setAutoCommit(false);
        try {
            int inserted;
            try(var s=db.prepareStatement("INSERT OR IGNORE INTO campaign_runs(id,account_id,nickname,nation,distance,gold,victory,ended,duration_ms) VALUES(?,?,?,?,?,?,?,?,?)")) {
                s.setString(1,run.id());s.setLong(2,run.account());s.setString(3,run.name());s.setInt(4,run.nation());s.setInt(5,run.distance());s.setInt(6,run.gold());s.setInt(7,run.victory()?1:0);s.setLong(8,System.currentTimeMillis());if(run.durationMillis()==null)s.setNull(9,Types.BIGINT);else s.setLong(9,run.durationMillis());inserted=s.executeUpdate();
            }
            if(inserted>0)try(var s=db.prepareStatement("INSERT INTO campaign_profiles VALUES(?,?,?) ON CONFLICT(account_id) DO UPDATE SET gold=gold+excluded.gold,best=MAX(best,excluded.best)")) {
                s.setLong(1,run.account());s.setInt(2,run.gold());s.setInt(3,run.distance());s.executeUpdate();
            }
            db.commit();
        }catch(SQLException e){db.rollback();throw e;}finally{db.setAutoCommit(true);}
        return gold(db,run.account());
    }
    static long gold(Connection db,long account)throws SQLException {try(var s=db.prepareStatement("SELECT gold FROM campaign_profiles WHERE account_id=?")){s.setLong(1,account);try(var r=s.executeQuery()){return r.next()?r.getLong(1):0;}}}
    record Rank(String name,int score,int gold,Long durationMillis,int rank){}
    static List<Rank> ranking(Connection db)throws SQLException {
        var rows=new ArrayList<Rank>();
        // Choose the best complete run per account; never combine distance, gold
        // and time from different runs. Unknown legacy times follow measured times.
        String sql="""
            WITH attempts AS (
                SELECT *,ROW_NUMBER() OVER(PARTITION BY account_id ORDER BY distance DESC,gold DESC,
                    COALESCE(duration_ms,9223372036854775807),ended,id) AS attempt
                FROM campaign_runs
            ), ranked AS (
                SELECT *,DENSE_RANK() OVER(ORDER BY distance DESC,gold DESC,
                    COALESCE(duration_ms,9223372036854775807)) AS place
                FROM attempts WHERE attempt=1
            )
            SELECT COALESCE(p.display_name,a.username),r.distance,r.gold,r.duration_ms,r.place
            FROM ranked r JOIN accounts a ON a.id=r.account_id
            LEFT JOIN player_profiles p ON p.account_id=a.id
            ORDER BY r.place,r.account_id LIMIT 50
            """;
        try(var s=db.createStatement();var r=s.executeQuery(sql)){
            while(r.next()){long duration=r.getLong(4);boolean unknown=r.wasNull();rows.add(new Rank(r.getString(1),r.getInt(2),r.getInt(3),unknown?null:duration,r.getInt(5)));}
        }
        return rows;
    }
}
