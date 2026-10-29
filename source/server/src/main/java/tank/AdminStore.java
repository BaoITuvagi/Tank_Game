package tank;

import java.sql.*;
import java.nio.file.*;
import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicLong;
import java.security.*;

/** Separate database and bounded writer: never performs disk IO on a room tick. */
final class AdminStore implements AutoCloseable {
    final Connection db;
    final ThreadPoolExecutor worker=new ThreadPoolExecutor(1,1,0,TimeUnit.SECONDS,
        new ArrayBlockingQueue<>(2048),Thread.ofPlatform().name("admin-store").factory(),new ThreadPoolExecutor.AbortPolicy());
    final AtomicLong failures=new AtomicLong();
    AdminStore(String path)throws Exception {
        Path file=Path.of(path).toAbsolutePath(); Files.createDirectories(file.getParent());
        db=DriverManager.getConnection("jdbc:sqlite:"+file);
        try(var s=db.createStatement()) {
            s.execute("PRAGMA journal_mode=WAL"); s.execute("PRAGMA busy_timeout=3000");
            s.execute("CREATE TABLE IF NOT EXISTS admins(name TEXT PRIMARY KEY,salt BLOB NOT NULL,hash BLOB NOT NULL)");
            s.execute("CREATE TABLE IF NOT EXISTS bans(ip TEXT PRIMARY KEY,reason TEXT NOT NULL,active INTEGER NOT NULL,updated INTEGER NOT NULL)");
            s.execute("CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY,session TEXT NOT NULL,ip TEXT NOT NULL,account TEXT NOT NULL,connected INTEGER NOT NULL,ended INTEGER,reason TEXT)");
            s.execute("CREATE INDEX IF NOT EXISTS session_ip ON sessions(ip,account)");
            s.execute("CREATE TABLE IF NOT EXISTS audit(id INTEGER PRIMARY KEY,time INTEGER NOT NULL,admin TEXT NOT NULL,action TEXT NOT NULL,target TEXT NOT NULL,detail TEXT NOT NULL,result TEXT NOT NULL)");
            s.execute("UPDATE sessions SET ended="+System.currentTimeMillis()+",reason='Server khởi động lại' WHERE ended IS NULL");
        }
    }
    <T> CompletableFuture<T> submit(Callable<T> job) {
        var f=new CompletableFuture<T>();
        try {worker.execute(()->{try{f.complete(job.call());}catch(Exception e){failures.incrementAndGet();f.completeExceptionally(e);}});}
        catch(RejectedExecutionException e){failures.incrementAndGet();f.completeExceptionally(e);}
        return f;
    }
    void execute(String sql,Object... args)throws SQLException {
        try(var s=db.prepareStatement(sql)){for(int i=0;i<args.length;i++)s.setObject(i+1,args[i]);s.executeUpdate();}
    }
    List<Map<String,Object>> rows(String sql,Object... args)throws SQLException {
        try(var s=db.prepareStatement(sql)) {
            for(int i=0;i<args.length;i++)s.setObject(i+1,args[i]);
            try(var r=s.executeQuery()) {
                var rows=new ArrayList<Map<String,Object>>(); var meta=r.getMetaData();
                while(r.next()){var row=new LinkedHashMap<String,Object>();for(int i=1;i<=meta.getColumnCount();i++)row.put(meta.getColumnLabel(i),r.getObject(i));rows.add(row);}return rows;
            }
        }
    }
    void audit(String admin,String action,String target,String detail,String result)throws SQLException {
        execute("INSERT INTO audit(time,admin,action,target,detail,result) VALUES(?,?,?,?,?,?)",System.currentTimeMillis(),admin,action,target,detail,result);
    }
    void identity(Server.Session s,String reason) {
        String previous=s.historyKey, next=reason==null?UUID.randomUUID().toString():null;
        s.historyKey=next;
        String account=s.account==null?"":s.account.name(); long now=System.currentTimeMillis();
        submit(()->{
            if(previous!=null)execute("UPDATE sessions SET ended=?,reason=? WHERE id=?",now,reason==null?"Thay đổi đăng nhập":reason,previous);
            if(next!=null)execute("INSERT INTO sessions(id,session,ip,account,connected) VALUES(?,?,?,?,?)",next,s.id,s.ip(),account,now);
            return null;
        });
    }
    void init(String name,char[] password)throws Exception {
        if(!name.matches("[A-Za-z0-9_]{3,32}")||password.length<12||password.length>128)throw new IllegalArgumentException("Admin: tên 3–32 ký tự, mật khẩu 12–128 ký tự");
        byte[] salt=new byte[16]; new SecureRandom().nextBytes(salt);
        byte[] hash=Database.hash(new String(password),salt,600000);
        submit(()->{execute("INSERT INTO admins(name,salt,hash) VALUES(?,?,?) ON CONFLICT(name) DO UPDATE SET salt=excluded.salt,hash=excluded.hash",name,salt,hash);return null;}).get();
        Arrays.fill(password,'\0');
    }
    boolean login(String name,String password)throws Exception {
        try(var s=db.prepareStatement("SELECT salt,hash FROM admins WHERE name=?")) {
            s.setString(1,name);try(var r=s.executeQuery()){return r.next()&&MessageDigest.isEqual(r.getBytes(2),Database.hash(password,r.getBytes(1),600000));}
        }
    }
    public void close(){worker.shutdown();try{if(!worker.awaitTermination(10,TimeUnit.SECONDS))worker.shutdownNow();db.close();}catch(Exception e){System.err.println("admin store shutdown failed");}}
}
