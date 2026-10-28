package tank;
import java.util.*;
import java.net.*;
import java.nio.file.*;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;

class AfkRejoinAndHitTest {
 @TempDir Path tmp;
 RoomFeaturesTest helper(){var h=new RoomFeaturesTest();h.tmp=tmp;return h;}
 void refresh(Room room){for(var m:room.members.values())m.session.lastUdp=System.nanoTime();}
 @Test void twoPlayerAfkFinishesSafelyForEitherSlotAndRoomMode()throws Exception{
  for(RoomMode mode:RoomMode.values())for(int idleSlot:new int[]{0,1}){
   var h=helper();h.tmp=Files.createDirectories(tmp.resolve(mode+"-"+idleSlot));
   try(Server s=h.server()){
    Room r=new Room(s,1,"AFK pair",2,1800,mode);s.rooms.put(1,r);
    for(int slot=0;slot<2;slot++){
     String name="pair"+slot;var account=s.db.submit(()->s.db.register(name,"password123")).get();var session=s.new Session(new Socket());session.account=account;session.room=r;r.join(session,1);
     var m=r.members.get(slot);m.roleplay.nation=slot;m.ready=true;
    }
    var idle=r.members.get(idleSlot);var survivor=r.members.get(1-idleSlot);
    r.prepare(r.members.get(0).session,2);r.phase=RoomState.RUNNING;r.startedUtc=System.currentTimeMillis();
    idle.afkWarn=true;idle.afkSince=System.nanoTime()-16_000_000_000L;idle.activity=System.nanoTime()-31_000_000_000L;refresh(r);
    assertDoesNotThrow(r::tick,"AFK slot "+idleSlot+" in "+mode);
    assertEquals(RoomState.WAITING,r.phase);assertNull(r.world);assertNull(idle.session.room);assertSame(r,survivor.session.room);
    assertTrue(survivor.session.outbound.stream().map(raw->decode(raw,false)).anyMatch(p->p.op()==END));
    idle.session.room=r;r.enqueue(()->r.join(idle.session,3));r.tick();assertEquals(2,r.members.size());assertSame(r,idle.session.room);
    r.enqueue(()->r.leave(survivor.session,0));r.tick();assertNull(survivor.session.room);assertEquals(1,r.members.size());
   }
  }
 }
 @Test void realTcpJoinLeaveAndLogoutStillRespondAfterAfkEndsTwoPlayerMatch()throws Exception{
  for(RoomMode mode:RoomMode.values()){
   var h=helper();h.tmp=Files.createDirectories(tmp.resolve("tcp-"+mode));
   try(Server s=h.server()){
    s.start();try(Socket a=new Socket("127.0.0.1",s.tcp.getLocalPort());Socket b=new Socket("127.0.0.1",s.tcp.getLocalPort())){
     a.setSoTimeout(3000);b.setSoTimeout(3000);
     byte[] tokenA=BaselineTest.auth(a,"idlehost"),tokenB=BaselineTest.auth(b,"activepeer");
     var host=s.tokens.get(HexFormat.of().formatHex(tokenA));var peer=s.tokens.get(HexFormat.of().formatHex(tokenB));
     BaselineTest.send(a,CREATE_ROOM,new Writer().str("AFK TCP").u8(2).u16(60).str("").u16(100).u8(mode.ordinal()).done());assertEquals(0,new Reader(BaselineTest.waitFor(a,ACTION_RESULT).payload()).u16());
     int id=new Reader(BaselineTest.waitFor(a,ROOM_STATE).payload()).u32();
     BaselineTest.send(b,JOIN_ROOM,new Writer().u32(id).done());assertEquals(0,new Reader(BaselineTest.waitFor(b,ACTION_RESULT).payload()).u16());
     Room r=s.rooms.get(id);var armed=new java.util.concurrent.CompletableFuture<Void>();
     r.enqueue(()->{
      for(var m:r.members.values()){m.roleplay.nation=m.slot;m.ready=true;}
      r.prepare(host,900);r.phase=RoomState.RUNNING;r.startedUtc=System.currentTimeMillis();refresh(r);
      var idle=r.member(host);idle.afkWarn=true;idle.afkSince=System.nanoTime()-16_000_000_000L;idle.activity=System.nanoTime()-31_000_000_000L;
      armed.complete(null);
     });
     armed.get(3,java.util.concurrent.TimeUnit.SECONDS);
     BaselineTest.waitFor(a,REMOVED);BaselineTest.waitFor(b,END);
     BaselineTest.send(a,JOIN_ROOM,new Writer().u32(id).done());assertEquals(0,new Reader(BaselineTest.waitFor(a,ACTION_RESULT).payload()).u16());
     assertTrue(r.thread.isAlive());assertSame(r,host.room);
     BaselineTest.send(b,LEAVE_ROOM,new byte[0]);assertEquals(0,new Reader(BaselineTest.waitFor(b,ACTION_RESULT).payload()).u16());assertNull(peer.room);
     BaselineTest.send(b,LOGOUT,new byte[0]);assertEquals(0,new Reader(BaselineTest.waitFor(b,LOGOUT_RESULT).payload()).u16());assertNull(peer.account);
     BaselineTest.send(a,LOGOUT,new byte[0]);assertEquals(0,new Reader(BaselineTest.waitFor(a,LOGOUT_RESULT).payload()).u16());assertNull(host.room);assertNull(host.account);
    }
   }
  }
 }
 @Test void afkCountdownExpiresWhileDead()throws Exception{
  var h=helper();try(Server s=h.server()){
   Room r=h.directRoom(s,3,3,180);r.prepare(r.members.get(0).session,1);r.phase=RoomState.RUNNING;
   var idle=r.members.get(2);idle.afkWarn=true;idle.afkSince=System.nanoTime()-14_000_000_000L;
   var tank=r.world.tanks.get(2);tank.hp=0;tank.respawn=1000;long began=idle.afkSince;refresh(r);r.tick();assertEquals(began,idle.afkSince);assertTrue(r.members.containsKey(2));
   idle.afkSince=System.nanoTime()-15_100_000_000L;refresh(r);r.tick();assertFalse(r.members.containsKey(2));assertTrue(r.canRejoin(idle.session));assertEquals(RoomState.RUNNING,r.phase);
  }
 }
 @Test void afkRejoinRestoresSlotAndScoreWithoutRestartingOtherPlayers()throws Exception{
  var h=helper();try(Server s=h.server()){
   Room r=h.directRoom(s,3,3,180);r.prepare(r.members.get(0).session,1);r.phase=RoomState.RUNNING;r.world.tick=300;r.startUs=77;r.startedUtc=System.currentTimeMillis();r.broadcast();
   var m=r.members.get(2);var t=r.world.tanks.get(2);t.kills=5;t.deaths=3;t.lastKill=210;byte[] bricks=r.world.bricks.clone();long match=r.match;
   r.leave(m.session,3);assertTrue(r.canRejoin(m.session));assertEquals(2,r.view.count());
   // Return through a fresh authenticated session with the same account.
   var replacement=s.new Session(new Socket());replacement.account=m.account;replacement.room=r;r.join(replacement,2);
   assertEquals(3,r.members.size());assertEquals(3,r.participants.size());assertTrue(m.rejoining);assertFalse(r.world.tanks.containsKey(2));assertFalse(r.canRejoin(replacement));
   var prepare=replacement.outbound.stream().map(f->decode(f,false)).filter(p->p.op()==PREPARE).findFirst().orElseThrow();assertEquals(1,prepare.payload()[prepare.payload().length-1]);
   refresh(r);r.tick();assertEquals(301,r.world.tick);assertEquals(match,r.match);assertArrayEquals(bricks,r.world.bricks);
   replacement.endpoint=new InetSocketAddress("127.0.0.1",12345);r.prepared(replacement,match,3);
   t=r.world.tanks.get(2);assertEquals(5,t.kills);assertEquals(3,t.deaths);assertEquals(210,t.lastKill);assertEquals(r.startingHp,t.hp);assertFalse(r.arenaMap.blocked(t.x,t.y,r.world.bricks));assertFalse(m.rejoining);
   assertTrue(replacement.outbound.stream().map(f->decode(f,false)).anyMatch(p->p.op()==START));
   r.finish(0);s.db.submit(()->null).get();var result=s.db.submit(()->s.db.historyDetail(m.account.id(),match)).get();assertEquals(3,result.players().size());assertEquals(1,result.players().stream().filter(p->p.account()==m.account.id()).count());assertEquals(5,result.players().get(0).kills());assertFalse(result.players().get(0).disconnected());
  }
 }
 @Test void finishedMatchDoesNotResumeAfterVoluntaryLeave()throws Exception{
  var h=helper();try(Server s=h.server()){
   Room r=h.directRoom(s,3,3,180);r.prepare(r.members.get(0).session,1);r.phase=RoomState.RUNNING;r.broadcast();var m=r.members.get(2);r.leave(m.session,0);
   assertTrue(r.canRejoin(m.session));assertEquals(2,r.members.size());
   r.finish(0);assertFalse(r.canRejoin(m.session));
  }
 }
 @Test void expiredRejoinPreparationCanRetryWithoutDuplicatingParticipant()throws Exception{
  var h=helper();try(Server s=h.server()){
   Room r=h.directRoom(s,3,3,180);r.prepare(r.members.get(0).session,1);r.phase=RoomState.RUNNING;r.broadcast();var m=r.members.get(2);r.leave(m.session,3);r.join(m.session,2);
   m.rejoinDeadline=System.nanoTime()-1;refresh(r);r.tick();assertFalse(r.members.containsKey(2));assertTrue(r.canRejoin(m.session));assertEquals(3,r.participants.size());
   r.join(m.session,3);refresh(r);m.session.lastUdp=System.nanoTime()-11_000_000_000L;r.tick();assertTrue(r.canRejoin(m.session));assertFalse(m.rejoining);assertEquals(3,r.participants.size());
  }
 }
 GameMap open(){GameMap m=new GameMap();m.width=m.height=100;m.walls=new GameMap.Rect[0];m.bricks=new GameMap.Rect[0];m.water=new GameMap.Rect[0];return m;}
 World.Tank target(World w,int slot,float x,float y,int body){var t=new World.Tank();t.slot=slot;t.x=x;t.y=y;t.body=body;w.tanks.put(slot,t);return t;}
 World.Bullet shot(World w,float x,float y,int angle){var b=new World.Bullet();b.id=1;b.owner=0;b.x=x;b.y=y;b.heading=angle;b.born=-20;w.bullets.put(1,b);return b;}
 @Test void grazingVisibleBodyDamagesEvenDuringOldShieldWindow(){
  var w=new World(open(),1);var t=target(w,1,10,10,0);t.shield=999;var b=shot(w,8.5f,11,0);w.advance(b);assertEquals(80,t.hp);assertFalse(w.bullets.containsKey(1));
 }
 @Test void rotatedCornerHitsAndNearMissDoesNotDamage(){
  var w=new World(open(),1);var t=target(w,1,10,10,4500);var b=shot(w,9.5f,11.3f,0);w.advance(b);assertEquals(80,t.hp);
  t.hp=100;b=shot(w,9.5f,10+GameRules.TankHalfSize*(float)Math.sqrt(2)+GameRules.BulletHalfWidth+.02f,0);w.advance(b);assertEquals(100,t.hp);assertTrue(w.bullets.containsKey(1));
 }
 @Test void relativeSweepCatchesFastMovingTargetCrossingBulletPath(){
  var w=new World(open(),1);var t=target(w,1,10,11.2f,9000);var b=shot(w,9.8f,10,0);
  var before=t.copy();before.y=8.8f;w.previousTanks.put(1,before);w.advance(b);assertEquals(80,t.hp);assertFalse(w.bullets.containsKey(1));
 }
 @Test void steelOccludesTargetAndFirstContactDoesNotDamageTwoTargets(){
  var m=open();var wall=new GameMap.Rect();wall.x=9;wall.y=0;wall.w=1;wall.h=20;m.walls=new GameMap.Rect[]{wall};
  var w=new World(m,1);var behind=target(w,1,10+GameRules.TankHalfSize+.4f,10,0);var b=shot(w,8.3f,10,0);w.advance(b);assertEquals(100,behind.hp);assertEquals(1,b.bounces);
  w=new World(open(),1);var near=target(w,1,10,10,0);var far=target(w,2,11,10,0);b=shot(w,8.5f,10,0);w.advance(b);assertEquals(80,near.hp);assertEquals(100,far.hp);assertFalse(w.bullets.containsKey(1));
 }
 @Test void hitKillsOnceAndRespawnHasNoInvulnerability()throws Exception{
  var h=helper();try(Server s=h.server()){
   var w=new World(s.map,1);w.add(0);w.add(1);var shooter=w.tanks.get(0);var t=w.tanks.get(1);t.x=t.y=10;t.hp=1;var b=shot(w,8.5f,10,0);w.advance(b);
   assertEquals(0,t.hp);assertEquals(1,t.deaths);assertEquals(1,shooter.kills);w.damage(t,0);assertEquals(1,t.deaths);assertEquals(1,shooter.kills);
   w.spawn(t);assertEquals(w.tick,t.shield);w.damage(t,0);assertEquals(80,t.hp);
  }
 }
}
