using RdpMan.Desktop;

var failures = new List<string>();

await CheckAsync("blank endpoint", async () =>
{
    var result = await PingProbe.CheckAsync("   ", CancellationToken.None);
    Assert(!result.Reachable, "Blank endpoints must not be reachable.");
    Assert(result.Message == "Kein Hostname hinterlegt", "Blank endpoints should explain the missing host.");
});

await CheckAsync("malformed endpoint", async () =>
{
    var result = await PingProbe.CheckAsync("[]:3389", CancellationToken.None);
    Assert(!result.Reachable, "Malformed endpoints must fail recoverably.");
});

await CheckAsync("pre-cancelled request", async () =>
{
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    await AssertThrowsAsync<OperationCanceledException>(
        () => PingProbe.CheckAsync("localhost", cancellation.Token));
});

Check("IPv4 port extraction", () =>
    Assert(EndpointResolver.HostPart("192.0.2.10:3389") == "192.0.2.10", "IPv4 port was not removed."));
Check("bracketed IPv6 port extraction", () =>
    Assert(EndpointResolver.HostPart("[2001:db8::10]:3389") == "2001:db8::10", "IPv6 brackets or port were not removed."));
Check("raw IPv6 extraction", () =>
    Assert(EndpointResolver.HostPart("2001:db8::10") == "2001:db8::10", "Raw IPv6 must remain unchanged."));

await CheckAsync("missing DNS host", async () =>
{
    var result = await PingProbe.CheckAsync("ping-probe-check-does-not-exist.invalid", CancellationToken.None);
    Assert(!result.Reachable, "A reserved missing DNS name must not be reachable.");
});

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} check(s) failed:");
    foreach (var failure in failures) Console.Error.WriteLine($"- {failure}");
    return 1;
}

Console.WriteLine("All PingProbe checks passed.");
return 0;

void Check(string name, Action action)
{
    try
    {
        action();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{name}: {exception.Message}");
    }
}

async Task CheckAsync(string name, Func<Task> action)
{
    try
    {
        await action();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{name}: {exception.Message}");
    }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task AssertThrowsAsync<TException>(Func<Task> action) where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

namespace RdpMan.Desktop
{
    public sealed class MachineEntry
    {
        public string DnsName { get; set; } = "";
        public string LastKnownIpAddress { get; set; } = "";
    }
}
