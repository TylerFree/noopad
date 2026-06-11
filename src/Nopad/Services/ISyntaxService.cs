using Noopad.Models;

namespace Noopad.Services;

public interface ISyntaxService
{
    SyntaxLanguage DetectFromExtension(string? filePath);
    string GetHighlightingDefinitionName(SyntaxLanguage language);
}
