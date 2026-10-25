package tank;

import java.io.IOException;
import java.net.*;
import java.nio.file.*;
import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicInteger;
import com.google.gson.GsonBuilder;
import static tank.Protocol.*;

/** External load generator: production server runs in a separate JVM. No game code is patched. */
public final class NetworkBenchmark {
    static final long STEP_US=1_000_000L/30;
    static final String PASSWORD="benchmark-only-2026";
    static long us(){return System.nanoTime()/1000;}
    static void json(Path path,Object value)throws IOException {
        Files.writeString(path,new GsonBuilder().setPrettyPrinting().create().toJson(value));
    }
    record Profile(String name,double loss,int delay,int jitter){}
    record Trial(Mode mode,Profile profile,int repeat,long seed){}

    static final class Client implements AutoCloseable {
        final Socket tcp;
        final DatagramSocket udp=new DatagramSocket(new InetSocketAddress("127.0.0.1",0));
        final InetSocketAddress remote;
        final AtomicInteger seq=new AtomicInteger();
        final Map<Integer,CompletableFuture<Packet>> pending=new ConcurrentHashMap<>();
        final LinkedHashMap<Integer,byte[]> cache=new LinkedHashMap<>();
        final ArrayDeque<World.Input> recent=new ArrayDeque<>();
        final List<Map<String,Object>> snapshots=new ArrayList<>(),pings=new ArrayList<>();
        volatile boolean open=true,handshake,prepared,ended;
        volatile Throwable failure;
        volatile byte[] token;
        volatile long match,startUs,lastArrival,lastValid,snapshotTime;
        volatile int roomId,slot=-1,snapshotId,snapshotTick,endReason=-1,endPlayers,disconnected;
        volatile double offset,minRtt=Double.MAX_VALUE;
        volatile int pingSamples,invalid,missingBase,decoded,full,delta;
        int targetTick,udpSeq;
        long lastHeartbeat,lastHandshake,lastPing,lastAck,lastResync;
        final Mode mode;
        final int warmTicks;
        Client(int tcpPort,int udpPort,Mode mode,int warmTicks)throws Exception {
            this.mode=mode;this.warmTicks=warmTicks;
            remote=new InetSocketAddress("127.0.0.1",udpPort);
            tcp=new Socket();tcp.connect(new InetSocketAddress("127.0.0.1",tcpPort),5000);tcp.setTcpNoDelay(true);
            Thread.ofVirtual().name("benchmark-tcp").start(this::readTcp);
            Thread.ofVirtual().name("benchmark-udp").start(this::readUdp);
        }
        int sendTcp(int op,byte[] payload,CompletableFuture<Packet> future)throws IOException {
            int id=seq.incrementAndGet();if(future!=null)pending.put(id,future);
            synchronized(tcp){tcp.getOutputStream().write(frame(op,id,payload));tcp.getOutputStream().flush();}
            return id;
        }
        Packet request(int op,byte[] payload)throws Exception {
            var f=new CompletableFuture<Packet>();sendTcp(op,payload,f);
            Packet p=f.get(20,TimeUnit.SECONDS);Reader r=new Reader(p.payload());
            int status=r.u16();if(status!=0)throw new IOException("op="+op+" status="+status);
            return p;
        }
        void auth(String name)throws Exception {
            byte[] creds=new Writer().str(name).str(PASSWORD).done();request(REGISTER,creds);
            Reader r=new Reader(request(LOGIN,creds).payload());r.u16();r.u64();r.str(96);token=r.bytes(16);
        }
        void readTcp(){
            try {while(open){Packet p=Protocol.read(tcp.getInputStream());
                switch(p.op()){
                    case ROOM_STATE -> {Reader r=new Reader(p.payload());roomId=r.u32();}
                    case PREPARE -> {Reader r=new Reader(p.payload());match=r.u64();roomId=r.u32();slot=r.u8();
                        r.u16();r.bytes(32);int m=r.u8(),hz=r.u8(),port=r.u16();
                        if(m!=mode.ordinal()||hz!=30||port!=remote.getPort())throw new IOException("prepare mismatch");}
                    case START -> {Reader r=new Reader(p.payload());if(r.u64()!=match)throw new IOException("start match");startUs=r.u64();}
                    case END -> {Reader r=new Reader(p.payload());if(r.u64()!=match)throw new IOException("end match");
                        endReason=r.u8();endPlayers=r.u8();for(int i=0;i<endPlayers;i++){
                            r.u8();r.str(96);r.u16();r.u16();r.u32();r.u8();if(r.bool())disconnected++;}
                        r.end();ended=true;}
                    case REMOVED -> throw new IOException("client removed");
                    default -> {}
                }
                var f=pending.remove(p.sequence());if(f!=null)f.complete(p);
            }}catch(Throwable e){if(open){failure=e;pending.values().forEach(f->f.completeExceptionally(e));}}
        }
        void readUdp(){
            byte[] buf=new byte[1201];
            while(open)try{
                DatagramPacket d=new DatagramPacket(buf,buf.length);udp.receive(d);
                if(!remote.equals(d.getSocketAddress()))continue;
                Packet p=decode(Arrays.copyOf(d.getData(),d.getLength()),true);Reader r=new Reader(p.payload());
                long now=us();if(r.u64()!=match)throw new IllegalArgumentException("UDP match");
                switch(p.op()){
                    case HANDSHAKE_ACK -> {r.u64();if(r.u8()!=slot)throw new IllegalArgumentException("slot");r.u64();r.end();handshake=true;}
                    case PONG -> {long t1=r.u64(),t2=r.u64(),t3=r.u64();r.end();double rtt=now-t1-(t3-t2);
                        if(rtt<0)break;double estimate=((double)t2-t1+(double)t3-now)/2;
                        if(rtt<minRtt){minRtt=rtt;if(pingSamples<8)offset=estimate;}
                        if(pingSamples>=8)offset=offset*.9+estimate*.1;pingSamples++;
                        if(startUs>0&&now+offset>=startUs+warmTicks*STEP_US&&!ended){
                            synchronized(pings){pings.add(Map.of("client_time_us",now,"rtt_us",rtt,"offset_us",offset));}}
                    }
                    case FULL,DELTA -> {
                        int id=r.u32(),tick=r.u32();long time=r.u64();r.u32();r.u16();
                        byte[] world;
                        if(p.op()==FULL){world=r.bytes(r.b.remaining());World.decode(world);full++;}
                        else {int base=r.u32();byte[] previous=cache.get(base);
                            if(previous==null){missingBase++;resync(now);break;}
                            world=DeltaCodec.apply(previous,r.bytes(r.b.remaining()));delta++;}
                        r.end();cache.put(id,world);while(cache.size()>128)cache.remove(cache.keySet().iterator().next());decoded++;
                        if(snapshotId!=0&&!newer(id,snapshotId))break;
                        snapshotTick=tick;snapshotTime=time;snapshotId=id;lastArrival=now;lastValid=now;
                        if(tick>warmTicks){synchronized(snapshots){snapshots.add(Map.of("tick",tick,"id",id,"age_us",now+offset-time,"bytes",d.getLength(),"op",p.op()));}}
                    }
                    default -> throw new IllegalArgumentException("UDP opcode");
                }
            }catch(SocketException e){if(open)failure=e;}
             catch(Throwable e){invalid++;failure=e;}
        }
        Writer prefix(){return new Writer().bytes(token).u64(match);}
        synchronized void sendUdp(int op,byte[] payload)throws IOException {
            byte[] b=frame(op,++udpSeq,payload);udp.send(new DatagramPacket(b,b.length,remote));
        }
        synchronized void resync(long now)throws IOException {
            if(now-lastResync>=500_000){lastResync=now;sendUdp(RESYNC,prefix().u32(snapshotId).done());}
        }
        void poll()throws Exception {
            if(failure!=null)throw new IOException("client failure",failure);
            if(ended)return;long now=us();
            if(now-lastHeartbeat>5_000_000){lastHeartbeat=now;sendTcp(HEARTBEAT,new Writer().u64(now).done(),null);}
            if(match==0)return;
            if(!handshake&&now-lastHandshake>500_000){lastHandshake=now;sendUdp(HANDSHAKE,prefix().u64(7).done());}
            if(!handshake)return;
            if(now-lastPing>(pingSamples<8?100_000:1_000_000)){lastPing=now;sendUdp(PING,prefix().u64(now).done());}
            if(now-lastAck>100_000){lastAck=now;sendUdp(ACK,prefix().u32(snapshotId).done());}
            if(pingSamples>=8&&!prepared){prepared=true;sendTcp(PREPARED,new Writer().u64(match).done(),null);}
            if(startUs==0||now+offset<startUs||snapshotId==0)return;
            if(now-lastValid>3_000_000)resync(now);
            int lead=Math.max(2,Math.min(12,(int)Math.ceil(minRtt/2/STEP_US)+2));
            int elapsed=(int)Math.floor(Math.max(0,Math.min(400_000,now+offset-snapshotTime))*30/1e6);
            int desired=snapshotTick+Math.min(30,elapsed+lead);
            if(targetTick==0)targetTick=desired-1;
            if(desired-targetTick>30){recent.clear();targetTick=desired-1;}
            for(int n=0;n<3&&targetTick<desired;n++){
                int tick=++targetTick;
                int direction=new int[]{1,8,2,4}[(tick/30+slot)%4];
                World.Input input=new World.Input(tick,direction|16,Math.floorMod(tick*17+slot*9000,36000));
                recent.addLast(input);while(recent.size()>3)recent.removeFirst();
                Writer w=prefix().u32(snapshotId).u8(mode.redundant()?recent.size():1);
                if(mode.redundant()){var it=recent.descendingIterator();while(it.hasNext()){var v=it.next();w.u32(v.tick()).u8(v.buttons()).u16(v.angle());}}
                else w.u32(input.tick()).u8(input.buttons()).u16(input.angle());
                sendUdp(INPUT,w.done());
            }
        }
        Map<String,Object> result(){
            var out=new LinkedHashMap<String,Object>();out.put("slot",slot);out.put("ended",ended);out.put("reason",endReason);
            out.put("end_players",endPlayers);out.put("disconnected",disconnected);out.put("last_tick",snapshotTick);
            out.put("decoded",decoded);out.put("full",full);out.put("delta",delta);out.put("invalid",invalid);out.put("missing_base",missingBase);
            synchronized(snapshots){out.put("snapshots",new ArrayList<>(snapshots));}
            synchronized(pings){out.put("pings",new ArrayList<>(pings));}return out;
        }
        public void close(){open=false;try{tcp.close();}catch(Exception ignored){}udp.close();}
    }

