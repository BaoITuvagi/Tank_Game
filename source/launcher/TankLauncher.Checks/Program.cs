using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TankLauncher;

// Dependency-free Windows integration checks. Fixture children use the production coordinator.
internal static class Program
{
    static int passed;
    static readonly JsonSerializerOptions Json = Updater.JsonOptions;
    record Fixture(string Root, string ManifestUrl, string GameUrl, string Behavior);

    static async Task<int> Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--apply-launcher-update")
        {
            using var helper = new LauncherSelfUpdate(args[1], args[2]);
            return helper.RunHelper();
        }
        if (args.FirstOrDefault() == "--hold") { Thread.Sleep(120000); return 0; }
        if (Path.GetFileName(Environment.ProcessPath!).Equals("Launcher.exe", StringComparison.OrdinalIgnoreCase))
            return RunFixture(args);
        if (args.Length is not (3 or 5 or 6)) { Console.Error.WriteLine("Usage: Checks.exe OLD_FIXTURE.exe NEW_FIXTURE.exe OUTPUT_DIRECTORY [OLD_UI.exe NEW_UI.exe [--keep-ui]]"); return 2; }
        string output = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(output);
        try
        {
            using var server = new LocalServer();
            await TransportChecks(server, output);
            await GameChecks(server, output);
            byte[] oldBytes = File.ReadAllBytes(args[0]), newBytes = File.ReadAllBytes(args[1]);
            await PreparationChecks(server, output, oldBytes, newBytes);
            await ProcessCase(server, output, oldBytes, newBytes, "healthy", "1.3.0");
            await ProcessCase(server, output, oldBytes, newBytes, "exit", "1.2.0");
            await ProcessCase(server, output, oldBytes, newBytes, "noack", "1.2.0");
            await ProcessCase(server, output, oldBytes, newBytes, "locked", "1.2.0");
            await InterruptedCase(server, output, oldBytes, newBytes, false);
            await InterruptedCase(server, output, oldBytes, newBytes, true);
            await InterruptedPreparedCase(server, output, oldBytes, newBytes);
            await StartFailureCase(server, output, oldBytes, newBytes);
            await DuplicateCase(server, output, oldBytes, newBytes);
            await PermissionCase(server, output, oldBytes, newBytes);
            if (args.Length >= 5) await RealLauncherCase(server, output, args[3], args[4], args.Length == 6);
            Console.WriteLine($"PASS: {passed} checks. Artifacts: {output}");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL: " + label);
        passed++;
        Console.WriteLine("PASS " + label);
    }

    static async Task Reject(Func<Task> action, string label)
    {
        try { await action(); }
        catch (Exception e) when (e is InvalidDataException or UriFormatException or HttpRequestException or IOException or OperationCanceledException)
        { Check(true, label); return; }
        throw new Exception("FAIL: accepted " + label);
    }

    static ReleaseManifest Manifest(byte[] bytes, string url, string version = "1.3.0") =>
        new(1, version, url, Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength);

