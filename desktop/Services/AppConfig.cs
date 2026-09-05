using System.IO;
using System.Text.Json;

namespace BlocksPlant.Desktop.Services;

public class AppConfig
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5118";

    /// <summary>Blazor owner dashboard URL (default http profile).</summary>
    public string WebUrl { get; set; } = "http://localhost:5137";

    /// <summary>When true, Desktop starts the API if it is not already listening.</summary>
    public bool AutoStartApi { get; set; } = true;

    /// <summary>When true, Desktop starts the Blazor web app if it is not already listening.</summary>
    public bool AutoStartWeb { get; set; } = true;

    private static string ConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BlocksPlant", "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch
        {
            // ignore and use defaults
        }

        return new AppConfig();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(ConfigPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
