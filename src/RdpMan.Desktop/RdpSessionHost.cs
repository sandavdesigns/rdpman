using Microsoft.Win32;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Versioning;

namespace RdpMan.Desktop;

[SupportedOSPlatform("windows")]
public sealed class RdpSessionHost : IRemoteSessionHost
{
    private readonly string _candidateName;
    private readonly System.Windows.Forms.Timer _resizeRefreshTimer;
    private Size _lastAppliedDesktopSize;

    public MachineEntry Machine { get; }
    public CredentialProfile? Credential { get; }
    public RdpActiveXHost ActiveXControl { get; }
    public Control Control => ActiveXControl;
    public bool IsConnected => ConnectedState() != 0;

    public RdpSessionHost(MachineEntry machine, CredentialProfile? credential)
    {
        Machine = machine;
        Credential = credential;

        var candidate = RdpClientCandidates().FirstOrDefault()
            ?? throw new InvalidOperationException("Kein registriertes Microsoft RDP ActiveX Control gefunden.");

        _candidateName = candidate.Name;
        ActiveXControl = CreateRdpControl(candidate.Clsid);
        _resizeRefreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 250,
        };
        _resizeRefreshTimer.Tick += (_, _) => RefreshResizeAfterLayout();
    }

    public void Connect()
    {
        if (IsConnected)
        {
            return;
        }

        ConfigureAndConnect();
    }

    public void Reconnect()
    {
        Disconnect();
        Connect();
    }

    public void Disconnect()
    {
        if (ConnectedState() != 0)
        {
            TryInvoke(ActiveXControl.OcxObject, "Disconnect");
        }
    }

    public void ResizeToHost()
    {
        if (ActiveXControl.Parent is null)
        {
            return;
        }

        var displayRectangle = ActiveXControl.Parent.DisplayRectangle;
        ActiveXControl.Dock = DockStyle.None;
        ActiveXControl.Location = displayRectangle.Location;
        ActiveXControl.Size = displayRectangle.Size;
        ActiveXControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        if (IsConnected)
        {
            UpdateRemoteDisplaySettings();
            _resizeRefreshTimer.Stop();
            _resizeRefreshTimer.Start();
        }
    }

    public void Dispose()
    {
        Disconnect();
        _resizeRefreshTimer.Dispose();
        ActiveXControl.Dispose();
    }

    private void ConfigureAndConnect()
    {
        ResizeToHost();

        var ocx = ActiveXControl.OcxObject;
        var desktopSize = DesktopSizeForHost();
        _lastAppliedDesktopSize = desktopSize;
        SetProperty(ocx, "Server", Machine.DnsName.Trim());
        SetProperty(ocx, "UserName", RdpConnectionProfile.BuildUsername(Machine, Credential));
        SetProperty(ocx, "ColorDepth", 32);
        SetProperty(ocx, "DesktopWidth", desktopSize.Width);
        SetProperty(ocx, "DesktopHeight", desktopSize.Height);

        var advancedSettings = GetAdvancedSettings(ocx);
        var advancedSettings2 = GetProperty(ocx, "AdvancedSettings2") ?? advancedSettings;
        SetProperty(advancedSettings, "EnableCredSspSupport", true);
        SetProperty(advancedSettings, "AuthenticationLevel", 0);
        SetProperty(advancedSettings2, "SmartSizing", true);
        SetProperty(advancedSettings, "RedirectClipboard", Machine.RedirectClipboard);
        SetProperty(advancedSettings, "RedirectPrinters", Machine.RedirectPrinters);
        SetProperty(advancedSettings, "RedirectSmartCards", Machine.RedirectSmartCards);
        SetProperty(advancedSettings, "RedirectWebAuthn", Machine.RedirectWebAuthn);
        SetProperty(advancedSettings2, "RedirectWebAuthn", Machine.RedirectWebAuthn);
        SetProperty(advancedSettings, "DisplayConnectionBar", false);
        SetProperty(advancedSettings, "PinConnectionBar", false);

        if (Credential is not null && !string.IsNullOrWhiteSpace(Credential.ProtectedPassword))
        {
            SetProperty(advancedSettings2 ?? advancedSettings ?? ocx, "ClearTextPassword", CredentialVault.Unprotect(Credential.ProtectedPassword));
        }

        Invoke(ocx, "Connect");
        ResizeToHost();
    }

    private void UpdateRemoteDisplaySettings()
    {
        var desktopSize = DesktopSizeForHost();
        if (Math.Abs(desktopSize.Width - _lastAppliedDesktopSize.Width) < 16 &&
            Math.Abs(desktopSize.Height - _lastAppliedDesktopSize.Height) < 16)
        {
            return;
        }

        var ocx = ActiveXControl.OcxObject;
        var advancedSettings = GetAdvancedSettings(ocx);
        var advancedSettings2 = GetProperty(ocx, "AdvancedSettings2") ?? advancedSettings;
        ApplyDisplaySettings(ocx, advancedSettings2, desktopSize, forceSmartSizingRefresh: false);
        _lastAppliedDesktopSize = desktopSize;
        ActiveXControl.Invalidate();
    }

    private void RefreshResizeAfterLayout()
    {
        _resizeRefreshTimer.Stop();
        if (!IsConnected)
        {
            return;
        }

        var desktopSize = DesktopSizeForHost();
        var ocx = ActiveXControl.OcxObject;
        var advancedSettings = GetAdvancedSettings(ocx);
        var advancedSettings2 = GetProperty(ocx, "AdvancedSettings2") ?? advancedSettings;
        ApplyDisplaySettings(ocx, advancedSettings2, desktopSize, forceSmartSizingRefresh: true);
        _lastAppliedDesktopSize = desktopSize;
        ActiveXControl.Invalidate();
    }

    private static void ApplyDisplaySettings(object ocx, object? advancedSettings2, Size desktopSize, bool forceSmartSizingRefresh)
    {
        if (forceSmartSizingRefresh)
        {
            SetProperty(advancedSettings2, "SmartSizing", false);
        }

        SetProperty(ocx, "DesktopWidth", desktopSize.Width);
        SetProperty(ocx, "DesktopHeight", desktopSize.Height);
        SetProperty(advancedSettings2, "SmartSizing", true);
        TryInvoke(ocx, "UpdateSessionDisplaySettings", (uint)desktopSize.Width, (uint)desktopSize.Height, 0, 0, 0, 100, 100);
    }

    private object? GetAdvancedSettings(object ocx)
    {
        return GetProperty(ocx, "AdvancedSettings9")
            ?? GetProperty(ocx, "AdvancedSettings8")
            ?? GetProperty(ocx, "AdvancedSettings7")
            ?? GetProperty(ocx, "AdvancedSettings");
    }

    private int ConnectedState()
    {
        var connected = GetProperty(ActiveXControl.OcxObject, "Connected");
        return connected is null ? 0 : Convert.ToInt32(connected);
    }

    private static RdpActiveXHost CreateRdpControl(string clsid)
    {
        var control = new RdpActiveXHost(clsid);
        ((ISupportInitialize)control).BeginInit();
        control.Dock = DockStyle.None;
        control.Enabled = true;
        ((ISupportInitialize)control).EndInit();
        return control;
    }

    private Size DesktopSizeForHost()
    {
        var host = ActiveXControl.Parent?.DisplayRectangle.Size ?? ActiveXControl.Size;
        if (host.Width <= 0 || host.Height <= 0)
        {
            var screen = Screen.FromControl(ActiveXControl);
            return new Size(Math.Max(screen.Bounds.Width, 1024), Math.Max(screen.Bounds.Height, 768));
        }

        var screenBounds = Screen.FromControl(ActiveXControl).Bounds;
        var width = Math.Max(host.Width, 1024);
        var height = (int)Math.Round(width * (host.Height / (double)host.Width));

        if (height < 768)
        {
            height = 768;
            width = (int)Math.Round(height * (host.Width / (double)host.Height));
        }

        width = Math.Min(width, Math.Max(screenBounds.Width, 1024));
        height = Math.Min(height, Math.Max(screenBounds.Height, 768));
        return new Size(width, height);
    }

    private static IEnumerable<RdpClientCandidate> RdpClientCandidates()
    {
        string[] preferredProgIds =
        [
            "MsTscAx.MsTscAx.10",
            "MsTscAx.MsTscAx.9",
            "MsTscAx.MsTscAx.8",
            "MsTscAx.MsTscAx.7",
            "MsTscAx.MsTscAx.6",
            "MsTscAx.MsTscAx.5",
            "MsTscAx.MsTscAx",
        ];

        var seenClsids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var progId in preferredProgIds)
        {
            var clsid = ClsidFromProgId(progId);
            if (!string.IsNullOrWhiteSpace(clsid) && IsRegisteredClsid(clsid) && seenClsids.Add(clsid))
            {
                yield return new RdpClientCandidate(progId, clsid);
            }
        }

        string[] fallbackClsids =
        [
            "8B918B82-7985-4C24-89DF-C33AD2BBFBCD",
            "A3BC03A0-041D-42E3-AD22-882B7865C9C5",
            "6AE29350-321B-42be-BBE5-12FB5270C0DE",
            "4eb2f086-c818-447e-b32c-c51ce2b30d31",
        ];

        foreach (var clsid in fallbackClsids)
        {
            if (IsRegisteredClsid(clsid) && seenClsids.Add(clsid))
            {
                yield return new RdpClientCandidate($"CLSID {clsid}", clsid);
            }
        }
    }

    private static string? ClsidFromProgId(string progId)
    {
        using var key = Registry.ClassesRoot.OpenSubKey($@"{progId}\CLSID");
        return key?.GetValue(null)?.ToString()?.Trim('{', '}');
    }

    private static bool IsRegisteredClsid(string clsid)
    {
        using var key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{{{clsid.Trim('{', '}')}}}\InprocServer32");
        return key is not null;
    }

    private static object? GetProperty(object target, string name)
    {
        try
        {
            return target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);
        }
        catch
        {
            return null;
        }
    }

    private static void SetProperty(object? target, string name, object value)
    {
        if (target is null)
        {
            return;
        }

        try
        {
            target.GetType().InvokeMember(name, BindingFlags.SetProperty, null, target, [value]);
        }
        catch
        {
            // RDP control versions differ; unsupported optional settings are ignored.
        }
    }

    private static object? TryInvoke(object target, string name, params object[] args)
    {
        try
        {
            return target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args);
        }
        catch
        {
            return null;
        }
    }

    private static object? Invoke(object target, string name, params object[] args)
    {
        try
        {
            return target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Das Microsoft RDP ActiveX Control konnte \"{name}\" nicht ausführen.", ex);
        }
    }

    private sealed record RdpClientCandidate(string Name, string Clsid);
}

public sealed class RdpActiveXHost : AxHost
{
    public RdpActiveXHost(string clsid) : base(clsid)
    {
    }

    public object OcxObject => GetOcx();
}
