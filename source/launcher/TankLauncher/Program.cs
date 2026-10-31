using System.Diagnostics;
using System.Text.Json;

namespace TankLauncher;

internal static class Program
{
    internal static string DataRoot
    {
        get
        {
#if LAUNCHER_TEST
            // Only the UI smoke build accepts an isolated data root; release builds ignore this environment variable.
            return Path.GetFullPath(Environment.GetEnvironmentVariable("TANK_LAUNCHER_TEST_ROOT")
                ?? throw new InvalidOperationException("UI smoke build requires TANK_LAUNCHER_TEST_ROOT."));
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TankBattle");
#endif
        }
    }

    [STAThread]
    static int Main(string[] args)
    {
        string dataRoot = DataRoot;
        string updateRoot = Path.Combine(dataRoot, "launcher-updates");
        if (args.Length == 3 && args[0] == "--apply-launcher-update")
        {
            try
            {
                using var helper = new LauncherSelfUpdate(args[1], args[2]);
                return helper.RunHelper();
            }
            catch { return 1; } // A helper must never open a modal dialog and strand its parent.
        }
        bool restored = args.Length == 2 && args[0] == "--launcher-update-restored";
        string? token = args.Length == 2 && (restored || args[0] == "--launcher-update-ready") ? args[1] : null;
        using var mutex = new Mutex(false, LauncherSelfUpdate.UiMutexName);
        bool acquired;
        try { acquired = mutex.WaitOne(token == null ? TimeSpan.Zero : TimeSpan.FromSeconds(10)); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) { MessageBox.Show("Launcher đang chạy.", "Tank Battle"); return 1; }
        try
        {
            using var selfUpdate = new LauncherSelfUpdate(Environment.ProcessPath!, updateRoot);
            string? notice = null;
            bool skip = restored;
            try
            {
                var startup = selfUpdate.InspectStartup(token);
                if (startup == LauncherStartup.Busy)
                {
                    // Do not hold the UI mutex in a modal dialog while the helper is
                    // trying to start the replacement launcher.
                    return 1;
                }
                if (startup == LauncherStartup.Recover) { selfUpdate.BeginHandoff(true); return 0; }
                notice = selfUpdate.LastError();
            }
            catch (Exception e)
            {
                if (token != null && !restored) return 1; // The helper will roll back this failed startup.
                notice = "Không khôi phục được cập nhật launcher: " + e.Message;
                skip = true;
            }
            ApplicationConfiguration.Initialize();
            Application.Run(new LauncherForm(selfUpdate, dataRoot, skip, restored ? null : token, notice));
            return 0;
        }
        finally { mutex.ReleaseMutex(); }
    }
}

sealed class LauncherForm : Form
{
    readonly Label status = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    readonly Label version = new() { AutoSize = true, Text = "Chưa cài game" };
    readonly ProgressBar progress = new() { Dock = DockStyle.Fill };
    readonly Button check = new() { Text = "Kiểm tra cập nhật", AutoSize = true };
    readonly Button play = new() { Text = "Chơi game", AutoSize = true, Enabled = false };
    readonly Button cancel = new() { Text = "Hủy tải", AutoSize = true, Enabled = false };
    readonly Updater updater;
    CancellationTokenSource? operation;
    bool busy;

