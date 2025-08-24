using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using TUnit.Assertions;
using TUnit.Core;
using HlpAI.Models;

namespace HlpAI.Tests.Services;

public class SqliteConfigurationServiceTests
{
    private string _testDirectory = null!;
    private ILogger<SqliteConfigurationServiceTests> _logger = null!;
    private string _originalUserProfile = null!;

    [Before(Test)]
    public void Setup()
    {
        // Create a unique test directory for each test to avoid database conflicts
        _testDirectory = FileTestHelper.CreateTempDirectory($"sqlite_config_tests_{Guid.NewGuid().ToString("N")[..8]}");
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<SqliteConfigurationServiceTests>();
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
    }

    [After(Test)]
    public void Cleanup()
    {
        // Restore original user profile
        Environment.SetEnvironmentVariable("USERPROFILE", _originalUserProfile);
        FileTestHelper.SafeDeleteDirectory(_testDirectory);
    }

    /// <summary>
    /// Creates a test-isolated SqliteConfigurationService using a unique database path
    /// </summary>
    private SqliteConfigurationService CreateTestService()
    {
        var testDbPath = Path.Combine(_testDirectory, $"test_{Guid.NewGuid():N}.db");
        return new SqliteConfigurationService(testDbPath, _logger);
    }

    /// <summary>
    /// Creates a test-isolated SqliteConfigurationService using a shared database path
    /// </summary>
    private SqliteConfigurationService CreateSharedTestService()
    {
        var testDbPath = Path.Combine(_testDirectory, "shared_config.db");
        return new SqliteConfigurationService(testDbPath, _logger);
    }

    [Test]
    public async Task Constructor_CreatesConfigurationDirectoryAndDatabase()
    {
        // Arrange
        var hlpAiDir = Path.Combine(_testDirectory, ".hlpai");
        var dbPath = Path.Combine(hlpAiDir, "config.db");

        // Act
        using var service = new SqliteConfigurationService(dbPath, _logger);

        // Assert
        await Assert.That(Directory.Exists(hlpAiDir)).IsTrue();
        await Assert.That(File.Exists(dbPath)).IsTrue();
        await Assert.That(service.DatabasePath).IsEqualTo(dbPath);
    }

