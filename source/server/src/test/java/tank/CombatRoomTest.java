package tank;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;

/** Direct room-loop load/lifecycle check; does not replace graphical multiplayer playtests. */
class CombatRoomTest {
    @TempDir Path tmp;
    @Test void fourPlayerNormalAndTenEightPlayerRoleplayMatchesKeepHistoryAndCachesBounded()throws Exception {
        for(boolean rp:new boolean[]{false,true}) {
            Path folder=Files.createDirectories(tmp.resolve(rp?"roleplay":"normal"));
            var normal=new RoomFeaturesTest();normal.tmp=folder;var roleplay=new RoleplayRoomTest();roleplay.tmp=folder;
            try(Server server=rp?roleplay.server():normal.server()) {
                int count=rp?8:4;Room room=rp?roleplay.room(server,count):normal.directRoom(server,count,count,60);
                List<Long> times=new ArrayList<>();
                for(int match=0;match<(rp?10:1);match++) {
                    for(var m:room.members.values()) {m.ready=true;m.session.outbound.clear();}
                    if(rp)roleplay.running(room);else {room.prepare(room.members.get(0).session,2);room.phase=RoomState.RUNNING;}
                    assertTrue(room.world.combat.events.isEmpty());assertEquals(1,room.world.combat.nextId);
                    for(var m:room.members.values())m.session.endpoint=null; // No packets sent to unrelated local ports.
                    World world=room.world;
                    for(int step=0;step<300;step++) {
                        int tick=world.tick+1;
                        for(var m:room.members.values()) {
                            m.session.lastUdp=System.nanoTime();m.session.outbound.clear();
                            int slot=m.slot;
                            room.input(m.session,m.ack,List.of(new World.Input(tick,16|32|(1<<((tick/90+slot)%4)),(tick*97+slot*4500)%36000)));
                            if(rp&&(step==0&&slot!=7||step==240&&slot==7))world.skillIntents.add(()->{world.skillAim(slot,40,40);world.useSkill(slot,0);});
                            if(step==60||step==180) {var t=world.tanks.get(slot);if(t.hp>0)world.items.apply(t,step==60?WorldItems.HE:WorldItems.MIRROR);}
                        }
                        long start=System.nanoTime();room.tick();long elapsed=System.nanoTime()-start;
                        if(step>=30)times.add(elapsed);
                        assertEquals(RoomState.RUNNING,room.phase);assertEquals(count,room.members.size());
                        assertTrue(world.combat.events.size()<=GameRules.MaxCombatEvents);
                        byte[] full=world.encode();assertTrue(full.length+30<32768);
                        for(var m:room.members.values()) {
                            assertTrue(m.cache.size()<=128);assertArrayEquals(full,m.cache.get(room.snapshotId));
                            // Drop ACKs for one client across both short and beyond-retention intervals.
                            if(m.slot!=0||step<45||step>180)room.ack(m.session,room.snapshotId);
                        }
                    }
                    assertTrue(world.combat.nextId>1);room.finish(0);assertNull(room.world);
                    for(var m:room.members.values())assertTrue(m.cache.isEmpty());
                }
                Collections.sort(times);long median=times.get(times.size()/2),p95=times.get((int)(times.size()*.95));
                System.out.printf(Locale.ROOT,"COMBAT_ROOM_LOAD mode=%s players=%d samples=%d medianMs=%.3f p95Ms=%.3f (headless direct loop; not graphical FPS)%n",rp?"ROLEPLAY":"NORMAL",count,times.size(),median/1e6,p95/1e6);
            }
        }
    }
}
