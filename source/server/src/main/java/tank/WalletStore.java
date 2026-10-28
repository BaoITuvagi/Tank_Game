package tank;

import java.sql.*;
import java.util.concurrent.*;

/** Shares the campaign wallet. All SQL runs on Database.worker. */
final class WalletStore {
    record Purchase(long match,long account,int wave,int item,String reservation) {
        Purchase(long match,long account,int wave,int item) {this(match,account,wave,item,java.util.UUID.randomUUID().toString());}
    }
    final Database database;
    final ConcurrentHashMap<Purchase,Boolean> pending=new ConcurrentHashMap<>();
    final java.util.Set<Purchase> writing=ConcurrentHashMap.newKeySet();
    WalletStore(Database database) {this.database=database;}
    static void schema(Connection db)throws SQLException {
        try(var s=db.createStatement()) {
            s.execute("CREATE TABLE IF NOT EXISTS roleplay_purchases(match_id INTEGER NOT NULL,account_id INTEGER NOT NULL REFERENCES accounts(id),wave INTEGER NOT NULL,item INTEGER NOT NULL,cost INTEGER NOT NULL,state INTEGER NOT NULL,reservation TEXT NOT NULL,PRIMARY KEY(match_id,account_id,wave,item))");
        }
        // A reservation without a confirmed grant is returned after a server restart.
        db.setAutoCommit(false);
        try(var s=db.createStatement()) {
            s.executeUpdate("UPDATE campaign_profiles SET gold=gold+COALESCE((SELECT SUM(cost) FROM roleplay_purchases WHERE account_id=campaign_profiles.account_id AND state=0),0)");
            s.executeUpdate("UPDATE roleplay_purchases SET state=2 WHERE state=0");
            db.commit();
        }catch(SQLException e){db.rollback();throw e;}finally{db.setAutoCommit(true);}
    }
    static Long reserve(Connection db,Purchase p)throws SQLException {
        db.setAutoCommit(false);
        try {
            try(var s=db.prepareStatement("DELETE FROM roleplay_purchases WHERE match_id=? AND account_id=? AND wave=? AND item=? AND state=2")) {key(s,p);s.executeUpdate();}
            try(var s=db.prepareStatement("SELECT 1 FROM roleplay_purchases WHERE match_id=? AND account_id=? AND wave=? AND item=?")) {
                key(s,p);try(var r=s.executeQuery()){if(r.next()){db.rollback();return null;}}
            }
            try(var s=db.prepareStatement("UPDATE campaign_profiles SET gold=gold-? WHERE account_id=? AND gold>=?")) {
                s.setInt(1,GameRules.RoleplayItemPrice);s.setLong(2,p.account());s.setInt(3,GameRules.RoleplayItemPrice);
                if(s.executeUpdate()!=1){db.rollback();return null;}
            }
            try(var s=db.prepareStatement("INSERT INTO roleplay_purchases VALUES(?,?,?,?,?,0,?)")) {key(s,p);s.setInt(5,GameRules.RoleplayItemPrice);s.setString(6,p.reservation());s.executeUpdate();}
            long balance=CampaignStore.gold(db,p.account());db.commit();return balance;
        }catch(SQLException e){db.rollback();throw e;}finally{db.setAutoCommit(true);}
    }
    static long settle(Connection db,Purchase p,boolean granted)throws SQLException {
        db.setAutoCommit(false);
        try {
            if(!granted)try(var s=db.prepareStatement("UPDATE campaign_profiles SET gold=gold+COALESCE((SELECT cost FROM roleplay_purchases WHERE match_id=? AND account_id=? AND wave=? AND item=? AND reservation=? AND state=0),0) WHERE account_id=?")) {
                key(s,p);s.setString(5,p.reservation());s.setLong(6,p.account());s.executeUpdate();
            }
            try(var s=db.prepareStatement("UPDATE roleplay_purchases SET state=? WHERE match_id=? AND account_id=? AND wave=? AND item=? AND reservation=? AND state=0")) {
                s.setInt(1,granted?1:2);s.setLong(2,p.match());s.setLong(3,p.account());s.setInt(4,p.wave());s.setInt(5,p.item());s.setString(6,p.reservation());s.executeUpdate();
            }
            long balance=CampaignStore.gold(db,p.account());db.commit();return balance;
        }catch(SQLException e){db.rollback();throw e;}finally{db.setAutoCommit(true);}
    }
    static void key(PreparedStatement s,Purchase p)throws SQLException {s.setLong(1,p.match());s.setLong(2,p.account());s.setInt(3,p.wave());s.setInt(4,p.item());}
    CompletableFuture<Long> finish(Purchase p,boolean granted) {
        pending.put(p,granted);
        return persist(p,granted);
    }
    CompletableFuture<Long> persist(Purchase p,boolean granted) {
        if(!writing.add(p))return CompletableFuture.failedFuture(new IllegalStateException("Wallet settlement in progress"));
        return database.submit(()->settle(database.db,p,granted)).whenComplete((gold,error)->{
            if(error==null)pending.remove(p,granted);
            writing.remove(p);
        });
    }
    void retry() {pending.forEach((p,granted)->{if(!writing.contains(p))persist(p,granted);});}
}
