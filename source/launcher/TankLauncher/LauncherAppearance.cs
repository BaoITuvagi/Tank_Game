using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace TankLauncher;

sealed partial class LauncherForm
{
    readonly LauncherVisuals visuals = new();
    readonly Label heading = new() { Text = "Sẵn sàng xuất kích", UseCompatibleTextRendering = true };
    readonly Label status = new() { Text = "Đang chuẩn bị launcher...", UseCompatibleTextRendering = true };
    readonly LauncherProgress progress = new();
    readonly LauncherButton check = new() { Text = "KIỂM TRA CẬP NHẬT", Glyph = LauncherGlyph.Refresh, TabIndex = 0 };
    readonly LauncherButton play = new() { Text = "CHƠI GAME", Primary = true, Glyph = LauncherGlyph.Play, Enabled = false, TabIndex = 1 };
    readonly LauncherButton cancel = new() { Text = "HỦY TẢI", Glyph = LauncherGlyph.Close, Visible = false, TabIndex = 2 };
    string installedVersion = "CHƯA CÀI GAME";
    Color statusColor = LauncherVisuals.Gold;
    bool appearanceReady;
    float layoutScale;
    int panelTop;

    void InitializeAppearance()
    {
        SuspendLayout();
        Text = "Tank Battle";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.Sizable;
        ControlBox = MinimizeBox = MaximizeBox = true;
        BackColor = LauncherVisuals.Panel;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        float dpi = DeviceDpi / 96f;
        MinimumSize = SizeFromClientSize(new Size((int)(900 * dpi), (int)(600 * dpi)));
        ClientSize = new Size((int)(1120 * dpi), (int)(660 * dpi));
        // The icon is derived from the original logo, not from the concept image.
        using (var bitmap = new Bitmap(visuals.Logo, 64, 64))
        {
            IntPtr handle = bitmap.GetHicon();
            try { using var icon = System.Drawing.Icon.FromHandle(handle); Icon = (Icon)icon.Clone(); }
            finally { DestroyIcon(handle); }
        }
        foreach (var button in new[] { check, play, cancel })
        {
            button.Visuals = visuals;
            Controls.Add(button);
        }
        foreach (var label in new[] { heading, status })
        {
            label.BackColor = LauncherVisuals.Panel;
            label.ForeColor = LauncherVisuals.Cream;
            Controls.Add(label);
        }
        status.ForeColor = LauncherVisuals.Muted;
        status.AutoEllipsis = true;
        Controls.Add(progress);
        status.TextChanged += (_, _) => { ArrangeAppearance(); Invalidate(); };
        appearanceReady = true;
        ArrangeAppearance();
        ResumeLayout(false);
    }

    void SetStatus(string title, string message, Color color)
    {
        heading.Text = title;
        statusColor = color;
        progress.FillColor = color;
        status.Text = message;
        status.AccessibleName = title + ". " + message;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ArrangeAppearance();
    }

