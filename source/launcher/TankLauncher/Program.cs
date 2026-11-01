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

sealed partial class LauncherForm : Form
{
    readonly Updater updater;
    CancellationTokenSource? operation;
    bool busy;

    public LauncherForm(LauncherSelfUpdate selfUpdate, string dataRoot, bool skipSelfUpdate, string? readyToken, string? notice)
    {
        updater = new Updater(dataRoot, ReadConfigSafely()?.AllowLoopbackHttp ?? false);
        InitializeAppearance();
        RefreshCurrentSafely();
        check.Click += async (_, _) => await UpdateAsync();
        cancel.Click += (_, _) => CancelUpdate();
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
        FormClosing += (_, e) => { if (busy) { CancelUpdate(); e.Cancel = true; } };
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
        installedVersion = current == null ? "CHƯA CÀI GAME" : "GAME " + current.Version;
        play.Enabled = current != null;
        Invalidate();
    }

    void RefreshCurrentSafely()
    {
        try { RefreshCurrent(); }
        catch (Exception e) { play.Enabled = false; installedVersion = "CHƯA CÀI GAME"; SetStatus("Bản cài cần kiểm tra", e.Message, LauncherVisuals.Warning); }
    }

    void CancelUpdate()
    {
        if (operation == null || !cancel.Enabled) return;
        operation.Cancel();
        cancel.Enabled = false;
        SetStatus("Đang hủy cập nhật", "Vui lòng đợi thao tác hiện tại kết thúc.", LauncherVisuals.Gold);
    }

    async Task UpdateAsync(bool includeLauncher = true, string? notice = null)
    {
        if (busy) return;
        busy = true;
        check.Enabled = false;
        cancel.Enabled = true;
        play.Enabled = false;
        play.Text = "ĐANG CẬP NHẬT";
        cancel.Visible = true;
        SetStatus("Đang kiểm tra cập nhật", "Đang kết nối tới máy chủ cập nhật...", LauncherVisuals.Gold);
        operation = new CancellationTokenSource();
        var activeOperation = operation;
        progress.Value = 0;
        bool handoff = false;
        try
        {
            var config = ReadConfig();
            if (includeLauncher)
            {
                try
                {
                    SetStatus("Đang kiểm tra launcher", "Phiên bản hiện tại: " + LauncherVersion.Current, LauncherVisuals.Gold);
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
                        {
                            // Progress<T> posts to the UI queue; ignore callbacks after cancellation/completion or from an earlier operation.
                            if (!ReferenceEquals(operation, activeOperation) || !cancel.Enabled) return;
                            SetStatus("Đang cập nhật launcher", p.Message, LauncherVisuals.Gold);
                            progress.Value = Math.Clamp(p.Percent, 0, 100);
                        }), operation.Token);
                        operation.Token.ThrowIfCancellationRequested();
                        cancel.Enabled = false;
                        SetStatus("Đang mở lại launcher", "Đang áp dụng giao diện và bản cập nhật mới...", LauncherVisuals.Gold);
                        await Task.Run(() => download.BeginHandoff());
                        handoff = true;
                        return;
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException || !operation.IsCancellationRequested)
                { notice = "Chưa cập nhật được launcher: " + UpdateError(e); }
            }
            SetStatus("Đang kiểm tra game", "Đang kiểm tra phiên bản game mới nhất...", LauncherVisuals.Gold);
            progress.Value = 0;
            var manifest = await updater.CheckAsync(new Uri(config.ManifestUrl), operation.Token);
            InstalledRelease? current;
            try { current = updater.Current(); }
            catch (Exception e) when (e is InvalidDataException or JsonException) { current = null; }
            if (current?.Version == manifest.Version && current.Sha256.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                progress.Value = 100;
                SetStatus("Sẵn sàng chơi", "Game đã ở phiên bản mới nhất.", LauncherVisuals.Ready);
            }
            else
            {
                await updater.InstallAsync(manifest, new Progress<UpdateProgress>(p =>
                {
                    if (!ReferenceEquals(operation, activeOperation) || !cancel.Enabled) return;
                    SetStatus("Đang cập nhật game", p.Message, LauncherVisuals.Gold);
                    progress.Value = Math.Clamp(p.Percent, 0, 100);
                }), operation.Token);
                progress.Value = 100;
                SetStatus("Sẵn sàng chơi", "Đã cập nhật game " + manifest.Version + ".", LauncherVisuals.Ready);
            }
        }
        catch (OperationCanceledException) { SetStatus("Đã hủy cập nhật", "Bản game đã cài được giữ nguyên.", LauncherVisuals.Warning); }
        catch (Exception e) { SetStatus("Chưa cập nhật được", UpdateError(e), LauncherVisuals.Warning); }
        finally
        {
            busy = false;
            operation.Dispose();
            operation = null;
            check.Enabled = true;
            cancel.Enabled = false;
            cancel.Visible = false;
            play.Text = "CHƠI GAME";
            if (handoff) Close();
            else
            {
                RefreshCurrentSafely();
                if (!string.IsNullOrEmpty(notice))
                    SetStatus("Thông báo cập nhật", notice + "\n" + status.Text, LauncherVisuals.Warning);
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
