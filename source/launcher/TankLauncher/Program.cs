using System.Diagnostics;
using System.Text.Json;

namespace TankLauncher;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, @"Local\TankBattleLauncher", out bool first);
        if (!first) { MessageBox.Show("Launcher đang chạy.", "Tank Battle"); return; }
        ApplicationConfiguration.Initialize();
        Application.Run(new LauncherForm());
    }
}

public sealed record LauncherConfig(string ManifestUrl, bool AllowLoopbackHttp = false);

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

    public LauncherForm()
    {
        Text = "Tank Battle Launcher";
        ClientSize = new Size(560, 245);
        MinimumSize = new Size(520, 280);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        updater = new Updater(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TankBattle"), ReadConfigSafely()?.AllowLoopbackHttp ?? false);
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
        check.Click += async (_, _) => await UpdateAsync();
        cancel.Click += (_, _) => operation?.Cancel();
        play.Click += (_, _) => Play();
        Shown += async (_, _) => await UpdateAsync();
        FormClosing += (_, e) => { if (busy) { operation?.Cancel(); e.Cancel = true; status.Text = "Đang hủy tải; hãy đóng lại sau khi hoàn tất."; } };
        FormClosed += (_, _) => updater.Dispose();
    }

    static LauncherConfig ReadConfig() => JsonSerializer.Deserialize<LauncherConfig>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "launcher.json")), Updater.JsonOptions)
        ?? throw new InvalidDataException("launcher.json rỗng.");

    static LauncherConfig? ReadConfigSafely() { try { return ReadConfig(); } catch { return null; } }

    void RefreshCurrent()
    {
        var current = updater.Current();
        version.Text = current == null ? "Chưa cài game" : "Bản đã cài: " + current.Version;
        play.Enabled = current != null;
    }

    async Task UpdateAsync()
    {
        if (busy) return;
        busy = true;
        check.Enabled = play.Enabled = false;
        cancel.Enabled = true;
        operation = new CancellationTokenSource();
        progress.Value = 0;
        try
        {
            status.Text = "Đang kiểm tra cập nhật...";
            var manifest = await updater.CheckAsync(new Uri(ReadConfig().ManifestUrl), operation.Token);
            InstalledRelease? current;
            try { current = updater.Current(); }
            catch (Exception e) when (e is InvalidDataException or JsonException) { current = null; }
            if (current?.Version == manifest.Version && current.Sha256.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                status.Text = "Game đã ở phiên bản mới nhất. Sẵn sàng chơi.";
            else
                await updater.InstallAsync(manifest, new Progress<UpdateProgress>(p => { status.Text = p.Message; progress.Value = Math.Clamp(p.Percent, 0, 100); }), operation.Token);
        }
        catch (OperationCanceledException) { status.Text = "Đã hủy cập nhật. Bản đã cài được giữ nguyên."; }
        catch (Exception e) { status.Text = "Không cập nhật được: " + e.Message; }
        finally
        {
            busy = false;
            operation.Dispose();
            operation = null;
            check.Enabled = true;
            cancel.Enabled = false;
            try { RefreshCurrent(); } catch (Exception e) { play.Enabled = false; version.Text = e.Message; }
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
