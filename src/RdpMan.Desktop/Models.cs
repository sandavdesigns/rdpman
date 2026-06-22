using System.Text.Json.Serialization;

namespace RdpMan.Desktop;

public sealed class AppData
{
    public List<MachineEntry> Machines { get; set; } = [];
    public List<CredentialProfile> Credentials { get; set; } = [];
    public List<Guid> AutoReconnectMachineIds { get; set; } = [];
    public Guid? QuickConnectCredentialProfileId { get; set; }
}

public sealed class MachineEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string DnsName { get; set; } = "";
    public string? Notes { get; set; }
    public Guid? CredentialProfileId { get; set; }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? DnsName : Name;

    [JsonIgnore]
    public bool IsTemporary { get; set; }
}

public sealed class CredentialProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Username { get; set; } = "";
    public string ProtectedPassword { get; set; } = "";

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var user = string.IsNullOrWhiteSpace(Domain) ? Username : $"{Domain}\\{Username}";
            return string.IsNullOrWhiteSpace(Label) ? user : $"{Label} ({user})";
        }
    }
}
