package tank;

import com.sun.net.httpserver.*;
import com.google.gson.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.*;
import java.security.*;

/** Local HTTP control plane. No game tokens or credentials are included in DTOs. */
final class AdminWeb implements AutoCloseable {
    final Server game;
    final AdminStore store;
    final HttpServer http;
    final Gson gson=new Gson();
    final Set<String> banned=ConcurrentHashMap.newKeySet();
    final ConcurrentHashMap<String,Login> logins=new ConcurrentHashMap<>();
    final RateLimit loginRate=new RateLimit(5.0/60,10);
    final ThreadPoolExecutor executor=new ThreadPoolExecutor(4,4,0,TimeUnit.SECONDS,new ArrayBlockingQueue<>(64),Thread.ofPlatform().name("admin-http").factory(),new ThreadPoolExecutor.AbortPolicy());
    record Login(String name,String csrf,long expires){}
    static final class Problem extends RuntimeException {final int code;Problem(int c,String m){super(m);code=c;}}
    AdminWeb(Server game)throws Exception {
        this.game=game;
        store=new AdminStore(game.cfg.value("admin.database.path","runtime/admin.db"));
        HttpServer listener=null;
        try {
            for(var row:store.submit(()->store.rows("SELECT ip FROM bans WHERE active=1")).get())banned.add((String)row.get("ip"));
            listener=HttpServer.create(new InetSocketAddress(game.cfg.value("admin.bind","127.0.0.1"),game.cfg.integer("admin.port",8080)),32);
            listener.setExecutor(executor);listener.createContext("/",this::handle);http=listener;
        } catch(Exception e){if(listener!=null)listener.stop(0);store.close();executor.shutdownNow();throw e;}
    }
    void start(){http.start();System.out.println("Admin web http://"+game.cfg.value("admin.bind","127.0.0.1")+":"+http.getAddress().getPort());}
    boolean isBanned(String ip){return banned.contains(ip);}
    static String random(){byte[] b=new byte[32];new SecureRandom().nextBytes(b);return Base64.getUrlEncoder().withoutPadding().encodeToString(b);}
    static String text(JsonObject body,String name,int max){
        if(!body.has(name)||!body.get(name).isJsonPrimitive()||!body.get(name).getAsJsonPrimitive().isString())throw new Problem(400,"Thiếu trường "+name);
        String s=body.get(name).getAsString();if(!name.equals("password"))s=s.strip();if(s.length()>max)throw new Problem(400,"Trường "+name+" quá dài");return s;
    }
    static String ip(String value){
        try {
            if(!(value.matches("[0-9.]+")||value.contains(":")&&value.matches("[0-9a-fA-F:]+")))throw new Exception();
            if(!value.contains(":")) {String[] parts=value.split("\\.",-1);if(parts.length!=4)throw new Exception();for(String p:parts)if(!p.matches("0|[1-9][0-9]{0,2}")||Integer.parseInt(p)>255)throw new Exception();}
            return InetAddress.getByName(value).getHostAddress();
        }catch(Exception e){throw new Problem(400,"IP không hợp lệ. Nhập một IPv4 hoặc IPv6 cụ thể.");}
    }
    void handle(HttpExchange x) {
        try {
            x.getResponseHeaders().set("X-Content-Type-Options","nosniff");
            x.getResponseHeaders().set("Cache-Control","no-store");
            x.getResponseHeaders().set("Content-Security-Policy","default-src 'self'; script-src 'self'; style-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'");
            String path=x.getRequestURI().getPath(),method=x.getRequestMethod();
            if(!path.startsWith("/api/")) {
                if(!method.equals("GET"))throw new Problem(405,"Phương thức không hỗ trợ");
                String resource=switch(path){case "/","/index.html"->"index.html";case "/app.js"->"app.js";case "/style.css"->"style.css";default->throw new Problem(404,"Không tìm thấy");};
                try(var in=AdminWeb.class.getResourceAsStream("/admin/"+resource)) {
                    if(in==null)throw new Problem(404,"Thiếu tài nguyên web");
                    x.getResponseHeaders().set("Content-Type",resource.endsWith("js")?"text/javascript; charset=utf-8":resource.endsWith("css")?"text/css; charset=utf-8":"text/html; charset=utf-8");
                    byte[] bytes=in.readAllBytes();x.sendResponseHeaders(200,bytes.length);x.getResponseBody().write(bytes);return;
                }
            }
            boolean write=method.equals("POST");
            if(!write&&!method.equals("GET"))throw new Problem(405,"Phương thức không hỗ trợ");
            JsonObject body=new JsonObject();
            if(write) {
                String origin=x.getRequestHeaders().getFirst("Origin"),host=x.getRequestHeaders().getFirst("Host");
                if(origin!=null&&!origin.equals("http://"+host))throw new Problem(403,"Origin không hợp lệ");
                if(!Optional.ofNullable(x.getRequestHeaders().getFirst("Content-Type")).orElse("").startsWith("application/json"))throw new Problem(415,"Cần application/json");
                byte[] bytes=x.getRequestBody().readNBytes(8193);if(bytes.length>8192)throw new Problem(413,"Nội dung quá lớn");
                try{body=JsonParser.parseString(new String(bytes,StandardCharsets.UTF_8)).getAsJsonObject();}catch(RuntimeException e){throw new Problem(400,"JSON không hợp lệ");}
            }
            if(path.equals("/api/login")&&write){login(x,body);return;}
            Login login=authenticate(x);
            if(write&&!login.csrf().equals(x.getRequestHeaders().getFirst("X-CSRF-Token")))throw new Problem(403,"Phiên xác nhận không hợp lệ; hãy đăng nhập lại");
            Object result;
            if(method.equals("GET"))result=switch(path) {
                case "/api/me"->Map.of("name",login.name(),"csrf",login.csrf(),"expires",login.expires());
                case "/api/state"->snapshot();
                case "/api/bans"->store.submit(()->store.rows("SELECT * FROM bans ORDER BY updated DESC")).get(5,TimeUnit.SECONDS);
                case "/api/history"->query(x,false);
                case "/api/relations"->query(x,false);
                case "/api/audit"->query(x,true);
                default->throw new Problem(404,"Không tìm thấy API");
            }; else if(path.equals("/api/logout")) {
                store.submit(()->{store.audit(login.name(),"LOGOUT","","","OK");return null;}).get(5,TimeUnit.SECONDS);
                logins.remove(cookie(x));x.getResponseHeaders().add("Set-Cookie","tank_admin=; Path=/; HttpOnly; SameSite=Strict; Max-Age=0");result=Map.of("message","Đã đăng xuất");
            } else {
                synchronized(game.accessGate){result=mutate(path,body,login.name());}
            }
            json(x,200,result);
        }catch(Problem e){safeError(x,e.code,e.getMessage());}
        catch(TimeoutException e){safeError(x,503,"Thao tác chưa xác định kết quả do quá thời gian. Hãy tải lại dữ liệu trước khi thử lại.");}
        catch(Exception e){safeError(x,503,"Dịch vụ quản trị đang bận hoặc không lưu được dữ liệu. Hãy tải lại và kiểm tra nhật ký.");}
        finally{x.close();}
    }
    void login(HttpExchange x,JsonObject b)throws Exception {
        if(!loginRate.allow())throw new Problem(429,"Quá nhiều lần đăng nhập. Vui lòng chờ.");
        String name=text(b,"name",32),password=text(b,"password",128);
        boolean ok=store.submit(()->store.login(name,password)).get(5,TimeUnit.SECONDS);
        store.submit(()->{store.audit(name,"LOGIN","","",ok?"OK":"DENIED");return null;}).get(5,TimeUnit.SECONDS);
        if(!ok)throw new Problem(401,"Tên admin hoặc mật khẩu không đúng. Tài khoản game không có quyền quản trị.");
        long now=System.currentTimeMillis();logins.entrySet().removeIf(e->e.getValue().expires()<now);
        if(logins.size()>=100)throw new Problem(429,"Quá nhiều phiên quản trị");
        String token=random();Login login=new Login(name,random(),now+game.cfg.integer("admin.session.minutes",60)*60000L);
        logins.remove(cookie(x));logins.put(token,login);
        x.getResponseHeaders().add("Set-Cookie","tank_admin="+token+"; Path=/; HttpOnly; SameSite=Strict; Max-Age="+game.cfg.integer("admin.session.minutes",60)*60);
        json(x,200,Map.of("name",name,"csrf",login.csrf()));
    }
    String cookie(HttpExchange x){String h=x.getRequestHeaders().getFirst("Cookie");if(h!=null)for(String p:h.split(";"))if(p.strip().startsWith("tank_admin="))return p.strip().substring(11);return "";}
    Login authenticate(HttpExchange x){String token=cookie(x);Login l=logins.get(token);if(l==null||l.expires()<System.currentTimeMillis()){logins.remove(token);throw new Problem(401,"Cần đăng nhập quản trị");}return l;}
    Object query(HttpExchange x,boolean audit)throws Exception {
        Map<String,String> q=new HashMap<>();String raw=x.getRequestURI().getRawQuery();
        if(raw!=null)for(String p:raw.split("&")){String[] pair=p.split("=",2);q.put(URLDecoder.decode(pair[0],StandardCharsets.UTF_8),pair.length<2?"":URLDecoder.decode(pair[1],StandardCharsets.UTF_8));}
        int page;long from,to;
        try{page=Math.max(0,Math.min(100000,Integer.parseInt(q.getOrDefault("page","0"))));from=Long.parseLong(q.getOrDefault("from","0"));to=Long.parseLong(q.getOrDefault("to","9223372036854775807"));}catch(Exception e){throw new Problem(400,"Bộ lọc thời gian hoặc trang không hợp lệ");}
        String search=q.getOrDefault("q","");if(search.length()>200)throw new Problem(400,"Từ khóa quá dài");
        String like="%"+search+"%",action=q.getOrDefault("action","");
        if(x.getRequestURI().getPath().equals("/api/relations"))return store.submit(()->store.rows("SELECT s.ip,s.account,count(*) AS count,min(s.connected) AS firstSeen,max(coalesce(s.ended,s.connected)) AS lastSeen,coalesce(b.active,0) AS active,b.reason FROM sessions s LEFT JOIN bans b ON b.ip=s.ip WHERE s.ip LIKE ? OR s.account LIKE ? GROUP BY s.ip,s.account ORDER BY lastSeen DESC LIMIT 100",like,like)).get(5,TimeUnit.SECONDS);
        String exactAccount=q.getOrDefault("account","");
        return store.submit(()->audit?store.rows("SELECT * FROM audit WHERE time BETWEEN ? AND ? AND (target LIKE ? OR detail LIKE ? OR admin LIKE ?) AND (?='' OR action=?) ORDER BY id DESC LIMIT 51 OFFSET ?",from,to,like,like,like,action,action,page*50):store.rows("SELECT * FROM sessions WHERE connected BETWEEN ? AND ? AND (ip LIKE ? OR account LIKE ? OR session LIKE ?) AND (?='' OR account=?) ORDER BY connected DESC LIMIT 51 OFFSET ?",from,to,like,like,like,exactAccount,exactAccount,page*50)).get(5,TimeUnit.SECONDS);
    }
    Map<String,Object> session(Server.Session s){
        Room r=s.room;Database.Account account=s.account;String state=!s.open?"DISCONNECTED":account==null?"UNAUTHENTICATED":r==null?"LOBBY":r.view.state().name();
        long now=System.nanoTime(),last=Math.max(s.lastTcp,s.lastUdp);
        var row=new LinkedHashMap<String,Object>();row.put("id",s.id);row.put("ip",s.ip());row.put("port",s.socket.getPort());row.put("account",account==null?"":account.name());row.put("connected",s.connectedAt);row.put("lastActivity",System.currentTimeMillis()-Math.max(0,(now-last)/1000000));row.put("state",state);row.put("tcp",s.open);row.put("udp",s.endpoint!=null&&s.lastUdp>0&&now-s.lastUdp<Room.UDP_TIMEOUT_NANOS);row.put("room",r==null?null:r.id);row.put("match",r==null||r.match==0?null:Long.toString(r.match));row.put("rtt",null);return row;
    }
    Object snapshot(){
        var sessions=game.connections.stream().filter(s->s.open).map(this::session).toList();
        return Map.of("time",System.currentTimeMillis(),"sessions",sessions,"stats",Map.of("sessions",sessions.size(),"ips",sessions.stream().map(s->s.get("ip")).distinct().count(),"accounts",sessions.stream().map(s->s.get("account")).filter(a->!a.equals("")).distinct().count(),"playing",sessions.stream().filter(s->s.get("state").equals("RUNNING")).count(),"bans",banned.size()),"storageFailures",store.failures.get(),"tcpPort",game.tcp.getLocalPort(),"udpPort",game.udp.getLocalPort());
    }
    List<Server.Session> targets(JsonObject b){
        String scope=text(b,"scope",16),target=text(b,"target",128);
        if(!Set.of("all","session","ip","room").contains(scope))throw new Problem(400,"Phạm vi không hợp lệ");
        if(scope.equals("ip"))target=ip(target);final String value=target;
        var list=game.connections.stream().filter(s->s.open).filter(s->switch(scope){case "all"->true;case "session"->s.id.equals(value);case "ip"->s.ip().equals(value);case "room"->{Room room=s.room;yield room!=null&&Integer.toString(room.id).equals(value);}default->false;}).toList();
        if(list.isEmpty())throw new Problem(409,"Không còn phiên phù hợp. Danh sách đã thay đổi.");return list;
    }
    Object mutate(String path,JsonObject b,String admin)throws Exception {
        if(path.equals("/api/preview")) {
            String ip=ip(text(b,"ip",64));
            var sessions=game.connections.stream().filter(s->s.open&&s.ip().equals(ip)).map(this::session).toList();
            return Map.of("ip",ip,"sessions",sessions,"count",sessions.size());
        }
        if(path.equals("/api/ban")||path.equals("/api/ban-delete")) {
            String ip=ip(text(b,"ip",64)),reason=text(b,"reason",500);
            boolean delete=path.endsWith("delete");
            if(!delete&&reason.isBlank())throw new Problem(400,"Cần nhập lý do ban");
            if(!delete&&(!b.has("active")||!b.get("active").isJsonPrimitive()||!b.get("active").getAsJsonPrimitive().isBoolean()))throw new Problem(400,"Trạng thái không hợp lệ");
            boolean active=!delete&&b.get("active").getAsBoolean();
            String action=delete?"BAN_DELETE":"BAN_SAVE";
            store.submit(()->{
                store.db.setAutoCommit(false);
                try {
                    store.audit(admin,action,ip,reason,active?"ACTIVE":"INACTIVE");
                    if(delete)store.execute("DELETE FROM bans WHERE ip=?",ip);
                    else store.execute("INSERT INTO bans(ip,reason,active,updated) VALUES(?,?,?,?) ON CONFLICT(ip) DO UPDATE SET reason=excluded.reason,active=excluded.active,updated=excluded.updated",ip,reason,active?1:0,System.currentTimeMillis());
                    store.db.commit();
                }catch(Exception e){store.db.rollback();throw e;}finally{store.db.setAutoCommit(true);}return null;
            }).get();
            int affected=0;
            if(active){banned.add(ip);for(var s:List.copyOf(game.connections))if(s.ip().equals(ip)&&s.closeWithReason("Ban IP: "+reason))affected++;}
            else banned.remove(ip);
            return Map.of("message",delete?"Đã xóa quy tắc; IP có thể kết nối lại":active?"Ban IP có hiệu lực":"Đã tạm dừng quy tắc","affected",affected);
        }
        if(!path.equals("/api/kick")&&!path.equals("/api/notice"))throw new Problem(404,"Không tìm thấy API");
        if(path.equals("/api/kick")&&!Set.of("session","ip").contains(text(b,"scope",16)))throw new Problem(400,"Chỉ ngắt một phiên hoặc một IP");
        String message=text(b,"message",500);
        if(path.endsWith("notice")&&(message.isBlank()||message.getBytes(StandardCharsets.UTF_8).length>1000))throw new Problem(400,"Thông báo cần 1–1000 byte UTF-8");
        String type=path.endsWith("notice")?text(b,"type",20):"";
        if(path.endsWith("notice")&&!Set.of("info","warning","error").contains(type))throw new Problem(400,"Loại thông báo không hợp lệ");
        var targets=targets(b);
        String target=targets.stream().map(s->{Database.Account a=s.account;return s.ip()+"/"+(a==null?"":a.name())+"/"+s.id;}).collect(java.util.stream.Collectors.joining(","));
        store.submit(()->{store.audit(admin,path.endsWith("kick")?"KICK":"NOTICE",target,message,"REQUESTED");return null;}).get(5,TimeUnit.SECONDS);
        int count=0;
        for(var s:targets) {
            if(path.endsWith("kick")){if(s.closeWithReason("Admin ngắt: "+message))count++;}
            else synchronized(s){if(s.open&&s.outbound.offer(Protocol.frame(Protocol.NOTICE,0,new Protocol.Writer().u16(type.equals("info")?200:type.equals("warning")?201:202).str(message).done())))count++;}
        }
        final int total=count;
        store.submit(()->{store.audit(admin,path.endsWith("kick")?"KICK_RESULT":"NOTICE_RESULT",target,message,"COUNT="+total);return null;}).get(5,TimeUnit.SECONDS);
        if(count==0)throw new Problem(409,"Không có phiên được xử lý; phiên đã đóng hoặc hàng đợi đầy");
        return Map.of("affected",count,"message",path.endsWith("kick")?"Đã ngắt kết nối; client được phép kết nối lại":"Đã đưa thông báo vào hàng đợi gửi (chưa xác nhận đã đọc)");
    }
    void json(HttpExchange x,int status,Object value)throws java.io.IOException {byte[] bytes=gson.toJson(value).getBytes(StandardCharsets.UTF_8);x.getResponseHeaders().set("Content-Type","application/json; charset=utf-8");x.sendResponseHeaders(status,bytes.length);x.getResponseBody().write(bytes);}
    void safeError(HttpExchange x,int code,String message){try{json(x,code,Map.of("message",message));}catch(Exception ignored){}}
    public void close(){http.stop(0);executor.shutdown();try{executor.awaitTermination(5,TimeUnit.SECONDS);}catch(InterruptedException e){Thread.currentThread().interrupt();}logins.clear();store.close();}
}
