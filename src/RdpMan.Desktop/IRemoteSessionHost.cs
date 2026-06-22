namespace RdpMan.Desktop;

public interface IRemoteSessionHost : IDisposable
{
    MachineEntry Machine { get; }
    Control Control { get; }
    bool IsConnected { get; }
    void Connect();
    void Reconnect();
    void Disconnect();
    void ResizeToHost();
}

