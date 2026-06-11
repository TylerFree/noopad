using Noopad.Models;
using Noopad.Services;

namespace Nopad.Tests;

internal sealed class FakeRecoveryService : IRecoveryService
{
    public string RecoveryDirectory { get; } = string.Empty;
    public EditorSessionManifest? Manifest { get; set; }
    public Dictionary<string, string> Contents { get; } = new();
    public int LoadManifestCalls { get; private set; }

    public Task<EditorSessionManifest?> LoadManifestAsync()
    {
        LoadManifestCalls++;
        return Task.FromResult(Manifest);
    }

    public Task SaveManifestAsync(EditorSessionManifest manifest)
    {
        Manifest = manifest;
        return Task.CompletedTask;
    }

    public Task SaveTabContentAsync(string tabId, string content)
    {
        Contents[$"tabs/{tabId}.txt"] = content;
        return Task.CompletedTask;
    }

    public Task<string?> LoadTabContentAsync(string recoveryPath)
    {
        return Task.FromResult(Contents.TryGetValue(recoveryPath, out var content) ? content : null);
    }

    public void DeleteTabContent(string tabId)
    {
        Contents.Remove($"tabs/{tabId}.txt");
    }
}

internal sealed class FakeFormattingService : IFormattingService
{
    public (bool success, string result) FormatJson(string input) => (true, input);
    public (bool success, string result) FormatXml(string input) => (true, input);
}

internal sealed class FakeSyntaxService : ISyntaxService
{
    public SyntaxLanguage DetectFromExtension(string? filePath) => Path.GetExtension(filePath ?? string.Empty).ToLowerInvariant() switch
    {
        ".md" => SyntaxLanguage.Markdown,
        ".json" => SyntaxLanguage.Json,
        _ => SyntaxLanguage.PlainText
    };

    public string GetHighlightingDefinitionName(SyntaxLanguage language) => language.ToString();
}

internal sealed class FakeMarkdownPreviewService : IMarkdownPreviewService
{
    public string RenderToHtml(string markdown) => markdown;
}

internal sealed class FakeSearchReplaceService : ISearchReplaceService
{
    public IEnumerable<(int start, int length)> FindAll(string text, string pattern, bool matchCase, bool wholeWord, bool regex)
        => Enumerable.Empty<(int start, int length)>();

    public string ReplaceAll(string text, string pattern, string replacement, bool matchCase, bool wholeWord, bool regex)
        => text.Replace(pattern, replacement, matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
}

internal sealed class FakeUserSettingsService : IUserSettingsService
{
    public UserSettings Settings { get; } = new();
    public void Save() { }
}
