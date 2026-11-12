using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace RdpMan.Desktop;

internal sealed record PingResult(bool Reachable, string Message);

internal static class PingProbe
{
    public static async Task<PingResult> CheckAsync(string endpoint, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var host = EndpointResolver.HostPart(endpoint.Trim());
            if (string.IsNullOrWhiteSpace(host)) return new(false, "Kein Hostname hinterlegt");
            var addresses = await Dns.GetHostAddressesAsync(host, deadline.Token).ConfigureAwait(false);
            var address = addresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault();
            if (address is null) return new(false, "Keine IP-Adresse gefunden");
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, 1000).WaitAsync(deadline.Token).ConfigureAwait(false);
            return reply.Status == IPStatus.Success
                ? new(true, $"Antwort von {reply.Address}: {reply.RoundtripTime} ms")
                : new(false, $"{address}: {StatusText(reply.Status)}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "Zeitüberschreitung bei der Erreichbarkeitsprüfung");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is SocketException or PingException or ArgumentException or InvalidOperationException or System.Security.SecurityException or NotSupportedException)
        {
            return new(false, $"Nicht erreichbar: {ex.Message}");
        }
    }

    private static string StatusText(IPStatus status) => status switch
    {
        IPStatus.TimedOut => "Zeitüberschreitung",
        IPStatus.DestinationHostUnreachable => "Zielhost nicht erreichbar",
        IPStatus.DestinationNetworkUnreachable => "Zielnetz nicht erreichbar",
        _ => $"Keine Antwort ({status})",
    };
}