    public LauncherForm(LauncherSelfUpdate selfUpdate, string dataRoot, bool skipSelfUpdate, string? readyToken, string? notice)
    {
        Text = "Tank Battle Launcher";
        ClientSize = new Size(560, 245);
        MinimumSize = new Size(520, 280);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        updater = new Updater(dataRoot, ReadConfigSafely()?.AllowLoopbackHttp ?? false);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), RowCount = 5, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.Controls.Add(new Label { Text = "TANK BATTLE", Font = new Font(Font.FontFamily, 20, FontStyle.Bold), AutoSize = true }, 0, 0);
        layout.Controls.Add(version, 0, 1);
        layout.Controls.Add(status, 0, 2);
        layout.Controls.Add(progress, 0, 3);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange([check, play, cancel]);
        layout.Controls.Add(buttons, 0, 4);
        Controls.Add(layout);
        RefreshCurrentSafely();
        check.Click += async (_, _) => await UpdateAsync();
        cancel.Click += (_, _) => operation?.Cancel();
        play.Click += (_, _) => Play();
        Shown += async (_, _) =>
        {
            if (readyToken != null)
            {
                try { selfUpdate.Acknowledge(readyToken); }
                catch { Close(); return; }
            }
            await UpdateAsync(!skipSelfUpdate, notice);
        };
        FormClosing += (_, e) => { if (busy) { operation?.Cancel(); e.Cancel = true; status.Text = "Đang hủy tải; hãy đóng lại sau khi hoàn tất."; } };
        FormClosed += (_, _) => updater.Dispose();
    }

    static LauncherConfig ReadConfig() => JsonSerializer.Deserialize<LauncherConfig>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "launcher.json")), Updater.JsonOptions)
        ?? throw new InvalidDataException("launcher.json rỗng.");

    static LauncherConfig? ReadConfigSafely() { try { return ReadConfig(); } catch { return null; } }

    static string UpdateError(Exception error) => error switch
    {
        HttpRequestException => "Không tải được dữ liệu.",
        OperationCanceledException => "Hết thời gian chờ máy chủ cập nhật.",
        UnauthorizedAccessException => "Không có quyền ghi file cập nhật.",
        _ => error.Message
    };

    void RefreshCurrent()
    {
        updater.CleanupOldVersions();
        var current = updater.Current();
        version.Text = current == null ? "Chưa cài game" : "Bản đã cài: " + current.Version;
        play.Enabled = current != null;
    }

    void RefreshCurrentSafely()
    {
        try { RefreshCurrent(); }
        catch (Exception e) { play.Enabled = false; version.Text = e.Message; }
    }

    async Task UpdateAsync(bool includeLauncher = true, string? notice = null)
    {
        if (busy) return;
        busy = true;
        check.Enabled = false;
        cancel.Enabled = true;
        play.Enabled = false;
        operation = new CancellationTokenSource();
        progress.Value = 0;
        bool handoff = false;
        try
        {
            var config = ReadConfig();
            if (includeLauncher)
            {
                try
                {
                    status.Text = "Đang kiểm tra cập nhật launcher " + LauncherVersion.Current + "...";
                    using var checkTimeout = CancellationTokenSource.CreateLinkedTokenSource(operation.Token);
                    checkTimeout.CancelAfter(TimeSpan.FromSeconds(20));
                    // Honor the same localhost setting as the game updater.
                    using var download = new LauncherSelfUpdate(Environment.ProcessPath!,
                        Path.Combine(Program.DataRoot, "launcher-updates"), config.AllowLoopbackHttp);
                    var release = await download.CheckAsync(config.SelfUpdateUri, checkTimeout.Token);
                    if (LauncherSelfUpdate.IsNewer(release.Version, LauncherVersion.Current))
                    {
#pragma warning disable IL3000 // A non-bundled debug build cannot be copied as a standalone helper.
                        if (!string.IsNullOrEmpty(typeof(Program).Assembly.Location))
                            throw new IOException("Tự cập nhật cần bản single-file từ scripts/build-launcher.ps1.");
#pragma warning restore IL3000
                        await download.PrepareAsync(release, new Progress<UpdateProgress>(p =>
                        { status.Text = "Launcher: " + p.Message; progress.Value = Math.Clamp(p.Percent, 0, 100); }), operation.Token);
                        operation.Token.ThrowIfCancellationRequested();
                        cancel.Enabled = false;
                        status.Text = "Đang mở lại launcher để áp dụng cập nhật...";
                        await Task.Run(() => download.BeginHandoff());
                        handoff = true;
                        return;
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException || !operation.IsCancellationRequested)
                { notice = "Chưa cập nhật được launcher: " + UpdateError(e); }
            }
            status.Text = "Đang kiểm tra cập nhật...";
            progress.Value = 0;
            var manifest = await updater.CheckAsync(new Uri(config.ManifestUrl), operation.Token);
            InstalledRelease? current;
            try { current = updater.Current(); }
            catch (Exception e) when (e is InvalidDataException or JsonException) { current = null; }
            if (current?.Version == manifest.Version && current.Sha256.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                status.Text = "Game đã ở phiên bản mới nhất. Sẵn sàng chơi.";
            else
                await updater.InstallAsync(manifest, new Progress<UpdateProgress>(p => { status.Text = p.Message; progress.Value = Math.Clamp(p.Percent, 0, 100); }), operation.Token);
        }
        catch (OperationCanceledException) { status.Text = "Đã hủy cập nhật. Bản đã cài được giữ nguyên."; }
        catch (Exception e) { status.Text = "Không cập nhật được: " + UpdateError(e); }
        finally
        {
            busy = false;
            operation.Dispose();
            operation = null;
            check.Enabled = true;
            cancel.Enabled = false;
            if (handoff) Close();
            else
            {
                RefreshCurrentSafely();
                if (!string.IsNullOrEmpty(notice)) status.Text = notice + "\n" + status.Text;
            }
        }
    }

    void Play()
    {
        try
        {
            var current = updater.Current() ?? throw new IOException("Chưa có bản game. Hãy kiểm tra cập nhật.");
            string executable = updater.GamePath(current);
            Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = true });
        }
        catch (Exception e) { MessageBox.Show(this, e.Message, "Không mở được game", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
