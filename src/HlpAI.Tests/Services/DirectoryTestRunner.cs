using HlpAI.Services;
using Microsoft.Extensions.Logging;
using TUnit.Core;

namespace HlpAI.Tests.Services;

public class DirectoryTestRunner
{
    private string? _testDirectory;
    private string? _testDbPath;
    private ILogger? _logger;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "HlpAI_DirectoryTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);
        _testDbPath = Path.Combine(_testDirectory, "test_config.db");
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("DirectoryTest");
    }

    [After(Test)]
    public void Cleanup()
    {
        try
        {
            // Release any singleton instances
            SqliteConfigurationService.ReleaseInstance();
            
            // Clear SQLite connection pools
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            
            // Clean up test directory
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cleanup error: {ex.Message}");
        }
    }

    [Test]
    public async Task TestDirectoryRememberingSimple()
    {
        Console.WriteLine("=== Testing Directory Remembering Functionality ===");
        
        // Test setting a directory
        var testDirectory = @"C:\Users\mikec\Desktop\ChmData";
        Console.WriteLine($"Setting test directory: {testDirectory}");
        
        // Set up isolated test instance
        var configService = SqliteConfigurationService.SetTestInstance(_testDbPath!, _logger);
        var result = await configService.UpdateLastDirectoryAsync(testDirectory);
        Console.WriteLine($"UpdateLastDirectory result: {result}");
        
        // Clear cache and reload using the same test instance
        ConfigurationService.ClearCache();
        var config1 = await configService.LoadAppConfigurationAsync();
        Console.WriteLine($"LoadAppConfigurationAsync - LastDirectory: {config1.LastDirectory ?? "Not set"}");
        
        // Test the same instance again
        var config2 = await configService.LoadAppConfigurationAsync();
        Console.WriteLine($"Second LoadAppConfigurationAsync - LastDirectory: {config2.LastDirectory ?? "Not set"}");
        
        Console.WriteLine($"RememberLastDirectory: {config1.RememberLastDirectory}");
        Console.WriteLine("Should the directory be available for startup? " + 
            (config1.RememberLastDirectory && !string.IsNullOrEmpty(config1.LastDirectory) && Directory.Exists(config1.LastDirectory)));
        
        // Verify that both configuration loading calls return valid objects
        await Assert.That(config1).IsNotNull();
        await Assert.That(config2).IsNotNull();
        
        // With isolated test instance, the update should work reliably
        await Assert.That(result).IsTrue();
        await Assert.That(config1.LastDirectory).IsEqualTo(testDirectory);
        await Assert.That(config2.LastDirectory).IsEqualTo(testDirectory);
        
        Console.WriteLine($"Test completed successfully. Update result: {result}");
        
        await Task.CompletedTask;
    }
}