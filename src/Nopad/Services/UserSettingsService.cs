using System.Text.Json;

namespace Nopad.Services;

public class UserSettingsService : IUserSettingsService
{
    private readonly string _settingsPath;
    public UserSettings Settings { get; private set; }

    public UserSettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "Nopad");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
        Settings = Load();
    }

    private UserSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
        }
        catch { }
        return new UserSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }
}
