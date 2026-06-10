using System.Collections.ObjectModel;
using System.Text.Json;
using Nopad.Models;

namespace Nopad.Services;

public sealed class RecoveryService
{
    private readonly string _root;
    private readonly string _tabsRoot;
    private readonly string _manifestPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public RecoveryService()
    {
        _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nopad", "Recovery");
        _tabsRoot = Path.Combine(_root, "tabs");
        _manifestPath = Path.Combine(_root, "session.json");
        Directory.CreateDirectory(_tabsRoot);
    }

    public async Task<(List<EditorTab> Tabs, string? ActiveTabId, int NextUntitledNumber, string Message)> LoadAsync()
    {
        if (!File.Exists(_manifestPath))
        {
            return ([], null, 1, "");
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<EditorSessionManifest>(await File.ReadAllTextAsync(_manifestPath), _jsonOptions);
            if (manifest is null)
            {
                return ([], null, 1, "Recovery manifest was empty.");
            }

            var tabs = new List<EditorTab>();
            foreach (var record in manifest.Tabs)
            {
                var contentPath = Path.Combine(_root, record.RecoveryPath);
                var text = File.Exists(contentPath)
                    ? await File.ReadAllTextAsync(contentPath)
                    : await TryReadCleanFileAsync(record);

                tabs.Add(new EditorTab
                {
                    Id = record.Id,
                    FilePath = record.FilePath,
                    Title = record.IsDirty ? $"Recovered: {record.Title}" : record.Title,
                    Text = text,
                    IsDirty = record.IsDirty,
                    Syntax = record.Syntax,
                    WordWrap = record.WordWrap,
                    ShowLineNumbers = record.ShowLineNumbers,
                    CursorLine = record.CursorLine,
                    CursorColumn = record.CursorColumn,
                    VerticalOffset = record.VerticalOffset,
                    DiskLastWriteUtc = record.DiskLastWriteUtc
                });
            }

            return (tabs, manifest.ActiveTabId, Math.Max(1, manifest.NextUntitledNumber), tabs.Any(t => t.IsDirty) ? "Recovered unsaved work." : "");
        }
        catch
        {
            var tabs = new List<EditorTab>();
            foreach (var blob in Directory.EnumerateFiles(_tabsRoot, "*.txt"))
            {
                tabs.Add(new EditorTab
                {
                    Title = $"Recovered: {Path.GetFileNameWithoutExtension(blob)}",
                    Text = await File.ReadAllTextAsync(blob),
                    IsDirty = true
                });
            }

            return (tabs, tabs.FirstOrDefault()?.Id, 1, "Recovery manifest was corrupted; restored available content blobs.");
        }
    }

    public async Task SaveAsync(IEnumerable<EditorTab> tabs, EditorTab? activeTab, int nextUntitledNumber)
    {
        Directory.CreateDirectory(_tabsRoot);
        var records = new List<RecoveryTabRecord>();

        foreach (var tab in tabs)
        {
            var blobName = $"{tab.Id}.txt";
            await AtomicWriteAsync(Path.Combine(_tabsRoot, blobName), tab.Text);
            records.Add(new RecoveryTabRecord
            {
                Id = tab.Id,
                FilePath = tab.FilePath,
                Title = tab.Title.Replace("Recovered: ", "", StringComparison.Ordinal),
                RecoveryPath = $"tabs/{blobName}",
                IsDirty = tab.IsDirty,
                Syntax = tab.Syntax,
                WordWrap = tab.WordWrap,
                ShowLineNumbers = tab.ShowLineNumbers,
                CursorLine = tab.CursorLine,
                CursorColumn = tab.CursorColumn,
                VerticalOffset = tab.VerticalOffset,
                DiskLastWriteUtc = tab.DiskLastWriteUtc
            });
        }

        var manifest = new EditorSessionManifest
        {
            ActiveTabId = activeTab?.Id,
            NextUntitledNumber = nextUntitledNumber,
            Tabs = records
        };

        await AtomicWriteAsync(_manifestPath, JsonSerializer.Serialize(manifest, _jsonOptions));
    }

    public void RemoveBlob(EditorTab tab)
    {
        var path = Path.Combine(_tabsRoot, $"{tab.Id}.txt");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static async Task<string> TryReadCleanFileAsync(RecoveryTabRecord record)
    {
        if (!record.IsDirty && record.FilePath is not null && File.Exists(record.FilePath))
        {
            return await File.ReadAllTextAsync(record.FilePath);
        }

        return "";
    }

    private static async Task AtomicWriteAsync(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.tmp";
        await File.WriteAllTextAsync(temp, content);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(temp, path);
    }
}
