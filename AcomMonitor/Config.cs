using System.Text.Json;

namespace AcomMonitor;

public sealed class Config
{
    public string AmplifierUrl { get; set; } = "http://192.168.1.68/";

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AcomMonitor",
        "config.json"
    );

    public static Config Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<Config>(json);
                if (config != null) return config;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load config: {ex.Message}");
        }

        // Return default config if file doesn't exist or failed to load
        var defaultConfig = new Config();
        defaultConfig.Save(); // Save default config for future use
        return defaultConfig;
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save config: {ex.Message}");
        }
    }
}