    void ArrangeAppearance()
    {
        if (!appearanceReady || ClientSize.Width == 0) return;
        float scale = ClientSize.Width / 1120f;
        int S(float value) => (int)Math.Round(value * scale);
        if (Math.Abs(scale - layoutScale) > .001f)
        {
            bool replaceFonts = layoutScale > 0;
            layoutScale = scale;
            foreach (var control in new Control[] { heading, status, check, play, cancel })
            {
                var previous = control.Font;
                float size = control == heading ? 23 : control == play ? 28 : control == status ? 15 : 14;
                control.Font = visuals.Font(size * scale, control == play);
                // Each control owns its assigned font; the initial system font is shared.
                if (replaceFonts) previous.Dispose();
            }
        }
        int left = S(42), rightWidth = S(330), gap = S(32);
        int textWidth = ClientSize.Width - left * 2 - rightWidth - gap;
        using var graphics = CreateGraphics();
        int detailHeight = (int)Math.Ceiling(graphics.MeasureString(status.Text, status.Font, textWidth - S(42)).Height) + S(3);
        detailHeight = Math.Clamp(detailHeight, S(26), S(100));
        int footer = S(30), panelHeight = S(146) + detailHeight;
        panelTop = ClientSize.Height - footer - panelHeight;
        heading.SetBounds(left + S(42), panelTop + S(20), textWidth - S(42), S(36));
        status.SetBounds(left + S(42), panelTop + S(57), textWidth - S(42), detailHeight);
        int barY = panelTop + S(64) + detailHeight;
        progress.SetBounds(left, barY, textWidth, S(7));
        check.SetBounds(left, barY + S(17), S(232), S(44));
        cancel.SetBounds(left + S(244), barY + S(17), S(126), S(44));
        play.SetBounds(ClientSize.Width - left - rightWidth, panelTop + (panelHeight - S(74)) / 2, rightWidth, S(74));
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!appearanceReady) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        float scale = ClientSize.Width / 1120f;
        float height = ClientSize.Height / scale, top = panelTop / scale;
        g.ScaleTransform(scale, scale);
        using var panel = new SolidBrush(LauncherVisuals.Panel);
        using var border = new Pen(LauncherVisuals.Border);
        g.FillRectangle(panel, 0, 0, 1120, height);
        var hero = new RectangleF(1, 0, 1118, Math.Max(1, top));
        // Cover the hero with the project's original battlefield artwork.
        float ratio = Math.Max(hero.Width / visuals.Background.Width, hero.Height / visuals.Background.Height);
        float sourceWidth = hero.Width / ratio, sourceHeight = hero.Height / ratio;
        g.DrawImage(visuals.Background, hero, new RectangleF((visuals.Background.Width - sourceWidth) / 2,
            (visuals.Background.Height - sourceHeight) / 2, sourceWidth, sourceHeight), GraphicsUnit.Pixel);
        using (var shade = new LinearGradientBrush(new RectangleF(0, 0, 790, hero.Height), Color.Transparent, Color.Transparent, 0f))
        {
            shade.InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(245, 13, 28, 20), Color.FromArgb(185, 13, 28, 20), Color.FromArgb(0, 13, 28, 20)],
                Positions = [0f, .55f, 1f]
            };
            g.FillRectangle(shade, 0, 0, 790, hero.Height);
        }
        DrawHeroContent(g, hero);
        g.DrawLine(border, 0, top, 1120, top);
        using (var halo = new SolidBrush(Color.FromArgb(40, statusColor))) g.FillEllipse(halo, 42, top + 25, 25, 25);
        using (var dot = new SolidBrush(statusColor)) g.FillEllipse(dot, 48, top + 31, 13, 13);
        if (busy)
            DrawText(g, progress.Value + "%", 13, false, LauncherVisuals.Muted, new RectangleF(594, top + 24, 85, 25), true);
        g.DrawLine(border, 0, height - 30, 1120, height - 30);
        DrawText(g, installedVersion, 11, false, LauncherVisuals.Muted, new RectangleF(24, height - 23, 450, 22));
        DrawText(g, "LAUNCHER " + LauncherVersion.Current, 11, false, LauncherVisuals.Muted,
            new RectangleF(875, height - 23, 220, 22), false, StringAlignment.Far);
        g.DrawRectangle(border, 1, 1, 1118, height - 2);
    }

    void DrawHeroContent(Graphics g, RectangleF hero)
    {
        // Measure actual glyph outlines, including Vietnamese accents. Every block
        // starts after the preceding block's full visible bounds, not a guessed baseline.
        using var first = visuals.Headline("SẴN SÀNG", 68);
        using var second = visuals.Headline("XUẤT KÍCH", 68);
        var firstBounds = first.GetBounds();
        var secondBounds = second.GetBounds();
        const float logoWidth = 210;
        float logoHeight = logoWidth * visuals.LogoBounds.Height / visuals.LogoBounds.Width;
        float firstY = logoHeight + 24;
        float secondY = firstY + firstBounds.Height + 12;
        float subtitleY = secondY + secondBounds.Height + 20;
        const string subtitle = "Khởi động xe tăng.\nBước vào trận chiến.";
        using var bodyFont = visuals.Font(23);
        float subtitleHeight = g.MeasureString(subtitle, bodyFont, 520).Height;
        float contentHeight = subtitleY + subtitleHeight;
        float contentScale = Math.Max(.01f, Math.Min(1.15f,
            Math.Min((hero.Height - 64) / contentHeight, (hero.Width - 84) / 520)));
        float contentTop = hero.Top + (hero.Height - contentHeight * contentScale) / 2;
        var saved = g.Save();
        g.SetClip(hero);
        g.TranslateTransform(42, contentTop);
        g.ScaleTransform(contentScale, contentScale);
        g.DrawImage(visuals.Logo, new RectangleF(0, 0, logoWidth, logoHeight), visuals.LogoBounds, GraphicsUnit.Pixel);
        using var cream = new SolidBrush(LauncherVisuals.Cream);
        foreach (var (path, bounds, y) in new[] { (first, firstBounds, firstY), (second, secondBounds, secondY) })
        {
            using var move = new Matrix();
            move.Translate(-bounds.Left, y - bounds.Top);
            path.Transform(move);
            g.FillPath(cream, path);
        }
        DrawText(g, subtitle, 23, false, LauncherVisuals.Cream, new RectangleF(0, subtitleY, 520, subtitleHeight));
        g.Restore(saved);
    }

    void DrawText(Graphics g, string text, float size, bool display, Color color, RectangleF rect,
        bool centered = false, StringAlignment alignment = StringAlignment.Near)
    {
        using var font = visuals.Font(size, display);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = centered ? StringAlignment.Center : alignment,
            LineAlignment = centered ? StringAlignment.Center : StringAlignment.Near };
        g.DrawString(text, font, brush, rect, format);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            var fonts = new Control[] { heading, status, check, play, cancel }.Select(c => c.Font).ToArray();
            base.Dispose(true);
            foreach (var font in fonts) font.Dispose();
            Icon?.Dispose();
            visuals.Dispose();
        }
        else base.Dispose(false);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DestroyIcon(IntPtr icon);
}

