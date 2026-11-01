using System.IO.Compression;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TankLauncher;

public sealed record ReleaseManifest(int SchemaVersion, string Version, string PackageUrl, string Sha256, long Size);
public sealed record InstalledRelease(string Version, string Sha256, string Directory);
public sealed record UpdateProgress(string Message, int Percent = 0);

public sealed class Updater : IDisposable
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    const long MaxPackageSize = 4L * 1024 * 1024 * 1024;
    const long MaxExpandedSize = 8L * 1024 * 1024 * 1024;
    readonly UpdateTransport transport;
    readonly string root;
    string StatePath => Path.Combine(root, "current.json");

    public Updater(string installRoot, bool allowLoopbackHttp = false)
    {
        root = Path.GetFullPath(installRoot);
        transport = new UpdateTransport(allowLoopbackHttp);
    }

    public Task<ReleaseManifest> CheckAsync(Uri manifestUrl, CancellationToken ct = default) =>
        transport.CheckAsync(manifestUrl, MaxPackageSize, ct);

    string ReleaseDirectory(string version, string hash) => version + "-" + hash.ToLowerInvariant();

    public InstalledRelease? Current()
    {
        if (!File.Exists(StatePath)) return null;
        var state = JsonSerializer.Deserialize<InstalledRelease>(File.ReadAllText(StatePath), JsonOptions);
        if (state == null || state.Version == null || state.Sha256 == null || !Regex.IsMatch(state.Version, @"^\d{1,6}\.\d{1,6}\.\d{1,6}$") ||
            !Regex.IsMatch(state.Sha256, "^[a-fA-F0-9]{64}$") || state.Directory != ReleaseDirectory(state.Version, state.Sha256))
            throw new InvalidDataException("Thông tin bản cài bị hỏng. Hãy kiểm tra cập nhật để cài lại.");
        return IsGameDirectory(Path.Combine(root, "versions", state.Directory)) ? state : null;
    }

    static bool IsGameDirectory(string directory) => File.Exists(Path.Combine(directory, "TankBattle.exe")) &&
        File.Exists(Path.Combine(directory, "UnityPlayer.dll")) && Directory.Exists(Path.Combine(directory, "TankBattle_Data"));

    public string GamePath(InstalledRelease release)
    {
        if (release.Directory != ReleaseDirectory(release.Version, release.Sha256) ||
            !Regex.IsMatch(release.Directory, @"^\d{1,6}\.\d{1,6}\.\d{1,6}-[a-f0-9]{64}$"))
            throw new InvalidDataException("Đường dẫn bản cài không hợp lệ.");
        return Path.Combine(root, "versions", release.Directory, "TankBattle.exe");
    }

    public async Task InstallAsync(ReleaseManifest manifest, IProgress<UpdateProgress>? progress = null, CancellationToken ct = default)
    {
        transport.ValidateManifest(manifest, MaxPackageSize);
        Directory.CreateDirectory(root);
        // Also protects command-line callers and other launcher processes using this installation.
        using var installLock = new FileStream(Path.Combine(root, "update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        string versions = Path.Combine(root, "versions");
        Directory.CreateDirectory(versions);
        string staging = Path.Combine(versions, ".staging-" + Guid.NewGuid().ToString("N"));
        string archive = Path.Combine(root, "download-" + Guid.NewGuid().ToString("N") + ".zip");
        string stateTemp = Path.Combine(root, "current-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await transport.DownloadAsync(manifest, archive, MaxPackageSize, progress, ct);
            progress?.Report(new("Đang giải nén và kiểm tra...", 85));
            Directory.CreateDirectory(staging);
            await ExtractAsync(archive, staging, ct);
            if (!IsGameDirectory(staging)) throw new InvalidDataException("Gói cập nhật thiếu TankBattle.exe, UnityPlayer.dll hoặc TankBattle_Data.");
            ct.ThrowIfCancellationRequested();
            string directory = ReleaseDirectory(manifest.Version, manifest.Sha256);
            string destination = Path.Combine(versions, directory);
            // Never overwrite an installed release, including one that might be running.
            if (!Directory.Exists(destination)) Directory.Move(staging, destination);
            else if (!IsGameDirectory(destination)) throw new InvalidDataException("Bản cài cùng phiên bản bị hỏng; cần đổi số phiên bản phát hành.");
            var state = new InstalledRelease(manifest.Version, manifest.Sha256.ToLowerInvariant(), directory);
            await File.WriteAllTextAsync(stateTemp, JsonSerializer.Serialize(state, JsonOptions), ct);
            File.Move(stateTemp, StatePath, true);
            // This is the commit point. Cleanup must never run before the new
            // release is installed AND selected, and must not invalidate success.
            PruneOldVersions(state);
            progress?.Report(new("Đã cập nhật bản " + manifest.Version, 100));
        }
        finally
        {
            if (File.Exists(archive)) File.Delete(archive);
            if (File.Exists(stateTemp)) File.Delete(stateTemp);
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    public int CleanupOldVersions()
    {
        // Retry deferred cleanup even when checking for updates is offline.
        // Use the same lock as installation so another launcher cannot switch
        // current.json while this instance is deciding which release to retain.
        try
        {
            if (!PlainDirectory(root)) return 0;
            using var installLock = new FileStream(Path.Combine(root, "update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var current = Current();
            return current == null ? 0 : PruneOldVersions(current);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or System.Security.SecurityException)
        { return 0; }
    }

    static bool PlainDirectory(string path) => Directory.Exists(path) &&
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;

    int PruneOldVersions(InstalledRelease current)
    {
        int deleted = 0;
        try
        {
            string versions = Path.GetFullPath(Path.Combine(root, "versions"));
            if (!PlainDirectory(root) || !PlainDirectory(versions)) return 0;
            foreach (string candidate in Directory.EnumerateDirectories(versions))
            {
                string path = Path.GetFullPath(candidate), name = Path.GetFileName(path);
                // Only managed releases immediately inside this installation.
                // Ignore unknown folders, links, staging and the selected release.
                if (!string.Equals(Path.GetDirectoryName(path), versions, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, current.Directory, StringComparison.OrdinalIgnoreCase) ||
                    !Regex.IsMatch(name, @"^\d{1,6}\.\d{1,6}\.\d{1,6}-[a-fA-F0-9]{64}$")) continue;
                if (TryDeleteRelease(path)) deleted++;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return deleted;
    }

    static bool RunningRelease(string directory)
    {
        string executable = Path.Combine(directory, "TankBattle.exe");
        foreach (var process in Process.GetProcessesByName("TankBattle"))
        {
            using (process)
            {
                try
                {
                    if (string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch (InvalidOperationException) { } // Process already exited.
                catch (System.ComponentModel.Win32Exception) { return true; } // Cannot safely inspect it.
            }
        }
        return false;
    }

    static bool TryDeleteRelease(string directory)
    {
        var handles = new List<FileStream>();
        try
        {
            if (!PlainDirectory(directory) || RunningRelease(directory)) return false;
            var folders = new Stack<string>(); folders.Push(directory);
            var files = new List<string>();
            while (folders.Count > 0)
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(folders.Pop()))
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) return false;
                    if ((attributes & FileAttributes.Directory) != 0) folders.Push(entry); else files.Add(entry);
                }
            }
            // Check every file BEFORE deleting anything. Hold handles that allow
            // deletion but reject new readers/writers until cleanup is complete.
            // A running/locked old build is left whole for a subsequent retry.
            foreach (string file in files) handles.Add(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Delete));
            Directory.Delete(directory, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { return false; }
        finally { foreach (var handle in handles) handle.Dispose(); }
    }

    static async Task ExtractAsync(string archive, string staging, CancellationToken ct)
    {
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count > 100000) throw new InvalidDataException("Gói cập nhật chứa quá nhiều file.");
        long expanded = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();
            string name = entry.FullName.Replace('\\', '/');
            string relative = name.TrimEnd('/');
            var parts = relative.Split('/');
            if (parts.Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith(' ') || p.EndsWith('.') ||
                p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Regex.IsMatch(p, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase)) ||
                ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || !seen.Add(relative))
                throw new InvalidDataException("Gói ZIP chứa đường dẫn không an toàn hoặc trùng lặp.");
            expanded = checked(expanded + entry.Length);
            if (expanded > MaxExpandedSize) throw new InvalidDataException("Gói giải nén quá lớn.");
            string path = Path.GetFullPath(Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Đường dẫn ZIP nằm ngoài thư mục cài.");
            if (name.EndsWith('/')) { Directory.CreateDirectory(path); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var input = entry.Open();
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, ct);
        }
    }

    public void Dispose() => transport.Dispose();
}
