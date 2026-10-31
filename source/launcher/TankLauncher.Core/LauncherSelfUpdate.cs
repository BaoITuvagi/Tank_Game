using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TankLauncher;

public enum LauncherStartup { Continue, Busy, Recover }

// Only the helper replaces the executable. The UI process never replaces its own image.
[SupportedOSPlatform("windows")]
public sealed class LauncherSelfUpdate : IDisposable
{
    public const string UiMutexName = @"Local\TankBattleLauncher";
    public const long MaxSize = 512L * 1024 * 1024;
    readonly UpdateTransport transport;
    readonly string target;
    readonly string updateRoot;
    readonly string workspace;
    string StatePath => Path.Combine(workspace, "transaction.json");
    string Candidate => Path.Combine(workspace, "candidate.exe");
    string Helper => Path.Combine(workspace, "helper.exe");
    string ReadyPath => Path.Combine(workspace, "ready");
    string ErrorPath => Path.Combine(workspace, "last-error.txt");
    string Backup(Transaction t) => target + ".update-" + t.Token + ".bak";
    string Staged(Transaction t) => target + ".update-" + t.Token + ".new";
    static string EventName(Transaction t) => @"Local\TankBattleLauncherHelper-" + t.Token;

    sealed record Transaction(int SchemaVersion, string Target, string Token, string Sha256, string OldSha256,
        int ParentId, long ParentStartTicks, bool RecoveryOnly = false);

