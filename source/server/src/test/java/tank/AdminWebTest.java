package tank;

import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;
import java.net.*;
import java.net.http.*;
import java.nio.file.*;
import java.time.Duration;
import java.util.*;
import java.util.concurrent.*;
import java.util.function.BooleanSupplier;
import com.google.gson.*;

class AdminWebTest {
    @TempDir Path tmp;
    final HttpClient http=HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(3)).build();
    final Gson gson=new Gson();
    String cookie="",csrf="";
    Path config()throws Exception {
        Path cfg=tmp.resolve("server.properties");
        String map=Path.of("../../source/client/Assets/StreamingAssets/map.json").toAbsolutePath().normalize().toString().replace('\\','/');
        Files.writeString(cfg,"bind=127.0.0.1\ntcp.port=0\nudp.port=0\nbenchmark=true\nadmin.enabled=true\nadmin.port=0\nmap.path="+map+"\ndatabase.path="+path("game.db")+"\nadmin.database.path="+path("admin.db")+"\nspool.path="+path("spool")+"\nmetrics.path="+path("metrics.csv")+"\n");return cfg;
    }
    String path(String name){return tmp.resolve(name).toString().replace('\\','/');}
    HttpResponse<String> request(Server s,String method,String endpoint,Object body,boolean csrfHeader)throws Exception {
        var builder=HttpRequest.newBuilder(URI.create("http://127.0.0.1:"+s.admin.http.getAddress().getPort()+endpoint)).timeout(Duration.ofSeconds(10));
        if(!cookie.isEmpty())builder.header("Cookie",cookie);
        if(csrfHeader)builder.header("X-CSRF-Token",csrf);
        if(method.equals("POST"))builder.header("Content-Type","application/json").POST(HttpRequest.BodyPublishers.ofString(gson.toJson(body)));
        else builder.GET();return http.send(builder.build(),HttpResponse.BodyHandlers.ofString());
    }
    JsonObject post(Server s,String endpoint,Object body)throws Exception {var r=request(s,"POST",endpoint,body,true);assertEquals(200,r.statusCode(),r.body());return JsonParser.parseString(r.body()).getAsJsonObject();}
    JsonObject state(Server s)throws Exception {var r=request(s,"GET","/api/state",null,false);assertEquals(200,r.statusCode(),r.body());return JsonParser.parseString(r.body()).getAsJsonObject();}
    void login(Server s)throws Exception {
        s.admin.store.init("admin","test-admin-password".toCharArray());
        var r=request(s,"POST","/api/login",Map.of("name","admin","password","test-admin-password"),false);
        assertEquals(200,r.statusCode(),r.body());cookie=r.headers().firstValue("Set-Cookie").orElseThrow().split(";",2)[0];csrf=JsonParser.parseString(r.body()).getAsJsonObject().get("csrf").getAsString();
        assertTrue(r.headers().firstValue("Set-Cookie").orElseThrow().contains("HttpOnly; SameSite=Strict"));
    }
    static void eventually(BooleanSupplier condition)throws Exception {long end=System.nanoTime()+TimeUnit.SECONDS.toNanos(4);while(!condition.getAsBoolean()){if(System.nanoTime()>end)fail("Timed out waiting for real server state");Thread.sleep(10);}}
    Socket socket(Server s)throws Exception {Socket socket=new Socket("127.0.0.1",s.tcp.getLocalPort());socket.setSoTimeout(4000);return socket;}
    @Test void authenticationCsrfValidationAndLogout()throws Exception {
        try(Server s=new Server(new Config(config()))) {
            s.start();assertEquals(401,request(s,"GET","/api/state",null,false).statusCode());
            var page=request(s,"GET","/",null,false);assertEquals(200,page.statusCode());assertTrue(page.body().contains("Kết nối trực tiếp"));
            assertEquals(401,request(s,"POST","/api/login",Map.of("name","player","password","password123"),false).statusCode());
            login(s);
            assertEquals(403,request(s,"POST","/api/ban",Map.of("ip","127.0.0.1","reason","test","active",true),false).statusCode());
            for(String bad:List.of("localhost","256.1.1.1","127.0.0.1/24","1.2.3","01.2.3.4"))assertEquals(400,request(s,"POST","/api/ban",Map.of("ip",bad,"reason","test","active",true),true).statusCode());
            assertEquals(400,request(s,"POST","/api/notice",Map.of("scope","all","target","","type","info","message",""),true).statusCode());
            post(s,"/api/logout",Map.of());assertEquals(401,request(s,"GET","/api/state",null,false).statusCode());
        }
    }
    @Test void realSessionsNoticeKickBanAndPersistence()throws Exception {
        Path cfg=config();
        try(Server s=new Server(new Config(cfg))) {
            s.start();login(s);
            try(Socket a=socket(s);Socket b=socket(s);Socket guest=socket(s)) {
                eventually(()->s.connections.size()==3);
                assertEquals(3,state(s).getAsJsonArray("sessions").size());
                assertEquals(1,state(s).getAsJsonObject("stats").get("ips").getAsInt());
                BaselineTest.auth(a,"alice");BaselineTest.auth(b,"bravo");
                BaselineTest.send(a,CREATE_ROOM,new Writer().str("Admin integration").done());BaselineTest.waitFor(a,ACTION_RESULT);
                int room=new Reader(BaselineTest.waitFor(a,ROOM_STATE).payload()).u32();
                BaselineTest.send(b,JOIN_ROOM,new Writer().u32(room).done());BaselineTest.waitFor(b,ACTION_RESULT);
                eventually(()->s.rooms.get(room).view.count()==2);
                String aId=s.connections.stream().filter(x->x.account!=null&&x.account.name().equals("alice")).findFirst().orElseThrow().id;
                assertEquals(1,post(s,"/api/notice",Map.of("scope","session","target",aId,"type","warning","message","Cảnh báo thử nghiệm")).get("affected").getAsInt());
                Reader notice=new Reader(BaselineTest.waitFor(a,NOTICE).payload());assertEquals(201,notice.u16());assertEquals("Cảnh báo thử nghiệm",notice.str(1000));
                assertEquals(2,post(s,"/api/notice",Map.of("scope","room","target",""+room,"type","info","message","room")).get("affected").getAsInt());
                assertEquals(3,post(s,"/api/notice",Map.of("scope","ip","target","127.0.0.1","type","error","message","ip")).get("affected").getAsInt());
                assertEquals(3,post(s,"/api/notice",Map.of("scope","all","target","","type","info","message","all")).get("affected").getAsInt());
                assertEquals(400,request(s,"POST","/api/notice",Map.of("scope","all","target","","type","info","message",""),true).statusCode());
                assertEquals(1,post(s,"/api/kick",Map.of("scope","session","target",aId,"message","test kick")).get("affected").getAsInt());
                eventually(()->s.connections.size()==2&&s.rooms.get(room).view.count()==1);
                assertFalse(s.admin.banned.contains("127.0.0.1"));
                assertEquals(409,request(s,"POST","/api/kick",Map.of("scope","session","target",aId,"message","again"),true).statusCode());
                assertEquals(2,post(s,"/api/ban",Map.of("ip","127.0.0.1","reason","same NAT","active",true)).get("affected").getAsInt());
                eventually(()->s.connections.isEmpty()&&s.rooms.isEmpty());assertTrue(s.tokens.isEmpty());
                try(Socket blocked=socket(s)){assertEquals(-1,blocked.getInputStream().read());}
                var hist=request(s,"GET","/api/history?q=alice",null,false);assertTrue(hist.body().contains("alice"));assertTrue(hist.body().contains("Admin ngắt"));
                assertFalse(state(s).toString().contains("password"));assertFalse(state(s).toString().contains("token"));
                var relations=request(s,"GET","/api/relations?q=127.0.0.1",null,false);assertTrue(relations.body().contains("firstSeen"));assertTrue(relations.body().contains("bravo"));
            }
        }
        cookie="";csrf="";
        try(Server s=new Server(new Config(cfg))) {
            s.start();assertTrue(s.admin.isBanned("127.0.0.1"));
            try(Socket blocked=socket(s)){assertEquals(-1,blocked.getInputStream().read());}
            // Existing admin credentials survive restart.
            var response=request(s,"POST","/api/login",Map.of("name","admin","password","test-admin-password"),false);assertEquals(200,response.statusCode());
            cookie=response.headers().firstValue("Set-Cookie").orElseThrow().split(";",2)[0];csrf=JsonParser.parseString(response.body()).getAsJsonObject().get("csrf").getAsString();
            assertTrue(request(s,"GET","/api/history?q=bravo",null,false).body().contains("bravo"));
            assertTrue(request(s,"GET","/api/audit?q=alice",null,false).body().contains("KICK"));
            post(s,"/api/ban",Map.of("ip","127.0.0.1","reason","paused","active",false));
            try(Socket connected=socket(s)){eventually(()->s.connections.size()==1);}
            post(s,"/api/ban-delete",Map.of("ip","127.0.0.1","reason","unban"));
            assertEquals("[]",request(s,"GET","/api/bans",null,false).body());
        }
    }
    @Test void ipBanDoesNotCloseOtherIpAndKickIpAllowsReconnect()throws Exception {
        try(Server s=new Server(new Config(config()))) {
            s.start();login(s);
            try(Socket first=socket(s);Socket second=new Socket()) {
                second.bind(new InetSocketAddress("127.0.0.2",0));second.connect(new InetSocketAddress("127.0.0.1",s.tcp.getLocalPort()));
                eventually(()->s.connections.size()==2);
                post(s,"/api/kick",Map.of("scope","ip","target","127.0.0.1","message","IP kick"));
                eventually(()->s.connections.size()==1);
                try(Socket again=socket(s)) {
                    eventually(()->s.connections.size()==2);
                    post(s,"/api/ban",Map.of("ip","127.0.0.1","reason","isolation","active",true));
                    eventually(()->s.connections.size()==1);assertEquals("127.0.0.2",s.connections.iterator().next().ip());
                    BaselineTest.send(second,HEARTBEAT,new Writer().u64(123).done());assertEquals(HEARTBEAT_ACK,BaselineTest.waitFor(second,HEARTBEAT_ACK).op());
                }
            }
        }
    }
    @Test void inMatchKickRevokesUdpAndUsesNormalMatchCleanup()throws Exception {
        inMatchKick(RoomMode.NORMAL);
    }
    @Test void roleplayMatchCanBeMonitoredNotifiedAndKicked()throws Exception {
        inMatchKick(RoomMode.ROLEPLAY);
    }
    @Test void campaignCanBeMonitoredNotifiedAndDisconnected()throws Exception {
        try(Server s=new Server(new Config(config()))) {
            s.start();login(s);
            try(Socket player=socket(s)) {
                BaselineTest.auth(player,"campaign_admin_test");
                BaselineTest.send(player,CampaignSession.REQUEST,new Writer().u8(8).u8(0).u8(0).done());
                assertEquals(0,new Reader(BaselineTest.waitFor(player,CampaignSession.RESPONSE).payload()).u16());
                var row=state(s).getAsJsonArray("sessions").get(0).getAsJsonObject();
                assertEquals("CAMPAIGN",row.get("state").getAsString());
                assertEquals(1,state(s).getAsJsonObject("stats").get("playing").getAsInt());
                String id=row.get("id").getAsString();
                post(s,"/api/notice",Map.of("scope","session","target",id,"type","info","message","Campaign notice"));
                assertEquals(200,new Reader(BaselineTest.waitFor(player,NOTICE).payload()).u16());
                post(s,"/api/kick",Map.of("scope","session","target",id,"message","campaign kick"));
                eventually(()->s.connections.isEmpty()&&s.accounts.isEmpty()&&s.tokens.isEmpty());
            }
        }
    }
    void inMatchKick(RoomMode mode)throws Exception {
        try(Server s=new Server(new Config(config()))) {
            s.start();login(s);
            try(Socket a=socket(s);Socket b=socket(s);DatagramSocket ua=new DatagramSocket();DatagramSocket ub=new DatagramSocket()) {
                byte[] ta=BaselineTest.auth(a,"fighter_a"),tb=BaselineTest.auth(b,"fighter_b");
                BaselineTest.send(a,CREATE_ROOM,new Writer().str("Real UDP match").u8(2).u16(180).str("").u16(World.DEFAULT_HP).u8(mode.ordinal()).done());BaselineTest.waitFor(a,ACTION_RESULT);
                int id=new Reader(BaselineTest.waitFor(a,ROOM_STATE).payload()).u32();
                BaselineTest.send(b,JOIN_ROOM,new Writer().u32(id).done());BaselineTest.waitFor(b,ACTION_RESULT);
                if(mode==RoomMode.ROLEPLAY) {
                    BaselineTest.send(a,SELECT_NATION,new Writer().u8(0).done());NetworkRejoinTest.ok(a,ACTION_RESULT);
                    BaselineTest.send(b,SELECT_NATION,new Writer().u8(1).done());NetworkRejoinTest.ok(b,ACTION_RESULT);
                }
                BaselineTest.send(a,SET_READY,new Writer().u8(1).done());BaselineTest.waitFor(a,ACTION_RESULT);
                BaselineTest.send(b,SET_READY,new Writer().u8(1).done());BaselineTest.waitFor(b,ACTION_RESULT);
                BaselineTest.send(a,START_MATCH,new byte[0]);BaselineTest.waitFor(a,ACTION_RESULT);
                long match=new Reader(BaselineTest.waitFor(a,PREPARE).payload()).u64();BaselineTest.waitFor(b,PREPARE);
                BaselineTest.handshake(s,ua,ta,match);BaselineTest.handshake(s,ub,tb,match);
                Writer prepared=new Writer().u64(match);
                if(mode==RoomMode.ROLEPLAY)prepared.u8(PrepStage.LOADING.ordinal()).u32(RoleplayContent.Version).bytes(RoleplayMatch.CONTENT_HASH);
                BaselineTest.send(a,PREPARED,prepared.done());BaselineTest.send(b,PREPARED,prepared.done());
                if(mode==RoomMode.ROLEPLAY) {
                    NetworkRejoinTest.ok(a,ACTION_RESULT);NetworkRejoinTest.ok(b,ACTION_RESULT);
                    BaselineTest.waitFor(a,MATCH_LOADOUT);BaselineTest.waitFor(b,MATCH_LOADOUT);
                    RoleplayMatch loadout=s.rooms.get(id).roleplay;
                    byte[] confirmed=new Writer().u64(match).u8(PrepStage.SYNC_LOADOUT.ordinal()).u32(loadout.revision).bytes(loadout.loadoutHash).done();
                    BaselineTest.send(a,PREPARED,confirmed);BaselineTest.send(b,PREPARED,confirmed);
                }
                BaselineTest.waitFor(a,START);BaselineTest.waitFor(b,START);
                eventually(()->s.rooms.get(id).phase==RoomState.RUNNING);
                assertEquals(2,state(s).getAsJsonObject("stats").get("playing").getAsInt());
                post(s,"/api/notice",Map.of("scope","room","target",""+id,"type","warning","message","Thông báo trong trận"));
                assertEquals(201,new Reader(BaselineTest.waitFor(a,NOTICE).payload()).u16());
                Server.Session target=s.accounts.values().stream().filter(x->x.account.name().equals("fighter_a")).findFirst().orElseThrow();
                post(s,"/api/kick",Map.of("scope","session","target",target.id,"message","during match"));
                assertNull(s.tokens.get(HexFormat.of().formatHex(ta)));assertFalse(target.open);assertNull(target.endpoint);
                // An old authenticated UDP frame cannot revive the closed session.
                BaselineTest.udp(s,ua,INPUT,new Writer().bytes(ta).u64(match).u32(0).u8(1).u32(1).u8(8).u16(0).done());
                BaselineTest.waitFor(b,END);
                eventually(()->s.rooms.get(id).phase==RoomState.WAITING&&s.rooms.get(id).view.count()==1);
                assertEquals("WAITING",state(s).getAsJsonArray("sessions").get(0).getAsJsonObject().get("state").getAsString());
                assertEquals(1,s.connections.size());assertNull(target.endpoint);
            }
        }
    }
}
