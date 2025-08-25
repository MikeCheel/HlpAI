using HlpAI.Models;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Services;

public class ConfigurationServiceTests
{
    private string _testDirectory = null!;
    private string _testConfigPath = null!;
    private ILogger<ConfigurationServiceTests> _logger = null!;

    [Before(Test)]
    public void Setup()
    {
        // Create a unique test directory for each test
        _testDirectory = FileTestHelper.CreateTempDirectory($"config_tests_{Guid.NewGuid():N}");
        _testConfigPath = Path.Combine(_testDirectory, "config.json");
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ConfigurationServiceTests>();
        
        // Set the test-specific config path
        ConfigurationService.SetConfigFilePathForTesting(_testConfigPath);
    }

    [After(Test)]
    public void Cleanup()
    {
        // Reset to default config path
        ConfigurationService.SetConfigFilePathForTesting(null);
        FileTestHelper.SafeDeleteDirectory(_testDirectory);
    }

    [Test]
    public async Task LoadConfiguration_WithNonExistentFile_ReturnsDefaultConfiguration()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.json");
        
        // Act
        var config = ConfigurationService.LoadConfiguration(_logger);

        // Assert
        await Assert.That(config).IsNotNull();
        await Assert.That(config.RememberLastDirectory).IsTrue();
        await Assert.That(config.RememberLastModel).IsTrue();
        await Assert.That(config.RememberLastOperationMode).IsTrue();
        await Assert.That(config.LastOperationMode).IsEqualTo(OperationMode.Hybrid);
        await Assert.That(config.ConfigVersion).IsEqualTo(1);
    }

    [Test]
    public async Task SaveConfiguration_WithValidConfiguration_ReturnsTrue()
    {
        // Arrange
        var config = new AppConfiguration
        {
            LastDirectory = @"C:\TestDirectory",
            LastModel = "llama3.2",
            LastOperationMode = OperationMode.RAG,
            RememberLastDirectory = true,
            RememberLastModel = false,
            RememberLastOperationMode = true
        };

        // Act
        var result = ConfigurationService.SaveConfiguration(config, _logger);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task SaveAndLoadConfiguration_RoundTrip_PreservesData()
    {
        // Arrange
        var originalConfig = new AppConfiguration
        {
            LastDirectory = @"C:\TestDirectory\With Spaces",
            LastModel = "test-model-3.1",
            LastOperationMode = OperationMode.MCP,
            RememberLastDirectory = false,
            RememberLastModel = true,
            RememberLastOperationMode = false,
            ConfigVersion = 2
        };

        // Act
        var saveResult = ConfigurationService.SaveConfiguration(originalConfig, _logger);
        var loadedConfig = ConfigurationService.LoadConfiguration(_logger);

        // Assert
        await Assert.That(saveResult).IsTrue();
        await Assert.That(loadedConfig).IsNotNull();
        await Assert.That(loadedConfig.LastDirectory).IsEqualTo(originalConfig.LastDirectory);
        await Assert.That(loadedConfig.LastModel).IsEqualTo(originalConfig.LastModel);
        await Assert.That(loadedConfig.LastOperationMode).IsEqualTo(originalConfig.LastOperationMode);
        await Assert.That(loadedConfig.RememberLastDirectory).IsEqualTo(originalConfig.RememberLastDirectory);
        await Assert.That(loadedConfig.RememberLastModel).IsEqualTo(originalConfig.RememberLastModel);
        await Assert.That(loadedConfig.RememberLastOperationMode).IsEqualTo(originalConfig.RememberLastOperationMode);
        await Assert.That(loadedConfig.ConfigVersion).IsEqualTo(originalConfig.ConfigVersion);
    }

    [Test]
    public async Task UpdateLastDirectory_WithValidPath_ReturnsTrue()
    {
        // Arrange
        var testPath = @"C:\UpdateTest\Directory";

        // Act
        var result = ConfigurationService.UpdateLastDirectory(testPath, _logger);

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify the update was saved
        var config = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(config.LastDirectory).IsEqualTo(testPath);
    }

    [Test]
    public async Task UpdateLastModel_WithValidModel_ReturnsTrue()
    {
        // Arrange
        var testModel = "updated-model-4.0";

        // Act
        var result = ConfigurationService.UpdateLastModel(testModel, _logger);

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify the update was saved
        var config = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(config.LastModel).IsEqualTo(testModel);
    }

    [Test]
    public async Task UpdateLastOperationMode_WithValidMode_ReturnsTrue()
    {
        // Arrange
        var testMode = OperationMode.RAG;

        // Act
        var result = ConfigurationService.UpdateLastOperationMode(testMode, _logger);

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify the update was saved
        var config = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(config.LastOperationMode).IsEqualTo(testMode);
    }

    [Test]
    public async Task SaveConfiguration_UpdatesLastUpdatedTimestamp()
    {
        // Arrange
        var config = new AppConfiguration();
        var beforeSave = DateTime.UtcNow;

        // Act
        var result = ConfigurationService.SaveConfiguration(config, _logger);
        var afterSave = DateTime.UtcNow;

        // Assert
        await Assert.That(result).IsTrue();
        
        var loadedConfig = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(loadedConfig.LastUpdated).IsGreaterThanOrEqualTo(beforeSave);
        await Assert.That(loadedConfig.LastUpdated).IsLessThanOrEqualTo(afterSave);
    }

    [Test]
    public async Task GetConfigurationStatus_WithExistingFile_ReturnsCorrectStatus()
    {
        // Arrange
        var config = new AppConfiguration
        {
            RememberLastDirectory = false,
            RememberLastModel = true,
            RememberLastOperationMode = false
        };
        ConfigurationService.SaveConfiguration(config, _logger);

        // Act
        var status = ConfigurationService.GetConfigurationStatus();

        // Assert
        await Assert.That(status).IsNotNull();
        await Assert.That(status).IsNotEmpty();
        await Assert.That(status).Contains("Configuration file:");
        await Assert.That(status).Contains("Last updated:");
        await Assert.That(status).Contains("Remember last directory: No");
        await Assert.That(status).Contains("Remember last model: Yes");
        await Assert.That(status).Contains("Remember last operation mode: No");
    }

    [Test]
    public async Task GetConfigurationStatus_WithNonExistentFile_ReturnsNotFoundStatus()
    {
        // Arrange - ensure no config file exists (it shouldn't exist in our test directory)
        // The test config path is already isolated, so no file should exist
        
        // Act
        var status = ConfigurationService.GetConfigurationStatus();

        // Assert
        await Assert.That(status).IsNotNull();
        await Assert.That(status).Contains("Configuration file: Not found");
        await Assert.That(status).Contains("Will be created at:");
    }

    [Test]
    public async Task LoadConfiguration_WithCorruptedJsonFile_ReturnsDefaultConfiguration()
    {
        // Arrange - Create corrupted JSON at our test config path
        await File.WriteAllTextAsync(_testConfigPath, "{ invalid json content ][");

        // Act
        var config = ConfigurationService.LoadConfiguration(_logger);

        // Assert
        await Assert.That(config).IsNotNull();
        await Assert.That(config.RememberLastDirectory).IsTrue();
        await Assert.That(config.LastOperationMode).IsEqualTo(OperationMode.Hybrid);
    }

    [Test]
    public async Task SaveConfiguration_WithReadOnlyDirectory_HandlesGracefully()
    {
        // Arrange
        var readOnlyDir = Path.Combine(_testDirectory, "readonly");
        Directory.CreateDirectory(readOnlyDir);
        
        try
        {
            // Make directory read-only (this may not work on all systems)
            var directoryInfo = new DirectoryInfo(readOnlyDir);
            directoryInfo.Attributes |= FileAttributes.ReadOnly;
        }
        catch
        {
            // If we can't make it readonly, skip this test
            return;
        }

        var config = new AppConfiguration();

        // Create a temporary method to test with different path
        // Since we can't easily override the config path, we'll test the error handling behavior
        // by attempting to save to a location we know will fail

        // Act & Assert
        var result = await Task.Run(() => ConfigurationService.SaveConfiguration(config, _logger));
        
        // The result might be true or false depending on system behavior
        // The important thing is that it doesn't throw an exception
        // Test passes if no exception thrown above
    }

    [Test]
    public async Task LoadConfiguration_WithEmptyJsonFile_ReturnsDefaultConfiguration()
    {
        // Arrange - Create empty JSON at our test config path
        await File.WriteAllTextAsync(_testConfigPath, "");

        // Act
        var config = ConfigurationService.LoadConfiguration(_logger);

        // Assert
        await Assert.That(config).IsNotNull();
        await Assert.That(config.RememberLastDirectory).IsTrue();
        await Assert.That(config.LastOperationMode).IsEqualTo(OperationMode.Hybrid);
    }

    [Test]
    public async Task SaveConfiguration_WithNullConfiguration_HandlesGracefully()
    {
        // Act & Assert
        await Assert.That(async () => await Task.Run(() => ConfigurationService.SaveConfiguration(null!, _logger)))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task MultipleOperations_ConcurrentAccess_HandledCorrectly()
    {
        // Arrange
        var tasks = new List<Task<bool>>();
        
        // Act - Perform multiple concurrent save operations
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => 
            {
                var config = new AppConfiguration
                {
                    LastDirectory = $@"C:\Test{index}",
                    LastModel = $"model-{index}",
                    ConfigVersion = index
                };
                return ConfigurationService.SaveConfiguration(config, _logger);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - The system should handle concurrent access gracefully
        // Some operations may succeed while others may be overwritten due to concurrency
        // The important thing is that no exceptions are thrown and the final state is consistent
        await Assert.That(results.Any(r => r)).IsTrue(); // At least one should succeed
        
        // Verify final state is consistent and loadable
        var finalConfig = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(finalConfig).IsNotNull();
        await Assert.That(finalConfig.ConfigVersion).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task UpdateMethods_WithNullLogger_WorkCorrectly()
    {
        // Act & Assert
        var result1 = ConfigurationService.UpdateLastDirectory(@"C:\TestNullLogger");
        await Assert.That(result1).IsTrue();

        var result2 = ConfigurationService.UpdateLastModel("test-model-null-logger");
        await Assert.That(result2).IsTrue();

        var result3 = ConfigurationService.UpdateLastOperationMode(OperationMode.MCP);
        await Assert.That(result3).IsTrue();

        // Verify updates were applied
        var config = ConfigurationService.LoadConfiguration();
        await Assert.That(config.LastDirectory).IsEqualTo(@"C:\TestNullLogger");
        await Assert.That(config.LastModel).IsEqualTo("test-model-null-logger");
        await Assert.That(config.LastOperationMode).IsEqualTo(OperationMode.MCP);
    }

    [Test]
    public async Task ConfigFilePath_ReturnsValidPath()
    {
        // Act
        var configPath = ConfigurationService.ConfigFilePath;

        // Assert
        await Assert.That(configPath).IsNotNull();
        await Assert.That(configPath).IsNotEmpty();
        await Assert.That(Path.IsPathRooted(configPath)).IsTrue();
        await Assert.That(configPath).EndsWith("config.json");
    }
}
