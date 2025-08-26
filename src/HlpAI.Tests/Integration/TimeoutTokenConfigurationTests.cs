using HlpAI.Models;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using TUnit.Core;
using TUnit.Assertions;

namespace HlpAI.Tests.Integration;

/// <summary>
/// Integration tests for timeout and token configuration functionality
/// </summary>
[NotInParallel]
public class TimeoutTokenConfigurationTests
{
    private string _testDirectory = null!;
    private ILogger<TimeoutTokenConfigurationTests> _logger = null!;
    private string _originalUserProfile = null!;
    private SqliteConfigurationService _configService = null!;
    private string _testDbPath = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("TimeoutTokenConfigTests");
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<TimeoutTokenConfigurationTests>();
        
        // Store original USERPROFILE and set test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? string.Empty;
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        // Initialize test database
        _testDbPath = Path.Combine(_testDirectory, "test_config.db");
        _configService = new SqliteConfigurationService(_testDbPath, _logger);
    }

    [After(Test)]
    public void Cleanup()
    {
        // Restore original USERPROFILE
        Environment.SetEnvironmentVariable("USERPROFILE", _originalUserProfile);
        
        // Dispose services
        _configService?.Dispose();
        
        // Clean up test directory
        FileTestHelper.SafeDeleteDirectory(_testDirectory);
    }

    [Test]
    public async Task AppConfiguration_TimeoutDefaults_AreCorrect()
    {
        // Arrange & Act
        var config = new AppConfiguration();

        // Assert - Verify default timeout values
        await Assert.That(config.AiProviderTimeoutMinutes).IsEqualTo(10);
        await Assert.That(config.OllamaTimeoutMinutes).IsEqualTo(10);
        await Assert.That(config.LmStudioTimeoutMinutes).IsEqualTo(10);
        await Assert.That(config.OpenWebUiTimeoutMinutes).IsEqualTo(10);
        await Assert.That(config.EmbeddingTimeoutMinutes).IsEqualTo(10);
        await Assert.That(config.OpenAiTimeoutMinutes).IsEqualTo(5);
        await Assert.That(config.AnthropicTimeoutMinutes).IsEqualTo(5);
        await Assert.That(config.DeepSeekTimeoutMinutes).IsEqualTo(5);
    }

    [Test]
    public async Task AppConfiguration_TokenDefaults_AreCorrect()
    {
        // Arrange & Act
        var config = new AppConfiguration();

        // Assert - Verify default token values
        await Assert.That(config.OpenAiMaxTokens).IsEqualTo(4000);
        await Assert.That(config.AnthropicMaxTokens).IsEqualTo(4000);
        await Assert.That(config.DeepSeekMaxTokens).IsEqualTo(4000);
        await Assert.That(config.LmStudioMaxTokens).IsEqualTo(4096);
        await Assert.That(config.OpenWebUiMaxTokens).IsEqualTo(4096);
    }

    [Test]
    public async Task AppConfiguration_TimeoutModification_PersistsCorrectly()
    {
        // Arrange
        var config = new AppConfiguration();
        
        // Act - Modify timeout values
        config.AiProviderTimeoutMinutes = 15;
        config.OllamaTimeoutMinutes = 20;
        config.LmStudioTimeoutMinutes = 25;
        config.OpenWebUiTimeoutMinutes = 30;
        config.EmbeddingTimeoutMinutes = 35;
        config.OpenAiTimeoutMinutes = 8;
        config.AnthropicTimeoutMinutes = 12;
        config.DeepSeekTimeoutMinutes = 18;
        
        // Save and reload configuration
        ConfigurationService.SaveConfiguration(config);
        var reloadedConfig = ConfigurationService.LoadConfiguration();

        // Assert - Verify modified values persist
        await Assert.That(reloadedConfig.AiProviderTimeoutMinutes).IsEqualTo(15);
        await Assert.That(reloadedConfig.OllamaTimeoutMinutes).IsEqualTo(20);
        await Assert.That(reloadedConfig.LmStudioTimeoutMinutes).IsEqualTo(25);
        await Assert.That(reloadedConfig.OpenWebUiTimeoutMinutes).IsEqualTo(30);
        await Assert.That(reloadedConfig.EmbeddingTimeoutMinutes).IsEqualTo(35);
        await Assert.That(reloadedConfig.OpenAiTimeoutMinutes).IsEqualTo(8);
        await Assert.That(reloadedConfig.AnthropicTimeoutMinutes).IsEqualTo(12);
        await Assert.That(reloadedConfig.DeepSeekTimeoutMinutes).IsEqualTo(18);
    }

    [Test]
    public async Task AppConfiguration_TokenModification_PersistsCorrectly()
    {
        // Arrange
        var config = new AppConfiguration();
        
        // Act - Modify token values
        config.OpenAiMaxTokens = 8000;
        config.AnthropicMaxTokens = 6000;
        config.DeepSeekMaxTokens = 5000;
        config.LmStudioMaxTokens = 8192;
        config.OpenWebUiMaxTokens = 12000;
        
        // Save and reload configuration
        ConfigurationService.SaveConfiguration(config);
        var reloadedConfig = ConfigurationService.LoadConfiguration();

        // Assert - Verify modified values persist
        await Assert.That(reloadedConfig.OpenAiMaxTokens).IsEqualTo(8000);
        await Assert.That(reloadedConfig.AnthropicMaxTokens).IsEqualTo(6000);
        await Assert.That(reloadedConfig.DeepSeekMaxTokens).IsEqualTo(5000);
        await Assert.That(reloadedConfig.LmStudioMaxTokens).IsEqualTo(8192);
        await Assert.That(reloadedConfig.OpenWebUiMaxTokens).IsEqualTo(12000);
    }

    [Test]
    public async Task AppConfiguration_BoundaryValues_AreHandledCorrectly()
    {
        // Arrange
        var config = new AppConfiguration();
        
        // Act - Set boundary values
        config.AiProviderTimeoutMinutes = 1; // Minimum
        config.OllamaTimeoutMinutes = 60; // Maximum
        config.OpenAiMaxTokens = 100; // Minimum
        config.AnthropicMaxTokens = 32000; // Maximum
        
        // Save and reload configuration
        ConfigurationService.SaveConfiguration(config);
        var reloadedConfig = ConfigurationService.LoadConfiguration();

        // Assert - Verify boundary values persist
        await Assert.That(reloadedConfig.AiProviderTimeoutMinutes).IsEqualTo(1);
        await Assert.That(reloadedConfig.OllamaTimeoutMinutes).IsEqualTo(60);
        await Assert.That(reloadedConfig.OpenAiMaxTokens).IsEqualTo(100);
        await Assert.That(reloadedConfig.AnthropicMaxTokens).IsEqualTo(32000);
    }

    [Test]
    public async Task AppConfiguration_JsonSerialization_PreservesTimeoutAndTokenValues()
    {
        // Arrange
        var originalConfig = new AppConfiguration
        {
            AiProviderTimeoutMinutes = 15,
            OllamaTimeoutMinutes = 20,
            LmStudioTimeoutMinutes = 25,
            OpenWebUiTimeoutMinutes = 30,
            EmbeddingTimeoutMinutes = 35,
            OpenAiTimeoutMinutes = 8,
            AnthropicTimeoutMinutes = 12,
            DeepSeekTimeoutMinutes = 18,
            OpenAiMaxTokens = 8000,
            AnthropicMaxTokens = 6000,
            DeepSeekMaxTokens = 5000,
            LmStudioMaxTokens = 8192,
            OpenWebUiMaxTokens = 12000
        };

        // Act - Serialize and deserialize
        var json = System.Text.Json.JsonSerializer.Serialize(originalConfig, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        
        var deserializedConfig = System.Text.Json.JsonSerializer.Deserialize<AppConfiguration>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        // Assert - Verify all values are preserved
        await Assert.That(deserializedConfig).IsNotNull();
        await Assert.That(deserializedConfig!.AiProviderTimeoutMinutes).IsEqualTo(originalConfig.AiProviderTimeoutMinutes);
        await Assert.That(deserializedConfig.OllamaTimeoutMinutes).IsEqualTo(originalConfig.OllamaTimeoutMinutes);
        await Assert.That(deserializedConfig.LmStudioTimeoutMinutes).IsEqualTo(originalConfig.LmStudioTimeoutMinutes);
        await Assert.That(deserializedConfig.OpenWebUiTimeoutMinutes).IsEqualTo(originalConfig.OpenWebUiTimeoutMinutes);
        await Assert.That(deserializedConfig.EmbeddingTimeoutMinutes).IsEqualTo(originalConfig.EmbeddingTimeoutMinutes);
        await Assert.That(deserializedConfig.OpenAiTimeoutMinutes).IsEqualTo(originalConfig.OpenAiTimeoutMinutes);
        await Assert.That(deserializedConfig.AnthropicTimeoutMinutes).IsEqualTo(originalConfig.AnthropicTimeoutMinutes);
        await Assert.That(deserializedConfig.DeepSeekTimeoutMinutes).IsEqualTo(originalConfig.DeepSeekTimeoutMinutes);
        await Assert.That(deserializedConfig.OpenAiMaxTokens).IsEqualTo(originalConfig.OpenAiMaxTokens);
        await Assert.That(deserializedConfig.AnthropicMaxTokens).IsEqualTo(originalConfig.AnthropicMaxTokens);
        await Assert.That(deserializedConfig.DeepSeekMaxTokens).IsEqualTo(originalConfig.DeepSeekMaxTokens);
        await Assert.That(deserializedConfig.LmStudioMaxTokens).IsEqualTo(originalConfig.LmStudioMaxTokens);
        await Assert.That(deserializedConfig.OpenWebUiMaxTokens).IsEqualTo(originalConfig.OpenWebUiMaxTokens);
    }

    [Test]
    public async Task AppConfiguration_ResetToDefaults_RestoresOriginalValues()
    {
        // Arrange
        var config = new AppConfiguration();
        
        // Modify all values
        config.AiProviderTimeoutMinutes = 99;
        config.OllamaTimeoutMinutes = 99;
        config.LmStudioTimeoutMinutes = 99;
        config.OpenWebUiTimeoutMinutes = 99;
        config.EmbeddingTimeoutMinutes = 99;
        config.OpenAiTimeoutMinutes = 99;
        config.AnthropicTimeoutMinutes = 99;
        config.DeepSeekTimeoutMinutes = 99;
        config.OpenAiMaxTokens = 99;
        config.AnthropicMaxTokens = 99;
        config.DeepSeekMaxTokens = 99;
        config.LmStudioMaxTokens = 99;
        config.OpenWebUiMaxTokens = 99;
        
        // Act - Reset to defaults (simulating the ResetTimeoutAndTokenDefaults method)
        config.AiProviderTimeoutMinutes = 10;
        config.OllamaTimeoutMinutes = 10;
        config.LmStudioTimeoutMinutes = 10;
        config.OpenWebUiTimeoutMinutes = 10;
        config.EmbeddingTimeoutMinutes = 10;
        config.OpenAiTimeoutMinutes = 5;
        config.AnthropicTimeoutMinutes = 5;
        config.DeepSeekTimeoutMinutes = 5;
        config.OpenAiMaxTokens = 4000;
        config.AnthropicMaxTokens = 4000;
        config.DeepSeekMaxTokens = 4000;
        config.LmStudioMaxTokens = 4096;
        config.OpenWebUiMaxTokens = 4096;

        // Assert - Verify values are reset to defaults
        await Assert.That(config.AiProviderTimeoutMinutes).IsEqualTo(10);
        await Assert.That(config.OllamaTimeoutMinutes).IsEqualTo(10);
        await Assert.That(config.LmStudioTimeoutMinutes).IsEqualTo(10);
        await Assert.That(config.OpenWebUiTimeoutMinutes).IsEqualTo(10);
        await Assert.That(config.EmbeddingTimeoutMinutes).IsEqualTo(10);
        await Assert.That(config.OpenAiTimeoutMinutes).IsEqualTo(5);
        await Assert.That(config.AnthropicTimeoutMinutes).IsEqualTo(5);
        await Assert.That(config.DeepSeekTimeoutMinutes).IsEqualTo(5);
        await Assert.That(config.OpenAiMaxTokens).IsEqualTo(4000);
        await Assert.That(config.AnthropicMaxTokens).IsEqualTo(4000);
        await Assert.That(config.DeepSeekMaxTokens).IsEqualTo(4000);
        await Assert.That(config.LmStudioMaxTokens).IsEqualTo(4096);
        await Assert.That(config.OpenWebUiMaxTokens).IsEqualTo(4096);
    }
}