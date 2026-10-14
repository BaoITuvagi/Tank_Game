package tank;
import java.util.*;
import java.nio.file.*;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;

class PlayerRecoveryTest {
 @TempDir Path tmp;
 RoomFeaturesTest helper(){var h=new RoomFeaturesTest();h.tmp=tmp;return h;}
 void refresh(Room room){for(var m:room.members.values())m.session.lastUdp=System.nanoTime();}
 @Test void warningStartsAtFifteenSecondsAndBlockedMovementIsActivity()throws Exception{
  var h=helper();try(Server s=h.server()){
   Room room=h.directRoom(s,3,4,180);room.prepare(room.members.get(0).session,1);room.phase=RoomState.RUNNING;var m=room.members.get(2);
   m.activity=System.nanoTime()-14_000_000_000L;refresh(room);room.tick();assertFalse(m.afkWarn);
   m.activity=System.nanoTime()-15_100_000_000L;refresh(room);room.tick();assertTrue(m.afkWarn);
   room.input(m.session,0,List.of(new World.Input(room.world.tick+1,8,0)));refresh(room);room.tick();assertFalse(m.afkWarn);
  }
 }
 @Test void temporaryUdpGapRecoversAndOnlyExpiredPlayerLeaves()throws Exception{
  var h=helper();try(Server s=h.server()){
   Room room=h.directRoom(s,3,4,180);room.prepare(room.members.get(0).session,1);room.phase=RoomState.RUNNING;var third=room.members.get(2);
   refresh(room);third.session.lastUdp=System.nanoTime()-4_000_000_000L;room.tick();assertEquals(3,room.members.size());
   refresh(room);room.input(third.session,0,List.of(new World.Input(room.world.tick+1,24,0)));float x=room.world.tanks.get(2).x;room.tick();assertTrue(room.world.tanks.get(2).x>x);assertFalse(room.world.bullets.isEmpty());
   refresh(room);third.session.lastUdp=System.nanoTime()-11_000_000_000L;room.tick();assertNull(third.session.room);assertEquals(-1,third.session.slot);assertNull(third.session.endpoint);
   assertEquals(RoomState.RUNNING,room.phase);assertEquals(2,room.view.count());assertEquals(RoomState.RUNNING,room.view.state());assertFalse(room.world.tanks.containsKey(2));
  }
 }
 @Test void picturedBrickCornerAllowsRetreatFiringAndRespawn()throws Exception{
  var h=helper();try(Server s=h.server()){
   World w=new World(s.map,1);w.add(0);var t=w.tanks.get(0);
   // Lower left brick formation in the supplied image. Touch its top/right edge.
   var brick=Arrays.stream(s.map.bricks).filter(r->r.x<s.map.width/2&&r.y<s.map.height/2).findFirst().orElseThrow();
   t.x=brick.x+brick.w+1.21f;t.y=brick.y+brick.h+1.21f;assertFalse(s.map.blocked(t.x,t.y,w.bricks));
   float x=t.x,y=t.y;for(int i=0;i<15;i++)w.step(Map.of(0,new World.Input(w.tick+1,25,9000)));
   assertTrue(t.x>x);assertTrue(t.y>y);assertTrue(w.nextBullet>1);assertFalse(s.map.blocked(t.x,t.y,w.bricks));
   t.hp=0;t.respawn=w.tick+1;w.step(Map.of());assertTrue(t.hp>0);assertFalse(s.map.blocked(t.x,t.y,w.bricks));
   x=t.x;w.step(Map.of(0,new World.Input(w.tick+1,8,0)));assertNotEquals(x,t.x);
  }
 }
 @Test void historyDetailPreservesAllParticipantsAndRejectsOtherAccounts()throws Exception{
  var h=helper();try(Server s=h.server()){
   Room room=h.directRoom(s,3,4,180);room.prepare(room.members.get(0).session,1);room.phase=RoomState.RUNNING;room.startedUtc=System.currentTimeMillis();var third=room.members.get(2);long account=third.account.id();
   var tank=room.world.tanks.get(2);tank.kills=4;tank.deaths=2;room.leave(third.session,2);room.finish(0);s.db.submit(()->null).get();
   var detail=s.db.submit(()->s.db.historyDetail(account,room.match)).get();assertEquals(3,detail.players().size());assertEquals(4,detail.players().get(0).kills());assertTrue(detail.players().get(0).disconnected());assertEquals(2,detail.players().get(0).deaths());
   assertThrows(Database.Failure.class,()->s.db.historyDetail(99999,room.match));assertThrows(Database.Failure.class,()->s.db.historyDetail(account,room.match+1));
  }
 }
}
