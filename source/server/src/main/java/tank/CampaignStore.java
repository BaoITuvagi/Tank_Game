package tank;

import java.sql.*;
import java.util.*;
import java.nio.file.*;
import java.util.concurrent.*;

/** Uses the database worker/connection, exclusively owns campaign tables. */
final class CampaignStore {
    record Result(String id,long account,String name,int nation,int distance,int gold,boolean victory){}
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
        Result result=new Result(run.id,account,name,run.nation,run.distance,run.gold,run.victory);
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
            s.execute("CREATE TABLE IF NOT EXISTS campaign_profiles(account_id INTEGER PRIMARY KEY REFERENCES accounts(id),gold INTEGER NOT NULL DEFAULT 0,best INTEGER NOT NULL DEFAULT 0)");
            s.execute("CREATE INDEX IF NOT EXISTS campaign_ranking ON campaign_profiles(best DESC,account_id)");
        }
    }
    static long save(Connection db,long account,String name,CampaignRun run)throws SQLException {
        return save(db,new Result(run.id,account,name,run.nation,run.distance,run.gold,run.victory));
    }
    static long save(Connection db,Result run)throws SQLException {
        db.setAutoCommit(false);
        try {
            int inserted;
            try(var s=db.prepareStatement("INSERT OR IGNORE INTO campaign_runs VALUES(?,?,?,?,?,?,?,?)")) {
                s.setString(1,run.id());s.setLong(2,run.account());s.setString(3,run.name());s.setInt(4,run.nation());s.setInt(5,run.distance());s.setInt(6,run.gold());s.setInt(7,run.victory()?1:0);s.setLong(8,System.currentTimeMillis());inserted=s.executeUpdate();
            }
            if(inserted>0)try(var s=db.prepareStatement("INSERT INTO campaign_profiles VALUES(?,?,?) ON CONFLICT(account_id) DO UPDATE SET gold=gold+excluded.gold,best=MAX(best,excluded.best)")) {
                s.setLong(1,run.account());s.setInt(2,run.gold());s.setInt(3,run.distance());s.executeUpdate();
            }
            db.commit();
        }catch(SQLException e){db.rollback();throw e;}finally{db.setAutoCommit(true);}
        return gold(db,run.account());
    }
    static long gold(Connection db,long account)throws SQLException {try(var s=db.prepareStatement("SELECT gold FROM campaign_profiles WHERE account_id=?")){s.setLong(1,account);try(var r=s.executeQuery()){return r.next()?r.getLong(1):0;}}}
    record Rank(String name,int score){}
    static List<Rank> ranking(Connection db)throws SQLException {
        var rows=new ArrayList<Rank>();
        try(var s=db.createStatement();var r=s.executeQuery("SELECT COALESCE(p.display_name,a.username),c.best FROM campaign_profiles c JOIN accounts a ON a.id=c.account_id LEFT JOIN player_profiles p ON p.account_id=a.id ORDER BY c.best DESC,c.account_id LIMIT 50")){while(r.next())rows.add(new Rank(r.getString(1),r.getInt(2)));}
        return rows;
    }
}