// Assets are linked from the Unity project and embedded in the single-file executable.
// Font licenses and their source notice are embedded alongside the original fonts.
sealed class LauncherVisuals : IDisposable
{
    public static readonly Color Panel = Color.FromArgb(25, 44, 32), Border = Color.FromArgb(78, 103, 79),
        Cream = Color.FromArgb(244, 238, 205), Muted = Color.FromArgb(196, 207, 182),
        Gold = Color.FromArgb(248, 196, 77), Ready = Color.FromArgb(164, 215, 124), Warning = Color.FromArgb(241, 162, 92);
    readonly PrivateFontCollection display = new(), body = new();
    readonly FontFamily displayFamily, bodyFamily;
    readonly List<IntPtr> memory = [];
    public Bitmap Logo { get; } = LoadImage("logo.png");
    public Bitmap Background { get; } = LoadImage("background.png");
    public Rectangle LogoBounds { get; }

    public LauncherVisuals()
    {
        LoadFont(display, "display.ttf"); LoadFont(body, "body.ttf");
        displayFamily = display.Families[0]; bodyFamily = body.Families[0];
        LogoBounds = VisibleBounds(Logo);
    }

    static Stream Resource(string name) => typeof(LauncherVisuals).Assembly.GetManifestResourceStream("LauncherArt." + name)
        ?? throw new InvalidDataException("Thiếu tài nguyên launcher: " + name);

    static Bitmap LoadImage(string name)
    {
        using var input = Resource(name);
        using var image = Image.FromStream(input);
        return new Bitmap(image);
    }

    void LoadFont(PrivateFontCollection fonts, string name)
    {
        using var input = Resource(name);
        using var buffer = new MemoryStream(); input.CopyTo(buffer);
        byte[] bytes = buffer.ToArray();
        IntPtr pointer = Marshal.AllocCoTaskMem(bytes.Length);
        memory.Add(pointer);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        fonts.AddMemoryFont(pointer, bytes.Length);
    }

