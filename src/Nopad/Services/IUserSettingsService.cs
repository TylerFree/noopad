namespace Nopad.Services;

public class UserSettings
{
    public bool WordWrap { get; set; } = false;
    public bool ShowLineNumbers { get; set; } = true;
    public string ThemeVariant { get; set; } = "Default";
}

public interface IUserSettingsService
{
    UserSettings Settings { get; }
    void Save();
}
