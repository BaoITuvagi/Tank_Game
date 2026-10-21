package tank;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import java.nio.file.*;
import java.net.*;
import java.util.*;
import static tank.Protocol.*;
import static org.junit.jupiter.api.Assertions.*;
class RoomRefinementTest {
 @TempDir Path tmp;
 Server server()throws Exception{Path c=tmp.resolve("config");Files.writeString(c,"bind=127.0.0.1\ntcp.port=0\nudp.port=0\nbenchmark=true\nmap.path="+Path.of("../client/Assets/StreamingAssets/map.json").toAbsolutePath().normalize().toString().replace('\\','/')+"\ndatabase.path="+tmp.resolve("db").toString().replace('\\','/')+"\nspool.path="+tmp.resolve("spool").toString().replace('\\','/')+"\nmetrics.path="+tmp.resolve("metrics").toString().replace('\\','/')+"\n");return new Server(new Config(c));}
 @Test void lockedRoomRejectsWrongPasswordAndAcceptsCorrectPassword()throws Exception{
  try(Server s=server()){s.start();try(Socket a=new Socket("127.0.0.1",s.tcp.getLocalPort());Socket b=new Socket("127.0.0.1",s.tcp.getLocalPort())){
   a.setSoTimeout(5000);b.setSoTimeout(5000);BaselineTest.auth(a,"lockhost");BaselineTest.auth(b,"lockjoin");
   BaselineTest.send(a,CREATE_ROOM,new Writer().str("Private").u8(4).u16(180).str("friends").done());assertEquals(0,new Reader(BaselineTest.waitFor(a,ACTION_RESULT).payload()).u16());int room=new Reader(BaselineTest.waitFor(a,ROOM_STATE).payload()).u32();
   for(String password:List.of("","wrong")){BaselineTest.send(b,JOIN_ROOM,new Writer().u32(room).str(password).done());assertEquals((password.isEmpty()?Protocol.Error.ROOM_PASSWORD_REQUIRED:Protocol.Error.ROOM_PASSWORD_INCORRECT).ordinal(),new Reader(BaselineTest.waitFor(b,ACTION_RESULT).payload()).u16());assertEquals(1,s.rooms.get(room).members.size());}
   BaselineTest.send(b,JOIN_ROOM,new Writer().u32(room).str("friends").done());assertEquals(0,new Reader(BaselineTest.waitFor(b,ACTION_RESULT).payload()).u16());assertEquals(2,s.rooms.get(room).members.size());
  }}
 }
 @Test void onlyHostCanEditAndSettingsResetReadiness()throws Exception{
  try(Server s=server()){Room room=new Room(s,1,"Before",4,5400);s.rooms.put(1,room);for(int i=0;i<2;i++){String name="edit"+i;var a=s.db.submit(()->s.db.register(name,"password123")).get();var session=s.new Session(new Socket());session.account=a;session.room=room;room.join(session,1);room.members.get(i).ready=true;}
   room.configure(room.members.get(1).session,2,"Wrong",8,60,1,"");assertEquals("Before",room.name);
   room.configure(room.members.get(0).session,3,"After",2,300,2,"newpass");assertEquals("After",room.name);assertEquals(2,room.capacity);assertEquals(9000,room.matchTicks);assertTrue(room.access.matches("newpass"));assertFalse(room.members.values().stream().anyMatch(m->m.ready));
   room.configure(room.members.get(0).session,4,"Open",2,60,1,"");assertFalse(room.access.locked);assertTrue(room.access.matches(""));
   room.phase=RoomState.RUNNING;room.configure(room.members.get(0).session,5,"No",8,180,1,"");assertEquals("Open",room.name);
  }
 }
 @Test void displayNamePersistsWithoutChangingLoginIdentity()throws Exception{
  Path db=tmp.resolve("profiles.db");long id;
  try(Database d=new Database(db.toString(),tmp.resolve("spool").toString())){var a=d.submit(()->d.register("account","password123")).get();id=a.id();assertEquals("Bao Tank",d.submit(()->d.setDisplayName(id,"Bao Tank")).get());assertEquals("account",d.submit(()->d.login("account","password123")).get().name());}
  try(Database d=new Database(db.toString(),tmp.resolve("spool").toString())){assertEquals("Bao Tank",d.submit(()->d.displayName(id,"account")).get());}
 }
 @Test void duplicateDisplayNamesStillHaveDistinctOwnSlots()throws Exception{
  try(Server s=server()){Room room=new Room(s,1,"Aliases");for(int i=0;i<2;i++){String name="alias"+i;var a=s.db.submit(()->s.db.register(name,"password123")).get();var session=s.new Session(new Socket());session.account=a;session.nickname="Same";session.room=room;room.join(session,1);}
   for(var member:room.members.values()){Packet latest=member.session.outbound.stream().map(f->decode(f,false)).filter(p->p.op()==ROOM_STATE).reduce((a,b)->b).orElseThrow();assertEquals(member.slot,Byte.toUnsignedInt(latest.payload()[latest.payload().length-3]));}
  }
 }
}
