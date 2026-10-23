package tank;

import java.security.SecureRandom;
import static tank.Protocol.*;

/** Additive TCP adapter: 90 request, 91 response; versioned campaign-only payload. */
final class CampaignSession {
    static final int REQUEST=90, RESPONSE=91;
    final Server.Session session;
    final Database db;
    CampaignRun run;
    boolean saving;
    boolean detached;
    boolean visualEvents;
    boolean tactics;
    boolean shop;
    boolean progression;
    boolean environment;
    String saveStatus="";
    long totalGold;
    CampaignSession(Server.Session session,Database db){this.session=session;this.db=db;}
    synchronized void request(Packet p) {
        try {
            if(session.account==null)throw new IllegalArgumentException("Hãy đăng nhập để chơi chiến dịch.");
            if(session.room!=null)throw new IllegalArgumentException("Hãy rời phòng trước khi vào chiến dịch.");
            Reader r=new Reader(p.payload());
            int version=r.u8();
            if(version<1||version>6)throw new IllegalArgumentException("Phiên bản chiến dịch không tương thích.");
            visualEvents=version>=2;tactics=version>=3;shop=version>=4;progression=version>=5;environment=version>=6;
            int command=r.u8();
            if(command==0) {
                int nation=r.u8();r.end();
                if(saving||run!=null&&!run.finished)throw new IllegalArgumentException("Chiến dịch đang diễn ra.");
                if(run!=null&&!saveStatus.equals("Đã lưu kết quả."))throw new IllegalArgumentException("Hãy lưu lại kết quả trước khi chơi tiếp.");
                run=new CampaignRun(nation,new SecureRandom().nextLong());saveStatus="";state(p.sequence());
            }else if(command==1) {
                int turn=r.u32(),action=r.u8();r.end();
                if(run==null)throw new IllegalArgumentException("Chưa bắt đầu chiến dịch.");
                if(turn==run.turn)run.act(action);
                if(run.finished)save();state(p.sequence());
            }else if(command==5) {
                if(!shop)throw new IllegalArgumentException("Shop requires campaign version 4.");
                String id=r.str(36);int turn=r.u32(),revision=r.u32(),item=r.u8();r.end();
                if(run==null)throw new IllegalArgumentException("No active campaign.");
                if(run.id.equals(id)&&run.turn==turn&&run.shopRevision==revision)run.buy(item);
                state(p.sequence());
            }else if(command==2) {
                r.end();if(run!=null&&!run.finished){run.finished=true;run.message="Đã kết thúc chuyến đi.";}if(run!=null)save();state(p.sequence());
            }else if(command==3) {
                r.end();long account=session.account.id();
                db.submit(()->new Object[]{CampaignStore.ranking(db.db),CampaignStore.gold(db.db,account)}).whenComplete((result,error)->{
                    if(detached||session.account==null||session.account.id()!=account)return;
                    if(error!=null){failure(p.sequence(),"Không tải được bảng xếp hạng. Hãy thử lại.");return;}
                    @SuppressWarnings("unchecked") var rows=(java.util.List<CampaignStore.Rank>)result[0];
                    Writer w=new Writer().u16(0).u8(1).u8(1).u64((long)result[1]).u8(rows.size());for(var row:rows)w.str(row.name()).u32(row.score());session.send(RESPONSE,p.sequence(),w.done());
                });
            }else if(command==4){r.end();if(run!=null&&run.finished)save();state(p.sequence());}
            else throw new IllegalArgumentException("Thao tác chiến dịch không hợp lệ.");
        }catch(RuntimeException e){failure(p.sequence(),e instanceof IllegalArgumentException?e.getMessage():"Không xử lý được chiến dịch.");}
    }
    void failure(int sequence,String message){session.send(RESPONSE,sequence,new Writer().u16(1).str(message).done());}
    synchronized boolean active(){return saving||run!=null&&(!run.finished||!saveStatus.equals("Đã lưu kết quả."));}
    synchronized void save() {
        if(run==null||saving||saveStatus.equals("Đã lưu kết quả.")||session.account==null)return;
        saving=true;saveStatus="Đang lưu kết quả…";
        CampaignRun ended=run;long account=session.account.id();String name=session.displayName();
        db.campaigns.save(account,name,ended).whenComplete((gold,error)->{
            synchronized(this){saving=false;saveStatus=error==null?"Đã lưu kết quả.":"Chưa lưu được. Bấm Lưu lại để thử lại.";if(error==null)totalGold=gold;state(0);}
        });
    }
    synchronized void close(){detached=true;if(run!=null){run.finished=true;save();}}
    synchronized void state(int sequence) {
        if(detached)return;
        Writer w=new Writer().u16(0).u8(1).u8(0).u8(run==null?0:1);
        if(run!=null){
            w.str(run.id).u8(run.nation).u32(run.turn).u8(run.x).u32(run.y).u32(run.distance).u16(run.hp).u8(run.facing).u8(run.shot).u8(run.skill).u32(run.gold).u32(run.kills).u8(run.doubleHits).u8(run.shields).u8(run.bosses).u8(run.finished?1:0).u8(run.victory?1:0).str(run.message).str(saveStatus).u64(totalGold);
            int bottom=Math.max(0,run.y-7);w.u32(bottom);
            for(int row=bottom;row<bottom+CampaignRun.VIEW;row++)for(int x=0;x<CampaignRun.WIDTH;x++)w.u8(run.terrain(x,row));
            var enemies=run.enemies.stream().filter(e->e.y>=bottom&&e.y<bottom+CampaignRun.VIEW).toList();
            w.u16(enemies.size());for(var e:enemies)w.u32(e.id).u8(e.x).u32(e.y).u16(e.hp).u16(e.maxHp).u8(e.boss);
            var loot=run.loot.stream().filter(l->l.y()>=bottom&&l.y()<bottom+CampaignRun.VIEW).toList();
            w.u16(loot.size());for(var l:loot)w.u8(l.x()).u32(l.y()).u8(l.kind());
        }
        if(run!=null&&visualEvents){
            w.u8(1).u16(run.visuals.size());
            for(var v:run.visuals)w.u8(v.kind()).u32(v.source()).u32(v.target()).u8(v.x()).u32(v.y()).u8(v.tx()).u32(v.ty()).u16(v.amount());
        }
        if(run!=null&&tactics){
            var visible=run.enemies.stream().filter(e->e.y>=Math.max(0,run.y-7)&&e.y<Math.max(0,run.y-7)+CampaignRun.VIEW).toList();
            w.u8(1).u16(visible.size());
            for(var e:visible)w.u32(e.id).u8(e.role.ordinal()).u32(e.squad).u8(CampaignTactics.covered(run,e)?e.guard:0);
        }
        if(run!=null&&shop)w.u8(1).u8(CampaignRun.SHOT_RANGE).u8(CampaignRun.MAX_BUFF).u32(run.shopRevision).u32(run.price(0)).u32(run.price(1)).u32(run.price(2));
        if(run!=null&&progression){
            var stage=CampaignDifficulty.at(run.distance);w.u8(1).u8(stage.index()).str(stage.name());
            int bottom=Math.max(0,run.y-7);
            var bosses=run.enemies.stream().filter(e->e.boss>0&&e.y>=bottom&&e.y<bottom+CampaignRun.VIEW).toList();
            w.u8(bosses.size());for(var boss:bosses)w.u32(boss.id).u8(boss.phase);
            var danger=run.dangers.stream().filter(d->d.y()>=bottom&&d.y()<bottom+CampaignRun.VIEW).toList();
            w.u16(danger.size());for(var d:danger)w.u32(d.source()).u8(d.x()).u32(d.y()).u32(d.due()).u16(d.damage());
        }
        if(run!=null&&environment){
            int bottom=Math.max(0,run.y-7);w.u8(1).u8(run.campaignMap.template(run.y)).u8(CampaignMap.style(run.seed,run.y/CampaignMap.SECTION)).str(run.campaignMap.name(run.y));
            for(int row=bottom;row<bottom+CampaignRun.VIEW;row++)for(int x=0;x<CampaignRun.WIDTH;x++)w.u8(run.campaignMap.ground(x,row));
            for(int row=bottom;row<bottom+CampaignRun.VIEW;row++)for(int x=0;x<CampaignRun.WIDTH;x++)w.u8(run.campaignMap.decoration(x,row));
        }
        session.send(RESPONSE,sequence,w.done());
    }
}
