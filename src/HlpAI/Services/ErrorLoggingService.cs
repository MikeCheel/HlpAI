using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HlpAI.Services;

/// <summary>
/// Service for logging errors and system events to SQLite database with configurable settings
/// </summary>
public class ErrorLoggingService : IDisposable
{
    private readonly ILogger? _logger;
    private readonly SqliteConfigurationService _configService;
    private readonly bool _ownsConfigService;
    private bool _disposed = false;

    public ErrorLoggingService(ILogger? logger = null)
    {
        _logger = logger;
        _configService = new SqliteConfigurationService(logger);
        _ownsConfigService = true;
    }

    public ErrorLoggingService(SqliteConfigurationService configService, ILogger? logger = null)
    {
        _logger = logger;
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _ownsConfigService = false;
    }

    /// <summary>
    /// Log an error to the database
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="exception">Optional exception details</param>
    /// <param name="context">Optional context information</param>
    /// <param name="logLevel">Log level (default: Error)</param>
    public async Task LogErrorAsync(string message, Exception? exception = null, string? context = null, LogLevel logLevel = LogLevel.Error)
    {
        if (!await IsLoggingEnabledAsync())
        {
            return;
        }

        // Debug: Check if exception has stack trace
        string? stackTrace = exception?.StackTrace;
        if (exception != null && string.IsNullOrEmpty(stackTrace))
        {
            _logger?.LogWarning("Exception of type {ExceptionType} has null or empty stack trace", exception.GetType().Name);
            
            // Try to capture stack trace manually if it's null
            try
            {
                stackTrace = Environment.StackTrace;
            }
            catch
            {
                stackTrace = "Stack trace unavailable";
            }
        }

        var logEntry = new ErrorLogEntry
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            LogLevel = logLevel.ToString(),
            Message = message,
            ExceptionType = exception?.GetType().Name,
            ExceptionMessage = exception?.Message,
            StackTrace = stackTrace,
            Context = context,
            Source = "HlpAI.Interactive"
        };

