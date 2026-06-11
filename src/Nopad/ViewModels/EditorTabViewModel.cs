using CommunityToolkit.Mvvm.ComponentModel;
using Nopad.Models;

namespace Nopad.ViewModels;

public partial class EditorTabViewModel : ObservableObject
{
    [ObservableProperty] private string _id;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string? _filePath;
    [ObservableProperty] private string _content = string.Empty;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private SyntaxLanguage _syntax = SyntaxLanguage.PlainText;
    [ObservableProperty] private bool _wordWrap;
    [ObservableProperty] private bool _showLineNumbers = true;
    [ObservableProperty] private bool _showMarkdownPreview;
    [ObservableProperty] private int _cursorLine = 1;
    [ObservableProperty] private int _cursorColumn = 1;
    [ObservableProperty] private double _verticalOffset;
    [ObservableProperty] private DateTime? _fileLastWriteTime;
    [ObservableProperty] private string _markdownHtml = string.Empty;

    // Called when content changes to mark dirty
    private string _savedContent = string.Empty;

    public EditorTabViewModel(string id)
    {
        _id = id;
    }

    public void MarkClean(string content)
    {
        _savedContent = content;
        Content = content;
        IsDirty = false;
    }

    partial void OnContentChanged(string value)
    {
        // IsDirty is set externally by the view's text-changed handler
    }

    partial void OnFilePathChanged(string? value)
    {
        if (value != null)
            Title = Path.GetFileName(value);
    }

    public EditorDocument ToDocument() => new()
    {
        Id = Id,
        FilePath = FilePath,
        Title = Title,
        Content = Content,
        IsDirty = IsDirty,
        Syntax = Syntax,
        WordWrap = WordWrap,
        ShowLineNumbers = ShowLineNumbers,
        CursorLine = CursorLine,
        CursorColumn = CursorColumn,
        VerticalOffset = VerticalOffset,
        FileLastWriteTime = FileLastWriteTime
    };
}
