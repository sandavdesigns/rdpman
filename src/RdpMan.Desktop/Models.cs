using System.Text.Json.Serialization;

namespace RdpMan.Desktop;

public sealed class AppData
{
    public List<MachineEntry> Machines { get; set; } = [];
    public List<MachineGroup> Groups { get; set; } = [];
    public List<CredentialProfile> Credentials { get; set; } = [];
    public List<Guid> AutoReconnectMachineIds { get; set; } = [];
    public Guid? GlobalCredentialProfileId { get; set; }
    public Guid? QuickConnectCredentialProfileId { get; set; }
    public bool GlobalRedirectClipboard { get; set; }
    public bool GlobalRedirectPrinters { get; set; }
    public bool GlobalRedirectSmartCards { get; set; }
    public bool GlobalRedirectWebAuthn { get; set; }
    public bool RememberConnectedSessions { get; set; } = true;
    public bool RestoreConnectedSessionsOnStart { get; set; } = true;
    public bool SetupWizardCompleted { get; set; }
}

public sealed class MachineGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ColorKey { get; set; } = "blue";
    public Guid? CredentialProfileId { get; set; }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Ohne Gruppe" : Name;
}

public sealed class MachineEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string DnsName { get; set; } = "";
    public string LastKnownIpAddress { get; set; } = "";
    public Guid? GroupId { get; set; }
    public string GroupName { get; set; } = "";
    public string ColorKey { get; set; } = "";
    public string? Notes { get; set; }
    public Guid? CredentialProfileId { get; set; }
    public bool IsFavorite { get; set; }
    public bool? UseGlobalRedirectSettings { get; set; }
    public bool RedirectClipboard { get; set; }
    public bool RedirectPrinters { get; set; }
    public bool RedirectSmartCards { get; set; }
    public bool RedirectWebAuthn { get; set; }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? DnsName : Name;

    [JsonIgnore]
    public bool IsTemporary { get; set; }

    [JsonIgnore]
    public string ConnectionHost { get; set; } = "";
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
