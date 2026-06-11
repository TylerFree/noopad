using System.Text.Json;
using Nopad.Models;

namespace Nopad.Services;

public class RecoveryService : IRecoveryService
{
    public string RecoveryDirectory { get; }
    private readonly string _manifestPath;
    private readonly string _tabsDir;
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public RecoveryService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        RecoveryDirectory = Path.Combine(appData, "Nopad", "recovery");
        _tabsDir = Path.Combine(RecoveryDirectory, "tabs");
        Directory.CreateDirectory(_tabsDir);
        _manifestPath = Path.Combine(RecoveryDirectory, "session.json");
    }

    public async Task<EditorSessionManifest?> LoadManifestAsync()
    {
        try
        {
            if (!File.Exists(_manifestPath)) return null;
            var json = await File.ReadAllTextAsync(_manifestPath);
            return JsonSerializer.Deserialize<EditorSessionManifest>(json);
        }
        catch { return null; }
    }

    public async Task SaveManifestAsync(EditorSessionManifest manifest)
    {
        var tmp = _manifestPath + ".tmp";
        var json = JsonSerializer.Serialize(manifest, _jsonOptions);
        await File.WriteAllTextAsync(tmp, json);
        File.Move(tmp, _manifestPath, overwrite: true);
    }

    public async Task SaveTabContentAsync(string tabId, string content)
    {
        var path = Path.Combine(_tabsDir, $"{tabId}.txt");
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }

    public async Task<string?> LoadTabContentAsync(string recoveryPath)
    {
        try
        {
            var fullPath = Path.Combine(RecoveryDirectory, recoveryPath);
            if (!File.Exists(fullPath)) return null;
            return await File.ReadAllTextAsync(fullPath);
        }
        catch { return null; }
    }

    public void DeleteTabContent(string tabId)
    {
        var path = Path.Combine(_tabsDir, $"{tabId}.txt");
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
