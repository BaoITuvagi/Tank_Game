package tank;
import java.io.*;
import java.nio.file.*;
import java.lang.management.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.*;
/** Room threads capture numbers only. A single worker owns formatting and disk I/O. */
public class Metrics implements AutoCloseable  {
    final PrintWriter out;
    final ArrayBlockingQueue<Sample> samples = new ArrayBlockingQueue<>(8192);
    final Thread writer;
    volatile boolean open = true;
    public final LongAdder txBytes = new LongAdder(), rxBytes = new LongAdder();
    public final LongAdder txPackets = new LongAdder(), rxPackets = new LongAdder();
    public final LongAdder invalid = new LongAdder(), droppedSamples = new LongAdder();
    final long epoch = System.nanoTime();
    record Sample(double elapsed, int room, long match, String mode, int tick, long duration, long encode, long send, long lag, long[] counters)  {
    }
    public Metrics(String path) throws IOException  {
        this(openWriter(path));
    }
    Metrics(Writer destination)  {
        out = new PrintWriter(destination);
        writer = Thread.ofPlatform().name("metrics-writer").start(this::writeLoop);
    }
    static Writer openWriter(String path) throws IOException  {
        Path p = Path.of(path);
        Files.createDirectories(p.toAbsolutePath().getParent());
        return Files.newBufferedWriter(p);
    }
    public void sent(int n)  {
        txBytes.add(n);
        txPackets.increment();
    }
    public void received(int n)  {
        rxBytes.add(n);
        rxPackets.increment();
    }
    public void row(Room r, long loopStarted, long encode, long send, long lag)  {
        long on = 0, missing = 0, late = 0, duplicate = 0, conflict = 0, drop = 0;
        for (var member : r.members.values())  {
            var input = member.input;
            on += input.onTime;
            missing += input.missing;
            late += input.late;
            duplicate += input.duplicate;
            conflict += input.conflict;
            drop += input.dropped;
        }
        long[] counters =  {
            r.fullCount, r.deltaCount, r.fallbackCount, r.resyncCount, on, missing, late, duplicate, conflict, drop, r.commands.size(), txBytes.sum(), rxBytes.sum(), txPackets.sum(), rxPackets.sum(), invalid.sum(), r.server.transport.dropped.sum()
        };
        long now = System.nanoTime();
        Sample sample = new Sample((now - epoch) / 1e9, r.id, r.match, r.server.cfg.mode().name(), r.world.tick, now - loopStarted, encode, send, lag, counters);
        if (!open || !samples.offer(sample)) droppedSamples.increment();
    }
    void writeLoop()  {
        try  {
            out.println("elapsed_s,room,match,mode,tick,tick_us,encode_us,send_us,overrun,full,delta,fallback,resync,input_ontime,input_missing,input_late,input_duplicate,input_conflict,input_drop,command_depth,tx_bytes,rx_bytes,tx_packets,rx_packets,invalid,udp_drop,heap_bytes,gc_count,gc_ms,process_cpu_ns,schedule_lag_us,metrics_drop");
            while (open || !samples.isEmpty())  {
                Sample sample = samples.poll(100, TimeUnit.MILLISECONDS);
                if (sample == null) continue;
                long gc = 0, gcMs = 0;
                for (var collector : ManagementFactory.getGarbageCollectorMXBeans())  {
                    gc += Math.max(0, collector.getCollectionCount());
                    gcMs += Math.max(0, collector.getCollectionTime());
                }
                long cpu = ManagementFactory.getOperatingSystemMXBean() instanceof com.sun.management.OperatingSystemMXBean os ? os.getProcessCpuTime() : -1;
                StringBuilder row = new StringBuilder(String.format(java.util.Locale.ROOT, "%.6f,%d,%d,%s,%d,%d,%d,%d,%d", sample.elapsed, sample.room, sample.match, sample.mode, sample.tick, sample.duration / 1000, sample.encode / 1000, sample.send / 1000, sample.lag + sample.duration > 33_333_333L ? 1 : 0));
                for (long value : sample.counters) row.append(',').append(value);
                row.append(',').append(ManagementFactory.getMemoryMXBean().getHeapMemoryUsage().getUsed()) .append(',').append(gc).append(',').append(gcMs).append(',').append(cpu) .append(',').append(sample.lag / 1000).append(',').append(droppedSamples.sum());
                out.println(row);
                if (sample.tick % 30 == 0 && out.checkError()) throw new IOException("Cannot write metrics CSV");
            }
        }
        catch (InterruptedException e)  {
            Thread.currentThread().interrupt();
        }
        catch (IOException e)  {
            System.err.println("metrics: " + e.getMessage());
        }
        finally  {
            open = false;
            out.close();
        }
    }
    public void close()  {
        open = false;
        try  {
            writer.join(5000);
            if (writer.isAlive())  {
                writer.interrupt();
                writer.join(1000);
            }
            if (writer.isAlive()) System.err.println("Metrics writer did not drain before shutdown");
        }
        catch (InterruptedException e)  {
            Thread.currentThread().interrupt();
        }
    }
}
