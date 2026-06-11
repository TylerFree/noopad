namespace Noopad.Services;

public class UserSettings
{
    public bool WordWrap { get; set; } = false;
    public bool ShowLineNumbers { get; set; } = true;
    public string ThemeVariant { get; set; } = "Dark";
    public double FontSize { get; set; } = 14;
    public string FontFamily { get; set; } = "Cascadia Code,Consolas,Courier New,monospace";
}

public interface IUserSettingsService
{
    UserSettings Settings { get; }
    void Save();
}