        var logJson = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = false });
        var key = $"error_log_{logEntry.Timestamp:yyyyMMdd_HHmmss}_{logEntry.Id[..8]}";
        
        await _configService.SetConfigurationAsync(key, logJson, "error_logs");
        
        _logger?.LogDebug("Error logged to database: {Message}", message);
        
        // Cleanup old logs if retention policy is exceeded
        await CleanupOldLogsAsync();
    }

    /// <summary>
    /// Log an information message
    /// </summary>
    public async Task LogInformationAsync(string message, string? context = null)
    {
        await LogErrorAsync(message, null, context, LogLevel.Information);
    }

    /// <summary>
    /// Log a warning message
    /// </summary>
    public async Task LogWarningAsync(string message, string? context = null)
    {
        await LogErrorAsync(message, null, context, LogLevel.Warning);
    }

    /// <summary>
    /// Get recent error logs
    /// </summary>
    /// <param name="count">Number of logs to retrieve (default: 50)</param>
    /// <param name="logLevel">Filter by log level (optional)</param>
    /// <returns>List of error log entries</returns>
    public async Task<List<ErrorLogEntry>> GetRecentLogsAsync(int count = 50, LogLevel? logLevel = null)
    {
        var allLogs = await _configService.GetCategoryConfigurationAsync("error_logs");
        var logEntries = new List<ErrorLogEntry>();

        foreach (var logKvp in allLogs)
        {
            try
            {
                if (!string.IsNullOrEmpty(logKvp.Value))
                {
                    var logEntry = JsonSerializer.Deserialize<ErrorLogEntry>(logKvp.Value);
                    if (logEntry != null)
                    {
                        if (logLevel == null || logEntry.LogLevel == logLevel.ToString())
                        {
                            logEntries.Add(logEntry);
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning("Failed to deserialize log entry {Key}: {Message}", logKvp.Key, ex.Message);
            }
        }

        return logEntries
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Get log statistics
    /// </summary>
    public async Task<ErrorLogStatistics> GetLogStatisticsAsync()
    {
        var allLogs = await _configService.GetCategoryConfigurationAsync("error_logs");
        var logEntries = new List<ErrorLogEntry>();

        foreach (var logKvp in allLogs)
        {
            try
            {
                if (!string.IsNullOrEmpty(logKvp.Value))
                {
                    var logEntry = JsonSerializer.Deserialize<ErrorLogEntry>(logKvp.Value);
                    if (logEntry != null)
                    {
                        logEntries.Add(logEntry);
                    }
                }
            }
            catch (JsonException)
            {
                // Skip invalid entries
            }
        }

        var now = DateTime.UtcNow;
        var last24Hours = logEntries.Where(l => l.Timestamp > now.AddDays(-1)).ToList();
        var last7Days = logEntries.Where(l => l.Timestamp > now.AddDays(-7)).ToList();

        return new ErrorLogStatistics
        {
            TotalLogs = logEntries.Count,
            ErrorsLast24Hours = last24Hours.Count(l => l.LogLevel == LogLevel.Error.ToString()),
            WarningsLast24Hours = last24Hours.Count(l => l.LogLevel == LogLevel.Warning.ToString()),
            ErrorsLast7Days = last7Days.Count(l => l.LogLevel == LogLevel.Error.ToString()),
            WarningsLast7Days = last7Days.Count(l => l.LogLevel == LogLevel.Warning.ToString()),
            OldestLogDate = logEntries.OrderBy(l => l.Timestamp).FirstOrDefault()?.Timestamp,
            NewestLogDate = logEntries.OrderByDescending(l => l.Timestamp).FirstOrDefault()?.Timestamp
        };
    }

    /// <summary>
    /// Clear all error logs
    /// </summary>
    public async Task<bool> ClearAllLogsAsync()
    {
        try
        {
            await _configService.ClearCategoryAsync("error_logs");
            _logger?.LogInformation("All error logs cleared");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to clear error logs");
            return false;
        }
    }

    /// <summary>
    /// Check if logging is enabled
    /// </summary>
    public async Task<bool> IsLoggingEnabledAsync()
    {
        var setting = await _configService.GetConfigurationAsync("error_logging_enabled", "logging", "true");
        return bool.TryParse(setting, out var enabled) && enabled;
    }

    /// <summary>
    /// Enable or disable error logging
    /// </summary>
    public async Task<bool> SetLoggingEnabledAsync(bool enabled)
    {
        var result = await _configService.SetConfigurationAsync("error_logging_enabled", enabled.ToString().ToLower(), "logging");
        if (result)
        {
            _logger?.LogInformation("Error logging {Status}", enabled ? "enabled" : "disabled");
        }
        return result;
    }

    /// <summary>
    /// Get log retention days setting
    /// </summary>
    public async Task<int> GetLogRetentionDaysAsync()
    {
        var setting = await _configService.GetConfigurationAsync("log_retention_days", "logging", "30");
        return int.TryParse(setting, out var days) && days > 0 ? days : 30;
    }

    /// <summary>
    /// Set log retention days
    /// </summary>
    public async Task<bool> SetLogRetentionDaysAsync(int days)
    {
        if (days <= 0)
        {
            return false;
        }

        var result = await _configService.SetConfigurationAsync("log_retention_days", days.ToString(), "logging");
        if (result)
        {
            _logger?.LogInformation("Log retention set to {Days} days", days);
            await CleanupOldLogsAsync();
        }
        return result;
    }

    /// <summary>
    /// Get minimum log level setting
    /// </summary>
    public async Task<LogLevel> GetMinimumLogLevelAsync()
    {
        var setting = await _configService.GetConfigurationAsync("minimum_log_level", "logging", "Warning");
        return Enum.TryParse<LogLevel>(setting, out var level) ? level : LogLevel.Warning;
    }

    /// <summary>
    /// Set minimum log level
    /// </summary>
    public async Task<bool> SetMinimumLogLevelAsync(LogLevel logLevel)
    {
        var result = await _configService.SetConfigurationAsync("minimum_log_level", logLevel.ToString(), "logging");
        if (result)
        {
            _logger?.LogInformation("Minimum log level set to {Level}", logLevel);
        }
        return result;
    }

    /// <summary>
    /// Clean up old logs based on retention policy
    /// </summary>
    private async Task CleanupOldLogsAsync()
    {
        try
        {
            var retentionDays = await GetLogRetentionDaysAsync();
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            
            var allLogs = await _configService.GetCategoryConfigurationAsync("error_logs");
            var logsToDelete = new List<string>();

            foreach (var logKvp in allLogs)
            {
                try
                {
                    if (!string.IsNullOrEmpty(logKvp.Value))
                    {
                        var logEntry = JsonSerializer.Deserialize<ErrorLogEntry>(logKvp.Value);
                        if (logEntry != null && logEntry.Timestamp < cutoffDate)
                        {
                            logsToDelete.Add(logKvp.Key);
                        }
                    }
                    else
                    {
                        // Empty/null entries should be deleted
                        logsToDelete.Add(logKvp.Key);
                    }
                }
                catch (JsonException)
                {
                    // If we can't parse it, it's probably corrupted, so delete it
                    logsToDelete.Add(logKvp.Key);
                }
            }

            foreach (var key in logsToDelete)
            {
                await _configService.RemoveConfigurationAsync(key, "error_logs");
            }

            if (logsToDelete.Count > 0)
            {
                _logger?.LogDebug("Cleaned up {Count} old log entries", logsToDelete.Count);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to cleanup old logs");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                // Only dispose the config service if we own it
                if (_ownsConfigService)
                {
                    _configService?.Dispose();
                }
            }
            catch (Exception ex)
            {
                // Log disposal error but don't throw
                _logger?.LogError(ex, "Error disposing ErrorLoggingService");
            }
            
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents a single error log entry
/// </summary>
public class ErrorLogEntry
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string LogLevel { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? StackTrace { get; set; }
    public string? Context { get; set; }
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// Statistics about error logs
/// </summary>
public class ErrorLogStatistics
{
    public int TotalLogs { get; set; }
    public int ErrorsLast24Hours { get; set; }
    public int WarningsLast24Hours { get; set; }
    public int ErrorsLast7Days { get; set; }
    public int WarningsLast7Days { get; set; }
    public DateTime? OldestLogDate { get; set; }
    public DateTime? NewestLogDate { get; set; }
}
