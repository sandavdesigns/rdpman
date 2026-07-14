namespace RdpMan.Desktop;

public interface IRemoteSessionHost : IDisposable
{
    MachineEntry Machine { get; }
    CredentialProfile? Credential { get; }
    Control Control { get; }
    bool IsConnected { get; }
    void Connect();
    void Reconnect();
    void Disconnect();
    bool TryRequestInteractiveLogOff();
    void ResizeToHost();
}
