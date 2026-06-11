using Nopad.Models;

namespace Nopad.Services;

public interface ISyntaxService
{
    SyntaxLanguage DetectFromExtension(string? filePath);
    string GetHighlightingDefinitionName(SyntaxLanguage language);
}
