package tank;

import java.util.*;
import java.util.concurrent.ConcurrentLinkedQueue;

/** Bounded event batches on the DB worker; report-linked matches are retained. */
final class MatchEvidence {
    record Event(String id,long match,int tick,int slot,long account,String driver,String kind,String text,long time){}
    final Database db;
    final Queue<Event> pending=new ConcurrentLinkedQueue<>();
    boolean writing,closing;
    long cleanupAt=System.currentTimeMillis()+3_600_000;
    MatchEvidence(Database db)throws Exception {
        this.db=db;
        db.reports.execute("CREATE TABLE IF NOT EXISTS match_evidence(id TEXT PRIMARY KEY,match_id INTEGER NOT NULL,tick INTEGER NOT NULL,slot INTEGER NOT NULL,account_id INTEGER NOT NULL,driver TEXT NOT NULL,kind TEXT NOT NULL,text TEXT NOT NULL,created INTEGER NOT NULL)");
        db.reports.execute("CREATE INDEX IF NOT EXISTS evidence_match ON match_evidence(match_id,tick,created)");
        db.reports.execute("CREATE INDEX IF NOT EXISTS evidence_created ON match_evidence(created)");
        cleanup();
    }
    void add(long match,int tick,int slot,long account,String driver,String kind,String text){
        pending.add(new Event(UUID.randomUUID().toString(),match,tick,slot,account,driver,kind,text,System.currentTimeMillis()));flush();
    }
    void cleanup()throws Exception {
        db.reports.execute("DELETE FROM match_evidence WHERE created<? AND NOT EXISTS(SELECT 1 FROM player_reports p WHERE p.match_id=match_evidence.match_id)",System.currentTimeMillis()-30L*86400000);
    }
    synchronized void flush() {
        if(closing||writing||pending.isEmpty())return;
        var batch=new ArrayList<Event>();Event e;while(batch.size()<256&&(e=pending.poll())!=null)batch.add(e);
        writing=true;
        db.submit(()->{
            db.db.setAutoCommit(false);
            try {
                for(var row:batch)db.reports.execute("INSERT OR IGNORE INTO match_evidence VALUES(?,?,?,?,?,?,?,?,?)",row.id,row.match,row.tick,row.slot,row.account,row.driver,row.kind,row.text,row.time);
                db.db.commit();return true;
            }catch(Exception failure){db.db.rollback();throw failure;}finally{db.db.setAutoCommit(true);}
        }).whenComplete((ok,error)->{synchronized(this){if(error!=null)pending.addAll(batch);writing=false;if(error==null&&!pending.isEmpty())flush();}});
    }
    void tick(){
        flush();long now=System.currentTimeMillis();
        if(now>=cleanupAt){cleanupAt=now+3_600_000;db.submit(()->{cleanup();return null;});}
    }
    void close() {
        synchronized(this){closing=true;}
        try {
            db.submit(()->{
                var all=new ArrayList<Event>();Event event;while((event=pending.poll())!=null)all.add(event);
                db.db.setAutoCommit(false);
                try {
                    for(var row:all)db.reports.execute("INSERT OR IGNORE INTO match_evidence VALUES(?,?,?,?,?,?,?,?,?)",row.id,row.match,row.tick,row.slot,row.account,row.driver,row.kind,row.text,row.time);
                    db.db.commit();
                }catch(Exception e){db.db.rollback();pending.addAll(all);throw e;}finally{db.db.setAutoCommit(true);}
                return null;
            }).get(10,java.util.concurrent.TimeUnit.SECONDS);
        }catch(Exception e){System.err.println("Evidence pending at shutdown: "+pending.size());}
    }

}
