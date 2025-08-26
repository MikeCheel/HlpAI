using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HlpAI.Services;

/// <summary>
/// Service for managing cleanup operations across the HlpAI system
/// </summary>
public class CleanupService : IDisposable
{
    private readonly ILogger? _logger;
    private readonly SqliteConfigurationService _configService;
    private bool _disposed = false;

    public CleanupService(ILogger? logger = null, SqliteConfigurationService? configService = null)
    {
        _logger = logger;
        _configService = configService ?? new SqliteConfigurationService(logger);
    }

    /// <summary>
    /// Get cleanup options with configured retention periods
    /// </summary>
    public async Task<CleanupOptions> GetConfiguredCleanupOptionsAsync()
    {
        var options = new CleanupOptions();
        
        // Load configured retention periods, falling back to defaults if not set
        var errorRetention = await _configService.GetConfigurationAsync("error_log_retention_days", "cleanup");
        if (int.TryParse(errorRetention, out var errorDays))
            options.ErrorLogRetentionDays = errorDays;
            
        var exportRetention = await _configService.GetConfigurationAsync("export_log_retention_days", "cleanup");
        if (int.TryParse(exportRetention, out var exportDays))
            options.ExportLogRetentionDays = exportDays;
            
        var tempFileAge = await _configService.GetConfigurationAsync("temp_file_age_hours", "cleanup");
        if (int.TryParse(tempFileAge, out var tempHours))
            options.TempFileAgeHours = tempHours;
            
        var cacheRetention = await _configService.GetConfigurationAsync("cache_retention_days", "cleanup");
        if (int.TryParse(cacheRetention, out var cacheDays))
            options.CacheRetentionDays = cacheDays;
            
        return options;
    }

    /// <summary>
    /// Perform comprehensive system cleanup
    /// </summary>
    public async Task<CleanupResult> PerformCleanupAsync(CleanupOptions options)
    {
        var result = new CleanupResult
        {
            StartedAt = DateTime.UtcNow,
            Options = options
        };

        try
        {
            _logger?.LogInformation("Starting cleanup operation with options: {Options}", 
                JsonSerializer.Serialize(options));

            if (options.CleanVectorDatabase)
            {
                var vectorResult = await CleanVectorDatabaseAsync(options.VectorDatabasePath);
                result.VectorDatabaseCleaned = vectorResult.Success;
                result.VectorDatabaseSize = vectorResult.SpaceFreed;
                result.Details.Add("Vector Database", vectorResult.Message);
            }

            if (options.CleanErrorLogs)
            {
                var errorResult = await CleanErrorLogsAsync(options.ErrorLogRetentionDays);
                result.ErrorLogsCleaned = errorResult.Success;
                result.ErrorLogsRemoved = errorResult.ItemsProcessed;
                result.Details.Add("Error Logs", errorResult.Message);
            }

            if (options.CleanExportLogs)
            {
                var exportResult = await CleanExportLogsAsync(options.ExportLogRetentionDays);
                result.ExportLogsCleaned = exportResult.Success;
                result.ExportLogsRemoved = exportResult.ItemsProcessed;
                result.Details.Add("Export Logs", exportResult.Message);
            }

            if (options.CleanTempFiles)
            {
                var tempResult = await CleanTempFilesAsync(options.TempFileAgeHours);
                result.TempFilesCleaned = tempResult.Success;
                result.TempFilesRemoved = tempResult.ItemsProcessed;
                result.TempFilesSize = tempResult.SpaceFreed;
                result.Details.Add("Temporary Files", tempResult.Message);
            }

            if (options.CleanOutdatedCache)
            {
                var cacheResult = await CleanOutdatedCacheAsync(options.CacheRetentionDays);
                result.CacheCleaned = cacheResult.Success;
                result.CacheEntriesRemoved = cacheResult.ItemsProcessed;
                result.Details.Add("Cache", cacheResult.Message);
            }

            if (options.OptimizeDatabase)
            {
                var optimizeResult = await OptimizeDatabaseAsync();
                result.DatabaseOptimized = optimizeResult.Success;
                result.Details.Add("Database Optimization", optimizeResult.Message);
            }

            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt - result.StartedAt;

            _logger?.LogInformation("Cleanup operation completed successfully in {Duration}ms", 
                result.Duration.TotalMilliseconds);

            await LogCleanupOperationAsync(result);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt - result.StartedAt;

            _logger?.LogError(ex, "Cleanup operation failed after {Duration}ms", 
                result.Duration.TotalMilliseconds);
        }

        return result;
    }

