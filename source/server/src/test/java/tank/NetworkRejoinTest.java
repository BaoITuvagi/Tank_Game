package tank;
import java.net.*;
import java.nio.file.*;
import java.util.*;
import java.util.concurrent.*;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;

class NetworkRejoinTest {
 @TempDir Path tmp;
 RoomFeaturesTest helper(Path directory)throws Exception{var h=new RoomFeaturesTest();h.tmp=Files.createDirectories(directory);return h;}
 @Test void voluntaryDisconnectAndUdpTimeoutCanRejoinWithOriginalScoreAndLoadout()throws Exception{
  for(RoomMode mode:RoomMode.values())for(int reason=0;reason<=3;reason++){
   var h=helper(tmp.resolve(mode+"-"+reason));try(Server s=h.server()){
    Room r;
    if(mode==RoomMode.ROLEPLAY){var rp=new RoleplayRoomTest();rp.tmp=h.tmp;r=rp.room(s,3);rp.running(r);}
    else {r=h.directRoom(s,3,3,180);r.prepare(r.members.get(0).session,1);r.phase=RoomState.RUNNING;r.broadcast();}
    var member=r.members.get(2);var tank=r.world.tanks.get(2);tank.kills=7;tank.deaths=4;tank.lastKill=90;tank.qReadyTick=1200;
    tank.qBaseReadyTick=900;tank.qPenalty=300;tank.qPenaltySources.add(0);
    tank.armorEndTick=1000;tank.invincibilityEndTick=900;tank.mirrorShots=3;
    long match=r.match;r.world.bricks[0]=0;byte[] bricks=r.world.bricks.clone();
    if(reason==1){member.session.close();for(var m:r.members.values())m.session.lastUdp=System.nanoTime();r.tick();}
    else if(reason==2){for(var m:r.members.values())m.session.lastUdp=System.nanoTime();member.session.lastUdp-=11_000_000_000L;r.tick();}
    else r.leave(member.session,reason);
    assertEquals(RoomState.RUNNING,r.phase);
    var replacement=s.new Session(new Socket());replacement.account=member.account;assertTrue(r.canRejoin(replacement),mode+" leave reason "+reason);replacement.room=r;r.join(replacement,20);
    assertSame(replacement,member.session);assertTrue(member.rejoining);assertEquals(3,r.participants.size());
    replacement.endpoint=new InetSocketAddress(InetAddress.getLoopbackAddress(),30100);
    if(mode==RoomMode.ROLEPLAY){
     r.roleplay.prepared(replacement,match,PrepStage.LOADING.ordinal(),RoleplayContent.Version,RoleplayMatch.CONTENT_HASH,21);
     assertTrue(member.roleplay.awaitingLoadout);
     r.roleplay.prepared(replacement,match,PrepStage.SYNC_LOADOUT.ordinal(),r.roleplay.revision,r.roleplay.loadoutHash,22);
    }
    else r.prepared(replacement,match,21);
    assertFalse(member.rejoining);tank=r.world.tanks.get(2);assertNotNull(tank);
    assertEquals(7,tank.kills);assertEquals(4,tank.deaths);assertEquals(90,tank.lastKill);assertEquals(match,r.match);assertArrayEquals(bricks,r.world.bricks);
    assertEquals(1000,tank.armorEndTick);assertEquals(900,tank.invincibilityEndTick);assertEquals(3,tank.mirrorShots);
    if(mode==RoomMode.ROLEPLAY){
     assertEquals(2,tank.nation);assertEquals(1200,tank.qReadyTick);assertEquals(900,tank.qBaseReadyTick);assertEquals(Set.of(0),tank.qPenaltySources);
     r.world.items.apply(tank,WorldItems.PURIFICATION);assertEquals(900,tank.qReadyTick);assertEquals(0,tank.qPenalty);assertEquals(3,tank.mirrorShots);
    }
    r.leave(replacement,0);assertTrue(r.canRejoin(replacement));assertEquals(7,member.resultTank.kills);
   }
  }
 }
 @Test void removedPlayerAndNonParticipantCannotEnterAnOngoingMatch()throws Exception{
  var h=helper(tmp);try(Server s=h.server()){
   Room r=h.directRoom(s,3,3,180);r.prepare(r.members.get(0).session,1);r.phase=RoomState.RUNNING;r.broadcast();var member=r.members.get(2);r.leave(member.session,4);
   assertFalse(r.canRejoin(member.session));member.session.room=r;r.join(member.session,2);assertNull(member.session.room);
   var outsider=s.new Session(new Socket());outsider.account=s.db.submit(()->s.db.register("outsider","password123")).get();outsider.room=r;
   assertFalse(r.canRejoin(outsider));r.join(outsider,3);assertNull(outsider.room);assertEquals(2,r.members.size());
  }
 }
 @Test void tcpLeaveRejoinLogoutLoginAndUdpTimeoutKeepRoomUsable()throws Exception{
  for(RoomMode mode:RoomMode.values()){
   var h=helper(tmp.resolve("tcp-"+mode));try(Server s=h.server()){
    s.start();try(Socket a=new Socket("127.0.0.1",s.tcp.getLocalPort());Socket b=new Socket("127.0.0.1",s.tcp.getLocalPort());Socket c=new Socket("127.0.0.1",s.tcp.getLocalPort());DatagramSocket udp=new DatagramSocket()){
     for(var socket:List.of(a,b,c))socket.setSoTimeout(3000);
     byte[] hostToken=BaselineTest.auth(a,"net_host");BaselineTest.auth(b,"net_peer");byte[] token=BaselineTest.auth(c,"net_return");
     var host=s.tokens.get(HexFormat.of().formatHex(hostToken));var returning=s.tokens.get(HexFormat.of().formatHex(token));
     BaselineTest.send(a,CREATE_ROOM,new Writer().str("Network recovery").u8(3).u16(60).str("").u16(100).u8(mode.ordinal()).done());ok(a,ACTION_RESULT);
     int id=new Reader(BaselineTest.waitFor(a,ROOM_STATE).payload()).u32();
     for(var socket:List.of(b,c)){BaselineTest.send(socket,JOIN_ROOM,new Writer().u32(id).done());ok(socket,ACTION_RESULT);}
     Room r=s.rooms.get(id);var armed=new CompletableFuture<Long>();
     r.enqueue(()->{
      for(var m:r.members.values()){m.ready=true;m.roleplay.nation=m.slot;m.session.lastUdp=System.nanoTime();}
      r.prepare(host,900);if(r.roleplay!=null){r.roleplay.publishLoadout(Server.nowUs());r.roleplay.stage=PrepStage.COUNTDOWN;}
      r.phase=RoomState.RUNNING;r.startUs=Server.nowUs()-1;r.startedUtc=System.currentTimeMillis();r.world.tanks.get(2).kills=6;r.world.tanks.get(2).qReadyTick=1200;r.broadcast();armed.complete(r.match);
     });
     long match=armed.get(3,TimeUnit.SECONDS);BaselineTest.waitFor(c,PREPARE);
     BaselineTest.send(c,LEAVE_ROOM,new byte[0]);BaselineTest.waitFor(c,REMOVED);ok(c,ACTION_RESULT);assertListedRejoinable(c,id);
     rejoin(s,c,udp,token,id,match,mode);
     BaselineTest.send(c,LOGOUT,new byte[0]);ok(c,LOGOUT_RESULT);assertNull(returning.account);assertNull(returning.room);
     BaselineTest.send(c,LOGIN,new Writer().str("net_return").str("password123").done());var login=new Reader(BaselineTest.waitFor(c,LOGIN_RESULT).payload());assertEquals(0,login.u16());login.u64();login.str(96);token=login.bytes(16);
     assertListedRejoinable(c,id);rejoin(s,c,udp,token,id,match,mode);
     r.enqueue(()->{for(var m:r.members.values())m.session.lastUdp=System.nanoTime();r.member(returning).session.lastUdp-=11_000_000_000L;});
     BaselineTest.waitFor(c,REMOVED);assertListedRejoinable(c,id);rejoin(s,c,udp,token,id,match,mode);
     var checked=new CompletableFuture<Void>();r.enqueue(()->{
      try{assertEquals(RoomState.RUNNING,r.phase);assertEquals(match,r.match);assertEquals(3,r.participants.size());assertEquals(6,r.world.tanks.get(2).kills);if(mode==RoomMode.ROLEPLAY)assertEquals(1200,r.world.tanks.get(2).qReadyTick);assertTrue(r.thread.isAlive());checked.complete(null);}catch(Throwable failure){checked.completeExceptionally(failure);}
     });checked.get(3,TimeUnit.SECONDS);
    }
   }
  }
 }
 static void ok(Socket socket,int op)throws Exception{assertEquals(0,new Reader(BaselineTest.waitFor(socket,op).payload()).u16());}
 static void assertListedRejoinable(Socket socket,int id)throws Exception{
  BaselineTest.send(socket,ROOM_LIST,new byte[0]);var list=new Reader(BaselineTest.waitFor(socket,ROOM_LIST_RESULT).payload());assertEquals(0,list.u16());assertEquals(1,list.u16());assertEquals(id,list.u32());list.str(48);assertEquals(2,list.u8());assertEquals(3,list.u8());assertEquals(RoomState.RUNNING.ordinal(),list.u8());list.bool();assertTrue(list.bool());list.u8();list.end();
 }
 static void rejoin(Server server,Socket socket,DatagramSocket udp,byte[] token,int id,long match,RoomMode mode)throws Exception{
  BaselineTest.send(socket,JOIN_ROOM,new Writer().u32(id).done());ok(socket,ACTION_RESULT);assertEquals(match,new Reader(BaselineTest.waitFor(socket,PREPARE).payload()).u64());
  udp.setSoTimeout(2000);BaselineTest.udp(server,udp,HANDSHAKE,new Writer().bytes(token).u64(match).u64(7).done());
  // Snapshots from the previous attempt may still be queued on the same UDP socket.
  boolean acknowledged=false;for(int i=0;i<200&&!acknowledged;i++){var datagram=new DatagramPacket(new byte[1200],1200);udp.receive(datagram);acknowledged=decode(Arrays.copyOf(datagram.getData(),datagram.getLength()),true).op()==HANDSHAKE_ACK;}assertTrue(acknowledged);
  var prepared=new Writer().u64(match);
  if(mode==RoomMode.ROLEPLAY)prepared.u8(PrepStage.LOADING.ordinal()).u32(RoleplayContent.Version).bytes(RoleplayMatch.CONTENT_HASH);
  BaselineTest.send(socket,PREPARED,prepared.done());ok(socket,ACTION_RESULT);
  if(mode==RoomMode.ROLEPLAY){var loadout=server.rooms.get(id).roleplay;BaselineTest.send(socket,PREPARED,new Writer().u64(match).u8(PrepStage.SYNC_LOADOUT.ordinal()).u32(loadout.revision).bytes(loadout.loadoutHash).done());ok(socket,ACTION_RESULT);}
  assertEquals(match,new Reader(BaselineTest.waitFor(socket,START).payload()).u64());
 }
}
