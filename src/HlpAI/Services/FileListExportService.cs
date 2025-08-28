using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using HlpAI.MCP;

namespace HlpAI.Services;

/// <summary>
/// Service for exporting file lists in various formats with configurable options
/// </summary>
public class FileListExportService : IDisposable
{
    private readonly ILogger? _logger;
    private readonly SqliteConfigurationService _configService;
    private bool _disposed = false;

    public FileListExportService(ILogger? logger = null)
    {
        _logger = logger;
        _configService = SqliteConfigurationService.GetInstance(logger);
    }

    public FileListExportService(SqliteConfigurationService configService, ILogger? logger = null)
    {
        _logger = logger;
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    /// <summary>
    /// Export file list to specified format
    /// </summary>
    /// <param name="resources">List of file resources to export</param>
    /// <param name="format">Export format (csv, json, txt, xml)</param>
    /// <param name="outputPath">Output file path</param>
    /// <param name="includeMetadata">Whether to include file metadata in export</param>
    /// <returns>Export result information</returns>
    public async Task<FileExportResult> ExportFileListAsync(
        List<ResourceInfo> resources, 
        FileExportFormat format, 
        string outputPath,
        bool includeMetadata = true)
    {
        try
        {
            var exportSettings = await GetExportSettingsAsync();
            var content = format switch
            {
                FileExportFormat.Csv => GenerateCsvContent(resources, includeMetadata, exportSettings),
                FileExportFormat.Json => GenerateJsonContent(resources, includeMetadata, exportSettings),
                FileExportFormat.Txt => GenerateTxtContent(resources, includeMetadata, exportSettings),
                FileExportFormat.Xml => GenerateXmlContent(resources, includeMetadata, exportSettings),
                _ => throw new ArgumentException($"Unsupported export format: {format}")
            };

            await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8);

            var result = new FileExportResult
            {
                Success = true,
                OutputPath = outputPath,
                Format = format,
                ExportedCount = resources.Count,
                FileSizeBytes = new FileInfo(outputPath).Length,
                ExportedAt = DateTime.UtcNow
            };

            _logger?.LogInformation("Exported {Count} files to {Format} format at {Path}", 
                resources.Count, format, outputPath);

            await LogExportOperationAsync(result);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to export file list to {Format} format at {Path}", format, outputPath);
            return new FileExportResult
            {
                Success = false,
                Format = format,
                ErrorMessage = ex.Message,
                ExportedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Generate CSV content from resource list
    /// </summary>
    private string GenerateCsvContent(List<ResourceInfo> resources, bool includeMetadata, FileExportSettings settings)
    {
        var csv = new StringBuilder();
        
        // Header
        if (includeMetadata)
        {
            csv.AppendLine("Uri,Name,Description,MimeType,RelativePath,FileSize,LastModified");
        }
        else
        {
            csv.AppendLine("Uri,Name");
        }

        // Data rows
        foreach (var resource in resources)
        {
            if (includeMetadata)
            {
                var fileInfo = GetFileInfo(resource.Uri);
                csv.AppendLine($"\"{EscapeCsv(resource.Uri)}\",\"{EscapeCsv(resource.Name)}\",\"{EscapeCsv(resource.Description ?? "")}\",\"{EscapeCsv(resource.MimeType ?? "")}\",\"{EscapeCsv(GetRelativePath(resource.Uri))}\",{fileInfo?.Length ?? 0},\"{fileInfo?.LastWriteTime:yyyy-MM-dd HH:mm:ss}\"");
            }
            else
            {
                csv.AppendLine($"\"{EscapeCsv(resource.Uri)}\",\"{EscapeCsv(resource.Name)}\"");
            }
        }

        return csv.ToString();
    }

    /// <summary>
    /// Generate JSON content from resource list
    /// </summary>
    private string GenerateJsonContent(List<ResourceInfo> resources, bool includeMetadata, FileExportSettings settings)
    {
        var exportData = new
        {
            exportInfo = new
            {
                exportedAt = DateTime.UtcNow,
                totalFiles = resources.Count,
                includeMetadata,
                format = "json"
            },
            files = resources.Select(r => {
                var baseInfo = new Dictionary<string, object?>
                {
                    ["uri"] = r.Uri,
                    ["name"] = r.Name
                };

                if (includeMetadata)
                {
                    var fileInfo = GetFileInfo(r.Uri);
                    baseInfo["description"] = r.Description;
                    baseInfo["mimeType"] = r.MimeType;
                    baseInfo["relativePath"] = GetRelativePath(r.Uri);
                    baseInfo["fileSize"] = fileInfo?.Length ?? 0;
                    baseInfo["lastModified"] = fileInfo?.LastWriteTime;
                }

                return baseInfo;
            }).ToList()
        };

        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions 
        { 
            WriteIndented = settings.PrettyPrint,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Generate plain text content from resource list
    /// </summary>
    private string GenerateTxtContent(List<ResourceInfo> resources, bool includeMetadata, FileExportSettings settings)
    {
        var txt = new StringBuilder();
        
        txt.AppendLine("File List Export");
        txt.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        txt.AppendLine($"Total Files: {resources.Count}");
        txt.AppendLine(new string('=', 50));
        txt.AppendLine();

        foreach (var resource in resources.OrderBy(r => r.Name))
        {
            txt.AppendLine($"Name: {resource.Name}");
            txt.AppendLine($"URI: {resource.Uri}");
            
            if (includeMetadata)
            {
                var fileInfo = GetFileInfo(resource.Uri);
                txt.AppendLine($"Path: {GetRelativePath(resource.Uri)}");
                txt.AppendLine($"Type: {resource.MimeType ?? "Unknown"}");
                txt.AppendLine($"Size: {fileInfo?.Length ?? 0} bytes");
                txt.AppendLine($"Modified: {fileInfo?.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                if (!string.IsNullOrEmpty(resource.Description))
                    txt.AppendLine($"Description: {resource.Description}");
            }
            
            txt.AppendLine(new string('-', 30));
            txt.AppendLine();
        }

        return txt.ToString();
    }

    /// <summary>
    /// Generate XML content from resource list
    /// </summary>
    private string GenerateXmlContent(List<ResourceInfo> resources, bool includeMetadata, FileExportSettings settings)
    {
        var xml = new StringBuilder();
        
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<fileList>");
        xml.AppendLine($"  <exportInfo>");
        xml.AppendLine($"    <exportedAt>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</exportedAt>");
        xml.AppendLine($"    <totalFiles>{resources.Count}</totalFiles>");
        xml.AppendLine($"    <includeMetadata>{includeMetadata.ToString().ToLower()}</includeMetadata>");
        xml.AppendLine($"  </exportInfo>");
        xml.AppendLine("  <files>");

        foreach (var resource in resources)
        {
            xml.AppendLine("    <file>");
            xml.AppendLine($"      <uri>{EscapeXml(resource.Uri)}</uri>");
            xml.AppendLine($"      <name>{EscapeXml(resource.Name)}</name>");
            
            if (includeMetadata)
            {
                var fileInfo = GetFileInfo(resource.Uri);
                xml.AppendLine($"      <relativePath>{EscapeXml(GetRelativePath(resource.Uri))}</relativePath>");
                xml.AppendLine($"      <mimeType>{EscapeXml(resource.MimeType ?? "")}</mimeType>");
                xml.AppendLine($"      <fileSize>{fileInfo?.Length ?? 0}</fileSize>");
                xml.AppendLine($"      <lastModified>{fileInfo?.LastWriteTime:yyyy-MM-ddTHH:mm:ssZ}</lastModified>");
                if (!string.IsNullOrEmpty(resource.Description))
                    xml.AppendLine($"      <description>{EscapeXml(resource.Description)}</description>");
            }
            
            xml.AppendLine("    </file>");
        }

        xml.AppendLine("  </files>");
        xml.AppendLine("</fileList>");

        return xml.ToString();
    }

    /// <summary>
    /// Get file information from URI
    /// </summary>
    private FileInfo? GetFileInfo(string uri)
    {
        try
        {
            var path = uri.StartsWith("file:///") ? uri[8..] : uri;
            path = path.Replace('/', Path.DirectorySeparatorChar);
            return File.Exists(path) ? new FileInfo(path) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get relative path from URI
    /// </summary>
    private string GetRelativePath(string uri)
    {
        try
        {
            var path = uri.StartsWith("file:///") ? uri[8..] : uri;
            return path.Replace('/', Path.DirectorySeparatorChar);
        }
        catch
        {
            return uri;
        }
    }

    /// <summary>
    /// Escape CSV field content
    /// </summary>
    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\"", "\"\"");
    }

    /// <summary>
    /// Escape XML content
    /// </summary>
    private string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    /// <summary>
    /// Get export settings from configuration
    /// </summary>
    private async Task<FileExportSettings> GetExportSettingsAsync()
    {
        var prettyPrint = await _configService.GetConfigurationAsync("export_pretty_print", "file_export", "true");
        var includeHeaders = await _configService.GetConfigurationAsync("export_include_headers", "file_export", "true");
        var defaultFormat = await _configService.GetConfigurationAsync("export_default_format", "file_export", "csv");

        return new FileExportSettings
        {
            PrettyPrint = bool.Parse(prettyPrint ?? "true"),
            IncludeHeaders = bool.Parse(includeHeaders ?? "true"),
            DefaultFormat = Enum.TryParse<FileExportFormat>(defaultFormat, true, out var format) ? format : FileExportFormat.Csv
        };
    }

    /// <summary>
    /// Set export settings in configuration
    /// </summary>
    public async Task<bool> SetExportSettingsAsync(FileExportSettings settings)
    {
        try
        {
            await _configService.SetConfigurationAsync("export_pretty_print", settings.PrettyPrint.ToString().ToLower(), "file_export");
            await _configService.SetConfigurationAsync("export_include_headers", settings.IncludeHeaders.ToString().ToLower(), "file_export");
            await _configService.SetConfigurationAsync("export_default_format", settings.DefaultFormat.ToString().ToLower(), "file_export");

            _logger?.LogInformation("Export settings updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update export settings");
            return false;
        }
    }

    /// <summary>
    /// Log export operation for audit trail
    /// </summary>
    private async Task LogExportOperationAsync(FileExportResult result)
    {
        try
        {
            var logEntry = JsonSerializer.Serialize(new
            {
                operation = "file_list_export",
                success = result.Success,
                format = result.Format.ToString(),
                outputPath = result.OutputPath,
                exportedCount = result.ExportedCount,
                fileSizeBytes = result.FileSizeBytes,
                exportedAt = result.ExportedAt,
                errorMessage = result.ErrorMessage
            });

            // Use a unique key with milliseconds and a random component to prevent collisions
            var key = $"export_log_{result.ExportedAt:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}";
            await _configService.SetConfigurationAsync(key, logEntry, "export_logs");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to log export operation");
        }
    }

    /// <summary>
    /// Get export operation history
    /// </summary>
    public async Task<List<FileExportResult>> GetExportHistoryAsync(int maxRecords = 50)
    {
        try
        {
            var exportLogs = await _configService.GetCategoryConfigurationAsync("export_logs");
            var results = new List<FileExportResult>();

            foreach (var log in exportLogs.OrderByDescending(kvp => kvp.Key).Take(maxRecords))
            {
                try
                {
                    var logData = JsonSerializer.Deserialize<Dictionary<string, object>>(log.Value ?? string.Empty);
                    if (logData != null && logData.ContainsKey("operation") && 
                        logData["operation"].ToString() == "file_list_export")
                    {
                        var result = new FileExportResult
                        {
                            Success = bool.Parse(logData["success"].ToString() ?? "false"),
                            Format = Enum.Parse<FileExportFormat>(logData["format"].ToString() ?? "Csv", true),
                            OutputPath = logData["outputPath"]?.ToString() ?? "",
                            ExportedCount = int.Parse(logData["exportedCount"]?.ToString() ?? "0"),
                            FileSizeBytes = long.Parse(logData["fileSizeBytes"]?.ToString() ?? "0"),
                            ExportedAt = DateTime.Parse(logData["exportedAt"]?.ToString() ?? DateTime.MinValue.ToString()),
                            ErrorMessage = logData["errorMessage"]?.ToString()
                        };
                        results.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse export log entry: {Key}", log.Key);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve export history");
            return [];
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                _configService?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing FileListExportService");
            }
            
            _disposed = true;
        }
    }
}

/// <summary>
/// Supported file export formats
/// </summary>
public enum FileExportFormat
{
    Csv,
    Json,
    Txt,
    Xml
}

/// <summary>
/// Export settings configuration
/// </summary>
public class FileExportSettings
{
    public bool PrettyPrint { get; set; } = true;
    public bool IncludeHeaders { get; set; } = true;
    public FileExportFormat DefaultFormat { get; set; } = FileExportFormat.Csv;
}

/// <summary>
/// Result of file export operation
/// </summary>
public class FileExportResult
{
    public bool Success { get; set; }
    public FileExportFormat Format { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public int ExportedCount { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime ExportedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
