namespace RdpMan.Desktop;

internal static class Brand
{
    public const string AppName = "RDPMan";
    public const string AppSubtitle = "Remote Connection Manager";
    public const string ExecutableName = "RDPMan";
    public const string PackageName = "RDPMan";

    // Keep the existing folder so updates and a possible rollback keep all machines and credentials.
    public const string LegacyDataDirectoryName = "RDP Man";
    public const string BackupFilter = "RDPMan Backup (*.json)|*.json|Alle Dateien (*.*)|*.*";
}
