using System.Text;
using HtmlAgilityPack;
using HlpAI.Models;
using SystemPath = System.IO.Path;

namespace HlpAI.FileExtractors;

public class HhcFileExtractor : IFileExtractor
{
    public bool CanHandle(string filePath)
    {
        return string.Equals(SystemPath.GetExtension(filePath), ".hhc", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> ExtractTextAsync(string filePath)
    {
        try
        {
            var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var extractedText = new StringBuilder();
            extractedText.AppendLine("=== HTML Help Contents File ===");

            var tocItems = doc.DocumentNode.Descendants("param")
                .Where(n => string.Equals(n.GetAttributeValue("name", ""), "name", StringComparison.OrdinalIgnoreCase))
                .Select(n => n.GetAttributeValue("value", ""))
                .Where(v => !string.IsNullOrWhiteSpace(v));

            foreach (var item in tocItems)
            {
                extractedText.AppendLine($"- {item}");
            }

            var localParams = doc.DocumentNode.Descendants("param")
                .Where(n => string.Equals(n.GetAttributeValue("name", ""), "local", StringComparison.OrdinalIgnoreCase))
                .Select(n => n.GetAttributeValue("value", ""))
                .Where(v => !string.IsNullOrWhiteSpace(v));

            if (localParams.Any())
            {
                extractedText.AppendLine();
                extractedText.AppendLine("=== Referenced Files ===");
                foreach (var localRef in localParams)
                {
                    extractedText.AppendLine($"- {localRef}");
                }
            }

            return extractedText.ToString();
        }
        catch (Exception ex)
        {
            return $"Error extracting HHC file: {ex.Message}";
        }
    }

    public string GetMimeType() => "text/html";
}