package tank;
import java.security.*;
import java.util.*;
import java.util.function.Supplier;
import static tank.Protocol.*;
import tank.Protocol.Error;

/** Room-thread-owned reliable nation loadout and per-match request state. */
final class RoleplayMatch {
    static final byte[] CONTENT_HASH=HexFormat.of().parseHex(RoleplayContent.Hash);
    static final class Reply {
        final byte[] request;
        final int action;
        final LinkedHashSet<Integer> sequences=new LinkedHashSet<>();
        byte[] result;
        Reply(int action,byte[] request,int seq) {this.action=action;this.request=request.clone();sequences.add(seq);}
    }
    static final class Player {
        int nation=-1,highestRequest;
        int shopWave,shopMask;
        int inventoryRevision;
        final int[] inventory=new int[GameRules.ItemTypeCount];
        WalletStore.Purchase shopPending;
        final LinkedHashMap<Integer,Reply> replies=new LinkedHashMap<>();
        boolean loadoutConfirmed,awaitingLoadout,usMetricsOpen;
        long rateRejected,lastRateNotice,qUses,qAvailableTicks,aliveTicks;
        void resetMatch() {
            highestRequest=0;replies.clear();
            shopWave=shopMask=0;shopPending=null;
            inventoryRevision=0;Arrays.fill(inventory,0);
            loadoutConfirmed=awaitingLoadout=usMetricsOpen=false;
            rateRejected=lastRateNotice=qUses=qAvailableTicks=aliveTicks=0;
        }
    }
    final Room room;
    final RoleplayShop shop;
    PrepStage stage=PrepStage.LOADING;
    int revision;
    byte[] loadout,loadoutHash;
    RoleplayMatch(Room room) {this.room=room;shop=new RoleplayShop(room);}
    void selectNation(Server.Session session,int nation,int seq) {
        Room.Member m=room.member(session);
        if(m==null||room.phase!=RoomState.WAITING||m.ready) {session.error(ACTION_RESULT,seq,Error.INVALID_STATE);return;}
        if(nation<0||nation>7) {session.error(ACTION_RESULT,seq,Error.INVALID_REQUEST);return;}
        if(room.members.values().stream().anyMatch(other->other!=m&&other.roleplay.nation==nation)) {session.error(ACTION_RESULT,seq,Error.NATION_TAKEN);return;}
        m.roleplay.nation=nation;
        session.ok(ACTION_RESULT,seq);room.broadcast();
    }
    void begin() {
        stage=PrepStage.LOADING;revision=0;loadout=loadoutHash=null;
        for(Room.Member m:room.members.values()) {
            m.roleplay.resetMatch();
            room.world.tanks.get(m.slot).nation=m.roleplay.nation;
        }
    }
    void resetLobby() {
        stage=PrepStage.LOADING;revision=0;loadout=loadoutHash=null;
        for(Room.Member m:room.members.values())m.roleplay.resetMatch();
    }
    void cancel(String reason) {
        System.out.println("ROLEPLAY_CANCEL match="+room.match+" room="+room.id+" reason="+reason);
        room.phase=RoomState.WAITING;room.announced=false;room.startUs=room.deadline=0;
        for(Room.Member m:room.members.values()) {
            m.ready=m.prepared=false;m.session.endpoint=null;
            m.session.notice(Error.NOT_READY.ordinal(),reason);
        }
        room.participants.clear();room.world=null;resetLobby();room.broadcast();
    }
    void tick(long now) {
        if(stage==PrepStage.LOADING) {
            if(room.members.values().stream().allMatch(m->m.prepared&&m.session.endpoint!=null)) {
                publishLoadout(now);
            }
            else if(now>=room.deadline)cancel("Hủy chuẩn bị: tải trận quá hạn");
        }
        else if(stage==PrepStage.SYNC_LOADOUT) {
            if(room.members.values().stream().allMatch(m->m.roleplay.loadoutConfirmed)) {
                stage=PrepStage.COUNTDOWN;room.announced=true;room.startUs=room.deadline=now+3_000_000;
                for(Room.Member m:room.members.values())m.session.event(START,new Writer().u64(room.match).u64(room.startUs).done());
                room.broadcast();
            }
            else if(now>=room.deadline)cancel("Hủy chuẩn bị: xác nhận bộ kỹ năng quá hạn");
        }
    }
    void publishLoadout(long now) {
        stage=PrepStage.SYNC_LOADOUT;revision=1;room.deadline=now+5_000_000;
        Writer w=new Writer().u64(room.match).u32(revision).bytes(CONTENT_HASH).u8(room.participants.size());
        room.participants.stream().sorted(Comparator.comparingInt(m->m.slot)).forEach(m-> {
            Player p=m.roleplay;w.u8(m.slot).u8(p.nation);
            World.Tank tank=room.world.tanks.get(m.slot);tank.nation=p.nation;
            p.loadoutConfirmed=false;
        });
        byte[] canonical=w.done();
        try {loadoutHash=MessageDigest.getInstance("SHA-256").digest(canonical);}
        catch(NoSuchAlgorithmException e) {throw new IllegalStateException(e);}
        loadout=new Writer().bytes(canonical).bytes(loadoutHash).done();
        for(Room.Member m:room.members.values())m.session.event(MATCH_LOADOUT,loadout);
        room.broadcast();
    }
    void prepared(Server.Session session,long match,int incomingStage,int incomingRevision,byte[] hash,int seq) {
        Room.Member m=room.member(session);
        if(m==null||match!=room.match||session.endpoint==null||!(room.phase==RoomState.PREPARING||room.phase==RoomState.RUNNING&&m.rejoining)) {session.error(ACTION_RESULT,seq,Error.INVALID_STATE);return;}
        if(!m.rejoining&&(stage==PrepStage.LOADING||stage==PrepStage.SYNC_LOADOUT)&&Server.nowUs()>=room.deadline) {
            cancel("Hủy chuẩn bị: xác nhận đến sau thời hạn");session.error(ACTION_RESULT,seq,Error.INVALID_STATE);return;
        }
        if(incomingStage==PrepStage.LOADING.ordinal()&&(m.rejoining||stage==PrepStage.LOADING)) {
            if(incomingRevision!=RoleplayContent.Version||!Arrays.equals(hash,CONTENT_HASH)) {session.error(ACTION_RESULT,seq,Error.CONTENT_MISMATCH);return;}
            m.prepared=true;
            if(m.rejoining) {
                m.roleplay.awaitingLoadout=true;
                session.event(MATCH_LOADOUT,loadout);
            }
            session.ok(ACTION_RESULT,seq);return;
        }
        if(incomingStage==PrepStage.SYNC_LOADOUT.ordinal()&&(m.rejoining&&m.roleplay.awaitingLoadout||!m.rejoining&&stage==PrepStage.SYNC_LOADOUT)) {
            if(incomingRevision!=revision||!Arrays.equals(hash,loadoutHash)) {session.error(ACTION_RESULT,seq,Error.CONTENT_MISMATCH);return;}
            m.roleplay.loadoutConfirmed=true;m.roleplay.awaitingLoadout=false;
            if(m.rejoining)room.prepared(session,match,seq);
            else session.ok(ACTION_RESULT,seq);
            return;
        }
        session.error(ACTION_RESULT,seq,Error.INVALID_STATE);
    }
    void use(Server.Session session,long match,int requestId,int slot,byte[] request,int seq) {use(session,match,requestId,slot,request,seq,0,0);}
    void use(Server.Session session,long match,int requestId,int slot,byte[] request,int seq,float aimX,float aimY) {
        dispatch(session,match,requestId,USE_SKILL,slot,request,seq,true,()-> {
            Room.Member m=room.member(session);
            if(room.phase!=RoomState.RUNNING||m.rejoining)return Error.INVALID_STATE;
            World.Tank before=room.world.tanks.get(m.slot);int energy=before.energy,recovery=before.energyRecovery,bullets=room.world.bullets.size();float x=before.x,y=before.y;
            int aimError=Math.abs(Math.floorMod(before.desiredAim-before.turret+18000,36000)-18000);
            room.world.skillAim(m.slot,aimX,aimY);
            Error result=room.world.useSkill(m.slot,slot);
            World.Tank tank=room.world.tanks.get(m.slot);
            if(result==Error.OK) {
                m.roleplay.qUses++;
            }
            System.out.println("ROLEPLAY_USE match="+room.match+" account="+m.account.id()+" nation="+m.roleplay.nation+" hp="+tank.hp+" map="+room.arenaMap.mapId+" slot="+slot+" skill="+m.roleplay.nation+" result="+result+" tick="+room.world.tick+" energyBefore="+energy+" energyReceived="+(tank.energy-energy)+" recoveryBefore="+recovery+" shiftHeld="+((before.desiredButtons&32)!=0)+" moving="+((before.desiredButtons&15)!=0)+" aimErrorBefore="+aimError+" inWater="+room.arenaMap.water(x,y)+" distance="+Math.hypot(tank.x-x,tank.y-y)+" bulletsRemoved="+(bullets-room.world.bullets.size()));
            return result;
        });
    }
    void dispatch(Server.Session session,long match,int requestId,int action,int slot,byte[] request,int seq,boolean deferred,Supplier<Error> apply) {
        Room.Member m=room.member(session);
        if(m==null||match!=room.match) {sendResult(session,seq,Error.INVALID_STATE,match,requestId,action,slot);return;}
        Player p=m.roleplay;Reply previous=p.replies.get(requestId);
        if(previous!=null) {
            if(previous.action!=action||!Arrays.equals(previous.request,request)) {sendResult(session,seq,Error.INVALID_REQUEST,match,requestId,action,slot);return;}
            if(previous.result!=null)session.send(ACTION_RESULT,seq,previous.result);
            else if(previous.sequences.size()<16)previous.sequences.add(seq);
            return;
        }
        if(requestId==0||Integer.compareUnsigned(requestId,p.highestRequest)<=0) {sendResult(session,seq,Error.STALE_REQUEST,match,requestId,action,slot);return;}
        p.highestRequest=requestId;
        Reply reply=new Reply(action,request,seq);p.replies.put(requestId,reply);
        while(p.replies.size()>256)p.replies.remove(p.replies.keySet().iterator().next());
        boolean admitted=session.actions.allow();
        Runnable run=()-> {
            Error error=!admitted?Error.RATE_LIMITED:room.member(session)!=m?Error.INVALID_STATE:apply.get();
            reply.result=result(error,match,requestId,action,slot,m);
            if(action==USE_SKILL&&error!=Error.RATE_LIMITED)System.out.println("ROLEPLAY_ACTION match="+match+" account="+m.account.id()+" request="+Integer.toUnsignedString(requestId)+" slot="+slot+" result="+error+" tick="+(room.world==null?0:room.world.tick));
            if(error==Error.RATE_LIMITED) {
                p.rateRejected++;long now=System.nanoTime();
                if(now-p.lastRateNotice>=1_000_000_000L) {p.lastRateNotice=now;System.out.println("ROLEPLAY_RATE match="+match+" account="+m.account.id()+" rejectedTotal="+p.rateRejected);}
            }
            for(int responseSeq:reply.sequences)session.send(ACTION_RESULT,responseSeq,reply.result);
            reply.sequences.clear();
        };
        if(admitted&&deferred&&room.world!=null&&(room.phase==RoomState.RUNNING||room.phase==RoomState.PREPARING&&room.announced&&Server.nowUs()>=room.startUs))room.world.skillIntents.add(run);
        else run.run();
    }
    byte[] result(Error error,long match,int requestId,int action,int slot,Room.Member member) {
        Writer w=new Writer().u16(error.ordinal());if(error!=Error.OK)w.str(error.name());
        World.Tank tank=room.world==null||member==null?null:room.world.tanks.get(member.slot);
        int ready=tank==null||action!=USE_SKILL?0:slot==0?tank.qReadyTick:0;
        return w.u64(match).u32(requestId).u8(action).u8(slot).u32(room.world==null?0:room.world.tick).u16(ready).done();
    }
    void sendResult(Server.Session session,int seq,Error error,long match,int requestId,int action,int slot) {
        session.send(ACTION_RESULT,seq,result(error,match,requestId,action,slot,room.member(session)));
    }
    void observe() {
        for(Room.Member m:room.members.values()) {
            World.Tank tank=room.world.tanks.get(m.slot);if(tank==null)continue;
            Player p=m.roleplay;
            if(tank.hp>0) {p.aliveTicks++;if(room.world.tick>=tank.qReadyTick)p.qAvailableTicks++;}
            if(p.usMetricsOpen&&room.world.tick>=tank.usQTick+150) {
                System.out.println("ROLEPLAY_UTILITY match="+room.match+" account="+m.account.id()+" nation="+p.nation+" hp="+tank.maxHp+" map="+room.arenaMap.mapId+" skill=Q_US sprintTicks="+tank.usSprintTicks+" survived5s="+(tank.deaths==tank.usDeathsAtQ));p.usMetricsOpen=false;
            }
        }
    }
    void summary() {
        for(Room.Member m:room.participants) {
            Player p=m.roleplay;
            System.out.println("ROLEPLAY_SUMMARY match="+room.match+" account="+m.account.id()+" nation="+p.nation+" hp="+room.startingHp+" map="+room.arenaMap.mapId+" qUses="+p.qUses+" qAvailableTicks="+p.qAvailableTicks+" aliveTicks="+p.aliveTicks+" rateRejected="+p.rateRejected);
        }
    }
}
