using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Noopad.Services;

public class FormattingService : IFormattingService
{
    public (bool success, string result) FormatJson(string input)
    {
        try
        {
            var token = JToken.Parse(input);
            return (true, token.ToString(Newtonsoft.Json.Formatting.Indented));
        }
        catch (JsonException ex)
        {
            return (false, $"Invalid JSON: {ex.Message}");
        }
    }

    public (bool success, string result) FormatXml(string input)
    {
        try
        {
            var doc = XDocument.Parse(input);
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineOnAttributes = false
            };
            using var sw = new StringWriter();
            using var writer = XmlWriter.Create(sw, settings);
            doc.Save(writer);
            return (true, sw.ToString());
        }
        catch (XmlException ex)
        {
            return (false, $"Invalid XML: {ex.Message}");
        }
    }
}
