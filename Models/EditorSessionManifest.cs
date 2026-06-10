namespace Nopad.Models;

public sealed class EditorSessionManifest
{
    public int Version { get; set; } = 1;
    public string? ActiveTabId { get; set; }
    public int NextUntitledNumber { get; set; } = 1;
    public List<RecoveryTabRecord> Tabs { get; set; } = [];
}

public sealed class RecoveryTabRecord
{
    public string Id { get; set; } = "";
    public string? FilePath { get; set; }
    public string Title { get; set; } = "";
    public string RecoveryPath { get; set; } = "";
    public bool IsDirty { get; set; }
    public string Syntax { get; set; } = "Plain text";
    public bool WordWrap { get; set; }
    public bool ShowLineNumbers { get; set; } = true;
    public int CursorLine { get; set; } = 1;
    public int CursorColumn { get; set; } = 1;
    public double VerticalOffset { get; set; }
    public DateTimeOffset? DiskLastWriteUtc { get; set; }
}
