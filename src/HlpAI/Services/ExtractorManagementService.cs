using System.Text.Json;
using Microsoft.Extensions.Logging;
using HlpAI.FileExtractors;
using HlpAI.Models;

namespace HlpAI.Services;

/// <summary>
/// Service for managing file type extractors and their supported file extensions.
/// Provides functionality to dynamically add/remove file extensions from extractors,
/// test extraction capabilities, and store configurations in SQLite.
/// </summary>
public class ExtractorManagementService : IDisposable
{
    private readonly ILogger? _logger;
    private readonly SqliteConfigurationService _configService;
    
    private static readonly Dictionary<string, ExtractorInfo> DefaultExtractors = new()
    {
        ["text"] = new ExtractorInfo 
        { 
            Name = "Text File Extractor", 
            Type = "TextFileExtractor",
            DefaultExtensions = [".txt", ".md", ".log", ".csv"],
            MimeType = "text/plain",
            Description = "Extracts plain text content from text-based files"
        },
        ["html"] = new ExtractorInfo 
        { 
            Name = "HTML File Extractor", 
            Type = "HtmlFileExtractor",
            DefaultExtensions = [".html", ".htm"],
            MimeType = "text/html", 
            Description = "Extracts text content from HTML files, removing script and style tags"
        },
        ["pdf"] = new ExtractorInfo 
        { 
            Name = "PDF File Extractor", 
            Type = "PdfFileExtractor",
            DefaultExtensions = [".pdf"],
            MimeType = "application/pdf",
            Description = "Extracts text content from PDF documents using iText library"
        },
        ["chm"] = new ExtractorInfo 
        { 
            Name = "CHM File Extractor", 
            Type = "ChmFileExtractor",
            DefaultExtensions = [".chm"],
            MimeType = "application/vnd.ms-htmlhelp",
            Description = "Extracts content from compiled HTML help files using hh.exe"
        },
        ["hhc"] = new ExtractorInfo 
        { 
            Name = "HHC File Extractor", 
            Type = "HhcFileExtractor",
            DefaultExtensions = [".hhc"],
            MimeType = "text/html",
            Description = "Extracts table of contents information from HTML Help contents files"
        }
    };

    public ExtractorManagementService(ILogger? logger = null) 
        : this(SqliteConfigurationService.GetInstance(logger), logger)
    {
    }

