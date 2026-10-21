using System.IO.Compression;
using System.Security.Cryptography;
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
    readonly HttpClient http;
    readonly string root;
    readonly bool allowLoopbackHttp;
    string StatePath => Path.Combine(root, "current.json");

    public Updater(string installRoot, bool allowLoopbackHttp = false)
    {
        root = Path.GetFullPath(installRoot);
        this.allowLoopbackHttp = allowLoopbackHttp;
        http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TankBattleLauncher/1.0");
    }

    void ValidateUrl(Uri uri)
    {
        if (!uri.IsAbsoluteUri || !string.IsNullOrEmpty(uri.UserInfo) ||
            (uri.Scheme != "https" && !(allowLoopbackHttp && uri.Scheme == "http" && uri.IsLoopback)))
            throw new InvalidDataException("Địa chỉ cập nhật phải dùng HTTPS (HTTP chỉ dành cho thử nghiệm localhost).");
    }

    async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken ct)
    {
        for (int i = 0; i < 6; i++)
        {
            ValidateUrl(uri);
            var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location == null) throw new InvalidDataException("Chuyển hướng cập nhật không hợp lệ.");
                uri = new Uri(uri, location);
                continue;
            }
            try { response.EnsureSuccessStatusCode(); return response; }
            catch { response.Dispose(); throw; }
        }
        throw new InvalidDataException("Quá nhiều chuyển hướng cập nhật.");
    }

    public async Task<ReleaseManifest> CheckAsync(Uri manifestUrl, CancellationToken ct = default)
    {
        using var response = await GetAsync(manifestUrl, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        byte[] block = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(block, ct)) != 0)
        {
            if (buffer.Length + count > 65536) throw new InvalidDataException("Manifest quá lớn.");
            buffer.Write(block, 0, count);
        }
        var manifest = JsonSerializer.Deserialize<ReleaseManifest>(buffer.ToArray(), JsonOptions)
            ?? throw new InvalidDataException("Manifest rỗng.");
        ValidateManifest(manifest);
        return manifest;
    }

    void ValidateManifest(ReleaseManifest m)
    {
        if (m.SchemaVersion != 1 || !Regex.IsMatch(m.Version ?? "", @"^\d{1,6}\.\d{1,6}\.\d{1,6}$") ||
            !Regex.IsMatch(m.Sha256 ?? "", "^[a-fA-F0-9]{64}$") || m.Size <= 0 || m.Size > MaxPackageSize)
            throw new InvalidDataException("Manifest không hợp lệ hoặc launcher chưa hỗ trợ định dạng này.");
        ValidateUrl(new Uri(m.PackageUrl, UriKind.Absolute));
    }

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
        ValidateManifest(manifest);
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
            using (var response = await GetAsync(new Uri(manifest.PackageUrl), ct))
            {
                if (response.Content.Headers.ContentLength is long size && size != manifest.Size)
                    throw new InvalidDataException("Dung lượng gói tải không khớp manifest.");
                await using var input = await response.Content.ReadAsStreamAsync(ct);
                await using var output = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] block = new byte[81920];
                long total = 0;
                int count;
                while ((count = await input.ReadAsync(block, ct)) > 0)
                {
                    total += count;
                    if (total > manifest.Size) throw new InvalidDataException("Gói tải vượt dung lượng khai báo.");
                    sha.AppendData(block, 0, count);
                    await output.WriteAsync(block.AsMemory(0, count), ct);
                    progress?.Report(new("Đang tải bản " + manifest.Version, (int)(total * 80 / manifest.Size)));
                }
                if (total != manifest.Size || !Convert.ToHexString(sha.GetHashAndReset()).Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Gói cập nhật bị thiếu hoặc sai SHA-256. Bản cũ được giữ nguyên.");
            }
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
            progress?.Report(new("Đã cập nhật bản " + manifest.Version, 100));
        }
        finally
        {
            if (File.Exists(archive)) File.Delete(archive);
            if (File.Exists(stateTemp)) File.Delete(stateTemp);
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
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

    public void Dispose() => http.Dispose();
}
