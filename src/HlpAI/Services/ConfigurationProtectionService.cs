using Microsoft.Extensions.Logging;
using HlpAI.Models;

namespace HlpAI.Services;

/// <summary>
/// Service to protect critical configuration settings from being reset
/// </summary>
public class ConfigurationProtectionService
{
    private readonly SqliteConfigurationService _configService;
    private readonly ILogger? _logger;
    private readonly string _protectedSettingsKey = "protected_user_preferences";

    public ConfigurationProtectionService(SqliteConfigurationService configService, ILogger? logger = null)
    {
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// Backs up critical user preferences that should survive resets
    /// </summary>
    public async Task<bool> BackupUserPreferencesAsync()
    {
        try
        {
            var config = await _configService.LoadAppConfigurationAsync();
            
            var preferences = new ProtectedUserPreferences
            {
                LastDirectory = config.LastDirectory,
                RememberLastDirectory = config.RememberLastDirectory,
                DefaultPromptBehavior = await GetPromptBehaviorAsync(),
                BackupTimestamp = DateTime.UtcNow
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(preferences);
            await _configService.SetConfigurationAsync(_protectedSettingsKey, json, "system");
            
            _logger?.LogInformation("User preferences backed up successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to backup user preferences");
            return false;
        }
    }

    /// <summary>
    /// Restores critical user preferences after a reset
    /// </summary>
    public async Task<bool> RestoreUserPreferencesAsync()
    {
        try
        {
            var json = await _configService.GetConfigurationAsync(_protectedSettingsKey, "system");
            if (string.IsNullOrEmpty(json)) return false;
            
            var preferences = System.Text.Json.JsonSerializer.Deserialize<ProtectedUserPreferences>(json);
            if (preferences == null) return false;
            
            var config = await _configService.LoadAppConfigurationAsync();
            
            // Restore directory preferences
            config.LastDirectory = preferences.LastDirectory;
            config.RememberLastDirectory = preferences.RememberLastDirectory;
            
            await _configService.SaveAppConfigurationAsync(config);
            
            // Restore prompt behavior
            if (preferences.DefaultPromptBehavior != null)
            {
                using var promptService = new PromptService(_configService, _logger);
                await promptService.SetDefaultPromptBehaviorAsync(preferences.DefaultPromptBehavior);
            }
            
            _logger?.LogInformation("User preferences restored successfully from backup created at {BackupTime}", 
                preferences.BackupTimestamp);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to restore user preferences");
            return false;
        }
    }

    /// <summary>
    /// Automatically backs up preferences before any reset operation
    /// </summary>
    public async Task<bool> PreResetBackupAsync()
    {
        await BackupUserPreferencesAsync();
        
        // Set a flag indicating a reset is about to happen
        await _configService.SetConfigurationAsync("pending_reset", "true", "system");
        return true;
    }

    /// <summary>
    /// Checks if preferences should be restored after startup
    /// </summary>
    public async Task<bool> CheckAndRestoreAfterResetAsync()
    {
        try
        {
            var pendingReset = await _configService.GetConfigurationAsync("pending_reset", "system");
            if (pendingReset == "true")
            {
                // Remove the flag first
                await _configService.RemoveConfigurationAsync("pending_reset", "system");
                
                // Restore preferences
                var restored = await RestoreUserPreferencesAsync();
                if (restored)
                {
                    Console.WriteLine("✅ User preferences automatically restored after reset");
                    return true;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to check and restore preferences after reset");
            return false;
        }
    }

    /// <summary>
    /// Gets the current prompt behavior setting
    /// </summary>
    private async Task<bool?> GetPromptBehaviorAsync()
    {
        try
        {
            var setting = await _configService.GetConfigurationAsync("default_prompt_behavior", "ui");
            return setting?.ToLowerInvariant() switch
            {
                "yes" => true,
                "no" => false,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Protected user preferences that should survive configuration resets
/// </summary>
public class ProtectedUserPreferences
{
    public string? LastDirectory { get; set; }
    public bool RememberLastDirectory { get; set; } = true;
    public bool? DefaultPromptBehavior { get; set; }
    public DateTime BackupTimestamp { get; set; }
}