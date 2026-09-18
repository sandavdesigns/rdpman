using System.Globalization;

namespace RdpMan.Desktop;

internal static class SshTerminalTheme
{
    public const string DefaultTextColor = "#39FF14";

    public static Color ParseTextColor(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && value.Length == 7
            && value[0] == '#'
            && int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return Color.FromArgb((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
        }

        return Color.FromArgb(57, 255, 20);
    }

    public static string ToHtml(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
