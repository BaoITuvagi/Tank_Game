package tank;
import java.net.*;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;
import tank.Protocol.Error;
class RoleplayRoomTest {
    @TempDir Path tmp;
    Server server() throws Exception {
        Path cfg=tmp.resolve("server.properties");String base=tmp.toString().replace('\\','/');
        Files.writeString(cfg,"bind=127.0.0.1\ntcp.port=0\nudp.port=0\nbenchmark=true\nmap.path="+Path.of("../client/Assets/StreamingAssets/map.json").toAbsolutePath().normalize().toString().replace('\\','/')+"\ndatabase.path="+base+"/db\nspool.path="+base+"/spool\nmetrics.path="+base+"/metrics.csv\n");return new Server(new Config(cfg));
    }
    Room room(Server s,int count) throws Exception {
        Room r=new Room(s,1,"Roleplay",count,1800,RoomMode.ROLEPLAY);s.rooms.put(1,r);
        for(int i=0;i<count;i++) {
            final String name="rp"+count+"_"+i+"_"+Long.toUnsignedString(s.matchId.get(),36);var account=s.db.submit(()->s.db.register(name,"password123")).get();
            var session=s.new Session(new Socket());session.account=account;session.room=r;r.join(session,1);session.outbound.clear();
        }
        return r;
    }
    void tick(Room r) {for(var m:r.members.values())m.session.lastUdp=System.nanoTime();r.tick();}
    void loading(Room r) {
        for(var m:r.members.values()){m.roleplay.nation=m.slot;m.ready=true;}
        r.prepare(r.members.get(0).session,2);
        for(var m:r.members.values()) {
            m.session.endpoint=new InetSocketAddress(InetAddress.getLoopbackAddress(),30000+m.slot);m.session.lastUdp=System.nanoTime();
            r.roleplay.prepared(m.session,r.match,0,RoleplayContent.Version,RoleplayMatch.CONTENT_HASH,3);
        }
        tick(r);assertEquals(PrepStage.SYNC_LOADOUT,r.roleplay.stage);
    }
    void sync(Room r) {
        loading(r);
    }
    void running(Room r) {
        sync(r);
        for(var m:r.members.values())r.roleplay.prepared(m.session,r.match,PrepStage.SYNC_LOADOUT.ordinal(),r.roleplay.revision,r.roleplay.loadoutHash,5);
        tick(r);assertEquals(PrepStage.COUNTDOWN,r.roleplay.stage);
        r.startUs=Server.nowUs()-1;tick(r);assertEquals(RoomState.RUNNING,r.phase);
    }
    Packet response(Server.Session s,int seq) {
        return s.outbound.stream().map(raw->decode(raw,false)).filter(p->p.op()==ACTION_RESULT&&p.sequence()==seq).findFirst().orElseThrow();
    }
    @Test void nationSelectionIsUniqueAtomicAndLockedWhileReady() throws Exception {
        try(Server s=server()) {
            Room r=room(s,2);var a=r.members.get(0);var b=r.members.get(1);
            r.ready(a.session,true,10);assertFalse(a.ready);assertEquals(Error.NOT_READY.ordinal(),new Reader(response(a.session,10).payload()).u16());
            r.roleplay.selectNation(a.session,0,11);r.roleplay.selectNation(b.session,1,12);r.roleplay.selectNation(b.session,0,13);
            assertEquals(1,b.roleplay.nation);assertEquals(Error.NATION_TAKEN.ordinal(),new Reader(response(b.session,13).payload()).u16());
            r.ready(b.session,true,14);r.roleplay.selectNation(b.session,2,15);assertEquals(1,b.roleplay.nation);
            r.ready(b.session,false,16);r.roleplay.selectNation(b.session,2,17);assertEquals(2,b.roleplay.nation);
        }
    }
    @Test void loadingPublishesNationOnlyLoadoutAndAckGatesCountdown() throws Exception {
        try(Server s=server()) {
            Room r=room(s,2);loading(r);var a=r.members.get(0);
            assertFalse(r.announced);assertEquals(1,r.roleplay.revision);
            var loadout=new Reader(r.roleplay.loadout);assertEquals(r.match,loadout.u64());assertEquals(1,loadout.u32());
            assertArrayEquals(RoleplayMatch.CONTENT_HASH,loadout.bytes(32));assertEquals(2,loadout.u8());
            for(int slot=0;slot<2;slot++){assertEquals(slot,loadout.u8());assertEquals(slot,loadout.u8());}
            assertArrayEquals(r.roleplay.loadoutHash,loadout.bytes(32));loadout.end();
            r.roleplay.prepared(a.session,r.match,PrepStage.SYNC_LOADOUT.ordinal(),1,new byte[32],30);assertFalse(a.roleplay.loadoutConfirmed);
            r.roleplay.prepared(a.session,r.match,PrepStage.SYNC_LOADOUT.ordinal(),1,r.roleplay.loadoutHash,31);tick(r);assertFalse(r.announced);
            r.roleplay.prepared(r.members.get(1).session,r.match,PrepStage.SYNC_LOADOUT.ordinal(),1,r.roleplay.loadoutHash,32);tick(r);
            assertTrue(r.announced);assertEquals(PrepStage.COUNTDOWN,r.roleplay.stage);assertEquals(RoomState.PREPARING,r.phase);
        }
    }
    @Test void timeoutCancelsPreparationAndLateAcknowledgementsCannotReviveIt() throws Exception {
        try(Server s=server()) {
            Room r=room(s,2);for(var m:r.members.values()){m.roleplay.nation=m.slot;m.ready=true;}r.prepare(r.members.get(0).session,2);long old=r.match;
            r.deadline=Server.nowUs()-1;tick(r);assertEquals(RoomState.WAITING,r.phase);assertNull(r.world);
            for(var m:r.members.values()){assertFalse(m.ready);assertEquals(m.slot,m.roleplay.nation);}
            var a=r.members.get(0);a.session.endpoint=new InetSocketAddress(InetAddress.getLoopbackAddress(),30000);
            r.roleplay.prepared(a.session,old,0,RoleplayContent.Version,RoleplayMatch.CONTENT_HASH,40);assertEquals(RoomState.WAITING,r.phase);assertFalse(r.announced);
        }
    }
    @Test void acknowledgementAfterDeadlineIsRejectedBeforeTheNextTimerTick() throws Exception {
        try(Server s=server()) {
            Room r=room(s,2);for(var m:r.members.values()){m.roleplay.nation=m.slot;m.ready=true;}r.prepare(r.members.get(0).session,2);
            var a=r.members.get(0);a.session.endpoint=new InetSocketAddress(InetAddress.getLoopbackAddress(),30000);r.deadline=Server.nowUs()-1;
            r.roleplay.prepared(a.session,r.match,0,RoleplayContent.Version,RoleplayMatch.CONTENT_HASH,90);
            assertEquals(RoomState.WAITING,r.phase);assertFalse(a.prepared);assertFalse(r.announced);
        }
    }
    @Test void twoFourAndEightPlayersKeepNationLoadoutsOnTheSelectedMaps() throws Exception {
        try(Server s=server()) {
            for(int count:new int[]{2,4,8}) {
                Room r=room(s,count);running(r);assertEquals(2,r.arenaMap.mapId);
                Set<Integer> nations=new HashSet<>();for(var tank:r.world.tanks.values()){assertTrue(nations.add(tank.nation));}
                assertEquals(count,nations.size());assertEquals(RoomMode.ROLEPLAY,r.roomMode);r.stop();
            }
        }
    }
    @Test void syncTimeoutResetsConfirmationAndKeepsNations() throws Exception {
        try(Server s=server()) {
            Room r=room(s,2);sync(r);r.deadline=Server.nowUs()-1;tick(r);assertEquals(RoomState.WAITING,r.phase);
            assertNull(r.roleplay.loadout);assertEquals(0,r.roleplay.revision);
            for(var m:r.members.values()){assertEquals(m.slot,m.roleplay.nation);assertFalse(m.ready);assertFalse(m.roleplay.loadoutConfirmed);}
        }
    }
    @Test void duplicateSkillRequestsApplyOnceAndReturnTheSameAppliedTick() throws Exception {
        try(Server s=server()) {
            Room r=room(s,2);running(r);var a=r.members.get(0);a.session.outbound.clear();r.world.tanks.get(0).energy=0;
            byte[] payload=new Writer().u64(r.match).u32(100).u8(0).done();
            r.roleplay.use(a.session,r.match,100,0,payload,50);r.roleplay.use(a.session,r.match,100,0,payload,51);
            assertEquals(0,r.world.tanks.get(0).energy);tick(r);assertEquals(1,r.world.tanks.get(0).energy);
            assertArrayEquals(response(a.session,50).payload(),response(a.session,51).payload());
            var result=new Reader(response(a.session,50).payload());assertEquals(0,result.u16());assertEquals(r.match,result.u64());assertEquals(100,result.u32());assertEquals(USE_SKILL,result.u8());assertEquals(0,result.u8());assertEquals(r.world.tick,result.u32());assertEquals(r.world.tick+900,result.u16());result.end();
            r.roleplay.use(a.session,r.match,100,0,payload,52);assertEquals(1,r.world.tanks.get(0).energy);
            byte[] different=new Writer().u64(r.match).u32(100).u8(1).done();r.roleplay.use(a.session,r.match,100,1,different,53);
            assertEquals(Error.INVALID_REQUEST.ordinal(),new Reader(response(a.session,53).payload()).u16());
        }
    }
    @Test void evictedRequestIdsAreRejected() throws Exception {
        try(Server s=server()) {
            Room r=room(s,2);running(r);var a=r.members.get(0);
            for(int id=1;id<=258;id++) {a.session.outbound.clear();byte[] payload=new Writer().u64(r.match).u32(id).u8(9).done();r.roleplay.use(a.session,r.match,id,9,payload,70);tick(r);}
            assertEquals(256,a.roleplay.replies.size());a.session.outbound.clear();
            byte[] request=new Writer().u64(r.match).u32(1).u8(9).done();r.roleplay.use(a.session,r.match,1,9,request,71);
            assertEquals(Error.STALE_REQUEST.ordinal(),new Reader(response(a.session,71).payload()).u16());
        }
    }
    @Test void unsupportedSkillSlotCannotChangeWorldOrBlockQ() throws Exception {
        try(Server s=server()) {
            Room r=room(s,2);running(r);var a=r.members.get(0);a.session.outbound.clear();var tank=r.world.tanks.get(0);tank.energy=0;
            r.roleplay.use(a.session,r.match,100,1,new Writer().u64(r.match).u32(100).u8(1).done(),100);
            r.roleplay.use(a.session,r.match,101,0,new Writer().u64(r.match).u32(101).u8(0).done(),101);tick(r);
            assertEquals(Error.INVALID_STATE.ordinal(),new Reader(response(a.session,100).payload()).u16());
            assertEquals(0,new Reader(response(a.session,101).payload()).u16());assertEquals(r.world.tick+900,tank.qReadyTick);assertEquals(1,tank.energy);
        }
    }
    @Test void afkRejoinKeepsLoadoutAndCooldownsWithoutRestoringBuffs() throws Exception {
        try(Server s=server()) {
            Room r=room(s,3);running(r);var a=r.members.get(0);var tank=r.world.tanks.get(0);tank.kills=3;tank.deaths=2;tank.qReadyTick=900;tank.qEndTick=200;tank.protectionFlags=1;
            r.leave(a.session,3);assertEquals(RoomState.RUNNING,r.phase);assertTrue(r.canRejoin(a.session));
            var session=s.new Session(new Socket());session.account=a.account;session.room=r;r.join(session,80);assertTrue(a.rejoining);
            session.endpoint=new InetSocketAddress(InetAddress.getLoopbackAddress(),30100);r.roleplay.prepared(session,r.match,0,RoleplayContent.Version,RoleplayMatch.CONTENT_HASH,81);assertFalse(r.world.tanks.containsKey(0));
            r.roleplay.prepared(session,r.match,PrepStage.SYNC_LOADOUT.ordinal(),1,new byte[32],82);assertTrue(a.rejoining);
            r.roleplay.prepared(session,r.match,PrepStage.SYNC_LOADOUT.ordinal(),1,r.roleplay.loadoutHash,83);assertFalse(a.rejoining);
            var restored=r.world.tanks.get(0);assertEquals(900,restored.qReadyTick);assertEquals(0,restored.nation);
            assertEquals(3,restored.kills);assertEquals(2,restored.deaths);assertEquals(0,restored.protectionFlags);assertTrue(restored.qEndTick<=r.world.tick);
        }
    }
    @Test void katyushaAimIgnoresReorderedAndOutOfWindowInput() throws Exception {
        try(Server s=server()) {
            Room r=room(s,2);running(r);var player=r.members.get(1);var tank=r.world.tanks.get(1);
            r.world.useSkill(1,0);int now=r.world.tick;
            r.input(player.session,0,List.of(new World.Input(now+2,0,0)),50,50);assertEquals(50,tank.aimX);
            r.input(player.session,0,List.of(new World.Input(now+1,0,0)),10,10);assertEquals(50,tank.aimX);
            r.input(player.session,0,List.of(new World.Input(now+31,0,0)),10,10);assertEquals(50,tank.aimX);
            r.input(player.session,0,List.of(new World.Input(now+3,0,0)),200,-100);assertEquals(r.arenaMap.width-GameRules.KatyushaRadius,tank.aimX);assertEquals(11.475f,tank.aimY);
        }
    }
}
