package tank;
import java.net.*;
import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.*;
import java.util.function.BiConsumer;
/** Both directions use separate seeded random streams; disabled path has no queue. */
public class UdpTransport implements AutoCloseable  {
    final DatagramSocket socket;
    final Config cfg;
    final ScheduledThreadPoolExecutor scheduler=new ScheduledThreadPoolExecutor(1);
    final Random tx,rx;
    final AtomicInteger queued=new AtomicInteger();
    public final LongAdder dropped=new LongAdder();
    final long started=System.nanoTime();
    public UdpTransport(DatagramSocket s,Config c) {
        socket=s;
        cfg=c;
        long seed=Long.parseLong(c.value("impairment.seed","1"));
        tx=new Random(seed);
        rx=new Random(seed^0x6571);
        scheduler.setRemoveOnCancelPolicy(true);
    }
    public void send(byte[] a,InetSocketAddress e) {
        dispatch(a,e,(b,p)-> {
            try {
                socket.send(new DatagramPacket(b,b.length,p));
            }
            catch(Exception x) {
                if(!socket.isClosed())dropped.increment();
            }
        },tx);
    }
    public void receive(byte[] a,InetSocketAddress e,BiConsumer<byte[],InetSocketAddress> next) {
        dispatch(a,e,next,rx);
    }
    synchronized void dispatch(byte[] a,InetSocketAddress e,BiConsumer<byte[],InetSocketAddress> next,Random random) {
        if(!cfg.flag("impairment.enabled")) {
            next.accept(a,e);
            return;
        }
        long period=cfg.integer("impairment.burst.period.ms",0),duration=cfg.integer("impairment.burst.duration.ms",0),elapsed=(System.nanoTime()-started)/1_000_000;
        if((period>0&&elapsed%period<duration)||random.nextDouble()<cfg.number("impairment.loss",0)) {
            dropped.increment();
            return;
        }
        int copies=random.nextDouble()<cfg.number("impairment.duplicate",0)?2:1;
        for(int i=0;i<copies;i++) {
            double delay=cfg.number("impairment.delay.ms",0)+(random.nextDouble()*2-1)*cfg.number("impairment.jitter.ms",0);
            if(random.nextDouble()<cfg.number("impairment.reorder",0))delay+=40;
            if(queued.incrementAndGet()>8192) {
                queued.decrementAndGet();
                dropped.increment();
                continue;
            }
            scheduler.schedule(()-> {
                try {
                    next.accept(a,e);
                }
                finally {
                    queued.decrementAndGet();
                }
            },(long)Math.max(0,delay)+i,TimeUnit.MILLISECONDS);
        }
    }
    public void close() {
        scheduler.shutdownNow();
    }
}
