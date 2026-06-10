using AvaloniaEdit.Highlighting;

namespace Nopad.Services;

public static class SyntaxService
{
    public static string Detect(string? path)
    {
        return Path.GetExtension(path ?? "").ToLowerInvariant() switch
        {
            ".json" => "JSON",
            ".xml" => "XML",
            ".yaml" or ".yml" => "YAML",
            ".md" or ".markdown" => "Markdown",
            ".cs" => "C#",
            ".ps1" => "PowerShell",
            ".sh" => "Shell",
            _ => "Plain text"
        };
    }

    public static IHighlightingDefinition? GetHighlighting(string syntax)
    {
        var name = syntax switch
        {
            "JSON" => "JavaScript",
            "XML" => "XML",
            "C#" => "C#",
            "PowerShell" => "PowerShell",
            _ => null
        };

        return name is null ? null : HighlightingManager.Instance.GetDefinition(name);
    }
}
