package tank;
import java.net.*;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;
import tank.Protocol.Error;
class RoomFeaturesTest {
 @TempDir Path tmp;
 Server server()throws Exception{Path p=tmp.resolve("settings.properties");Files.writeString(p,"bind=127.0.0.1\ntcp.port=0\nudp.port=0\nbenchmark=true\nmap.path="+Path.of("../client/Assets/StreamingAssets/map.json").toAbsolutePath().normalize().toString().replace('\\','/')+"\ndatabase.path="+tmp.resolve("db").toString().replace('\\','/')+"\nspool.path="+tmp.resolve("spool").toString().replace('\\','/')+"\nmetrics.path="+tmp.resolve("csv").toString().replace('\\','/')+"\n");return new Server(new Config(p));}
 Room directRoom(Server s,int count,int capacity,int seconds)throws Exception {
  Room room=new Room(s,1,"Configured",capacity,seconds*30);s.rooms.put(1,room);
  for(int slot=0;slot<count;slot++){String name="settings"+slot;var account=s.db.submit(()->s.db.register(name,"password123")).get();var session=s.new Session(new Socket());session.account=account;session.room=room;room.join(session,1);room.members.get(slot).ready=true;}
  return room;
 }
 @Test void customSettingsRoundTripAndCapacityIsEnforced()throws Exception {
  try(Server s=server()){s.start();try(Socket a=new Socket("127.0.0.1",s.tcp.getLocalPort());Socket b=new Socket("127.0.0.1",s.tcp.getLocalPort());Socket c=new Socket("127.0.0.1",s.tcp.getLocalPort())){
   for(var socket:List.of(a,b,c))socket.setSoTimeout(5000);
   BaselineTest.auth(a,"hostcfg");BaselineTest.auth(b,"joincfg");BaselineTest.auth(c,"fullcfg");
   for(int[] invalid:new int[][]{{1,60},{9,60},{4,30},{4,61},{4,660}}){BaselineTest.send(a,CREATE_ROOM,new Writer().str("Invalid").u8(invalid[0]).u16(invalid[1]).done());assertEquals(Error.INVALID_REQUEST.ordinal(),new Reader(BaselineTest.waitFor(a,ACTION_RESULT).payload()).u16());assertTrue(s.rooms.isEmpty());}
   BaselineTest.send(a,CREATE_ROOM,new Writer().str("Duel").u8(2).u16(60).done());assertEquals(0,new Reader(BaselineTest.waitFor(a,ACTION_RESULT).payload()).u16());
   Reader state=new Reader(BaselineTest.waitFor(a,ROOM_STATE).payload());int roomId=state.u32();assertEquals("Duel",state.str(48));state.u8();state.u8();assertEquals(1,state.u8());state.u8();state.str(24);state.bool();assertEquals(2,state.u8());assertEquals(60,state.u16());assertFalse(state.bool());assertEquals(0,state.u8());assertEquals(100,state.u16());state.end();
   BaselineTest.send(b,JOIN_ROOM,new Writer().u32(roomId).done());assertEquals(0,new Reader(BaselineTest.waitFor(b,ACTION_RESULT).payload()).u16());
   BaselineTest.send(c,JOIN_ROOM,new Writer().u32(roomId).done());assertEquals(Error.ROOM_FULL.ordinal(),new Reader(BaselineTest.waitFor(c,ACTION_RESULT).payload()).u16());assertEquals(2,s.rooms.get(roomId).members.size());
   BaselineTest.send(c,ROOM_LIST,new byte[0]);Reader list=new Reader(BaselineTest.waitFor(c,ROOM_LIST_RESULT).payload());list.u16();assertEquals(1,list.u16());list.u32();list.str(48);assertEquals(2,list.u8());assertEquals(2,list.u8());
  }}
 }
 @Test void oneMinuteRoomEndsAtItsOwnDeadline()throws Exception {
  try(Server s=server()){Room room=directRoom(s,2,4,60);room.prepare(room.members.get(0).session,2);assertEquals(2,room.arenaMap.mapId);room.phase=RoomState.RUNNING;room.world.tick=1798;World finished=room.world;
   for(var member:room.members.values())member.session.lastUdp=System.nanoTime();room.tick();assertEquals(RoomState.RUNNING,room.phase);
   for(var member:room.members.values())member.session.lastUdp=System.nanoTime();room.tick();assertEquals(RoomState.WAITING,room.phase);assertEquals(1800,finished.tick);assertNull(room.world);
  }
 }
 @Test void eightPlayersReceiveBroadMapWithUniqueSpawns()throws Exception {
  try(Server s=server()){Room room=directRoom(s,8,8,600);room.prepare(room.members.get(0).session,2);assertEquals(2,room.arenaMap.mapId);assertEquals(120,room.arenaMap.width);assertEquals(80,room.arenaMap.height);assertEquals(18000,room.matchTicks);
   Set<String> positions=new HashSet<>();for(var tank:room.world.tanks.values()){positions.add(tank.x+","+tank.y);assertFalse(room.arenaMap.blocked(tank.x,tank.y,room.world.bricks));}
   assertEquals(8,positions.size());assertEquals(16,room.arenaMap.spawns.length);
   for(var tank:room.world.tanks.values())for(var other:room.world.tanks.values())if(tank!=other)assertTrue(Math.hypot(tank.x-other.x,tank.y-other.y)>=12);
   for(int i=0;i<24;i++){var tank=room.world.tanks.get(i%8);tank.hp=0;room.world.spawn(tank);for(var other:room.world.tanks.values())if(tank!=other)assertTrue(Math.hypot(tank.x-other.x,tank.y-other.y)>=2);}
  }
 }
 @Test void allLargeMapSpawnsConnectWithIntactBricks()throws Exception {
  try(Server s=server()){GameMap map=s.largeMap;byte[] hp=new byte[64];Arrays.fill(hp,(byte)GameRules.BrickMaxHp);boolean[][] seen=new boolean[(int)map.width][(int)map.height];ArrayDeque<int[]> queue=new ArrayDeque<>();queue.add(new int[]{6,6});seen[6][6]=true;
   while(!queue.isEmpty()){var p=queue.remove();for(var d:new int[][]{{1,0},{-1,0},{0,1},{0,-1}}){int x=p[0]+d[0],y=p[1]+d[1];if(x<1||y<1||x>=map.width-1||y>=map.height-1||seen[x][y]||map.blocked(x,y,hp))continue;seen[x][y]=true;queue.add(new int[]{x,y});}}
   for(var spawn:map.spawns)assertTrue(seen[(int)spawn.x][(int)spawn.y],"unreachable spawn "+spawn.x+","+spawn.y);
  }
 }
 @Test void singleRectangularArenaFitsReducedVehiclesAndHasMoreTerrain()throws Exception {
  try(Server s=server()) {
   var map=s.largeMap;assertEquals(120,map.width);assertEquals(80,map.height);assertEquals(1.02f,GameRules.TankHalfSize);
   assertEquals(16,map.walls.length);assertEquals(36,map.bricks.length);assertEquals(6,map.water.length);assertEquals(21,map.itemSpawns.length);
   assertEquals(3,map.walls[4].w);assertEquals(15,map.walls[4].h);
   byte[] hp=new byte[64];Arrays.fill(hp,(byte)GameRules.BrickMaxHp);
   for(var brick:map.bricks){assertEquals(3,brick.w);assertEquals(3,brick.h);}
   for(var spawn:map.spawns){assertFalse(map.water(spawn.x,spawn.y));for(int body:new int[]{0,4500,9000,13500})assertFalse(map.blocked(spawn.x,spawn.y,body,hp));}
   assertTrue(map.blocked(2.9f,10,4500,hp));assertFalse(map.blocked(3,10,4500,hp));
   for(RoomMode mode:RoomMode.values())for(int count:new int[]{2,4,8}) {
    var room=new Room(s,100+count,"Single arena",count,1800,mode);assertSame(map,room.arenaMap);
   }
  }
 }
 @Test void turretActivityCancelsAnExistingAfkWarning()throws Exception {
  try(Server s=server()){Room room=directRoom(s,2,4,180);room.prepare(room.members.get(0).session,2);room.phase=RoomState.RUNNING;var idle=room.members.get(0);idle.afkWarn=true;idle.activity=System.nanoTime()-121_000_000_000L;idle.session.outbound.clear();for(var member:room.members.values())member.session.lastUdp=System.nanoTime();room.input(idle.session,0,List.of(new World.Input(1,0,1000)));room.tick();assertFalse(idle.afkWarn);assertEquals(2,room.members.size());assertTrue(idle.session.outbound.stream().map(raw->decode(raw,false)).anyMatch(p->p.op()==NOTICE&&new Reader(p.payload()).u16()==101));}
 }
 @Test void aimRecalculationAfterRespawnDoesNotResetAfk()throws Exception {
  try(Server s=server()){Room room=directRoom(s,2,4,180);room.prepare(room.members.get(0).session,2);room.phase=RoomState.RUNNING;room.world.tick=200;var idle=room.members.get(0);room.world.tanks.get(0).respawn=200;idle.previousAim=0;idle.activity=System.nanoTime()-61_000_000_000L;for(var member:room.members.values())member.session.lastUdp=System.nanoTime();room.input(idle.session,0,List.of(new World.Input(201,0,18000)));room.tick();assertTrue(idle.afkWarn);assertTrue(System.nanoTime()-idle.activity>60_000_000_000L);}
 }
}
