package tank;
import java.io.*;
import java.net.*;
import java.nio.file.*;
import java.security.*;
import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.*;
import static tank.Protocol.*;
import tank.Protocol.Error;
import tank.Protocol.Reader;
import tank.Protocol.Writer;
public class Server implements AutoCloseable  {
    final Config cfg;
    final GameMap map,largeMap;
    final Database db;
    final ConcurrentHashMap<Integer,Room> rooms=new ConcurrentHashMap<>();
    final Set<Session> connections=ConcurrentHashMap.newKeySet();
    final ConcurrentHashMap<Long,Session> accounts=new ConcurrentHashMap<>();
    final ConcurrentHashMap<String,Session> tokens=new ConcurrentHashMap<>();
    final ConcurrentHashMap<InetSocketAddress,RateLimit> endpointRates=new ConcurrentHashMap<>();
    final ConcurrentHashMap<String,RateLimit> authRates=new ConcurrentHashMap<>();
    final AtomicInteger roomId=new AtomicInteger();
    final AtomicLong matchId;
    final ServerSocket tcp;
    final DatagramSocket udp;
    final UdpTransport transport;
    final Metrics metrics;
    final ScheduledExecutorService maintenance=Executors.newSingleThreadScheduledExecutor();
    volatile boolean running=true;
    public Server(Config cfg)throws Exception  {
        this.cfg=cfg;
        map=GameMap.load(Path.of(cfg.value("map.path","source/client/Assets/StreamingAssets/map.json")));
        Path largePath=Path.of(cfg.value("map.large.path",Path.of(cfg.value("map.path","source/client/Assets/StreamingAssets/map.json")).resolveSibling("map-large.json").toString()));
        largeMap=GameMap.load(largePath);
        ServerSocket listener=new ServerSocket();
        DatagramSocket datagram=null;
        Database database=null;
        Metrics recorder=null;
        UdpTransport wrapper=null;
        AtomicLong nextMatch=null;
        try  {
            listener.bind(new InetSocketAddress(cfg.value("bind","0.0.0.0"),cfg.integer("tcp.port",8888)));
            datagram=new DatagramSocket(new InetSocketAddress(cfg.value("bind","0.0.0.0"),cfg.integer("udp.port",8889)));
            database=new Database(cfg.value("database.path","runtime/tank.db"),cfg.value("spool.path","runtime/spool"));
            nextMatch=new AtomicLong(Math.max(database.nextMatch(),System.currentTimeMillis()*1000));
            recorder=new Metrics(cfg.value("metrics.path","runtime/server.csv"));
            wrapper=new UdpTransport(datagram,cfg);
        }
        catch(Exception e)  {
            try {
                listener.close();
            }
            catch(IOException ignored) {
            }
            if(datagram!=null)datagram.close();
            if(wrapper!=null)wrapper.close();
            if(recorder!=null)recorder.close();
            if(database!=null)database.close();
            throw e;
        }
        tcp=listener;
        udp=datagram;
        db=database;
        metrics=recorder;
        transport=wrapper;
        matchId=nextMatch;
    }
    public void start() {
        Thread.ofVirtual().name("udp-receiver").start(this::udpLoop);
        Thread.ofVirtual().name("tcp-acceptor").start(()-> {
            while(running)try {
                Socket socket=tcp.accept();socket.setTcpNoDelay(true);socket.setSoTimeout(15000);if(connections.size()>=cfg.integer("max.sessions",320)) {
                    socket.close();continue;
                }
                Session s=new Session(socket);connections.add(s);s.start();
            }
            catch(IOException e) {
                if(running)System.err.println("accept: "+e.getMessage());
            }
        });
        maintenance.scheduleAtFixedRate(()-> {
            long now=System.nanoTime();for(Session s:connections)if(now-s.lastTcp>15_000_000_000L)s.close();authRates.entrySet().removeIf(e->e.getValue().expired());endpointRates.entrySet().removeIf(e->e.getValue().expired());
        },5,5,TimeUnit.SECONDS);
        System.out.println("Tank server "+cfg.mode()+" TCP="+tcp.getLocalPort()+" UDP="+udp.getLocalPort());
    }
    boolean benchmarkAuthExempt(InetAddress address) {
        if(!cfg.flag("benchmark"))return false;
        if(address.isLoopbackAddress())return true;
        for(String ip:cfg.value("benchmark.auth.allowed.ips","").split(","))if(ip.trim().equals(address.getHostAddress()))return true;
        return false;
    }
    public final class Session implements AutoCloseable  {
        CampaignSession campaign=new CampaignSession(this,db);
        final Socket socket;
        final ArrayBlockingQueue<byte[]> outbound=new ArrayBlockingQueue<>(128);
        final RateLimit actions=new RateLimit(10,10),udpRate=new RateLimit(120,240);
        volatile Database.Account account;
        volatile String nickname;
        volatile byte[] token;
        volatile Room room;
        volatile int slot=-1;
        volatile InetSocketAddress endpoint;
        volatile long lastTcp=System.nanoTime(),lastUdp;
        volatile boolean open=true;
        volatile boolean authBusy;
        final AtomicInteger sequence=new AtomicInteger();
        Session(Socket s) {
            socket=s;
        }
        String displayName() {
            return nickname!=null?nickname:account==null?"":account.name();
        }
        String ip() {
            return socket.getInetAddress().getHostAddress();
        }
        void start() {
            Thread.ofVirtual().name("tcp-writer").start(()-> {
                try {
                    OutputStream out=socket.getOutputStream();while(open) {
                        byte[] f=outbound.poll(1,TimeUnit.SECONDS);if(f!=null) {
                            out.write(f);out.flush();
                        }
                    }
                }
                catch(Exception e) {
                    close();
                }
            });
            Thread.ofVirtual().name("tcp-reader").start(()-> {
                try {
                    while(open) {
                        Packet p=Protocol.read(socket.getInputStream());lastTcp=System.nanoTime();request(p);
                    }
                }
                catch(Exception e) {
                    close();
                }
            });
        }
        void send(int op,int seq,byte[] payload) {
            if(open&&!outbound.offer(frame(op,seq,payload)))close();
        }
        void event(int op,byte[] payload) {
            send(op,0,payload);
        }
        void error(int op,int seq,Error code) {
            send(op,seq,new Writer().u16(code.ordinal()).str(code.name()).done());
        }
        void ok(int op,int seq) {
            send(op,seq,new Writer().u16(0).done());
        }
        void notice(int code,String text) {
            event(NOTICE,new Writer().u16(code).str(text).done());
        }
        void request(Packet p) {
            int response=switch(p.op()) {
                case REGISTER->REGISTER_RESULT;
                case LOGIN->LOGIN_RESULT;
                case LOGOUT->LOGOUT_RESULT;
                case ROOM_LIST->ROOM_LIST_RESULT;
                case HISTORY->HISTORY_RESULT;
                case HISTORY_DETAIL->HISTORY_DETAIL_RESULT;
                case SET_DISPLAY_NAME->DISPLAY_NAME_RESULT;
                case SHOP->SHOP_RESULT;
                default->ACTION_RESULT;
            };
            try {
                Reader r=new Reader(p.payload());
                if(p.op()==CampaignSession.REQUEST) {
                    if(!actions.allow()){campaign.failure(p.sequence(),"Thao tác quá nhanh. Hãy thử lại.");return;}
                    campaign.request(p);return;
                }
                if(p.op()==HEARTBEAT) {
                    long nonce=r.u64();
                    r.end();
                    send(HEARTBEAT_ACK,p.sequence(),new Writer().u64(nonce).done());
                    return;
                }
                if(p.op()==REGISTER||p.op()==LOGIN) {
                    authenticate(p,response,r);
                    return;
                }
                if(account==null)throw new Database.Failure(Error.UNAUTHENTICATED);
                boolean matchAction=p.op()==USE_SKILL;
                if(!(matchAction&&room!=null&&room.roleplay!=null)&&!actions.allow())throw new Database.Failure(Error.RATE_LIMITED);
                switch(p.op()) {
                    case SHOP-> {
                        int command=r.u8();long match=r.u64();
                        if(command==0) {
                            r.end();
                            if(match==0)RoleplayShop.wallet(this,db,p.sequence(),0,0,0,0,new int[GameRules.ItemTypeCount]);
                            else {
                                Room target=requireRoom();
                                if(target.roleplay==null)throw new Database.Failure(Error.INVALID_STATE);
                                target.enqueue(()->target.roleplay.shop.query(this,match,p.sequence()));
                            }
                        }else if(command==1) {
                            int wave=r.u8(),item=r.u8();r.end();Room target=requireRoom();
                            if(target.roleplay==null)throw new Database.Failure(Error.INVALID_STATE);
                            target.enqueue(()->target.roleplay.shop.buy(this,match,wave,item,p.sequence()));
                        }else if(command==2) {
                            int revision=r.u32(),item=r.u8();r.end();Room target=requireRoom();
                            if(target.roleplay==null)throw new Database.Failure(Error.INVALID_STATE);
                            target.enqueue(()->target.roleplay.shop.use(this,match,revision,item,p.sequence()));
                        }else throw new IllegalArgumentException("shop command");
                    }
                    case LOGOUT-> {
                        r.end();
                        Room target=room;
                        if(target==null) {
                            logout();
                            ok(LOGOUT_RESULT,p.sequence());
                        }
                        else {
                            authBusy=true;
                            try {
                                target.enqueue(()-> {
                                    target.leave(this,0);logout();authBusy=false;ok(LOGOUT_RESULT,p.sequence());
                                });
                            }
                            catch(RuntimeException e) {
                                authBusy=false;
                                throw e;
                            }
                        }
                    }
                    case ROOM_LIST-> {
                        r.end();
                        List<Room> listed=new ArrayList<>(rooms.values());
                        Writer w=new Writer().u16(0).u16(listed.size());
                        for(Room room:listed) {
                            Room.View v=room.view;
                            w.u32(room.id).str(room.name).u8(v.count()).u8(room.capacity).u8(v.state().ordinal());
                        }
                        for(Room listedRoom:listed)w.u8(listedRoom.access.locked?1:0);
                        for(Room listedRoom:listed)w.u8(listedRoom.canRejoin(this)?1:0);
                        for(Room listedRoom:listed)w.u8(listedRoom.roomMode.ordinal());
                        send(ROOM_LIST_RESULT,p.sequence(),w.done());
                    }
                    case CREATE_ROOM-> {
                        if(campaign.active())throw new Database.Failure(Error.INVALID_STATE);
                        String name=r.str(48);
                        int capacity=cfg.integer("max.players",8),seconds=cfg.integer("match.ticks",5400)/30;
                        boolean custom=r.b.hasRemaining();
                        if(custom) {
                            capacity=r.u8();
                            seconds=r.u16();
                            if(capacity<2||capacity>cfg.integer("max.players",8)||seconds<60||seconds>600||seconds%60!=0)throw new IllegalArgumentException("room settings");
                        }
                        String roomPassword=r.b.hasRemaining()?r.str(128):"";
                        RoomAccess.validate(roomPassword);
                        int hp=r.b.hasRemaining()?r.u16():World.DEFAULT_HP;
                        if(hp<1||hp>World.MAX_START_HP)throw new IllegalArgumentException("starting HP");
                        int roomMode=r.b.hasRemaining()?r.u8():0;
                        if(roomMode>1)throw new IllegalArgumentException("room mode");
                        r.end();
                        if(name.isBlank())throw new IllegalArgumentException("name");
                        synchronized(rooms) {
                            if(room!=null)throw new Database.Failure(Error.INVALID_STATE);
                            if(rooms.size()>=cfg.integer("max.rooms",40))throw new Database.Failure(Error.ROOM_FULL);
                            Room created=new Room(Server.this,roomId.incrementAndGet(),name,capacity,custom?seconds*30:cfg.integer("match.ticks",5400),RoomMode.values()[roomMode]);
                            created.startingHp=hp;
                            created.access.set(roomPassword);
                            rooms.put(created.id,created);
                            room=created;
                            created.enqueue(()->created.join(this,p.sequence(),roomPassword));
                            created.start();
                        }
                    }
                    case JOIN_ROOM-> {
                        if(campaign.active())throw new Database.Failure(Error.INVALID_STATE);
                        int id=r.u32();
                        String joinPassword=r.b.hasRemaining()?r.str(128):"";
                        RoomAccess.validate(joinPassword);
                        r.end();
                        synchronized(this) {
                            if(room!=null)throw new Database.Failure(Error.INVALID_STATE);
                            Room target=rooms.get(id);
                            if(target==null)throw new Database.Failure(Error.ROOM_MISSING);
                            room=target;
                            try {
                                target.enqueue(()->target.join(this,p.sequence(),joinPassword));
                            }
                            catch(RuntimeException e) {
                                room=null;
                                throw e;
                            }
                        }
                    }
                    case SET_DISPLAY_NAME-> {
                        String value=Database.validDisplayName(r.str(96));
                        r.end();
                        long id=account.id();
                        db.submit(()->db.setDisplayName(id,value)).whenComplete((saved,e)-> {
                            if(e!=null) {
                                error(DISPLAY_NAME_RESULT,p.sequence(),Error.DATABASE_UNAVAILABLE);return;
                            }
                            synchronized(this) {
                                if(!open||account==null||account.id()!=id)return;nickname=saved;
                            }
                            Room target=room;if(target!=null)try {
                                target.enqueue(()->target.rename(this,saved));
                            }
                            catch(Database.Failure ignored) {
                            }
                            send(DISPLAY_NAME_RESULT,p.sequence(),new Writer().u16(0).str(saved).done());
                        });
                    }
                    case ROOM_SETTINGS-> {
                        String value=r.str(48);
                        int cap=r.u8(),seconds=r.u16(),mode=r.u8();
                        String password=mode==2?r.str(128):"";
                        RoomAccess.validate(password);
                        int hp=r.b.hasRemaining()?r.u16():-1;
                        if(hp!=-1&&(hp<1||hp>World.MAX_START_HP))throw new IllegalArgumentException("starting HP");
                        r.end();
                        Room target=requireRoom();
                        target.enqueue(()->target.configure(this,p.sequence(),value,cap,seconds,mode,password,hp<0?target.startingHp:hp));
                    }
                    case LEAVE_ROOM-> {
                        r.end();
                        Room target=requireRoom();
                        target.enqueue(()-> {
                            target.leave(this,0);ok(ACTION_RESULT,p.sequence());
                        });
                    }
                    case SET_READY-> {
                        boolean ready=r.bool();
                        r.end();
                        Room target=requireRoom();
                        target.enqueue(()->target.ready(this,ready,p.sequence()));
                    }
                    case START_MATCH-> {
                        r.end();
                        Room target=requireRoom();
                        target.enqueue(()->target.prepare(this,p.sequence()));
                    }
                    case KICK_PLAYER-> {
                        int slot=r.u8();
                        r.end();
                        Room target=requireRoom();
                        target.enqueue(()->target.kick(this,slot,p.sequence()));
                    }
                    case PREPARED-> {
                        long match=r.u64();
                        Room target=requireRoom();
                        if(target.roleplay==null) {
                            r.end();target.enqueue(()->target.prepared(this,match,p.sequence()));
                        }
                        else {
                            int stage=r.u8(),revision=r.u32();byte[] hash=r.bytes(32);r.end();
                            target.enqueue(()->target.roleplay.prepared(this,match,stage,revision,hash,p.sequence()));
                        }
                    }
                    case SELECT_NATION-> {
                        int nation=r.u8();r.end();Room target=requireRoom();
                        if(target.roleplay==null)throw new Database.Failure(Error.INVALID_STATE);
                        target.enqueue(()->target.roleplay.selectNation(this,nation,p.sequence()));
                    }
                    case USE_SKILL-> {
                        long match=r.u64();int requestId=r.u32(),slot=r.u8();float aimX=r.b.hasRemaining()?r.f32():0,aimY=r.b.hasRemaining()?r.f32():0;r.end();
                        Room target=requireRoom();if(target.roleplay==null)throw new Database.Failure(Error.INVALID_STATE);
                        target.enqueue(()->target.roleplay.use(this,match,requestId,slot,p.payload(),p.sequence(),aimX,aimY));
                    }
                    case HISTORY_DETAIL-> {
                        long match=r.u64();
                        r.end();
                        long id=account.id();
                        db.submit(()->db.historyDetail(id,match)).whenComplete((detail,e)-> {
                            if(e!=null) {
                                error(HISTORY_DETAIL_RESULT,p.sequence(),e instanceof Database.Failure f?f.code:Error.DATABASE_UNAVAILABLE);return;
                            }
                            Writer w=new Writer().u16(0).u64(detail.id()).u16(detail.mapId()).u64(detail.started()).u64(detail.ended()).u8(detail.reason()).u8(detail.players().size());
                            for(var player:detail.players())w.u8(player.slot()).str(player.nickname()).u16(player.kills()).u16(player.deaths()).u8(player.rank()).u8(player.disconnected()?1:0).u8(player.account()==id?1:0);
                            boolean metadata=detail.roomMode()!=null||detail.contentVersion()!=null||detail.players().stream().anyMatch(player->player.nationId()!=null);
                            if(detail.durationTicks()!=null||metadata)w.u32(detail.durationTicks()==null?-1:detail.durationTicks());
                            if(metadata) {
                                w.u8(1).str(detail.roomMode()==null?"":detail.roomMode()).str(detail.contentVersion()==null?"":detail.contentVersion()).u8(detail.players().size());
                                for(var player:detail.players())w.u8(player.slot()).u8(player.nationId()==null?255:player.nationId());
                            }
                            send(HISTORY_DETAIL_RESULT,p.sequence(),w.done());
                        });
                    }
                    case HISTORY-> {
                        int offset=r.u16(),limit=r.u8();
                        r.end();
                        if(offset>1000||limit<1||limit>20)throw new IllegalArgumentException("pagination");
                        long id=account.id();
                        db.submit(()->db.history(id,offset,limit)).whenComplete((rows,e)-> {
                            if(e!=null) {
                                error(HISTORY_RESULT,p.sequence(),Error.DATABASE_UNAVAILABLE);return;
                            }
                            Writer w=new Writer().u16(0).u8(rows.size());for(var h:rows)w.u64(h.match()).u64(h.ended()).u8(h.reason()).u16(h.kills()).u16(h.deaths()).u8(h.rank());send(HISTORY_RESULT,p.sequence(),w.done());
                        });
                    }
                    default->throw new IllegalArgumentException("opcode");
                }
            }
            catch(Database.Failure f) {
                error(response,p.sequence(),f.code);
            }
            catch(RuntimeException e) {
                error(response,p.sequence(),Error.INVALID_REQUEST);
            }
        }
        void authenticate(Packet p,int response,Reader r) {
            String username=r.str(24),password=r.str(256);
            r.end();
            synchronized(this) {
                if(!open)return;
                if(account!=null||authBusy)throw new Database.Failure(Error.INVALID_STATE);
                if(!benchmarkAuthExempt(socket.getInetAddress())&&!authRates.computeIfAbsent(ip(),k->new RateLimit(5.0/60,5)).allow())throw new Database.Failure(Error.RATE_LIMITED);
                authBusy=true;
            }
            db.submit(()-> {
                var authenticated=p.op()==REGISTER?db.register(username,password):db.login(username,password);if(p.op()==LOGIN)nickname=db.displayName(authenticated.id(),authenticated.name());return authenticated;
            }).whenComplete((a,e)-> {
                synchronized(this) {
                    try {
                        if(!open)return; if(e!=null) {
                            error(response,p.sequence(),e instanceof Database.Failure f?f.code:Error.DATABASE_UNAVAILABLE);return;
                        }
                        if(p.op()==REGISTER) {
                            ok(response,p.sequence());return;
                        }
                        if(accounts.putIfAbsent(a.id(),this)!=null) {
                            error(response,p.sequence(),Error.ALREADY_LOGGED_IN);return;
                        }
                        account=a;token=new byte[16];new SecureRandom().nextBytes(token);tokens.put(HexFormat.of().formatHex(token),this); send(LOGIN_RESULT,p.sequence(),new Writer().u16(0).u64(a.id()).str(displayName()).bytes(token).str(a.name()).done());
                    }
                    finally {
                        authBusy=false;
                    }
                }
            });
        }
        Room requireRoom() {
            Room r=room;
            if(r==null)throw new Database.Failure(Error.INVALID_STATE);
            return r;
        }
        void leave(int reason) {
            Room target=room;
            if(target!=null)try {
                target.enqueue(()->target.leave(this,reason));
            }
            catch(Database.Failure ignored) {
            }
        }
        synchronized void logout() {
            campaign.close();
            campaign=new CampaignSession(this,db);
            Database.Account a=account;
            if(a!=null)accounts.remove(a.id(),this);
            if(token!=null)tokens.remove(HexFormat.of().formatHex(token),this);
            account=null;
            token=null;
            endpoint=null;
        }
        public synchronized void close() {
            if(!open)return;
            open=false;
            leave(1);
            logout();
            connections.remove(this);
            try {
                socket.close();
            }
            catch(IOException ignored) {
            }
        }
    }
    void udpLoop() {
        byte[] buffer=new byte[1201];
        while(running)try {
            DatagramPacket d=new DatagramPacket(buffer,buffer.length);
            udp.receive(d);
            byte[] raw=Arrays.copyOf(d.getData(),d.getLength());
            InetSocketAddress endpoint=(InetSocketAddress)d.getSocketAddress();
            transport.receive(raw,endpoint,this::route);
        }
        catch(IOException e) {
            if(running)System.err.println("udp receive error");
        }
    }
    void route(byte[] raw,InetSocketAddress endpoint) {
        try {
            if(endpointRates.size()>=4096&&!endpointRates.containsKey(endpoint))return;
            if(!endpointRates.computeIfAbsent(endpoint,k->new RateLimit(120,240)).allow())return;
            Packet p=decode(raw,true);
            Reader r=new Reader(p.payload());
            Session s=tokens.get(HexFormat.of().formatHex(r.bytes(16)));
            long match=r.u64();
            if(s==null||!s.open||s.room==null||!s.udpRate.allow())return;
            Room room=s.room;
            if(room.match!=match||(room.phase!=RoomState.PREPARING&&room.phase!=RoomState.RUNNING))return;
            if(p.op()==HANDSHAKE) {
                long nonce=r.u64();
                r.end();
                if(s.endpoint!=null&&!s.endpoint.equals(endpoint))return;
                room.enqueue(()-> {
                    Room.Member member=room.member(s);if(s.room!=room||room.match!=match||member==null||!(room.phase==RoomState.PREPARING||room.phase==RoomState.RUNNING&&member.rejoining))return;s.endpoint=endpoint;s.lastUdp=System.nanoTime();udpSend(s,HANDSHAKE_ACK,new Writer().u64(match).u64(nonce).u8(s.slot).u64(nowUs()).done());
                });
                return;
            }
            if(!endpoint.equals(s.endpoint))return;
            switch(p.op()) {
                case PING-> {
                    long client=r.u64();
                    r.end();
                    long received=nowUs();
                    udpSend(s,PONG,new Writer().u64(match).u64(client).u64(received).u64(nowUs()).done());
                }
                case INPUT-> {
                    int ack=r.u32(),count=r.u8();
                    if(count<1||count>(cfg.mode().redundant()?3:1))return;
                    List<World.Input> input=new ArrayList<>();
                    for(int i=0;i<count;i++)input.add(new World.Input(r.u32(),r.u8(),r.u16()));
                    float aimX=r.b.hasRemaining()?r.f32():0,aimY=r.b.hasRemaining()?r.f32():0;
                    r.end();
                    room.enqueue(()->room.input(s,ack,input,aimX,aimY));
                }
                case ACK-> {
                    int id=r.u32();
                    r.end();
                    room.enqueue(()->room.ack(s,id));
                }
                case RESYNC-> {
                    r.u32();
                    r.end();
                    room.enqueue(()->room.resync(s));
                }
                default-> {
                    return;
                }
            }
            s.lastUdp=System.nanoTime();
            metrics.received(raw.length);
        }
        catch(RuntimeException ignored) {
            metrics.invalid.increment();
        }
    }
    void udpSend(Session s,int op,byte[] p) {
        if(s.endpoint==null)return;
        byte[] raw=frame(op,s.sequence.incrementAndGet(),p);
        if(raw.length>1200) {
            System.err.println("UDP size invariant");
            return;
        }
        transport.send(raw,s.endpoint);
        metrics.sent(raw.length);
    }
    static final long CLOCK_ORIGIN=System.nanoTime();
    static long nowUs() {
        return (System.nanoTime()-CLOCK_ORIGIN)/1000;
    }
    public synchronized void close() {
        if(!running)return;
        running=false;
        try {
            tcp.close();
        }
        catch(IOException ignored) {
        }
        maintenance.shutdownNow();
        endpointRates.clear();
        for(Session s:connections)s.close();
        for(Room r:rooms.values())r.stop();
        transport.close();
        udp.close();
        db.close();
        metrics.close();
    }
}
