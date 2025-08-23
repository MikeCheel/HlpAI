using Microsoft.Extensions.Logging;
using HlpAI.Models;
using HlpAI.MCP;

namespace HlpAI.Services;

/// <summary>
/// Service for parsing and handling command-line arguments, especially logging configuration
/// </summary>
public class CommandLineArgumentsService
{
    private readonly ILogger? _logger;
    private readonly Dictionary<string, string> _arguments;
    private readonly List<string> _positionalArguments;

    public CommandLineArgumentsService(string[] args, ILogger? logger = null)
    {
        _logger = logger;
        _positionalArguments = new List<string>();
        _arguments = ParseArguments(args);
    }

    /// <summary>
    /// Parse command-line arguments into a dictionary
    /// </summary>
    private Dictionary<string, string> ParseArguments(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            
            if (arg.StartsWith("--"))
            {
                var key = arg[2..]; // Remove --
                
                // Check if this is a flag or has a value
                // Negative numbers like -5 should be treated as values, not flags
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--") && 
                    (!args[i + 1].StartsWith("-") || IsNumericValue(args[i + 1])))
                {
                    // Has a value
                    result[key] = args[i + 1];
                    i++; // Skip the value in next iteration
                }
                else
                {
                    // It's a flag (boolean)
                    result[key] = "true";
                }
            }
            else if (arg.StartsWith("-") && arg.Length > 1)
            {
                // Single character flags like -v, -h
                var key = arg[1..]; // Remove -
                
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    result[key] = args[i + 1];
                    i++;
                }
                else
                {
                    result[key] = "true";
                }
            }
            else
            {
                // Positional argument
                _positionalArguments.Add(arg);
            }
        }
        
        _logger?.LogDebug("Parsed {Count} command line arguments and {PositionalCount} positional arguments", result.Count, _positionalArguments.Count);
        return result;
    }

    /// <summary>
    /// Check if a specific argument is present
    /// </summary>
    public bool HasArgument(string key)
    {
        return _arguments.ContainsKey(key);
    }

    /// <summary>
    /// Get the value of an argument, or return default if not present
    /// </summary>
    public string? GetArgument(string key, string? defaultValue = null)
    {
        return _arguments.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Get an argument as a boolean value
    /// </summary>
    public bool GetBooleanArgument(string key, bool defaultValue = false)
    {
        if (!_arguments.TryGetValue(key, out var value))
            return defaultValue;
            
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Get an argument as an integer value
    /// </summary>
    public int GetIntegerArgument(string key, int defaultValue = 0)
    {
        if (!_arguments.TryGetValue(key, out var value))
            return defaultValue;
            
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Get an argument as a LogLevel enum
    /// </summary>
    public LogLevel GetLogLevelArgument(string key, LogLevel defaultValue = LogLevel.Warning)
    {
        if (!_arguments.TryGetValue(key, out var value))
            return defaultValue;
            
        return Enum.TryParse<LogLevel>(value, true, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Apply logging configuration from command line arguments
    /// </summary>
    public async Task<LoggingConfiguration> ApplyLoggingConfigurationAsync(ErrorLoggingService loggingService)
    {
        var config = new LoggingConfiguration();
        bool hasChanges = false;

        // Check for --enable-logging or --disable-logging
        if (HasArgument("enable-logging"))
        {
            var enableLogging = GetBooleanArgument("enable-logging", true);
            await loggingService.SetLoggingEnabledAsync(enableLogging);
            config.LoggingEnabled = enableLogging;
            hasChanges = true;
            _logger?.LogInformation("Logging enabled set to {Enabled} via command line", enableLogging);
        }
        else if (HasArgument("disable-logging"))
        {
            await loggingService.SetLoggingEnabledAsync(false);
            config.LoggingEnabled = false;
            hasChanges = true;
            _logger?.LogInformation("Logging disabled via command line");
        }

        // Check for --log-level
        if (HasArgument("log-level"))
        {
            var logLevel = GetLogLevelArgument("log-level", LogLevel.Warning);
            await loggingService.SetMinimumLogLevelAsync(logLevel);
            config.LogLevel = logLevel;
            hasChanges = true;
            _logger?.LogInformation("Log level set to {Level} via command line", logLevel);
        }

        // Check for --log-retention-days
        if (HasArgument("log-retention-days"))
        {
            var retentionDays = GetIntegerArgument("log-retention-days", 30);
            if (retentionDays > 0)
            {
                await loggingService.SetLogRetentionDaysAsync(retentionDays);
                config.RetentionDays = retentionDays;
                hasChanges = true;
                _logger?.LogInformation("Log retention set to {Days} days via command line", retentionDays);
            }
            else
            {
                _logger?.LogWarning("Invalid log retention days: {Days}. Must be greater than 0", retentionDays);
            }
        }

        // Check for --clear-logs
        if (HasArgument("clear-logs"))
        {
            var cleared = await loggingService.ClearAllLogsAsync();
            config.LogsCleared = cleared;
            hasChanges = true;
            if (cleared)
            {
                _logger?.LogInformation("All logs cleared via command line");
                Console.WriteLine("✅ All error logs have been cleared.");
            }
            else
            {
                _logger?.LogWarning("Failed to clear logs via command line");
                Console.WriteLine("❌ Failed to clear error logs.");
            }
        }

        // Check for --show-log-stats
        if (HasArgument("show-log-stats"))
        {
            var stats = await loggingService.GetLogStatisticsAsync();
            config.ShowStats = true;
            hasChanges = true;
            
            Console.WriteLine("\n📊 Error Log Statistics");
            Console.WriteLine("========================");
            Console.WriteLine($"Total logs: {stats.TotalLogs}");
            Console.WriteLine($"Errors (24h): {stats.ErrorsLast24Hours}");
            Console.WriteLine($"Warnings (24h): {stats.WarningsLast24Hours}");
            Console.WriteLine($"Errors (7d): {stats.ErrorsLast7Days}");
            Console.WriteLine($"Warnings (7d): {stats.WarningsLast7Days}");
            
            if (stats.OldestLogDate.HasValue)
                Console.WriteLine($"Oldest log: {stats.OldestLogDate:yyyy-MM-dd HH:mm:ss}");
            if (stats.NewestLogDate.HasValue)
                Console.WriteLine($"Newest log: {stats.NewestLogDate:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
        }

        // Check for --show-recent-logs
        if (HasArgument("show-recent-logs"))
        {
            var count = GetIntegerArgument("show-recent-logs", 10);
            var logs = await loggingService.GetRecentLogsAsync(count);
            config.ShowRecentLogs = count;
            hasChanges = true;
            
            Console.WriteLine($"\n📋 Recent Error Logs (Last {logs.Count})");
            Console.WriteLine("==========================================");
            
            if (logs.Count == 0)
            {
                Console.WriteLine("No error logs found.");
            }
            else
            {
                foreach (var log in logs)
                {
                    Console.WriteLine($"[{log.Timestamp:yyyy-MM-dd HH:mm:ss}] {log.LogLevel}: {log.Message}");
                    if (!string.IsNullOrEmpty(log.Context))
                        Console.WriteLine($"  Context: {log.Context}");
                    if (!string.IsNullOrEmpty(log.ExceptionType))
                        Console.WriteLine($"  Exception: {log.ExceptionType}");
                    Console.WriteLine();
                }
            }
        }

        config.HasChanges = hasChanges;
        return config;
    }

    /// <summary>
    /// Check if help should be shown
    /// </summary>
    public bool ShouldShowHelp()
    {
        return HasArgument("help") || HasArgument("h") || HasArgument("?");
    }

    /// <summary>
    /// Check if this is a logging-only command (no directory processing needed)
    /// </summary>
    public bool IsLoggingOnlyCommand()
    {
        return HasArgument("show-log-stats") || 
               HasArgument("show-recent-logs") || 
               HasArgument("clear-logs") ||
               (HasArgument("enable-logging") && !HasPositionalArguments()) ||
               (HasArgument("disable-logging") && !HasPositionalArguments()) ||
               (HasArgument("log-level") && !HasPositionalArguments()) ||
               (HasArgument("log-retention-days") && !HasPositionalArguments());
    }

    /// <summary>
    /// Check if this is a file export command
    /// </summary>
    public bool IsFileExportCommand()
    {
        return HasArgument("export-files") || 
               HasArgument("list-files-export");
    }

    /// <summary>
    /// Check if command contains only extractor management commands
    /// </summary>
    public bool IsExtractorManagementCommand()
    {
        return HasArgument("list-extractors") ||
               HasArgument("extractor-stats") ||
               HasArgument("add-file-type") ||
               HasArgument("remove-file-type") ||
               HasArgument("test-extraction") ||
               HasArgument("reset-extractor");
    }

    /// <summary>
    /// Check if this is a cleanup command
    /// </summary>
    public bool IsCleanupCommand()
    {
        return HasArgument("cleanup") || 
               HasArgument("cleanup-all") ||
               HasArgument("cleanup-stats") ||
               HasArgument("cleanup-history");
    }

    /// <summary>
    /// Check if this is a file filtering command
    /// </summary>
    public bool IsFileFilteringCommand()
    {
        return HasArgument("include-files") ||
               HasArgument("exclude-files") ||
               HasArgument("filter-stats") ||
               HasArgument("test-filters") ||
               HasArgument("reset-filters");
    }

    /// <summary>
    /// Get file export format from arguments
    /// </summary>
    public FileExportFormat GetFileExportFormat(FileExportFormat defaultFormat = FileExportFormat.Csv)
    {
        var formatArg = GetArgument("export-format", defaultFormat.ToString());
        return Enum.TryParse<FileExportFormat>(formatArg, true, out var format) ? format : defaultFormat;
    }

    /// <summary>
    /// Apply file export configuration from command line arguments
    /// </summary>
    public async Task<FileExportConfiguration> ApplyFileExportConfigurationAsync(FileListExportService exportService, List<ResourceInfo> resources)
    {
        var config = new FileExportConfiguration();

        // Check for --export-files
        if (HasArgument("export-files"))
        {
            var outputPath = GetArgument("export-files");
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = $"file_list_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            }

            var format = GetFileExportFormat();
            var includeMetadata = GetBooleanArgument("export-metadata", true);

            var result = await exportService.ExportFileListAsync(resources, format, outputPath, includeMetadata);
            config.ExportResult = result;
            config.HasExport = true;

            if (result.Success)
            {
                Console.WriteLine($"✅ Exported {result.ExportedCount} files to {result.Format} format: {result.OutputPath}");
                Console.WriteLine($"   File size: {result.FileSizeBytes:N0} bytes");
                _logger?.LogInformation("File list exported via command line to {Path} in {Format} format", 
                    result.OutputPath, result.Format);
            }
            else
            {
                Console.WriteLine($"❌ Failed to export file list: {result.ErrorMessage}");
                _logger?.LogError("File list export failed via command line: {Error}", result.ErrorMessage);
            }
        }

        // Check for --list-files-export (export and display)
        if (HasArgument("list-files-export"))
        {
            var format = GetFileExportFormat();
            var outputPath = GetArgument("list-files-export") ?? $"file_list_{DateTime.Now:yyyyMMdd_HHmmss}.{format.ToString().ToLower()}";
            var includeMetadata = GetBooleanArgument("export-metadata", true);

            // Export first
            var result = await exportService.ExportFileListAsync(resources, format, outputPath, includeMetadata);
            config.ExportResult = result;
            config.HasExport = true;
            config.ShouldDisplay = true;

            if (result.Success)
            {
                Console.WriteLine($"\n📂 File List ({resources.Count} files)");
                Console.WriteLine("=".PadRight(50, '='));
                
                // Display summary
                foreach (var resource in resources.OrderBy(r => r.Name))
                {
                    Console.WriteLine($"• {resource.Name}");
                    if (includeMetadata && !string.IsNullOrEmpty(resource.Description))
                    {
                        Console.WriteLine($"  {resource.Description}");
                    }
                }

                Console.WriteLine($"\n✅ Also exported to {result.Format} format: {result.OutputPath}");
                Console.WriteLine($"   File size: {result.FileSizeBytes:N0} bytes");
            }
            else
            {
                Console.WriteLine($"❌ Failed to export file list: {result.ErrorMessage}");
            }
        }

        return config;
    }

    /// <summary>
    /// Apply cleanup configuration from command line arguments
    /// </summary>
    public async Task<CleanupConfiguration> ApplyCleanupConfigurationAsync(CleanupService cleanupService)
    {
        var config = new CleanupConfiguration();

        // Check for --cleanup-stats
        if (HasArgument("cleanup-stats"))
        {
            var stats = await cleanupService.GetCleanupStatisticsAsync();
            config.ShowStats = true;
            config.Statistics = stats;

            Console.WriteLine("\n🧹 System Cleanup Statistics");
            Console.WriteLine("============================");
            Console.WriteLine($"Vector Database Size: {stats.VectorDatabaseSize:N0} bytes");
            Console.WriteLine($"Configuration Database Size: {stats.ConfigurationDatabaseSize:N0} bytes");
            Console.WriteLine($"Error Log Count: {stats.ErrorLogCount:N0}");
            Console.WriteLine($"Export Log Count: {stats.ExportLogCount:N0}");
            Console.WriteLine($"Temp File Count: {stats.TempFileCount:N0} ({stats.TempFileSize:N0} bytes)");
            Console.WriteLine($"Cache Entry Count: {stats.CacheEntryCount:N0}");
            
            if (stats.OldestErrorLog.HasValue && stats.NewestErrorLog.HasValue)
            {
                Console.WriteLine($"Error Log Range: {stats.OldestErrorLog:yyyy-MM-dd} to {stats.NewestErrorLog:yyyy-MM-dd}");
            }
            
            Console.WriteLine($"Last Calculated: {stats.LastCalculated:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
        }

        // Check for --cleanup-history
        if (HasArgument("cleanup-history"))
        {
            var count = GetIntegerArgument("cleanup-history", 10);
            var history = await cleanupService.GetCleanupHistoryAsync(count);
            config.ShowHistory = true;
            config.HistoryCount = count;

            Console.WriteLine($"\n📋 Cleanup History (Last {history.Count})");
            Console.WriteLine("================================");
            
            if (history.Count == 0)
            {
                Console.WriteLine("No cleanup operations found.");
            }
            else
            {
                foreach (var result in history)
                {
                    var status = result.Success ? "✅" : "❌";
                    Console.WriteLine($"{status} [{result.StartedAt:yyyy-MM-dd HH:mm:ss}] Duration: {result.Duration.TotalSeconds:F1}s");
                    
                    if (result.Success)
                    {
                        if (result.VectorDatabaseCleaned) Console.WriteLine($"  • Vector DB: {result.VectorDatabaseSize:N0} bytes freed");
                        if (result.ErrorLogsCleaned) Console.WriteLine($"  • Error Logs: {result.ErrorLogsRemoved} removed");
                        if (result.ExportLogsCleaned) Console.WriteLine($"  • Export Logs: {result.ExportLogsRemoved} removed");
                        if (result.TempFilesCleaned) Console.WriteLine($"  • Temp Files: {result.TempFilesRemoved} files, {result.TempFilesSize:N0} bytes");
                        if (result.CacheCleaned) Console.WriteLine($"  • Cache: {result.CacheEntriesRemoved} entries");
                        if (result.DatabaseOptimized) Console.WriteLine($"  • Database optimized");
                    }
                    else
                    {
                        Console.WriteLine($"  Error: {result.ErrorMessage}");
                    }
                    Console.WriteLine();
                }
            }
        }

        // Check for --cleanup or --cleanup-all
        if (HasArgument("cleanup") || HasArgument("cleanup-all"))
        {
            var options = new CleanupOptions();
            
            if (HasArgument("cleanup-all"))
            {
                // Full cleanup
                options.CleanVectorDatabase = true;
                options.CleanErrorLogs = true;
                options.CleanExportLogs = true;
                options.CleanTempFiles = true;
                options.CleanOutdatedCache = true;
                options.OptimizeDatabase = true;
            }
            else
            {
                // Selective cleanup based on flags
                options.CleanVectorDatabase = GetBooleanArgument("cleanup-vector", false);
                options.CleanErrorLogs = GetBooleanArgument("cleanup-errors", true);
                options.CleanExportLogs = GetBooleanArgument("cleanup-exports", true);
                options.CleanTempFiles = GetBooleanArgument("cleanup-temp", true);
                options.CleanOutdatedCache = GetBooleanArgument("cleanup-cache", true);
                options.OptimizeDatabase = GetBooleanArgument("cleanup-optimize", true);
            }

            // Override retention periods if specified
            options.ErrorLogRetentionDays = GetIntegerArgument("error-retention-days", 30);
            options.ExportLogRetentionDays = GetIntegerArgument("export-retention-days", 90);
            options.TempFileAgeHours = GetIntegerArgument("temp-file-age-hours", 24);
            options.CacheRetentionDays = GetIntegerArgument("cache-retention-days", 7);

            Console.WriteLine("\n🧹 Starting System Cleanup");
            Console.WriteLine("===========================");
            Console.WriteLine($"Vector Database: {(options.CleanVectorDatabase ? "✅" : "⏭️")}");
            Console.WriteLine($"Error Logs (>{options.ErrorLogRetentionDays}d): {(options.CleanErrorLogs ? "✅" : "⏭️")}");
            Console.WriteLine($"Export Logs (>{options.ExportLogRetentionDays}d): {(options.CleanExportLogs ? "✅" : "⏭️")}");
            Console.WriteLine($"Temp Files (>{options.TempFileAgeHours}h): {(options.CleanTempFiles ? "✅" : "⏭️")}");
            Console.WriteLine($"Outdated Cache (>{options.CacheRetentionDays}d): {(options.CleanOutdatedCache ? "✅" : "⏭️")}");
            Console.WriteLine($"Database Optimization: {(options.OptimizeDatabase ? "✅" : "⏭️")}");
            Console.WriteLine();

            var result = await cleanupService.PerformCleanupAsync(options);
            config.HasCleanup = true;
            config.CleanupResult = result;

            if (result.Success)
            {
                Console.WriteLine("✅ Cleanup completed successfully!");
                Console.WriteLine($"⏱️  Duration: {result.Duration.TotalSeconds:F1} seconds");
                Console.WriteLine();
                
                foreach (var detail in result.Details)
                {
                    Console.WriteLine($"• {detail.Key}: {detail.Value}");
                }
                
                var totalSpace = result.VectorDatabaseSize + result.TempFilesSize;
                if (totalSpace > 0)
                {
                    Console.WriteLine($"\n💾 Total space freed: {totalSpace:N0} bytes ({totalSpace / 1024.0 / 1024.0:F2} MB)");
                }
            }
            else
            {
                Console.WriteLine($"❌ Cleanup failed: {result.ErrorMessage}");
            }
        }

        return config;
    }

    /// <summary>
    /// Apply file filtering configuration from command line arguments
    /// </summary>
    public async Task<FileFilteringConfiguration> ApplyFileFilteringConfigurationAsync(FileTypeFilterService filterService)
    {
        var config = new FileFilteringConfiguration();

        // Check for --filter-stats
        if (HasArgument("filter-stats"))
        {
            var stats = await filterService.GetFilterStatisticsAsync();
            config.ShowStats = true;
            config.Statistics = stats;

            Console.WriteLine("\n🗂️  File Type Filtering Statistics");
            Console.WriteLine("================================");
            Console.WriteLine($"Include Patterns: {stats.IncludePatternCount}");
            Console.WriteLine($"Exclude Patterns: {stats.ExcludePatternCount}");
            Console.WriteLine($"Supported File Types: {stats.SupportedTypeCount}");
            Console.WriteLine($"Only Supported Types: {(stats.OnlySupportedTypes ? "✅ Yes" : "❌ No")}");
            Console.WriteLine($"Size Filters Active: {(stats.HasSizeFilters ? "✅ Yes" : "❌ No")}");
            Console.WriteLine($"Age Filters Active: {(stats.HasAgeFilters ? "✅ Yes" : "❌ No")}");
            Console.WriteLine($"Last Updated: {stats.LastUpdated:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();

            // Show current patterns
            var currentConfig = await filterService.GetFilterConfigurationAsync();
            if (currentConfig.IncludePatterns?.Any() == true)
            {
                Console.WriteLine("📥 Include Patterns:");
                foreach (var pattern in currentConfig.IncludePatterns)
                {
                    Console.WriteLine($"  • {pattern}");
                }
                Console.WriteLine();
            }

            if (currentConfig.ExcludePatterns?.Any() == true)
            {
                Console.WriteLine("🚫 Exclude Patterns:");
                foreach (var pattern in currentConfig.ExcludePatterns)
                {
                    Console.WriteLine($"  • {pattern}");
                }
                Console.WriteLine();
            }

            if (currentConfig.SupportedTypes?.Any() == true)
            {
                Console.WriteLine("🔧 Supported File Types:");
                Console.WriteLine($"  {string.Join(", ", currentConfig.SupportedTypes)}");
                Console.WriteLine();
            }
        }

        // Check for --include-files
        if (HasArgument("include-files"))
        {
            var patterns = GetArgument("include-files")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
            config.IncludePatternsAdded = patterns.Length;
            
            foreach (var pattern in patterns.Select(p => p.Trim()))
            {
                var success = await filterService.AddIncludePatternAsync(pattern);
                if (success)
                {
                    Console.WriteLine($"✅ Added include pattern: {pattern}");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to add include pattern: {pattern}");
                }
            }
        }

        // Check for --exclude-files
        if (HasArgument("exclude-files"))
        {
            var patterns = GetArgument("exclude-files")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
            config.ExcludePatternsAdded = patterns.Length;
            
            foreach (var pattern in patterns.Select(p => p.Trim()))
            {
                var success = await filterService.AddExcludePatternAsync(pattern);
                if (success)
                {
                    Console.WriteLine($"✅ Added exclude pattern: {pattern}");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to add exclude pattern: {pattern}");
                }
            }
        }

        // Check for --test-filters
        if (HasArgument("test-filters"))
        {
            var testFilesArg = GetArgument("test-filters");
            List<string> testFiles;

            if (!string.IsNullOrEmpty(testFilesArg))
            {
                // Use provided test files
                testFiles = testFilesArg.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim()).ToList();
            }
            else
            {
                // Use sample test files
                testFiles = new List<string>
                {
                    "document.txt", "README.md", "style.css", "script.js", 
                    "data.csv", "report.pdf", "temp.tmp", "backup~",
                    "image.png", "video.mp4", "archive.zip", "config.xml",
                    "help.chm", "contents.hhc", "page.html", "log.log"
                };
            }

            var result = await filterService.TestPatternsAsync(testFiles);
            config.TestResult = result;
            config.HasTest = true;

            Console.WriteLine("\n🧪 File Filter Test Results");
            Console.WriteLine("============================");
            Console.WriteLine($"Total Test Files: {result.TestFiles.Count}");
            Console.WriteLine($"Accepted: {result.AcceptedFiles.Count}");
            Console.WriteLine($"Rejected: {result.RejectedFiles.Count}");
            Console.WriteLine();

            if (result.AcceptedFiles.Any())
            {
                Console.WriteLine("✅ Accepted Files:");
                foreach (var file in result.AcceptedFiles)
                {
                    Console.WriteLine($"  • {file}");
                }
                Console.WriteLine();
            }

            if (result.RejectedFiles.Any())
            {
                Console.WriteLine("❌ Rejected Files:");
                foreach (var file in result.RejectedFiles)
                {
                    Console.WriteLine($"  • {file}");
                }
                Console.WriteLine();
            }
        }

        // Check for --reset-filters
        if (HasArgument("reset-filters"))
        {
            var success = await filterService.ResetToDefaultsAsync();
            config.FiltersReset = success;

            if (success)
            {
                Console.WriteLine("✅ File filtering configuration reset to defaults");
            }
            else
            {
                Console.WriteLine("❌ Failed to reset file filtering configuration");
            }
        }

        return config;
    }

    /// <summary>
    /// Apply extractor management configuration from command line arguments
    /// </summary>
    public async Task<ExtractorManagementConfiguration> ApplyExtractorManagementConfigurationAsync()
    {
        var config = new ExtractorManagementConfiguration();
        var extractorService = new ExtractorManagementService(_logger);

        // Check for --list-extractors
        if (HasArgument("list-extractors"))
        {
            var extractors = await extractorService.GetExtractorsAsync();
            config.ShowExtractors = true;
            
            Console.WriteLine("\n🔧 Available File Extractors");
            Console.WriteLine("============================");
            
            foreach (var (key, extractor) in extractors)
            {
                Console.WriteLine($"\n📦 {extractor.Name} ({key})");
                Console.WriteLine($"   Type: {extractor.Type}");
                Console.WriteLine($"   MIME: {extractor.MimeType}");
                Console.WriteLine($"   Description: {extractor.Description}");
                Console.WriteLine($"   Extensions: {string.Join(", ", extractor.CustomExtensions)}");
                
                var customCount = extractor.CustomExtensions.Count - extractor.DefaultExtensions.Count;
                if (customCount > 0)
                {
                    Console.WriteLine($"   Custom extensions added: {customCount}");
                }
            }
            Console.WriteLine();
        }

        // Check for --extractor-stats
        if (HasArgument("extractor-stats"))
        {
            var stats = await extractorService.GetExtractionStatisticsAsync();
            config.ShowStats = true;
            config.Statistics = stats;
            
            Console.WriteLine("\n📊 Extractor Statistics");
            Console.WriteLine("=======================");
            Console.WriteLine($"Total extractors: {stats.TotalExtractors}");
            Console.WriteLine($"Total supported extensions: {stats.TotalSupportedExtensions}");
            
            foreach (var (key, extractorStats) in stats.ExtractorStats)
            {
                Console.WriteLine($"\n• {extractorStats.Name}:");
                Console.WriteLine($"  Supported extensions: {extractorStats.SupportedExtensionCount}");
                Console.WriteLine($"  Default extensions: {extractorStats.DefaultExtensionCount}");
                Console.WriteLine($"  Custom extensions: {extractorStats.CustomExtensionCount}");
            }
            Console.WriteLine();
        }

        // Check for --add-file-type
        if (HasArgument("add-file-type"))
        {
            var value = GetArgument("add-file-type");
            if (!string.IsNullOrEmpty(value))
            {
                var parts = value.Split(':', 2);
                if (parts.Length == 2)
                {
                    var extractorKey = parts[0].Trim();
                    var extensions = parts[1].Split(',').Select(e => e.Trim()).ToArray();
                    
                    foreach (var extension in extensions)
                    {
                        var success = await extractorService.AddFileExtensionAsync(extractorKey, extension);
                        if (success)
                        {
                            config.ExtensionsAdded++;
                            Console.WriteLine($"✅ Added extension {extension} to {extractorKey} extractor");
                        }
                        else
                        {
                            Console.WriteLine($"❌ Failed to add extension {extension} to {extractorKey} extractor");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("❌ Invalid format for --add-file-type. Use: extractorKey:extension1,extension2");
                }
            }
        }

        // Check for --remove-file-type
        if (HasArgument("remove-file-type"))
        {
            var value = GetArgument("remove-file-type");
            if (!string.IsNullOrEmpty(value))
            {
                var parts = value.Split(':', 2);
                if (parts.Length == 2)
                {
                    var extractorKey = parts[0].Trim();
                    var extensions = parts[1].Split(',').Select(e => e.Trim()).ToArray();
                    
                    foreach (var extension in extensions)
                    {
                        var success = await extractorService.RemoveFileExtensionAsync(extractorKey, extension);
                        if (success)
                        {
                            config.ExtensionsRemoved++;
                            Console.WriteLine($"✅ Removed extension {extension} from {extractorKey} extractor");
                        }
                        else
                        {
                            Console.WriteLine($"❌ Extension {extension} was not found in {extractorKey} extractor");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("❌ Invalid format for --remove-file-type. Use: extractorKey:extension1,extension2");
                }
            }
        }

        // Check for --test-extraction
        if (HasArgument("test-extraction"))
        {
            var filePath = GetArgument("test-extraction");
            if (!string.IsNullOrEmpty(filePath))
            {
                var result = await extractorService.TestFileExtractionAsync(filePath);
                config.HasTest = true;
                config.TestResult = result;
                
                Console.WriteLine($"\n🧪 Extraction Test: {Path.GetFileName(filePath)}");
                Console.WriteLine("====================================");
                
                if (result.Success)
                {
                    Console.WriteLine($"✅ Success");
                    Console.WriteLine($"   Extractor: {result.ExtractorUsed}");
                    Console.WriteLine($"   Content length: {result.ContentLength:N0} characters");
                    Console.WriteLine($"   Extraction time: {result.ExtractionTimeMs}ms");
                    Console.WriteLine($"   File size: {result.FileSizeBytes:N0} bytes");
                    
                    if (!string.IsNullOrEmpty(result.ContentPreview))
                    {
                        Console.WriteLine($"   Preview: {result.ContentPreview.Replace('\n', ' ').Replace('\r', ' ')}");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ Failed: {result.ErrorMessage}");
                    if (!string.IsNullOrEmpty(result.ExtractorUsed))
                    {
                        Console.WriteLine($"   Attempted extractor: {result.ExtractorUsed}");
                    }
                }
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("❌ File path required for --test-extraction");
            }
        }

        // Check for --reset-extractor
        if (HasArgument("reset-extractor"))
        {
            var extractorKey = GetArgument("reset-extractor");
            if (!string.IsNullOrEmpty(extractorKey))
            {
                var success = await extractorService.ResetExtractorToDefaultAsync(extractorKey);
                if (success)
                {
                    config.ExtractorsReset++;
                    Console.WriteLine($"✅ Reset {extractorKey} extractor to default configuration");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to reset {extractorKey} extractor (unknown extractor)");
                }
            }
            else
            {
                Console.WriteLine("❌ Extractor key required for --reset-extractor");
            }
        }

        return config;
    }

    /// <summary>
    /// Check if there are any positional arguments (non-flag arguments)
    /// </summary>
    public bool HasPositionalArguments()
    {
        return _positionalArguments.Count > 0;
    }

    /// <summary>
    /// Get all positional arguments
    /// </summary>
    public List<string> GetPositionalArguments()
    {
        return new List<string>(_positionalArguments);
    }

    /// <summary>
    /// Get all parsed arguments (for debugging)
    /// </summary>
    public Dictionary<string, string> GetAllArguments()
    {
        return new Dictionary<string, string>(_arguments);
    }

    /// <summary>
    /// Checks if a string represents a numeric value (including negative numbers)
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <returns>True if the string is a valid numeric value</returns>
    private static bool IsNumericValue(string value)
    {
        return double.TryParse(value, out _);
    }
}

/// <summary>
/// Configuration applied from command line arguments
/// </summary>
public class LoggingConfiguration
{
    public bool? LoggingEnabled { get; set; }
    public LogLevel? LogLevel { get; set; }
    public int? RetentionDays { get; set; }
    public bool LogsCleared { get; set; }
    public bool ShowStats { get; set; }
    public int? ShowRecentLogs { get; set; }
    public bool HasChanges { get; set; }
}

/// <summary>
/// File export configuration from command line arguments
/// </summary>
public class FileExportConfiguration
{
    public bool HasExport { get; set; }
    public bool ShouldDisplay { get; set; }
    public FileExportResult? ExportResult { get; set; }
}

/// <summary>
/// Cleanup configuration from command line arguments
/// </summary>
public class CleanupConfiguration
{
    public bool ShowStats { get; set; }
    public bool ShowHistory { get; set; }
    public bool HasCleanup { get; set; }
    public int HistoryCount { get; set; }
    public CleanupStatistics? Statistics { get; set; }
    public CleanupResult? CleanupResult { get; set; }
}

/// <summary>
/// File filtering configuration from command line arguments
/// </summary>
public class FileFilteringConfiguration
{
    public bool ShowStats { get; set; }
    public bool HasTest { get; set; }
    public bool FiltersReset { get; set; }
    public int IncludePatternsAdded { get; set; }
    public int ExcludePatternsAdded { get; set; }
    public FileFilterStatistics? Statistics { get; set; }
    public FileFilterTestResult? TestResult { get; set; }
}

/// <summary>
/// Extractor management configuration from command line arguments
/// </summary>
public class ExtractorManagementConfiguration
{
    public bool ShowExtractors { get; set; }
    public bool ShowStats { get; set; }
    public bool HasTest { get; set; }
    public int ExtensionsAdded { get; set; }
    public int ExtensionsRemoved { get; set; }
    public int ExtractorsReset { get; set; }
    public ExtractionStatistics? Statistics { get; set; }
    public ExtractionTestResult? TestResult { get; set; }
}