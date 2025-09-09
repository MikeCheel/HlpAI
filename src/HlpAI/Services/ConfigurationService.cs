using System.Text.Json;
using HlpAI.Models;
using Microsoft.Extensions.Logging;
using HlpAI.Utilities;

namespace HlpAI.Services;

public class ConfigurationService
{
    private readonly ILogger<ConfigurationService>? _logger;
    private readonly SqliteConfigurationService _sqliteConfigurationService;
    
    // Thread-local storage for config file path override (used in tests)

    
    // Static cache for configuration to prevent multiple loads
    private static AppConfiguration? _cachedConfiguration;
    private static readonly object _cacheLock = new object();
    private static DateTime _lastCacheUpdate = DateTime.MinValue;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);
    
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ConfigurationService(ILogger<ConfigurationService>? logger = null, SqliteConfigurationService? sqliteConfigurationService = null)
    {
        _logger = logger;
        _sqliteConfigurationService = sqliteConfigurationService ?? SqliteConfigurationService.GetInstance(logger);
    }



    /// <summary>
    /// Loads the application configuration from SQLite database with caching
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <param name="forceReload">Force reload from database, bypassing cache</param>
    /// <returns>The loaded configuration or a new default configuration</returns>
    public static AppConfiguration LoadConfiguration(ILogger? logger = null, bool forceReload = false)
    {
        lock (_cacheLock)
        {
            // Return cached configuration if it exists and is not expired
            if (!forceReload && _cachedConfiguration != null &&
                (DateTime.UtcNow - _lastCacheUpdate) < CacheExpiration)
            {
                logger?.LogDebug("Returning cached configuration (last updated: {LastUpdate})", _lastCacheUpdate);
                return _cachedConfiguration;
            }

            try
            {
                var sqliteConfig = SqliteConfigurationService.GetInstance(logger);
                var result = sqliteConfig.LoadAppConfigurationAsync().GetAwaiter().GetResult();
                SqliteConfigurationService.ReleaseInstance();
                
                // Update cache
                _cachedConfiguration = result;
                _lastCacheUpdate = DateTime.UtcNow;
                logger?.LogDebug("Configuration loaded from SQLite and cached (expires in {Minutes} minutes)",
                    CacheExpiration.TotalMinutes);
                
                return result;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error loading configuration from SQLite. Using default configuration.");
                SqliteConfigurationService.ReleaseInstance();
                
                // Return default configuration but don't cache errors
                return new AppConfiguration();
            }
        }
    }
    
    /// <summary>
    /// Clears the configuration cache, forcing the next load to read from SQLite
    /// </summary>
    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            _cachedConfiguration = null;
            _lastCacheUpdate = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Saves the application configuration to SQLite database and updates cache
    /// </summary>
    /// <param name="config">The configuration to save</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>True if saved successfully, false otherwise</returns>
    public static bool SaveConfiguration(AppConfiguration config, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        
        try
        {
            var sqliteConfig = SqliteConfigurationService.GetInstance(logger);
            config.LastUpdated = DateTime.UtcNow;
            var result = sqliteConfig.SaveAppConfigurationAsync(config).GetAwaiter().GetResult();
            SqliteConfigurationService.ReleaseInstance();
            
            if (result)
            {
                // Update cache with the saved configuration
                lock (_cacheLock)
                {
                    _cachedConfiguration = config;
                    _lastCacheUpdate = DateTime.UtcNow;
                    logger?.LogDebug("Configuration saved to SQLite and cache updated");
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error saving configuration to SQLite");
            SqliteConfigurationService.ReleaseInstance();
            return false;
        }
    }

    /// <summary>
    /// Updates the last used directory in the configuration
    /// </summary>
    /// <param name="directory">The directory path to remember</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>True if updated and saved successfully</returns>
    public static bool UpdateLastDirectory(string directory, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        
        var config = LoadConfiguration(logger);
        config.LastDirectory = directory;
        return SaveConfiguration(config, logger);
    }

    /// <summary>
    /// Updates the last used model in the configuration
    /// </summary>
    /// <param name="model">The model name to remember</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>True if updated and saved successfully</returns>
    public static bool UpdateLastModel(string model, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        
        var config = LoadConfiguration(logger);
        config.LastModel = model;
        
        return SaveConfiguration(config, logger);
    }

    /// <summary>
    /// Updates the AI provider configuration in SQLite
    /// </summary>
    /// <param name="providerType">The AI provider type</param>
    /// <param name="model">The model name</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>True if updated successfully</returns>
    public static bool UpdateAiProviderConfiguration(AiProviderType providerType, string model, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        
        var config = LoadConfiguration(logger);
        config.LastProvider = providerType;
        config.LastModel = model;
        
        // Save to SQLite (primary storage)
        bool sqliteSuccess = false;
        try
        {
            var sqliteConfig = SqliteConfigurationService.GetInstance(logger);
            sqliteSuccess = sqliteConfig.SetAiProviderConfigurationAsync(providerType, model).GetAwaiter().GetResult();
            SqliteConfigurationService.ReleaseInstance();
            if (sqliteSuccess)
            {
                logger?.LogInformation("AI provider configuration saved to SQLite: {Provider} with model {Model}", providerType, model);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to save AI provider configuration to SQLite");
            SqliteConfigurationService.ReleaseInstance();
            sqliteSuccess = false;
        }
        
        // Also save the complete configuration to SQLite
        var configSuccess = SaveConfiguration(config, logger);
        
        return sqliteSuccess && configSuccess;
    }

    /// <summary>
    /// Updates the last used operation mode in the configuration
    /// </summary>
    /// <param name="mode">The operation mode to remember</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>True if updated and saved successfully</returns>
    public static bool UpdateLastOperationMode(OperationMode mode, ILogger? logger = null)
    {
        var config = LoadConfiguration(logger);
        config.LastOperationMode = mode;
        return SaveConfiguration(config, logger);
    }

    /// <summary>
    /// Updates the hh.exe path in the configuration
    /// </summary>
    /// <param name="hhExePath">The path to hh.exe executable</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>True if updated and saved successfully</returns>
    public static bool UpdateHhExePath(string? hhExePath, ILogger? logger = null)
    {
        var config = LoadConfiguration(logger);
        config.HhExePath = hhExePath;
        return SaveConfiguration(config, logger);
    }

    /// <summary>
    /// Updates the auto-detect hh.exe setting in the configuration
    /// </summary>
    /// <param name="autoDetect">Whether to auto-detect hh.exe location</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>True if updated and saved successfully</returns>
    public static bool UpdateAutoDetectHhExe(bool autoDetect, ILogger? logger = null)
    {
        var config = LoadConfiguration(logger);
        config.AutoDetectHhExe = autoDetect;
        return SaveConfiguration(config, logger);
    }

    /// <summary>
    /// Attempts to find hh.exe in common locations
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>The path to hh.exe if found, null otherwise</returns>
    public static string? DetectHhExePath(ILogger? logger = null)
    {
        var commonPaths = new[]
        {
            @"C:\Windows\hh.exe",
            @"C:\Windows\System32\hh.exe",
            @"C:\Program Files (x86)\HTML Help Workshop\hh.exe",
            @"C:\Program Files\HTML Help Workshop\hh.exe"
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                logger?.LogInformation("Found hh.exe at: {HhExePath}", path);
                return path;
            }
        }

        // Additional common installation paths to check
        var additionalPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "HTML Help Workshop", "hh.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "HTML Help Workshop", "hh.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "hh.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "hh.exe")
        };

        foreach (var path in additionalPaths)
        {
            if (File.Exists(path))
            {
                logger?.LogInformation("Found hh.exe at additional location: {HhExePath}", path);
                return path;
            }
        }

        logger?.LogWarning("hh.exe not found in any common locations");
        return null;
    }

    /// <summary>
    /// Gets the configured or detected hh.exe path
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>The path to hh.exe, or "hh.exe" as fallback</returns>
    public static string GetHhExePath(ILogger? logger = null)
    {
        var config = LoadConfiguration(logger);
        
        // If explicitly configured, use that path
        if (!string.IsNullOrEmpty(config.HhExePath))
        {
            if (File.Exists(config.HhExePath))
            {
                return config.HhExePath;
            }
            else
            {
                logger?.LogWarning("Configured hh.exe path does not exist: {HhExePath}", config.HhExePath);
            }
        }

        // If auto-detect is enabled and no valid manual path exists, try to find it
        if (config.AutoDetectHhExe)
        {
            var detectedPath = DetectHhExePath(logger);
            if (detectedPath != null && ValidateHhExePath(detectedPath, logger))
            {
                // Save the detected path for future use only if it's valid
                config.HhExePath = detectedPath;
                SaveConfiguration(config, logger);
                return detectedPath;
            }
        }

        // Fallback to just "hh.exe" - user must ensure it's accessible or configure manually
        logger?.LogInformation("Using fallback hh.exe path - configure manually if needed");
        return "hh.exe";
    }

    /// <summary>
    /// Validates if the given hh.exe path is valid and executable
    /// </summary>
    /// <param name="hhExePath">The path to validate</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>True if the path is valid and executable</returns>
    public static bool ValidateHhExePath(string? hhExePath, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(hhExePath))
        {
            return false;
        }

        if (!File.Exists(hhExePath))
        {
            logger?.LogWarning("hh.exe not found at path: {HhExePath}", hhExePath);
            return false;
        }

        try
        {
            var fileInfo = new FileInfo(hhExePath);
            if (fileInfo.Name.Equals("hh.exe", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else
            {
                logger?.LogWarning("File is not hh.exe: {HhExePath}", hhExePath);
                return false;
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error validating hh.exe path: {HhExePath}", hhExePath);
            return false;
        }
    }

    /// <summary>
    /// Gets the configuration status for display purposes using cached configuration when available
    /// </summary>
    /// <param name="sqliteConfigService">Optional SQLite configuration service for testing purposes</param>
    /// <returns>A string describing the configuration status</returns>
    public static string GetConfigurationStatus(SqliteConfigurationService? sqliteConfigService = null)
    {
        try
        {
            DatabasePathHelper.EnsureApplicationDirectoryExists();
            var dbPath = sqliteConfigService?.DatabasePath ?? DatabasePathHelper.ConfigDatabasePath;
            
            // Check if database file exists
            if (!File.Exists(dbPath))
            {
                return $"Configuration database: Not found\n" +
                       $"Will be created at: {dbPath}";
            }
            
            // Try to use cached configuration first
            lock (_cacheLock)
            {
                if (_cachedConfiguration != null && (DateTime.UtcNow - _lastCacheUpdate) < CacheExpiration)
                {
                    var config = _cachedConfiguration;
                    return $"Configuration database: {dbPath}\n" +
                           $"Last updated: {config.LastUpdated:yyyy-MM-dd HH:mm:ss} UTC\n" +
                           $"Configuration source: Cached (expires in {(CacheExpiration - (DateTime.UtcNow - _lastCacheUpdate)).TotalMinutes:F1} minutes)\n" +
                           $"Remember last directory: {(config.RememberLastDirectory ? "Yes" : "No")}\n" +
                           $"Remember last model: {(config.RememberLastModel ? "Yes" : "No")}\n" +
                           $"Remember last operation mode: {(config.RememberLastOperationMode ? "Yes" : "No")}\n" +
                           $"hh.exe path: {(string.IsNullOrEmpty(config.HhExePath) ? "Auto-detect" : config.HhExePath)}\n" +
                           $"Auto-detect hh.exe: {(config.AutoDetectHhExe ? "Yes" : "No")}";
                }
            }
            
            // Fall back to loading from SQLite if cache is expired or empty
            SqliteConfigurationService configToUse;
            bool shouldDispose = false;
            
            if (sqliteConfigService != null)
            {
                configToUse = sqliteConfigService;
            }
            else
            {
                configToUse = SqliteConfigurationService.GetInstance();
                shouldDispose = true;
            }
            
            try
            {
                var config = configToUse.LoadAppConfigurationAsync().GetAwaiter().GetResult();
                var stats = configToUse.GetStatsAsync().GetAwaiter().GetResult();
                
                // Update cache with the loaded configuration
                lock (_cacheLock)
                {
                    _cachedConfiguration = config;
                    _lastCacheUpdate = DateTime.UtcNow;
                }
                
                return $"Configuration database: {dbPath}\n" +
                       $"Last updated: {config.LastUpdated:yyyy-MM-dd HH:mm:ss} UTC\n" +
                       $"Total configurations: {stats.TotalItems}\n" +
                       $"Categories: {stats.TotalCategories}\n" +
                       $"Remember last directory: {(config.RememberLastDirectory ? "Yes" : "No")}\n" +
                       $"Remember last model: {(config.RememberLastModel ? "Yes" : "No")}\n" +
                       $"Remember last operation mode: {(config.RememberLastOperationMode ? "Yes" : "No")}\n" +
                       $"hh.exe path: {(string.IsNullOrEmpty(config.HhExePath) ? "Auto-detect" : config.HhExePath)}\n" +
                       $"Auto-detect hh.exe: {(config.AutoDetectHhExe ? "Yes" : "No")}";
            }
            finally
            {
                if (shouldDispose)
                {
                    SqliteConfigurationService.ReleaseInstance();
                }
            }
        }
        catch (Exception ex)
        {
            DatabasePathHelper.EnsureApplicationDirectoryExists();
            var dbPath = sqliteConfigService?.DatabasePath ?? DatabasePathHelper.ConfigDatabasePath;
            return $"Configuration database: Error reading configuration - {ex.Message}";
        }
    }
}
