using System.Text.Json.Serialization;

namespace Noopad.Models;

public class EditorSessionManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("activeTabId")]
    public string? ActiveTabId { get; set; }

    [JsonPropertyName("nextUntitledNumber")]
    public int NextUntitledNumber { get; set; } = 1;

    [JsonPropertyName("tabs")]
    public List<RecoveryTabRecord> Tabs { get; set; } = new();
}
