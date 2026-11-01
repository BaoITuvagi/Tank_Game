package tank;
import java.net.*;
import java.nio.file.*;
import java.util.*;
import java.util.concurrent.*;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;
import tank.Protocol.Error;

class MatchChatTest {
 @TempDir Path tmp;
 RoomFeaturesTest helper(Path dir)throws Exception{var h=new RoomFeaturesTest();h.tmp=Files.createDirectories(dir);return h;}
 Room running(Server s,RoomMode mode,RoomFeaturesTest h)throws Exception {
  if(mode==RoomMode.ROLEPLAY){var rp=new RoleplayRoomTest();rp.tmp=h.tmp;Room r=rp.room(s,8);rp.running(r);return r;}
  Room r=h.directRoom(s,8,8,180);r.prepare(r.members.get(0).session,1);r.phase=RoomState.RUNNING;return r;
 }
 static List<Packet> packets(Server.Session s,int op) {return s.outbound.stream().map(raw->decode(raw,false)).filter(p->p.op()==op).toList();}
 static int result(Server.Session s) {return new Reader(packets(s,ACTION_RESULT).getLast().payload()).u16();}
 @Test void unicodeNormalizationAndLimits() {
  assertEquals("Xin chào   bạn",MatchChat.normalize(" \tXin chào\r\n\tbạn \n"));
  assertEquals("😀".repeat(200),MatchChat.normalize("😀".repeat(200)));
  for(String value:new String[]{"","  \r\n\t","a".repeat(201),"😀".repeat(201),"hi\u0000there","hi\u0085there"})assertThrows(IllegalArgumentException.class,()->MatchChat.normalize(value));
 }
 @Test void allColorsAuthoritativeNamesDeadPlayersAndRoomIsolation()throws Exception {
  for(RoomMode mode:RoomMode.values()) {
   var h=helper(tmp.resolve(mode.name()));try(Server s=h.server()) {
    Room r=running(s,mode,h);for(var m:r.members.values())m.session.outbound.clear();
    var outside=s.new Session(new Socket());outside.account=r.members.get(0).account;outside.room=new Room(s,99,"other");
    for(var m:r.members.values()) {
     m.displayName="Tên trùng <color=red>";if(mode==RoomMode.ROLEPLAY)m.roleplay.nation=7-m.slot;
     r.world.tanks.get(m.slot).hp=0;
     long activity=m.activity;r.chat.send(m.session,r.match," Xin chào ",20+m.slot);assertEquals(activity,m.activity);
     assertEquals(0,result(m.session));var message=r.chat.messages.getLast();assertEquals(m.slot,message.slot());assertEquals(mode==RoomMode.ROLEPLAY?7-m.slot:m.slot,message.palette());assertEquals(m.displayName,message.name());
    }
    for(var m:r.members.values())assertEquals(8,packets(m.session,CHAT_MESSAGES).size());
    assertTrue(outside.outbound.isEmpty());
    var sender=r.members.get(0);r.chat.send(sender.session,r.match,"spam",40);assertEquals(Error.RATE_LIMITED.ordinal(),result(sender.session));
    r.chat.send(sender.session,r.match+1,"stale",41);assertEquals(Error.INVALID_STATE.ordinal(),result(sender.session));
    r.chat.send(outside,r.match,"outside",42);assertEquals(Error.INVALID_STATE.ordinal(),result(outside));
    sender.lastChat=0;r.chat.send(sender.session,r.match,"\u0000bad",43);assertEquals(Error.INVALID_REQUEST.ordinal(),result(sender.session));assertEquals(8,r.chat.messages.size());
   }
  }
 }
 @Test void replayRestoresLastHundredInBoundedFramesAfterBothRejoinPaths()throws Exception {
  for(RoomMode mode:RoomMode.values()) {
   var h=helper(tmp.resolve(mode.name()));try(Server s=h.server()) {
    Room r=running(s,mode,h);var sender=r.members.get(0);sender.displayName="Người đã rời";
    for(int i=0;i<105;i++){sender.lastChat=0;r.chat.send(sender.session,r.match,"😀".repeat(200),1);for(var m:r.members.values())m.session.outbound.clear();}
    int palette=r.chat.messages.getFirst().palette();r.leave(sender.session,0);
    var returning=r.members.get(7);r.leave(returning.session,0);
    var replacement=s.new Session(new Socket());replacement.account=returning.account;replacement.room=r;r.join(replacement,2);
    r.chat.send(replacement,r.match,"too early",3);assertEquals(Error.INVALID_STATE.ordinal(),result(replacement));
    replacement.endpoint=new InetSocketAddress(InetAddress.getLoopbackAddress(),30100);
    if(mode==RoomMode.ROLEPLAY){r.roleplay.prepared(replacement,r.match,PrepStage.LOADING.ordinal(),RoleplayContent.Version,RoleplayMatch.CONTENT_HASH,4);r.roleplay.prepared(replacement,r.match,PrepStage.SYNC_LOADOUT.ordinal(),r.roleplay.revision,r.roleplay.loadoutHash,5);}
    else r.prepared(replacement,r.match,4);
    var replay=packets(replacement,CHAT_MESSAGES);assertEquals(10,replay.size());int expected=6;
    for(var packet:replay){assertTrue(packet.payload().length<=16384);var reader=new Reader(packet.payload());assertEquals(r.match,reader.u64());assertEquals(10,reader.u8());for(int i=0;i<10;i++){assertEquals(expected++,reader.u32());assertEquals(0,reader.u8());assertEquals(palette,reader.u8());assertEquals("Người đã rời",reader.str(96));assertEquals("😀".repeat(200),reader.str(800));}reader.end();}
    assertEquals(106,expected);r.finish(0);assertTrue(r.chat.messages.isEmpty());assertEquals(0,r.chat.nextId);
   }
  }
 }
 @Test void realTcpRequestBroadcastsOnlyToSameMatchAndRejectsMalformedPayload()throws Exception {
  var h=helper(tmp);try(Server s=h.server()) {
   s.start();try(Socket a=new Socket("127.0.0.1",s.tcp.getLocalPort());Socket b=new Socket("127.0.0.1",s.tcp.getLocalPort());Socket c=new Socket("127.0.0.1",s.tcp.getLocalPort())) {
    for(var socket:List.of(a,b,c))socket.setSoTimeout(3000);
    byte[] token=BaselineTest.auth(a,"chat_host");BaselineTest.auth(b,"chat_peer");BaselineTest.auth(c,"chat_other");
    BaselineTest.send(a,CREATE_ROOM,new Writer().str("Chat").u8(2).u16(60).done());BaselineTest.waitFor(a,ACTION_RESULT);int id=new Reader(BaselineTest.waitFor(a,ROOM_STATE).payload()).u32();
    BaselineTest.send(b,JOIN_ROOM,new Writer().u32(id).done());BaselineTest.waitFor(b,ACTION_RESULT);
    var host=s.tokens.get(HexFormat.of().formatHex(token));Room r=s.rooms.get(id);var ready=new CompletableFuture<Long>();
    r.enqueue(()->{for(var m:r.members.values())m.ready=true;r.prepare(host,900);r.phase=RoomState.RUNNING;for(var m:r.members.values())m.session.lastUdp=System.nanoTime();ready.complete(r.match);});long match=ready.get(3,TimeUnit.SECONDS);
    BaselineTest.send(a,CHAT_SEND,new Writer().u64(match).str("Chào bạn <b>!").done());
    assertEquals(0,new Reader(BaselineTest.waitFor(a,ACTION_RESULT).payload()).u16());
    byte[] sent=BaselineTest.waitFor(a,CHAT_MESSAGES).payload();assertArrayEquals(sent,BaselineTest.waitFor(b,CHAT_MESSAGES).payload());
    BaselineTest.send(c,CHAT_SEND,new Writer().u64(match).str("outside").done());assertNotEquals(0,new Reader(BaselineTest.waitFor(c,ACTION_RESULT).payload()).u16());
    BaselineTest.send(a,CHAT_SEND,new Writer().u64(match).str("x").u8(99).done());assertEquals(Error.INVALID_REQUEST.ordinal(),new Reader(BaselineTest.waitFor(a,ACTION_RESULT).payload()).u16());
   }
  }
 }
}
