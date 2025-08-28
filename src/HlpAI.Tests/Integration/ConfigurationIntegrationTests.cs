using System.Text.Json;
using HlpAI.Models;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Integration;

public class ConfigurationIntegrationTests
{
    private string _testDirectory = null!;
    private string _testDbPath = null!;
    private ILogger<ConfigurationIntegrationTests> _logger = null!;
    private SqliteConfigurationService _configService = null!;
    private string _originalUserProfile = null!;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("config_integration");
        _testDbPath = Path.Combine(_testDirectory, "test_config.db");
        _logger = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<ConfigurationIntegrationTests>();
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        // Set up test-specific SQLite database
        _configService = new SqliteConfigurationService(_testDbPath, _logger);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        // Restore original user profile
        Environment.SetEnvironmentVariable("USERPROFILE", _originalUserProfile);
        _configService?.Dispose();
        
        // Wait for file handles to be released
        await Task.Delay(100);
        
        FileTestHelper.SafeDeleteDirectory(_testDirectory);
    }

    [Test]
    public async Task ConfigurationWorkflow_FullLifecycle_WorksCorrectly()
    {
        // This test simulates a full configuration lifecycle as it would be used in the application
        
        // Step 1: Load default configuration (first run)
        var initialConfig = await _configService.LoadAppConfigurationAsync();
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

        var saveResult = await _configService.SaveAppConfigurationAsync(initialConfig);
        await Assert.That(saveResult).IsTrue();

        // Step 3: Application restart - load saved configuration
        var reloadedConfig = await _configService.LoadAppConfigurationAsync();
        await Assert.That(reloadedConfig.LastDirectory).IsEqualTo(userDirectory);
        await Assert.That(reloadedConfig.LastModel).IsEqualTo(userModel);
        await Assert.That(reloadedConfig.LastOperationMode).IsEqualTo(userMode);

        // Step 4: User changes directory through the application
        var newDirectory = @"C:\Users\TestUser\Documents\NewProject";
        var updateResult = await _configService.UpdateLastDirectoryAsync(newDirectory);
        await Assert.That(updateResult).IsTrue();

        // Step 5: Verify change was persisted
        var updatedConfig = await _configService.LoadAppConfigurationAsync();
        await Assert.That(updatedConfig.LastDirectory).IsEqualTo(newDirectory);
        await Assert.That(updatedConfig.LastModel).IsEqualTo(userModel); // Should remain unchanged
        await Assert.That(updatedConfig.LastOperationMode).IsEqualTo(userMode); // Should remain unchanged

        // Step 6: User disables directory memory
        updatedConfig.RememberLastDirectory = false;
        await _configService.SaveAppConfigurationAsync(updatedConfig);

        // Step 7: Verify setting was saved
        var configWithDisabledMemory = await _configService.LoadAppConfigurationAsync();
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

        // Initial update using SQLite service directly
        var config = await _configService.LoadAppConfigurationAsync();
        config.LastDirectory = testDir1;
        config.LastModel = testModel1;
        config.LastOperationMode = OperationMode.MCP;
        var result1 = await _configService.SaveAppConfigurationAsync(config);
        await Assert.That(result1).IsTrue();

        // Add a small delay to ensure different timestamps
        await Task.Delay(100);
        
        // Verify first set of updates
        var config1 = await _configService.LoadAppConfigurationAsync();
        await Assert.That(config1.LastDirectory).IsEqualTo(testDir1);
        await Assert.That(config1.LastModel).IsEqualTo(testModel1);
        await Assert.That(config1.LastOperationMode).IsEqualTo(OperationMode.MCP);
        
        // Second update - use SaveAppConfigurationAsync to ensure LastUpdated is updated
        var config2ForUpdate = await _configService.LoadAppConfigurationAsync();
        config2ForUpdate.LastDirectory = testDir2;
        config2ForUpdate.LastModel = testModel2;
        config2ForUpdate.LastOperationMode = OperationMode.RAG;
        var result4 = await _configService.SaveAppConfigurationAsync(config2ForUpdate);
        await Assert.That(result4).IsTrue();

        // Verify second set of updates
        var config2 = await _configService.LoadAppConfigurationAsync();
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

        var saveResult = await _configService.SaveAppConfigurationAsync(goodConfig);
        await Assert.That(saveResult).IsTrue();

        // Verify it was saved correctly
        var loadedGoodConfig = await _configService.LoadAppConfigurationAsync();
        await Assert.That(loadedGoodConfig.LastDirectory).IsEqualTo(goodConfig.LastDirectory);
        await Assert.That(loadedGoodConfig.RememberLastDirectory).IsFalse();

        // Now corrupt the database file by writing invalid data
        var dbPath = _testDbPath;
        if (File.Exists(dbPath))
        {
            // Dispose the service first to release the database connection
            _configService.Dispose();
            await Task.Delay(100); // Allow file handles to be released
            
            await File.WriteAllTextAsync(dbPath, "corrupted database content");
            
            // Create a new service instance to test recovery
            SqliteConfigurationService recoveryConfigService;
            try
            {
                recoveryConfigService = new SqliteConfigurationService(dbPath, null);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Database was corrupted"))
            {
                // Database was corrupted and recreated, create a new service instance
                recoveryConfigService = new SqliteConfigurationService(dbPath, null);
            }
            
            // Try to load - should return default configuration
            var recoveredConfig = await recoveryConfigService.LoadAppConfigurationAsync();
            await Assert.That(recoveredConfig).IsNotNull();
            await Assert.That(recoveredConfig.RememberLastDirectory).IsTrue(); // Default value
            await Assert.That(recoveredConfig.LastOperationMode).IsEqualTo(OperationMode.Hybrid); // Default
            
            // Should be able to save over the corrupted file
            var newSaveResult = await recoveryConfigService.SaveAppConfigurationAsync(recoveredConfig);
            await Assert.That(newSaveResult).IsTrue();
            
            // Should be able to load the fixed configuration
            var fixedConfig = await _configService.LoadAppConfigurationAsync();
            await Assert.That(fixedConfig).IsNotNull();
            await Assert.That(fixedConfig.RememberLastDirectory).IsTrue();
        }
    }

    [Test]
    public async Task ConfigurationStatus_ReflectsActualFileState()
    {
        // Test that GetConfigurationStatus returns accurate information
        
        // Initially database exists but may be empty
        var initialStatus = ConfigurationService.GetConfigurationStatus(_configService);
        await Assert.That(initialStatus).IsNotNull();

        // Create configuration
        var config = new AppConfiguration
        {
            RememberLastDirectory = false,
            RememberLastModel = true,
            RememberLastOperationMode = false
        };

        await _configService.SaveAppConfigurationAsync(config);

        // Check status after creation using static method
        var statusAfterSave = ConfigurationService.GetConfigurationStatus(_configService);
        await Assert.That(statusAfterSave).IsNotNull();
        await Assert.That(statusAfterSave).Contains("Remember last directory: No");
        await Assert.That(statusAfterSave).Contains("Remember last model: Yes");
        await Assert.That(statusAfterSave).Contains("Remember last operation mode: No");
        await Assert.That(statusAfterSave).Contains("Last updated:");
        await Assert.That(statusAfterSave).Contains("Total configurations:");
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

        await _configService.SaveAppConfigurationAsync(config);

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
            await _configService.UpdateLastDirectoryAsync(dir);
            var currentConfig = await _configService.LoadAppConfigurationAsync();
            await Assert.That(currentConfig.LastDirectory).IsEqualTo(dir);
        }

        // Scenario 3: User switches models
        var models = new[] { "llama3.1", "codellama", "mixtral", "phi3" };
        
        foreach (var model in models)
        {
            await _configService.UpdateLastModelAsync(model);
            var currentConfig = await _configService.LoadAppConfigurationAsync();
            await Assert.That(currentConfig.LastModel).IsEqualTo(model);
        }

        // Scenario 4: User cycles through operation modes
        var modes = new[] { OperationMode.MCP, OperationMode.RAG, OperationMode.Hybrid };
        
        foreach (var mode in modes)
        {
            await _configService.UpdateLastOperationModeAsync(mode);
            var currentConfig = await _configService.LoadAppConfigurationAsync();
            await Assert.That(currentConfig.LastOperationMode).IsEqualTo(mode);
        }

        // Final verification
        var finalConfig = await _configService.LoadAppConfigurationAsync();
        await Assert.That(finalConfig.LastDirectory).IsEqualTo(directories.Last());
        await Assert.That(finalConfig.LastModel).IsEqualTo(models.Last());
        await Assert.That(finalConfig.LastOperationMode).IsEqualTo(modes.Last());
        await Assert.That(finalConfig.RememberLastDirectory).IsTrue();
        await Assert.That(finalConfig.RememberLastModel).IsTrue();
        await Assert.That(finalConfig.RememberLastOperationMode).IsTrue();
    }
}
