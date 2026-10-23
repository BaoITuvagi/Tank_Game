package tank;

import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import java.nio.file.*;
import java.net.Socket;
import java.util.Arrays;
import static tank.CampaignRun.*;
import static tank.Protocol.*;
import static org.junit.jupiter.api.Assertions.*;

class CampaignShopTest {
    @TempDir Path tmp;
    CampaignRun clean(){var r=new CampaignRun(0,42);r.enemies.clear();r.loot.clear();r.generated=GOAL;return r;}
    @Test void bothSidesHaveTheSameFourCellFiringLimit(){
        var r=clean();var gun=new Enemy(1,6,SHOT_RANGE,100,8,0);gun.role=Role.RANGED;r.enemies.add(gun);
        assertTrue(r.act(4));assertEquals(60,gun.hp);assertEquals(92,r.hp);
        r=clean();gun=new Enemy(1,6,SHOT_RANGE+1,100,8,0);gun.role=Role.RANGED;r.enemies.add(gun);
        r.act(4);assertEquals(100,gun.hp);assertEquals(100,r.hp);
        assertTrue(r.visuals.stream().noneMatch(v->v.kind()==HIT));
    }
    @Test void purchaseHealsOnlyToTheCapAndDoesNotAdvanceCombat(){
        var r=clean();r.hp=90;r.gold=100;r.shot=5;r.skill=20;
        var e=new Enemy(1,6,1,100,8,0);r.enemies.add(e);
        assertTrue(r.buy(0));assertEquals(100,r.hp);assertEquals(80,r.gold);assertEquals(1,r.shopRevision);
        assertEquals(0,r.turn);assertEquals(5,r.shot);assertEquals(20,r.skill);assertEquals(1,e.y);assertEquals(100,e.hp);
        assertEquals(0,r.x-6);assertEquals(0,r.y);
    }
    @Test void rejectsInsufficientGoldFullStatsInvalidItemsAndFinishedRuns(){
        var r=clean();r.hp=50;r.gold=19;assertFalse(r.buy(0));assertEquals(50,r.hp);assertEquals(19,r.gold);
        r.gold=100;r.hp=100;assertFalse(r.buy(0));assertFalse(r.buy(-1));assertFalse(r.buy(3));
        r.shields=9;assertFalse(r.buy(1));r.doubleHits=9;assertFalse(r.buy(2));
        r.finished=true;r.shields=0;assertFalse(r.buy(1));assertEquals(100,r.gold);assertEquals(0,r.shopRevision);
    }
    @Test void shieldsAndDamageStackButCannotOverfill(){
        var r=clean();r.gold=1000;r.shields=6;r.doubleHits=6;
        assertTrue(r.buy(1));assertTrue(r.buy(2));assertEquals(9,r.shields);assertEquals(9,r.doubleHits);int balance=r.gold;
        assertFalse(r.buy(1));assertFalse(r.buy(2));assertEquals(balance,r.gold);
        r.shields=8;r.doubleHits=7;assertFalse(r.buy(1));assertFalse(r.buy(2));assertEquals(balance,r.gold);
    }
    @Test void pricesFollowFurthestDistanceAndCannotBeReducedByWalkingBack(){
        var r=clean();assertEquals(20,r.price(0));assertEquals(30,r.price(1));assertEquals(35,r.price(2));
        int[] previous={0,0,0};
        for(int m=0;m<=10000;m+=250){r.distance=m;for(int item=0;item<3;item++){assertTrue(r.price(item)>previous[item]);previous[item]=r.price(item);}}
        r.y=0;assertEquals(previous[0],r.price(0));
    }
    @Test void purchasedDamageAppliesToShotsAndMeleeAndMissesDoNotConsumeIt(){
        var r=clean();r.gold=100;assertTrue(r.buy(2));
        var target=new Enemy(1,6,4,300,0,0);r.enemies.add(target);r.act(4);assertEquals(220,target.hp);assertEquals(2,r.doubleHits);
        r.enemies.clear();r.shot=0;r.act(4);assertEquals(2,r.doubleHits);
        target=new Enemy(2,6,1,200,0,0);r.enemies.add(target);r.act(0);assertEquals(160,target.hp);assertEquals(1,r.doubleHits);
    }
    @Test void mapPickupsPreservePurchasedCharges(){
        var r=clean();r.shields=6;r.doubleHits=6;
        r.loot.add(new Loot(6,1,2,0));r.act(0);assertEquals(9,r.shields);
        r.loot.add(new Loot(6,2,1,0));r.act(0);assertEquals(9,r.doubleHits);
    }
    @Test void endingRunCreditsOnlyUnspentGold()throws Exception {
        try(Database db=new Database(tmp.resolve("shop.db").toString(),tmp.resolve("spool").toString())){
            long account=db.submit(()->db.register("shop_saver","password123")).get().id();
            var r=clean();r.gold=100;r.hp=50;assertTrue(r.buy(0));r.finished=true;
            assertEquals(80L,db.submit(()->CampaignStore.save(db.db,account,"Shop",r)).get());
            assertEquals(80L,db.submit(()->CampaignStore.save(db.db,account,"Shop",r)).get());
            assertTrue(db.submit(()->db.history(account,0,20)).get().isEmpty());
        }
    }
    @Test void tcpPurchaseRejectsReplaysWrongRunAndStaleTurn()throws Exception {
        var helper=new RoomFeaturesTest();helper.tmp=tmp;
        try(Server server=helper.server()){
            server.start();try(var socket=new Socket("127.0.0.1",server.tcp.getLocalPort())){
                socket.setSoTimeout(5000);byte[] token=BaselineTest.auth(socket,"shop_user");
                var session=server.tokens.get(java.util.HexFormat.of().formatHex(token));
                BaselineTest.send(socket,90,new Writer().u8(4).u8(0).u8(0).done());BaselineTest.waitFor(socket,91);
                var r=session.campaign.run;r.gold=100;r.hp=40;
                byte[] buy=new Writer().u8(4).u8(5).str(r.id).u32(0).u32(0).u8(0).done();
                for(int i=0;i<2;i++){BaselineTest.send(socket,90,buy);assertEquals(0,new Reader(BaselineTest.waitFor(socket,91).payload()).u16());}
                assertEquals(65,r.hp);assertEquals(80,r.gold);assertEquals(1,r.shopRevision);assertEquals(0,r.turn);
                for(byte[] invalid:new byte[][]{
                    new Writer().u8(4).u8(5).str("old-run").u32(0).u32(1).u8(1).done(),
                    new Writer().u8(4).u8(5).str(r.id).u32(1).u32(1).u8(1).done()}){
                    BaselineTest.send(socket,90,invalid);BaselineTest.waitFor(socket,91);
                }
                assertEquals(80,r.gold);assertEquals(0,r.shields);assertNull(session.room);
            }
        }
    }
    @Test void exportsShopMetadataWithoutChangingVersionThreePayload()throws Exception {
        var helper=new RoomFeaturesTest();helper.tmp=tmp;
        try(Server server=helper.server()){
            var session=server.new Session(new Socket());var r=clean();r.hp=60;r.gold=180;r.distance=500;r.buy(0);
            for(int i=0;i<3;i++){var e=new Enemy(i+1,6+(i==1?1:0),3-i,100,8,0);e.role=Role.values()[(i+1)%3];e.squad=7;r.enemies.add(e);}
            CampaignTactics.refreshGuards(r);session.campaign.run=r;session.campaign.visualEvents=session.campaign.tactics=true;
            session.campaign.state(127);byte[] legacy=decode(session.outbound.remove(),false).payload();
            session.campaign.shop=true;session.campaign.state(127);byte[] packet=session.outbound.remove();
            assertArrayEquals(legacy,Arrays.copyOf(decode(packet,false).payload(),legacy.length));
            Path path=Path.of("target/interop/campaign-shop.bin");Files.createDirectories(path.getParent());Files.write(path,packet);
        }
    }
}
