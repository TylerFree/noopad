using System.Text.Json.Serialization;

namespace Nopad.Models;

public class RecoveryTabRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("recoveryPath")]
    public string RecoveryPath { get; set; } = string.Empty;

    [JsonPropertyName("isDirty")]
    public bool IsDirty { get; set; }

    [JsonPropertyName("syntax")]
    public string Syntax { get; set; } = "plainText";

    [JsonPropertyName("wordWrap")]
    public bool WordWrap { get; set; }

    [JsonPropertyName("showLineNumbers")]
    public bool ShowLineNumbers { get; set; } = true;

    [JsonPropertyName("cursorLine")]
    public int CursorLine { get; set; } = 1;

    [JsonPropertyName("cursorColumn")]
    public int CursorColumn { get; set; } = 1;

    [JsonPropertyName("verticalOffset")]
    public double VerticalOffset { get; set; }

    [JsonPropertyName("fileLastWriteTime")]
    public DateTime? FileLastWriteTime { get; set; }
}
