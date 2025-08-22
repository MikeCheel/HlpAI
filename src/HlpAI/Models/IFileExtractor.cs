namespace HlpAI.Models;

public interface IFileExtractor
{
    bool CanHandle(string filePath);
    Task<string> ExtractTextAsync(string filePath);
    string GetMimeType();
}