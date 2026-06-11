using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using Nopad.Models;
using Nopad.ViewModels;

namespace Nopad.Views;

public partial class EditorDocumentView : UserControl
{
    private EditorTabViewModel? _viewModel;
    private bool _updatingFromVm;
    private bool _updatingFromEditor;

    public EditorDocumentView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnVmPropertyChanged;
        }

        _viewModel = DataContext as EditorTabViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnVmPropertyChanged;
            SyncFromViewModel();
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor == null) return;

        editor.TextChanged += OnEditorTextChanged;
        editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        editor.TextArea.TextView.ScrollOffsetChanged += OnScrollOffsetChanged;

        // Key bindings
        editor.TextArea.KeyDown += OnEditorKeyDown;

        SyncFromViewModel();
    }

    private void SyncFromViewModel()
    {
        if (_viewModel == null) return;
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor == null) return;

        _updatingFromVm = true;
        try
        {
            if (editor.Text != _viewModel.Content)
                editor.Text = _viewModel.Content;

            ApplySyntaxHighlighting(editor, _viewModel.Syntax);
            UpdatePreviewColumnWidth(_viewModel.ShowMarkdownPreview);
        }
        finally
        {
            _updatingFromVm = false;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_updatingFromEditor) return;

        Dispatcher.UIThread.Post(() =>
        {
            var editor = this.FindControl<TextEditor>("TextEditor");
            if (editor == null) return;

            switch (e.PropertyName)
            {
                case nameof(EditorTabViewModel.Content):
                    if (_viewModel != null && editor.Text != _viewModel.Content)
                    {
                        _updatingFromVm = true;
                        editor.Text = _viewModel.Content;
                        _updatingFromVm = false;
                    }
                    break;
                case nameof(EditorTabViewModel.Syntax):
                    if (_viewModel != null)
                        ApplySyntaxHighlighting(editor, _viewModel.Syntax);
                    break;
                case nameof(EditorTabViewModel.ShowMarkdownPreview):
                    if (_viewModel != null)
                        UpdatePreviewColumnWidth(_viewModel.ShowMarkdownPreview);
                    break;
            }
        });
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_updatingFromVm || _viewModel == null) return;
        _updatingFromEditor = true;
        try
        {
            var editor = (TextEditor)sender!;
            // Notify VM of content change
            if (DataContext is EditorTabViewModel vm)
            {
                var mainVm = GetMainViewModel();
                if (mainVm != null)
                    mainVm.OnContentChanged(vm, editor.Text);
                else
                {
                    vm.Content = editor.Text;
                    vm.IsDirty = true;
                }
            }
        }
        finally
        {
            _updatingFromEditor = false;
        }
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_viewModel == null) return;
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor == null) return;
        var caret = editor.TextArea.Caret;
        var mainVm = GetMainViewModel();
        mainVm?.OnCursorChanged(_viewModel, caret.Line, caret.Column);
        _viewModel.CursorLine = caret.Line;
        _viewModel.CursorColumn = caret.Column;
    }

    private void OnScrollOffsetChanged(object? sender, EventArgs e)
    {
        if (_viewModel == null) return;
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor != null)
            _viewModel.VerticalOffset = editor.VerticalOffset;
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor == null) return;

        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None)
        {
            InsertIndent(editor);
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.Shift)
        {
            RemoveIndent(editor);
            e.Handled = true;
        }
    }

    private static void InsertIndent(TextEditor editor)
    {
        var doc = editor.Document;
        var caret = editor.TextArea.Caret;
        doc.Insert(caret.Offset, "   "); // 3 spaces
    }

    private static void RemoveIndent(TextEditor editor)
    {
        var doc = editor.Document;
        var caret = editor.TextArea.Caret;
        var line = doc.GetLineByOffset(caret.Offset);
        var lineText = doc.GetText(line.Offset, line.Length);
        int spaces = 0;
        foreach (var ch in lineText)
        {
            if (ch == ' ' && spaces < 3) spaces++;
            else break;
        }
        if (spaces > 0)
            doc.Remove(line.Offset, spaces);
    }

    private void ApplySyntaxHighlighting(TextEditor editor, SyntaxLanguage syntax)
    {
        var name = syntax switch
        {
            SyntaxLanguage.Json => "Json",
            SyntaxLanguage.Xml => "XML",
            SyntaxLanguage.Yaml => "YAML",
            SyntaxLanguage.Markdown => "MarkDown",
            SyntaxLanguage.CSharp => "C#",
            SyntaxLanguage.Python => "Python",
            SyntaxLanguage.JavaScript => "JavaScript",
            _ => null
        };

        try
        {
            editor.SyntaxHighlighting = name != null
                ? HighlightingManager.Instance.GetDefinition(name)
                : null;
        }
        catch { editor.SyntaxHighlighting = null; }
    }

    private void UpdatePreviewColumnWidth(bool show)
    {
        var grid = this.FindControl<Grid>("EditorGrid");
        if (grid?.ColumnDefinitions.Count >= 3)
        {
            grid.ColumnDefinitions[2].Width = show
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0);
        }
    }

    public void SelectText(int start, int length)
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor == null) return;
        editor.Focus();
        editor.Select(start, length);
        editor.TextArea.Caret.Offset = start + length;
    }

    public void Undo()
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        editor?.Undo();
    }

    public void Redo()
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        editor?.Redo();
    }

    public void SelectAll()
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        editor?.SelectAll();
    }

    private MainWindowViewModel? GetMainViewModel()
    {
        var window = TopLevel.GetTopLevel(this) as Avalonia.Controls.Window;
        return window?.DataContext as MainWindowViewModel;
    }
}
