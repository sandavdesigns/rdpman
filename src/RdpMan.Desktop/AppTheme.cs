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
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var background = new SolidBrush(SidebarAlt);
        using var accent = new SolidBrush(Accent);
        using var green = new SolidBrush(Success);
        using var text = new SolidBrush(Color.White);
        using var titleFont = new Font("Segoe UI Semibold", Math.Max(9, bounds.Height * 0.22f), FontStyle.Bold);

        graphics.FillRoundedRectangle(background, bounds, Math.Max(8, bounds.Width / 8));
        var screen = Rectangle.Inflate(bounds, -bounds.Width / 6, -bounds.Height / 4);
        screen.Height = bounds.Height / 3;
        graphics.FillRoundedRectangle(accent, screen, Math.Max(4, bounds.Width / 18));
        graphics.FillRectangle(green, bounds.Left + bounds.Width / 3, screen.Bottom + bounds.Height / 11, bounds.Width / 3, Math.Max(3, bounds.Height / 16));
        graphics.DrawString("RDP", titleFont, text, bounds.Left + bounds.Width * 0.22f, bounds.Top + bounds.Height * 0.58f);
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
