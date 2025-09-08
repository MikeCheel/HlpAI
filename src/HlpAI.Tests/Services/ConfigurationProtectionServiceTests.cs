using Microsoft.Extensions.Logging;
using HlpAI.Services;
using HlpAI.Models;
using HlpAI.Tests.TestHelpers;
using TUnit.Assertions;

namespace HlpAI.Tests.Services;

public class ConfigurationProtectionServiceTests
{
    private string _testDirectory = null!;
    private SqliteConfigurationService _configService = null!;
    private ConfigurationProtectionService _service = null!;
    private ILogger _logger = null!;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory($"config_protection_tests_{Guid.NewGuid().ToString("N")[..8]}");
        var testDbPath = Path.Combine(_testDirectory, "test_config.db");
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ConfigurationProtectionServiceTests>();
        _configService = new SqliteConfigurationService(testDbPath, _logger);
        _service = new ConfigurationProtectionService(_configService, _logger);
    }

    [After(Test)]
    public void Cleanup()
    {
        _configService?.Dispose();
        FileTestHelper.SafeDeleteDirectory(_testDirectory);
    }

    [Test]
    public async Task BackupUserPreferencesAsync_ShouldIncludeAiProviderSettings()
    {
        // Arrange
        var testConfig = new AppConfiguration
        {
            LastDirectory = "/test/path",
            RememberLastDirectory = true,
            LastProvider = AiProviderType.DeepSeek,
            LastModel = "deepseek-chat"
        };
        
        await _configService.SaveAppConfigurationAsync(testConfig);
        await _configService.SetConfigurationAsync("DefaultPromptBehavior", "true", "system");

        // Act
        var result = await _service.BackupUserPreferencesAsync();

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify the backup was created with AI provider settings
        var backupJson = await _configService.GetConfigurationAsync("protected_user_preferences", "system");
        await Assert.That(backupJson).IsNotNull();
        await Assert.That(backupJson!.Contains("6")).IsTrue(); // DeepSeek enum value
        await Assert.That(backupJson.Contains("deepseek-chat")).IsTrue();
    }

    [Test]
    public async Task RestoreUserPreferencesAsync_ShouldRestoreAiProviderSettings()
    {
        // Arrange - First create a backup
        var originalConfig = new AppConfiguration
        {
            LastDirectory = "/test/path",
            RememberLastDirectory = true,
            LastProvider = AiProviderType.DeepSeek,
            LastModel = "deepseek-chat"
        };
        
        await _configService.SaveAppConfigurationAsync(originalConfig);
        await _configService.SetConfigurationAsync("DefaultPromptBehavior", "true", "system");
        await _service.BackupUserPreferencesAsync();
        
        // Now change the config to simulate a reset
        var resetConfig = new AppConfiguration
        {
            LastDirectory = "/current/path",
            RememberLastDirectory = false,
            LastProvider = AiProviderType.None,
            LastModel = null
        };
        await _configService.SaveAppConfigurationAsync(resetConfig);

        // Act
        var result = await _service.RestoreUserPreferencesAsync();

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify the configuration was restored
        var restoredConfig = await _configService.LoadAppConfigurationAsync();
        await Assert.That(restoredConfig.LastProvider).IsEqualTo(AiProviderType.DeepSeek);
        await Assert.That(restoredConfig.LastModel).IsEqualTo("deepseek-chat");
        await Assert.That(restoredConfig.LastDirectory).IsEqualTo("/test/path");
        await Assert.That(restoredConfig.RememberLastDirectory).IsTrue();
    }

    [Test]
    public async Task CheckAndRestoreAfterResetAsync_ShouldRestoreWhenConfigurationIsReset()
    {
        // Arrange - First create a backup
        var originalConfig = new AppConfiguration
        {
            LastDirectory = "/test/path",
            RememberLastDirectory = true,
            LastProvider = AiProviderType.DeepSeek,
            LastModel = "deepseek-chat"
        };
        
        await _configService.SaveAppConfigurationAsync(originalConfig);
        await _configService.SetConfigurationAsync("DefaultPromptBehavior", "true", "system");
        await _service.BackupUserPreferencesAsync();
        
        // Now simulate a reset by setting config to default values and setting the pending reset flag
        var resetConfig = new AppConfiguration
        {
            LastDirectory = null,
            RememberLastDirectory = false,
            LastProvider = AiProviderType.None,
            LastModel = null
        };
        await _configService.SaveAppConfigurationAsync(resetConfig);
        await _configService.SetConfigurationAsync("pending_reset", "true", "system");

        // Act
        var result = await _service.CheckAndRestoreAfterResetAsync();

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify the configuration was restored
        var restoredConfig = await _configService.LoadAppConfigurationAsync();
        await Assert.That(restoredConfig.LastProvider).IsEqualTo(AiProviderType.DeepSeek);
        await Assert.That(restoredConfig.LastModel).IsEqualTo("deepseek-chat");
    }
}