    public ExtractorManagementService(SqliteConfigurationService configService, ILogger? logger = null)
    {
        _logger = logger;
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    /// <summary>
    /// Gets all available extractors with their current file extension mappings
    /// </summary>
    public async Task<Dictionary<string, ExtractorInfo>> GetExtractorsAsync()
    {
        var extractors = new Dictionary<string, ExtractorInfo>();
        
        foreach (var (key, defaultInfo) in DefaultExtractors)
        {
            var storedConfig = await _configService.GetConfigurationAsync($"extractor_{key}", "ExtractorManagement");
            
            if (!string.IsNullOrEmpty(storedConfig))
            {
                try
                {
                    var customInfo = JsonSerializer.Deserialize<ExtractorInfo>(storedConfig);
                    if (customInfo != null)
                    {
                        extractors[key] = customInfo;
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to deserialize stored extractor config for {ExtractorKey}", key);
                }
            }
            
            // Use default configuration
            extractors[key] = defaultInfo with { CustomExtensions = defaultInfo.DefaultExtensions.ToList() };
        }
        
        return extractors;
    }

    /// <summary>
    /// Adds a file extension to an extractor's supported extensions
    /// </summary>
    public async Task<bool> AddFileExtensionAsync(string extractorKey, string extension)
    {
        if (string.IsNullOrWhiteSpace(extractorKey) || string.IsNullOrWhiteSpace(extension))
            return false;

        extension = NormalizeExtension(extension);
        
        var extractors = await GetExtractorsAsync();
        if (!extractors.TryGetValue(extractorKey, out var extractor))
        {
            _logger?.LogWarning("Extractor {ExtractorKey} not found", extractorKey);
            return false;
        }

        if (extractor.CustomExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            _logger?.LogInformation("Extension {Extension} already exists for extractor {ExtractorKey}", extension, extractorKey);
            return true;
        }

        // Check if extension is already handled by another extractor
        var conflictingExtractor = extractors.Values.FirstOrDefault(e => 
            e.CustomExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
        
        if (conflictingExtractor != null)
        {
            _logger?.LogWarning("Extension {Extension} is already handled by {ExtractorName}", 
                extension, conflictingExtractor.Name);
            return false;
        }

        extractor.CustomExtensions.Add(extension);
        await SaveExtractorConfigAsync(extractorKey, extractor);
        
        _logger?.LogInformation("Added extension {Extension} to extractor {ExtractorKey}", extension, extractorKey);
        return true;
    }

    /// <summary>
    /// Removes a file extension from an extractor's supported extensions
    /// </summary>
    public async Task<bool> RemoveFileExtensionAsync(string extractorKey, string extension)
    {
        if (string.IsNullOrWhiteSpace(extractorKey) || string.IsNullOrWhiteSpace(extension))
            return false;

        extension = NormalizeExtension(extension);
        
        var extractors = await GetExtractorsAsync();
        if (!extractors.TryGetValue(extractorKey, out var extractor))
        {
            _logger?.LogWarning("Extractor {ExtractorKey} not found", extractorKey);
            return false;
        }

        var removed = extractor.CustomExtensions.RemoveAll(e => 
            string.Equals(e, extension, StringComparison.OrdinalIgnoreCase)) > 0;

        if (removed)
        {
            await SaveExtractorConfigAsync(extractorKey, extractor);
            _logger?.LogInformation("Removed extension {Extension} from extractor {ExtractorKey}", extension, extractorKey);
        }
        else
        {
            _logger?.LogInformation("Extension {Extension} was not found in extractor {ExtractorKey}", extension, extractorKey);
        }

        return removed;
    }

    /// <summary>
    /// Tests if a file can be extracted using the configured extractors
    /// </summary>
    public async Task<ExtractionTestResult> TestFileExtractionAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new ExtractionTestResult
            {
                Success = false,
                ErrorMessage = "File does not exist",
                FilePath = filePath
            };
        }

        var extractors = await GetExtractorsAsync();
        var fileExtension = Path.GetExtension(filePath);
        
        var matchingExtractor = extractors.Values.FirstOrDefault(e => 
            e.CustomExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase));

        if (matchingExtractor == null)
        {
            return new ExtractionTestResult
            {
                Success = false,
                ErrorMessage = $"No extractor configured for extension {fileExtension}",
                FilePath = filePath,
                FileExtension = fileExtension
            };
        }

        try
        {
            var extractor = CreateExtractorInstance(matchingExtractor.Type);
            if (extractor == null)
            {
                return new ExtractionTestResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to create extractor instance for {matchingExtractor.Type}",
                    FilePath = filePath,
                    FileExtension = fileExtension,
                    ExtractorUsed = matchingExtractor.Name
                };
            }

            var startTime = DateTime.Now;
            var content = await extractor.ExtractTextAsync(filePath);
            var extractionTime = DateTime.Now - startTime;

            var fileInfo = new FileInfo(filePath);
            
            return new ExtractionTestResult
            {
                Success = true,
                FilePath = filePath,
                FileExtension = fileExtension,
                ExtractorUsed = matchingExtractor.Name,
                ContentLength = content?.Length ?? 0,
                ExtractionTimeMs = (int)extractionTime.TotalMilliseconds,
                FileSizeBytes = fileInfo.Length,
                ContentPreview = content?.Length > 200 ? content[..200] + "..." : content
            };
        }
        catch (Exception ex)
        {
            return new ExtractionTestResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                FilePath = filePath,
                FileExtension = fileExtension,
                ExtractorUsed = matchingExtractor.Name
            };
        }
    }

    /// <summary>
    /// Gets extraction statistics and performance metrics
    /// </summary>
    public async Task<ExtractionStatistics> GetExtractionStatisticsAsync()
    {
        var extractors = await GetExtractorsAsync();
        var stats = new ExtractionStatistics();

        foreach (var (key, extractor) in extractors)
        {
            stats.ExtractorStats[key] = new ExtractorStats
            {
                Name = extractor.Name,
                Type = extractor.Type,
                SupportedExtensionCount = extractor.CustomExtensions.Count,
                DefaultExtensionCount = extractor.DefaultExtensions.Count,
                CustomExtensionCount = extractor.CustomExtensions.Count - extractor.DefaultExtensions.Count,
                SupportedExtensions = extractor.CustomExtensions.ToList()
            };
        }

        stats.TotalExtractors = extractors.Count;
        stats.TotalSupportedExtensions = extractors.Values.SelectMany(e => e.CustomExtensions).Distinct().Count();
        
        return stats;
    }

    /// <summary>
    /// Resets an extractor to its default configuration
    /// </summary>
    public async Task<bool> ResetExtractorToDefaultAsync(string extractorKey)
    {
        if (!DefaultExtractors.TryGetValue(extractorKey, out var defaultExtractor))
        {
            _logger?.LogWarning("Unknown extractor key: {ExtractorKey}", extractorKey);
            return false;
        }

        await _configService.RemoveConfigurationAsync($"extractor_{extractorKey}", "ExtractorManagement");
        _logger?.LogInformation("Reset extractor {ExtractorKey} to default configuration", extractorKey);
        return true;
    }

    /// <summary>
    /// Gets the configured extractors as IFileExtractor instances
    /// </summary>
    public async Task<List<IFileExtractor>> GetConfiguredExtractorInstancesAsync()
    {
        var extractors = new List<IFileExtractor>();
        var extractorConfigs = await GetExtractorsAsync();

        foreach (var config in extractorConfigs.Values)
        {
            var extractor = CreateExtractorInstance(config.Type);
            if (extractor != null)
            {
                extractors.Add(new ConfigurableFileExtractor(extractor, config.CustomExtensions));
            }
        }

        return extractors;
    }

    private async Task SaveExtractorConfigAsync(string extractorKey, ExtractorInfo extractor)
    {
        var json = JsonSerializer.Serialize(extractor, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        await _configService.SetConfigurationAsync($"extractor_{extractorKey}", json, "ExtractorManagement");
    }

    private IFileExtractor? CreateExtractorInstance(string extractorType)
    {
        return extractorType switch
        {
            "TextFileExtractor" => new TextFileExtractor(),
            "HtmlFileExtractor" => new HtmlFileExtractor(),
            "PdfFileExtractor" => new PdfFileExtractor(),
            "ChmFileExtractor" => new ChmFileExtractor(_logger),
            "HhcFileExtractor" => new HhcFileExtractor(),
            _ => null
        };
    }

    private static string NormalizeExtension(string extension)
    {
        if (!extension.StartsWith('.'))
            extension = '.' + extension;
        return extension.ToLowerInvariant();
    }

    public void Dispose()
    {
        _configService?.Dispose();
    }
}