    static byte[] Encode<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Json);

    static async Task TransportChecks(LocalServer server, string root)
    {
        Check(LauncherSelfUpdate.IsNewer("1.10.0", "1.9.9"), "numeric version comparison");
        Check(!LauncherSelfUpdate.IsNewer("1.2.0", "1.2.0"), "same version is not installed");
        Check(!LauncherSelfUpdate.IsNewer("1.1.9", "1.2.0"), "older version is not installed");
        var legacy = JsonSerializer.Deserialize<LauncherConfig>("{\"manifestUrl\":\"https://example.org/releases/manifest.json\"}", Json)!;
        Check(legacy.SelfUpdateUri.AbsoluteUri == "https://example.org/releases/launcher-manifest.json", "old config derives sibling manifest");
        Check((legacy with { LauncherManifestUrl = "https://other.example/launcher.json" }).SelfUpdateUri.Host == "other.example", "explicit launcher URL");
        byte[] payload = Encoding.UTF8.GetBytes("verified payload");
        server.Routes["/payload"] = new(payload);
        server.Routes["/redirect"] = new([], 302, "/payload");
        server.Routes["/unsafe"] = new([], 302, "http://example.invalid/payload");
        server.Routes["/loop"] = new([], 302, "/loop");
        server.Routes["/truncated"] = new(payload[..3], DeclaredLength: payload.Length);
        server.Routes["/slow"] = new(payload, DelayMs: 1000);
        using var transport = new UpdateTransport(true);
        using var secureOnly = new UpdateTransport();
        var manifest = Manifest(payload, server.Url + "/payload");
        server.Routes["/manifest"] = new(Encode(manifest));
        Check(await transport.CheckAsync(new(server.Url + "/manifest"), LauncherSelfUpdate.MaxSize) == manifest, "read launcher manifest");
        foreach (var invalid in new[] { manifest with { SchemaVersion = 2 }, manifest with { Version = "1.2" },
            manifest with { Sha256 = "bad" }, manifest with { Size = 0 }, manifest with { Size = LauncherSelfUpdate.MaxSize + 1 },
            manifest with { PackageUrl = "https://user:secret@example.org/update" } })
        {
            server.Routes["/invalid"] = new(Encode(invalid));
            await Reject(async () => { await transport.CheckAsync(new(server.Url + "/invalid"), LauncherSelfUpdate.MaxSize); }, "invalid manifest " + invalid.Version + "/" + invalid.Size + "/" + invalid.SchemaVersion);
        }
        server.Routes["/oversize"] = new(new byte[65537]);
        await Reject(async () => { await transport.CheckAsync(new(server.Url + "/oversize"), LauncherSelfUpdate.MaxSize); }, "manifest size limit");
        await Reject(async () => { await secureOnly.CheckAsync(new(server.Url + "/manifest"), LauncherSelfUpdate.MaxSize); }, "HTTP disabled by default");
        string good = Path.Combine(root, "good.download");
        await transport.DownloadAsync(manifest with { PackageUrl = server.Url + "/redirect" }, good, LauncherSelfUpdate.MaxSize);
        Check(File.ReadAllBytes(good).SequenceEqual(payload), "download follows safe redirect and verifies hash");
        foreach (string route in new[] { "unsafe", "loop", "truncated" })
            await Reject(() => transport.DownloadAsync(manifest with { PackageUrl = server.Url + "/" + route }, Path.Combine(root, route), LauncherSelfUpdate.MaxSize), route);
        await Reject(() => transport.DownloadAsync(manifest with { Sha256 = new string('0', 64) }, Path.Combine(root, "badhash"), LauncherSelfUpdate.MaxSize), "hash mismatch");
        await Reject(() => transport.DownloadAsync(manifest with { Size = payload.Length + 1 }, Path.Combine(root, "badsize"), LauncherSelfUpdate.MaxSize), "length mismatch");
        using var canceled = new CancellationTokenSource(100);
        await Reject(() => transport.DownloadAsync(manifest with { PackageUrl = server.Url + "/slow" }, Path.Combine(root, "canceled"), LauncherSelfUpdate.MaxSize, ct: canceled.Token), "cancel while receiving");
        await Reject(async () => { await transport.CheckAsync(new("http://127.0.0.1:1/offline"), LauncherSelfUpdate.MaxSize); }, "offline endpoint");
    }

    static async Task GameChecks(LocalServer server, string root)
    {
        byte[] zip;
        using (var data = new MemoryStream())
        {
            using (var archive = new ZipArchive(data, ZipArchiveMode.Create, true))
                foreach (string name in new[] { "TankBattle.exe", "UnityPlayer.dll", "TankBattle_Data/data" })
                { using var file = archive.CreateEntry(name).Open(); file.Write([1, 2, 3]); }
            zip = data.ToArray();
        }
        server.Routes["/game.zip"] = new(zip);
        var manifest = Manifest(zip, server.Url + "/game.zip", "2.0.0");
        string gameRoot = Path.Combine(root, "game-regression");
        using var game = new Updater(gameRoot, true);
        await game.InstallAsync(manifest);
        var current = game.Current();
        Check(current?.Version == "2.0.0" && File.Exists(game.GamePath(current!)), "game install through shared transport");
        await Reject(() => game.InstallAsync(manifest with { Version = "2.0.1", Sha256 = new string('0', 64) }), "bad game package rejected");
        Check(game.Current() == current, "game update failure keeps current release");
    }

    static (string Target, Fixture Config, byte[] ConfigBytes) Setup(LocalServer server, string root, byte[] oldBytes, byte[] newBytes, string name)
    {
        string directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, "Launcher.exe");
        File.WriteAllBytes(target, oldBytes);
        server.Routes["/" + name + ".exe"] = new(newBytes);
        string manifestUrl = server.Url + "/" + name + ".json";
        server.Routes["/" + name + ".json"] = new(Encode(Manifest(newBytes, server.Url + "/" + name + ".exe")));
        string dataRoot = Path.Combine(directory, "data");
        Directory.CreateDirectory(dataRoot);
        File.WriteAllText(Path.Combine(dataRoot, "current.json"), "existing game state");
        var config = new Fixture(dataRoot, manifestUrl, server.Url + "/fixture-game.json", name);
        server.Routes["/fixture-game.json"] = new(Encode(Manifest([1], server.Url + "/game-unused.zip", "2.0.0")));
        byte[] configBytes = Encode(config);
        File.WriteAllBytes(Path.Combine(directory, "launcher.json"), configBytes);
        return (target, config, configBytes);
    }

    static Process Launch(string exe, params string[] args)
    {
        var start = new ProcessStartInfo(exe) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Path.GetDirectoryName(exe)! };
        foreach (string arg in args) start.ArgumentList.Add(arg);
        return Process.Start(start)!;
    }

    static async Task WaitFor(Func<bool> condition, int seconds = 50)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(seconds)) throw new TimeoutException("Fixture timed out.");
            await Task.Delay(100);
        }
    }

    static async Task ProcessCase(LocalServer server, string root, byte[] oldBytes, byte[] newBytes, string behavior, string expected)
    {
        var fixture = Setup(server, root, oldBytes, newBytes, behavior);
        string gameExe = Path.Combine(Path.GetDirectoryName(fixture.Target)!, "TankBattle.exe");
        File.WriteAllBytes(gameExe, oldBytes);
        using var runningGame = Launch(gameExe, "--hold");
        using var locked = behavior == "locked" ? new FileStream(fixture.Target, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        using var parent = Launch(fixture.Target);
        try
        {
            string result = Path.Combine(fixture.Config.Root, "result.json");
            if (behavior == "noack")
            {
                await WaitFor(() => FileVersionInfo.GetVersionInfo(fixture.Target).FileMinorPart == 3);
                using var observer = new LauncherSelfUpdate(fixture.Target, Path.Combine(fixture.Config.Root, "launcher-updates"), true);
                Check(observer.InspectStartup() == LauncherStartup.Busy, "active helper blocks unrelated startup");
                string token = JsonNode.Parse(File.ReadAllText(StateFile(fixture.Config)))!["token"]!.GetValue<string>();
                Check(observer.InspectStartup(token) == LauncherStartup.Continue, "replacement startup accepted only with its transaction token");
            }
            await WaitFor(() => File.Exists(result));
            var actual = JsonNode.Parse(File.ReadAllText(result))!;
            Check(actual["version"]!.GetValue<string>() == expected, behavior + " process result version");
            Check(actual["gameChecked"]!.GetValue<bool>(), behavior + " continues game check");
            Check(!runningGame.HasExited, behavior + " does not terminate running game");
            Check(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(fixture.Target)!, "launcher.json")).SequenceEqual(fixture.ConfigBytes), behavior + " keeps config bytes");
            Check(File.ReadAllText(Path.Combine(fixture.Config.Root, "current.json")) == "existing game state", behavior + " keeps game installation state");
            await parent.WaitForExitAsync();
            await Task.Delay(500);
            // After a rollback the lock deliberately prevents pruning until this test releases it.
        }
        finally { if (!runningGame.HasExited) { runningGame.Kill(); runningGame.WaitForExit(); } }
    }

    static async Task PreparationChecks(LocalServer server, string root, byte[] oldBytes, byte[] newBytes)
    {
        var fixture = Setup(server, root, oldBytes, newBytes, "preparation-errors");
        string updateRoot = Path.Combine(fixture.Config.Root, "launcher-updates");
        using var update = new LauncherSelfUpdate(fixture.Target, updateRoot, true);
        var manifest = await update.CheckAsync(new(fixture.Config.ManifestUrl));
        await Reject(() => update.PrepareAsync(manifest with { Sha256 = new string('0', 64) }), "preparation rejects bad hash");
        await Reject(() => update.PrepareAsync(manifest with { Version = "1.4.0" }), "preparation rejects executable version mismatch");
        server.Routes["/preparation-errors.exe"] = server.Routes["/preparation-errors.exe"] with { DelayMs = 1000 };
        using var cancel = new CancellationTokenSource(100);
        await Reject(() => update.PrepareAsync(manifest, ct: cancel.Token), "cancel launcher preparation during download");
        Check(File.ReadAllBytes(fixture.Target).SequenceEqual(oldBytes), "failed/canceled preparation preserves original launcher");
        Check(!Directory.GetFiles(updateRoot, "candidate.exe", SearchOption.AllDirectories).Any() &&
            !Directory.GetFiles(updateRoot, "transaction.json", SearchOption.AllDirectories).Any(), "failed/canceled preparation removes incomplete download and state");
    }

    static string StateFile(Fixture config) => Directory.GetFiles(Path.Combine(config.Root, "launcher-updates"), "transaction.json", SearchOption.AllDirectories).Single();

    static async Task InterruptedCase(LocalServer server, string root, byte[] oldBytes, byte[] newBytes, bool acknowledged)
    {
        string name = acknowledged ? "interrupted-ack" : "interrupted-swap";
        var fixture = Setup(server, root, oldBytes, newBytes, name);
        using (var parent = Launch(fixture.Target, "--prepare-only")) { await parent.WaitForExitAsync(); Check(parent.ExitCode == 0, name + " preparation"); }
        string statePath = StateFile(fixture.Config);
        var state = JsonNode.Parse(File.ReadAllText(statePath))!;
        string token = state["token"]!.GetValue<string>();
        string staged = fixture.Target + ".update-" + token + ".new";
        File.Copy(Path.Combine(Path.GetDirectoryName(statePath)!, "candidate.exe"), staged);
        File.Replace(staged, fixture.Target, fixture.Target + ".update-" + token + ".bak", ignoreMetadataErrors: true);
        if (acknowledged) File.WriteAllText(Path.Combine(Path.GetDirectoryName(statePath)!, "ready"), token);
        using var next = Launch(fixture.Target);
        await WaitFor(() => File.Exists(Path.Combine(fixture.Config.Root, "result.json")));
        var result = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture.Config.Root, "result.json")))!;
        Check(result["version"]!.GetValue<string>() == (acknowledged ? "1.3.0" : "1.2.0"), name + " startup recovery");
        await next.WaitForExitAsync();
        await Task.Delay(500);
    }

    static async Task PermissionCase(LocalServer server, string root, byte[] oldBytes, byte[] newBytes)
    {
        var fixture = Setup(server, root, oldBytes, newBytes, "denied");
        var directory = new DirectoryInfo(Path.GetDirectoryName(fixture.Target)!);
        var original = directory.GetAccessControl(AccessControlSections.Access);
        byte[] descriptor = original.GetSecurityDescriptorBinaryForm();
        using var user = WindowsIdentity.GetCurrent();
        original.AddAccessRule(new FileSystemAccessRule(user.User!, FileSystemRights.CreateFiles, AccessControlType.Deny));
        try
        {
            directory.SetAccessControl(original);
            using var update = new LauncherSelfUpdate(fixture.Target, Path.Combine(fixture.Config.Root, "launcher-updates"), true);
            var manifest = await update.CheckAsync(new(fixture.Config.ManifestUrl));
            bool denied = false;
            try { await update.PrepareAsync(manifest); }
            catch (UnauthorizedAccessException) { denied = true; }
            Check(denied, "unwritable destination rejected before handoff");
            Check(File.ReadAllBytes(fixture.Target).SequenceEqual(oldBytes), "permission failure preserves executable");
        }
        finally
        {
            var restore = new DirectorySecurity();
            restore.SetSecurityDescriptorBinaryForm(descriptor, AccessControlSections.Access);
            directory.SetAccessControl(restore);
        }
    }

    static async Task InterruptedPreparedCase(LocalServer server, string root, byte[] oldBytes, byte[] newBytes)
    {
        var fixture = Setup(server, root, oldBytes, newBytes, "interrupted-prepare");
        using (var parent = Launch(fixture.Target, "--prepare-only")) await parent.WaitForExitAsync();
        using var update = new LauncherSelfUpdate(fixture.Target, Path.Combine(fixture.Config.Root, "launcher-updates"), true);
        Check(update.InspectStartup() == LauncherStartup.Continue, "interrupted preparation can start normally");
        Check(File.ReadAllBytes(fixture.Target).SequenceEqual(oldBytes), "interrupted preparation keeps old executable");
        Check(!Directory.GetFiles(Path.Combine(fixture.Config.Root, "launcher-updates"), "transaction.json", SearchOption.AllDirectories).Any(), "interrupted preparation prunes its state");
    }

    static async Task StartFailureCase(LocalServer server, string root, byte[] oldBytes, byte[] newBytes)
    {
        var fixture = Setup(server, root, oldBytes, newBytes, "start-failure");
        using (var parent = Launch(fixture.Target, "--prepare-only")) await parent.WaitForExitAsync();
        string path = StateFile(fixture.Config);
        var state = JsonNode.Parse(File.ReadAllText(path))!;
        // Fault injection after preparation: a verified but non-executable image exercises Process.Start failure.
        byte[] invalidExe = Encoding.UTF8.GetBytes("not an executable");
        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(path)!, "candidate.exe"), invalidExe);
        state["sha256"] = Convert.ToHexString(SHA256.HashData(invalidExe));
        File.WriteAllText(path, state.ToJsonString());
        using var handoff = Launch(fixture.Target, "--handoff-only");
        await WaitFor(() => File.Exists(Path.Combine(fixture.Config.Root, "result.json")));
        var result = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture.Config.Root, "result.json")))!;
        Check(result["restored"]!.GetValue<bool>() && result["version"]!.GetValue<string>() == "1.2.0", "unstartable candidate rolls back");
        await handoff.WaitForExitAsync();
        await Task.Delay(500);
    }

    static async Task DuplicateCase(LocalServer server, string root, byte[] oldBytes, byte[] newBytes)
    {
        var fixture = Setup(server, root, oldBytes, newBytes, "duplicate");
        using var first = Launch(fixture.Target, "--hold-ui");
        try
        {
            await WaitFor(() => File.Exists(Path.Combine(fixture.Config.Root, "mutex-held")));
            using var second = Launch(fixture.Target);
            await second.WaitForExitAsync();
            Check(second.ExitCode == 3, "second launcher cannot acquire UI mutex");
        }
        finally
        {
            File.WriteAllText(Path.Combine(fixture.Config.Root, "release-mutex"), "done");
            await first.WaitForExitAsync();
        }
    }

    static async Task RealLauncherCase(LocalServer server, string root, string oldExe, string newExe, bool keepUi)
    {
        var fixture = Setup(server, root, File.ReadAllBytes(oldExe), File.ReadAllBytes(newExe), "ui-actual");
        var game = Manifest(server.Routes["/game.zip"].Body, server.Url + "/game.zip", "2.0.0");
        server.Routes["/ui-game.json"] = new(Encode(game));
        var config = new LauncherConfig(server.Url + "/ui-game.json", true, fixture.Config.ManifestUrl);
        string configPath = Path.Combine(Path.GetDirectoryName(fixture.Target)!, "launcher.json");
        byte[] configBytes = Encode(config);
        File.WriteAllBytes(configPath, configBytes);
        File.Delete(Path.Combine(fixture.Config.Root, "current.json"));
        using (var installed = new Updater(fixture.Config.Root, true)) await installed.InstallAsync(game with { Version = "1.9.0" });
        var start = new ProcessStartInfo(fixture.Target) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
        start.Environment["TANK_LAUNCHER_TEST_ROOT"] = fixture.Config.Root;
        using var original = Process.Start(start)!;
        try
        {
            await WaitFor(() =>
            {
                using var installed = new Updater(fixture.Config.Root, true);
                return installed.Current()?.Version == "2.0.0";
            });
            var version = FileVersionInfo.GetVersionInfo(fixture.Target);
            Check(version.FileMajorPart == 1 && version.FileMinorPart == 3, "actual WinForms launcher upgraded to 1.3.0");
            Check(File.ReadAllBytes(configPath).SequenceEqual(configBytes), "actual WinForms update preserves config");
            var requests = server.Requests.ToArray();
            Check(Array.IndexOf(requests, "/ui-actual.exe") < Array.IndexOf(requests, "/ui-game.json"), "actual WinForms checks/installs game after launcher restart");
            await original.WaitForExitAsync();
            if (keepUi)
            {
                // Slow future checks so a human/Computer Use test can exercise Cancel without timing races.
                server.Routes["/ui-actual.json"] = server.Routes["/ui-actual.json"] with { DelayMs = 15000 };
                File.WriteAllText(Path.Combine(root, "ui-ready"), fixture.Target);
                Console.WriteLine("UI ready for inspection. Create " + Path.Combine(root, "close-ui") + " when finished.");
                await WaitFor(() => File.Exists(Path.Combine(root, "close-ui")), 300);
            }
        }
        finally
        {
            foreach (var process in Process.GetProcessesByName("Launcher"))
            using (process)
            {
                try
                {
                    if (string.Equals(process.MainModule?.FileName, fixture.Target, StringComparison.OrdinalIgnoreCase) && !process.HasExited)
                    { process.Kill(); process.WaitForExit(5000); }
                }
                catch (InvalidOperationException) { }
            }
        }
    }

    static int RunFixture(string[] args)
    {
        string target = Environment.ProcessPath!;
        var config = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "launcher.json")), Json)!;
        using var update = new LauncherSelfUpdate(target, Path.Combine(config.Root, "launcher-updates"), true);
        bool restored = args.FirstOrDefault() == "--launcher-update-restored";
        string? token = args.Length == 2 ? args[1] : null;
        using var mutex = new Mutex(false, LauncherSelfUpdate.UiMutexName);
        bool acquired;
        try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(10)); } catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) return 3;
        try
        {
            if (args.FirstOrDefault() == "--hold-ui")
            {
                File.WriteAllText(Path.Combine(config.Root, "mutex-held"), "held");
                var timer = Stopwatch.StartNew();
                while (!File.Exists(Path.Combine(config.Root, "release-mutex")) && timer.Elapsed < TimeSpan.FromSeconds(30)) Thread.Sleep(100);
                return 0;
            }
            if (args.FirstOrDefault() == "--handoff-only") { update.BeginHandoff(); return 0; }
            var startup = update.InspectStartup(token);
            if (startup == LauncherStartup.Busy) return 4;
            if (startup == LauncherStartup.Recover) { update.BeginHandoff(true); return 0; }
            if (token != null && !restored)
            {
                if (config.Behavior == "exit") return 5;
                if (config.Behavior == "noack") { Thread.Sleep(40000); return 6; }
                update.Acknowledge(token);
            }
            if (!restored)
            {
                var manifest = update.CheckAsync(new(config.ManifestUrl)).GetAwaiter().GetResult();
                if (LauncherSelfUpdate.IsNewer(manifest.Version, LauncherVersion.Current))
                {
                    update.PrepareAsync(manifest).GetAwaiter().GetResult();
                    if (args.FirstOrDefault() == "--prepare-only") return 0;
                    update.BeginHandoff();
                    return 0;
                }
            }
            using var game = new Updater(config.Root, true);
            game.CheckAsync(new(config.GameUrl)).GetAwaiter().GetResult();
            File.WriteAllBytes(Path.Combine(config.Root, "result.json"), Encode(new { version = LauncherVersion.Current, gameChecked = true, restored }));
            return 0;
        }
        finally { mutex.ReleaseMutex(); }
    }

    sealed record Response(byte[] Body, int Status = 200, string? Location = null, int? DeclaredLength = null, int DelayMs = 0);

    sealed class LocalServer : IDisposable
    {
        readonly TcpListener listener = new(IPAddress.Loopback, 0);
        readonly CancellationTokenSource stop = new();
        readonly Task accept;
        public ConcurrentDictionary<string, Response> Routes { get; } = new();
        public ConcurrentQueue<string> Requests { get; } = new();
        public string Url { get; }
        public LocalServer()
        {
            listener.Start();
            Url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
            accept = Task.Run(async () =>
            {
                try { while (!stop.IsCancellationRequested) { var client = await listener.AcceptTcpClientAsync(stop.Token); _ = Serve(client); } }
                catch (OperationCanceledException) { }
            });
        }
        async Task Serve(TcpClient client)
        {
            using (client)
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                string? first = await reader.ReadLineAsync(stop.Token);
                if (first == null) return;
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(stop.Token))) { }
                string path = first.Split(' ')[1];
                Requests.Enqueue(path);
                var response = Routes.TryGetValue(path, out var found) ? found : new Response([], 404);
                string header = $"HTTP/1.1 {response.Status} Status\r\nContent-Length: {response.DeclaredLength ?? response.Body.Length}\r\nConnection: close\r\n";
                if (response.Location != null) header += "Location: " + response.Location + "\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(header + "\r\n"), stop.Token);
                if (response.DelayMs > 0) await Task.Delay(response.DelayMs, stop.Token);
                await stream.WriteAsync(response.Body, stop.Token);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException) { }
        }
        public void Dispose() { stop.Cancel(); listener.Stop(); accept.GetAwaiter().GetResult(); stop.Dispose(); }
    }
}
