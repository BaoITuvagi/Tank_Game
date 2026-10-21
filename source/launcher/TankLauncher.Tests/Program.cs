using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using TankLauncher;

// Integration tests use real HTTP, ZIPs and filesystem state, without NuGet test dependencies.
string testRoot = Path.Combine(Path.GetFullPath(args[0]), Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testRoot);
using var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
int port = ((IPEndPoint)listener.LocalEndpoint).Port;
string baseUrl = $"http://127.0.0.1:{port}/";
var routes = new System.Collections.Concurrent.ConcurrentDictionary<string, byte[]>();
using var shutdown = new CancellationTokenSource();
var server = Task.Run(async () =>
{
    while (!shutdown.IsCancellationRequested)
    {
        TcpClient client;
        try { client = await listener.AcceptTcpClientAsync(shutdown.Token); }
        catch (Exception) when (shutdown.IsCancellationRequested) { break; }
        using (client)
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, leaveOpen: true);
            string path = (await reader.ReadLineAsync())!.Split(' ')[1];
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
            bool found = routes.TryGetValue(path, out var bytes);
            bytes ??= [];
            string headers = path == "/redirect"
                ? $"HTTP/1.1 302 Found\r\nLocation: {baseUrl}manifest.json\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                : $"HTTP/1.1 {(found ? "200 OK" : "404 Not Found")}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(headers));
            if (path != "/redirect") await stream.WriteAsync(bytes);
        }
    }
});
int passed = 0;
void Assert(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
async Task Reject(Func<Task> action, string name)
{
    try { await action(); } catch (Exception e) when (e is IOException or InvalidDataException or HttpRequestException or OperationCanceledException or JsonException) { Assert(true, name); return; }
    throw new Exception("FAIL: " + name + " accepted invalid input");
}
byte[] Package(string marker, string? extra = null)
{
    using var memory = new MemoryStream();
    using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        foreach (string name in new[] { "TankBattle.exe", "UnityPlayer.dll", "TankBattle_Data/data.bin" }.Concat(extra == null ? [] : new[] { extra }))
        { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(marker); }
    return memory.ToArray();
}
ReleaseManifest Publish(string version, byte[] package)
{
    routes["/game.zip"] = package;
    var manifest = new ReleaseManifest(1, version, baseUrl + "game.zip", Convert.ToHexString(SHA256.HashData(package)), package.LongLength);
    routes["/manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest, Updater.JsonOptions);
    return manifest;
}
try
{
    using var updater = new Updater(Path.Combine(testRoot, "install"), true);
    using var secure = new Updater(Path.Combine(testRoot, "secure"));
    Assert(updater.Current() == null, "fresh install has no current version");
    var first = Publish("1.0.0", Package("first"));
    await Reject(() => secure.CheckAsync(new Uri(baseUrl + "manifest.json")), "HTTP disabled by default");
    var checkedManifest = await updater.CheckAsync(new Uri(baseUrl + "redirect"));
    Assert(checkedManifest == first, "read manifest through HTTP redirect");
    await updater.InstallAsync(checkedManifest);
    var original = updater.Current()!;
    Assert(original.Version == "1.0.0" && File.ReadAllText(updater.GamePath(original)) == "first", "first install with SHA-256 and expected layout");
    var next = Publish("1.0.1", Package("second"));
    await Reject(() => updater.InstallAsync(next with { Sha256 = new string('0', 64) }), "bad checksum rejected");
    Assert(updater.Current() == original, "bad checksum preserves current release");
    await Reject(() => updater.InstallAsync(next with { Size = next.Size + 1 }), "wrong download length rejected");
    foreach (string path in new[] { "../escaped.txt", "/escaped.txt", "TankBattle_Data/../../escaped.txt", "TankBattle.exe:evil", "CON.txt", "tankbattle.exe" })
    {
        var malicious = Publish("1.0.2", Package("malicious", path));
        await Reject(() => updater.InstallAsync(malicious), "unsafe ZIP rejected: " + path);
        Assert(updater.Current() == original, "unsafe ZIP preserves current release");
    }
    var corrupt = Publish("1.0.2", [1, 2, 3, 4]);
    await Reject(() => updater.InstallAsync(corrupt), "invalid ZIP rejected after checksum succeeds");
    using var canceled = new CancellationTokenSource();
    canceled.Cancel();
    next = Publish("1.0.1", Package("second"));
    await Reject(() => updater.InstallAsync(next, ct: canceled.Token), "canceled update rejected");
    Assert(updater.Current() == original, "cancellation preserves current release");
    using (var lockFile = new FileStream(Path.Combine(testRoot, "install/update.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        await Reject(() => updater.InstallAsync(next), "concurrent install rejected");
    await updater.InstallAsync(next);
    Assert(updater.Current()!.Version == "1.0.1" && File.ReadAllText(updater.GamePath(updater.Current()!)) == "second", "successful update switches current version");
    Assert(File.ReadAllText(updater.GamePath(original)) == "first", "previous release retained after update");
    await updater.InstallAsync(next);
    Assert(updater.Current()!.Version == "1.0.1", "same release can be installed again safely");
    Assert(!Directory.EnumerateDirectories(Path.Combine(testRoot, "install/versions"), ".staging-*").Any() &&
        !Directory.EnumerateFiles(Path.Combine(testRoot, "install"), "download-*").Any(), "temporary downloads and staging cleaned");
    routes["/manifest.json"] = new byte[65537];
    await Reject(() => updater.CheckAsync(new Uri(baseUrl + "manifest.json")), "oversized manifest rejected");
    if (args.Length > 1)
    {
        string releaseDirectory = Path.GetFullPath(args[1]);
        var real = JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(Path.Combine(releaseDirectory, "manifest.json")), Updater.JsonOptions)!;
        string filename = Path.GetFileName(new Uri(real.PackageUrl).AbsolutePath);
        routes["/game.zip"] = File.ReadAllBytes(Path.Combine(releaseDirectory, filename));
        routes["/manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(real with { PackageUrl = baseUrl + "game.zip" }, Updater.JsonOptions);
        using var realUpdater = new Updater(Path.Combine(testRoot, "real-unity-build"), true);
        await realUpdater.InstallAsync(await realUpdater.CheckAsync(new Uri(baseUrl + "manifest.json")));
        Assert(realUpdater.Current()!.Version == real.Version, "install actual Python-packaged Unity build over HTTP");
        Assert(File.Exists(Path.Combine(Path.GetDirectoryName(realUpdater.GamePath(realUpdater.Current()!))!, "TankBattle_Data/StreamingAssets/map.json")), "actual Unity map survives packaging and installation");
    }
    File.WriteAllText(Path.Combine(testRoot, "install/current.json"), JsonSerializer.Serialize(new InstalledRelease("1.0.0", new string('a', 64), "../escape"), Updater.JsonOptions));
    try { updater.Current(); throw new Exception("unsafe state accepted"); } catch (InvalidDataException) { Assert(true, "unsafe installed state rejected"); }
    Console.WriteLine($"All {passed} launcher checks passed. Artifacts: {testRoot}");
}
finally
{
    shutdown.Cancel();
    listener.Stop();
    await server;
}