/// <summary>
/// Information about a file extractor and its configuration
/// </summary>
public record ExtractorInfo
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<string> DefaultExtensions { get; init; } = [];
    public List<string> CustomExtensions { get; set; } = [];
}

/// <summary>
/// Result of testing file extraction
/// </summary>
public record ExtractionTestResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string FileExtension { get; init; } = string.Empty;
    public string? ExtractorUsed { get; init; }
    public int ContentLength { get; init; }
    public int ExtractionTimeMs { get; init; }
    public long FileSizeBytes { get; init; }
    public string? ContentPreview { get; init; }
}

/// <summary>
/// Statistics about extraction capabilities
/// </summary>
public record ExtractionStatistics
{
    public int TotalExtractors { get; set; }
    public int TotalSupportedExtensions { get; set; }
    public Dictionary<string, ExtractorStats> ExtractorStats { get; set; } = [];
}

/// <summary>
/// Statistics for a specific extractor
/// </summary>
public record ExtractorStats
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int SupportedExtensionCount { get; init; }
    public int DefaultExtensionCount { get; init; }
    public int CustomExtensionCount { get; init; }
    public List<string> SupportedExtensions { get; init; } = [];
}

/// <summary>
/// Wrapper for IFileExtractor that uses configurable extensions instead of hard-coded ones
/// </summary>
internal class ConfigurableFileExtractor : IFileExtractor
{
    private readonly IFileExtractor _innerExtractor;
    private readonly List<string> _supportedExtensions;

    public ConfigurableFileExtractor(IFileExtractor innerExtractor, List<string> supportedExtensions)
    {
        _innerExtractor = innerExtractor;
        _supportedExtensions = supportedExtensions;
    }

    public bool CanHandle(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(extension) && 
               _supportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public Task<string> ExtractTextAsync(string filePath) => _innerExtractor.ExtractTextAsync(filePath);

    public string GetMimeType() => _innerExtractor.GetMimeType();
}