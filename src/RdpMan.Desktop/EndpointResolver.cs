using System.Net;
using System.Net.Sockets;

namespace RdpMan.Desktop;

internal static class EndpointResolver
{
    public static string SelectConnectionAddress(MachineEntry machine)
    {
        var address = machine.DnsName.Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            return "";
        }

        var host = HostPart(address);
        if (IPAddress.TryParse(host, out _))
        {
            return address;
        }

        if (TryResolveCurrentIp(machine, out _))
        {
            return address;
        }

        var lastKnownIp = machine.LastKnownIpAddress.Trim();
        return IPAddress.TryParse(lastKnownIp, out _)
            ? ReplaceHost(address, lastKnownIp)
            : address;
    }

    public static bool TryResolveCurrentIp(MachineEntry machine, out string ipAddress)
    {
        ipAddress = "";
        var host = HostPart(machine.DnsName.Trim());
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (IPAddress.TryParse(host, out var literalIp))
        {
            ipAddress = literalIp.ToString();
            return true;
        }

        try
        {
            var addresses = Dns.GetHostAddresses(host);
            var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetworkV6);
            if (address is null)
            {
                return false;
            }

            ipAddress = address.ToString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string HostPart(string address)
    {
        if (address.StartsWith("[", StringComparison.Ordinal))
        {
            var end = address.IndexOf(']');
            return end > 1 ? address[1..end] : address;
        }

        var colonCount = address.Count(character => character == ':');
        if (colonCount == 1)
        {
            var colonIndex = address.IndexOf(':');
            return colonIndex > 0 ? address[..colonIndex] : address;
        }

        return address;
    }

    private static string ReplaceHost(string address, string host)
    {
        if (address.StartsWith("[", StringComparison.Ordinal))
        {
            var end = address.IndexOf(']');
            var bracketSuffix = end >= 0 ? address[(end + 1)..] : "";
            return host.Contains(':', StringComparison.Ordinal) ? $"[{host}]{bracketSuffix}" : $"{host}{bracketSuffix}";
        }

        var oldHost = HostPart(address);
        var suffix = address.Length > oldHost.Length ? address[oldHost.Length..] : "";
        return host.Contains(':', StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(suffix)
            ? $"[{host}]{suffix}"
            : $"{host}{suffix}";
    }
}
