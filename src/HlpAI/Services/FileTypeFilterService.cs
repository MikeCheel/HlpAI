using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace HlpAI.Services;

/// <summary>
/// Service for managing file type filtering with include/exclude patterns
/// </summary>
public class FileTypeFilterService : IDisposable
{
    private readonly ILogger? _logger;
    private readonly SqliteConfigurationService _configService;
    private bool _disposed = false;

    // Default supported file types from current extractors
    private static readonly string[] DefaultSupportedTypes = 
    {
        ".txt", ".md", ".log", ".csv",    // TextFileExtractor
        ".html", ".htm",                  // HtmlFileExtractor
        ".pdf",                          // PdfFileExtractor
        ".chm",                          // ChmFileExtractor
        ".hhc"                           // HhcFileExtractor
    };

    // Default exclude patterns to prevent system files from being processed
    private static readonly string[] DefaultExcludePatterns = 
    {
        "vector.db",      // Vector database file
        "vectors.db",     // Vector database file (alternative name)
        "*.db-*",         // Database backup files
        "config.db"       // Configuration database
    };

    public FileTypeFilterService(ILogger? logger = null)
    {
        _logger = logger;
        _configService = new SqliteConfigurationService(logger);
    }

    /// <summary>
    /// Check if a file should be processed based on current filter configuration
    /// </summary>
    public async Task<bool> ShouldProcessFileAsync(string filePath)
    {
        try
        {
            var filterConfig = await GetFilterConfigurationAsync();
            return ShouldProcessFile(filePath, filterConfig);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking if file should be processed: {FilePath}", filePath);
            // Default to allowing file if there's an error
            return true;
        }
    }

    /// <summary>
    /// Check if a file should be processed based on provided filter configuration
    /// </summary>
    public bool ShouldProcessFile(string filePath, FileTypeFilterConfiguration filterConfig)
    {
        var fileName = Path.GetFileName(filePath);
        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

        // First check exclude patterns - if any match, reject the file
        if (filterConfig.ExcludePatterns?.Any() == true)
        {
            foreach (var pattern in filterConfig.ExcludePatterns)
            {
                if (MatchesPattern(fileName, pattern) || MatchesPattern(filePath, pattern))
                {
                    _logger?.LogDebug("File excluded by pattern '{Pattern}': {FilePath}", pattern, filePath);
                    return false;
                }
            }
        }

        // Then check include patterns - if specified, at least one must match
        if (filterConfig.IncludePatterns?.Any() == true)
        {
            var matchesInclude = false;
            foreach (var pattern in filterConfig.IncludePatterns)
            {
                if (MatchesPattern(fileName, pattern) || MatchesPattern(filePath, pattern))
                {
                    matchesInclude = true;
                    break;
                }
            }

            if (!matchesInclude)
            {
                _logger?.LogDebug("File does not match any include patterns: {FilePath}", filePath);
                return false;
            }
        }

        // Check if file extension is supported
        if (filterConfig.OnlySupportedTypes && !string.IsNullOrEmpty(fileExtension))
        {
            var supportedTypes = filterConfig.SupportedTypes?.Any() == true 
                ? filterConfig.SupportedTypes 
                : DefaultSupportedTypes.ToList();

            if (!supportedTypes.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
            {
                _logger?.LogDebug("File extension not supported: {Extension} for file {FilePath}", fileExtension, filePath);
                return false;
            }
        }

        // Check file size limits
        if (filterConfig.MaxFileSizeBytes.HasValue || filterConfig.MinFileSizeBytes.HasValue)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (filterConfig.MaxFileSizeBytes.HasValue && fileInfo.Length > filterConfig.MaxFileSizeBytes.Value)
                {
                    _logger?.LogDebug("File too large: {Size} bytes > {MaxSize} bytes for file {FilePath}", 
                        fileInfo.Length, filterConfig.MaxFileSizeBytes.Value, filePath);
                    return false;
                }

                if (filterConfig.MinFileSizeBytes.HasValue && fileInfo.Length < filterConfig.MinFileSizeBytes.Value)
                {
                    _logger?.LogDebug("File too small: {Size} bytes < {MinSize} bytes for file {FilePath}", 
                        fileInfo.Length, filterConfig.MinFileSizeBytes.Value, filePath);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not check file size for {FilePath}", filePath);
            }
        }

        // Check file age limits
        if (filterConfig.MaxFileAgeDays.HasValue || filterConfig.MinFileAgeHours.HasValue)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileAge = DateTime.UtcNow - fileInfo.LastWriteTimeUtc;

                if (filterConfig.MaxFileAgeDays.HasValue && fileAge.TotalDays > filterConfig.MaxFileAgeDays.Value)
                {
                    _logger?.LogDebug("File too old: {Age} days > {MaxAge} days for file {FilePath}", 
                        fileAge.TotalDays, filterConfig.MaxFileAgeDays.Value, filePath);
                    return false;
                }

                if (filterConfig.MinFileAgeHours.HasValue && fileAge.TotalHours < filterConfig.MinFileAgeHours.Value)
                {
                    _logger?.LogDebug("File too new: {Age} hours < {MinAge} hours for file {FilePath}", 
                        fileAge.TotalHours, filterConfig.MinFileAgeHours.Value, filePath);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not check file age for {FilePath}", filePath);
            }
        }

