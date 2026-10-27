package tank;

import java.util.concurrent.ConcurrentLinkedQueue;
import static tank.Protocol.*;

/** Gameplay validation and grants belong to the room; wallet work never blocks its tick. */
final class RoleplayShop {
    final Room room;
    final ConcurrentLinkedQueue<Runnable> completions=new ConcurrentLinkedQueue<>();
    RoleplayShop(Room room) {this.room=room;}
    void tick() {Runnable action;while((action=completions.poll())!=null)action.run();}
    static void failure(Server.Session s,int seq,String message) {s.send(SHOP_RESULT,seq,new Writer().u16(1).str(message).done());}
    static void result(Server.Session s,int seq,long gold,long match,int wave,int mask,int revision,int[] inventory) {
        Writer out=new Writer().u16(0).u8(2).u64(gold).u64(match).u8(wave).u8(mask).u32(revision);
        for(int count:inventory)out.u8(count);
        s.send(SHOP_RESULT,seq,out.done());
    }
    static void wallet(Server.Session s,Database db,int seq,long match,int wave,int mask,int revision,int[] inventory) {
        long account=s.account.id();
        db.submit(()->CampaignStore.gold(db.db,account)).whenComplete((gold,error)->{
            if(s.account==null||s.account.id()!=account)return;
            if(error!=null)failure(s,seq,"Không tải được số dư vàng. Hãy thử lại.");else result(s,seq,gold,match,wave,mask,revision,inventory);
        });
    }
    int wave() {return room.phase==RoomState.RUNNING&&room.world!=null?room.world.items.wave():0;}
    void query(Server.Session s,long match,int seq) {
        Room.Member m=room.member(s);
        if(m==null||match!=room.match){failure(s,seq,"Trận đấu đã thay đổi.");return;}
        state(s,m,seq);
    }
    void state(Server.Session s,Room.Member m,int seq) {
        int wave=wave();wallet(s,room.server.db,seq,room.match,wave,m.roleplay.shopWave==wave?m.roleplay.shopMask:0,m.roleplay.inventoryRevision,m.roleplay.inventory.clone());
    }
    boolean active(Server.Session s,Room.Member m,long match,int item) {
        return !room.stopRequested&&room.alive&&room.phase==RoomState.RUNNING&&room.world!=null&&room.match==match&&room.member(s)==m&&!m.rejoining&&s.open&&s.account!=null&&s.account.id()==m.account.id()&&item>=0&&item<GameRules.ItemTypeCount&&room.world.tanks.containsKey(m.slot)&&room.world.tanks.get(m.slot).hp>0;
    }
    boolean valid(Server.Session s,Room.Member m,long match,int wave,int item) {
        return active(s,m,match,item)&&wave>0&&wave()==wave;
    }
    void use(Server.Session s,long match,int revision,int item,int seq) {
        Room.Member m=room.member(s);
        if(m==null||!active(s,m,match,item)){failure(s,seq,"Chỉ dùng vật phẩm khi xe còn sống trong trận.");return;}
        var player=m.roleplay;
        if(player.shopPending!=null||revision!=player.inventoryRevision){failure(s,seq,"Túi vật phẩm đang cập nhật. Hãy thử lại.");return;}
        if(player.inventory[item]==0){failure(s,seq,"Bạn chưa có vật phẩm này.");return;}
        player.inventory[item]--;player.inventoryRevision++;
        room.world.items.apply(room.world.tanks.get(m.slot),item);
        state(s,m,seq);
    }
    void buy(Server.Session s,long match,int wave,int item,int seq) {
        Room.Member m=room.member(s);
        if(m==null||!valid(s,m,match,wave,item)){failure(s,seq,"Chỉ mua khi xe còn sống và đợt vật phẩm đang mở.");return;}
        var player=m.roleplay;
        if(player.shopPending!=null){failure(s,seq,"Đang xử lý giao dịch trước.");return;}
        if(player.shopWave!=wave){player.shopWave=wave;player.shopMask=0;}
        if((player.shopMask&(1<<item))!=0){failure(s,seq,"Bạn đã mua loại này trong đợt hiện tại.");return;}
        var purchase=new WalletStore.Purchase(match,m.account.id(),wave,item);
        player.shopPending=purchase;
        room.server.db.submit(()->WalletStore.reserve(room.server.db.db,purchase)).whenComplete((gold,error)->{
            Runnable complete=()->{
                if(error!=null||gold==null) {
                    if(purchase.equals(player.shopPending))player.shopPending=null;
                    failure(s,seq,error==null?"Không đủ vàng hoặc loại này đã được mua.":"Không trừ được vàng. Hãy thử lại.");return;
                }
                boolean granted=valid(s,m,match,wave,item);
                if(granted){player.shopWave=wave;player.shopMask|=1<<item;player.inventory[item]++;player.inventoryRevision++;}
                room.server.db.wallet.finish(purchase,granted).whenComplete((balance,settleError)->{
                    // Serialize pending state with room commands, including logout/rejoin.
                    Runnable reply=()->{
                        if(purchase.equals(player.shopPending))player.shopPending=null;
                        if(!s.open||s.account==null||s.account.id()!=purchase.account())return;
                        if(granted&&room.match==match&&room.member(s)==m)state(s,m,seq);
                        else if(granted)result(s,seq,settleError==null?balance:gold,match,wave,1<<item,player.inventoryRevision,player.inventory.clone());
                        else failure(s,seq,settleError==null?"Đợt đã đóng hoặc xe không còn sẵn sàng. Vàng đã được hoàn lại.":"Giao dịch hủy; vàng đang chờ hoàn lại.");
                    };
                    if(room.alive&&!room.stopRequested)completions.add(reply);else reply.run();
                });
            };
            if(room.alive&&!room.stopRequested)completions.add(complete);else complete.run();
        });
    }
}