    public Font Font(float pixels, bool headline = false) => new(headline ? displayFamily : bodyFamily, pixels,
        FontStyle.Regular, GraphicsUnit.Pixel);

    public GraphicsPath Headline(string text, float pixels, bool display = true)
    {
        var path = new GraphicsPath();
        using var format = StringFormat.GenericTypographic;
        path.AddString(text, display ? displayFamily : bodyFamily, (int)FontStyle.Regular, pixels, PointF.Empty, format);
        return path;
    }

    static Rectangle VisibleBounds(Bitmap bitmap)
    {
        int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).A != 0)
                {
                    left = Math.Min(left, x); top = Math.Min(top, y);
                    right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
        return right < left ? new Rectangle(0, 0, bitmap.Width, bitmap.Height)
            : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    public static GraphicsPath CutCorner(RectangleF rect, float cut)
    {
        var path = new GraphicsPath();
        path.AddPolygon(new PointF[] { new(rect.Left + cut, rect.Top), new(rect.Right - cut, rect.Top), new(rect.Right, rect.Top + cut),
            new(rect.Right, rect.Bottom - cut), new(rect.Right - cut, rect.Bottom), new(rect.Left + cut, rect.Bottom),
            new(rect.Left, rect.Bottom - cut), new(rect.Left, rect.Top + cut) });
        return path;
    }

    public void Dispose()
    {
        Logo.Dispose(); Background.Dispose(); displayFamily.Dispose(); bodyFamily.Dispose(); display.Dispose(); body.Dispose();
        foreach (var pointer in memory) Marshal.FreeCoTaskMem(pointer);
    }
}

sealed class LauncherButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal LauncherVisuals? Visuals { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Primary { get; init; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal LauncherGlyph Glyph { get; init; }
    bool hovered, pressed;

    public LauncherButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        BackColor = LauncherVisuals.Panel;
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (Capture && (MouseButtons & MouseButtons.Left) != 0) { pressed = ClientRectangle.Contains(e.Location); Invalidate(); }
        base.OnMouseMove(e);
    }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        if (Visuals == null || Height < 8 || Width < 8) return;
        float scale = Height / (Primary ? 74f : 44f);
        g.ScaleTransform(scale, scale);
        float width = Width / scale, height = Height / scale;
        bool down = Enabled && pressed;
        float depth = Primary ? 4 : 2;
        var face = new RectangleF(1, 1 + (down ? depth - 1 : 0), width - 3, height - depth - 3);
        var shadow = face; shadow.Y = 1 + depth;
        using var shadowPath = LauncherVisuals.CutCorner(shadow, 8);
        using var shadowInk = new SolidBrush(Enabled && Primary ? Color.FromArgb(139, 96, 30) : Color.FromArgb(12, 26, 19));
        g.FillPath(shadowInk, shadowPath);
        using var path = LauncherVisuals.CutCorner(face, 8);
        Color fill = !Enabled ? Color.FromArgb(48, 65, 53) : Primary ?
            (down ? Color.FromArgb(229, 171, 51) : hovered ? Color.FromArgb(255, 211, 101) : LauncherVisuals.Gold) :
            hovered ? Color.FromArgb(57, 80, 58) : Color.FromArgb(39, 59, 44);
        using var surface = new SolidBrush(fill);
        using var border = new Pen(Primary && Enabled ? Color.FromArgb(15, 30, 20) :
            hovered && Enabled ? LauncherVisuals.Gold : LauncherVisuals.Border, Primary ? 1.8f : 1.2f);
        g.FillPath(surface, path); g.DrawPath(border, path);
        if (Enabled)
        {
            using var bevel = new Pen(Color.FromArgb(Primary ? 135 : 35, LauncherVisuals.Cream), 1.5f);
            g.DrawLine(bevel, face.Left + 9, face.Top + 2, face.Right - 9, face.Top + 2);
        }
        Color inkColor = !Enabled ? Color.FromArgb(153, 170, 151) : Primary ? Color.FromArgb(22, 36, 27) : LauncherVisuals.Cream;
        bool updating = Primary && Text != "CHƠI GAME";
        using var text = Visuals.Headline(Text, Primary ? updating ? 17 : 28 : 14, Primary);
        var bounds = text.GetBounds();
        float iconSize = Primary ? 34 : 18, gap = Primary ? 14 : 10;
        float contentWidth = iconSize + gap + bounds.Width;
        float fit = Math.Min(1, (face.Width - 28) / contentWidth);
        float x = face.Left + (face.Width - contentWidth * fit) / 2;
        float centerY = face.Top + face.Height / 2;
        var content = g.Save();
        g.TranslateTransform(x, centerY);
        g.ScaleTransform(fit, fit);
        if (Primary)
        {
            using var badge = new SolidBrush(Enabled ? Color.FromArgb(29, 46, 31) : Color.FromArgb(37, 52, 42));
            g.FillEllipse(badge, 0, -iconSize / 2, iconSize, iconSize);
        }
        LauncherGlyphs.Draw(g, updating ? LauncherGlyph.Download : Glyph,
            new RectangleF(Primary ? 7 : 0, Primary ? -10 : -9, Primary ? 20 : 18, Primary ? 20 : 18),
            Primary && Enabled ? LauncherVisuals.Gold : inkColor);
        using var move = new Matrix();
        move.Translate(iconSize + gap - bounds.Left, -bounds.Top - bounds.Height / 2);
        text.Transform(move);
        using var ink = new SolidBrush(inkColor);
        g.FillPath(ink, text);
        g.Restore(content);
        if (Focused)
        {
            var inside = face; inside.Inflate(-5, -5);
            using var focusPath = LauncherVisuals.CutCorner(inside, 5);
            using var focus = new Pen(Primary ? Color.FromArgb(65, 78, 43) : LauncherVisuals.Gold, 1.5f) { DashStyle = DashStyle.Dot };
            g.DrawPath(focus, focusPath);
        }
    }
}

