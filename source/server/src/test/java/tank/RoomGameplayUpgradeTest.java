package tank;
import java.net.*;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;

class RoomGameplayUpgradeTest {
 @TempDir Path tmp;
 RoomFeaturesTest helper(){var h=new RoomFeaturesTest();h.tmp=tmp;return h;}
 void refresh(Room room){for(var m:room.members.values())m.session.lastUdp=System.nanoTime();}
 @Test void everyRoomSizeUsesLargeMapAndCanImmediatelyStartFreshMatches()throws Exception{
  for(int count:new int[]{2,3,4,8}){
   var h=helper();h.tmp=Files.createDirectories(tmp.resolve("players-"+count));
   try(Server s=h.server()){
    Room room=h.directRoom(s,count,count,60);var host=room.members.get(0).session;
    Set<String> assignments=new HashSet<>();Set<Long> seeds=new HashSet<>();String previous="";
    for(int match=0;match<3;match++){
     room.prepare(host,2);assertEquals(RoomState.PREPARING,room.phase);assertEquals(2,room.arenaMap.mapId);assertArrayEquals(s.largeMap.hash,room.arenaMap.hash);assertTrue(seeds.add(room.seed));
     String assignment="";for(var t:room.world.tanks.values()){assertEquals(0,t.qReadyTick);assignment+=t.x+","+t.y+";";}assertNotEquals(previous,assignment);previous=assignment;assignments.add(assignment);
     for(var m:room.members.values()){
      Packet prepare=m.session.outbound.stream().map(raw->decode(raw,false)).filter(p->p.op()==PREPARE).reduce((a,b)->b).orElseThrow();
      Reader r=new Reader(prepare.payload());assertEquals(room.match,r.u64());r.u32();r.u8();assertEquals(2,r.u16());assertArrayEquals(s.largeMap.hash,r.bytes(32));
     }
     room.phase=RoomState.RUNNING;room.world.tanks.get(0).qReadyTick=999;
     room.world.skillIntents.add(()->{throw new AssertionError("old skill intent");});
     room.finish(0);assertEquals(RoomState.WAITING,room.phase);assertNull(room.world);assertTrue(room.participants.isEmpty());
     assertTrue(room.members.values().stream().noneMatch(m->m.ready));
     for(var m:room.members.values())room.ready(m.session,true,3);
    }
    assertTrue(assignments.size()>1);
   }
  }
 }
 @Test void warningAlwaysAllowsFifteenSecondsBeforeRemoval()throws Exception{
  var h=helper();try(Server s=h.server()){Room room=h.directRoom(s,3,4,180);room.prepare(room.members.get(0).session,2);room.phase=RoomState.RUNNING;
   var idle=room.members.get(0);idle.activity=System.nanoTime()-90_000_000_000L;refresh(room);room.tick();
   assertTrue(idle.afkWarn);assertTrue(room.members.containsKey(0));assertTrue(System.nanoTime()-idle.afkSince<1_000_000_000L);
   idle.afkSince=System.nanoTime()-14_000_000_000L;refresh(room);room.tick();assertTrue(room.members.containsKey(0));
   idle.afkSince=System.nanoTime()-15_100_000_000L;refresh(room);room.tick();assertFalse(room.members.containsKey(0));assertNull(idle.session.room);
  }
 }
 @Test void deadTimeDoesNotPauseGraceAndInputCancelsIt()throws Exception{
  var h=helper();try(Server s=h.server()){Room room=h.directRoom(s,3,4,180);room.prepare(room.members.get(0).session,2);room.phase=RoomState.RUNNING;var idle=room.members.get(0);
   idle.afkWarn=true;idle.activity=System.nanoTime()-70_000_000_000L;idle.afkSince=System.nanoTime()-10_000_000_000L;idle.activityCheck=System.nanoTime()-5_000_000_000L;
   var tank=room.world.tanks.get(0);tank.hp=0;tank.respawn=100;refresh(room);room.tick();
   assertTrue(idle.afkWarn);assertTrue(System.nanoTime()-idle.afkSince>=10_000_000_000L);assertTrue(room.members.containsKey(0));
   tank.hp=3;room.input(idle.session,0,List.of(new World.Input(2,40,0)));refresh(room);room.tick();assertFalse(idle.afkWarn);assertEquals(0,idle.afkSince);
  }
 }
 @Test void startingHpCanOnlyBeChangedByHostWhileWaitingAndResetsReady()throws Exception{
  var h=helper();try(Server s=h.server()){Room room=h.directRoom(s,2,4,180);var host=room.members.get(0).session;var peer=room.members.get(1).session;
   room.configure(peer,2,"No",4,180,0,"",10);assertEquals(100,room.startingHp);
   for(int hp:new int[]{0,501}){room.configure(host,3,"No",4,180,0,"",hp);assertEquals(100,room.startingHp);}
   room.configure(host,4,"Ten HP",4,180,0,"",10);assertEquals(10,room.startingHp);assertTrue(room.members.values().stream().noneMatch(m->m.ready));
   for(var m:room.members.values())m.ready=true;room.prepare(host,5);for(var t:room.world.tanks.values()){assertEquals(10,t.hp);assertEquals(10,t.maxHp);}
   room.configure(host,6,"No",4,180,0,"",20);assertEquals(10,room.startingHp);
  }
 }
 @Test void createAndEditHpTravelOverRealTcpAndReachEachMember()throws Exception{
  var h=helper();try(Server s=h.server()){s.start();try(Socket a=new Socket("127.0.0.1",s.tcp.getLocalPort());Socket b=new Socket("127.0.0.1",s.tcp.getLocalPort())){
   a.setSoTimeout(5000);b.setSoTimeout(5000);BaselineTest.auth(a,"hphost");BaselineTest.auth(b,"hpjoin");
   for(int hp:new int[]{0,501}){BaselineTest.send(a,CREATE_ROOM,new Writer().str("HP").u8(4).u16(180).str("").u16(hp).done());assertEquals(Protocol.Error.INVALID_REQUEST.ordinal(),new Reader(BaselineTest.waitFor(a,ACTION_RESULT).payload()).u16());assertTrue(s.rooms.isEmpty());}
   BaselineTest.send(a,CREATE_ROOM,new Writer().str("HP").u8(4).u16(180).str("").u16(500).done());assertEquals(0,new Reader(BaselineTest.waitFor(a,ACTION_RESULT).payload()).u16());
   byte[] state=BaselineTest.waitFor(a,ROOM_STATE).payload();assertEquals(500,new Reader(Arrays.copyOfRange(state,state.length-2,state.length)).u16());int id=new Reader(state).u32();
   BaselineTest.send(b,JOIN_ROOM,new Writer().u32(id).done());BaselineTest.waitFor(b,ACTION_RESULT);state=BaselineTest.waitFor(b,ROOM_STATE).payload();assertEquals(500,new Reader(Arrays.copyOfRange(state,state.length-2,state.length)).u16());BaselineTest.waitFor(a,ROOM_STATE);
   BaselineTest.send(a,ROOM_SETTINGS,new Writer().str("New HP").u8(4).u16(180).u8(0).u16(300).done());assertEquals(0,new Reader(BaselineTest.waitFor(a,ACTION_RESULT).payload()).u16());
   for(var socket:List.of(a,b)){state=BaselineTest.waitFor(socket,ROOM_STATE).payload();assertEquals(300,new Reader(Arrays.copyOfRange(state,state.length-2,state.length)).u16());}assertEquals(300,s.rooms.get(id).startingHp);
  }}
 }
}
