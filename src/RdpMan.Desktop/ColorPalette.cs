namespace RdpMan.Desktop;

internal sealed record ColorChoice(string Key, string Label)
{
    public override string ToString() => Label;
}

internal static class ColorPalette
{
    public static readonly ColorChoice[] Choices =
    [
        new("blue", "Blau"),
        new("green", "Grün"),
        new("amber", "Gelb"),
        new("red", "Rot"),
        new("violet", "Violett"),
        new("cyan", "Cyan"),
        new("slate", "Grau"),
    ];

    public static ColorChoice DefaultChoice => Choices[0];

    public static ColorChoice? Find(string? key)
    {
        return Choices.FirstOrDefault(choice => choice.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    public static Color Marker(string? key, bool isConnected)
    {
        var (light, dark) = key switch
        {
            "green" => (Color.FromArgb(134, 239, 172), Color.FromArgb(22, 163, 74)),
            "amber" => (Color.FromArgb(253, 224, 71), Color.FromArgb(202, 138, 4)),
            "red" => (Color.FromArgb(252, 165, 165), Color.FromArgb(220, 38, 38)),
            "violet" => (Color.FromArgb(196, 181, 253), Color.FromArgb(124, 58, 237)),
            "cyan" => (Color.FromArgb(103, 232, 249), Color.FromArgb(8, 145, 178)),
            "slate" => (Color.FromArgb(148, 163, 184), Color.FromArgb(71, 85, 105)),
            _ => (Color.FromArgb(147, 197, 253), Color.FromArgb(37, 99, 235)),
        };

        return isConnected ? dark : light;
    }

    public static void ConfigureColorCombo(ComboBox comboBox)
    {
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.DrawMode = DrawMode.OwnerDrawFixed;
        comboBox.FlatStyle = FlatStyle.Flat;
        AppTheme.StyleInput(comboBox);
        SetDpiScaledItemHeight(comboBox, 26);
        comboBox.DrawItem += DrawColorChoice;
    }

    private static void DrawColorChoice(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox comboBox || e.Index < 0 || e.Index >= comboBox.Items.Count)
        {
            return;
        }

        e.DrawBackground();
        var choice = comboBox.Items[e.Index] as ColorChoice;
        var text = choice?.Label ?? comboBox.Items[e.Index]?.ToString() ?? "";
        var dotColor = string.IsNullOrWhiteSpace(choice?.Key)
            ? Color.FromArgb(148, 163, 184)
            : Marker(choice.Key, isConnected: true);
        using var dot = new SolidBrush(dotColor);
        using var fore = new SolidBrush(e.ForeColor);
        var scale = comboBox.DeviceDpi / 96f;
        var left = (int)Math.Round(8 * scale);
        var top = (int)Math.Round(6 * scale);
        var size = (int)Math.Round(14 * scale);
        var textLeft = (int)Math.Round(30 * scale);
        var textTop = (int)Math.Round(4 * scale);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.FillEllipse(dot, e.Bounds.Left + left, e.Bounds.Top + top, size, size);
        e.Graphics.DrawString(text, AppTheme.UiFont, fore, e.Bounds.Left + textLeft, e.Bounds.Top + textTop);
        e.DrawFocusRectangle();
    }

    private static void SetDpiScaledItemHeight(ComboBox comboBox, int baselineHeight)
    {
        void UpdateItemHeight() => comboBox.ItemHeight = (int)Math.Round(baselineHeight * comboBox.DeviceDpi / 96f);

        comboBox.HandleCreated += (_, _) => UpdateItemHeight();
        comboBox.DpiChangedAfterParent += (_, _) => UpdateItemHeight();
        UpdateItemHeight();
    }
}
