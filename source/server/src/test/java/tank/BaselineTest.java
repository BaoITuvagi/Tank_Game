package tank;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import java.nio.file.*;
import java.net.*;
import java.util.*;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;
class BaselineTest {
 @TempDir Path tmp;
 @Test void accountLobbyMatchHistory()throws Exception{
  Path config=tmp.resolve("server.properties");String map=Path.of("../../source/client/Assets/StreamingAssets/map.json").toAbsolutePath().normalize().toString().replace("\\","/");Files.writeString(config,"bind=127.0.0.1\ntcp.port=0\nudp.port=0\nbenchmark=true\nnetwork.mode=BASELINE\nmatch.ticks=3\nmap.path="+map+"\ndatabase.path="+tmp.resolve("test.db").toString().replace("\\","/")+"\nspool.path="+tmp.resolve("spool").toString().replace("\\","/")+"\nmetrics.path="+tmp.resolve("metrics.csv").toString().replace("\\","/")+"\n");
  try(Server server=new Server(new Config(config))){server.start();try(Socket a=new Socket("127.0.0.1",server.tcp.getLocalPort());Socket b=new Socket("127.0.0.1",server.tcp.getLocalPort());DatagramSocket ua=new DatagramSocket();DatagramSocket ub=new DatagramSocket()){
   a.setSoTimeout(15000);b.setSoTimeout(15000);byte[] ta=auth(a,"player_a"),tb=auth(b,"player_b");send(a,CREATE_ROOM,new Writer().str("Phòng").done());assertEquals(0,new Reader(waitFor(a,ACTION_RESULT).payload()).u16());Reader state=new Reader(waitFor(a,ROOM_STATE).payload());int room=state.u32();send(b,JOIN_ROOM,new Writer().u32(room).done());assertEquals(0,new Reader(waitFor(b,ACTION_RESULT).payload()).u16());send(a,SET_READY,new Writer().u8(1).done());waitFor(a,ACTION_RESULT);send(b,SET_READY,new Writer().u8(1).done());waitFor(b,ACTION_RESULT);send(a,START_MATCH,new byte[0]);waitFor(a,ACTION_RESULT);Packet pa=waitFor(a,PREPARE);waitFor(b,PREPARE);long match=new Reader(pa.payload()).u64();handshake(server,ua,ta,match);handshake(server,ub,tb,match);send(a,PREPARED,new Writer().u64(match).done());send(b,PREPARED,new Writer().u64(match).done());waitFor(a,START);waitFor(b,START); // ACK traffic maintains liveness during countdown.
   for(int i=0;i<35;i++){udp(server,ua,ACK,new Writer().bytes(ta).u64(match).u32(0).done());udp(server,ub,ACK,new Writer().bytes(tb).u64(match).u32(0).done());Thread.sleep(100);}
   Packet ended=waitFor(a,END);assertEquals(match,new Reader(ended.payload()).u64());server.db.submit(()->null).get();send(a,HISTORY,new Writer().u16(0).u8(20).done());Reader h=new Reader(waitFor(a,HISTORY_RESULT).payload());assertEquals(0,h.u16());assertEquals(1,h.u8());assertEquals(match,h.u64());
  }}
 }
 static byte[] auth(Socket s,String name)throws Exception{byte[] creds=new Writer().str(name).str("password123").done();send(s,REGISTER,creds);assertEquals(0,new Reader(waitFor(s,REGISTER_RESULT).payload()).u16());send(s,LOGIN,creds);Reader r=new Reader(waitFor(s,LOGIN_RESULT).payload());assertEquals(0,r.u16());r.u64();r.str(24);return r.bytes(16);}
 static void send(Socket s,int op,byte[] p)throws Exception{s.getOutputStream().write(frame(op,1,p));}
 static Packet waitFor(Socket s,int op)throws Exception{for(int i=0;i<100;i++){Packet p=read(s.getInputStream());if(p.op()==op)return p;}throw new AssertionError("missing opcode "+op);}
 static void udp(Server s,DatagramSocket u,int op,byte[] p)throws Exception{byte[] f=frame(op,1,p);u.send(new DatagramPacket(f,f.length,InetAddress.getLoopbackAddress(),s.udp.getLocalPort()));}
 static void handshake(Server s,DatagramSocket u,byte[] token,long match)throws Exception{u.setSoTimeout(2000);udp(s,u,HANDSHAKE,new Writer().bytes(token).u64(match).u64(7).done());DatagramPacket d=new DatagramPacket(new byte[1200],1200);u.receive(d);assertEquals(HANDSHAKE_ACK,decode(Arrays.copyOf(d.getData(),d.getLength()),true).op());}
}
