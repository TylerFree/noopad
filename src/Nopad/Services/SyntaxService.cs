using Noopad.Models;

namespace Noopad.Services;

public class SyntaxService : ISyntaxService
{
    public SyntaxLanguage DetectFromExtension(string? filePath)
    {
        if (filePath == null) return SyntaxLanguage.PlainText;
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".json" => SyntaxLanguage.Json,
            ".xml" or ".xaml" or ".axaml" or ".csproj" or ".props" or ".targets" or ".config" => SyntaxLanguage.Xml,
            ".yaml" or ".yml" => SyntaxLanguage.Yaml,
            ".md" or ".markdown" => SyntaxLanguage.Markdown,
            ".cs" => SyntaxLanguage.CSharp,
            ".py" => SyntaxLanguage.Python,
            ".js" or ".ts" => SyntaxLanguage.JavaScript,
            _ => SyntaxLanguage.PlainText
        };
    }

    public string GetHighlightingDefinitionName(SyntaxLanguage language)
    {
        return language switch
        {
            SyntaxLanguage.Json => "Json",
            SyntaxLanguage.Xml => "XML",
            SyntaxLanguage.Yaml => "YAML",
            SyntaxLanguage.Markdown => "MarkDown",
            SyntaxLanguage.CSharp => "C#",
            SyntaxLanguage.Python => "Python",
            SyntaxLanguage.JavaScript => "JavaScript",
            _ => string.Empty
        };
    }
}
