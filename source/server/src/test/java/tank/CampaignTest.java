package tank;

import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import java.nio.file.*;
import java.net.*;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;

class CampaignTest {
    @TempDir Path tmp;
    CampaignRun clean(int nation){var r=new CampaignRun(nation,42);r.enemies.clear();r.loot.clear();r.generated=10000;return r;}
    @Test void gridBumpAttackAndEnemyReactionAreOneTurn(){
        var r=clean(0);r.enemies.add(new CampaignRun.Enemy(1,6,1,60,7,0));
        assertTrue(r.act(0));assertEquals(0,r.y);assertEquals(40,r.enemies.getFirst().hp);assertEquals(93,r.hp);assertEquals(1,r.turn);
        assertTrue(r.act(0));assertTrue(r.act(0));assertTrue(r.enemies.isEmpty());assertEquals(1,r.kills);assertEquals(0,r.y);
        assertTrue(r.act(0));assertEquals(1,r.y);assertEquals(1,r.distance);
        r.act(2);assertEquals(1,r.distance);assertEquals(0,r.y);
    }
    @Test void shotsCooldownAndInvalidActions(){
        var r=clean(0);r.enemies.add(new CampaignRun.Enemy(1,6,2,100,7,0));
        r.act(4);assertEquals(60,r.enemies.getFirst().hp);assertEquals(10,r.shot);
        assertFalse(r.act(4));assertEquals(1,r.turn);
        for(int i=0;i<10;i++)r.act(i%2==0?1:3);
        assertEquals(0,r.shot);
        r.enemies.clear();assertFalse(r.act(5));assertEquals(0,r.skill);
        r.x=0;assertFalse(r.act(3));assertEquals(11,r.turn);
    }
    @Test void eachNationAutoTargetsAndCooldownIsFiftyTurns(){
        for(int nation:new int[]{0,1,5}){
            var r=clean(nation);r.enemies.add(new CampaignRun.Enemy(1,6,4,200,1,0));r.enemies.add(new CampaignRun.Enemy(2,7,4,200,1,0));
            assertTrue(r.act(5));assertEquals(50,r.skill);assertEquals(nation==5?80:nation==0?140:155,r.enemies.getFirst().hp);
            assertEquals(nation==5?200:nation==0?140:155,r.enemies.get(1).hp);
            assertFalse(r.act(5));r.enemies.clear();for(int i=0;i<50;i++)r.act(i%2==0?1:3);assertEquals(0,r.skill);
        }
    }
    @Test void buffsHealShieldAndGold(){
        var r=clean(0);r.hp=40;r.loot.add(new CampaignRun.Loot(6,1,0,0));r.act(0);assertEquals(65,r.hp);
        r.loot.add(new CampaignRun.Loot(6,2,1,0));r.act(0);assertEquals(3,r.doubleHits);
        r.enemies.add(new CampaignRun.Enemy(1,6,3,200,10,0));r.shields=3;
        for(int i=0;i<3;i++)r.act(0);assertEquals(80,r.enemies.getFirst().hp);assertEquals(0,r.doubleHits);assertEquals(0,r.shields);assertEquals(65,r.hp);
        r.act(0);assertEquals(55,r.hp);r.enemies.clear();r.loot.add(new CampaignRun.Loot(6,3,3,25));r.act(0);assertEquals(25,r.gold);
    }
    @Test void bossGateEveryFiveHundredAndFinalRescue(){
        var r=clean(5);r.y=499;r.distance=499;r.act(0);assertEquals(1,r.enemies.size());assertEquals(1,r.enemies.getFirst().boss);
        assertFalse(r.act(0));assertEquals(500,r.y);
        r.hit(r.enemies.getFirst(),10000);assertEquals(1,r.bosses);assertTrue(r.act(0));assertEquals(501,r.y);
        r.enemies.clear();r.bosses=19;r.y=9999;r.distance=9999;r.hp=100;r.act(0);assertEquals(4,r.enemies.getFirst().boss);
        r.hit(r.enemies.getFirst(),10000);assertTrue(r.finished);assertTrue(r.victory);assertEquals(10000,r.distance);assertEquals(20,r.bosses);assertFalse(r.act(0));
    }
    @Test void mapIsConnectedAndMemoryBounded(){
        var r=new CampaignRun(0,123);
        int[][] initial=new int[25][13];for(int y=0;y<25;y++)for(int x=0;x<13;x++)initial[y][x]=r.terrain(x,y);
        for(int row=0;row<10000;row++){r.y=row;r.distance=row;r.generate();assertEquals(0,r.terrain(CampaignMap.pathX(r.seed,row),row));assertTrue(r.enemies.size()<200);assertTrue(r.loot.size()<100);}
        for(int y=0;y<25;y++)for(int x=0;x<13;x++)assertEquals(initial[y][x],r.terrain(x,y),"Revisited terrain must remain stable");
    }
    @Test void resultIsIdempotentAndSeparateFromMultiplayerHistory()throws Exception {
        String path=tmp.resolve("campaign.db").toString();long id;
        try(Database db=new Database(path,tmp.resolve("spool").toString())){
            id=db.submit(()->db.register("commander","password123")).get().id();
            var run=clean(0);run.gold=75;run.distance=777;run.finished=true;
            assertEquals(75L,db.submit(()->CampaignStore.save(db.db,id,"Commander",run)).get());
            assertEquals(75L,db.submit(()->CampaignStore.save(db.db,id,"Commander",run)).get());
            assertTrue(db.submit(()->db.history(id,0,20)).get().isEmpty());
            assertEquals(777,db.submit(()->CampaignStore.ranking(db.db)).get().getFirst().score());
        }
        try(Database db=new Database(path,tmp.resolve("spool").toString())){assertEquals(75L,db.submit(()->CampaignStore.gold(db.db,id)).get());}
    }
    @Test void failedWriteSurvivesRestartWithoutDuplicatingGold()throws Exception {
        String path=tmp.resolve("retry.db").toString(),spool=tmp.resolve("spool").toString();long id;
        try(Database db=new Database(path,spool)){
            id=db.submit(()->db.register("recovery_campaign","password123")).get().id();db.retry.shutdownNow();
            db.submit(()->{try(var s=db.db.createStatement()){s.execute("PRAGMA query_only=ON");}return null;}).get();
            var run=clean(0);run.finished=true;run.gold=88;run.distance=999;
            assertThrows(java.util.concurrent.ExecutionException.class,()->db.campaigns.save(id,"Recovery",run).get());
            assertTrue(Files.exists(Path.of(spool,"campaign",run.id+".json")));
        }
        try(Database db=new Database(path,spool)){
            db.campaigns.retry();db.submit(()->null).get();
            assertEquals(88L,db.submit(()->CampaignStore.gold(db.db,id)).get());
            assertTrue(db.campaigns.pending.isEmpty());
            assertEquals(999,db.submit(()->CampaignStore.ranking(db.db)).get().getFirst().score());
        }
    }
    @Test void publishesJavaStateFixtureForUnityCodec()throws Exception {
        var helper=new RoomFeaturesTest();helper.tmp=tmp;
        try(Server server=helper.server()){
            var session=server.new Session(new Socket());var run=clean(1);run.x=7;run.y=12;run.distance=15;run.hp=75;run.gold=42;run.shields=2;
            run.enemies.add(new CampaignRun.Enemy(33,6,14,160,12,2));run.loot.add(new CampaignRun.Loot(5,13,3,25));session.campaign.run=run;session.campaign.state(123);
            Path fixture=Path.of("target/interop/campaign-state.bin");Files.createDirectories(fixture.getParent());Files.write(fixture,session.outbound.remove());
        }
    }
    @Test void deathAndAccountResetDoNotLeakCampaignState()throws Exception {
        var run=clean(0);run.hp=1;run.enemies.add(new CampaignRun.Enemy(1,6,1,200,7,0));run.act(0);assertTrue(run.finished);assertFalse(run.victory);assertEquals(0,run.hp);
        var helper=new RoomFeaturesTest();helper.tmp=tmp;
        try(Server server=helper.server()){
            var session=server.new Session(new Socket());session.account=server.db.submit(()->server.db.register("campaign_reset","password123")).get();
            session.campaign.run=run;var old=session.campaign;session.logout();server.db.submit(()->null).get();assertTrue(old.detached);assertNull(session.campaign.run);assertNull(session.account);
        }
    }
    @Test void visualEventsSeparateDamageHealingAndBlockedHits() {
        var r=clean(0);r.hp=50;r.enemies.add(new CampaignRun.Enemy(1,6,1,20,7,0));r.enemies.add(new CampaignRun.Enemy(2,7,0,60,9,0));r.loot.add(new CampaignRun.Loot(6,0,0,0));
        r.act(0);assertEquals(66,r.hp);assertEquals(1,r.kills);
        assertTrue(r.visuals.stream().anyMatch(v->v.kind()==CampaignRun.HIT&&v.target()==0&&v.amount()==9));
        assertTrue(r.visuals.stream().anyMatch(v->v.kind()==CampaignRun.HEAL&&v.amount()==25));
        assertTrue(r.visuals.stream().anyMatch(v->v.kind()==CampaignRun.DEATH&&v.target()==1));
        r.shields=1;r.act(1);assertEquals(66,r.hp);
        assertTrue(r.visuals.stream().anyMatch(v->v.kind()==CampaignRun.BLOCK&&v.target()==0));
        assertFalse(r.visuals.stream().anyMatch(v->v.kind()==CampaignRun.HIT&&v.target()==0));
        int turn=r.turn,count=r.visuals.size();r.x=0;assertFalse(r.act(3));assertEquals(turn,r.turn);assertEquals(count,r.visuals.size());
    }
    @Test void publishesVisualExtensionFixtureAndLegacyPayloadRemainsUnchanged()throws Exception {
        var helper=new RoomFeaturesTest();helper.tmp=tmp;
        try(Server server=helper.server()){
            var session=server.new Session(new Socket());var run=clean(0);run.hp=50;
            run.enemies.add(new CampaignRun.Enemy(1,6,1,20,7,0));run.enemies.add(new CampaignRun.Enemy(2,7,0,60,9,0));run.loot.add(new CampaignRun.Loot(6,0,0,0));run.act(0);
            session.campaign.run=run;session.campaign.state(124);var legacy=session.outbound.remove();
            session.campaign.visualEvents=true;session.campaign.state(124);var modern=session.outbound.remove();
            byte[] a=decode(legacy,false).payload(),b=decode(modern,false).payload();assertArrayEquals(a,java.util.Arrays.copyOf(b,a.length));assertTrue(b.length>a.length);
            Path path=Path.of("target/interop/campaign-motion.bin");Files.createDirectories(path.getParent());Files.write(path,modern);
        }
    }
    @Test void tcpCampaignHasOwnStateAndRejectsRoomEntryAndStaleTurns()throws Exception {
        var helper=new RoomFeaturesTest();helper.tmp=tmp;
        try(Server server=helper.server()){
            server.start();try(Socket socket=new Socket("127.0.0.1",server.tcp.getLocalPort())){
                socket.setSoTimeout(5000);byte[] token=BaselineTest.auth(socket,"campaign_user");
                var session=server.tokens.get(java.util.HexFormat.of().formatHex(token));
                BaselineTest.send(socket,90,new Writer().u8(2).u8(0).u8(0).done());assertEquals(0,new Reader(BaselineTest.waitFor(socket,91).payload()).u16());
                assertNull(session.room);assertTrue(server.rooms.isEmpty());
                BaselineTest.send(socket,CREATE_ROOM,new Writer().str("Blocked").done());assertNotEquals(0,new Reader(BaselineTest.waitFor(socket,ACTION_RESULT).payload()).u16());
                for(int i=0;i<2;i++){BaselineTest.send(socket,90,new Writer().u8(2).u8(1).u32(0).u8(0).done());BaselineTest.waitFor(socket,91);}
                assertEquals(1,session.campaign.run.turn);assertEquals(1,session.campaign.run.distance);
                BaselineTest.send(socket,90,new Writer().u8(2).u8(2).done());BaselineTest.waitFor(socket,91);
                server.db.submit(()->null).get();assertTrue(session.campaign.run.finished);
                assertEquals(1,server.db.submit(()->CampaignStore.ranking(server.db.db)).get().getFirst().score());
            }
        }
    }
}
