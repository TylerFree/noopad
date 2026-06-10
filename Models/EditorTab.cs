using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nopad.Models;

public sealed class EditorTab : INotifyPropertyChanged
{
    private string _title = "";
    private string _text = "";
    private bool _isDirty;
    private string _syntax = "Plain text";
    private bool _showPreview;
    private int _cursorLine = 1;
    private int _cursorColumn = 1;

    public string Id { get; init; } = Guid.NewGuid().ToString("n");
    public string? FilePath { get; set; }
    public string Title { get => _title; set => SetField(ref _title, value); }
    public string Text { get => _text; set => SetField(ref _text, value); }
    public bool IsDirty { get => _isDirty; set => SetField(ref _isDirty, value); }
    public string Syntax { get => _syntax; set => SetField(ref _syntax, value); }
    public bool ShowPreview { get => _showPreview; set => SetField(ref _showPreview, value); }
    public bool WordWrap { get; set; }
    public bool ShowLineNumbers { get; set; } = true;
    public int CursorLine { get => _cursorLine; set => SetField(ref _cursorLine, value); }
    public int CursorColumn { get => _cursorColumn; set => SetField(ref _cursorColumn, value); }
    public double VerticalOffset { get; set; }
    public DateTimeOffset? DiskLastWriteUtc { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
