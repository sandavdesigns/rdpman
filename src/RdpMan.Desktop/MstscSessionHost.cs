using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace RdpMan.Desktop;

[SupportedOSPlatform("windows")]
public sealed class MstscSessionHost : IRemoteSessionHost
{
    private const int GwlStyle = -16;
    private const int WsCaption = 0x00C00000;
    private const int WsThickFrame = 0x00040000;
    private const int WsMinimize = 0x20000000;
    private const int WsMaximize = 0x01000000;
    private const int WsSysMenu = 0x00080000;
    private const int WsChild = 0x40000000;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsClipSiblings = 0x04000000;
    private const int WsClipChildren = 0x02000000;
    private const int SwpNoZOrder = 0x0004;
    private const int SwpFrameChanged = 0x0020;
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const int WmSize = 0x0005;
    private const int RdwInvalidate = 0x0001;
    private const int RdwAllChildren = 0x0080;
    private const int RdwUpdateNow = 0x0100;

    private Process? _process;
    private readonly System.Windows.Forms.Timer _windowMonitor;
    private readonly HashSet<int> _mstscProcessIds = [];
    private DateTime _connectStartedAtUtc;
    private Size _lastSessionSize;
    private bool _isConnecting;
    private IntPtr _windowHandle;

    public MachineEntry Machine { get; }
    public CredentialProfile? Credential { get; }
    public Control Control { get; } = new Panel
    {
        Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(15, 23, 42),
    };

    public bool IsConnected => _process is { HasExited: false } || HasTrackedMstscProcess();

    public MstscSessionHost(MachineEntry machine, CredentialProfile? credential)
    {
        Machine = machine;
        Credential = credential;
        Control.Resize += (_, _) =>
        {
            ResizeToHost();
        };
        _windowMonitor = new System.Windows.Forms.Timer
        {
            Interval = 350,
        };
        _windowMonitor.Tick += (_, _) => CaptureCurrentMstscWindow();
    }

    public void Connect()
    {
        if (IsConnected || _isConnecting)
        {
            return;
        }

        _isConnecting = true;
        try
        {
            StoreCredential();
            var sessionSize = CurrentSessionSize();
            var targetHost = RdpConnectionProfile.ConnectionAddress(Machine);
            if (string.IsNullOrWhiteSpace(targetHost))
            {
                throw new InvalidOperationException("Kein Zielhost für die RDP-Verbindung angegeben.");
            }

            _lastSessionSize = sessionSize;
            _connectStartedAtUtc = DateTime.UtcNow;
            _mstscProcessIds.Clear();
            _windowHandle = IntPtr.Zero;
            _process = Process.Start(new ProcessStartInfo
            {
                FileName = "mstsc.exe",
                Arguments = $"/v:\"{targetHost}\" /w:{sessionSize.Width} /h:{sessionSize.Height}",
                UseShellExecute = true,
            }) ?? throw new InvalidOperationException("mstsc.exe konnte nicht gestartet werden.");
            _mstscProcessIds.Add(_process.Id);

            _windowMonitor.Start();
        }
        finally
        {
            _isConnecting = false;
        }
    }

    public void Reconnect()
    {
        Disconnect();
        Connect();
    }

    public void Disconnect()
    {
        _windowMonitor.Stop();
        if (_process is { HasExited: false })
        {
            _process.CloseMainWindow();
            if (!_process.WaitForExit(1500))
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        _process?.Dispose();
        _process = null;
        CloseTrackedMstscProcesses();
        _mstscProcessIds.Clear();
        _connectStartedAtUtc = default;
        _windowHandle = IntPtr.Zero;
    }

    public void ResizeToHost()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        var width = Math.Max(Control.ClientSize.Width, 320);
        var height = Math.Max(Control.ClientSize.Height, 240);
        MoveWindow(_windowHandle, 0, 0, width, height, repaint: true);
        SendMessage(_windowHandle, WmSize, IntPtr.Zero, MakeSizeLParam(width, height));
        RedrawWindow(_windowHandle, IntPtr.Zero, IntPtr.Zero, RdwInvalidate | RdwAllChildren | RdwUpdateNow);
    }

