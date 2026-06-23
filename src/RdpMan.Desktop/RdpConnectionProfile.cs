using System.Text;

namespace RdpMan.Desktop;

public static class RdpConnectionProfile
{
    public static string BuildUsername(MachineEntry machine, CredentialProfile? credential)
    {
        if (credential is null || string.IsNullOrWhiteSpace(credential.Username))
        {
            return "";
        }

        var domain = string.IsNullOrWhiteSpace(credential.Domain)
            ? TargetDomain(machine)
            : credential.Domain.Trim();

        return string.IsNullOrWhiteSpace(domain)
            ? credential.Username.Trim()
            : $@"{domain}\{credential.Username.Trim()}";
    }

    public static string TargetHost(MachineEntry machine)
    {
        var host = machine.DnsName.Trim();
        if (host.StartsWith("[", StringComparison.Ordinal))
        {
            var end = host.IndexOf(']');
            return end > 1 ? host[1..end] : host;
        }

        var colonIndex = host.IndexOf(':');
        return colonIndex > 0 ? host[..colonIndex] : host;
    }

    public static string WriteRdpFile(MachineEntry machine, CredentialProfile? credential, Size desktopSize)
    {
        var directory = Path.Combine(Path.GetTempPath(), Brand.ExecutableName);
        Directory.CreateDirectory(directory);
        var fileName = string.Concat(machine.DisplayName.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var path = Path.Combine(directory, $"{fileName}.rdp");
        var username = BuildUsername(machine, credential);

        var width = Math.Max(desktopSize.Width, 800);
        var height = Math.Max(desktopSize.Height, 600);

        var lines = new List<string>
        {
            "screen mode id:i:1",
            "use multimon:i:0",
            $"desktopwidth:i:{width}",
            $"desktopheight:i:{height}",
            "session bpp:i:32",
            "smart sizing:i:1",
            "dynamic resolution:i:0",
            "desktop size id:i:0",
            $"redirectclipboard:i:{Flag(machine.RedirectClipboard)}",
            $"redirectprinters:i:{Flag(machine.RedirectPrinters)}",
            $"redirectsmartcards:i:{Flag(machine.RedirectSmartCards)}",
            $"redirectwebauthn:i:{Flag(machine.RedirectWebAuthn)}",
            "authentication level:i:0",
            "enablecredsspsupport:i:1",
            "prompt for credentials:i:0",
            "promptcredentialonce:i:1",
            $"full address:s:{machine.DnsName}",
        };
        if (!string.IsNullOrWhiteSpace(username))
        {
            lines.Add($"username:s:{username}");
        }

        File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n", Encoding.ASCII);
        return path;
    }

    private static string TargetDomain(MachineEntry machine)
    {
        var host = TargetHost(machine);
        if (string.IsNullOrWhiteSpace(host) || host.All(character => char.IsDigit(character) || character == '.'))
        {
            return "";
        }

        var dotIndex = host.IndexOf('.');
        return dotIndex > 0 ? host[..dotIndex] : host;
    }

    private static int Flag(bool value) => value ? 1 : 0;
}
