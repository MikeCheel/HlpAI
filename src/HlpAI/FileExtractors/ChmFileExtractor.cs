using System.Diagnostics;
using System.Text;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using HlpAI.Models;
using HlpAI.Services;
using SystemPath = System.IO.Path;

namespace HlpAI.FileExtractors;

public class ChmFileExtractor(ILogger? logger = null, AppConfiguration? config = null) : IFileExtractor, IDisposable
{
    private readonly ILogger? _logger = logger;
    private readonly AppConfiguration _config = config ?? ConfigurationService.LoadConfiguration(logger);
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
                if (!File.Exists(filePath))
                {
                    _logger?.LogWarning("CHM file does not exist: {FilePath}", filePath);
                    return $"Error: CHM file not found: {filePath}";
                }
                
                Directory.CreateDirectory(_tempDir);

                var hhExePath = ConfigurationService.GetHhExePath(_logger);
                var processInfo = new ProcessStartInfo
                {
                    FileName = hhExePath,
                    Arguments = $"-decompile \"{_tempDir}\" \"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = SystemPath.GetDirectoryName(filePath) ?? Environment.CurrentDirectory
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    // Start reading output and error streams immediately to prevent deadlocks
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    // Wait for the process to complete with a reasonable timeout (30 seconds)
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    try
                    {
                        await process.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogWarning("HH.exe process timed out after 30 seconds for file {FilePath}", filePath);
                        process.Kill(true);
                        return "Error: CHM extraction timed out";
                    }

                    var output = await outputTask;
                    var error = await errorTask;

                    if (process.ExitCode != 0)
                    {
                        _logger?.LogWarning("HH.exe returned exit code {ExitCode}: {Error}", process.ExitCode, error);
                    }
                    
                    // Log output for debugging purposes
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        _logger?.LogDebug("HH.exe output: {Output}", output);
                    }
                }

                var extractedText = new StringBuilder();
                
                // Log temp directory contents for debugging
                if (Directory.Exists(_tempDir))
                {
                    var files = Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories);
                    _logger?.LogDebug("Found {FileCount} files in temp directory {TempDir}: {Files}", 
                        files.Length, _tempDir, string.Join(", ", files.Take(_config.MaxChmExtractorFilesDisplayed)));
                }
                else
                {
                    _logger?.LogWarning("Temp directory {TempDir} does not exist after CHM extraction", _tempDir);
                }
                
                await ExtractTextFromDirectory(_tempDir, extractedText);

                var result = extractedText.ToString();
                _logger?.LogDebug("Extracted {TextLength} characters from CHM file {FilePath}", 
                    result.Length, filePath);
                    
                return result;
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