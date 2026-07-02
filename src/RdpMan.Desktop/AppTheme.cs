using System.Drawing.Drawing2D;

namespace RdpMan.Desktop;

internal static class AppTheme
{
    public static readonly Color Window = Color.FromArgb(246, 248, 251);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceAlt = Color.FromArgb(240, 244, 248);
    public static readonly Color Sidebar = Color.FromArgb(17, 24, 39);
    public static readonly Color SidebarAlt = Color.FromArgb(31, 41, 55);
    public static readonly Color Border = Color.FromArgb(214, 222, 232);
    public static readonly Color Text = Color.FromArgb(17, 24, 39);
    public static readonly Color MutedText = Color.FromArgb(100, 116, 139);
    public static readonly Color Accent = Color.FromArgb(37, 99, 235);
    public static readonly Color AccentSoft = Color.FromArgb(219, 234, 254);
    public static readonly Color Success = Color.FromArgb(22, 163, 74);

    public static readonly Font UiFont = new("Segoe UI", 9.5f, FontStyle.Regular);
    public static readonly Font SmallFont = new("Segoe UI", 8.75f, FontStyle.Regular);
    public static readonly Font TitleFont = new("Segoe UI Semibold", 16f, FontStyle.Bold);
    public static readonly Font SectionFont = new("Segoe UI Semibold", 10f, FontStyle.Bold);

    public static Icon AppIcon { get; } = LoadAppIcon();

    public static void ApplyWindow(Form form)
    {
        form.Font = UiFont;
        form.BackColor = Window;
        form.ForeColor = Text;
        form.Icon = AppIcon;
        form.StartPosition = FormStartPosition.CenterParent;
    }

    public static Button Button(string text, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Width = primary ? 116 : 104,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            Font = primary ? SectionFont : UiFont,
            BackColor = primary ? Accent : Surface,
            ForeColor = primary ? Color.White : Text,
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(29, 78, 216) : SurfaceAlt;
        button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(30, 64, 175) : Color.FromArgb(226, 232, 240);
        return button;
    }

    public static Button SidebarButton(string text, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            Font = primary ? SectionFont : UiFont,
            BackColor = primary ? Accent : Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = new Padding(4),
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(51, 65, 85);
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(29, 78, 216) : Color.FromArgb(51, 65, 85);
        button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(30, 64, 175) : Color.FromArgb(71, 85, 105);
        return button;
    }

    public static Button SidebarIconButton(string symbol, bool primary = false)
    {
        var button = SidebarButton(symbol, primary);
        button.Font = new Font("Segoe UI Symbol", 14f, FontStyle.Regular);
        button.Margin = new Padding(3);
        button.AccessibleName = symbol;
        return button;
    }

    public static Label Label(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = SmallFont,
            ForeColor = MutedText,
            Margin = new Padding(0, 8, 0, 4),
        };
    }

    public static TextBox TextBox(bool multiline = false)
    {
        return new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            Font = UiFont,
            Multiline = multiline,
            Height = multiline ? 84 : 32,
            Margin = new Padding(0, 0, 0, 10),
        };
    }

    public static void StyleInput(Control control)
    {
        control.Font = UiFont;
        control.BackColor = Color.White;
        control.ForeColor = Text;
        control.Margin = new Padding(0, 0, 0, 10);
    }

    public static Panel Card(DockStyle dock = DockStyle.Fill)
    {
        return new Panel
        {
            Dock = dock,
            BackColor = Surface,
            Padding = new Padding(22),
            Margin = new Padding(0),
        };
    }

    public static FlowLayoutPanel Footer()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Window,
            Padding = new Padding(18, 14, 18, 14),
        };
    }

    public static void DrawLogo(Graphics graphics, Rectangle bounds)
    {
        DrawRdpManLogo(graphics, bounds);
    }

    private static void DrawRdpManLogo(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var background = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var green = new SolidBrush(Success);
        using var screenShell = new SolidBrush(Color.FromArgb(239, 246, 255));
        using var screen = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var line = new Pen(Color.FromArgb(96, 165, 250), Math.Max(2, bounds.Width / 18f));
        using var arrow = new Pen(green.Color, Math.Max(3, bounds.Width / 13f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        graphics.FillRoundedRectangle(background, bounds, Math.Max(8, bounds.Width / 8));

        var monitor = new Rectangle(bounds.Left + bounds.Width / 5, bounds.Top + bounds.Height / 5, bounds.Width * 3 / 5, bounds.Height * 9 / 20);
        graphics.FillRoundedRectangle(screenShell, monitor, Math.Max(5, bounds.Width / 10));
        var inner = Rectangle.Inflate(monitor, -bounds.Width / 13, -bounds.Height / 12);
        graphics.FillRoundedRectangle(screen, inner, Math.Max(3, bounds.Width / 18));
        graphics.DrawLine(line, inner.Left + inner.Width / 6, inner.Top + inner.Height / 3, inner.Left + inner.Width / 2, inner.Top + inner.Height / 3);
        graphics.DrawLine(line, inner.Left + inner.Width / 6, inner.Top + inner.Height * 2 / 3, inner.Left + inner.Width * 2 / 5, inner.Top + inner.Height * 2 / 3);

        var stand = new Rectangle(bounds.Left + bounds.Width * 43 / 100, monitor.Bottom - bounds.Height / 30, bounds.Width * 14 / 100, bounds.Height / 6);
        graphics.FillRectangle(screenShell, stand);
        var baseRect = new Rectangle(bounds.Left + bounds.Width / 4, bounds.Top + bounds.Height * 70 / 100, bounds.Width / 2, bounds.Height / 8);
        graphics.FillRoundedRectangle(screenShell, baseRect, Math.Max(4, bounds.Width / 14));

        var arrowY = bounds.Top + bounds.Height * 39 / 100;
        graphics.DrawLine(arrow, bounds.Left + bounds.Width * 61 / 100, arrowY, bounds.Left + bounds.Width * 79 / 100, arrowY);
        graphics.DrawLine(arrow, bounds.Left + bounds.Width * 71 / 100, arrowY - bounds.Height / 11, bounds.Left + bounds.Width * 80 / 100, arrowY);
        graphics.DrawLine(arrow, bounds.Left + bounds.Width * 71 / 100, arrowY + bounds.Height / 11, bounds.Left + bounds.Width * 80 / 100, arrowY);

        var dot = new Rectangle(bounds.Left + bounds.Width * 66 / 100, bounds.Top + bounds.Height * 64 / 100, bounds.Width / 6, bounds.Width / 6);
        using var dotInner = new SolidBrush(Color.FromArgb(220, 252, 231));
        graphics.FillEllipse(green, dot);
        graphics.FillEllipse(dotInner, Rectangle.Inflate(dot, -dot.Width / 4, -dot.Height / 4));
    }

    private static Icon LoadAppIcon()
    {
        var outputIcon = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(outputIcon))
        {
            return new Icon(outputIcon);
        }

        var sourceIcon = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "app.ico");
        if (File.Exists(sourceIcon))
        {
            return new Icon(sourceIcon);
        }

        using var bitmap = new Bitmap(64, 64);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            DrawLogo(graphics, new Rectangle(0, 0, 64, 64));
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = RoundedRectangle(bounds, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using var path = RoundedRectangle(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
