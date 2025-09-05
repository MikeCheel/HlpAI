using Microsoft.Extensions.Logging;

namespace HlpAI.Services;

/// <summary>
/// Service to validate and enforce critical configuration rules
/// </summary>
public static class ConfigurationValidationService
{
    /// <summary>
    /// Ensures critical settings are properly configured on startup
    /// </summary>
    public static async Task EnforceConfigurationRulesAsync(SqliteConfigurationService configService, ILogger? logger = null)
    {
        try
        {
            // Ensure RememberLastDirectory is always enabled unless explicitly disabled
            var config = await configService.LoadAppConfigurationAsync();
            if (!config.RememberLastDirectory)
            {
                // Check if user explicitly disabled it
                var explicitSetting = await configService.GetConfigurationAsync("user_disabled_remember_directory", "system");
                if (explicitSetting != "true")
                {
                    // Re-enable it as it may have been reset accidentally
                    config.RememberLastDirectory = true;
                    await configService.SaveAppConfigurationAsync(config);
                    logger?.LogInformation("Re-enabled RememberLastDirectory after potential reset");
                }
            }

            // Ensure prompt behavior doesn't get stuck on "always no"
            using var promptService = new PromptService(configService, logger);
            var promptBehavior = await promptService.GetDefaultPromptBehaviorAsync();
            if (promptBehavior == false) // If set to always "No"
            {
                // Check if user explicitly set this
                var explicitSetting = await configService.GetConfigurationAsync("user_wants_always_no", "system");
                if (explicitSetting != "true")
                {
                    // Reset to individual defaults
                    await promptService.SetDefaultPromptBehaviorAsync(null);
                    logger?.LogInformation("Reset prompt behavior from 'always no' to individual defaults");
                    
                    Console.WriteLine("🔧 Fixed prompt behavior: Reset from 'Always No' to individual defaults");
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error enforcing configuration rules");
        }
    }

    /// <summary>
    /// Mark that user explicitly disabled RememberLastDirectory
    /// </summary>
    public static async Task MarkUserDisabledRememberDirectoryAsync(SqliteConfigurationService configService)
    {
        await configService.SetConfigurationAsync("user_disabled_remember_directory", "true", "system");
    }

    /// <summary>
    /// Mark that user explicitly wants "Always No" prompt behavior
    /// </summary>
    public static async Task MarkUserWantsAlwaysNoAsync(SqliteConfigurationService configService)
    {
        await configService.SetConfigurationAsync("user_wants_always_no", "true", "system");
    }
}