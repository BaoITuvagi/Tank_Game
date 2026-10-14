package tank;

import java.net.Socket;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;

/** Fault injection uses aged monotonic timestamps, never shortened production timeouts. */
class ReliabilityTest {
 @TempDir Path tmp;

 Server server() throws Exception {
  Path config=tmp.resolve("server.properties");
  Files.writeString(config,"bind=127.0.0.1\ntcp.port=0\nudp.port=0\nbenchmark=true\nmap.path="+
   Path.of("../client/Assets/StreamingAssets/map.json").toAbsolutePath().normalize().toString().replace('\\','/')+
   "\ndatabase.path="+tmp.resolve("test.db").toString().replace('\\','/')+
   "\nspool.path="+tmp.resolve("spool").toString().replace('\\','/')+
   "\nmetrics.path="+tmp.resolve("metrics.csv").toString().replace('\\','/')+"\n");
  return new Server(new Config(config));
 }

 Room room(Server server) throws Exception {
  Room room=new Room(server,1,"Reliability");
  server.rooms.put(1,room);
  for(int slot=0;slot<3;slot++) {
   final String name="fault"+slot;
   var account=server.db.submit(()->server.db.register(name,"password123")).get();
   var session=server.new Session(new Socket());
   session.account=account;
   session.room=room;
   room.join(session,1);
   room.members.get(slot).ready=true;
  }
  room.prepare(room.members.get(0).session,2);
  room.phase=RoomState.RUNNING;
  room.startedUtc=System.currentTimeMillis();
  refreshUdp(room);
  clearOutbound(room);
  return room;
 }

 void refreshUdp(Room room) {for(var m:room.members.values())m.session.lastUdp=System.nanoTime();}
 void clearOutbound(Room room) {for(var m:room.members.values())m.session.outbound.clear();}
 List<Packet> packets(Server.Session session) {
  List<Packet> result=new ArrayList<>();byte[] frame;
  while((frame=session.outbound.poll())!=null)result.add(decode(frame,false));
  return result;
 }

 @Test void idlePacketsDoNotPreventWarningOrRemoval() throws Exception {
  try(Server server=server()) {
   Room room=room(server);var idle=room.members.get(0);
   idle.activity=System.nanoTime()-61_000_000_000L;
   room.input(idle.session,0,List.of(new World.Input(1,0,0)));
   room.tick();
   assertTrue(idle.afkWarn);
   assertTrue(packets(idle.session).stream().anyMatch(p->p.op()==NOTICE&&new Reader(p.payload()).u16()==100));
   idle.activity=System.nanoTime()-76_000_000_000L;idle.afkSince=System.nanoTime()-16_000_000_000L;
   room.input(idle.session,0,List.of(new World.Input(2,0,0)));
   refreshUdp(room);room.tick();
   assertNull(idle.session.room);
   assertEquals(1,room.host);
   assertFalse(room.world.tanks.containsKey(0));
   Packet removed=packets(idle.session).stream().filter(p->p.op()==REMOVED).findFirst().orElseThrow();
   Reader r=new Reader(removed.payload());r.u32();r.u8();assertEquals(3,r.u8());
  }
 }

 @Test void meaningfulInputCancelsAfkAndDeathCannotPauseAnExistingWarning() throws Exception {
  try(Server server=server()) {
   Room room=room(server);var moving=room.members.get(0);var dead=room.members.get(1);
   moving.activity=dead.activity=System.nanoTime()-121_000_000_000L;
   moving.afkWarn=true;
   room.world.tanks.get(1).hp=0;room.world.tanks.get(1).respawn=100;
   room.input(moving.session,0,List.of(new World.Input(1,8,0)));
   room.tick();
   assertEquals(3,room.members.size());assertFalse(moving.afkWarn);
   assertTrue(System.nanoTime()-dead.activity>119_000_000_000L,"Death must not reset accumulated inactivity");
   // The countdown expires even before respawn.
   dead.afkWarn=true;dead.afkSince=System.nanoTime()-16_000_000_000L;refreshUdp(room);
   room.input(moving.session,0,List.of(new World.Input(2,8,0)));room.tick();
   assertFalse(room.members.containsKey(1));assertTrue(room.members.containsKey(0));
  }
 }

