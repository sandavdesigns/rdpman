namespace RdpMan.Desktop;

internal static class Brand
{
    public const string AppName = "Lord of the Pings";
    public const string AppSubtitle = "Remote Desktop Manager";
    public const string ExecutableName = "LordOfThePings";
    public const string PackageName = "LordOfThePings";

    // Keep the existing folder so updates and a possible rollback keep all machines and credentials.
    public const string LegacyDataDirectoryName = "RDP Man";
    public const string BackupFilter = "Lord of the Pings Backup (*.json)|*.json|Alle Dateien (*.*)|*.*";
}
