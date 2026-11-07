package tank;
import java.net.*;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;
import tank.Protocol.Error;

class RoomMapSelectionTest {
    @TempDir Path tmp;
    Server server(Path directory)throws Exception {
        var helper=new RoomFeaturesTest();helper.tmp=Files.createDirectories(directory);return helper.server();
    }
    Room room(Server server,RoomMode mode,int mapId)throws Exception {
        Room room=new Room(server,mapId,"Selected arena",3,1800,mode,mapId);server.rooms.put(room.id,room);
        for(int slot=0;slot<3;slot++) {
            String name="map"+mapId+"_"+slot;var account=server.db.submit(()->server.db.register(name,"password123")).get();
            var session=server.new Session(new Socket());session.account=account;session.room=room;room.join(session,1);
            var member=room.members.get(slot);member.roleplay.nation=slot;member.ready=true;
        }
        return room;
    }
    Reader latest(Server.Session session,int opcode) {
        return new Reader(session.outbound.stream().map(raw->decode(raw,false)).filter(packet->packet.op()==opcode).reduce((a,b)->b).orElseThrow().payload());
    }
    void assertPrepare(Room room,Server.Session session,boolean rejoining) {
        var packet=latest(session,PREPARE);assertEquals(room.match,packet.u64());assertEquals(room.id,packet.u32());assertEquals(session.slot,packet.u8());
        assertEquals(room.arenaMap.mapId,packet.u16());assertArrayEquals(room.arenaMap.hash,packet.bytes(32));
        packet.u8();assertEquals(30,packet.u8());assertEquals(room.server.udp.getLocalPort(),packet.u16());
        if(room.roleplay!=null) {assertEquals(rejoining,packet.bool());assertEquals(1,packet.u8());assertEquals(RoleplayContent.Version,packet.u16());assertArrayEquals(RoleplayMatch.CONTENT_HASH,packet.bytes(32));packet.u32();}
        else if(rejoining)assertTrue(packet.bool());
        packet.end();
    }
    @Test void selectedMapsSurviveSettingsPrepareRejoinAndRepeatedMatchesInBothModes()throws Exception {
        for(RoomMode mode:RoomMode.values())for(int mapId:new int[]{2,3,4})try(Server server=server(tmp.resolve(mode+"-"+mapId))) {
            Room room=room(server,mode,mapId);GameMap selected=server.maps.get(mapId);assertSame(selected,room.arenaMap);
            var host=room.members.get(0).session;
            room.configure(host,2,"New room settings",3,60,0,"",200);assertSame(selected,room.arenaMap);
            for(var member:room.members.values())member.ready=true;
            room.prepare(host,3);assertSame(selected,room.world.map);assertSame(selected,room.arenaMap);
            for(var member:room.members.values())assertPrepare(room,member.session,false);
            if(room.roleplay!=null)room.roleplay.publishLoadout(Server.nowUs());
            room.phase=RoomState.RUNNING;room.startedUtc=System.currentTimeMillis();
            var returning=room.members.get(2);room.leave(returning.session,0);assertEquals(RoomState.RUNNING,room.phase);
            var replacement=server.new Session(new Socket());replacement.account=returning.account;replacement.room=room;room.join(replacement,4);
            assertPrepare(room,replacement,true);assertSame(selected,room.world.map);
            replacement.endpoint=new InetSocketAddress(InetAddress.getLoopbackAddress(),30100);
            if(room.roleplay!=null) {
                room.roleplay.prepared(replacement,room.match,PrepStage.LOADING.ordinal(),RoleplayContent.Version,RoleplayMatch.CONTENT_HASH,5);
                room.roleplay.prepared(replacement,room.match,PrepStage.SYNC_LOADOUT.ordinal(),room.roleplay.revision,room.roleplay.loadoutHash,6);
            }else room.prepared(replacement,room.match,5);
            assertFalse(returning.rejoining);assertSame(selected,room.arenaMap);
            room.finish(0);assertEquals(RoomState.WAITING,room.phase);assertNull(room.world);assertSame(selected,room.arenaMap);
            for(var member:room.members.values())member.ready=true;
            room.prepare(host,7);assertSame(selected,room.world.map);assertPrepare(room,host,false);
        }
    }
    @Test void unknownMapIdsAreRejectedBeforeAllocatingARoomAndLegacyCreateUsesDefault()throws Exception {
        try(Server server=server(tmp.resolve("unknown"))) {
            server.start();try(Socket socket=new Socket("127.0.0.1",server.tcp.getLocalPort())) {
                socket.setSoTimeout(3000);BaselineTest.auth(socket,"map_unknown");
                for(int mapId:new int[]{0,1,5,65535}) {
                    BaselineTest.send(socket,CREATE_ROOM,new Writer().str("Unknown map").u8(2).u16(60).str("").u16(100).u8(0).u16(mapId).done());
                    assertEquals(Error.INVALID_REQUEST.ordinal(),new Reader(BaselineTest.waitFor(socket,ACTION_RESULT).payload()).u16());assertTrue(server.rooms.isEmpty());assertEquals(0,server.roomId.get());
                }
                BaselineTest.send(socket,CREATE_ROOM,new Writer().str("Default arena").done());assertEquals(0,new Reader(BaselineTest.waitFor(socket,ACTION_RESULT).payload()).u16());
                int id=new Reader(BaselineTest.waitFor(socket,ROOM_STATE).payload()).u32();assertSame(server.largeMap,server.rooms.get(id).arenaMap);
            }
        }
    }
    @Test void realTcpCreationAndRoomListCarrySelectedMapForNormalAndRoleplay()throws Exception {
        try(Server server=server(tmp.resolve("wire"))) {
            server.start();try(Socket host=new Socket("127.0.0.1",server.tcp.getLocalPort());Socket viewer=new Socket("127.0.0.1",server.tcp.getLocalPort())) {
                host.setSoTimeout(3000);viewer.setSoTimeout(3000);BaselineTest.auth(host,"map_host");BaselineTest.auth(viewer,"map_viewer");
                for(RoomMode mode:RoomMode.values())for(int mapId:new int[]{3,4}) {
                    BaselineTest.send(host,CREATE_ROOM,new Writer().str("Map wire").u8(2).u16(60).str("").u16(100).u8(mode.ordinal()).u16(mapId).done());
                    assertEquals(0,new Reader(BaselineTest.waitFor(host,ACTION_RESULT).payload()).u16());
                    var state=new Reader(BaselineTest.waitFor(host,ROOM_STATE).payload());int id=state.u32();state.str(48);state.u8();state.u8();assertEquals(1,state.u8());state.u8();state.str(96);state.bool();state.u8();state.u16();state.bool();state.u8();state.u16();assertEquals(mapId,state.u16());assertEquals(mode.ordinal(),state.u8());
                    if(mode==RoomMode.ROLEPLAY){state.u8();state.u64();state.u64();state.u32();assertEquals(255,state.u8());}state.end();
                    BaselineTest.send(viewer,ROOM_LIST,new byte[0]);var list=new Reader(BaselineTest.waitFor(viewer,ROOM_LIST_RESULT).payload());assertEquals(0,list.u16());assertEquals(1,list.u16());assertEquals(id,list.u32());list.str(48);list.u8();list.u8();list.u8();list.bool();list.bool();assertEquals(mode.ordinal(),list.u8());assertEquals(mapId,list.u16());list.end();
                    assertSame(server.maps.get(mapId),server.rooms.get(id).arenaMap);
                    BaselineTest.send(host,LEAVE_ROOM,new byte[0]);BaselineTest.waitFor(host,REMOVED);BaselineTest.waitFor(host,ACTION_RESULT);
                }
            }
        }
    }
}
