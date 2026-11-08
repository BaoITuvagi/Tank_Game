package tank;

import java.sql.*;
import java.nio.charset.StandardCharsets;
import java.time.*;
import java.util.*;
import static tank.Protocol.*;

/** Durable moderation inbox, always accessed on the game database worker. */
final class PlayerReports {
    static final int SUBMIT=REPORT_PLAYER,RESULT=REPORT_RESULT,REASONS=63;
    final Database owner;
    PlayerReports(Database owner){this.owner=owner;}
    static void schema(Connection db)throws SQLException {
        try(var s=db.createStatement()) {
            s.execute("CREATE TABLE IF NOT EXISTS player_reports(id INTEGER PRIMARY KEY,match_id INTEGER NOT NULL,reporter_id INTEGER NOT NULL,target_id INTEGER NOT NULL,reporter_name TEXT NOT NULL,target_name TEXT NOT NULL,reporter_account TEXT NOT NULL,target_account TEXT NOT NULL,map_id INTEGER NOT NULL,room_mode TEXT NOT NULL,reasons INTEGER NOT NULL,details TEXT NOT NULL,created INTEGER NOT NULL,status TEXT NOT NULL DEFAULT 'OPEN',review_note TEXT NOT NULL DEFAULT '',reviewed_by TEXT NOT NULL DEFAULT '',updated INTEGER NOT NULL,UNIQUE(match_id,reporter_id,target_id))");
            s.execute("CREATE INDEX IF NOT EXISTS reports_created ON player_reports(created DESC,id DESC)");
            s.execute("CREATE INDEX IF NOT EXISTS reports_status ON player_reports(status,created DESC)");
            s.execute("CREATE TABLE IF NOT EXISTS report_reviews(id INTEGER PRIMARY KEY,report_id INTEGER NOT NULL REFERENCES player_reports(id),admin TEXT NOT NULL,status TEXT NOT NULL,note TEXT NOT NULL,time INTEGER NOT NULL)");
            s.execute("CREATE TABLE IF NOT EXISTS account_sanctions(id INTEGER PRIMARY KEY,account_id INTEGER NOT NULL REFERENCES accounts(id),report_id INTEGER NOT NULL REFERENCES player_reports(id),action TEXT NOT NULL,reason TEXT NOT NULL,admin TEXT NOT NULL,created INTEGER NOT NULL,expires INTEGER NOT NULL DEFAULT 0,revoked INTEGER NOT NULL DEFAULT 0,revoked_by TEXT NOT NULL DEFAULT '',revoke_reason TEXT NOT NULL DEFAULT '',revoked_at INTEGER NOT NULL DEFAULT 0)");
            s.execute("CREATE INDEX IF NOT EXISTS sanction_account ON account_sanctions(account_id,expires)");
            s.execute("CREATE TABLE IF NOT EXISTS account_notifications(id INTEGER PRIMARY KEY,account_id INTEGER NOT NULL REFERENCES accounts(id),message TEXT NOT NULL,code INTEGER NOT NULL,delivered INTEGER NOT NULL DEFAULT 0,created INTEGER NOT NULL)");
            s.execute("CREATE INDEX IF NOT EXISTS notification_account ON account_notifications(account_id,delivered)");
        }
        column(db,"player_reports","review_action","TEXT NOT NULL DEFAULT 'NONE'");
        column(db,"player_reports","revision","INTEGER NOT NULL DEFAULT 0");
        column(db,"player_reports","resolved_by_report","INTEGER NOT NULL DEFAULT 0");
        column(db,"report_reviews","action","TEXT NOT NULL DEFAULT 'NONE'");
        column(db,"report_reviews","duration_minutes","INTEGER NOT NULL DEFAULT 0");
        column(db,"report_reviews","public_reason","TEXT NOT NULL DEFAULT ''");
        column(db,"report_reviews","request_id","TEXT NOT NULL DEFAULT ''");
        column(db,"account_notifications","sanction_id","INTEGER NOT NULL DEFAULT 0");
        try(var s=db.createStatement()){s.execute("CREATE UNIQUE INDEX IF NOT EXISTS review_request ON report_reviews(request_id) WHERE request_id<>''");}
    }
    static void column(Connection db,String table,String name,String type)throws SQLException {
        try(var s=db.createStatement();var r=s.executeQuery("PRAGMA table_info("+table+")")){while(r.next())if(name.equals(r.getString("name")))return;}
        try(var s=db.createStatement()){s.execute("ALTER TABLE "+table+" ADD COLUMN "+name+" "+type);}
    }
    static final class Problem extends RuntimeException {final int code;Problem(int code,String message){super(message);this.code=code;}}
    record Receipt(long id,boolean duplicate){}
    static String validate(int reasons,String details) {
        if(reasons==0||(reasons&~REASONS)!=0||details.length()>500||details.getBytes(StandardCharsets.UTF_8).length>1500||details.codePoints().anyMatch(c->Character.isISOControl(c)&&c!='\n'&&c!='\t'))throw new Database.Failure(Protocol.Error.INVALID_REQUEST);
        details=details.strip();
        if((reasons&32)!=0&&details.isEmpty())throw new Database.Failure(Protocol.Error.INVALID_REQUEST);
        return details;
    }
    Receipt submit(long reporter,long match,int slot,int reasons,String details)throws Exception {
        details=validate(reasons,details);
        Database.Result record=owner.pending.get(match);
        if(record==null)record=owner.historyDetail(reporter,match);
        var actor=record.players().stream().filter(p->p.account()==reporter).findFirst().orElseThrow(()->new Database.Failure(Protocol.Error.INVALID_REQUEST));
        var target=record.players().stream().filter(p->p.slot()==slot).findFirst().orElseThrow(()->new Database.Failure(Protocol.Error.INVALID_REQUEST));
        if(Boolean.TRUE.equals(target.botOrigin())||isBot(target.account()))throw new Database.Failure(Protocol.Error.INVALID_REQUEST);
        if(target.account()==reporter)throw new Database.Failure(Protocol.Error.INVALID_REQUEST);
        var existing=rows("SELECT id FROM player_reports WHERE match_id=? AND reporter_id=? AND target_id=?",match,reporter,target.account());
        if(!existing.isEmpty())return new Receipt(((Number)existing.getFirst().get("id")).longValue(),true);
        long now=System.currentTimeMillis();
        if(((Number)rows("SELECT count(*) AS n FROM player_reports WHERE reporter_id=? AND created>=?",reporter,now-86400000L).getFirst().get("n")).intValue()>=100)throw new Database.Failure(Protocol.Error.RATE_LIMITED);
        owner.db.setAutoCommit(false);
        try {
        execute("INSERT INTO player_reports(match_id,reporter_id,target_id,reporter_name,target_name,reporter_account,target_account,map_id,room_mode,reasons,details,created,updated) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)",match,reporter,target.account(),actor.nickname(),target.nickname(),account(reporter),account(target.account()),record.mapId(),record.roomMode()==null?"NORMAL":record.roomMode(),reasons,details,now,now);
        long id=((Number)rows("SELECT last_insert_rowid() AS id").getFirst().get("id")).longValue();
        var resolved=rows("SELECT id,reviewed_by FROM player_reports WHERE match_id=? AND target_id=? AND reasons=? AND id<>? AND status='REVIEWED' AND review_action IN ('WARN','SUSPEND') ORDER BY id LIMIT 1",match,target.account(),reasons,id);
        if(!resolved.isEmpty()) {
            var source=resolved.getFirst();long sourceId=((Number)source.get("id")).longValue();String linked="Vụ việc đã được xử lý theo đơn #"+sourceId+"; không áp dụng thêm hình phạt.";
            execute("UPDATE player_reports SET status='REVIEWED',review_action='LINKED',resolved_by_report=?,review_note=?,reviewed_by=?,revision=1 WHERE id=?",sourceId,linked,source.get("reviewed_by"),id);
            execute("INSERT INTO report_reviews(report_id,admin,status,note,time,action) VALUES(?,?,?,?,?,?)",id,source.get("reviewed_by"),"REVIEWED",linked,now,"LINKED");
            notification(reporter,"Đơn tố cáo #"+id+" đã được tiếp nhận. Vụ việc này đã được xem xét và xử lý cùng các phản ánh về trận đấu.",200);
        }
        owner.db.commit();return new Receipt(id,false);
        }catch(Exception e){owner.db.rollback();throw e;}finally{owner.db.setAutoCommit(true);}
    }
    boolean isBot(long id)throws SQLException {var found=rows("SELECT is_bot FROM accounts WHERE id=?",id);return !found.isEmpty()&&((Number)found.getFirst().get("is_bot")).intValue()!=0;}
    String account(long id)throws SQLException {var result=rows("SELECT username FROM accounts WHERE id=?",id);return result.isEmpty()?"#"+id:(String)result.getFirst().get("username");}
    List<Map<String,Object>> rows(String sql,Object... values)throws SQLException {
        try(var s=owner.db.prepareStatement(sql)) {
            for(int i=0;i<values.length;i++)s.setObject(i+1,values[i]);
            try(var r=s.executeQuery()) {
                var result=new ArrayList<Map<String,Object>>();var meta=r.getMetaData();
                while(r.next()) {var row=new LinkedHashMap<String,Object>();for(int i=1;i<=meta.getColumnCount();i++){String key=meta.getColumnLabel(i);Object value=r.getObject(i);row.put(key,key.equals("match_id")?r.getString(i):value);}result.add(row);}
                return result;
            }
        }
    }
    int execute(String sql,Object... values)throws SQLException {try(var s=owner.db.prepareStatement(sql)){for(int i=0;i<values.length;i++)s.setObject(i+1,values[i]);return s.executeUpdate();}}
    Map<String,Object> list(String q,String status,int reason,long from,long to,int page)throws SQLException {
        String where=" WHERE created BETWEEN ? AND ? AND (?='' OR status=?) AND (?=0 OR (reasons & ?)<>0) AND (reporter_account LIKE ? OR target_account LIKE ? OR reporter_name LIKE ? OR target_name LIKE ? OR CAST(match_id AS TEXT) LIKE ?)";
        String like="%"+q+"%";Object[] args={from,to,status,status,reason,reason,like,like,like,like,like};
        var count=rows("SELECT count(*) AS n FROM player_reports"+where,args).getFirst().get("n");
        var values=new ArrayList<Object>(Arrays.asList(args));values.add(page*25);
        return Map.of("rows",rows("SELECT * FROM player_reports"+where+" ORDER BY created DESC,id DESC LIMIT 25 OFFSET ?",values.toArray()),"total",count,"page",page);
    }
    Map<String,Object> detail(long id)throws SQLException {
        var reports=rows("SELECT * FROM player_reports WHERE id=?",id);
        if(reports.isEmpty())throw new IllegalArgumentException("Không tìm thấy đơn tố cáo.");
        long target=((Number)reports.getFirst().get("target_id")).longValue();
        return Map.of("report",reports.getFirst(),"reviews",rows("SELECT admin,status,note,time,action,duration_minutes,public_reason FROM report_reviews WHERE report_id=? ORDER BY id DESC",id),"sanctions",rows("SELECT *,CASE WHEN action='SUSPEND' AND revoked=0 AND expires>? THEN 1 ELSE 0 END AS active FROM account_sanctions WHERE account_id=? ORDER BY id DESC LIMIT 20",System.currentTimeMillis(),target),"related",rows("SELECT id,match_id,reasons,status,created FROM player_reports WHERE target_id=? AND id<>? ORDER BY created DESC,id DESC LIMIT 10",target,id),"relatedTotal",rows("SELECT count(*) AS n FROM player_reports WHERE target_id=? AND id<>?",target,id).getFirst().get("n"),"evidence",rows("SELECT e.tick,e.slot,e.account_id,e.driver,e.kind,e.text,e.created,COALESCE(p.nickname,a.username) AS nickname FROM match_evidence e LEFT JOIN match_players p ON p.match_id=e.match_id AND p.account_id=e.account_id LEFT JOIN accounts a ON a.id=e.account_id WHERE e.match_id=? ORDER BY e.tick,e.created,e.id",reports.getFirst().get("match_id")),"botOrigin",isBot(target));
    }
    Map<String,Object> related(long id,int page)throws SQLException {
        var report=rows("SELECT target_id FROM player_reports WHERE id=?",id);
        if(report.isEmpty())throw new Problem(404,"Không tìm thấy đơn tố cáo.");
        Object target=report.getFirst().get("target_id");
        return Map.of("rows",rows("SELECT id,match_id,reasons,status,created FROM player_reports WHERE target_id=? AND id<>? ORDER BY created DESC,id DESC LIMIT 25 OFFSET ?",target,id,page*25),"total",rows("SELECT count(*) AS n FROM player_reports WHERE target_id=? AND id<>?",target,id).getFirst().get("n"),"page",page);
    }
    Map<String,Object> summary()throws SQLException {
        var counts=rows("SELECT count(*) AS total,coalesce(sum(status='OPEN'),0) AS open,coalesce(sum(status='IN_REVIEW'),0) AS inReview,coalesce(sum(status='REVIEWED'),0) AS reviewed,coalesce(sum(status='DISMISSED'),0) AS dismissed FROM player_reports").getFirst();
        ZoneId zone=ZoneId.of("Asia/Ho_Chi_Minh");LocalDate today=LocalDate.now(zone),first=today.minusDays(13);
        var grouped=rows("SELECT strftime('%Y-%m-%d',created/1000,'unixepoch','+7 hours') AS day,count(*) AS count FROM player_reports WHERE created>=? GROUP BY day",first.atStartOfDay(zone).toInstant().toEpochMilli());
        var daily=new ArrayList<Map<String,Object>>();for(int i=0;i<14;i++){String day=first.plusDays(i).toString();long count=grouped.stream().filter(r->day.equals(r.get("day"))).mapToLong(r->((Number)r.get("count")).longValue()).sum();daily.add(Map.of("day",day,"count",count));}
        return Map.of("counts",counts,"daily",daily);
    }
    void review(long id,String status,String note,String admin)throws SQLException {
        if(!Set.of("OPEN","REVIEWED","DISMISSED").contains(status)||note.length()>500)throw new IllegalArgumentException("Trạng thái hoặc ghi chú không hợp lệ.");
        owner.db.setAutoCommit(false);
        try {
            long now=System.currentTimeMillis();
            if(execute("UPDATE player_reports SET status=?,review_note=?,reviewed_by=?,updated=? WHERE id=?",status,note.strip(),admin,now,id)!=1)throw new IllegalArgumentException("Không tìm thấy đơn tố cáo.");
            execute("INSERT INTO report_reviews(report_id,admin,status,note,time) VALUES(?,?,?,?,?)",id,admin,status,note.strip(),now);
            owner.db.commit();
        }catch(Exception e){owner.db.rollback();throw e;}finally{owner.db.setAutoCommit(true);}
    }
    record Decision(long reportId,int revision,String status,String action,int minutes,String reason,String note,String requestId){}
    record ReviewResult(long target,long reporter,boolean changed,String action,long until,String message,List<Long> recipients){}
    static void decision(Decision d) {
        if(!Set.of("OPEN","IN_REVIEW","REVIEWED","DISMISSED").contains(d.status)||!Set.of("NONE","WARN","SUSPEND").contains(d.action)||d.note.length()>500||d.reason.length()>300||!d.requestId.matches("[a-zA-Z0-9-]{16,64}"))throw new Problem(400,"Quyết định xử lý không hợp lệ.");
        if(d.status.equals("REVIEWED")) {
            if(d.action.equals("NONE")||d.note.strip().length()<10||d.reason.strip().length()<5)throw new Problem(400,"Cần chọn biện pháp, nhập lý do gửi người chơi và ghi rõ căn cứ xem xét (ít nhất 10 ký tự).");
        }else if(!d.action.equals("NONE"))throw new Problem(400,"Chỉ xử phạt sau khi xác nhận vi phạm.");
        if(d.status.equals("DISMISSED")&&d.note.strip().length()<5)throw new Problem(400,"Cần ghi lý do bác bỏ đơn.");
        if(d.action.equals("SUSPEND")?!Set.of(60,1440,4320,10080).contains(d.minutes):d.minutes!=0)throw new Problem(400,"Thời hạn khóa không hợp lệ.");
    }
    ReviewResult review(Decision d,String admin)throws SQLException {
        decision(d);
        var previous=rows("SELECT * FROM player_reports WHERE id=?",d.reportId);
        if(previous.isEmpty())throw new Problem(404,"Không tìm thấy đơn tố cáo.");
        var report=previous.getFirst();long target=((Number)report.get("target_id")).longValue(),reporter=((Number)report.get("reporter_id")).longValue();
        if(isBot(target)&&d.status.equals("REVIEWED"))throw new Problem(400,"Bot gốc không áp dụng biện pháp xử phạt tài khoản.");
        var retry=rows("SELECT * FROM report_reviews WHERE request_id=?",d.requestId);
        if(!retry.isEmpty()) {
            var r=retry.getFirst();
            if(((Number)r.get("report_id")).longValue()!=d.reportId||!r.get("admin").equals(admin)||!r.get("status").equals(d.status)||!r.get("action").equals(d.action)||((Number)r.get("duration_minutes")).intValue()!=d.minutes||!r.get("public_reason").equals(d.reason.strip())||!r.get("note").equals(d.note.strip()))throw new Problem(409,"Mã yêu cầu đã dùng cho nội dung khác. Hãy tải lại đơn.");
            return new ReviewResult(target,reporter,false,d.action,0,"Quyết định này đã được lưu; không áp dụng lại hình phạt.",List.of());
        }
        if(((Number)report.get("revision")).intValue()!=d.revision)throw new Problem(409,"Đơn đã được người khác cập nhật. Hãy mở lại để xem kết quả mới.");
        if(report.get("status").equals("REVIEWED")&&!report.get("review_action").equals("NONE"))throw new Problem(409,"Đơn đã được xử lý. Không áp dụng hình phạt lần thứ hai; có thể gỡ khóa trong phần biện pháp đã áp dụng.");
        if(report.get("status").equals(d.status)&&report.get("review_note").equals(d.note.strip())&&report.get("review_action").equals(d.action))return new ReviewResult(target,reporter,false,d.action,0,"Không có thay đổi cần lưu.",List.of());
        long now=System.currentTimeMillis(),until=d.action.equals("SUSPEND")?now+d.minutes*60000L:0;
        var recipients=new ArrayList<Long>();recipients.add(reporter);
        owner.db.setAutoCommit(false);
        try {
            execute("UPDATE player_reports SET status=?,review_action=?,review_note=?,reviewed_by=?,updated=?,revision=revision+1 WHERE id=?",d.status,d.action,d.note.strip(),admin,now,d.reportId);
            execute("INSERT INTO report_reviews(report_id,admin,status,note,time,action,duration_minutes,public_reason,request_id) VALUES(?,?,?,?,?,?,?,?,?)",d.reportId,admin,d.status,d.note.strip(),now,d.action,d.minutes,d.reason.strip(),d.requestId);
            if(!d.action.equals("NONE")) {
                execute("INSERT INTO account_sanctions(account_id,report_id,action,reason,admin,created,expires) VALUES(?,?,?,?,?,?,?)",target,d.reportId,d.action,d.reason.strip(),admin,now,until);
                long sanction=((Number)rows("SELECT last_insert_rowid() AS id").getFirst().get("id")).longValue();
                notification(target,d.action.equals("WARN")?"Cảnh cáo tài khoản: "+d.reason.strip():suspensionMessage(until,d.reason.strip()),d.action.equals("WARN")?201:202,sanction);
                notification(reporter,"Đơn tố cáo #"+d.reportId+" đã được xem xét và đã áp dụng biện pháp phù hợp. Cảm ơn bạn đã giúp giữ môi trường chơi công bằng.",200);
                for(var related:rows("SELECT id,reporter_id FROM player_reports WHERE match_id=? AND target_id=? AND reasons=? AND id<>? AND status IN ('OPEN','IN_REVIEW')",report.get("match_id"),target,report.get("reasons"),d.reportId)) {
                    long other=((Number)related.get("id")).longValue(),sender=((Number)related.get("reporter_id")).longValue();String linked="Cùng người chơi và trận đấu; đã xử lý theo đơn #"+d.reportId+". Không áp dụng thêm hình phạt.";
                    execute("UPDATE player_reports SET status='REVIEWED',review_action='LINKED',resolved_by_report=?,review_note=?,reviewed_by=?,updated=?,revision=revision+1 WHERE id=?",d.reportId,linked,admin,now,other);
                    execute("INSERT INTO report_reviews(report_id,admin,status,note,time,action) VALUES(?,?,?,?,?,?)",other,admin,"REVIEWED",linked,now,"LINKED");
                    notification(sender,"Đơn tố cáo #"+other+" đã được xem xét cùng các phản ánh về trận này và đã áp dụng biện pháp phù hợp.",200);recipients.add(sender);
                }
            }else if(d.status.equals("DISMISSED"))notification(reporter,"Đơn tố cáo #"+d.reportId+" đã được xem xét. Hiện chưa đủ cơ sở để áp dụng hình phạt. Cảm ơn bạn đã phản hồi.",200);
            owner.db.commit();
        }catch(Exception e){owner.db.rollback();throw e;}finally{owner.db.setAutoCommit(true);}
        String message=d.action.equals("WARN")?"Đã lưu quyết định và gửi cảnh cáo cho tài khoản.":d.action.equals("SUSPEND")?"Đã khóa tài khoản đến "+time(until)+". Phiên đang mở sẽ bị ngắt; hết hạn tự được đăng nhập lại.":d.status.equals("DISMISSED")?"Đã bác bỏ đơn, ghi lý do và gửi phản hồi cho người tố cáo.":"Đã cập nhật tiến trình xem xét; chưa áp dụng hình phạt.";
        return new ReviewResult(target,reporter,true,d.action,until,message,List.copyOf(recipients));
    }
    void notification(long account,String message,int code)throws SQLException {notification(account,message,code,0);}
    void notification(long account,String message,int code,long sanction)throws SQLException {execute("INSERT INTO account_notifications(account_id,message,code,created,sanction_id) VALUES(?,?,?,?,?)",account,message,code,System.currentTimeMillis(),sanction);}
    static String time(long millis){return Instant.ofEpochMilli(millis).atZone(ZoneId.of("Asia/Ho_Chi_Minh")).format(java.time.format.DateTimeFormatter.ofPattern("HH:mm dd/MM/yyyy"))+" (giờ Việt Nam)";}
    static String suspensionMessage(long until,String reason){return "Tài khoản đang bị khóa đến "+time(until)+". Lý do: "+reason;}
    String suspension(long account)throws SQLException {
        var active=rows("SELECT expires,reason FROM account_sanctions WHERE account_id=? AND action='SUSPEND' AND revoked=0 AND expires>? ORDER BY expires DESC LIMIT 1",account,System.currentTimeMillis());
        return active.isEmpty()?null:suspensionMessage(((Number)active.getFirst().get("expires")).longValue(),(String)active.getFirst().get("reason"));
    }
    long revoke(long id,String admin,String note)throws SQLException {
        if(note.strip().length()<5||note.length()>500)throw new Problem(400,"Cần ghi lý do gỡ khóa.");
        var result=rows("SELECT * FROM account_sanctions WHERE id=? AND action='SUSPEND'",id);if(result.isEmpty())throw new Problem(404,"Không tìm thấy biện pháp khóa.");
        var sanction=result.getFirst();long account=((Number)sanction.get("account_id")).longValue();
        if(((Number)sanction.get("revoked")).intValue()!=0)return account;
        owner.db.setAutoCommit(false);
        try {
            long now=System.currentTimeMillis();execute("UPDATE account_sanctions SET revoked=1,revoked_by=?,revoke_reason=?,revoked_at=? WHERE id=?",admin,note.strip(),now,id);
            execute("INSERT INTO report_reviews(report_id,admin,status,note,time,action) VALUES(?,?,?,?,?,?)",sanction.get("report_id"),admin,"REVIEWED",note.strip(),now,"REVOKE");
            notification(account,"Biện pháp khóa tài khoản từ đơn #"+sanction.get("report_id")+" đã được gỡ sau khi xem xét lại. Lý do: "+note.strip(),200);owner.db.commit();
        }catch(Exception e){owner.db.rollback();throw e;}finally{owner.db.setAutoCommit(true);}
        return account;
    }
}