    static void run(Path root,Trial trial,int seconds,int warmSeconds,int players)throws Exception {
        String id=trial.profile.name+"_r"+trial.repeat+"_"+trial.mode;
        Path dir=root.resolve(id);Files.createDirectories(dir);
        var config=new Properties();try(var in=Files.newInputStream(Path.of("config/server.properties"))){config.load(in);}
        config.setProperty("bind","127.0.0.1");config.setProperty("tcp.port","0");config.setProperty("udp.port","0");
        config.setProperty("benchmark","true");config.setProperty("network.mode",trial.mode.name());config.setProperty("match.ticks",""+(seconds*30));
        config.setProperty("match.seed",""+trial.seed);config.setProperty("impairment.seed",""+trial.seed);
        config.setProperty("impairment.enabled",""+!trial.profile.name.equals("clean"));
        config.setProperty("impairment.loss",""+trial.profile.loss);config.setProperty("impairment.delay.ms",""+trial.profile.delay);
        config.setProperty("impairment.jitter.ms",""+trial.profile.jitter);
        for(String key:List.of("impairment.duplicate","impairment.reorder","impairment.burst.period.ms","impairment.burst.duration.ms"))config.setProperty(key,"0");
        config.setProperty("database.path",dir.resolve("tank.db").toString().replace('\\','/'));
        config.setProperty("spool.path",dir.resolve("spool").toString().replace('\\','/'));
        config.setProperty("metrics.path",dir.resolve("server.csv").toString().replace('\\','/'));
        config.setProperty("log.path",dir.resolve("server.log").toString().replace('\\','/'));
        config.setProperty("control.stop.path",dir.resolve("stop").toString().replace('\\','/'));
        try(var out=Files.newOutputStream(dir.resolve("server.properties"))){config.store(out,"Isolated benchmark configuration");}
        Path console=dir.resolve("console.log");
        Process server=new ProcessBuilder(Path.of(System.getProperty("java.home"),"bin","java.exe").toString(),"-Xms128m","-Xmx512m",
            "-jar","build/server/tank-server-1.0.0.jar",dir.resolve("server.properties").toString())
            .redirectErrorStream(true).redirectOutput(console.toFile()).start();
        List<Client> clients=new ArrayList<>();boolean success=false;
        try {
            long deadline=us()+20_000_000;int tcpPort=0,udpPort=0;
            while(us()<deadline){if(!server.isAlive())throw new IOException("server exit "+server.exitValue());
                String text=Files.exists(console)?Files.readString(console):"";
                var m=java.util.regex.Pattern.compile("TCP=(\\d+) UDP=(\\d+)").matcher(text);
                if(m.find()){tcpPort=Integer.parseInt(m.group(1));udpPort=Integer.parseInt(m.group(2));break;}Thread.sleep(50);}
            if(tcpPort==0)throw new IOException("server startup timeout");
            for(int i=0;i<players;i++){Client c=new Client(tcpPort,udpPort,trial.mode,warmSeconds*30);clients.add(c);c.auth("bench_"+i);}
            Client host=clients.get(0);host.request(CREATE_ROOM,new Writer().str("Benchmark").done());
            deadline=us()+5_000_000;while(host.roomId==0&&us()<deadline)Thread.sleep(5);
            if(host.roomId==0)throw new IOException("room missing");
            for(int i=1;i<players;i++)clients.get(i).request(JOIN_ROOM,new Writer().u32(host.roomId).done());
            for(Client c:clients)c.request(SET_READY,new Writer().u8(1).done());
            host.request(START_MATCH,new byte[0]);deadline=us()+(seconds+25L)*1_000_000;
            while(us()<deadline&&clients.stream().anyMatch(c->!c.ended)){
                for(Client c:clients)c.poll();Thread.sleep(1);
            }
            for(Client c:clients){if(!c.ended||c.endReason!=0||c.endPlayers!=players||c.disconnected!=0||c.snapshotTick<seconds*30-30||c.invalid!=0)
                throw new IOException("incomplete run: "+c.result().entrySet().stream().filter(e->!e.getKey().equals("snapshots")&&!e.getKey().equals("pings")).toList());}
            success=true;
        }finally {
            json(dir.resolve("clients.json"),clients.stream().map(Client::result).toList());
            json(dir.resolve("trial.json"),Map.of("id",id,"mode",trial.mode.name(),"profile",trial.profile,"repeat",trial.repeat,"seed",trial.seed,
                "seconds",seconds,"warm_seconds",warmSeconds,"players",players,"success",success));
            clients.forEach(Client::close);Files.writeString(dir.resolve("stop"),"stop");
            if(!server.waitFor(15,TimeUnit.SECONDS)){server.destroy();if(!server.waitFor(5,TimeUnit.SECONDS))server.destroyForcibly();}
        }
    }
    public static void main(String[] args)throws Exception {
        Path root=Path.of(args.length>0?args[0]:"report/benchmarks/network-20261015");
        int seconds=args.length>1?Integer.parseInt(args[1]):30;
        int repeats=args.length>2?Integer.parseInt(args[2]):3;
        boolean pilot=args.length>3&&args[3].equals("pilot");
        Mode onlyMode=args.length>3&&!pilot?Mode.valueOf(args[3]):null;
        if(Files.exists(root))throw new IOException("Output already exists; choose a new directory");
        Files.createDirectories(root);
        json(root.resolve("environment.json"),Map.of("java",System.getProperty("java.version"),"vendor",System.getProperty("java.vendor"),
            "os",System.getProperty("os.name"),"arch",System.getProperty("os.arch"),"logical_processors",Runtime.getRuntime().availableProcessors(),
            "created",java.time.OffsetDateTime.now().toString(),"server_jvm",List.of("-Xms128m","-Xmx512m"),"transport","IPv4 loopback"));
        List<Profile> profiles=pilot?List.of(new Profile("clean",0,0,0)):
            List.of(new Profile("clean",0,0,0),new Profile("loss5",.05,0,0),new Profile("delay50_jitter20_loss5",.05,50,20));
        List<Trial> trials=new ArrayList<>();for(int repeat=1;repeat<=repeats;repeat++)for(Profile profile:profiles){
            List<Mode> modes=new ArrayList<>(onlyMode==null?List.of(Mode.values()):List.of(onlyMode));Collections.shuffle(modes,new Random(20261015L+repeat*31L+profile.name.hashCode()));
            for(Mode mode:modes)trials.add(new Trial(mode,profile,repeat,20261015L+repeat));}
        int i=0;for(Trial trial:trials){System.out.printf("BEGIN %d/%d %s %s repeat=%d%n",++i,trials.size(),trial.profile.name,trial.mode,trial.repeat);System.out.flush();
            run(root,trial,seconds,Math.min(5,seconds/3),4);System.out.printf("PASS %d/%d%n",i,trials.size());System.out.flush();}
        System.out.println("BENCHMARK COMPLETE "+trials.size()+" runs");
    }
}