 @Test void prepareClearsWarningFromPreviousMatch() throws Exception {
  try(Server server=server()) {
   Room room=room(server);var member=room.members.get(0);
   room.phase=RoomState.WAITING;member.afkWarn=true;member.previousAim=18000;
   room.prepare(member.session,3);
   assertFalse(member.afkWarn);assertEquals(room.world.tanks.get(0).turret,member.previousAim);
  }
 }

 @Test void slowConsumerOverflowDisconnectsAndRoomCleansUp() throws Exception {
  try(Server server=server()) {
   Room room=room(server);var slow=room.members.get(0).session;
   for(int i=0;i<128;i++)slow.event(HEARTBEAT_ACK,new Writer().u64(i).done());
   assertTrue(slow.open);
   slow.event(HEARTBEAT_ACK,new Writer().u64(128).done());
   assertFalse(slow.open);assertEquals(128,slow.outbound.size());
   room.tick();assertFalse(room.members.containsKey(0));assertEquals(1,room.host);
   assertEquals(RoomState.RUNNING,room.phase);
  }
 }

 @Test void actualSocketThatNeverReadsIsDisconnectedWithoutStoppingOtherClients() throws Exception {
  try(Server server=server();Socket unread=new Socket()) {
   unread.setReceiveBufferSize(1024);
   unread.connect(new java.net.InetSocketAddress("127.0.0.1",server.tcp.getLocalPort()));
   Socket accepted=server.tcp.accept();accepted.setSendBufferSize(1024);
   var slow=server.new Session(accepted);slow.start();
   try {
    byte[] payload=new byte[16384];
    long deadline=System.nanoTime()+5_000_000_000L;
    while(slow.open&&System.nanoTime()<deadline){slow.event(NOTICE,payload);Thread.sleep(1);}
    assertFalse(slow.open,"non-reading client must overflow its bounded outbound queue");
    assertTrue(accepted.isClosed());
    try(Socket healthy=new Socket("127.0.0.1",server.tcp.getLocalPort())) {
     healthy.setSoTimeout(2000);
     var other=server.new Session(server.tcp.accept());other.start();
     try {
      BaselineTest.send(healthy,HEARTBEAT,new Writer().u64(42).done());
      assertEquals(42,new Reader(BaselineTest.waitFor(healthy,HEARTBEAT_ACK).payload()).u64());
     } finally {other.close();}
    }
   } finally {slow.close();}
  }
 }

 @Test void udpTimeoutEndsMatchAndPreservesDisconnectedScore() throws Exception {
  try(Server server=server()) {
   Room room=room(server);var survivor=room.members.get(2).session;
   room.members.get(0).session.lastUdp=room.members.get(1).session.lastUdp=System.nanoTime()-11_000_000_000L;
   room.tick();assertEquals(RoomState.RESULTS,room.phase);
   Packet end=packets(survivor).stream().filter(p->p.op()==END).findFirst().orElseThrow();
   Reader r=new Reader(end.payload());assertEquals(room.match,r.u64());assertEquals(1,r.u8());assertEquals(3,r.u8());
   int disconnected=0;for(int i=0;i<3;i++){r.u8();r.str(24);r.u16();r.u16();r.u32();r.u8();if(r.bool())disconnected++;}
   assertEquals(2,disconnected);r.end();
   assertEquals(1,server.db.submit(()->server.db.history(survivor.account.id(),0,20)).get().size());
  }
 }

 @Test void shutdownDoesNotDependOnRoomQueueCapacity() throws Exception {
  try(Server server=server()) {
   Room room=room(server);var survivor=room.members.get(0).session;
   for(int i=0;i<256;i++)room.enqueue(()->{});
   room.stop();assertFalse(room.alive);assertEquals(RoomState.RESULTS,room.phase);
   assertEquals(2,server.db.submit(()->server.db.history(survivor.account.id(),0,20)).get().get(0).reason());
  }
 }
}
