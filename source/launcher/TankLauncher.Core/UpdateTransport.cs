using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TankLauncher;

public sealed record LauncherConfig(string ManifestUrl, bool AllowLoopbackHttp = false, string? LauncherManifestUrl = null)
{
    public Uri SelfUpdateUri => string.IsNullOrWhiteSpace(LauncherManifestUrl)
        ? new Uri(new Uri(ManifestUrl, UriKind.Absolute), "launcher-manifest.json")
        : new Uri(LauncherManifestUrl, UriKind.Absolute);
}

public static class LauncherVersion
{
    public static string Current => (Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 2, 0)).ToString(3);

    public static Version Parse(string value)
    {
        if (!Regex.IsMatch(value ?? "", @"^\d{1,6}\.\d{1,6}\.\d{1,6}$"))
            throw new InvalidDataException("Phiên bản phải có dạng MAJOR.MINOR.PATCH.");
        return Version.Parse(value!);
    }
}

// Shared by game and launcher updates so redirects and integrity checks use the same policy.
public sealed class UpdateTransport : IDisposable
{
    readonly HttpClient http;
    readonly bool allowLoopbackHttp;

    public UpdateTransport(bool allowLoopbackHttp = false)
    {
        this.allowLoopbackHttp = allowLoopbackHttp;
        http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TankBattleLauncher/" + LauncherVersion.Current);
    }

    public void ValidateUrl(Uri uri)
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
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or
                HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
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

    public void ValidateManifest(ReleaseManifest m, long maxSize)
    {
        if (m.SchemaVersion != 1 || !Regex.IsMatch(m.Sha256 ?? "", "^[a-fA-F0-9]{64}$") || m.Size <= 0 || m.Size > maxSize)
            throw new InvalidDataException("Manifest không hợp lệ hoặc launcher chưa hỗ trợ định dạng này.");
        LauncherVersion.Parse(m.Version);
        ValidateUrl(new Uri(m.PackageUrl, UriKind.Absolute));
    }

    public async Task<ReleaseManifest> CheckAsync(Uri uri, long maxSize, CancellationToken ct = default)
    {
        using var response = await GetAsync(uri, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        byte[] block = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(block, ct)) != 0)
        {
            if (buffer.Length + count > 65536) throw new InvalidDataException("Manifest quá lớn.");
            buffer.Write(block, 0, count);
        }
        var manifest = JsonSerializer.Deserialize<ReleaseManifest>(buffer.ToArray(), Updater.JsonOptions)
            ?? throw new InvalidDataException("Manifest rỗng.");
        ValidateManifest(manifest, maxSize);
        return manifest;
    }

    public async Task DownloadAsync(ReleaseManifest manifest, string destination, long maxSize,
        IProgress<UpdateProgress>? progress = null, CancellationToken ct = default)
    {
        ValidateManifest(manifest, maxSize);
        using var response = await GetAsync(new Uri(manifest.PackageUrl), ct);
        if (response.Content.Headers.ContentLength is long size && size != manifest.Size)
            throw new InvalidDataException("Dung lượng gói tải không khớp manifest.");
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
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

    public void Dispose() => http.Dispose();
}
