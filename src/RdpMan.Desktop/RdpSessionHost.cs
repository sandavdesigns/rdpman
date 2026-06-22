using Microsoft.Win32;
using System.Reflection;

namespace RdpMan.Desktop;

public sealed class RdpSessionHost : IDisposable
{
    public MachineEntry Machine { get; }
    public CredentialProfile? Credential { get; }
    public RdpActiveXHost Control { get; }
    public bool IsConnected => GetConnectedState() != 0;

    public RdpSessionHost(MachineEntry machine, CredentialProfile? credential)
    {
        Machine = machine;
        Credential = credential;
        Control = new RdpActiveXHost(ResolveRdpClientClsid());
        ((System.ComponentModel.ISupportInitialize)Control).BeginInit();
        Control.Dock = DockStyle.Fill;
        Control.Enabled = true;
        ((System.ComponentModel.ISupportInitialize)Control).EndInit();
    }

    public void Connect()
    {
        if (IsConnected)
        {
            return;
        }

        var ocx = Control.OcxObject;
        SetProperty(ocx, "Server", Machine.DnsName);
        SetProperty(ocx, "UserName", BuildUsername(Credential));

        var advancedSettings = GetProperty(ocx, "AdvancedSettings9") ?? GetProperty(ocx, "AdvancedSettings8") ?? GetProperty(ocx, "AdvancedSettings");
        if (advancedSettings is not null)
        {
            SetProperty(advancedSettings, "EnableCredSspSupport", true);
            SetProperty(advancedSettings, "RedirectClipboard", true);
            SetProperty(advancedSettings, "RedirectPrinters", false);
            SetProperty(advancedSettings, "SmartSizing", true);
            SetProperty(advancedSettings, "AuthenticationLevel", 2);
        }

        if (Credential is not null && !string.IsNullOrWhiteSpace(Credential.ProtectedPassword))
        {
            SetProperty(advancedSettings ?? ocx, "ClearTextPassword", CredentialVault.Unprotect(Credential.ProtectedPassword));
        }

        Invoke(ocx, "Connect");
    }

    public void Reconnect()
    {
        Disconnect();
        Connect();
    }

    public void Disconnect()
    {
        if (GetConnectedState() != 0)
        {
            Invoke(Control.OcxObject, "Disconnect");
        }
    }

    public void ResizeToHost()
    {
        if (Control.Parent is null)
        {
            return;
        }

        Control.Width = Control.Parent.ClientSize.Width;
        Control.Height = Control.Parent.ClientSize.Height;
        if (GetConnectedState() != 0)
        {
            Invoke(
                Control.OcxObject,
                "UpdateSessionDisplaySettings",
                (uint)Math.Max(Control.Width, 800),
                (uint)Math.Max(Control.Height, 600),
                0,
                0,
                0,
                100,
                100);
        }
    }

    public void Dispose()
    {
        Disconnect();
        Control.Dispose();
    }

    private static string BuildUsername(CredentialProfile? credential)
    {
        if (credential is null)
        {
            return "";
        }

        return string.IsNullOrWhiteSpace(credential.Domain)
            ? credential.Username
            : $@"{credential.Domain}\{credential.Username}";
    }

    private int GetConnectedState()
    {
        var connected = GetProperty(Control.OcxObject, "Connected");
        return connected is null ? 0 : Convert.ToInt32(connected);
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
            // Older RDP controls do not expose every setting. The connection can still proceed.
        }
    }

    private static object? Invoke(object target, string name, params object[] args)
    {
        return target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args);
    }

    private static string ResolveRdpClientClsid()
    {
        string[] progIds =
        [
            "MsTscAx.MsRdpClient12NotSafeForScripting",
            "MsTscAx.MsRdpClient11NotSafeForScripting",
            "MsTscAx.MsRdpClient10NotSafeForScripting",
            "MsTscAx.MsRdpClient9NotSafeForScripting",
            "MsTscAx.MsRdpClient8NotSafeForScripting",
            "MsTscAx.MsRdpClient7NotSafeForScripting",
            "MsTscAx.MsRdpClient6NotSafeForScripting",
            "MsTscAx.MsRdpClient5NotSafeForScripting",
            "MsTscAx.MsRdpClient4NotSafeForScripting",
            "MsTscAx.MsRdpClient3NotSafeForScripting",
            "MsTscAx.MsRdpClient2NotSafeForScripting",
            "MsTscAx.MsRdpClientNotSafeForScripting",
        ];

        foreach (var progId in progIds)
        {
            using var key = Registry.ClassesRoot.OpenSubKey($@"{progId}\CLSID");
            var clsid = key?.GetValue(null)?.ToString();
            if (!string.IsNullOrWhiteSpace(clsid))
            {
                return clsid.Trim('{', '}');
            }
        }

        throw new InvalidOperationException("Microsoft RDP ActiveX control was not found on this Windows installation.");
    }
}

public sealed class RdpActiveXHost : AxHost
{
    public RdpActiveXHost(string clsid) : base(clsid)
    {
    }

    public object OcxObject => GetOcx();
}
