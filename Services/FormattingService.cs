using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Nopad.Services;

public static class FormattingService
{
    public static (bool Success, string Text, string Error) FormatJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            return (true, JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            }), "");
        }
        catch (JsonException ex)
        {
            return (false, text, $"Invalid JSON at line {ex.LineNumber + 1}, column {ex.BytePositionInLine + 1}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, text, $"Invalid JSON: {ex.Message}");
        }
    }

    public static (bool Success, string Text, string Error) FormatXml(string text)
    {
        try
        {
            var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
            var builder = new StringBuilder();
            using var writer = XmlWriter.Create(builder, new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "   ",
                OmitXmlDeclaration = document.Declaration is null
            });
            document.Save(writer);
            return (true, builder.ToString(), "");
        }
        catch (XmlException ex)
        {
            return (false, text, $"Invalid XML at line {ex.LineNumber}, column {ex.LinePosition}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, text, $"Invalid XML: {ex.Message}");
        }
    }
}
