namespace Nopad.Models;

public class EditorDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? FilePath { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsDirty { get; set; }
    public SyntaxLanguage Syntax { get; set; } = SyntaxLanguage.PlainText;
    public bool WordWrap { get; set; }
    public bool ShowLineNumbers { get; set; } = true;
    public int CursorLine { get; set; } = 1;
    public int CursorColumn { get; set; } = 1;
    public double VerticalOffset { get; set; }
    public DateTime? FileLastWriteTime { get; set; }
}
