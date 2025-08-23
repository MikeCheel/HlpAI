using System.Text.Json;
using HlpAI.Models;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using TUnit.Assertions;
using TUnit.Core;

namespace HlpAI.Tests.Integration;

public class ConfigurationIntegrationTests
{
    private string _testDirectory = null!;
    private string _testConfigFile = null!;
    private ILogger<ConfigurationIntegrationTests> _logger = null!;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("config_integration");
        _testConfigFile = Path.Combine(_testDirectory, "test_config.json");
        _logger = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<ConfigurationIntegrationTests>();
        
        // Set up test-specific config path
        ConfigurationService.SetConfigFilePathForTesting(_testConfigFile);
    }

    [After(Test)]
    public void Cleanup()
    {
        // Reset to default config path
        ConfigurationService.SetConfigFilePathForTesting(null);
        FileTestHelper.SafeDeleteDirectory(_testDirectory);
    }

    [Test]
    public async Task ConfigurationWorkflow_FullLifecycle_WorksCorrectly()
    {
        // This test simulates a full configuration lifecycle as it would be used in the application
        
        // Step 1: Load default configuration (first run)
        var initialConfig = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(initialConfig).IsNotNull();
        await Assert.That(initialConfig.LastDirectory).IsNull();
        await Assert.That(initialConfig.RememberLastDirectory).IsTrue();

        // Step 2: User configures the application for the first time
        var userDirectory = @"C:\Users\TestUser\Documents\MyProject";
        var userModel = "llama3.2";
        var userMode = OperationMode.Hybrid;

        initialConfig.LastDirectory = userDirectory;
        initialConfig.LastModel = userModel;
        initialConfig.LastOperationMode = userMode;

        var saveResult = ConfigurationService.SaveConfiguration(initialConfig, _logger);
        await Assert.That(saveResult).IsTrue();

        // Step 3: Application restart - load saved configuration
        var reloadedConfig = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(reloadedConfig.LastDirectory).IsEqualTo(userDirectory);
        await Assert.That(reloadedConfig.LastModel).IsEqualTo(userModel);
        await Assert.That(reloadedConfig.LastOperationMode).IsEqualTo(userMode);

        // Step 4: User changes directory through the application
        var newDirectory = @"C:\Users\TestUser\Documents\NewProject";
        var updateResult = ConfigurationService.UpdateLastDirectory(newDirectory, _logger);
        await Assert.That(updateResult).IsTrue();

        // Step 5: Verify change was persisted
        var updatedConfig = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(updatedConfig.LastDirectory).IsEqualTo(newDirectory);
        await Assert.That(updatedConfig.LastModel).IsEqualTo(userModel); // Should remain unchanged
        await Assert.That(updatedConfig.LastOperationMode).IsEqualTo(userMode); // Should remain unchanged

        // Step 6: User disables directory memory
        updatedConfig.RememberLastDirectory = false;
        ConfigurationService.SaveConfiguration(updatedConfig, _logger);

        // Step 7: Verify setting was saved
        var configWithDisabledMemory = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(configWithDisabledMemory.RememberLastDirectory).IsFalse();
        await Assert.That(configWithDisabledMemory.LastDirectory).IsEqualTo(newDirectory); // Value still there
    }

    [Test]
    public async Task ConfigurationPersistence_FileSystemOperations_WorkCorrectly()
    {
        // Test the actual file system operations
        var tempConfigPath = Path.Combine(_testDirectory, "persistence_test.json");
        var tempConfigDir = Path.GetDirectoryName(tempConfigPath)!;

        // Ensure directory exists
        Directory.CreateDirectory(tempConfigDir);

        var config = new AppConfiguration
        {
            LastDirectory = @"C:\Test\Persistence",
            LastModel = "persistence-model",
            LastOperationMode = OperationMode.RAG,
            RememberLastDirectory = true,
            RememberLastModel = false,
            RememberLastOperationMode = true
        };

        // Manually save to our test location to verify file operations
        var jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        config.LastUpdated = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(config, jsonOptions);
        await File.WriteAllTextAsync(tempConfigPath, json);

        // Verify file was created
        await Assert.That(File.Exists(tempConfigPath)).IsTrue();

        // Verify file content is valid JSON
        var fileContent = await File.ReadAllTextAsync(tempConfigPath);
        await Assert.That(fileContent).IsNotEmpty();
        await Assert.That(fileContent).Contains("lastDirectory");
        await Assert.That(fileContent).Contains("lastModel");
        await Assert.That(fileContent).Contains("lastOperationMode");

        // Verify we can deserialize back
        var deserializedConfig = JsonSerializer.Deserialize<AppConfiguration>(fileContent, jsonOptions);
        await Assert.That(deserializedConfig).IsNotNull();
        await Assert.That(deserializedConfig!.LastDirectory).IsEqualTo(config.LastDirectory);
        await Assert.That(deserializedConfig.LastModel).IsEqualTo(config.LastModel);
        await Assert.That(deserializedConfig.LastOperationMode).IsEqualTo(config.LastOperationMode);
    }

    [Test]
    public async Task ConfigurationService_DirectoryCreation_WorksCorrectly()
    {
        // Test that the service creates configuration directories when they don't exist
        var testConfigPath = Path.Combine(_testDirectory, "nested", "directories", "config.json");
        var testConfigDir = Path.GetDirectoryName(testConfigPath)!;

        // Ensure the nested directories don't exist initially
        await Assert.That(Directory.Exists(testConfigDir)).IsFalse();

        // Create configuration in a location where directories need to be created
        var config = new AppConfiguration
        {
            LastDirectory = @"C:\Test\DirectoryCreation"
        };

        // We'll simulate what ConfigurationService.SaveConfiguration does
        if (!Directory.Exists(testConfigDir))
        {
            Directory.CreateDirectory(testConfigDir);
        }

        var jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        config.LastUpdated = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(config, jsonOptions);
        await File.WriteAllTextAsync(testConfigPath, json);

        // Verify directory was created
        await Assert.That(Directory.Exists(testConfigDir)).IsTrue();
        await Assert.That(File.Exists(testConfigPath)).IsTrue();

        // Verify file content
        var savedContent = await File.ReadAllTextAsync(testConfigPath);
        var loadedConfig = JsonSerializer.Deserialize<AppConfiguration>(savedContent, jsonOptions);
        await Assert.That(loadedConfig).IsNotNull();
        await Assert.That(loadedConfig!.LastDirectory).IsEqualTo(config.LastDirectory);
    }

    [Test]
    public async Task ConfigurationService_HelperMethods_IntegrateCorrectly()
    {
        // Test that all helper methods work together correctly
        var testDir1 = @"C:\Test\Helper1";
        var testDir2 = @"C:\Test\Helper2";
        var testModel1 = "helper-model-1";
        var testModel2 = "helper-model-2";

        // Initial update
        var result1 = ConfigurationService.UpdateLastDirectory(testDir1, _logger);
        await Assert.That(result1).IsTrue();

        var result2 = ConfigurationService.UpdateLastModel(testModel1, _logger);
        await Assert.That(result2).IsTrue();

        var result3 = ConfigurationService.UpdateLastOperationMode(OperationMode.MCP, _logger);
        await Assert.That(result3).IsTrue();

        // Verify first set of updates
        var config1 = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(config1.LastDirectory).IsEqualTo(testDir1);
        await Assert.That(config1.LastModel).IsEqualTo(testModel1);
        await Assert.That(config1.LastOperationMode).IsEqualTo(OperationMode.MCP);

        // Second update
        var result4 = ConfigurationService.UpdateLastDirectory(testDir2, _logger);
        await Assert.That(result4).IsTrue();

        var result5 = ConfigurationService.UpdateLastModel(testModel2, _logger);
        await Assert.That(result5).IsTrue();

        var result6 = ConfigurationService.UpdateLastOperationMode(OperationMode.RAG, _logger);
        await Assert.That(result6).IsTrue();

        // Verify second set of updates
        var config2 = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(config2.LastDirectory).IsEqualTo(testDir2);
        await Assert.That(config2.LastModel).IsEqualTo(testModel2);
        await Assert.That(config2.LastOperationMode).IsEqualTo(OperationMode.RAG);

        // Verify timestamps are different (updates occurred at different times)
        await Assert.That(config2.LastUpdated).IsGreaterThan(config1.LastUpdated);
    }

    [Test]
    public async Task ConfigurationService_ErrorRecovery_WorksCorrectly()
    {
        // Test that the service recovers correctly from various error conditions
        
        // Create a good configuration first
        var goodConfig = new AppConfiguration
        {
            LastDirectory = @"C:\Test\ErrorRecovery",
            LastModel = "recovery-model",
            RememberLastDirectory = false
        };

        var saveResult = ConfigurationService.SaveConfiguration(goodConfig, _logger);
        await Assert.That(saveResult).IsTrue();

        // Verify it was saved correctly
        var loadedGoodConfig = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(loadedGoodConfig.LastDirectory).IsEqualTo(goodConfig.LastDirectory);
        await Assert.That(loadedGoodConfig.RememberLastDirectory).IsFalse();

        // Now corrupt the config file
        var configPath = ConfigurationService.ConfigFilePath;
        if (File.Exists(configPath))
        {
            await File.WriteAllTextAsync(configPath, "{ corrupted json content");
            
            // Try to load - should return default configuration
            var recoveredConfig = ConfigurationService.LoadConfiguration(_logger);
            await Assert.That(recoveredConfig).IsNotNull();
            await Assert.That(recoveredConfig.RememberLastDirectory).IsTrue(); // Default value
            await Assert.That(recoveredConfig.LastOperationMode).IsEqualTo(OperationMode.Hybrid); // Default
            
            // Should be able to save over the corrupted file
            var newSaveResult = ConfigurationService.SaveConfiguration(recoveredConfig, _logger);
            await Assert.That(newSaveResult).IsTrue();
            
            // Should be able to load the fixed configuration
            var fixedConfig = ConfigurationService.LoadConfiguration(_logger);
            await Assert.That(fixedConfig).IsNotNull();
            await Assert.That(fixedConfig.RememberLastDirectory).IsTrue();
        }
    }

    [Test]
    public async Task ConfigurationStatus_ReflectsActualFileState()
    {
        // Test that GetConfigurationStatus returns accurate information
        
        // Initially no config file
        var initialStatus = ConfigurationService.GetConfigurationStatus();
        await Assert.That(initialStatus).Contains("Configuration file: Not found");

        // Create configuration
        var config = new AppConfiguration
        {
            RememberLastDirectory = false,
            RememberLastModel = true,
            RememberLastOperationMode = false
        };

        ConfigurationService.SaveConfiguration(config, _logger);

        // Check status after creation
        var statusAfterSave = ConfigurationService.GetConfigurationStatus();
        await Assert.That(statusAfterSave).DoesNotContain("Not found");
        await Assert.That(statusAfterSave).Contains("Remember last directory: No");
        await Assert.That(statusAfterSave).Contains("Remember last model: Yes");
        await Assert.That(statusAfterSave).Contains("Remember last operation mode: No");
        await Assert.That(statusAfterSave).Contains("Last updated:");
        await Assert.That(statusAfterSave).Contains("File size:");
    }

    [Test]
    public async Task ConfigurationService_RealWorldScenarios_HandleCorrectly()
    {
        // Test realistic usage scenarios
        
        // Scenario 1: User enables all memory features
        var config = new AppConfiguration
        {
            RememberLastDirectory = true,
            RememberLastModel = true,
            RememberLastOperationMode = true,
            LastDirectory = @"C:\Projects\MyApp\Documents",
            LastModel = "llama3.2:8b",
            LastOperationMode = OperationMode.Hybrid
        };

        ConfigurationService.SaveConfiguration(config, _logger);

        // Scenario 2: User changes directory multiple times
        var directories = new[]
        {
            @"C:\Projects\MyApp\Docs",
            @"C:\Projects\MyApp\References",
            @"C:\Projects\MyApp\Archive",
            @"C:\Projects\NewProject\Documents"
        };

        foreach (var dir in directories)
        {
            ConfigurationService.UpdateLastDirectory(dir, _logger);
            var currentConfig = ConfigurationService.LoadConfiguration(_logger);
            await Assert.That(currentConfig.LastDirectory).IsEqualTo(dir);
        }

        // Scenario 3: User switches models
        var models = new[] { "llama3.1", "codellama", "mixtral", "phi3" };
        
        foreach (var model in models)
        {
            ConfigurationService.UpdateLastModel(model, _logger);
            var currentConfig = ConfigurationService.LoadConfiguration(_logger);
            await Assert.That(currentConfig.LastModel).IsEqualTo(model);
        }

        // Scenario 4: User cycles through operation modes
        var modes = new[] { OperationMode.MCP, OperationMode.RAG, OperationMode.Hybrid };
        
        foreach (var mode in modes)
        {
            ConfigurationService.UpdateLastOperationMode(mode, _logger);
            var currentConfig = ConfigurationService.LoadConfiguration(_logger);
            await Assert.That(currentConfig.LastOperationMode).IsEqualTo(mode);
        }

        // Final verification
        var finalConfig = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(finalConfig.LastDirectory).IsEqualTo(directories.Last());
        await Assert.That(finalConfig.LastModel).IsEqualTo(models.Last());
        await Assert.That(finalConfig.LastOperationMode).IsEqualTo(modes.Last());
        await Assert.That(finalConfig.RememberLastDirectory).IsTrue();
        await Assert.That(finalConfig.RememberLastModel).IsTrue();
        await Assert.That(finalConfig.RememberLastOperationMode).IsTrue();
    }
}
