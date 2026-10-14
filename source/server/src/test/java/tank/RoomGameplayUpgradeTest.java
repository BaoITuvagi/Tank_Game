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
   room.configure(peer,2,"No",4,180,0,"",10);assertEquals(3,room.startingHp);
   for(int hp:new int[]{0,31}){room.configure(host,3,"No",4,180,0,"",hp);assertEquals(3,room.startingHp);}
   room.configure(host,4,"Ten HP",4,180,0,"",10);assertEquals(10,room.startingHp);assertTrue(room.members.values().stream().noneMatch(m->m.ready));
   for(var m:room.members.values())m.ready=true;room.prepare(host,5);for(var t:room.world.tanks.values()){assertEquals(10,t.hp);assertEquals(10,t.maxHp);}
   room.configure(host,6,"No",4,180,0,"",20);assertEquals(10,room.startingHp);
  }
 }
 @Test void createAndEditHpTravelOverRealTcpAndReachEachMember()throws Exception{
  var h=helper();try(Server s=h.server()){s.start();try(Socket a=new Socket("127.0.0.1",s.tcp.getLocalPort());Socket b=new Socket("127.0.0.1",s.tcp.getLocalPort())){
   a.setSoTimeout(5000);b.setSoTimeout(5000);BaselineTest.auth(a,"hphost");BaselineTest.auth(b,"hpjoin");
   for(int hp:new int[]{0,31}){BaselineTest.send(a,CREATE_ROOM,new Writer().str("HP").u8(4).u16(180).str("").u8(hp).done());assertEquals(Protocol.Error.INVALID_REQUEST.ordinal(),new Reader(BaselineTest.waitFor(a,ACTION_RESULT).payload()).u16());assertTrue(s.rooms.isEmpty());}
   BaselineTest.send(a,CREATE_ROOM,new Writer().str("HP").u8(4).u16(180).str("").u8(8).done());assertEquals(0,new Reader(BaselineTest.waitFor(a,ACTION_RESULT).payload()).u16());
   byte[] state=BaselineTest.waitFor(a,ROOM_STATE).payload();assertEquals(8,Byte.toUnsignedInt(state[state.length-1]));int id=new Reader(state).u32();
   BaselineTest.send(b,JOIN_ROOM,new Writer().u32(id).done());BaselineTest.waitFor(b,ACTION_RESULT);state=BaselineTest.waitFor(b,ROOM_STATE).payload();assertEquals(8,Byte.toUnsignedInt(state[state.length-1]));BaselineTest.waitFor(a,ROOM_STATE);
   BaselineTest.send(a,ROOM_SETTINGS,new Writer().str("New HP").u8(4).u16(180).u8(0).u8(12).done());assertEquals(0,new Reader(BaselineTest.waitFor(a,ACTION_RESULT).payload()).u16());
   for(var socket:List.of(a,b)){state=BaselineTest.waitFor(socket,ROOM_STATE).payload();assertEquals(12,Byte.toUnsignedInt(state[state.length-1]));}assertEquals(12,s.rooms.get(id).startingHp);
  }}
 }
}
