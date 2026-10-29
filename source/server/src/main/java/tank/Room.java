package tank;
import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.locks.LockSupport;
import static tank.Protocol.*;
import tank.Protocol.Error;
public class Room  {
    static final long AFK_IDLE_NANOS=15_000_000_000L,AFK_GRACE_NANOS=15_000_000_000L;
    static final long UDP_TIMEOUT_NANOS=10_000_000_000L;
    final Server server;
    final int id;
    final RoomMode roomMode;
    final RoleplayMatch roleplay;
    volatile String name;
    volatile int capacity,matchTicks,startingHp=World.DEFAULT_HP;
    final RoomAccess access=new RoomAccess();
    GameMap arenaMap;
    final LinkedHashMap<Integer,Member> members=new LinkedHashMap<>();
    final List<Member> participants=new ArrayList<>();
    final ArrayBlockingQueue<Runnable> commands=new ArrayBlockingQueue<>(256);
    volatile RoomState phase=RoomState.WAITING;
    volatile long match;
    volatile View view=new View(RoomState.WAITING,0);
    World world;
    int host=-1,snapshotId;
    long deadline,startUs,startedUtc,seed;
    String previousSpawnLayout="";
    boolean announced;
    volatile boolean alive=true,stopRequested;
    Thread thread;
    long fullCount,deltaCount,fallbackCount,resyncCount;
    volatile Set<Long> rejoinableAccounts=Set.of();
    record View(RoomState state,int count) {
    }
    static class Member  {
        Server.Session session;
        final Database.Account account;
        final int slot;
        String displayName;
        boolean ready,prepared,disconnected,afkWarn,rejoining;
        int leaveReason;
        long rejoinDeadline;
        long activity=System.nanoTime(),activityCheck=activity,afkSince;
        int previousAim,skillAimTick;
        World.Tank resultTank;
        InputBuffer input=new InputBuffer();
        int ack;
        boolean forceFull=true;
        final LinkedHashMap<Integer,byte[]> cache=new LinkedHashMap<>();
        final RoleplayMatch.Player roleplay=new RoleplayMatch.Player();
        Member(Server.Session s,int slot) {
            session=s;
            account=s.account;
            displayName=s.displayName();
            this.slot=slot;
        }
        boolean canRejoin() {
            // Leaving to recover a weak connection must not forfeit the running match.
            return disconnected&&leaveReason>=0&&leaveReason<=3;
        }
    }
    Room(Server s,int id,String name) {
        this(s,id,name,s.cfg.integer("max.players",8),s.cfg.integer("match.ticks",5400));
    }
    Room(Server s,int id,String name,int capacity,int ticks) {
        this(s,id,name,capacity,ticks,RoomMode.NORMAL);
    }
    Room(Server s,int id,String name,int capacity,int ticks,RoomMode mode) {
        server=s;
        this.id=id;
        this.name=name;
        this.capacity=capacity;
        roomMode=mode;
        roleplay=mode==RoomMode.ROLEPLAY?new RoleplayMatch(this):null;
        matchTicks=ticks;
        arenaMap=s.largeMap;
    }
    void start() {
        thread=Thread.ofVirtual().name("room-"+id).start(this::loop);
    }
    void enqueue(Runnable action) {
        if(!commands.offer(action))throw new Database.Failure(Error.RATE_LIMITED);
    }
    void loop() {
        long next=System.nanoTime();
        while(alive) {
            long now=System.nanoTime();
            int steps=0;
            while(now>=next&&steps<3) {
                long before=System.nanoTime(),lag=Math.max(0,before-next);
                boolean wasRunning=phase==RoomState.RUNNING;
                tick();
                next+=33_333_333L;
                steps++;
                now=System.nanoTime();
                if(world!=null&&(wasRunning||phase==RoomState.RUNNING))server.metrics.row(this,before,lastEncode,lastSend,lag);
                now=System.nanoTime();
            }
            if(now>=next)next=now+33_333_333L;
            LockSupport.parkNanos(Math.max(1,next-System.nanoTime()));
        }
    }
    long lastEncode,lastSend;
    void tick() {
        if(roleplay!=null)roleplay.shop.tick();
        if(stopRequested) {
            if(phase==RoomState.RUNNING)finish(2);
            alive=false;
            return;
        }
        Runnable c;
        int drained=0;
        while(drained++<256&&(c=commands.poll())!=null)try {
            c.run();
        }
        catch(Database.Failure e) {
            System.err.println("room command state error");
        }
        catch(RuntimeException e) {
            System.err.println("room command: "+e.getMessage());
        }
        for(Member removed:new ArrayList<>(members.values()))if(!removed.session.open)leave(removed.session,1);
        long now=Server.nowUs();
        if(phase==RoomState.PREPARING) {
            if(roleplay!=null)roleplay.tick(now);
            if(roleplay==null&&!announced&&members.values().stream().allMatch(m->m.prepared&&m.session.endpoint!=null)) {
                announced=true;
                startUs=now+3_000_000;
                for(Member m:members.values())m.session.event(START,new Writer().u64(match).u64(startUs).done());
            }
            if(phase==RoomState.PREPARING&&announced&&now>=startUs) {
                phase=RoomState.RUNNING;
                startedUtc=System.currentTimeMillis();
                for(var m:members.values()) {
                    m.activity=m.activityCheck=System.nanoTime();
                    m.session.lastUdp=System.nanoTime();
                }
                broadcast();
            }
            else if(roleplay==null&&!announced&&now>=deadline) {
                phase=RoomState.WAITING;
                for(Member m:members.values())m.session.notice(Error.NOT_READY.ordinal(),"Chuẩn bị trận quá hạn");
                broadcast();
            }
        }
        if(phase==RoomState.RUNNING) {
            for(Member m:new ArrayList<>(members.values())) {
                if(phase!=RoomState.RUNNING)break;
                if(System.nanoTime()-m.session.lastUdp>UDP_TIMEOUT_NANOS)leave(m.session,2);
            }
            if(phase!=RoomState.RUNNING)return;
            Map<Integer,World.Input> inputs=new HashMap<>();
            for(Member m:new ArrayList<>(members.values())) {
                // Removing a member can finish the match and release world mid-loop.
                if(phase!=RoomState.RUNNING)return;
                if(m.rejoining) {
                    if(System.nanoTime()>=m.rejoinDeadline)leave(m.session,3);
                    continue;
                }
                World.Tank t=world.tanks.get(m.slot);
                World.Input in=m.input.resolve(world.tick+1,t.turret);
                inputs.put(m.slot,in);
                long activityNow=System.nanoTime();
                m.activityCheck=activityNow;
                if(t.hp>0&&t.respawn>0&&world.tick-t.respawn<=30)m.previousAim=in.angle();
                if((in.buttons()&31)!=0||Math.abs(Math.floorMod(in.angle()-m.previousAim+18000,36000)-18000)>200) {
                    m.activity=System.nanoTime();
                    if(m.afkWarn)m.session.notice(101,"Đã tiếp tục hoạt động");
                    m.afkWarn=false;
                    m.afkSince=0;
                    m.previousAim=in.angle();
                }
                long idle=System.nanoTime()-m.activity;
                if(t.hp>0&&idle>=AFK_IDLE_NANOS&&!m.afkWarn) {
                    m.afkWarn=true;
                    m.afkSince=activityNow;
                    m.session.notice(100,"AFK: hãy di chuyển, ngắm hoặc bắn trong 15 giây");
                }
                if(m.afkWarn&&activityNow-m.afkSince>=AFK_GRACE_NANOS)leave(m.session,3);
            }
            if(phase!=RoomState.RUNNING)return;
            world.step(inputs);
            if(roleplay!=null)roleplay.observe();
            snapshot();
            if(world.tick>=matchTicks)finish(0);
        }
    }
    void join(Server.Session s,int seq) {
        join(s,seq,"");
    }
    void join(Server.Session s,int seq,String password) {
        synchronized(s) {
            if(!alive) {
                s.room=null;
                s.error(ACTION_RESULT,seq,Error.ROOM_MISSING);
                return;
            }
            if(!s.open||s.account==null) {
                if(s.room==this)s.room=null;
                if(members.isEmpty()) {
                    alive=false;
                    server.rooms.remove(id,this);
                }
                return;
            }
            if(phase==RoomState.RUNNING) {
                rejoin(s,seq);
                return;
            }
            if(phase!=RoomState.WAITING||members.size()>=capacity) {
                s.room=null;
                s.error(ACTION_RESULT,seq,phase!=RoomState.WAITING?Error.ROOM_BUSY:Error.ROOM_FULL);
                return;
            }
            if(!access.matches(password)) {
                s.room=null;
                s.error(ACTION_RESULT,seq,password.isEmpty()?Error.ROOM_PASSWORD_REQUIRED:Error.ROOM_PASSWORD_INCORRECT);
                return;
            }
            int slot=0;
            while(members.containsKey(slot))slot++;
            Member m=new Member(s,slot);
            members.put(slot,m);
            s.slot=slot;
            s.room=this;
            if(host<0)host=slot;
            s.ok(ACTION_RESULT,seq);
            broadcast();
        }
    }
    boolean canRejoin(Server.Session s) {
        return s.account!=null&&view.state()==RoomState.RUNNING&&view.count()<capacity&&rejoinableAccounts.contains(s.account.id());
    }
    void updateRejoinable() {
        Set<Long> accounts=new HashSet<>();
        if(phase==RoomState.RUNNING)for(Member m:participants)if(m.canRejoin())accounts.add(m.account.id());
        rejoinableAccounts=Set.copyOf(accounts);
    }
    void sendPrepare(Member m,boolean rejoin) {
        Writer w=new Writer().u64(match).u32(id).u8(m.slot).u16(arenaMap.mapId).bytes(arenaMap.hash).u8(server.cfg.mode().ordinal()).u8(30).u16(server.udp.getLocalPort());
        if(roleplay!=null)w.u8(rejoin?1:0).u8(roomMode.ordinal()).u16(RoleplayContent.Version).bytes(RoleplayMatch.CONTENT_HASH).u32(m.roleplay.highestRequest);
        else if(rejoin)w.u8(1);
        m.session.event(PREPARE,w.done());
    }
    void rejoin(Server.Session s,int seq) {
        Member returning=participants.stream().filter(m->m.account.id()==s.account.id()&&m.canRejoin()).findFirst().orElse(null);
        if(returning==null||members.containsKey(returning.slot)||members.size()>=capacity) {
            s.room=null;
            s.error(ACTION_RESULT,seq,returning!=null&&members.size()>=capacity?Error.ROOM_FULL:Error.ROOM_BUSY);
            return;
        }
        returning.session=s;
        returning.displayName=s.displayName();
        returning.disconnected=false;
        returning.rejoining=true;
        returning.prepared=false;
        returning.afkWarn=false;
        returning.afkSince=0;
        returning.ack=0;
        returning.forceFull=true;
        returning.cache.clear();
        returning.input=new InputBuffer();
        returning.rejoinDeadline=System.nanoTime()+UDP_TIMEOUT_NANOS;
        returning.roleplay.awaitingLoadout=false;
        members.put(returning.slot,returning);
        s.slot=returning.slot;
        s.room=this;
        s.endpoint=null;
        s.lastUdp=System.nanoTime();
        updateRejoinable();
        s.ok(ACTION_RESULT,seq);
        broadcast();
        sendPrepare(returning,true);
    }
    int activeCount() {
        return (int)members.values().stream().filter(m->!m.rejoining).count();
    }
    void broadcast() {
        view=new View(phase,members.size());
        Writer w=new Writer().u32(id).str(name).u8(phase.ordinal()).u8(host<0?255:host).u8(members.size());
        for(Member m:members.values())w.u8(m.slot).str(m.displayName).u8(m.ready?1:0);
        w.u8(capacity).u16(matchTicks/30).u8(access.locked?1:0);
        byte[] p=w.done();
        for(Member m:members.values()) {
            Writer state=new Writer().bytes(p).u8(m.slot).u16(startingHp);
            if(roleplay!=null) {
                state.u8(roomMode.ordinal()).u8(roleplay.stage.ordinal()).u64(deadline).u64(match).u32(roleplay.revision);
                for(Member player:members.values())state.u8(player.roleplay.nation<0?255:player.roleplay.nation);
            }
            m.session.event(ROOM_STATE,state.done());
        }
    }
    void rename(Server.Session session,String value) {
        Member m=member(session);
        if(m!=null) {
            m.displayName=value;
            broadcast();
        }
    }
    void configure(Server.Session s,int seq,String value,int cap,int seconds,int passwordMode,String password) {
        configure(s,seq,value,cap,seconds,passwordMode,password,startingHp);
    }
    void configure(Server.Session s,int seq,String value,int cap,int seconds,int passwordMode,String password,int hp) {
        if(s.slot!=host) {
            s.error(ACTION_RESULT,seq,Error.NOT_HOST);
            return;
        }
        if(phase!=RoomState.WAITING) {
            s.error(ACTION_RESULT,seq,Error.INVALID_STATE);
            return;
        }
        if(hp<1||hp>World.MAX_START_HP||cap<Math.max(2,members.size())||cap>server.cfg.integer("max.players",8)||seconds<60||seconds>600||seconds%60!=0||value.isBlank()||value.getBytes(java.nio.charset.StandardCharsets.UTF_8).length>48||passwordMode<0||passwordMode>2||(passwordMode==2&&password.isEmpty())) {
            s.error(ACTION_RESULT,seq,Error.INVALID_REQUEST);
            return;
        }
        RoomAccess.validate(password);
        if(passwordMode==1)access.set("");
        else if(passwordMode==2)access.set(password);
        name=value.strip();
        capacity=cap;
        matchTicks=seconds*30;
        startingHp=hp;
        for(Member m:members.values())m.ready=false;
        s.ok(ACTION_RESULT,seq);
        broadcast();
    }
    void ready(Server.Session s,boolean value,int seq) {
        if(phase!=RoomState.WAITING) {
            s.error(ACTION_RESULT,seq,Error.INVALID_STATE);
            return;
        }
        Member m=member(s);
        if(m==null)return;
        if(value&&roleplay!=null&&m.roleplay.nation<0) {
            s.error(ACTION_RESULT,seq,Error.NOT_READY);
            return;
        }
        m.ready=value;
        s.ok(ACTION_RESULT,seq);
        broadcast();
    }
    Member member(Server.Session s) {
        Member m=members.get(s.slot);
        return m!=null&&m.session==s?m:null;
    }
    void prepare(Server.Session s,int seq) {
        if(s.slot!=host) {
            s.error(ACTION_RESULT,seq,Error.NOT_HOST);
            return;
        }
        if(phase!=RoomState.WAITING) {
            s.error(ACTION_RESULT,seq,Error.INVALID_STATE);
            return;
        }
        if(members.size()<2||members.values().stream().anyMatch(m->!m.ready||roleplay!=null&&m.roleplay.nation<0)) {
            s.error(ACTION_RESULT,seq,Error.NOT_READY);
            return;
        }
        match=server.matchId.getAndIncrement();
        seed=Long.parseLong(server.cfg.value("match.seed","20261007"))^id^Long.rotateLeft(match,21);
        arenaMap=server.largeMap;
        world=new World(arenaMap,seed,startingHp);
        world.items.configure(roleplay!=null,matchTicks);
        world.battlefield.configure(matchTicks);
        if(roleplay!=null)world.telemetry=message->System.out.println("ROLEPLAY_EFFECT match="+match+" map="+arenaMap.mapId+" hp="+startingHp+" "+message);
        participants.clear();
        rejoinableAccounts=Set.of();
        for(Member m:members.values()) {
            world.add(m.slot);
            m.rejoining=false;
            m.prepared=false;
            m.afkWarn=false;
            m.afkSince=0;
            m.previousAim=world.tanks.get(m.slot).turret;
            m.skillAimTick=0;
            m.session.endpoint=null;
            m.ack=0;
            m.forceFull=true;
            m.cache.clear();
            m.input=new InputBuffer();
            participants.add(m);
        }
        previousSpawnLayout=world.arrangeInitialSpawns(previousSpawnLayout);
        snapshotId=0;
        fullCount=deltaCount=fallbackCount=resyncCount=0;
        announced=false;
        phase=RoomState.PREPARING;
        deadline=Server.nowUs()+10_000_000;
        if(roleplay!=null)roleplay.begin();
        s.ok(ACTION_RESULT,seq);
        broadcast();
        for(Member m:members.values())sendPrepare(m,false);
    }
    void prepared(Server.Session s,long match,int seq) {
        Member m=member(s);
        if(m==null||this.match!=match||s.endpoint==null||(phase!=RoomState.PREPARING&&!(phase==RoomState.RUNNING&&m.rejoining))) {
            s.error(ACTION_RESULT,seq,Error.INVALID_STATE);
            return;
        }
        if(m.rejoining) {
            world.add(m.slot);
            World.Tank tank=world.tanks.get(m.slot);
            if(m.resultTank!=null) {
                tank.kills=m.resultTank.kills;
                tank.deaths=m.resultTank.deaths;
                tank.lastKill=m.resultTank.lastKill;
                // Disconnect is not a death: retain item deadlines without extending them.
                if(m.resultTank.hp>0) {
                    tank.armorEndTick=m.resultTank.armorEndTick;
                    tank.heEndTick=m.resultTank.heEndTick;
                    tank.invincibilityEndTick=m.resultTank.invincibilityEndTick;
                    tank.mirrorShots=m.resultTank.mirrorShots;
                }
                if(roleplay!=null) {
                    tank.qReadyTick=m.resultTank.qReadyTick;
                    tank.qBaseReadyTick=m.resultTank.qBaseReadyTick;
                    tank.qPenalty=m.resultTank.qPenalty;
                    tank.qPenaltySources.addAll(m.resultTank.qPenaltySources);
                }
            }
            if(roleplay!=null) {tank.nation=m.roleplay.nation;}
            m.rejoining=false;
            m.activity=m.activityCheck=System.nanoTime();
            m.previousAim=tank.turret;
            m.session.lastUdp=System.nanoTime();
            m.resultTank=null;
            m.prepared=true;
            s.ok(ACTION_RESULT,seq);
            s.event(START,new Writer().u64(match).u64(startUs).done());
            broadcast();
            return;
        }
        m.prepared=true;
        s.ok(ACTION_RESULT,seq);
    }
    void kick(Server.Session s,int slot,int seq) {
        if(s.slot!=host) {
            s.error(ACTION_RESULT,seq,Error.NOT_HOST);
            return;
        }
        if(phase!=RoomState.WAITING||slot==host||!members.containsKey(slot)) {
            s.error(ACTION_RESULT,seq,Error.INVALID_STATE);
            return;
        }
        leave(members.get(slot).session,4);
        s.ok(ACTION_RESULT,seq);
    }
    void leave(Server.Session s,int reason) {
        Member m=member(s);
        if(m==null) {
            if(s.room==this)s.room=null;
            return;
        }
        members.remove(m.slot);
        m.disconnected=true;
        if(roleplay!=null) {
            if(m.roleplay.usMetricsOpen)System.out.println("ROLEPLAY_UTILITY match="+match+" account="+m.account.id()+" censored=true reason=leave");
            m.roleplay.usMetricsOpen=false;
        }
        m.leaveReason=m.rejoining?3:reason;
        m.rejoining=false;
        updateRejoinable();
        if(world!=null) {
            World.Tank t=world.tanks.get(m.slot);
            if(t!=null)m.resultTank=t.copy();
            world.remove(m.slot);
        }
        if(s.room==this) {
            s.room=null;
            s.slot=-1;
            s.endpoint=null;
        }
        s.event(REMOVED,new Writer().u32(id).u8(m.slot).u8(reason).done());
        if(host==m.slot)host=members.isEmpty()?-1:members.keySet().iterator().next();
        if(phase==RoomState.RUNNING&&activeCount()<2)finish(1);
        else if(phase==RoomState.PREPARING) {
            if(roleplay!=null)roleplay.cancel("Hủy chuẩn bị: thành viên rời phòng");
            else {
                phase=RoomState.WAITING;
                members.values().forEach(x->x.session.notice(Error.NOT_READY.ordinal(),"Hủy chuẩn bị: thành viên rời phòng"));
            }
        }
        broadcast();
        if(members.isEmpty()) {
            alive=false;
            server.rooms.remove(id,this);
        }
    }
    void input(Server.Session s,int ack,List<World.Input> input) {
        if(!s.open)return;
        Member m=member(s);
        if(m==null||m.rejoining||phase!=RoomState.RUNNING)return;
        ack(s,ack);
        for(World.Input in:input)m.input.put(in,world.tick);
    }
    void input(Server.Session s,int ack,List<World.Input> input,float aimX,float aimY) {
        if(!s.open)return;
        Member m=member(s);
        if(m==null||m.rejoining||phase!=RoomState.RUNNING)return;
        input(s,ack,input);
        int newest=input.stream().mapToInt(World.Input::tick).max().orElse(0);
        if(newest>m.skillAimTick&&newest>world.tick&&newest<=world.tick+30) {
            m.skillAimTick=newest;world.skillAim(m.slot,aimX,aimY);
        }
    }
    void ack(Server.Session s,int id) {
        Member m=member(s);
        if(m!=null&&m.cache.containsKey(id)&&(m.ack==0||newer(id,m.ack)))m.ack=id;
    }
    void resync(Server.Session s) {
        Member m=member(s);
        if(m!=null) {
            m.forceFull=true;
            resyncCount++;
        }
    }
    void snapshot() {
        long before=System.nanoTime();
        byte[] full=world.encode();
        snapshotId++;
        lastEncode=System.nanoTime()-before;
        long send=System.nanoTime();
        for(Member m:members.values()) {
            Writer w=new Writer().u64(match).u32(snapshotId).u32(world.tick).u64(Server.nowUs()).u32(world.tick).u16(Math.max(0,matchTicks-world.tick));
            byte[] base=m.cache.get(m.ack);
            byte[] delta=null;
            int op=FULL;
            if(server.cfg.mode().delta()&&!m.forceFull&&world.tick%30!=0&&base!=null) {
                long enc=System.nanoTime();
                delta=DeltaCodec.encode(base,full);
                lastEncode+=System.nanoTime()-enc;
                if(delta.length+4<full.length)op=DELTA;
                else fallbackCount++;
            }
            if(op==DELTA) {
                w.u32(m.ack).bytes(delta);
                deltaCount++;
            }
            else {
                w.bytes(full);
                fullCount++;
            }
            m.forceFull=false;
            byte[] payload=w.done();
            if(payload.length+12<=1200)server.udpSend(m.session,op,payload);
            else for(byte[] part:SnapshotFragments.split(match,snapshotId,op,payload))server.udpSend(m.session,SNAPSHOT_PART,part);
            m.cache.put(snapshotId,full);
            while(m.cache.size()>128)m.cache.remove(m.cache.keySet().iterator().next());
        }
        lastSend=System.nanoTime()-send;
    }
    void finish(int reason) {
        if(phase!=RoomState.RUNNING)return;
        if(roleplay!=null)roleplay.summary();
        if(world!=null) {world.skills.clear();world.battlefield.clear();}
        phase=RoomState.RESULTS;
        rejoinableAccounts=Set.of();
        List<Member> sorted=new ArrayList<>(participants);
        for(Member m:sorted) {
            World.Tank t=world.tanks.get(m.slot);
            if(t!=null&&!m.disconnected)m.resultTank=t.copy();
            if(m.resultTank==null) {
                m.resultTank=new World.Tank();
                m.resultTank.slot=m.slot;
            }
        }
        sorted.sort(Comparator.<Member>comparingInt(m->m.resultTank.kills).reversed().thenComparingLong(m->Integer.toUnsignedLong(m.resultTank.lastKill)).thenComparingInt(m->m.slot));
        Writer w=new Writer().u64(match).u8(reason).u8(sorted.size());
        List<Database.ResultPlayer> result=new ArrayList<>();
        int rank=0;
        for(Member m:sorted) {
            World.Tank t=m.resultTank;
            rank++;
            w.u8(m.slot).str(m.displayName).u16(t.kills).u16(t.deaths).u32(t.lastKill).u8(rank).u8(m.disconnected?1:0);
            result.add(new Database.ResultPlayer(m.account.id(),m.displayName,m.slot,t.kills,t.deaths,rank,m.disconnected,roleplay==null?null:m.roleplay.nation));
        }
        byte[] payload=w.done();
        for(Member m:members.values())m.session.event(END,payload);
        Database.Result record=new Database.Result(match,arenaMap.mapId,HexFormat.of().formatHex(arenaMap.hash),server.cfg.mode().name(),startedUtc,System.currentTimeMillis(),reason,seed,List.copyOf(result),world==null?null:world.tick,roomMode.name(),roleplay==null?null:RoleplayContent.Version+"@"+RoleplayContent.Hash);
        List<Server.Session> notify=members.values().stream().map(m->m.session).toList();
        server.db.save(record).thenAccept(saved-> {
            if(!saved)for(var s:notify)s.notice(Error.DATABASE_UNAVAILABLE.ordinal(),"Kết quả đang chờ lưu; kiểm tra log và spool");
        });
        phase=RoomState.WAITING;
        for(Member m:members.values()) {
            m.ready=m.prepared=m.rejoining=m.afkWarn=false;
            m.session.endpoint=null;
            m.resultTank=null;
            m.cache.clear();m.input=new InputBuffer();m.ack=0;m.forceFull=true;
        }
        participants.clear();world=null;
        announced=false;startUs=deadline=0;
        if(roleplay!=null)roleplay.resetLobby();
        broadcast();
    }
    void stop() {
        if(!alive)return;
        stopRequested=true;
        if(thread==null) {
            tick();
            return;
        }
        if(thread!=null) {
            LockSupport.unpark(thread);
            try {
                thread.join(3000);
            }
            catch(InterruptedException e) {
                Thread.currentThread().interrupt();
            }
        }
    }
}