    public LauncherSelfUpdate(string executable, string updateRoot, bool allowLoopbackHttp = false)
    {
        target = Path.GetFullPath(executable);
        if (!Path.GetFileName(target).Equals("Launcher.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Tên file launcher phải là Launcher.exe.");
        this.updateRoot = Path.GetFullPath(updateRoot);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target.ToUpperInvariant())));
        workspace = Path.Combine(this.updateRoot, key);
        transport = new UpdateTransport(allowLoopbackHttp);
    }

    static void PlainPath(string path)
    {
        for (string? p = Path.GetFullPath(path); p != null; p = Path.GetDirectoryName(p))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Không cập nhật launcher qua liên kết thư mục/file.");
    }

    FileStream Gate()
    {
        PlainPath(workspace);
        Directory.CreateDirectory(workspace);
        PlainPath(Path.Combine(workspace, "transaction.lock"));
        return new FileStream(Path.Combine(workspace, "transaction.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    static string Hash(string path)
    {
        PlainPath(path);
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    static bool SameHash(string path, string hash) => File.Exists(path) && Hash(path).Equals(hash, StringComparison.OrdinalIgnoreCase);

    static void WriteAtomic(string path, string content)
    {
        PlainPath(path);
        string temp = path + ".tmp";
        PlainPath(temp);
        using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            output.Write(Encoding.UTF8.GetBytes(content));
            output.Flush(true);
        }
        File.Move(temp, path, true);
    }

    void Save(Transaction t) => WriteAtomic(StatePath, JsonSerializer.Serialize(t, Updater.JsonOptions));

    Transaction Read()
    {
        PlainPath(StatePath);
        var t = JsonSerializer.Deserialize<Transaction>(File.ReadAllText(StatePath), Updater.JsonOptions)
            ?? throw new InvalidDataException("Trạng thái cập nhật launcher rỗng.");
        if (t.SchemaVersion != 1 || t.Target != target || !Guid.TryParseExact(t.Token, "N", out _) ||
            !Regex.IsMatch(t.Sha256 ?? "", "^[a-fA-F0-9]{64}$") || !Regex.IsMatch(t.OldSha256 ?? "", "^[a-fA-F0-9]{64}$") ||
            t.ParentId <= 0 || t.ParentStartTicks <= 0)
            throw new InvalidDataException("Trạng thái cập nhật launcher không hợp lệ.");
        return t;
    }

    bool IsReady(Transaction t)
    {
        PlainPath(ReadyPath);
        return File.Exists(ReadyPath) && File.ReadAllText(ReadyPath) == t.Token;
    }

    // This also runs on ordinary startup to recover a helper interrupted by logout/crash.
    public LauncherStartup InspectStartup(string? readyToken = null)
    {
        if (!Directory.Exists(workspace)) return LauncherStartup.Continue;
        FileStream gate;
        try { gate = Gate(); }
        catch (IOException)
        {
            if (readyToken != null && File.Exists(StatePath))
            {
                var pending = Read();
                if (pending.Token == readyToken && (SameHash(target, pending.OldSha256) ||
                    (!pending.RecoveryOnly && SameHash(target, pending.Sha256))))
                    return LauncherStartup.Continue;
            }
            return LauncherStartup.Busy;
        }
        using (gate)
        {
            if (!File.Exists(StatePath))
            {
                Delete(Helper);
                Delete(Candidate);
                Delete(ReadyPath);
                return LauncherStartup.Continue;
            }
            var t = Read();
            if (SameHash(target, t.OldSha256) || (IsReady(t) && SameHash(target, t.Sha256)))
            {
                Cleanup(t);
                return LauncherStartup.Continue;
            }
            if (SameHash(Backup(t), t.OldSha256) && SameHash(target, t.Sha256)) return LauncherStartup.Recover;
            throw new IOException("Cập nhật launcher bị gián đoạn; giữ các file dự phòng để khôi phục thủ công.");
        }
    }

    public Task<ReleaseManifest> CheckAsync(Uri manifestUri, CancellationToken ct = default) =>
        transport.CheckAsync(manifestUri, MaxSize, ct);

    public static bool IsNewer(string offered, string current) => LauncherVersion.Parse(offered) > LauncherVersion.Parse(current);

    public async Task PrepareAsync(ReleaseManifest manifest, IProgress<UpdateProgress>? progress = null, CancellationToken ct = default)
    {
        if (InspectStartup() != LauncherStartup.Continue) throw new IOException("Một cập nhật launcher đang chờ hoàn tất.");
        using var gate = Gate();
        if (File.Exists(StatePath)) throw new IOException("Một cập nhật launcher đang chờ hoàn tất.");
        PlainPath(target);
        // Probe the actual destination before asking the UI to exit. No elevation is attempted.
        string probe = target + ".probe-" + Guid.NewGuid().ToString("N");
        using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        File.Delete(probe);
        try
        {
            Delete(Candidate);
            Delete(Helper);
            Delete(ReadyPath);
            await transport.DownloadAsync(manifest, Candidate, MaxSize, progress, ct);
            var info = FileVersionInfo.GetVersionInfo(Candidate);
            if (new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart) != LauncherVersion.Parse(manifest.Version))
                throw new InvalidDataException("Phiên bản file launcher không khớp manifest.");
            ct.ThrowIfCancellationRequested();
            File.Copy(target, Helper);
            using var parent = Process.GetCurrentProcess();
            Save(new(1, target, Guid.NewGuid().ToString("N"), manifest.Sha256, Hash(target), parent.Id, parent.StartTime.ToUniversalTime().Ticks));
        }
        catch
        {
            Delete(Candidate);
            Delete(Helper);
            throw;
        }
    }

    public void BeginHandoff(bool recoveryOnly = false)
    {
        Transaction t;
        using (var gate = Gate())
        {
            t = Read();
            using var parent = Process.GetCurrentProcess();
            t = t with { ParentId = parent.Id, ParentStartTicks = parent.StartTime.ToUniversalTime().Ticks, RecoveryOnly = recoveryOnly };
            Save(t);
        }
        using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, EventName(t));
        using var helper = Start(Helper, "--apply-launcher-update", target, updateRoot);
        if (!ready.WaitOne(TimeSpan.FromSeconds(15)))
            throw new IOException("Tiến trình cập nhật launcher không khởi động được. Bản hiện tại được giữ nguyên.");
    }

    public void Acknowledge(string token)
    {
        var t = Read();
        if (t.RecoveryOnly || t.Token != token || !SameHash(target, t.Sha256))
            throw new InvalidDataException("Xác nhận cập nhật launcher không hợp lệ.");
        WriteAtomic(ReadyPath, token);
    }

    public string? LastError()
    {
        PlainPath(ErrorPath);
        if (!File.Exists(ErrorPath)) return null;
        string error = File.ReadAllText(ErrorPath);
        Delete(ErrorPath);
        return error.Split(['\r', '\n'], 2)[0];
    }

    static Process Start(string path, params string[] args)
    {
        var info = new ProcessStartInfo(path) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(path)! };
        if (args.FirstOrDefault() == "--apply-launcher-update") info.WindowStyle = ProcessWindowStyle.Hidden;
        foreach (string arg in args) info.ArgumentList.Add(arg);
        return Process.Start(info) ?? throw new IOException("Không mở được tiến trình launcher.");
    }

    static bool Acquire(Mutex mutex, TimeSpan timeout)
    {
        try { return mutex.WaitOne(timeout); }
        catch (AbandonedMutexException) { return true; }
    }

    void WithUiLock(Action action)
    {
        using var mutex = new Mutex(false, UiMutexName);
        if (!Acquire(mutex, TimeSpan.FromSeconds(30))) throw new IOException("Launcher khác chưa đóng.");
        try { action(); }
        finally { mutex.ReleaseMutex(); }
    }

    void Restore(Transaction t)
    {
        if (!SameHash(Backup(t), t.OldSha256)) throw new IOException("Bản dự phòng launcher không hợp lệ.");
        if (SameHash(target, t.OldSha256)) return; // A partial/failed replace may already have left the old image in place.
        if (File.Exists(target) && !SameHash(target, t.Sha256)) throw new IOException("File launcher đã thay đổi ngoài giao dịch cập nhật.");
        // Antivirus may briefly hold a file after the child exits.
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                if (File.Exists(target)) File.Replace(Backup(t), target, null, ignoreMetadataErrors: true);
                else File.Move(Backup(t), target);
                return;
            }
            catch (IOException) when (attempt < 49) { Thread.Sleep(100); }
        }
    }

    public int RunHelper()
    {
        using var gate = Gate();
        var t = Read();
        Process? child = null;
        bool parentExited = false;
        try
        {
            using var parent = Process.GetProcessById(t.ParentId);
            if (parent.StartTime.ToUniversalTime().Ticks != t.ParentStartTicks) throw new IOException("Tiến trình launcher gốc đã thay đổi.");
            using var ready = EventWaitHandle.OpenExisting(EventName(t));
            ready.Set();
            if (!parent.WaitForExit(30000)) throw new IOException("Launcher gốc chưa đóng; chưa áp dụng cập nhật.");
            parentExited = true;
            if (t.RecoveryOnly)
            {
                WithUiLock(() => Restore(t));
                WriteAtomic(ErrorPath, "Đã khôi phục launcher sau cập nhật bị gián đoạn.");
                using var restored = Start(target, "--launcher-update-restored", t.Token);
                return 0;
            }
            WithUiLock(() =>
            {
                if (!SameHash(target, t.OldSha256) || !SameHash(Candidate, t.Sha256))
                    throw new InvalidDataException("File launcher thay đổi sau khi kiểm tra.");
                PlainPath(Staged(t));
                PlainPath(Backup(t));
                File.Copy(Candidate, Staged(t), false);
                if (!SameHash(Staged(t), t.Sha256)) throw new InvalidDataException("File launcher staging sai SHA-256.");
                // Same-directory staging inherits destination-directory permissions. Restricted
                // accounts may lack WRITE_DAC for merging optional metadata, despite having
                // permission to replace the file; that must not invalidate an otherwise safe swap.
                File.Replace(Staged(t), target, Backup(t), ignoreMetadataErrors: true);
            });
            child = Start(target, "--launcher-update-ready", t.Token);
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < TimeSpan.FromSeconds(30))
            {
                if (IsReady(t))
                {
                    // Readiness is the commit point. A cleanup failure must never trigger rollback.
                    TryCleanup(t);
                    return 0;
                }
                if (child.HasExited) break;
                Thread.Sleep(100);
            }
            throw new IOException("Launcher mới không xác nhận khởi động trong 30 giây.");
        }
        catch (Exception e)
        {
            try
            {
                if (child != null && !child.HasExited)
                {
                    child.Kill(); // Never kill a process tree: it could include the player's game.
                    if (!child.WaitForExit(5000)) throw new IOException("Không đóng được launcher mới.");
                }
                if (parentExited)
                {
                    if (File.Exists(Backup(t))) WithUiLock(() => Restore(t));
                    if (!SameHash(target, t.OldSha256)) throw new IOException("Chưa khôi phục được launcher; giữ bản dự phòng.");
                    WriteAtomic(ErrorPath, "Cập nhật launcher thất bại; đã giữ/khôi phục bản cũ. " + e.Message + "\n" + e);
                    using var restored = Start(target, "--launcher-update-restored", t.Token);
                }
                else WriteAtomic(ErrorPath, e.Message + "\n" + e);
            }
            catch (Exception recoveryError) { WriteAtomic(ErrorPath, e.Message + " " + recoveryError.Message); }
            return 1;
        }
        finally { child?.Dispose(); }
    }

    static void Delete(string path)
    {
        PlainPath(path);
        if (File.Exists(path)) File.Delete(path);
    }

    void Cleanup(Transaction t)
    {
        Delete(Backup(t));
        Delete(Staged(t));
        Delete(Candidate);
        // The helper image stays locked until its process exits; retry on the next normal startup.
        try { Delete(Helper); } catch (IOException) { }
        Delete(StatePath);
        Delete(ReadyPath);
    }

    void TryCleanup(Transaction t)
    {
        try { Cleanup(t); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public void Dispose() => transport.Dispose();
}
