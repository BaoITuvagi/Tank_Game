package tank;
import java.sql.*;
import java.nio.file.*;
import java.security.*;
import javax.crypto.*;
import javax.crypto.spec.*;
import java.util.*;
import java.util.concurrent.*;
import com.google.gson.Gson;
public class Database implements AutoCloseable  {
    public record Account(long id,String name) {
    }
    public record ResultPlayer(long account,String nickname,int slot,int kills,int deaths,int rank,boolean disconnected) {
    }
    public record Result(long id,int mapId,String mapHash,String mode,long started,long ended,int reason,long seed,List<ResultPlayer> players,Integer durationTicks) {
        // Older spool records and callers have no simulation duration; preserve their timestamps.
        public Result(long id,int mapId,String mapHash,String mode,long started,long ended,int reason,long seed,List<ResultPlayer> players) {
            this(id,mapId,mapHash,mode,started,ended,reason,seed,players,null);
        }
    }
    public record History(long match,long ended,int reason,int kills,int deaths,int rank) {
    }
    public static class Failure extends RuntimeException  {
        final Protocol.Error code;
        Failure(Protocol.Error c) {
            super(c.name());
            code=c;
        }
    }
    final ThreadPoolExecutor worker=new ThreadPoolExecutor(1,1,0,TimeUnit.SECONDS,new ArrayBlockingQueue<>(1024),Thread.ofPlatform().name("database").factory(),new ThreadPoolExecutor.AbortPolicy());
    final ScheduledExecutorService retry=Executors.newSingleThreadScheduledExecutor();
    final Path spool;
    final Connection db;
    final Gson gson=new Gson();
    final Map<Long,Result> pending=new ConcurrentHashMap<>();
    public volatile long failures;
    public Database(String path,String spoolPath)throws Exception {
        Path file=Path.of(path).toAbsolutePath();
        Files.createDirectories(file.getParent());
        spool=Path.of(spoolPath);
        Files.createDirectories(spool);
        db=DriverManager.getConnection("jdbc:sqlite:"+file);
        try(var resource=Database.class.getResourceAsStream("/schema.sql")) {
            if(resource==null)throw new java.io.IOException("Missing bundled schema.sql");
            String schema=new String(resource.readAllBytes(),java.nio.charset.StandardCharsets.UTF_8);
            try(Statement statement=db.createStatement()) {
                for(String sql:schema.split(";"))if(!sql.isBlank())statement.execute(sql);
            }
        }
        migrateDurationColumn();
        try(var files=Files.list(spool)) {
            files.filter(p->p.toString().endsWith(".json")).forEach(p-> {
                try {
                    Result r=gson.fromJson(Files.readString(p),Result.class);pending.put(r.id,r);
                }
                catch(Exception e) {
                    System.err.println("Invalid spool file: "+p);
                }
            });
        }
        retry.scheduleWithFixedDelay(this::retryPending,0,5,TimeUnit.SECONDS);
    }
    void migrateDurationColumn() throws SQLException {
        try (Statement statement=db.createStatement();ResultSet columns=statement.executeQuery("PRAGMA table_info(matches)")) {
            while(columns.next())if("duration_ticks".equals(columns.getString("name")))return;
        }
        try(Statement statement=db.createStatement()) {
            statement.execute("ALTER TABLE matches ADD COLUMN duration_ticks INTEGER");
        }
    }
    public <T> CompletableFuture<T> submit(Callable<T> action) {
        CompletableFuture<T> f=new CompletableFuture<>();
        try {
            worker.execute(()-> {
                try {
                    f.complete(action.call());
                }
                catch(Throwable e) {
                    f.completeExceptionally(e);
                }
            });
        }
        catch(RejectedExecutionException e) {
            f.completeExceptionally(new Failure(Protocol.Error.DATABASE_UNAVAILABLE));
        }
        return f;
    }
    static void credentials(String u,String p) {
        if(!u.matches("[A-Za-z0-9_]{3,24}")||p.length()<8||p.length()>64||p.getBytes(java.nio.charset.StandardCharsets.UTF_8).length>256)throw new Failure(Protocol.Error.INVALID_REQUEST);
    }
    static byte[] hash(String p,byte[] salt,int iterations)throws Exception {
        PBEKeySpec spec=new PBEKeySpec(p.toCharArray(),salt,iterations,256);
        try {
            return SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256").generateSecret(spec).getEncoded();
        }
        finally {
            spec.clearPassword();
        }
    }
    public Account register(String username,String password)throws Exception {
        credentials(username,password);
        String u=username.toLowerCase(Locale.ROOT);
        try(var check=db.prepareStatement("SELECT 1 FROM accounts WHERE username=?")) {
            check.setString(1,u);
            try(var existing=check.executeQuery()) {
                if(existing.next())throw new Failure(Protocol.Error.USERNAME_EXISTS);
            }
        }
        byte[] salt=new byte[16];
        new SecureRandom().nextBytes(salt);
        byte[] hash=hash(password,salt,600000);
        try(PreparedStatement s=db.prepareStatement("INSERT INTO accounts(username,salt,hash,iterations,created) VALUES(?,?,?,?,?)")) {
            s.setString(1,u);
            s.setBytes(2,salt);
            s.setBytes(3,hash);
            s.setInt(4,600000);
            s.setLong(5,System.currentTimeMillis());
            s.executeUpdate();
        }
        catch(SQLException e) {
            if(e.getErrorCode()==19)throw new Failure(Protocol.Error.USERNAME_EXISTS);
            throw e;
        }
        return find(u);
    }
    Account find(String u)throws Exception {
        try(var s=db.prepareStatement("SELECT id,username FROM accounts WHERE username=?")) {
            s.setString(1,u);
            try(var r=s.executeQuery()) {
                if(!r.next())throw new Failure(Protocol.Error.INVALID_CREDENTIALS);
                return new Account(r.getLong(1),r.getString(2));
            }
        }
    }
    public Account login(String username,String password)throws Exception {
        credentials(username,password);
        String u=username.toLowerCase(Locale.ROOT);
        try(var s=db.prepareStatement("SELECT id,username,salt,hash,iterations FROM accounts WHERE username=?")) {
            s.setString(1,u);
            try(var r=s.executeQuery()) {
                if(!r.next()||!MessageDigest.isEqual(r.getBytes(4),hash(password,r.getBytes(3),r.getInt(5))))throw new Failure(Protocol.Error.INVALID_CREDENTIALS);
                return new Account(r.getLong(1),r.getString(2));
            }
        }
    }
    public static String validDisplayName(String value) {
        String name=java.text.Normalizer.normalize(value.strip(),java.text.Normalizer.Form.NFC);
        if(name.isEmpty()||name.codePointCount(0,name.length())>24||name.getBytes(java.nio.charset.StandardCharsets.UTF_8).length>96||name.codePoints().anyMatch(Character::isISOControl))throw new Failure(Protocol.Error.INVALID_REQUEST);
        return name;
    }
    public String displayName(long id,String fallback)throws Exception {
        try(var s=db.prepareStatement("SELECT display_name FROM player_profiles WHERE account_id=?")) {
            s.setLong(1,id);
            try(var r=s.executeQuery()) {
                return r.next()?r.getString(1):fallback;
            }
        }
    }
    public String setDisplayName(long id,String value)throws Exception {
        String name=validDisplayName(value);
        try(var s=db.prepareStatement("INSERT INTO player_profiles(account_id,display_name) VALUES(?,?) ON CONFLICT(account_id) DO UPDATE SET display_name=excluded.display_name")) {
            s.setLong(1,id);
            s.setString(2,name);
            s.executeUpdate();
        }
        return name;
    }
    public List<History> history(long account,int offset,int limit)throws Exception {
        List<History> rows=new ArrayList<>();
        try(var s=db.prepareStatement("SELECT m.id,m.ended,m.reason,p.kills,p.deaths,p.rank FROM match_players p JOIN matches m ON m.id=p.match_id WHERE p.account_id=? ORDER BY m.ended DESC,m.id DESC LIMIT ? OFFSET ?")) {
            s.setLong(1,account);
            s.setInt(2,limit);
            s.setInt(3,offset);
            try(var r=s.executeQuery()) {
                while(r.next())rows.add(new History(r.getLong(1),r.getLong(2),r.getInt(3),r.getInt(4),r.getInt(5),r.getInt(6)));
            }
        }
        return rows;
    }
    public Result historyDetail(long account,long match)throws Exception {
        try(var s=db.prepareStatement("SELECT m.* FROM matches m JOIN match_players p ON p.match_id=m.id WHERE m.id=? AND p.account_id=?")) {
            s.setLong(1,match);
            s.setLong(2,account);
            try(var r=s.executeQuery()) {
                if(!r.next())throw new Failure(Protocol.Error.INVALID_REQUEST);
                int mapId=r.getInt("map_id"),reason=r.getInt("reason");
                String hash=r.getString("map_hash"),mode=r.getString("mode");
                long started=r.getLong("started"),ended=r.getLong("ended"),seed=r.getLong("seed");
                List<ResultPlayer> players=new ArrayList<>();
                try(var ps=db.prepareStatement("SELECT * FROM match_players WHERE match_id=? ORDER BY rank,slot")) {
                    ps.setLong(1,match);
                    try(var rows=ps.executeQuery()) {
                        while(rows.next())players.add(new ResultPlayer(rows.getLong("account_id"),rows.getString("nickname"),rows.getInt("slot"),rows.getInt("kills"),rows.getInt("deaths"),rows.getInt("rank"),rows.getInt("disconnected")!=0));
                    }
                }
                Number ticks=(Number)r.getObject("duration_ticks");
                return new Result(match,mapId,hash,mode,started,ended,reason,seed,List.copyOf(players),ticks==null?null:ticks.intValue());
            }
        }
    }
    public CompletableFuture<Boolean> save(Result result) {
        pending.put(result.id,result);
        return submit(()->persistOrSpool(result)).handle((saved,error)-> {
            if(error!=null) {
                failures++;try {
                    retry.execute(()->spoolOnly(result));
                }
                catch(RejectedExecutionException e) {
                    System.err.println("Result worker unavailable; held in memory match="+result.id);
                }
            }
            return Boolean.TRUE.equals(saved);
        });
    }
    boolean persistOrSpool(Result r) {
        try {
            persist(r);
            pending.remove(r.id);
            Files.deleteIfExists(spool.resolve(r.id+".json"));
            return true;
        }
        catch(Exception e) {
            failures++;
            try {
                Path temp=spool.resolve(r.id+".tmp");
                Files.writeString(temp,gson.toJson(r));
                try {
                    Files.move(temp,spool.resolve(r.id+".json"),StandardCopyOption.ATOMIC_MOVE,StandardCopyOption.REPLACE_EXISTING);
                }
                catch(AtomicMoveNotSupportedException x) {
                    Files.move(temp,spool.resolve(r.id+".json"),StandardCopyOption.REPLACE_EXISTING);
                }
            }
            catch(Exception x) {
                System.err.println("RESULT NOT SAVED: DB and spool unavailable match="+r.id);
            }
            return false;
        }
    }
    void persist(Result r)throws Exception {
        db.setAutoCommit(false);
        try {
            try(var s=db.prepareStatement("INSERT OR IGNORE INTO matches(id,map_id,map_hash,mode,started,ended,reason,seed,duration_ticks) VALUES(?,?,?,?,?,?,?,?,?)")) {
                s.setLong(1,r.id);
                s.setInt(2,r.mapId);
                s.setString(3,r.mapHash);
                s.setString(4,r.mode);
                s.setLong(5,r.started);
                s.setLong(6,r.ended);
                s.setInt(7,r.reason);
                s.setLong(8,r.seed);
                if(r.durationTicks==null)s.setNull(9,Types.INTEGER);else s.setInt(9,r.durationTicks);
                s.executeUpdate();
            }
            try(var s=db.prepareStatement("INSERT OR IGNORE INTO match_players VALUES(?,?,?,?,?,?,?,?)")) {
                for(var p:r.players) {
                    s.setLong(1,r.id);
                    s.setLong(2,p.account);
                    s.setString(3,p.nickname);
                    s.setInt(4,p.slot);
                    s.setInt(5,p.kills);
                    s.setInt(6,p.deaths);
                    s.setInt(7,p.rank);
                    s.setInt(8,p.disconnected?1:0);
                    s.addBatch();
                }
                s.executeBatch();
            }
            db.commit();
        }
        catch(Exception e) {
            db.rollback();
            throw e;
        }
        finally {
            db.setAutoCommit(true);
        }
    }
    void retryPending() {
        for(var r:pending.values())submit(()->persistOrSpool(r));
    }
    public long nextMatch()throws Exception {
        return submit(()-> {
            try(var s=db.createStatement();var r=s.executeQuery("SELECT coalesce(max(id),0)+1 FROM matches")) {
                if(!r.next())throw new SQLException("Missing match sequence");return r.getLong(1);
            }
        }).get();
    }
    void spoolOnly(Result r) {
        try {
            Path temp=spool.resolve(r.id+".overflow.tmp");
            Files.writeString(temp,gson.toJson(r));
            Files.move(temp,spool.resolve(r.id+".json"),StandardCopyOption.REPLACE_EXISTING);
        }
        catch(Exception e) {
            System.err.println("RESULT NOT SAVED: spool unavailable match="+r.id);
        }
    }
    public void close() {
        retry.shutdownNow();
        for(Result r:pending.values())spoolOnly(r);
        retryPending();
        worker.shutdown();
        try {
            worker.awaitTermination(15,TimeUnit.SECONDS);
            db.close();
        }
        catch(Exception e) {
            System.err.println("database shutdown: "+e.getMessage());
        }
    }
}
