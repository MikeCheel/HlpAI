using System.Text;
using HtmlAgilityPack;
using HlpAI.Models;
using SystemPath = System.IO.Path;

namespace HlpAI.FileExtractors;

public class HtmlFileExtractor : IFileExtractor
{
    private static readonly string[] HtmlExtensions = [".html", ".htm"];

    public bool CanHandle(string filePath)
    {
        var ext = SystemPath.GetExtension(filePath);
        return !string.IsNullOrEmpty(ext) && HtmlExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string> ExtractTextAsync(string filePath)
    {
        var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        doc.DocumentNode.Descendants()
            .Where(n => n.Name == "script" || n.Name == "style")
            .ToList()
            .ForEach(n => n.Remove());

        return doc.DocumentNode.InnerText;
    }

    public string GetMimeType() => "text/html";
}