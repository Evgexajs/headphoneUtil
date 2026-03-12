using System.Text.Json;

namespace BtHeadphonesBattery;

public sealed class AppSettings
{
    public int RefreshSeconds { get; set; } = 60;

    private static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, "Settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();

            string json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);

            if (settings is null)
                return new AppSettings();

            if (settings.RefreshSeconds < 30)
                settings.RefreshSeconds = 30;

            if (settings.RefreshSeconds > 3600)
                settings.RefreshSeconds = 3600;

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(FilePath, json);
    }
}