package tank;

import java.nio.file.*;
import java.util.concurrent.*;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
import static tank.Protocol.*;

class RoleplayShopTest {
    @TempDir Path tmp;
    Database database()throws Exception {return new Database(tmp.resolve("wallet.db").toString(),tmp.resolve("spool").toString());}
    void fund(Database db,long account,int gold)throws Exception {
        db.submit(()->{try(var s=db.db.prepareStatement("INSERT INTO campaign_profiles VALUES(?,?,0) ON CONFLICT(account_id) DO UPDATE SET gold=excluded.gold")){s.setLong(1,account);s.setInt(2,gold);s.executeUpdate();}return null;}).get();
    }
    long balance(Database db,long account)throws Exception{return db.submit(()->CampaignStore.gold(db.db,account)).get();}
    @Test void campaignStartsAtZeroAndCreditsOnlyRemainingGoldOnce()throws Exception {
        try(Database db=database()) {
            long account=db.submit(()->db.register("wallet","password123").id()).get();
            CampaignRun run=new CampaignRun(0,7);assertEquals(0,run.gold);run.gold=150;
            assertTrue(run.buy(1));int remainder=run.gold;assertTrue(remainder<150);
            run.finish(false);assertEquals(remainder,db.campaigns.save(account,"Wallet",run).get());
            assertEquals(remainder,db.campaigns.save(account,"Wallet",run).get());assertEquals(remainder,balance(db,account));
        }
    }
    @Test void walletDebitsOnceRejectsInsufficientFundsAndRecoversReservations()throws Exception {
        long account;var reserved=new WalletStore.Purchase(1,1,1,0);
        try(Database db=database()) {
            account=db.submit(()->db.register("wallet","password123").id()).get();fund(db,account,150);
            var bought=new WalletStore.Purchase(1,account,1,1);
            assertEquals(100,db.submit(()->WalletStore.reserve(db.db,bought)).get());
            assertNull(db.submit(()->WalletStore.reserve(db.db,bought)).get());
            assertEquals(100,db.wallet.finish(bought,true).get());
            assertEquals(100,db.wallet.finish(bought,false).get(),"A granted item must never be refunded by a stale callback.");
            reserved=new WalletStore.Purchase(1,account,1,2);var p=reserved;
            assertEquals(50,db.submit(()->WalletStore.reserve(db.db,p)).get());
            var refund=new WalletStore.Purchase(1,account,1,3);
            assertEquals(0,db.submit(()->WalletStore.reserve(db.db,refund)).get());
            assertNull(db.submit(()->WalletStore.reserve(db.db,new WalletStore.Purchase(1,account,1,4))).get());
            assertEquals(50,db.wallet.finish(refund,false).get());assertEquals(50,db.wallet.finish(refund,false).get());
            var retried=new WalletStore.Purchase(1,account,1,3);
            assertEquals(0,db.submit(()->WalletStore.reserve(db.db,retried)).get());
            assertEquals(0,db.wallet.finish(refund,false).get(),"An old refund must not affect a new reservation.");
            assertEquals(50,db.wallet.finish(retried,false).get());
        }
        try(Database db=database()){assertEquals(100,balance(db,account));WalletStore.schema(db.db);assertEquals(100,balance(db,account));}
    }
    RoleplayRoomTest fixture(){var f=new RoleplayRoomTest();f.tmp=tmp;return f;}
    void await(Room room,RoleplayRoomTest f)throws Exception {
        long until=System.nanoTime()+TimeUnit.SECONDS.toNanos(5);
        while(room.members.values().stream().anyMatch(m->m.roleplay.shopPending!=null)&&System.nanoTime()<until){f.tick(room);Thread.sleep(5);}
        assertTrue(room.members.values().stream().allMatch(m->m.roleplay.shopPending==null));
    }
    Packet response(Server.Session s,int seq) {return s.outbound.stream().map(raw->decode(raw,false)).filter(p->p.op()==SHOP_RESULT&&p.sequence()==seq).findFirst().orElseThrow();}
    @Test void purchasesAreStoredAndUsedOnDemandAcrossDeathRejoinAndWaves()throws Exception {
        var f=fixture();try(Server s=f.server()) {
            Room r=f.room(s,3);r.matchTicks=5400;f.running(r);var m=r.members.get(0);fund(s.db,m.account.id(),1000);r.world.tick=1200;
            r.roleplay.shop.buy(m.session,r.match,1,WorldItems.MIRROR,10);r.roleplay.shop.buy(m.session,r.match,1,WorldItems.MIRROR,11);await(r,f);
            assertEquals(950,balance(s.db,m.account.id()));assertEquals(0,r.world.tanks.get(0).mirrorShots);assertEquals(1,m.roleplay.inventory[WorldItems.MIRROR]);
            assertEquals(0,new Reader(response(m.session,10).payload()).u16());
            r.world.items.apply(r.world.tanks.get(0),WorldItems.ARMOR);
            r.roleplay.shop.buy(m.session,r.match,1,WorldItems.ARMOR,12);await(r,f);assertEquals(900,balance(s.db,m.account.id()));
            r.world.damage(r.world.tanks.get(0),1,500,1);r.world.spawn(r.world.tanks.get(0));
            assertEquals(1,m.roleplay.inventory[WorldItems.ARMOR]);assertEquals(1,m.roleplay.inventory[WorldItems.MIRROR]);
            r.roleplay.shop.buy(m.session,r.match,1,WorldItems.MIRROR,13);assertEquals(1,new Reader(response(m.session,13).payload()).u16());
            r.world.tick=1800;int revision=m.roleplay.inventoryRevision;
            r.roleplay.shop.use(m.session,r.match,revision,WorldItems.MIRROR,14);assertEquals(0,m.roleplay.inventory[WorldItems.MIRROR]);assertEquals(3,r.world.tanks.get(0).mirrorShots);
            var original=m.session;r.leave(original,1);original.room=r;r.rejoin(original,15);original.endpoint=new java.net.InetSocketAddress(java.net.InetAddress.getLoopbackAddress(),31000);r.prepared(original,r.match,16);
            assertEquals(1,m.roleplay.inventory[WorldItems.ARMOR]);
            r.world.tick=2400;r.roleplay.shop.buy(original,r.match,2,WorldItems.ARMOR,17);await(r,f);assertEquals(850,balance(s.db,m.account.id()));assertEquals(2,m.roleplay.inventory[WorldItems.ARMOR]);
            r.world.tick=3000;revision=m.roleplay.inventoryRevision;
            r.roleplay.shop.use(original,r.match,revision,WorldItems.ARMOR,18);assertEquals(1,m.roleplay.inventory[WorldItems.ARMOR]);assertEquals(r.world.tick+GameRules.ArmorTicks,r.world.tanks.get(0).armorEndTick);
            r.roleplay.shop.use(original,r.match,revision,WorldItems.ARMOR,19);assertEquals(1,m.roleplay.inventory[WorldItems.ARMOR],"Retrying an old revision must not consume a second item.");
            r.world.tanks.get(0).hp=0;r.roleplay.shop.use(original,r.match,m.roleplay.inventoryRevision,WorldItems.ARMOR,23);assertEquals(1,m.roleplay.inventory[WorldItems.ARMOR]);
            assertEquals(850,balance(s.db,m.account.id()),"Using inventory never charges gold again.");
        }
    }
    @Test void closingWindowDuringDebitRefundsWithoutGrantingOrBlockingRoom()throws Exception {
        var f=fixture();try(Server s=f.server()) {
            Room r=f.room(s,2);r.matchTicks=5400;f.running(r);var m=r.members.get(0);fund(s.db,m.account.id(),100);r.world.tick=1200;
            var release=new CountDownLatch(1);var entered=new CountDownLatch(1);
            s.db.submit(()->{entered.countDown();release.await(5,TimeUnit.SECONDS);return null;});assertTrue(entered.await(1,TimeUnit.SECONDS));
            r.roleplay.shop.buy(m.session,r.match,1,WorldItems.MIRROR,20);f.tick(r);assertEquals(1201,r.world.tick);
            r.world.tick=1800;release.countDown();await(r,f);
            assertEquals(100,balance(s.db,m.account.id()));assertEquals(0,r.world.tanks.get(0).mirrorShots);
            assertEquals(1,new Reader(response(m.session,20).payload()).u16());
            r.roleplay.shop.buy(m.session,r.match,1,WorldItems.MIRROR,21);assertEquals(100,balance(s.db,m.account.id()));
            r.world.tick=2400;r.world.tanks.get(0).hp=0;r.roleplay.shop.buy(m.session,r.match,2,0,22);assertEquals(100,balance(s.db,m.account.id()));
        }
    }
}
