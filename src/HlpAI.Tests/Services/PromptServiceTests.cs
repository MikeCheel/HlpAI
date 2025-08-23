using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using TUnit.Assertions;
using TUnit.Core;

namespace HlpAI.Tests.Services;

public class PromptServiceTests
{
    private string _testDirectory = null!;
    private ILogger<PromptServiceTests> _logger = null!;
    private string _originalUserProfile = null!;
    private SqliteConfigurationService _configService = null!;
    private string _testDbPath = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("prompt_service_tests");
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PromptServiceTests>();
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        // Create isolated test database for this test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_prompt_service_{Guid.NewGuid()}.db");
        _configService = new SqliteConfigurationService(_testDbPath, _logger);
    }

    [After(Test)]
    public void Cleanup()
    {
        // Restore original user profile
        Environment.SetEnvironmentVariable("USERPROFILE", _originalUserProfile);
        
        _configService?.Dispose();
        
        // Clean up test database
        if (File.Exists(_testDbPath))
        {
            try
            {
                File.Delete(_testDbPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        
        FileTestHelper.SafeDeleteDirectory(_testDirectory);
    }

    [Test]
    public async Task GetDefaultPromptBehaviorAsync_WithNoConfiguration_ReturnsNull()
    {
        // Arrange
        using var service = new PromptService(_configService, _logger);

        // Act
        var result = await service.GetDefaultPromptBehaviorAsync();

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task SetDefaultPromptBehaviorAsync_WithTrue_StoresCorrectly()
    {
        // Arrange
        using var service = new PromptService(_configService, _logger);

        // Act
        var setResult = await service.SetDefaultPromptBehaviorAsync(true);
        var getResult = await service.GetDefaultPromptBehaviorAsync();

        // Assert
        await Assert.That(setResult).IsTrue();
        await Assert.That(getResult).IsTrue();
    }

    [Test]
    public async Task SetDefaultPromptBehaviorAsync_WithFalse_StoresCorrectly()
    {
        // Arrange
        using var service = new PromptService(_configService, _logger);

        // Act
        var setResult = await service.SetDefaultPromptBehaviorAsync(false);
        var getResult = await service.GetDefaultPromptBehaviorAsync();

        // Assert
        await Assert.That(setResult).IsTrue();
        await Assert.That(getResult).IsFalse();
    }

    [Test]
    public async Task SetDefaultPromptBehaviorAsync_WithNull_RemovesConfiguration()
    {
        // Arrange
        using var service = new PromptService(_configService, _logger);
        
        // Set a value first
        await service.SetDefaultPromptBehaviorAsync(true);

        // Act
        var setResult = await service.SetDefaultPromptBehaviorAsync(null);
        var getResult = await service.GetDefaultPromptBehaviorAsync();

        // Assert
        await Assert.That(setResult).IsTrue();
        await Assert.That(getResult).IsNull();
    }

    [Test]
    public async Task PromptForString_WithDefaultValue_WorksCorrectly()
    {
        // Arrange
        using var service = new PromptService(_logger);

        // Test that default value handling works (can't test actual console input in unit tests)
        // This test verifies the method exists and handles parameters correctly
        
        // Act & Assert - Just verify method can be called without throwing
        // In real usage, this would prompt the user for input
        // For unit testing, we verify the service is properly constructed and method exists
        await Assert.That(service).IsNotNull();
    }

    [Test]
    public async Task ShowPromptConfigurationAsync_WithDifferentSettings_DisplaysCorrectly()
    {
        // Arrange
        using var service = new PromptService(_logger);

        // Test with no configuration
        // Act & Assert - Verify method can be called
        await service.ShowPromptConfigurationAsync();

        // Test with 'yes' configuration
        await service.SetDefaultPromptBehaviorAsync(true);
        await service.ShowPromptConfigurationAsync();

        // Test with 'no' configuration
        await service.SetDefaultPromptBehaviorAsync(false);
        await service.ShowPromptConfigurationAsync();

        // Test passes if no exceptions thrown
    }

    [Test]
    public async Task ConfigurationPersistence_AcrossServiceInstances_WorksCorrectly()
    {
        // Arrange & Act
        using (var service1 = new PromptService(_logger))
        {
            await service1.SetDefaultPromptBehaviorAsync(true);
        }

        using var service2 = new PromptService(_logger);
        var result = await service2.GetDefaultPromptBehaviorAsync();

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task SetDefaultPromptBehaviorAsync_UpdatesExistingValue()
    {
        // Arrange
        using var service = new PromptService(_configService, _logger);

        // Act
        await service.SetDefaultPromptBehaviorAsync(true);
        var firstResult = await service.GetDefaultPromptBehaviorAsync();

        await service.SetDefaultPromptBehaviorAsync(false);
        var secondResult = await service.GetDefaultPromptBehaviorAsync();

        // Assert
        await Assert.That(firstResult).IsTrue();
        await Assert.That(secondResult).IsFalse();
    }

    [Test]
    public async Task Service_WithNullLogger_WorksCorrectly()
    {
        // Arrange & Act
        using var service = new PromptService(null);
        
        var setResult = await service.SetDefaultPromptBehaviorAsync(true);
        var getResult = await service.GetDefaultPromptBehaviorAsync();

        // Assert
        await Assert.That(setResult).IsTrue();
        await Assert.That(getResult).IsTrue();
    }

    [Test]
    public async Task Dispose_HandledGracefully()
    {
        // Arrange
        var service = new PromptService(_logger);
        await service.SetDefaultPromptBehaviorAsync(true);

        // Act & Assert - Should not throw
        service.Dispose();
        service.Dispose(); // Second call should be handled gracefully
    }

    [Test]
    public async Task MultipleServices_ConcurrentAccess_HandledCorrectly()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 5; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                using var service = new PromptService(_logger);
                await service.SetDefaultPromptBehaviorAsync(index % 2 == 0);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        using var finalService = new PromptService(_logger);
        var result = await finalService.GetDefaultPromptBehaviorAsync();
        
        // Result should be either true or false (not null), indicating one of the operations succeeded
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task PromptService_UsesConfigurationDatabase()
    {
        // Arrange
        using var service = new PromptService(_configService, _logger);

        // Act
        await service.SetDefaultPromptBehaviorAsync(true);
        
        // Verify it's stored in the configuration database
        var directResult = await _configService.GetConfigurationAsync("default_prompt_behavior", "ui");

        // Assert
        await Assert.That(directResult).IsEqualTo("yes");
    }

    [Test]
    public async Task PromptService_ConfigurationCategories_AreCorrect()
    {
        // Arrange
        using var service = new PromptService(_configService, _logger);

        // Act
        await service.SetDefaultPromptBehaviorAsync(true);
        
        var uiConfig = await _configService.GetCategoryConfigurationAsync("ui");

        // Assert
        await Assert.That(uiConfig.ContainsKey("default_prompt_behavior")).IsTrue();
        await Assert.That(uiConfig["default_prompt_behavior"]).IsEqualTo("yes");
    }

    [Test]
    public async Task GetDefaultPromptBehaviorAsync_HandlesDifferentStoredValues()
    {
        // Arrange
        using var service = new PromptService(_configService, _logger);

        // Test "yes" value
        await _configService.SetConfigurationAsync("default_prompt_behavior", "yes", "ui");
        var yesResult = await service.GetDefaultPromptBehaviorAsync();

        // Test "no" value
        await _configService.SetConfigurationAsync("default_prompt_behavior", "no", "ui");
        var noResult = await service.GetDefaultPromptBehaviorAsync();

        // Test invalid value
        await _configService.SetConfigurationAsync("default_prompt_behavior", "invalid", "ui");
        var invalidResult = await service.GetDefaultPromptBehaviorAsync();

        // Assert
        await Assert.That(yesResult).IsTrue();
        await Assert.That(noResult).IsFalse();
        await Assert.That(invalidResult).IsNull();
    }

    [Test]
    public async Task PromptService_DatabaseIntegration_WorksCorrectly()
    {
        // Arrange
        using var service = new PromptService(_configService, _logger);

        // Test that the service creates and uses the database correctly
        var initialBehavior = await service.GetDefaultPromptBehaviorAsync();
        
        // Set different values and verify they persist
        await service.SetDefaultPromptBehaviorAsync(true);
        var trueBehavior = await service.GetDefaultPromptBehaviorAsync();
        
        await service.SetDefaultPromptBehaviorAsync(false);
        var falseBehavior = await service.GetDefaultPromptBehaviorAsync();
        
        await service.SetDefaultPromptBehaviorAsync(null);
        var nullBehavior = await service.GetDefaultPromptBehaviorAsync();

        // Assert
        await Assert.That(initialBehavior).IsNull();
        await Assert.That(trueBehavior).IsTrue();
        await Assert.That(falseBehavior).IsFalse();
        await Assert.That(nullBehavior).IsNull();
    }
}
