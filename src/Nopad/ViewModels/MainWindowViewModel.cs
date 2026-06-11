using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Noopad.Models;
using Noopad.Services;

namespace Noopad.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IRecoveryService _recovery;
    private readonly IFormattingService _formatting;
    private readonly ISyntaxService _syntax;
    private readonly IMarkdownPreviewService _markdown;
    private readonly ISearchReplaceService _search;
    private readonly IUserSettingsService _settings;
    private readonly IReadOnlyList<string> _startupFilePaths;
    public IUserSettingsService Settings => _settings;

    public IFileDialogService? FileDialog { get; set; }
    public Func<string, Task<bool>>? CreateMissingFileHandler { get; set; }

    [ObservableProperty] private ObservableCollection<EditorTabViewModel> _tabs = new();
    [ObservableProperty] private EditorTabViewModel? _activeTab;
    [ObservableProperty] private int _nextUntitledNumber = 1;
    [ObservableProperty] private string _statusMessage = "Ready";

    public SearchReplacePanelViewModel SearchPanel { get; } = new();

    private System.Timers.Timer? _recoveryTimer;
    private bool _recoveryPending;

    public MainWindowViewModel(
        IRecoveryService recovery,
        IFormattingService formatting,
        ISyntaxService syntax,
        IMarkdownPreviewService markdown,
        ISearchReplaceService search,
        IUserSettingsService settings,
        IEnumerable<string>? startupFilePaths = null)
    {
        _recovery = recovery;
        _formatting = formatting;
        _syntax = syntax;
        _markdown = markdown;
        _search = search;
        _settings = settings;
        _startupFilePaths = startupFilePaths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray() ?? Array.Empty<string>();

        SearchPanel.FindNextRequested += OnFindNext;
        SearchPanel.FindPreviousRequested += OnFindPrevious;
        SearchPanel.ReplaceCurrentRequested += OnReplaceCurrent;
        SearchPanel.ReplaceAllRequested += OnReplaceAll;

        _recoveryTimer = new System.Timers.Timer(2000) { AutoReset = false };
        _recoveryTimer.Elapsed += async (_, _) =>
        {
            if (_recoveryPending)
            {
                _recoveryPending = false;
                await SaveRecoveryAsync();
            }
        };
    }

    public async Task InitializeAsync()
    {
        if (await OpenStartupFilePathsAsync(_startupFilePaths, promptToCreate: true))
            return;

        var manifest = await _recovery.LoadManifestAsync();
        if (manifest != null && manifest.Tabs.Count > 0)
        {
            NextUntitledNumber = manifest.NextUntitledNumber;
            foreach (var record in manifest.Tabs)
                Tabs.Add(await CreateTabFromRecordAsync(record));

            ActiveTab = Tabs.FirstOrDefault(t => t.Id == manifest.ActiveTabId) ?? Tabs.First();
            StatusMessage = $"Restored {Tabs.Count} document{(Tabs.Count == 1 ? string.Empty : "s")}";
        }
        else
        {
            CreateNewTab();
        }
    }

    private async Task<EditorTabViewModel> CreateTabFromRecordAsync(RecoveryTabRecord record)
    {
        var tab = new EditorTabViewModel(record.Id)
        {
            Title = record.Title,
            FilePath = record.FilePath,
            IsDirty = record.IsDirty,
            Syntax = Enum.TryParse<SyntaxLanguage>(record.Syntax, true, out var lang) ? lang : SyntaxLanguage.PlainText,
            WordWrap = record.WordWrap,
            ShowLineNumbers = record.ShowLineNumbers,
            CursorLine = record.CursorLine,
            CursorColumn = record.CursorColumn,
            VerticalOffset = record.VerticalOffset,
            FileLastWriteTime = record.FileLastWriteTime
        };
        await LoadTabContentAsync(tab, record);
        return tab;
    }

    private async Task LoadTabContentAsync(EditorTabViewModel tab, RecoveryTabRecord record)
    {
        string? content = null;

        if (record.IsDirty)
            content = await _recovery.LoadTabContentAsync(record.RecoveryPath);

        if (content == null && record.FilePath != null && File.Exists(record.FilePath))
        {
            var diskTime = File.GetLastWriteTimeUtc(record.FilePath);
            content = await File.ReadAllTextAsync(record.FilePath);
            tab.FileLastWriteTime = diskTime;
        }

        if (content == null)
            content = await _recovery.LoadTabContentAsync(record.RecoveryPath) ?? string.Empty;

        tab.Content = content;
        tab.IsDirty = record.IsDirty;
        UpdateMarkdownPreview(tab);
    }

    [RelayCommand]
    private void NewTab() => CreateNewTab();

    public void CreateNewTab()
    {
        var id = $"tab-{Guid.NewGuid():N}";
        var tab = new EditorTabViewModel(id)
        {
            Title = $"Unfile-{NextUntitledNumber}",
            ShowLineNumbers = _settings.Settings.ShowLineNumbers,
            WordWrap = _settings.Settings.WordWrap
        };
        NextUntitledNumber++;
        Tabs.Add(tab);
        ActiveTab = tab;
        ScheduleRecovery();
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        if (FileDialog == null) return;
        var path = await FileDialog.OpenFileAsync();
        if (path == null) return;

        await OpenDocumentAsync(path);
    }

    public async Task<bool> OpenStartupFilePathsAsync(IEnumerable<string> paths, bool promptToCreate)
    {
        var openedAny = false;
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
            openedAny |= await OpenDocumentAsync(path, promptToCreate);

        return openedAny;
    }

    public async Task<bool> OpenDocumentAsync(string path, bool promptToCreate = false)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            if (!promptToCreate || !await ShouldCreateMissingFileAsync(fullPath))
            {
                StatusMessage = $"File not found: {fullPath}";
                return false;
            }

            if (!TryCreateMissingFile(fullPath))
                return false;
        }

        var existing = Tabs.FirstOrDefault(t => t.FilePath != null && PathsEqual(t.FilePath, fullPath));
        if (existing != null)
        {
            ActiveTab = existing;
            StatusMessage = $"Switched to {existing.Title}";
            return true;
        }

        var content = await File.ReadAllTextAsync(fullPath);
        var id = $"tab-{Guid.NewGuid():N}";
        var syntax = _syntax.DetectFromExtension(fullPath);
        var tab = new EditorTabViewModel(id)
        {
            FilePath = fullPath,
            Title = Path.GetFileName(fullPath),
            Syntax = syntax,
            ShowLineNumbers = _settings.Settings.ShowLineNumbers,
            WordWrap = _settings.Settings.WordWrap,
            FileLastWriteTime = File.GetLastWriteTimeUtc(fullPath)
        };
        tab.MarkClean(content);
        Tabs.Add(tab);
        ActiveTab = tab;
        ScheduleRecovery();
        return true;
    }

    private async Task<bool> ShouldCreateMissingFileAsync(string fullPath)
    {
        return CreateMissingFileHandler != null && await CreateMissingFileHandler(fullPath);
    }

    private bool TryCreateMissingFile(string fullPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(fullPath, string.Empty);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            StatusMessage = $"Could not create file: {ex.Message}";
            return false;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (ActiveTab == null) return;
        await SaveTabAsync(ActiveTab);
    }

    [RelayCommand]
    private async Task SaveAs()
    {
        if (ActiveTab == null || FileDialog == null) return;
        var path = await FileDialog.SaveFileAsync(ActiveTab.FilePath, ActiveTab.Title);
        if (path == null) return;
        ActiveTab.FilePath = path;
        ActiveTab.Title = Path.GetFileName(path);
        ActiveTab.Syntax = _syntax.DetectFromExtension(path);
        await SaveTabAsync(ActiveTab);
    }

    [RelayCommand]
    private async Task SaveAll()
    {
        foreach (var tab in Tabs.Where(t => t.IsDirty).ToList())
            await SaveTabAsync(tab);
    }

    public async Task SaveTabAsync(EditorTabViewModel tab)
    {
        if (tab.FilePath == null)
        {
            if (FileDialog == null) return;
            var path = await FileDialog.SaveFileAsync(null, tab.Title);
            if (path == null) return;
            tab.FilePath = path;
            tab.Title = Path.GetFileName(path);
            tab.Syntax = _syntax.DetectFromExtension(path);
        }
        await File.WriteAllTextAsync(tab.FilePath, tab.Content);
        tab.FileLastWriteTime = File.GetLastWriteTimeUtc(tab.FilePath);
        tab.IsDirty = false;
        await _recovery.SaveTabContentAsync(tab.Id, tab.Content);
        await SaveRecoveryAsync();
        StatusMessage = $"Saved {tab.Title}";
    }

    [RelayCommand]
    private async Task CloseTab(EditorTabViewModel? tab)
    {
        tab ??= ActiveTab;
        if (tab == null) return;

        if (tab.IsDirty)
        {
            var result = await ShowSaveDialogAsync(tab);
            if (result == null) return;
            if (result == true) await SaveTabAsync(tab);
        }

        _recovery.DeleteTabContent(tab.Id);
        var idx = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (Tabs.Count == 0) CreateNewTab();
        else ActiveTab = Tabs[Math.Min(idx, Tabs.Count - 1)];

        await SaveRecoveryAsync();
    }

    [RelayCommand]
    private async Task CloseAllTabs()
    {
        foreach (var tab in Tabs.Where(t => t.IsDirty).ToList())
        {
            var result = await ShowSaveDialogAsync(tab);
            if (result == null) return;
            if (result == true) await SaveTabAsync(tab);
        }
        foreach (var tab in Tabs.ToList())
            _recovery.DeleteTabContent(tab.Id);
        Tabs.Clear();
        NextUntitledNumber = 1;
        CreateNewTab();
        await SaveRecoveryAsync();
    }

    public Func<EditorTabViewModel, Task<bool?>>? SaveDialogHandler { get; set; }

    private Task<bool?> ShowSaveDialogAsync(EditorTabViewModel tab)
    {
        return SaveDialogHandler != null
            ? SaveDialogHandler(tab)
            : Task.FromResult<bool?>(false);
    }

    public void NextTab()
    {
        if (Tabs.Count == 0) return;
        var idx = ActiveTab == null ? 0 : (Tabs.IndexOf(ActiveTab) + 1) % Tabs.Count;
        ActiveTab = Tabs[idx];
    }

    public void PreviousTab()
    {
        if (Tabs.Count == 0) return;
        var idx = ActiveTab == null ? 0 : (Tabs.IndexOf(ActiveTab) - 1 + Tabs.Count) % Tabs.Count;
        ActiveTab = Tabs[idx];
    }

    partial void OnActiveTabChanged(EditorTabViewModel? oldValue, EditorTabViewModel? newValue)
    {
        if (oldValue != null) oldValue.IsActive = false;
        if (newValue != null) newValue.IsActive = true;
    }

    [RelayCommand]
    private void ToggleWordWrap()
    {
        if (ActiveTab == null) return;
        ActiveTab.WordWrap = !ActiveTab.WordWrap;
    }

    [RelayCommand]
    private void ToggleLineNumbers()
    {
        if (ActiveTab == null) return;
        ActiveTab.ShowLineNumbers = !ActiveTab.ShowLineNumbers;
    }

    [RelayCommand]
    private void ToggleMarkdownPreview()
    {
        if (ActiveTab == null) return;
        ActiveTab.ShowMarkdownPreview = !ActiveTab.ShowMarkdownPreview;
        if (ActiveTab.ShowMarkdownPreview)
            UpdateMarkdownPreview(ActiveTab);
    }

    [RelayCommand]
    private void ShowFindPanel()
    {
        SearchPanel.ShowReplace = false;
        SearchPanel.IsVisible = true;
    }

    [RelayCommand]
    private void ShowReplacePanel()
    {
        SearchPanel.ShowReplace = true;
        SearchPanel.IsVisible = true;
    }

    [RelayCommand]
    private void FormatJson()
    {
        if (ActiveTab == null) return;
        var (success, result) = _formatting.FormatJson(ActiveTab.Content);
        if (success) { ActiveTab.Content = result; ActiveTab.IsDirty = true; StatusMessage = "Formatted JSON"; }
        else StatusMessage = result;
    }

    [RelayCommand]
    private void FormatXml()
    {
        if (ActiveTab == null) return;
        var (success, result) = _formatting.FormatXml(ActiveTab.Content);
        if (success) { ActiveTab.Content = result; ActiveTab.IsDirty = true; StatusMessage = "Formatted XML"; }
        else StatusMessage = result;
    }


    public Func<Task>? ShowSettingsHandler { get; set; }

    [RelayCommand]
    private async Task ShowSettings()
    {
        if (ShowSettingsHandler != null)
            await ShowSettingsHandler();
    }

    public void ApplySettingsToAllTabs()
    {
        foreach (var tab in Tabs)
        {
            tab.WordWrap = _settings.Settings.WordWrap;
            tab.ShowLineNumbers = _settings.Settings.ShowLineNumbers;
        }
    }
    public void OnContentChanged(EditorTabViewModel tab, string newContent)
    {
        tab.Content = newContent;
        tab.IsDirty = true;
        if (tab.ShowMarkdownPreview) UpdateMarkdownPreview(tab);
        ScheduleRecovery();
    }

    private void UpdateMarkdownPreview(EditorTabViewModel tab)
    {
        if (tab.Syntax == SyntaxLanguage.Markdown || tab.ShowMarkdownPreview)
            tab.MarkdownHtml = _markdown.RenderToHtml(tab.Content);
    }

    public void OnCursorChanged(EditorTabViewModel tab, int line, int col)
    {
        tab.CursorLine = line;
        tab.CursorColumn = col;
        StatusMessage = GetStatusBarText();
    }

    private int _searchMatchIndex = -1;
    private List<(int start, int length)> _searchMatches = new();

    public event Action<int, int>? SelectTextRequested;

    private void RefreshSearchMatches()
    {
        if (ActiveTab == null || string.IsNullOrEmpty(SearchPanel.SearchText))
        {
            _searchMatches.Clear();
            _searchMatchIndex = -1;
            return;
        }
        _searchMatches = _search.FindAll(
            ActiveTab.Content,
            SearchPanel.SearchText,
            SearchPanel.MatchCase,
            SearchPanel.WholeWord,
            SearchPanel.UseRegex).ToList();
        SearchPanel.StatusMessage = $"{_searchMatches.Count} match(es)";
    }

    private void OnFindNext()
    {
        RefreshSearchMatches();
        if (_searchMatches.Count == 0) return;
        _searchMatchIndex = (_searchMatchIndex + 1) % _searchMatches.Count;
        var m = _searchMatches[_searchMatchIndex];
        SelectTextRequested?.Invoke(m.start, m.length);
    }

    private void OnFindPrevious()
    {
        RefreshSearchMatches();
        if (_searchMatches.Count == 0) return;
        _searchMatchIndex = (_searchMatchIndex - 1 + _searchMatches.Count) % _searchMatches.Count;
        var m = _searchMatches[_searchMatchIndex];
        SelectTextRequested?.Invoke(m.start, m.length);
    }

    private void OnReplaceCurrent()
    {
        if (ActiveTab == null || _searchMatchIndex < 0 || _searchMatchIndex >= _searchMatches.Count) return;
        var (start, length) = _searchMatches[_searchMatchIndex];
        ActiveTab.Content = ActiveTab.Content[..start] + SearchPanel.ReplaceText + ActiveTab.Content[(start + length)..];
        ActiveTab.IsDirty = true;
        RefreshSearchMatches();
    }

    private void OnReplaceAll()
    {
        if (ActiveTab == null) return;
        ActiveTab.Content = _search.ReplaceAll(
            ActiveTab.Content, SearchPanel.SearchText, SearchPanel.ReplaceText,
            SearchPanel.MatchCase, SearchPanel.WholeWord, SearchPanel.UseRegex);
        ActiveTab.IsDirty = true;
        StatusMessage = "Replace All complete";
        RefreshSearchMatches();
    }

    private void ScheduleRecovery()
    {
        _recoveryPending = true;
        _recoveryTimer?.Stop();
        _recoveryTimer?.Start();
    }

    public async Task SaveRecoveryAsync()
    {
        var manifest = new EditorSessionManifest
        {
            ActiveTabId = ActiveTab?.Id,
            NextUntitledNumber = NextUntitledNumber,
            Tabs = Tabs.Select(t => new RecoveryTabRecord
            {
                Id = t.Id,
                FilePath = t.FilePath,
                Title = t.Title,
                RecoveryPath = $"tabs/{t.Id}.txt",
                IsDirty = t.IsDirty,
                Syntax = t.Syntax.ToString(),
                WordWrap = t.WordWrap,
                ShowLineNumbers = t.ShowLineNumbers,
                CursorLine = t.CursorLine,
                CursorColumn = t.CursorColumn,
                VerticalOffset = t.VerticalOffset,
                FileLastWriteTime = t.FileLastWriteTime
            }).ToList()
        };
        await _recovery.SaveManifestAsync(manifest);
        foreach (var tab in Tabs.ToList())
            await _recovery.SaveTabContentAsync(tab.Id, tab.Content);
    }

    public string GetStatusBarText()
    {
        if (ActiveTab == null) return "Ready";
        var tab = ActiveTab;
        return $"Ln {tab.CursorLine}, Col {tab.CursorColumn}  |  Spaces: 3  |  UTF-8  |  {tab.Syntax}";
    }
}
