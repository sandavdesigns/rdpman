using System.Text.Json;

namespace RdpMan.Desktop;

public sealed class DataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string DataDirectory { get; }
    public string DataFile { get; }

    public DataStore()
    {
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RDP Man");
        DataFile = Path.Combine(DataDirectory, "rdpman.json");
    }

    public AppData Load()
    {
        Directory.CreateDirectory(DataDirectory);
        if (!File.Exists(DataFile))
        {
            return new AppData();
        }

        var json = File.ReadAllText(DataFile);
        return JsonSerializer.Deserialize<AppData>(json, JsonOptions) ?? new AppData();
    }

    public void Save(AppData data)
    {
        Directory.CreateDirectory(DataDirectory);
        var tmpFile = DataFile + ".tmp";
        File.WriteAllText(tmpFile, JsonSerializer.Serialize(data, JsonOptions));
        File.Move(tmpFile, DataFile, overwrite: true);
    }
}