enum LauncherGlyph { Play, Refresh, Close, Download }

// A shared vector icon vocabulary avoids OS-font-dependent glyph shapes and baselines.
static class LauncherGlyphs
{
    public static void Draw(Graphics g, LauncherGlyph icon, RectangleF rect, Color color)
    {
        var saved = g.Save();
        g.TranslateTransform(rect.X, rect.Y);
        g.ScaleTransform(rect.Width / 24, rect.Height / 24);
        using var pen = new Pen(color, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var fill = new SolidBrush(color);
        switch (icon)
        {
            case LauncherGlyph.Play:
                g.FillPolygon(fill, new PointF[] { new(8, 4), new(21, 12), new(8, 20) });
                break;
            case LauncherGlyph.Refresh:
                g.DrawArc(pen, 4, 4, 16, 16, 40, 285);
                g.FillPolygon(fill, new PointF[] { new(18, 2), new(22, 10), new(14, 9) });
                break;
            case LauncherGlyph.Close:
                g.DrawLine(pen, 6, 6, 18, 18); g.DrawLine(pen, 6, 18, 18, 6);
                break;
            case LauncherGlyph.Download:
                g.DrawLine(pen, 12, 3, 12, 15);
                g.DrawLines(pen, new PointF[] { new(7, 10), new(12, 15), new(17, 10) });
                g.DrawLines(pen, new PointF[] { new(4, 17), new(4, 21), new(20, 21), new(20, 17) });
                break;
        }
        g.Restore(saved);
    }
}

sealed class LauncherProgress : Control
{
    int value;
    Color fillColor = LauncherVisuals.Gold;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int Value { get => value; set { this.value = Math.Clamp(value, 0, 100); AccessibleName = "Tiến độ cập nhật " + this.value + "%"; Invalidate(); Parent?.Invalidate(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Color FillColor { get => fillColor; set { fillColor = value; Invalidate(); } }
    public LauncherProgress() { DoubleBuffered = true; TabStop = false; AccessibleRole = AccessibleRole.ProgressBar; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.FromArgb(56, 78, 57));
        using var fill = new SolidBrush(FillColor);
        e.Graphics.FillRectangle(fill, 0, 0, Width * Value / 100f, Height);
    }
}
