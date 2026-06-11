namespace Noopad.Services;

public interface IFormattingService
{
    (bool success, string result) FormatJson(string input);
    (bool success, string result) FormatXml(string input);
}
