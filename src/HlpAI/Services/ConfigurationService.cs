using System.Text.Json;
using HlpAI.Models;
using Microsoft.Extensions.Logging;

namespace HlpAI.Services;

/// <summary>
/// Service for managing application configuration persistence
/// </summary>
public static class ConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Thread-local storage for test override support
    private static readonly ThreadLocal<string?> _configFilePathOverride = new();

    /// <summary>
    /// Gets the path to the configuration file
    /// </summary>
    public static string ConfigFilePath => 
        _configFilePathOverride.Value ?? 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                     "HlpAI", "config.json");

    /// <summary>
    /// Sets a custom configuration file path. This is intended for testing purposes only.
    /// </summary>
    /// <param name="path">The custom path to use, or null to revert to default</param>
    public static void SetConfigFilePathForTesting(string? path)
    {
        _configFilePathOverride.Value = path;
    }

    /// <summary>
    /// Loads the application configuration from disk
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>The loaded configuration or a new default configuration</returns>
    public static AppConfiguration LoadConfiguration(ILogger? logger = null)
    {
        try
        {
            var configPath = ConfigFilePath;
            
            if (!File.Exists(configPath))
            {
                logger?.LogInformation("Configuration file not found at {ConfigPath}. Using default configuration.", configPath);
                return new AppConfiguration();
            }

            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions);
            
            if (config == null)
            {
                logger?.LogWarning("Failed to deserialize configuration file. Using default configuration.");
                return new AppConfiguration();
            }

            logger?.LogInformation("Configuration loaded successfully from {ConfigPath}", configPath);
            return config;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error loading configuration. Using default configuration.");
            return new AppConfiguration();
        }
    }

    /// <summary>
    /// Saves the application configuration to disk
    /// </summary>
    /// <param name="config">The configuration to save</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>True if saved successfully, false otherwise</returns>
    public static bool SaveConfiguration(AppConfiguration config, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        
        try
        {
            var configPath = ConfigFilePath;
            var configDir = Path.GetDirectoryName(configPath);
            
            if (configDir != null && !Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
                logger?.LogInformation("Created configuration directory: {ConfigDir}", configDir);
            }

            config.LastUpdated = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(configPath, json);

            logger?.LogInformation("Configuration saved successfully to {ConfigPath}", configPath);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error saving configuration to {ConfigPath}", ConfigFilePath);
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
        
        // Also save to SQLite for consistency
        try
        {
            using var sqliteConfig = new SqliteConfigurationService(logger);
            var providerConfig = sqliteConfig.GetAiProviderConfigurationAsync().GetAwaiter().GetResult();
            if (providerConfig.HasValue)
            {
                sqliteConfig.SetAiProviderConfigurationAsync(providerConfig.Value.ProviderType, model).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to update model in SQLite configuration (falling back to JSON only)");
        }
        
        return SaveConfiguration(config, logger);
    }

    /// <summary>
    /// Updates the AI provider configuration in both JSON and SQLite
    /// </summary>
    /// <param name="providerType">The AI provider type</param>
    /// <param name="model">The model name</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>True if updated successfully in both stores</returns>
    public static bool UpdateAiProviderConfiguration(AiProviderType providerType, string model, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        
        var config = LoadConfiguration(logger);
        config.LastProvider = providerType;
        config.LastModel = model;
        
        // Save to SQLite first (primary storage)
        bool sqliteSuccess = false;
        try
        {
            using var sqliteConfig = new SqliteConfigurationService(logger);
            sqliteSuccess = sqliteConfig.SetAiProviderConfigurationAsync(providerType, model).GetAwaiter().GetResult();
            if (sqliteSuccess)
            {
                logger?.LogInformation("AI provider configuration saved to SQLite: {Provider} with model {Model}", providerType, model);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to save AI provider configuration to SQLite");
            sqliteSuccess = false;
        }
        
        // Always save to JSON for backward compatibility
        var jsonSuccess = SaveConfiguration(config, logger);
        
        if (jsonSuccess)
        {
            logger?.LogInformation("AI provider configuration saved to JSON: {Provider} with model {Model}", providerType, model);
        }
        
        return sqliteSuccess && jsonSuccess;
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

        // Try to find via PATH environment variable
        try
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathVar))
            {
                var paths = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                foreach (var pathDir in paths)
                {
                    var hhPath = Path.Combine(pathDir, "hh.exe");
                    if (File.Exists(hhPath))
                    {
                        logger?.LogInformation("Found hh.exe in PATH at: {HhExePath}", hhPath);
                        return hhPath;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Error searching for hh.exe in PATH");
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

        // Fallback to just "hh.exe" and hope it's in PATH
        logger?.LogInformation("Using fallback hh.exe path (hoping it's in system PATH)");
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
    /// Gets the configuration file status for display purposes
    /// </summary>
    /// <returns>A string describing the configuration file status</returns>
    public static string GetConfigurationStatus()
    {
        var configPath = ConfigFilePath;
        
        if (!File.Exists(configPath))
        {
            return $"Configuration file: Not found\nWill be created at: {configPath}";
        }

        try
        {
            var fileInfo = new FileInfo(configPath);
            var config = LoadConfiguration();
            
            return $"Configuration file: {configPath}\n" +
                   $"Last updated: {config.LastUpdated:yyyy-MM-dd HH:mm:ss} UTC\n" +
                   $"File size: {fileInfo.Length} bytes\n" +
                   $"Remember last directory: {(config.RememberLastDirectory ? "Yes" : "No")}\n" +
                   $"Remember last model: {(config.RememberLastModel ? "Yes" : "No")}\n" +
                   $"Remember last operation mode: {(config.RememberLastOperationMode ? "Yes" : "No")}\n" +
                   $"hh.exe path: {(string.IsNullOrEmpty(config.HhExePath) ? "Auto-detect" : config.HhExePath)}\n" +
                   $"Auto-detect hh.exe: {(config.AutoDetectHhExe ? "Yes" : "No")}";
        }
        catch
        {
            return $"Configuration file: {configPath} (Error reading file)";
        }
    }
}
