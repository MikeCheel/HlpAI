using System.Diagnostics;
using System.Text;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using HlpAI.Models;
using SystemPath = System.IO.Path;

namespace HlpAI.FileExtractors;

public class ChmFileExtractor(ILogger? logger = null) : IFileExtractor, IDisposable
{
    private readonly ILogger? _logger = logger;
    private readonly string _tempDir = SystemPath.Combine(SystemPath.GetTempPath(), "CHMExtractor", Guid.NewGuid().ToString());

    public bool CanHandle(string filePath)
    {
        return string.Equals(SystemPath.GetExtension(filePath), ".chm", StringComparison.OrdinalIgnoreCase);
    }

    public Task<string> ExtractTextAsync(string filePath)
    {
        return Task.Run(async () =>
        {
            try
            {
                Directory.CreateDirectory(_tempDir);

                var processInfo = new ProcessStartInfo
                {
                    FileName = "hh.exe",
                    Arguments = $"-decompile \"{_tempDir}\" \"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        _logger?.LogWarning("HH.exe returned exit code {ExitCode}: {Error}", process.ExitCode, error);
                    }
                }

                var extractedText = new StringBuilder();
                await ExtractTextFromDirectory(_tempDir, extractedText);

                return extractedText.ToString();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error extracting CHM file {FilePath}", filePath);
                return $"Error extracting CHM: {ex.Message}";
            }
            finally
            {
                try
                {
                    if (Directory.Exists(_tempDir))
                    {
                        Directory.Delete(_tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to clean up temp directory {TempDir}", _tempDir);
                }
            }
        });
    }

    private async Task ExtractTextFromDirectory(string directory, StringBuilder extractedText)
    {
        if (!Directory.Exists(directory))
            return;

        var htmlFiles = Directory.GetFiles(directory, "*.htm*", SearchOption.AllDirectories)
            .OrderBy(f => f);

        foreach (var htmlFile in htmlFiles)
        {
            try
            {
                var html = await File.ReadAllTextAsync(htmlFile, Encoding.UTF8);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                doc.DocumentNode.Descendants()
                    .Where(n => n.Name == "script" || n.Name == "style")
                    .ToList()
                    .ForEach(n => n.Remove());

                var text = doc.DocumentNode.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    extractedText.AppendLine($"=== {SystemPath.GetFileName(htmlFile)} ===");
                    extractedText.AppendLine(text.Trim());
                    extractedText.AppendLine();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to extract text from HTML file {HtmlFile}", htmlFile);
            }
        }

        var hhcFiles = Directory.GetFiles(directory, "*.hhc", SearchOption.AllDirectories);
        foreach (var hhcFile in hhcFiles)
        {
            try
            {
                await ExtractHhcStructure(hhcFile, extractedText);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to extract HHC structure from {HhcFile}", hhcFile);
            }
        }
    }

    private static async Task ExtractHhcStructure(string hhcFile, StringBuilder extractedText)
    {
        var html = await File.ReadAllTextAsync(hhcFile, Encoding.UTF8);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        extractedText.AppendLine("=== Table of Contents ===");

        var tocItems = doc.DocumentNode.Descendants("param")
            .Where(n => string.Equals(n.GetAttributeValue("name", ""), "name", StringComparison.OrdinalIgnoreCase))
            .Select(n => n.GetAttributeValue("value", ""))
            .Where(v => !string.IsNullOrWhiteSpace(v));

        foreach (var item in tocItems)
        {
            extractedText.AppendLine($"- {item}");
        }

        extractedText.AppendLine();
    }

    public string GetMimeType() => "application/vnd.ms-htmlhelp";

    private bool _disposed = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to clean up temp directory {TempDir} during disposal", _tempDir);
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}