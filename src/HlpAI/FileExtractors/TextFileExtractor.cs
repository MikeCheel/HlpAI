using System.Text;
using HlpAI.Models;
using SystemPath = System.IO.Path;

namespace HlpAI.FileExtractors;

public class TextFileExtractor : IFileExtractor
{
    private static readonly string[] TextExtensions = [".txt", ".md", ".log", ".csv", ".json", ".rst"];

    public bool CanHandle(string filePath)
    {
        var ext = SystemPath.GetExtension(filePath);
        return !string.IsNullOrEmpty(ext) && TextExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string> ExtractTextAsync(string filePath)
    {
        return await File.ReadAllTextAsync(filePath, Encoding.UTF8);
    }

    public string GetMimeType() => "text/plain";
}