    public void Dispose()
    {
        Disconnect();
        _windowMonitor.Dispose();
        Control.Dispose();
    }

    private void EmbedWhenReady()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            _process.WaitForInputIdle(5000);
        }
        catch
        {
            // mstsc may return before creating a normal input queue; polling below still handles it.
        }

        var deadline = DateTime.UtcNow.AddSeconds(12);
        while (DateTime.UtcNow < deadline)
        {
            RefreshTrackedMstscProcesses();
            if (!HasTrackedMstscProcess())
            {
                throw new InvalidOperationException("mstsc.exe wurde beendet, bevor das RDP-Fenster eingebettet werden konnte.");
            }

            var handle = FindBestMstscSessionWindow();
            if (handle != IntPtr.Zero)
            {
                EmbedWindow(handle);
                break;
            }

            Application.DoEvents();
            Thread.Sleep(100);
        }

        if (_windowHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Das mstsc.exe-Fenster wurde nicht gefunden.");
        }
    }

    private void CaptureCurrentMstscWindow()
    {
        RefreshTrackedMstscProcesses();
        if (!HasTrackedMstscProcess())
        {
            _windowMonitor.Stop();
            _windowHandle = IntPtr.Zero;
            return;
        }

        var handle = FindBestMstscSessionWindow();
        if (handle == IntPtr.Zero || handle == _windowHandle)
        {
            ResizeToHost();
            return;
        }

        EmbedWindow(handle);
    }

    private Size CurrentSessionSize()
    {
        return new Size(Math.Max(Control.ClientSize.Width, 800), Math.Max(Control.ClientSize.Height, 600));
    }

    private void RefreshTrackedMstscProcesses()
    {
        if (_connectStartedAtUtc == default)
        {
            return;
        }

        if (_process is not null)
        {
            try
            {
                _process.Refresh();
                if (!_process.HasExited)
                {
                    _mstscProcessIds.Add(_process.Id);
                }
            }
            catch
            {
                // The original launcher process may disappear while mstsc hands off to the session window.
            }
        }

        foreach (var process in Process.GetProcessesByName("mstsc"))
        {
            using (process)
            {
                try
                {
                    if (process.StartTime.ToUniversalTime() >= _connectStartedAtUtc.AddSeconds(-2))
                    {
                        _mstscProcessIds.Add(process.Id);
                    }
                }
                catch
                {
                    // Access to StartTime can fail for processes that exit during enumeration.
                }
            }
        }

        _mstscProcessIds.RemoveWhere(processId => !IsProcessAlive(processId));
    }

    private bool HasTrackedMstscProcess()
    {
        RefreshTrackedMstscProcesses();
        return _mstscProcessIds.Count > 0;
    }

    private void CloseTrackedMstscProcesses()
    {
        foreach (var processId in _mstscProcessIds.ToArray())
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    continue;
                }

                process.CloseMainWindow();
                if (!process.WaitForExit(1500))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Already gone or owned by a process mstsc created outside our control.
            }
        }
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private void EmbedWindow(IntPtr handle)
    {
        _windowHandle = handle;
        ShowWindow(handle, SwHide);
        var style = GetWindowLong(handle, GwlStyle);
        style &= ~(WsCaption | WsThickFrame | WsMinimize | WsMaximize | WsSysMenu | WsPopup);
        style |= WsChild | WsClipSiblings | WsClipChildren;

        SetWindowLong(handle, GwlStyle, style);
        SetParent(handle, Control.Handle);
        SetWindowPos(handle, IntPtr.Zero, 0, 0, Math.Max(Control.ClientSize.Width, 320), Math.Max(Control.ClientSize.Height, 240), SwpNoZOrder | SwpFrameChanged);
        ShowWindow(handle, SwShow);
        ResizeToHost();
    }

    private static bool IsMstscSecurityDialog(IntPtr handle)
    {
        var title = GetWindowTitle(handle);
        return title.Equals("Remotedesktopverbindung", StringComparison.OrdinalIgnoreCase)
            || title.Equals("Remote Desktop Connection", StringComparison.OrdinalIgnoreCase);
    }

    private void StoreCredential()
    {
        if (Credential is null || string.IsNullOrWhiteSpace(Credential.ProtectedPassword))
        {
            return;
        }

        var username = RdpConnectionProfile.BuildUsername(Machine, Credential);
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        var password = CredentialVault.Unprotect(Credential.ProtectedPassword);
        var targetHost = RdpConnectionProfile.ConnectionTargetHost(Machine);
        if (string.IsNullOrWhiteSpace(targetHost))
        {
            return;
        }

        RunCmdKey($"/generic:TERMSRV/{targetHost} /user:\"{username}\" /pass:\"{password}\"");
        var originalTargetHost = RdpConnectionProfile.TargetHost(Machine);
        if (!string.Equals(targetHost, originalTargetHost, StringComparison.OrdinalIgnoreCase))
        {
            RunCmdKey($"/generic:TERMSRV/{originalTargetHost} /user:\"{username}\" /pass:\"{password}\"");
        }
    }

    private static void RunCmdKey(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmdkey.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
        process?.WaitForExit(3000);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private IntPtr FindBestMstscSessionWindow()
    {
        RefreshTrackedMstscProcesses();
        var targetWindow = FindBestMstscWindowByTitle();
        if (targetWindow != IntPtr.Zero)
        {
            GetWindowThreadProcessId(targetWindow, out var processId);
            _mstscProcessIds.Add(processId);
            return targetWindow;
        }

        return FindBestProcessWindow(_mstscProcessIds, skipSecurityDialog: true);
    }

    private IntPtr FindBestMstscWindowByTitle()
    {
        var bestHandle = IntPtr.Zero;
        var bestArea = 0;
        var targetHost = RdpConnectionProfile.TargetHost(Machine);
        var titleNeedles = new[]
        {
            Machine.DisplayName,
            Machine.DnsName,
            targetHost,
        }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || IsMstscSecurityDialog(handle))
            {
                return true;
            }

            GetWindowThreadProcessId(handle, out var processId);
            if (!IsMstscProcess(processId))
            {
                return true;
            }

            var title = GetWindowTitle(handle);
            if (titleNeedles.Length > 0 && !titleNeedles.Any(needle => title.Contains(needle, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!GetWindowRect(handle, out var rect))
            {
                return true;
            }

            var area = Math.Max(0, rect.Right - rect.Left) * Math.Max(0, rect.Bottom - rect.Top);
            if (area > bestArea)
            {
                bestArea = area;
                bestHandle = handle;
            }

            return true;
        }, IntPtr.Zero);

        return bestHandle;
    }

    private static IntPtr FindBestProcessWindow(IReadOnlySet<int> processIds, bool skipSecurityDialog)
    {
        var bestHandle = IntPtr.Zero;
        var bestArea = 0;

        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out var windowProcessId);
            if (!processIds.Contains(windowProcessId) || !IsWindowVisible(handle))
            {
                return true;
            }

            if (skipSecurityDialog && IsMstscSecurityDialog(handle))
            {
                return true;
            }

            if (!GetWindowRect(handle, out var rect))
            {
                return true;
            }

            var area = Math.Max(0, rect.Right - rect.Left) * Math.Max(0, rect.Bottom - rect.Top);
            if (area > bestArea)
            {
                bestArea = area;
                bestHandle = handle;
            }
            return true;
        }, IntPtr.Zero);

        return bestHandle;
    }

    private static string GetWindowTitle(IntPtr handle)
    {
        var length = GetWindowTextLength(handle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    private static bool IsMstscProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName.Equals("mstsc", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr MakeSizeLParam(int width, int height)
    {
        return (IntPtr)((height << 16) | (width & 0xFFFF));
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Rect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }
}
