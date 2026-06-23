namespace RdpMan.Desktop;

internal static class Brand
{
#if KASTEN3000
    public const string AppName = "Kasten 3000";
    public const string AppSubtitle = "Remote Desktop Manager";
    public const string ExecutableName = "Kasten3000";
    public const string PackageName = "Kasten3000";
    public const string LegacyDataDirectoryName = "RDP Man";
    public const string BackupFilter = "Kasten 3000 Backup (*.json)|*.json|Alle Dateien (*.*)|*.*";
#else
    public const string AppName = "Lord of the Pings";
    public const string AppSubtitle = "Remote Desktop Manager";
    public const string ExecutableName = "LordOfThePings";
    public const string PackageName = "LordOfThePings";

    // Keep the existing folder so updates and a possible rollback keep all machines and credentials.
    public const string LegacyDataDirectoryName = "RDP Man";
    public const string BackupFilter = "Lord of the Pings Backup (*.json)|*.json|Alle Dateien (*.*)|*.*";
#endif
}
