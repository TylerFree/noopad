using Noopad.Models;
using Noopad.ViewModels;

namespace Nopad.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task SaveRecoveryAsync_PersistsTabbedContent_AndInitializeRestoresIt()
    {
        var recovery = new FakeRecoveryService();
        var vm = CreateViewModel(recovery);

        vm.CreateNewTab();
        var tab = Assert.Single(vm.Tabs);
        vm.OnContentChanged(tab, "recover me");

        await vm.SaveRecoveryAsync();

        var restored = CreateViewModel(recovery);
        await restored.InitializeAsync();

        var restoredTab = Assert.Single(restored.Tabs);
        Assert.Equal("recover me", restoredTab.Content);
        Assert.True(restoredTab.IsDirty);
        Assert.Same(restoredTab, restored.ActiveTab);
    }

    [Fact]
    public async Task InitializeAsync_WithStartupFile_OpensFileInsteadOfRecovery()
    {
        using var temp = new TempDirectory();
        var filePath = temp.WriteFile("startup.md", "# startup");
        var recovery = new FakeRecoveryService
        {
            Manifest = new EditorSessionManifest
            {
                Tabs =
                [
                    new RecoveryTabRecord
                    {
                        Id = "tab-recovery",
                        Title = "Recovered",
                        RecoveryPath = "tabs/tab-recovery.txt",
                        IsDirty = true
                    }
                ]
            }
        };
        recovery.Contents["tabs/tab-recovery.txt"] = "recovered";

        var vm = CreateViewModel(recovery, [filePath]);

        await vm.InitializeAsync();

        var tab = Assert.Single(vm.Tabs);
        Assert.Equal(filePath, tab.FilePath);
        Assert.Equal("# startup", tab.Content);
        Assert.False(tab.IsDirty);
        Assert.Equal(0, recovery.LoadManifestCalls);
    }

    [Fact]
    public async Task OpenDocumentAsync_WhenFileIsAlreadyOpen_SwitchesTabWithoutReloading()
    {
        using var temp = new TempDirectory();
        var firstPath = temp.WriteFile("first.txt", "first disk");
        var secondPath = temp.WriteFile("second.txt", "second disk");
        var vm = CreateViewModel();

        Assert.True(await vm.OpenDocumentAsync(firstPath));
        var firstTab = vm.ActiveTab!;
        vm.OnContentChanged(firstTab, "unsaved first");
        Assert.True(await vm.OpenDocumentAsync(secondPath));

        Assert.True(await vm.OpenDocumentAsync(firstPath));

        Assert.Equal(2, vm.Tabs.Count);
        Assert.Same(firstTab, vm.ActiveTab);
        Assert.Equal("unsaved first", firstTab.Content);
        Assert.True(firstTab.IsDirty);
    }

    [Fact]
    public async Task OpenDocumentAsync_WhenFileIsNotOpen_OpensNewActiveTab()
    {
        using var temp = new TempDirectory();
        var firstPath = temp.WriteFile("first.txt", "first");
        var secondPath = temp.WriteFile("second.json", "{}");
        var vm = CreateViewModel();

        Assert.True(await vm.OpenDocumentAsync(firstPath));
        Assert.True(await vm.OpenDocumentAsync(secondPath));

        Assert.Equal(2, vm.Tabs.Count);
        Assert.Equal(secondPath, vm.ActiveTab?.FilePath);
        Assert.Equal("{}", vm.ActiveTab?.Content);
        Assert.Equal(SyntaxLanguage.Json, vm.ActiveTab?.Syntax);
    }

    [Fact]
    public async Task OpenDocumentAsync_WhenMissingFileAndPromptAccepted_CreatesAndOpensFile()
    {
        using var temp = new TempDirectory();
        var missingPath = temp.GetPath("new", "note.txt");
        var promptedPath = string.Empty;
        var vm = CreateViewModel();
        vm.CreateMissingFileHandler = path =>
        {
            promptedPath = path;
            return Task.FromResult(true);
        };

        Assert.True(await vm.OpenDocumentAsync(missingPath, promptToCreate: true));

        Assert.True(File.Exists(missingPath));
        Assert.Equal(missingPath, promptedPath);
        var tab = Assert.Single(vm.Tabs);
        Assert.Equal(missingPath, tab.FilePath);
        Assert.Equal(string.Empty, tab.Content);
        Assert.False(tab.IsDirty);
    }

    [Fact]
    public async Task OpenDocumentAsync_WhenMissingFileAndPromptDeclined_DoesNotCreateFile()
    {
        using var temp = new TempDirectory();
        var missingPath = temp.GetPath("missing.txt");
        var vm = CreateViewModel();
        vm.CreateMissingFileHandler = _ => Task.FromResult(false);

        Assert.False(await vm.OpenDocumentAsync(missingPath, promptToCreate: true));

        Assert.False(File.Exists(missingPath));
        Assert.Empty(vm.Tabs);
    }

    [Fact]
    public async Task InitializeAsync_RestoresDirtyFileBackedTabFromRecoveryContent()
    {
        using var temp = new TempDirectory();
        var filePath = temp.WriteFile("note.txt", "disk content");
        var fileWriteTime = File.GetLastWriteTimeUtc(filePath);
        var recovery = new FakeRecoveryService
        {
            Manifest = new EditorSessionManifest
            {
                ActiveTabId = "tab-note",
                Tabs =
                [
                    new RecoveryTabRecord
                    {
                        Id = "tab-note",
                        FilePath = filePath,
                        Title = "note.txt",
                        RecoveryPath = "tabs/tab-note.txt",
                        IsDirty = true,
                        FileLastWriteTime = fileWriteTime
                    }
                ]
            }
        };
        recovery.Contents["tabs/tab-note.txt"] = "unsaved recovery";
        var vm = CreateViewModel(recovery);

        await vm.InitializeAsync();

        var tab = Assert.Single(vm.Tabs);
        Assert.Equal("unsaved recovery", tab.Content);
        Assert.True(tab.IsDirty);
        Assert.Same(tab, vm.ActiveTab);
    }

    private static MainWindowViewModel CreateViewModel(
        FakeRecoveryService? recovery = null,
        IEnumerable<string>? startupFilePaths = null)
    {
        return new MainWindowViewModel(
            recovery ?? new FakeRecoveryService(),
            new FakeFormattingService(),
            new FakeSyntaxService(),
            new FakeMarkdownPreviewService(),
            new FakeSearchReplaceService(),
            new FakeUserSettingsService(),
            startupFilePaths);
    }
}
