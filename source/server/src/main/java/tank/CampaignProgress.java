package tank;

import java.sql.*;
import java.security.SecureRandom;

/** Each acknowledged command atomically saves state, receipt, and any final reward. DB-worker only. */
final class CampaignProgress {
    record Saved(CampaignRun run,int revision,boolean paused,long gold){}
    static void schema(Connection db)throws SQLException {
        try(var s=db.createStatement()){
            s.execute("CREATE TABLE IF NOT EXISTS campaign_commands(account_id INTEGER NOT NULL,request_id TEXT NOT NULL,payload TEXT NOT NULL,PRIMARY KEY(account_id,request_id))");
            s.execute("CREATE TABLE IF NOT EXISTS campaign_progress(account_id INTEGER PRIMARY KEY REFERENCES accounts(id),run_id TEXT NOT NULL,revision INTEGER NOT NULL,state TEXT NOT NULL,status TEXT NOT NULL,request_id TEXT NOT NULL,payload TEXT NOT NULL,updated INTEGER NOT NULL)");
        }
    }
    static Saved apply(Database db,long account,String name,int command,String id,int expected,String request,String payload,int value,boolean sessionActive)throws Exception {
        String json=null,status="",lastRequest="",lastPayload="";int revision=0;long updated=0;
        try(var s=db.db.prepareStatement("SELECT * FROM campaign_progress WHERE account_id=?")){
            s.setLong(1,account);try(var r=s.executeQuery()){if(r.next()){json=r.getString("state");status=r.getString("status");revision=r.getInt("revision");lastRequest=r.getString("request_id");lastPayload=r.getString("payload");updated=r.getLong("updated");}}
        }
        CampaignRun run=json==null?null:CampaignCheckpoint.decode(json);
        boolean seen=false;
        if(command!=4)try(var stmt=db.db.prepareStatement("SELECT payload FROM campaign_commands WHERE account_id=? AND request_id=?")){
            stmt.setLong(1,account);stmt.setString(2,request);try(var row=stmt.executeQuery()){if(row.next()){if(!payload.equals(row.getString(1)))throw new IllegalArgumentException("Mã thao tác đã dùng cho nội dung khác.");seen=true;}}
        }
        if(command!=4&&(seen||request.equals(lastRequest))){
            if(!seen&&!payload.equals(lastPayload))throw new IllegalArgumentException("Mã thao tác đã dùng cho nội dung khác.");
            return new Saved(run,revision,!status.equals("ACTIVE"),CampaignStore.gold(db.db,account));
        }
        if(command==4){
            if(run!=null&&!run.finished&&!sessionActive){status="PAUSED";try(var s=db.db.prepareStatement("UPDATE campaign_progress SET status='PAUSED',updated=? WHERE account_id=?")){s.setLong(1,System.currentTimeMillis());s.setLong(2,account);s.executeUpdate();}}
            return new Saved(run,revision,!status.equals("ACTIVE"),CampaignStore.gold(db.db,account));
        }
        if(command==0){
            if(run!=null&&!run.finished)throw new IllegalArgumentException("Hãy tiếp tục hoặc kết thúc chuyến đi đã lưu.");
            run=new CampaignRun(value,new SecureRandom().nextLong());revision=0;status="ACTIVE";
        }else{
            if(run==null||!run.id.equals(id)||revision!=expected)throw new IllegalArgumentException("Chuyến đi đã thay đổi. Hãy tải lại bản lưu.");
            if(run.finished)throw new IllegalArgumentException("Chuyến đi đã kết thúc.");
            if(status.equals("ACTIVE")&&sessionActive)run.accumulatedMillis+=Math.max(0,System.currentTimeMillis()-updated);
            if(command==1||command==5){
                if(!sessionActive||!status.equals("ACTIVE"))throw new IllegalArgumentException("Hãy tiếp tục chuyến đi trước khi thao tác.");
                if(command==1)run.act(value);else run.buy(value);
            }else if(command==2){run.finish(false);run.message="Đã kết thúc chuyến đi.";}
            else if(command==6)status="PAUSED";
            else if(command==7)status="ACTIVE";
            else throw new IllegalArgumentException("Thao tác chiến dịch không hợp lệ.");
        }
        if(run.finished)status="FINISHED";
        String saved=CampaignCheckpoint.encode(run);long now=System.currentTimeMillis();revision++;
        db.db.setAutoCommit(false);
        try {
            if(run.finished)CampaignStore.record(db.db,new CampaignStore.Result(run.id,account,name,run.nation,run.distance,run.gold,run.victory,run.elapsedMillis()));
            try(var s=db.db.prepareStatement("INSERT INTO campaign_progress VALUES(?,?,?,?,?,?,?,?) ON CONFLICT(account_id) DO UPDATE SET run_id=excluded.run_id,revision=excluded.revision,state=excluded.state,status=excluded.status,request_id=excluded.request_id,payload=excluded.payload,updated=excluded.updated")){
                s.setLong(1,account);s.setString(2,run.id);s.setInt(3,revision);s.setString(4,saved);s.setString(5,status);s.setString(6,request);s.setString(7,payload);s.setLong(8,now);s.executeUpdate();
            }
            try(var receipt=db.db.prepareStatement("INSERT INTO campaign_commands VALUES(?,?,?)")){receipt.setLong(1,account);receipt.setString(2,request);receipt.setString(3,payload);receipt.executeUpdate();}
            db.db.commit();
        }catch(Exception e){db.db.rollback();throw e;}finally{db.db.setAutoCommit(true);}
        return new Saved(run,revision,!status.equals("ACTIVE"),CampaignStore.gold(db.db,account));
    }
}