    /// <summary>
    /// Clean vector database files
    /// </summary>
    private Task<CleanupOperationResult> CleanVectorDatabaseAsync(string? databasePath = null)
    {
        try
        {
            var dbPath = databasePath ?? "vectors.db";
            var configDbPath = databasePath ?? Path.Combine(Environment.CurrentDirectory, "vectors.db");

            long spaceBefore = 0;
            if (File.Exists(configDbPath))
            {
                spaceBefore = new FileInfo(configDbPath).Length;
            }

            // Check if database exists and get info
            if (!File.Exists(configDbPath))
            {
                return Task.FromResult(new CleanupOperationResult
                {
                    Success = true,
                    Message = "Vector database not found - nothing to clean",
                    SpaceFreed = 0,
                    ItemsProcessed = 0
                });
            }

            // Option to backup before deletion
            var backupPath = $"{configDbPath}.backup_{DateTime.Now:yyyyMMdd_HHmmss}";
            File.Copy(configDbPath, backupPath);

            // Delete the vector database
            File.Delete(configDbPath);

            return Task.FromResult(new CleanupOperationResult
            {
                Success = true,
                Message = $"Vector database deleted (backup created: {Path.GetFileName(backupPath)})",
                SpaceFreed = spaceBefore,
                ItemsProcessed = 1
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to clean vector database");
            return Task.FromResult(new CleanupOperationResult
            {
                Success = false,
                Message = $"Failed to clean vector database: {ex.Message}",
                SpaceFreed = 0,
                ItemsProcessed = 0
            });
        }
    }

    /// <summary>
    /// Clean old error logs
    /// </summary>
    private async Task<CleanupOperationResult> CleanErrorLogsAsync(int retentionDays = 30)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            
            using var errorService = new ErrorLoggingService(_logger);
            var allLogs = await errorService.GetRecentLogsAsync(10000); // Get all logs
            
            var logsToDelete = allLogs.Where(log => log.Timestamp < cutoffDate).ToList();
            
            if (logsToDelete.Count == 0)
            {
                return new CleanupOperationResult
                {
                    Success = true,
                    Message = $"No error logs older than {retentionDays} days found",
                    ItemsProcessed = 0
                };
            }

            // Clear old logs by recreating the error_logs category
            var errorLogsConfig = await _configService.GetCategoryConfigurationAsync("error_logs");
            var recentLogs = errorLogsConfig.Where(kvp => 
            {
                try
                {
                    var logData = JsonSerializer.Deserialize<Dictionary<string, object>>(kvp.Value ?? string.Empty);
                    if (logData != null && logData.ContainsKey("timestamp"))
                    {
                        var timestamp = DateTime.Parse(logData["timestamp"].ToString() ?? DateTime.MinValue.ToString());
                        return timestamp >= cutoffDate;
                    }
                }
                catch
                {
                    // Keep entries we can't parse
                    return true;
                }
                return false;
            }).ToList();

            // Clear the category and restore recent logs
            await _configService.ClearCategoryAsync("error_logs");
            
            foreach (var recentLog in recentLogs)
            {
                await _configService.SetConfigurationAsync(recentLog.Key, recentLog.Value, "error_logs");
            }

            return new CleanupOperationResult
            {
                Success = true,
                Message = $"Removed {logsToDelete.Count} error logs older than {retentionDays} days",
                ItemsProcessed = logsToDelete.Count
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to clean error logs");
            return new CleanupOperationResult
            {
                Success = false,
                Message = $"Failed to clean error logs: {ex.Message}",
                ItemsProcessed = 0
            };
        }
    }

    /// <summary>
    /// Clean old export logs
    /// </summary>
    private async Task<CleanupOperationResult> CleanExportLogsAsync(int retentionDays = 90)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            
            var exportLogs = await _configService.GetCategoryConfigurationAsync("export_logs");
            var logsToDelete = new List<string>();

            foreach (var log in exportLogs)
            {
                try
                {
                    var logData = JsonSerializer.Deserialize<Dictionary<string, object>>(log.Value ?? string.Empty);
                    if (logData != null && logData.ContainsKey("exportedAt"))
                    {
                        var exportedAt = DateTime.Parse(logData["exportedAt"].ToString() ?? DateTime.MinValue.ToString());
                        if (exportedAt < cutoffDate)
                        {
                            logsToDelete.Add(log.Key);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse export log entry: {Key}", log.Key);
                }
            }

            // Delete old export logs
            foreach (var key in logsToDelete)
            {
                await _configService.SetConfigurationAsync(key, null, "export_logs");
            }

            return new CleanupOperationResult
            {
                Success = true,
                Message = $"Removed {logsToDelete.Count} export logs older than {retentionDays} days",
                ItemsProcessed = logsToDelete.Count
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to clean export logs");
            return new CleanupOperationResult
            {
                Success = false,
                Message = $"Failed to clean export logs: {ex.Message}",
                ItemsProcessed = 0
            };
        }
    }

    /// <summary>
    /// Clean temporary files
    /// </summary>
    private Task<CleanupOperationResult> CleanTempFilesAsync(int ageHours = 24)
    {
        try
        {
            var tempPaths = new[]
            {
                Path.GetTempPath(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hlpai", "temp"),
                Environment.CurrentDirectory
            };

            var cutoffTime = DateTime.UtcNow.AddHours(-ageHours);
            var totalFiles = 0;
            long totalSize = 0;

            var tempFilePatterns = new[]
            {
                "*.tmp", "*.temp", "*~", ".DS_Store", "Thumbs.db", 
                "hlpai_temp_*", "export_temp_*", "*.bak"
            };

            foreach (var tempPath in tempPaths.Where(Directory.Exists))
            {
                foreach (var pattern in tempFilePatterns)
                {
                    try
                    {
                        var files = Directory.GetFiles(tempPath, pattern, SearchOption.TopDirectoryOnly)
                            .Where(file => File.GetLastWriteTime(file) < cutoffTime);

                        foreach (var file in files)
                        {
                            try
                            {
                                var fileInfo = new FileInfo(file);
                                totalSize += fileInfo.Length;
                                File.Delete(file);
                                totalFiles++;
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, "Failed to delete temp file: {File}", file);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to search for temp files with pattern {Pattern} in {Path}", 
                            pattern, tempPath);
                    }
                }
            }

            return Task.FromResult(new CleanupOperationResult
            {
                Success = true,
                Message = $"Removed {totalFiles} temporary files, freed {totalSize:N0} bytes",
                ItemsProcessed = totalFiles,
                SpaceFreed = totalSize
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to clean temporary files");
            return Task.FromResult(new CleanupOperationResult
            {
                Success = false,
                Message = $"Failed to clean temporary files: {ex.Message}",
                ItemsProcessed = 0
            });
        }
    }

    /// <summary>
    /// Clean outdated cache entries
    /// </summary>
    private async Task<CleanupOperationResult> CleanOutdatedCacheAsync(int retentionDays = 7)
    {
        try
        {
            // Clean various cache categories
            var cacheCategories = new[] { "embedding_cache", "model_cache", "extraction_cache" };
            var totalItems = 0;

            foreach (var category in cacheCategories)
            {
                var cacheEntries = await _configService.GetCategoryConfigurationAsync(category);
                var itemsToDelete = new List<string>();

                foreach (var entry in cacheEntries)
                {
                    // Check if entry has timestamp and is old
                    if (entry.Key.Contains("_") && DateTime.TryParse(
                        entry.Key.Split('_').LastOrDefault(), out var timestamp))
                    {
                        if (timestamp < DateTime.UtcNow.AddDays(-retentionDays))
                        {
                            itemsToDelete.Add(entry.Key);
                        }
                    }
                }

                foreach (var key in itemsToDelete)
                {
                    await _configService.SetConfigurationAsync(key, null, category);
                    totalItems++;
                }
            }

            return new CleanupOperationResult
            {
                Success = true,
                Message = $"Removed {totalItems} outdated cache entries",
                ItemsProcessed = totalItems
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to clean outdated cache");
            return new CleanupOperationResult
            {
                Success = false,
                Message = $"Failed to clean outdated cache: {ex.Message}",
                ItemsProcessed = 0
            };
        }
    }

    /// <summary>
    /// Optimize databases
    /// </summary>
    private async Task<CleanupOperationResult> OptimizeDatabaseAsync()
    {
        try
        {
            // Optimize configuration database
            await _configService.OptimizeDatabaseAsync();

            // Also optimize vector database if it exists
            var vectorDbPath = Path.Combine(Environment.CurrentDirectory, "vectors.db");
            if (File.Exists(vectorDbPath))
            {
                using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={vectorDbPath}");
                connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandText = "VACUUM; ANALYZE;";
                await command.ExecuteNonQueryAsync();
            }

            return new CleanupOperationResult
            {
                Success = true,
                Message = "Database optimization completed",
                ItemsProcessed = 1
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to optimize database");
            return new CleanupOperationResult
            {
                Success = false,
                Message = $"Failed to optimize database: {ex.Message}",
                ItemsProcessed = 0
            };
        }
    }

    /// <summary>
    /// Get cleanup statistics
    /// </summary>
    public async Task<CleanupStatistics> GetCleanupStatisticsAsync()
    {
        var stats = new CleanupStatistics();

        try
        {
            // Vector database size
            var vectorDbPath = Path.Combine(Environment.CurrentDirectory, "vectors.db");
            if (File.Exists(vectorDbPath))
            {
                stats.VectorDatabaseSize = new FileInfo(vectorDbPath).Length;
            }

            // Configuration database size
            stats.ConfigurationDatabaseSize = new FileInfo(_configService.DatabasePath).Length;

            // Count error logs
            using var errorService = new ErrorLoggingService(_logger);
            var errorLogs = await errorService.GetRecentLogsAsync(10000);
            stats.ErrorLogCount = errorLogs.Count;
            stats.OldestErrorLog = errorLogs.MinBy(l => l.Timestamp)?.Timestamp;
            stats.NewestErrorLog = errorLogs.MaxBy(l => l.Timestamp)?.Timestamp;

            // Count export logs
            var exportLogs = await _configService.GetCategoryConfigurationAsync("export_logs");
            stats.ExportLogCount = exportLogs.Count;

            // Temp files estimate
            var tempPath = Path.GetTempPath();
            if (Directory.Exists(tempPath))
            {
                var tempFiles = Directory.GetFiles(tempPath, "*.tmp", SearchOption.TopDirectoryOnly);
                stats.TempFileCount = tempFiles.Length;
                stats.TempFileSize = tempFiles.Sum(f => new FileInfo(f).Length);
            }

            // Cache entries
            var cacheCategories = new[] { "embedding_cache", "model_cache", "extraction_cache" };
            foreach (var category in cacheCategories)
            {
                var entries = await _configService.GetCategoryConfigurationAsync(category);
                stats.CacheEntryCount += entries.Count;
            }

            stats.LastCalculated = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to calculate cleanup statistics");
        }

        return stats;
    }

    /// <summary>
    /// Get cleanup history
    /// </summary>
    public async Task<List<CleanupResult>> GetCleanupHistoryAsync(int maxRecords = 20)
    {
        try
        {
            var cleanupLogs = await _configService.GetCategoryConfigurationAsync("cleanup_logs");
            var results = new List<CleanupResult>();

            foreach (var log in cleanupLogs.OrderByDescending(kvp => kvp.Key).Take(maxRecords))
            {
                try
                {
                    var result = JsonSerializer.Deserialize<CleanupResult>(log.Value ?? string.Empty, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse cleanup log entry: {Key}", log.Key);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve cleanup history");
            return [];
        }
    }

    /// <summary>
    /// Log cleanup operation for history
    /// </summary>
    private async Task LogCleanupOperationAsync(CleanupResult result)
    {
        try
        {
            var logEntry = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var key = $"cleanup_{result.StartedAt:yyyyMMdd_HHmmss}";
            await _configService.SetConfigurationAsync(key, logEntry, "cleanup_logs");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to log cleanup operation");
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
                _logger?.LogError(ex, "Error disposing CleanupService");
            }
            
            _disposed = true;
        }
    }
}

/// <summary>
/// Cleanup operation options
/// </summary>
public class CleanupOptions
{
    public bool CleanVectorDatabase { get; set; } = false;
    public bool CleanErrorLogs { get; set; } = true;
    public bool CleanExportLogs { get; set; } = true;
    public bool CleanTempFiles { get; set; } = true;
    public bool CleanOutdatedCache { get; set; } = true;
    public bool OptimizeDatabase { get; set; } = true;

    public string? VectorDatabasePath { get; set; }
    public int ErrorLogRetentionDays { get; set; } = 30;
    public int ExportLogRetentionDays { get; set; } = 90;
    public int TempFileAgeHours { get; set; } = 24;
    public int CacheRetentionDays { get; set; } = 7;
}

/// <summary>
/// Result of cleanup operation
/// </summary>
public class CleanupResult
{
    public bool Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public CleanupOptions Options { get; set; } = new();
    public Dictionary<string, string> Details { get; set; } = [];

    public bool VectorDatabaseCleaned { get; set; }
    public long VectorDatabaseSize { get; set; }
    
    public bool ErrorLogsCleaned { get; set; }
    public int ErrorLogsRemoved { get; set; }
    
    public bool ExportLogsCleaned { get; set; }
    public int ExportLogsRemoved { get; set; }
    
    public bool TempFilesCleaned { get; set; }
    public int TempFilesRemoved { get; set; }
    public long TempFilesSize { get; set; }
    
    public bool CacheCleaned { get; set; }
    public int CacheEntriesRemoved { get; set; }
    
    public bool DatabaseOptimized { get; set; }
}

/// <summary>
/// Individual cleanup operation result
/// </summary>
public class CleanupOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ItemsProcessed { get; set; }
    public long SpaceFreed { get; set; }
}

/// <summary>
/// System cleanup statistics
/// </summary>
public class CleanupStatistics
{
    public long VectorDatabaseSize { get; set; }
    public long ConfigurationDatabaseSize { get; set; }
    public int ErrorLogCount { get; set; }
    public int ExportLogCount { get; set; }
    public int TempFileCount { get; set; }
    public long TempFileSize { get; set; }
    public int CacheEntryCount { get; set; }
    public DateTime? OldestErrorLog { get; set; }
    public DateTime? NewestErrorLog { get; set; }
    public DateTime LastCalculated { get; set; }
}