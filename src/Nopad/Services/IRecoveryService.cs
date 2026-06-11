using Nopad.Models;

namespace Nopad.Services;

public interface IRecoveryService
{
    string RecoveryDirectory { get; }
    Task<EditorSessionManifest?> LoadManifestAsync();
    Task SaveManifestAsync(EditorSessionManifest manifest);
    Task SaveTabContentAsync(string tabId, string content);
    Task<string?> LoadTabContentAsync(string recoveryPath);
    void DeleteTabContent(string tabId);
}