        return true;
    }

    /// <summary>
    /// Filter a list of files based on current configuration
    /// </summary>
    public async Task<FileFilterResult> FilterFilesAsync(IEnumerable<string> filePaths)
    {
        var result = new FileFilterResult();
        var filterConfig = await GetFilterConfigurationAsync();

        foreach (var filePath in filePaths)
        {
            try
            {
                if (ShouldProcessFile(filePath, filterConfig))
                {
                    result.AcceptedFiles.Add(filePath);
                }
                else
                {
                    result.RejectedFiles.Add(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error filtering file {FilePath}", filePath);
                result.ErrorFiles.Add(filePath);
            }
        }

        result.TotalProcessed = result.AcceptedFiles.Count + result.RejectedFiles.Count + result.ErrorFiles.Count;
        return result;
    }

    /// <summary>
    /// Get current filter configuration
    /// </summary>
    public async Task<FileTypeFilterConfiguration> GetFilterConfigurationAsync()
    {
        try
        {
            var config = new FileTypeFilterConfiguration();

            // Load include patterns
            var includePatterns = await _configService.GetConfigurationAsync("include_patterns", "file_filtering");
            if (!string.IsNullOrEmpty(includePatterns))
            {
                config.IncludePatterns = JsonSerializer.Deserialize<List<string>>(includePatterns) ?? [];
            }

            // Load exclude patterns
            var excludePatterns = await _configService.GetConfigurationAsync("exclude_patterns", "file_filtering");
            if (!string.IsNullOrEmpty(excludePatterns))
            {
                config.ExcludePatterns = JsonSerializer.Deserialize<List<string>>(excludePatterns) ?? [];
            }
            else
            {
                // Use default exclude patterns if none are configured
                config.ExcludePatterns = DefaultExcludePatterns.ToList();
            }

            // Load supported types
            var supportedTypes = await _configService.GetConfigurationAsync("supported_types", "file_filtering");
            if (!string.IsNullOrEmpty(supportedTypes))
            {
                config.SupportedTypes = JsonSerializer.Deserialize<List<string>>(supportedTypes) ?? DefaultSupportedTypes.ToList();
            }
            else
            {
                config.SupportedTypes = DefaultSupportedTypes.ToList();
            }

            // Load other settings
            config.OnlySupportedTypes = bool.Parse(await _configService.GetConfigurationAsync("only_supported_types", "file_filtering", "true") ?? "true");
            config.CaseSensitivePatterns = bool.Parse(await _configService.GetConfigurationAsync("case_sensitive_patterns", "file_filtering", "false") ?? "false");
            
            var maxSizeStr = await _configService.GetConfigurationAsync("max_file_size_bytes", "file_filtering");
            if (!string.IsNullOrEmpty(maxSizeStr) && long.TryParse(maxSizeStr, out var maxSize))
            {
                config.MaxFileSizeBytes = maxSize;
            }

            var minSizeStr = await _configService.GetConfigurationAsync("min_file_size_bytes", "file_filtering");
            if (!string.IsNullOrEmpty(minSizeStr) && long.TryParse(minSizeStr, out var minSize))
            {
                config.MinFileSizeBytes = minSize;
            }

            var maxAgeStr = await _configService.GetConfigurationAsync("max_file_age_days", "file_filtering");
            if (!string.IsNullOrEmpty(maxAgeStr) && int.TryParse(maxAgeStr, out var maxAge))
            {
                config.MaxFileAgeDays = maxAge;
            }

            var minAgeStr = await _configService.GetConfigurationAsync("min_file_age_hours", "file_filtering");
            if (!string.IsNullOrEmpty(minAgeStr) && int.TryParse(minAgeStr, out var minAge))
            {
                config.MinFileAgeHours = minAge;
            }

            return config;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading filter configuration");
            return new FileTypeFilterConfiguration
            {
                SupportedTypes = DefaultSupportedTypes.ToList(),
                OnlySupportedTypes = true
            };
        }
    }

    /// <summary>
    /// Update filter configuration
    /// </summary>
    public async Task<bool> SetFilterConfigurationAsync(FileTypeFilterConfiguration config)
    {
        try
        {
            // Save include patterns
            if (config.IncludePatterns?.Any() == true)
            {
                var includePatternsJson = JsonSerializer.Serialize(config.IncludePatterns);
                await _configService.SetConfigurationAsync("include_patterns", includePatternsJson, "file_filtering");
            }
            else
            {
                await _configService.SetConfigurationAsync("include_patterns", null, "file_filtering");
            }

            // Save exclude patterns
            if (config.ExcludePatterns?.Any() == true)
            {
                var excludePatternsJson = JsonSerializer.Serialize(config.ExcludePatterns);
                await _configService.SetConfigurationAsync("exclude_patterns", excludePatternsJson, "file_filtering");
            }
            else
            {
                await _configService.SetConfigurationAsync("exclude_patterns", null, "file_filtering");
            }

            // Save supported types
            var supportedTypesJson = JsonSerializer.Serialize(config.SupportedTypes ?? DefaultSupportedTypes.ToList());
            await _configService.SetConfigurationAsync("supported_types", supportedTypesJson, "file_filtering");

            // Save other settings
            await _configService.SetConfigurationAsync("only_supported_types", config.OnlySupportedTypes.ToString().ToLower(), "file_filtering");
            await _configService.SetConfigurationAsync("case_sensitive_patterns", config.CaseSensitivePatterns.ToString().ToLower(), "file_filtering");

            if (config.MaxFileSizeBytes.HasValue)
            {
                await _configService.SetConfigurationAsync("max_file_size_bytes", config.MaxFileSizeBytes.Value.ToString(), "file_filtering");
            }
            else
            {
                await _configService.SetConfigurationAsync("max_file_size_bytes", null, "file_filtering");
            }

            if (config.MinFileSizeBytes.HasValue)
            {
                await _configService.SetConfigurationAsync("min_file_size_bytes", config.MinFileSizeBytes.Value.ToString(), "file_filtering");
            }
            else
            {
                await _configService.SetConfigurationAsync("min_file_size_bytes", null, "file_filtering");
            }

            if (config.MaxFileAgeDays.HasValue)
            {
                await _configService.SetConfigurationAsync("max_file_age_days", config.MaxFileAgeDays.Value.ToString(), "file_filtering");
            }
            else
            {
                await _configService.SetConfigurationAsync("max_file_age_days", null, "file_filtering");
            }

            if (config.MinFileAgeHours.HasValue)
            {
                await _configService.SetConfigurationAsync("min_file_age_hours", config.MinFileAgeHours.Value.ToString(), "file_filtering");
            }
            else
            {
                await _configService.SetConfigurationAsync("min_file_age_hours", null, "file_filtering");
            }

            _logger?.LogInformation("File filtering configuration updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving filter configuration");
            return false;
        }
    }

    /// <summary>
    /// Add include pattern
    /// </summary>
    public async Task<bool> AddIncludePatternAsync(string pattern)
    {
        try
        {
            var config = await GetFilterConfigurationAsync();
            config.IncludePatterns ??= [];
            
            if (!config.IncludePatterns.Contains(pattern, StringComparer.OrdinalIgnoreCase))
            {
                config.IncludePatterns.Add(pattern);
                return await SetFilterConfigurationAsync(config);
            }
            
            return true; // Already exists
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding include pattern: {Pattern}", pattern);
            return false;
        }
    }

    /// <summary>
    /// Add exclude pattern
    /// </summary>
    public async Task<bool> AddExcludePatternAsync(string pattern)
    {
        try
        {
            var config = await GetFilterConfigurationAsync();
            config.ExcludePatterns ??= [];
            
            if (!config.ExcludePatterns.Contains(pattern, StringComparer.OrdinalIgnoreCase))
            {
                config.ExcludePatterns.Add(pattern);
                return await SetFilterConfigurationAsync(config);
            }
            
            return true; // Already exists
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding exclude pattern: {Pattern}", pattern);
            return false;
        }
    }

    /// <summary>
    /// Remove include pattern
    /// </summary>
    public async Task<bool> RemoveIncludePatternAsync(string pattern)
    {
        try
        {
            var config = await GetFilterConfigurationAsync();
            if (config.IncludePatterns?.RemoveAll(p => string.Equals(p, pattern, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                return await SetFilterConfigurationAsync(config);
            }
            
            return true; // Didn't exist anyway
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing include pattern: {Pattern}", pattern);
            return false;
        }
    }

    /// <summary>
    /// Remove exclude pattern
    /// </summary>
    public async Task<bool> RemoveExcludePatternAsync(string pattern)
    {
        try
        {
            var config = await GetFilterConfigurationAsync();
            if (config.ExcludePatterns?.RemoveAll(p => string.Equals(p, pattern, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                return await SetFilterConfigurationAsync(config);
            }
            
            return true; // Didn't exist anyway
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing exclude pattern: {Pattern}", pattern);
            return false;
        }
    }

    /// <summary>
    /// Reset to default configuration
    /// </summary>
    public async Task<bool> ResetToDefaultsAsync()
    {
        try
        {
            await _configService.ClearCategoryAsync("file_filtering");
            _logger?.LogInformation("File filtering configuration reset to defaults");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error resetting filter configuration to defaults");
            return false;
        }
    }

    /// <summary>
    /// Test patterns against a set of file paths
    /// </summary>
    public async Task<FileFilterTestResult> TestPatternsAsync(IEnumerable<string> testFiles)
    {
        try
        {
            var config = await GetFilterConfigurationAsync();
            var result = new FileFilterTestResult
            {
                Configuration = config,
                TestFiles = testFiles.ToList()
            };

            foreach (var file in testFiles)
            {
                var shouldProcess = ShouldProcessFile(file, config);
                if (shouldProcess)
                {
                    result.AcceptedFiles.Add(file);
                }
                else
                {
                    result.RejectedFiles.Add(file);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error testing patterns");
            return new FileFilterTestResult();
        }
    }

    /// <summary>
    /// Check if a string matches a glob-style pattern
    /// </summary>
    private bool MatchesPattern(string text, string pattern)
    {
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(text))
            return false;

        // Convert glob pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        var options = RegexOptions.None;
        if (!GetFilterConfigurationAsync().Result.CaseSensitivePatterns)
        {
            options |= RegexOptions.IgnoreCase;
        }

        return Regex.IsMatch(text, regexPattern, options);
    }

    /// <summary>
    /// Get filtering statistics
    /// </summary>
    public async Task<FileFilterStatistics> GetFilterStatisticsAsync()
    {
        try
        {
            var config = await GetFilterConfigurationAsync();
            var stats = new FileFilterStatistics
            {
                IncludePatternCount = config.IncludePatterns?.Count ?? 0,
                ExcludePatternCount = config.ExcludePatterns?.Count ?? 0,
                SupportedTypeCount = config.SupportedTypes?.Count ?? DefaultSupportedTypes.Length,
                OnlySupportedTypes = config.OnlySupportedTypes,
                HasSizeFilters = config.MaxFileSizeBytes.HasValue || config.MinFileSizeBytes.HasValue,
                HasAgeFilters = config.MaxFileAgeDays.HasValue || config.MinFileAgeHours.HasValue,
                LastUpdated = DateTime.UtcNow
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting filter statistics");
            return new FileFilterStatistics();
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
                _logger?.LogError(ex, "Error disposing FileTypeFilterService");
            }
            
            _disposed = true;
        }
    }
}

/// <summary>
/// File type filter configuration
/// </summary>
public class FileTypeFilterConfiguration
{
    public List<string>? IncludePatterns { get; set; }
    public List<string>? ExcludePatterns { get; set; }
    public List<string>? SupportedTypes { get; set; }
    public bool OnlySupportedTypes { get; set; } = true;
    public bool CaseSensitivePatterns { get; set; } = false;
    public long? MaxFileSizeBytes { get; set; }
    public long? MinFileSizeBytes { get; set; }
    public int? MaxFileAgeDays { get; set; }
    public int? MinFileAgeHours { get; set; }
}

/// <summary>
/// Result of file filtering operation
/// </summary>
public class FileFilterResult
{
    public List<string> AcceptedFiles { get; set; } = [];
    public List<string> RejectedFiles { get; set; } = [];
    public List<string> ErrorFiles { get; set; } = [];
    public int TotalProcessed { get; set; }
}

/// <summary>
/// Result of pattern testing
/// </summary>
public class FileFilterTestResult
{
    public FileTypeFilterConfiguration Configuration { get; set; } = new();
    public List<string> TestFiles { get; set; } = [];
    public List<string> AcceptedFiles { get; set; } = [];
    public List<string> RejectedFiles { get; set; } = [];
}

/// <summary>
/// File filtering statistics
/// </summary>
public class FileFilterStatistics
{
    public int IncludePatternCount { get; set; }
    public int ExcludePatternCount { get; set; }
    public int SupportedTypeCount { get; set; }
    public bool OnlySupportedTypes { get; set; }
    public bool HasSizeFilters { get; set; }
    public bool HasAgeFilters { get; set; }
    public DateTime LastUpdated { get; set; }
}