    [Test]
    public async Task SetConfigurationAsync_WithValidData_ReturnsTrue()
    {
        // Arrange
        using var service = CreateTestService();

        // Act
        var result = await service.SetConfigurationAsync("test_key", "test_value", "test_category");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GetConfigurationAsync_AfterSet_ReturnsCorrectValue()
    {
        // Arrange
        using var service = CreateTestService();
        const string key = "database_path";
        const string value = "/path/to/database";
        const string category = "database";

        // Act
        await service.SetConfigurationAsync(key, value, category);
        var result = await service.GetConfigurationAsync(key, category);

        // Assert
        await Assert.That(result).IsEqualTo(value);
    }

    [Test]
    public async Task GetConfigurationAsync_WithNonExistentKey_ReturnsDefault()
    {
        // Arrange
        using var service = CreateTestService();
        const string defaultValue = "default_value";

        // Act
        var result = await service.GetConfigurationAsync("nonexistent_key", "general", defaultValue);

        // Assert
        await Assert.That(result).IsEqualTo(defaultValue);
    }

    [Test]
    public async Task SetConfigurationAsync_UpdatesExistingValue()
    {
        // Arrange
        using var service = CreateTestService();
        const string key = "update_test";
        const string originalValue = "original";
        const string updatedValue = "updated";

        // Act
        await service.SetConfigurationAsync(key, originalValue);
        await service.SetConfigurationAsync(key, updatedValue);
        var result = await service.GetConfigurationAsync(key);

        // Assert
        await Assert.That(result).IsEqualTo(updatedValue);
    }

    [Test]
    public async Task SetConfigurationBoolAsync_AndGetConfigurationBoolAsync_WorkCorrectly()
    {
        // Arrange
        using var service = CreateTestService();
        const string key = "bool_test";

        // Act & Assert - True
        await service.SetConfigurationBoolAsync(key, true);
        var trueResult = await service.GetConfigurationBoolAsync(key);
        await Assert.That(trueResult).IsTrue();

        // Act & Assert - False
        await service.SetConfigurationBoolAsync(key, false);
        var falseResult = await service.GetConfigurationBoolAsync(key);
        await Assert.That(falseResult).IsFalse();
    }

    [Test]
    public async Task GetConfigurationBoolAsync_WithNonExistentKey_ReturnsDefault()
    {
        // Arrange
        using var service = CreateTestService();

        // Act
        var result = await service.GetConfigurationBoolAsync("nonexistent_bool", "general", true);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task RemoveConfigurationAsync_RemovesExistingKey()
    {
        // Arrange
        using var service = CreateTestService();
        const string key = "remove_test";
        const string value = "to_be_removed";

        // Act
        await service.SetConfigurationAsync(key, value);
        var removeResult = await service.RemoveConfigurationAsync(key);
        var getResult = await service.GetConfigurationAsync(key);

        // Assert
        await Assert.That(removeResult).IsTrue();
        await Assert.That(getResult).IsNull();
    }

    [Test]
    public async Task RemoveConfigurationAsync_WithNonExistentKey_ReturnsFalse()
    {
        // Arrange
        using var service = CreateTestService();

        // Act
        var result = await service.RemoveConfigurationAsync("nonexistent_key");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task GetCategoryConfigurationAsync_ReturnsAllKeysInCategory()
    {
        // Arrange
        using var service = CreateTestService();
        const string category = "test_category";

        // Act
        await service.SetConfigurationAsync("key1", "value1", category);
        await service.SetConfigurationAsync("key2", "value2", category);
        await service.SetConfigurationAsync("key3", "value3", "other_category");

        var result = await service.GetCategoryConfigurationAsync(category);

        // Assert
        await Assert.That(result).HasCount().EqualTo(2);
        await Assert.That(result.ContainsKey("key1")).IsTrue();
        await Assert.That(result.ContainsKey("key2")).IsTrue();
        await Assert.That(result.ContainsKey("key3")).IsFalse();
        await Assert.That(result["key1"]).IsEqualTo("value1");
        await Assert.That(result["key2"]).IsEqualTo("value2");
    }

    [Test]
    public async Task ClearCategoryAsync_RemovesAllKeysInCategory()
    {
        // Arrange
        using var service = CreateTestService();
        const string category = "clear_test";

        // Act
        await service.SetConfigurationAsync("key1", "value1", category);
        await service.SetConfigurationAsync("key2", "value2", category);
        await service.SetConfigurationAsync("key3", "value3", "other_category");

        var deletedCount = await service.ClearCategoryAsync(category);
        var categoryConfig = await service.GetCategoryConfigurationAsync(category);
        var otherCategoryConfig = await service.GetCategoryConfigurationAsync("other_category");

        // Assert
        await Assert.That(deletedCount).IsEqualTo(2);
        await Assert.That(categoryConfig).IsEmpty();
        await Assert.That(otherCategoryConfig).HasCount().EqualTo(1);
    }

    [Test]
    public async Task GetStatsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        using var service = CreateSharedTestService();

        // Act
        await service.SetConfigurationAsync("key1", "value1", "category1");
        await service.SetConfigurationAsync("key2", "value2", "category1");
        await service.SetConfigurationAsync("key3", "value3", "category2");

        var stats = await service.GetStatsAsync();

        // Assert
        await Assert.That(stats.TotalItems).IsEqualTo(3);
        await Assert.That(stats.TotalCategories).IsEqualTo(2);
        await Assert.That(stats.LastUpdate).IsNotNull();
        await Assert.That(stats.DatabasePath).IsNotNull();
        await Assert.That(stats.DatabasePath).EndsWith("shared_config.db");
    }

    [Test]
    public async Task SetConfigurationAsync_WithNullValue_Succeeds()
    {
        // Arrange
        using var service = CreateTestService();
        const string key = "null_test";

        // Act
        var result = await service.SetConfigurationAsync(key, null);
        var retrievedValue = await service.GetConfigurationAsync(key);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(retrievedValue).IsNull();
    }

    [Test]
    public async Task Configuration_WithSpecialCharacters_HandledCorrectly()
    {
        // Arrange
        using var service = CreateTestService();
        const string key = "special_chars";
        const string value = "Value with 'quotes' and \"double quotes\" and \n newlines";

        // Act
        await service.SetConfigurationAsync(key, value);
        var result = await service.GetConfigurationAsync(key);

        // Assert
        await Assert.That(result).IsEqualTo(value);
    }

    [Test]
    public async Task MultipleServices_AccessSameDatabase_WorkCorrectly()
    {
        // Arrange & Act
        using var service1 = CreateSharedTestService();
        await service1.SetConfigurationAsync("shared_key", "shared_value");

        using var service2 = CreateSharedTestService();
        var result = await service2.GetConfigurationAsync("shared_key");

        // Assert
        await Assert.That(result).IsEqualTo("shared_value");
    }

    [Test]
    public async Task Service_WithNullLogger_WorksCorrectly()
    {
        // Arrange & Act
        using var service = new SqliteConfigurationService(null);
        
        var setResult = await service.SetConfigurationAsync("null_logger_test", "test_value");
        var getResult = await service.GetConfigurationAsync("null_logger_test");

        // Assert
        await Assert.That(setResult).IsTrue();
        await Assert.That(getResult).IsEqualTo("test_value");
    }

    [Test]
    public async Task Dispose_MultipleCalls_HandledGracefully()
    {
        // Arrange
        var service = CreateTestService();
        await service.SetConfigurationAsync("dispose_test", "value");

        // Act & Assert - Should not throw
        service.Dispose();
        service.Dispose(); // Second call should be handled gracefully
        
        // Test passes if no exception thrown
    }

    [Test]
    public async Task SetGetConfiguration_WithEmptyStrings_WorksCorrectly()
    {
        // Arrange
        using var service = CreateTestService();

        // Act
        var setResult = await service.SetConfigurationAsync("empty_key", "");
        var getResult = await service.GetConfigurationAsync("empty_key");

        // Assert
        await Assert.That(setResult).IsTrue();
        await Assert.That(getResult).IsEqualTo("");
    }

    [Test]
    public async Task SetConfigurationAsync_WithNullKeyOrCategory_ThrowsException()
    {
        // Arrange
        using var service = CreateTestService();

        // Act & Assert
        await Assert.That(async () => await service.SetConfigurationAsync(null!, "value"))
            .Throws<ArgumentNullException>();
            
        await Assert.That(async () => await service.SetConfigurationAsync("key", "value", null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task GetConfigurationAsync_WithNullKeyOrCategory_ThrowsException()
    {
        // Arrange
        using var service = CreateTestService();

        // Act & Assert
        await Assert.That(async () => await service.GetConfigurationAsync(null!))
            .Throws<ArgumentNullException>();
            
        await Assert.That(async () => await service.GetConfigurationAsync("key", null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ConfigurationValues_PersistAfterServiceRestart()
    {
        // Arrange
        const string key = "persistence_test";
        const string value = "persistent_value";

        // Act
        using (var service1 = CreateSharedTestService())
        {
            await service1.SetConfigurationAsync(key, value);
        }

        using var service2 = CreateSharedTestService();
        var result = await service2.GetConfigurationAsync(key);

        // Assert
        await Assert.That(result).IsEqualTo(value);
    }

    [Test]
    public async Task ConcurrentAccess_HandledCorrectly()
    {
        // Arrange
        using var service = CreateTestService();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await service.SetConfigurationAsync($"concurrent_key_{index}", $"value_{index}");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        for (int i = 0; i < 10; i++)
        {
            var result = await service.GetConfigurationAsync($"concurrent_key_{i}");
            await Assert.That(result).IsEqualTo($"value_{i}");
        }
    }

    #region AI Provider Configuration Tests

    [Test]
    public async Task SetAiProviderConfigurationAsync_WithValidData_ReturnsTrue()
    {
        // Arrange
        using var service = CreateTestService();
        const AiProviderType providerType = AiProviderType.Ollama;
        const string model = "llama2";

        // Act
        var result = await service.SetAiProviderConfigurationAsync(providerType, model);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GetAiProviderConfigurationAsync_AfterSet_ReturnsCorrectValues()
    {
        // Arrange
        using var service = CreateTestService();
        const AiProviderType expectedProvider = AiProviderType.LmStudio;
        const string expectedModel = "codellama:7b";

        // Act
        await service.SetAiProviderConfigurationAsync(expectedProvider, expectedModel);
        var result = await service.GetAiProviderConfigurationAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.ProviderType).IsEqualTo(expectedProvider);
        await Assert.That(result!.Value.Model).IsEqualTo(expectedModel);
    }

    [Test]
    public async Task GetAiProviderConfigurationAsync_WhenNotSet_ReturnsNull()
    {
        // Arrange
        using var service = CreateTestService();

        // Act
        var result = await service.GetAiProviderConfigurationAsync();

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task SetAiProviderConfigurationAsync_UpdatesExistingConfiguration()
    {
        // Arrange
        using var service = CreateTestService();
        const AiProviderType originalProvider = AiProviderType.Ollama;
        const string originalModel = "llama2";
        const AiProviderType updatedProvider = AiProviderType.OpenWebUi;
        const string updatedModel = "mistral:7b";

        // Act
        await service.SetAiProviderConfigurationAsync(originalProvider, originalModel);
        await service.SetAiProviderConfigurationAsync(updatedProvider, updatedModel);
        var result = await service.GetAiProviderConfigurationAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.ProviderType).IsEqualTo(updatedProvider);
        await Assert.That(result!.Value.Model).IsEqualTo(updatedModel);
    }

    [Test]
    public async Task ClearAiProviderConfigurationAsync_RemovesConfiguration()
    {
        // Arrange
        using var service = CreateTestService();
        const AiProviderType providerType = AiProviderType.Ollama;
        const string model = "llama2";

        // Act
        await service.SetAiProviderConfigurationAsync(providerType, model);
        var clearResult = await service.ClearAiProviderConfigurationAsync();
        var getResult = await service.GetAiProviderConfigurationAsync();

        // Assert
        await Assert.That(clearResult).IsTrue();
        await Assert.That(getResult).IsNull();
    }

    [Test]
    public async Task ClearAiProviderConfigurationAsync_WhenNotSet_ReturnsTrue()
    {
        // Arrange
        using var service = CreateTestService();

        // Act
        var result = await service.ClearAiProviderConfigurationAsync();

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task SetAiProviderConfigurationAsync_WithAllProviderTypes_WorksCorrectly()
    {
        // Arrange
        using var service = CreateTestService();
        var testCases = new[]
        {
            (AiProviderType.Ollama, "llama2:7b"),
            (AiProviderType.LmStudio, "codellama:13b"),
            (AiProviderType.OpenWebUi, "mistral:latest")
        };

        foreach (var (providerType, model) in testCases)
        {
            // Act
            var setResult = await service.SetAiProviderConfigurationAsync(providerType, model);
            var getResult = await service.GetAiProviderConfigurationAsync();

            // Assert
            await Assert.That(setResult).IsTrue();
            await Assert.That(getResult).IsNotNull();
            await Assert.That(getResult!.Value.ProviderType).IsEqualTo(providerType);
            await Assert.That(getResult!.Value.Model).IsEqualTo(model);
        }
    }

    [Test]
    public async Task SetAiProviderConfigurationAsync_WithSpecialCharactersInModel_WorksCorrectly()
    {
        // Arrange
        using var service = CreateTestService();
        const AiProviderType providerType = AiProviderType.Ollama;
        const string modelWithSpecialChars = "model-name_v2.1:latest";

        // Act
        var setResult = await service.SetAiProviderConfigurationAsync(providerType, modelWithSpecialChars);
        var getResult = await service.GetAiProviderConfigurationAsync();

        // Assert
        await Assert.That(setResult).IsTrue();
        await Assert.That(getResult).IsNotNull();
        await Assert.That(getResult!.Value.Model).IsEqualTo(modelWithSpecialChars);
    }

    [Test]
    public async Task AiProviderConfiguration_PersistsAfterServiceRestart()
    {
        // Arrange
        const AiProviderType providerType = AiProviderType.LmStudio;
        const string model = "persistent-model";

        // Act
        using (var service1 = CreateSharedTestService())
        {
            await service1.SetAiProviderConfigurationAsync(providerType, model);
        }

        using var service2 = CreateSharedTestService();
        var result = await service2.GetAiProviderConfigurationAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.ProviderType).IsEqualTo(providerType);
        await Assert.That(result!.Value.Model).IsEqualTo(model);
    }

    [Test]
    public async Task GetAiProviderConfigurationTimestampAsync_AfterSet_ReturnsRecentTimestamp()
    {
        // Arrange
        using var service = CreateTestService();
        const AiProviderType providerType = AiProviderType.Ollama;
        const string model = "test-model";
        var beforeSet = DateTime.UtcNow;

        // Act
        await service.SetAiProviderConfigurationAsync(providerType, model);
        var timestamp = await service.GetAiProviderConfigurationTimestampAsync();
        var afterSet = DateTime.UtcNow;

        // Assert
        await Assert.That(timestamp).IsNotNull();
        await Assert.That(timestamp!.Value).IsGreaterThanOrEqualTo(beforeSet.AddSeconds(-1)); // Allow 1 second tolerance
        await Assert.That(timestamp!.Value).IsLessThanOrEqualTo(afterSet.AddSeconds(1)); // Allow 1 second tolerance
    }

    [Test]
    public async Task GetAiProviderConfigurationTimestampAsync_WhenNotSet_ReturnsNull()
    {
        // Arrange
        using var service = CreateTestService();

        // Act
        var timestamp = await service.GetAiProviderConfigurationTimestampAsync();

        // Assert
        await Assert.That(timestamp).IsNull();
    }

    #endregion
}
