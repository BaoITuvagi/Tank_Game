package tank;
import java.net.*;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;
import static tank.WorldBattlefield.*;

class BattlefieldRoomTest {
    @TempDir Path tmp;
    @Test void bothModesRepeatAllEventsRejoinAndClearBetweenMatches()throws Exception {
        for(boolean rp:new boolean[]{false,true}) {
            var h=new RoomFeaturesTest();h.tmp=Files.createDirectories(tmp.resolve(rp?"rp":"normal"));var rh=new RoleplayRoomTest();rh.tmp=h.tmp;
            try(Server server=rp?rh.server():h.server()) {
                Room room=rp?rh.room(server,3):h.directRoom(server,3,3,600);room.matchTicks=18000;
                List<Long> times=new ArrayList<>();
                for(int match=0;match<3;match++) {
                    for(var m:room.members.values()) {m.ready=true;m.session.outbound.clear();}
                    if(rp)rh.running(room);else {room.prepare(room.members.get(0).session,2);room.phase=RoomState.RUNNING;}
                    World world=room.world;world.telemetry=message->{};assertTrue(world.battlefield.enabled);assertNull(world.battlefield.wave);
                    for(int kind:new int[]{FIRE,AIRSTRIKE}) {
                        world.battlefield.bag.clear();world.battlefield.bag.add(kind);world.battlefield.nextTick=world.tick+1;
                        for(var m:room.members.values()) {m.session.lastUdp=System.nanoTime();m.session.endpoint=null;m.session.outbound.clear();}
                        room.tick();assertEquals(kind,world.battlefield.wave.kind());var wave=world.battlefield.wave;
                        for(var m:room.members.values())assertEquals(wave,World.decode(m.cache.get(room.snapshotId)).battlefield.wave);
                        if(kind==FIRE) {
                            // Rejoin follows the production prepare/loadout handshake, preserving world state.
                            // Alternate warning and active-fire reconnects across repeated matches.
                            if(match%2==1) {world.tick=wave.impact()-1;room.tick();assertTrue(world.tick>=wave.impact());}
                            byte[] terrain=world.bricks.clone();
                            var returning=room.members.get(2);room.leave(returning.session,0);var replacement=server.new Session(new Socket());replacement.account=returning.account;replacement.room=room;room.join(replacement,20);
                            replacement.endpoint=new InetSocketAddress(InetAddress.getLoopbackAddress(),30100);
                            if(rp) {room.roleplay.prepared(replacement,room.match,0,RoleplayContent.Version,RoleplayMatch.CONTENT_HASH,21);room.roleplay.prepared(replacement,room.match,1,room.roleplay.revision,room.roleplay.loadoutHash,22);}
                            else room.prepared(replacement,room.match,21);
                            assertFalse(returning.rejoining);assertEquals(wave,world.battlefield.wave);assertArrayEquals(terrain,world.bricks);
                        }
                        world.tick=Math.max(world.tick,wave.impact()-1);
                        for(int step=0;step<wave.end()-wave.impact()+10;step++) {
                            for(var m:room.members.values()) {m.session.lastUdp=System.nanoTime();m.session.endpoint=null;m.session.outbound.clear();}
                            long before=System.nanoTime();room.tick();times.add(System.nanoTime()-before);
                            byte[] full=world.encode();for(var m:room.members.values()) {
                                assertArrayEquals(full,m.cache.get(room.snapshotId));assertTrue(m.cache.size()<=128);
                                if(m.slot!=0)room.ack(m.session,room.snapshotId); // one peer repeatedly needs full repair
                            }
                        }
                        assertNull(world.battlefield.wave);
                    }
                    assertEquals(RoomState.RUNNING,room.phase);room.finish(0);assertNull(room.world);assertFalse(world.battlefield.enabled);assertNull(world.battlefield.wave);
                }
                Collections.sort(times);System.out.printf(Locale.ROOT,"BATTLEFIELD_ROOM mode=%s players=3 matches=3 ticks=%d p95Ms=%.3f%n",rp?"ROLEPLAY":"NORMAL",times.size(),times.get((int)(times.size()*.95))/1e6);
            }
        }
    }
}
