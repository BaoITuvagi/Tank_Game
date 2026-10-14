using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace Tank  {
    // One writer owns the stream. Enqueue never performs socket I/O on Unity's main thread.
    public sealed class TcpSendQueue : IDisposable  {
        readonly Stream stream;
        readonly Action<string> failed;
        readonly ConcurrentQueue<byte[]> frames = new ConcurrentQueue<byte[]>();
        readonly SemaphoreSlim available = new SemaphoreSlim(0);
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        readonly int capacity, timeoutMs;
        readonly object gate = new object();
        int depth, disposed;
        public int Depth  {
            get  {
                return Volatile.Read(ref depth);
            }
        }
        public TcpSendQueue(Stream stream, Action<string> failed, int capacity = 128, int timeoutMs = 5000)  {
            if (stream == null || capacity < 1 || timeoutMs < 1) throw new ArgumentException("TCP writer settings");
            this.stream = stream;
            this.failed = failed;
            this.capacity = capacity;
            this.timeoutMs = timeoutMs;
            Task.Run(WriteLoop);
        }
        public bool TryEnqueue(byte[] frame)  {
            lock (gate) {
                if (disposed != 0 || depth >= capacity) return false;
                Interlocked.Increment(ref depth);
                frames.Enqueue(frame);
                available.Release();
                return true;
            }
        }
        async Task WriteLoop()  {
            try  {
                while (!cancellation.IsCancellationRequested)  {
                    await available.WaitAsync(cancellation.Token).ConfigureAwait(false);
                    byte[] frame;
                    if (!frames.TryDequeue(out frame)) continue;
                    Interlocked.Decrement(ref depth);
                    Task write = stream.WriteAsync(frame, 0, frame.Length, cancellation.Token);
                    _ = write.ContinueWith(t =>  {
                        var ignored = t.Exception;
                    }, TaskContinuationOptions.OnlyOnFaulted);
                    using (var timer = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token)) {
                        Task deadline = Task.Delay(timeoutMs, timer.Token);
                        if (await Task.WhenAny(write, deadline).ConfigureAwait(false) != write)  {
                            cancellation.Token.ThrowIfCancellationRequested();
                            throw new IOException("Gửi TCP quá hạn");
                        }
                        timer.Cancel();
                        await write.ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e)  {
                if (!cancellation.IsCancellationRequested)  {
                    // Closing the stream also releases a pending socket write on timeout.
                    try { stream.Dispose(); } catch { }
                    failed?.Invoke(e.Message);
                }
            }
            finally  {
                lock (gate) {
                    disposed = 1;
                    byte[] ignored;
                    while (frames.TryDequeue(out ignored)) Interlocked.Decrement(ref depth);
                    available.Dispose();
                    cancellation.Dispose();
                }
            }
        }
        public void Dispose()  {
            lock (gate) {
                if (disposed == 0) { disposed = 1; cancellation.Cancel(); }
            }
        }
    }
}
