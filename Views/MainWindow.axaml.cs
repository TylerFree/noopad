using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using Markdig;
using Nopad.Models;
using Nopad.Services;

namespace Nopad.Views;

public sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<EditorTab> _tabs = [];
    private readonly RecoveryService _recovery = new();
    private readonly DispatcherTimer _recoveryTimer;
    private readonly DispatcherTimer _previewTimer;
    private int _nextUntitledNumber = 1;
    private bool _loadingTab;
    private bool _closing;
    private int _lastFindIndex = -1;

    private EditorTab? ActiveTab => Tabs.SelectedItem as EditorTab;

    public MainWindow()
    {
        InitializeComponent();
        Tabs.ItemsSource = _tabs;
        Tabs.ItemTemplate = BuildTabHeaderTemplate();

        Editor.TextChanged += Editor_TextChanged;
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCursorStatus();
        Editor.TextArea.TextEntering += Editor_TextEntering;
        Editor.KeyDown += Editor_KeyDown;
        Closing += MainWindow_Closing;

        _recoveryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _recoveryTimer.Tick += async (_, _) =>
        {
            _recoveryTimer.Stop();
            await PersistSessionAsync();
        };

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            UpdateMarkdownPreview();
        };

        Opened += async (_, _) => await RestoreOrCreateAsync();
    }

    private static FuncDataTemplate<EditorTab> BuildTabHeaderTemplate()
    {
        return new FuncDataTemplate<EditorTab>((tab, _) =>
        {
            var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 7, Margin = new Thickness(10, 0) };
            var dirty = new Avalonia.Controls.Shapes.Ellipse { Width = 8, Height = 8, Fill = Brushes.Firebrick, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            dirty.Bind(IsVisibleProperty, new Avalonia.Data.Binding(nameof(EditorTab.IsDirty)));
            var title = new TextBlock { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis };
            title.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(EditorTab.Title)));
            var close = new Button
            {
                Classes = { "icon" },
                Content = "x",
                Tag = tab,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(2, 0, -4, 0)
            };
            ToolTip.SetTip(close, "Close tab");
            close.Click += (_, args) =>
            {
                args.Handled = true;
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime &&
                    lifetime.MainWindow is MainWindow window)
                {
                    _ = window.CloseTabAsync(tab);
                }
            };

            panel.Children.Add(dirty);
            panel.Children.Add(title);
            panel.Children.Add(close);
            return panel;
        });
    }

    private async Task RestoreOrCreateAsync()
    {
        var restored = await _recovery.LoadAsync();
        _nextUntitledNumber = restored.NextUntitledNumber;
        foreach (var tab in restored.Tabs)
        {
            _tabs.Add(tab);
        }

        if (_tabs.Count == 0)
        {
            NewTab();
        }
        else
        {
            Tabs.SelectedItem = _tabs.FirstOrDefault(t => t.Id == restored.ActiveTabId) ?? _tabs[0];
        }

        RecoveryStatus.Text = string.IsNullOrWhiteSpace(restored.Message) ? "Recovery on" : restored.Message;
        await PersistSessionAsync();
    }

    private void NewTab()
    {
        var tab = new EditorTab
        {
            Title = $"Unfile-{_nextUntitledNumber++}",
            Text = "",
            Syntax = "Plain text",
            IsDirty = false
        };

        _tabs.Add(tab);
        Tabs.SelectedItem = tab;
        ScheduleRecovery();
    }

    private void LoadActiveTabIntoEditor()
    {
        var tab = ActiveTab;
        _loadingTab = true;
        try
        {
            Editor.Text = tab?.Text ?? "";
            Editor.WordWrap = tab?.WordWrap ?? false;
            Editor.ShowLineNumbers = tab?.ShowLineNumbers ?? true;
            Editor.SyntaxHighlighting = tab is null ? null : SyntaxService.GetHighlighting(tab.Syntax);
            if (tab is { CursorLine: > 0, CursorColumn: > 0 })
            {
                var line = Math.Min(tab.CursorLine, Math.Max(1, Editor.Document.LineCount));
                var column = Math.Min(tab.CursorColumn, Editor.Document.GetLineByNumber(line).Length + 1);
                Editor.CaretOffset = Editor.Document.GetOffset(line, column);
            }
        }
        finally
        {
            _loadingTab = false;
        }

        UpdateStatus();
        UpdatePreviewVisibility();
        UpdateMarkdownPreview();
    }

    private async Task OpenFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "Open file"
        });

        foreach (var file in files)
        {
            if (file.Path.LocalPath is not { Length: > 0 } path)
            {
                continue;
            }

            var text = await File.ReadAllTextAsync(path);
            var tab = new EditorTab
            {
                FilePath = path,
                Title = System.IO.Path.GetFileName(path),
                Text = text,
                IsDirty = false,
                Syntax = SyntaxService.Detect(path),
                DiskLastWriteUtc = File.GetLastWriteTimeUtc(path)
            };
            _tabs.Add(tab);
            Tabs.SelectedItem = tab;
        }

        ScheduleRecovery();
    }

    private async Task<bool> SaveTabAsync(EditorTab tab, bool saveAs = false)
    {
        if (saveAs || string.IsNullOrWhiteSpace(tab.FilePath))
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save file",
                SuggestedFileName = tab.Title.StartsWith("Unfile-", StringComparison.Ordinal) ? "untitled.txt" : tab.Title
            });

            if (file?.Path.LocalPath is not { Length: > 0 } path)
            {
                return false;
            }

            tab.FilePath = path;
            tab.Title = System.IO.Path.GetFileName(path);
            tab.Syntax = SyntaxService.Detect(path);
        }

        try
        {
            await File.WriteAllTextAsync(tab.FilePath!, tab.Text);
            tab.IsDirty = false;
            tab.DiskLastWriteUtc = File.GetLastWriteTimeUtc(tab.FilePath!);
            SaveStatus.Text = "Saved";
            ScheduleRecovery();
            LoadActiveTabIntoEditor();
            return true;
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Save failed", ex.Message);
            tab.IsDirty = true;
            SaveStatus.Text = "Save failed";
            return false;
        }
    }

    private async Task<bool> CloseTabAsync(EditorTab tab)
    {
        if (tab.IsDirty)
        {
            var result = await PromptDirtyCloseAsync(tab);
            if (result == DirtyCloseAction.Cancel)
            {
                return false;
            }

            if (result == DirtyCloseAction.Save && !await SaveTabAsync(tab))
            {
                return false;
            }
        }

        _recovery.RemoveBlob(tab);
        var index = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        if (_tabs.Count == 0 && !_closing)
        {
            NewTab();
        }
        else if (_tabs.Count > 0)
        {
            Tabs.SelectedIndex = Math.Clamp(index, 0, _tabs.Count - 1);
        }

        ScheduleRecovery();
        return true;
    }

    private async Task<DirtyCloseAction> PromptDirtyCloseAsync(EditorTab tab)
    {
        var dialog = new Window
        {
            Title = "Unsaved changes",
            Width = 420,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };
        var result = DirtyCloseAction.Cancel;
        var text = new TextBlock
        {
            Text = $"Save changes to {tab.Title}?",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(18)
        };
        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(18, 0, 18, 18)
        };
        foreach (var (label, action) in new[] { ("Save", DirtyCloseAction.Save), ("Discard", DirtyCloseAction.Discard), ("Cancel", DirtyCloseAction.Cancel) })
        {
            var button = new Button { Content = label, MinWidth = 84 };
            button.Click += (_, _) =>
            {
                result = action;
                dialog.Close();
            };
            buttons.Children.Add(button);
        }

        dialog.Content = new DockPanel
        {
            Children =
            {
                buttons.WithDock(Dock.Bottom),
                text
            }
        };
        await dialog.ShowDialog(this);
        return result;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };
        var button = new Button { Content = "OK", MinWidth = 90, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Margin = new Thickness(18) };
        button.Click += (_, _) => dialog.Close();
        dialog.Content = new DockPanel
        {
            Children =
            {
                button.WithDock(Dock.Bottom),
                new TextBlock { Text = message, Margin = new Thickness(18), TextWrapping = TextWrapping.Wrap }
            }
        };
        await dialog.ShowDialog(this);
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (_loadingTab || ActiveTab is not { } tab)
        {
            return;
        }

        tab.Text = Editor.Text;
        tab.IsDirty = true;
        SaveStatus.Text = "Unsaved";
        ScheduleRecovery();
        SchedulePreview();
        UpdateLineEndingStatus();
    }

    private void Editor_TextEntering(object? sender, TextInputEventArgs e)
    {
        if (e.Text == "\n" || e.Text == "\r")
        {
            ApplyAutoIndent();
            e.Handled = true;
        }
    }

    private void Editor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.Shift)
        {
            ApplyIndent(false);
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            ApplyIndent(true);
            e.Handled = true;
        }
        else if (e.Key == Key.F3 && e.KeyModifiers == KeyModifiers.Shift)
        {
            FindPrevious();
            e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            FindNext();
            e.Handled = true;
        }
    }

    private void ApplyAutoIndent()
    {
        var caret = Editor.TextArea.Caret;
        var line = Editor.Document.GetLineByNumber(caret.Line);
        var text = Editor.Document.GetText(line);
        var indent = Regex.Match(text, "^\\s*").Value;
        var trimmed = text.TrimEnd();
        if (trimmed.EndsWith("{", StringComparison.Ordinal) ||
            trimmed.EndsWith("[", StringComparison.Ordinal) ||
            Regex.IsMatch(trimmed, "<[A-Za-z][^/!?>]*>$") ||
            Regex.IsMatch(trimmed, ":\\s*$"))
        {
            indent += "   ";
        }

        Editor.Document.Insert(caret.Offset, Environment.NewLine + indent);
    }

    private void ApplyIndent(bool indent)
    {
        var document = Editor.Document;
        var selection = Editor.TextArea.Selection;
        var startLine = document.GetLineByOffset(selection.IsEmpty ? Editor.CaretOffset : selection.SurroundingSegment.Offset).LineNumber;
        var endOffset = selection.IsEmpty ? Editor.CaretOffset : selection.SurroundingSegment.EndOffset;
        var endLine = document.GetLineByOffset(Math.Max(0, endOffset)).LineNumber;

        document.BeginUpdate();
        try
        {
            for (var lineNumber = startLine; lineNumber <= endLine; lineNumber++)
            {
                var line = document.GetLineByNumber(lineNumber);
                if (indent)
                {
                    document.Insert(line.Offset, "   ");
                }
                else
                {
                    var remove = Math.Min(3, document.GetText(line.Offset, Math.Min(3, line.Length)).TakeWhile(char.IsWhiteSpace).Count());
                    if (remove > 0)
                    {
                        document.Remove(line.Offset, remove);
                    }
                }
            }
        }
        finally
        {
            document.EndUpdate();
        }
    }

    private void UpdateStatus()
    {
        var tab = ActiveTab;
        SyntaxStatus.Text = tab?.Syntax ?? "Plain text";
        SaveStatus.Text = tab is null ? "Ready" : tab.IsDirty ? "Unsaved" : "Saved";
        UpdateCursorStatus();
        UpdateLineEndingStatus();
    }

    private void UpdateCursorStatus()
    {
        if (ActiveTab is not { } tab)
        {
            return;
        }

        tab.CursorLine = Editor.TextArea.Caret.Line;
        tab.CursorColumn = Editor.TextArea.Caret.Column;
        CursorStatus.Text = $"Ln {tab.CursorLine}, Col {tab.CursorColumn}";
    }

    private void UpdateLineEndingStatus()
    {
        LineEndingStatus.Text = Editor.Text.Contains("\r\n", StringComparison.Ordinal) ? "CRLF" : "LF";
    }

    private void UpdatePreviewVisibility()
    {
        var show = ActiveTab is { ShowPreview: true } tab && (tab.Syntax == "Markdown" || tab.Title.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
        EditorGrid.ColumnDefinitions[0].Width = show ? new GridLength(1, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);
        EditorGrid.ColumnDefinitions[1].Width = show ? new GridLength(5) : new GridLength(0);
        EditorGrid.ColumnDefinitions[2].Width = show ? new GridLength(0.9, GridUnitType.Star) : new GridLength(0);
        PreviewSplitter.IsVisible = show;
        PreviewPane.IsVisible = show;
    }

    private void UpdateMarkdownPreview()
    {
        if (PreviewPane.IsVisible)
        {
            var html = Markdown.ToHtml(Editor.Text);
            var plain = System.Net.WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", ""));
            plain = Regex.Replace(plain, @"\n{3,}", "\n\n");
            MarkdownPreview.Text = plain;
        }
    }

    private void ScheduleRecovery() => _recoveryTimer.Start();
    private void SchedulePreview() => _previewTimer.Start();

    private async Task PersistSessionAsync()
    {
        if (_tabs.Count == 0)
        {
            return;
        }

        await _recovery.SaveAsync(_tabs, ActiveTab, _nextUntitledNumber);
        RecoveryStatus.Text = $"Recovery saved {DateTime.Now:t}";
    }

    private RegexOptions SearchOptions => MatchCaseBox.IsChecked == true ? RegexOptions.None : RegexOptions.IgnoreCase;

    private IReadOnlyList<Match> FindMatches()
    {
        var query = FindBox.Text ?? "";
        if (string.IsNullOrEmpty(query))
        {
            return [];
        }

        var pattern = RegexBox.IsChecked == true ? query : Regex.Escape(query);
        if (WholeWordBox.IsChecked == true)
        {
            pattern = $@"\b(?:{pattern})\b";
        }

        try
        {
            return Regex.Matches(Editor.Text, pattern, SearchOptions).Cast<Match>().ToList();
        }
        catch (ArgumentException ex)
        {
            MatchStatus.Text = ex.Message;
            return [];
        }
    }

    private void FindNext() => SelectMatch(forward: true);
    private void FindPrevious() => SelectMatch(forward: false);

    private void SelectMatch(bool forward)
    {
        var matches = FindMatches();
        if (matches.Count == 0)
        {
            MatchStatus.Text = "No matches";
            return;
        }

        var caret = Editor.CaretOffset;
        Match match;
        if (forward)
        {
            match = matches.FirstOrDefault(m => m.Index > caret) ?? matches[0];
        }
        else
        {
            match = matches.LastOrDefault(m => m.Index < caret) ?? matches[^1];
        }

        _lastFindIndex = match.Index;
        Editor.Select(match.Index, match.Length);
        Editor.TextArea.Caret.Offset = match.Index + match.Length;
        Editor.ScrollToLine(Editor.Document.GetLineByOffset(match.Index).LineNumber);
        MatchStatus.Text = $"{matches.ToList().IndexOf(match) + 1} of {matches.Count}";
    }

    private void ReplaceCurrent()
    {
        if (Editor.SelectionLength == 0)
        {
            SelectMatch(true);
        }

        if (Editor.SelectionLength > 0)
        {
            Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, ReplaceBox.Text ?? "");
            FindNext();
        }
    }

    private async void ReplaceAll()
    {
        var matches = FindMatches();
        if (matches.Count == 0)
        {
            MatchStatus.Text = "No matches";
            return;
        }

        var replacement = ReplaceBox.Text ?? "";
        var pattern = RegexBox.IsChecked == true ? FindBox.Text ?? "" : Regex.Escape(FindBox.Text ?? "");
        if (WholeWordBox.IsChecked == true)
        {
            pattern = $@"\b(?:{pattern})\b";
        }

        try
        {
            Editor.Text = Regex.Replace(Editor.Text, pattern, replacement, SearchOptions);
            MatchStatus.Text = $"Replaced {matches.Count}";
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Replace failed", ex.Message);
        }
    }

    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_closing)
        {
            return;
        }

        e.Cancel = true;
        _closing = true;
        await PersistSessionAsync();
        Close();
    }

    private void NewTab_Click(object? sender, RoutedEventArgs e) => NewTab();
    private async void OpenFile_Click(object? sender, RoutedEventArgs e) => await OpenFileAsync();
    private async void Save_Click(object? sender, RoutedEventArgs e) { if (ActiveTab is { } tab) await SaveTabAsync(tab); }
    private async void SaveAs_Click(object? sender, RoutedEventArgs e) { if (ActiveTab is { } tab) await SaveTabAsync(tab, true); }
    private async void SaveAll_Click(object? sender, RoutedEventArgs e) { foreach (var tab in _tabs.ToList()) await SaveTabAsync(tab); }
    private async void CloseTab_Click(object? sender, RoutedEventArgs e) { if (ActiveTab is { } tab) await CloseTabAsync(tab); }
    private async void CloseAll_Click(object? sender, RoutedEventArgs e) { foreach (var tab in _tabs.ToList()) if (!await CloseTabAsync(tab)) break; }
    private void Exit_Click(object? sender, RoutedEventArgs e) => Close();
    private void Undo_Click(object? sender, RoutedEventArgs e) => Editor.Undo();
    private void Redo_Click(object? sender, RoutedEventArgs e) => Editor.Redo();
    private void Cut_Click(object? sender, RoutedEventArgs e) => Editor.Cut();
    private void Copy_Click(object? sender, RoutedEventArgs e) => Editor.Copy();
    private void Paste_Click(object? sender, RoutedEventArgs e) => Editor.Paste();
    private void SelectAll_Click(object? sender, RoutedEventArgs e) => Editor.SelectAll();
    private void Indent_Click(object? sender, RoutedEventArgs e) => ApplyIndent(true);
    private void Unindent_Click(object? sender, RoutedEventArgs e) => ApplyIndent(false);

    private void WordWrap_Click(object? sender, RoutedEventArgs e)
    {
        Editor.WordWrap = !Editor.WordWrap;
        if (ActiveTab is { } tab)
        {
            tab.WordWrap = Editor.WordWrap;
            ScheduleRecovery();
        }
    }

    private void LineNumbers_Click(object? sender, RoutedEventArgs e)
    {
        Editor.ShowLineNumbers = !Editor.ShowLineNumbers;
        if (ActiveTab is { } tab)
        {
            tab.ShowLineNumbers = Editor.ShowLineNumbers;
            ScheduleRecovery();
        }
    }

    private void MarkdownPreview_Click(object? sender, RoutedEventArgs e)
    {
        if (ActiveTab is { } tab)
        {
            tab.ShowPreview = !tab.ShowPreview;
            UpdatePreviewVisibility();
            UpdateMarkdownPreview();
            ScheduleRecovery();
        }
    }

    private void LightTheme_Click(object? sender, RoutedEventArgs e) => Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
    private void DarkTheme_Click(object? sender, RoutedEventArgs e) => Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
    private void ShowFind_Click(object? sender, RoutedEventArgs e) { SearchPanel.IsVisible = true; ReplaceBox.IsVisible = false; FindBox.Focus(); }
    private void ShowReplace_Click(object? sender, RoutedEventArgs e) { SearchPanel.IsVisible = true; ReplaceBox.IsVisible = true; FindBox.Focus(); }
    private void FindNext_Click(object? sender, RoutedEventArgs e) => FindNext();
    private void FindPrevious_Click(object? sender, RoutedEventArgs e) => FindPrevious();
    private void ReplaceCurrent_Click(object? sender, RoutedEventArgs e) => ReplaceCurrent();
    private void ReplaceAll_Click(object? sender, RoutedEventArgs e) => ReplaceAll();

    private void FindBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SearchPanel.IsVisible = false;
            Editor.Focus();
            e.Handled = true;
        }
    }

    private async void FormatJson_Click(object? sender, RoutedEventArgs e)
    {
        var result = FormattingService.FormatJson(Editor.Text);
        if (result.Success)
        {
            Editor.Text = result.Text;
        }
        else
        {
            await ShowMessageAsync("Format JSON", result.Error);
        }
    }

    private async void FormatXml_Click(object? sender, RoutedEventArgs e)
    {
        var result = FormattingService.FormatXml(Editor.Text);
        if (result.Success)
        {
            Editor.Text = result.Text;
        }
        else
        {
            await ShowMessageAsync("Format XML", result.Error);
        }
    }

    private async void Shortcuts_Click(object? sender, RoutedEventArgs e)
    {
        await ShowMessageAsync("Keyboard shortcuts", "Ctrl+T New tab\nCtrl+O Open\nCtrl+S Save\nCtrl+Shift+S Save all\nCtrl+W Close tab\nCtrl+F Find\nCtrl+H Replace\nF3 / Shift+F3 Find next/previous\nTab / Shift+Tab Indent with 3 spaces");
    }

    private async void About_Click(object? sender, RoutedEventArgs e)
    {
        await ShowMessageAsync("About Nopad", "Nopad is a lightweight engineer text editor with tabbed editing, recovery, formatting, search, and Markdown preview.");
    }

    private void Tabs_SelectionChanged(object? sender, SelectionChangedEventArgs e) => LoadActiveTabIntoEditor();

    private enum DirtyCloseAction
    {
        Save,
        Discard,
        Cancel
    }
}

internal static class DockPanelExtensions
{
    public static T WithDock<T>(this T control, Dock dock) where T : Control
    {
        DockPanel.SetDock(control, dock);
        return control;
    }
}
