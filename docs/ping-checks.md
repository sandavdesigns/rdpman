# Integrated ping monitor

Open the machine context menu and choose `Ping überwachen`. The modeless window
shows timestamps, response times, and sent/received counters. Start/stop affects
only this monitor, not the RDP/SSH connection. Reopening it for the same machine
activates the existing window. History is capped at 500 entries.

The monitor resolves the configured hostname on every probe, with a three-second
overall deadline and a one-second ICMP timeout. DNS failures and missing ICMP
replies are logged and retried. It does not run a shell or execute machine names
as commands. ICMP replies do not guarantee that RDP/SSH is ready to connect.

## Windows verification

- Open the monitor while the target is already offline. RDPMan must stay usable.
- Restart a reachable target. Check that replies stop and resume automatically.
- Test an invalid hostname, `hostname:3389`, and `[IPv6]:3389`.
- Stop/start repeatedly, then close the window during DNS lookup or a timeout.
  No stale result should overwrite a newly started run or access a closed window.
- Open monitors for two machines; reopening one must not create a duplicate.
- Close RDPMan while monitors are active. All probes must stop.

Portable regression checks: `dotnet run --project tests/PingProbe.Checks`.
