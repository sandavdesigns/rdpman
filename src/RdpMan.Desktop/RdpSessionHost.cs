using AxMSTSCLib;
using MSTSCLib;

namespace RdpMan.Desktop;

public sealed class RdpSessionHost : IDisposable
{
    public MachineEntry Machine { get; }
    public CredentialProfile? Credential { get; }
    public AxMsRdpClient9NotSafeForScripting Control { get; }
    public bool IsConnected { get; private set; }

    public RdpSessionHost(MachineEntry machine, CredentialProfile? credential)
    {
        Machine = machine;
        Credential = credential;
        Control = new AxMsRdpClient9NotSafeForScripting();
        ((System.ComponentModel.ISupportInitialize)Control).BeginInit();
        Control.Dock = DockStyle.Fill;
        Control.Enabled = true;
        ((System.ComponentModel.ISupportInitialize)Control).EndInit();

        Control.OnConnected += (_, _) => IsConnected = true;
        Control.OnDisconnected += (_, _) => IsConnected = false;
    }

    public void Connect()
    {
        if (IsConnected)
        {
            return;
        }

        Control.Server = Machine.DnsName;
        Control.UserName = BuildUsername(Credential);
        Control.AdvancedSettings9.EnableCredSspSupport = true;
        Control.AdvancedSettings9.RedirectClipboard = true;
        Control.AdvancedSettings9.RedirectPrinters = false;
        Control.AdvancedSettings9.SmartSizing = true;
        Control.AdvancedSettings9.AuthenticationLevel = 2;

        if (Credential is not null && !string.IsNullOrWhiteSpace(Credential.ProtectedPassword))
        {
            Control.AdvancedSettings9.ClearTextPassword = CredentialVault.Unprotect(Credential.ProtectedPassword);
        }

        Control.Connect();
    }

    public void Reconnect()
    {
        Disconnect();
        Connect();
    }

    public void Disconnect()
    {
        if (Control.Connected != 0)
        {
            Control.Disconnect();
        }
        IsConnected = false;
    }

    public void ResizeToHost()
    {
        if (Control.Parent is null)
        {
            return;
        }

        Control.Width = Control.Parent.ClientSize.Width;
        Control.Height = Control.Parent.ClientSize.Height;
        if (Control.Connected != 0)
        {
            Control.UpdateSessionDisplaySettings(
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
}

