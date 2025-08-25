using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Integration;

public class ConfigurationMenuTests
{
    private string _testDirectory = null!;
    private ILogger<ConfigurationMenuTests> _logger = null!;
    private string _originalUserProfile = null!;
    private SqliteConfigurationService _configService = null!;
    private string _testDbPath = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("config_menu_tests");
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ConfigurationMenuTests>();
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        // Create isolated test database for this test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_config_menu_{Guid.NewGuid()}.db");
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
    public async Task ConfigurationServices_Integration_WorksTogether()
    {
        // Arrange
        using var sqliteConfig = _configService;
        using var hhExeService = new HhExeDetectionService(_configService, _logger);

        // Act - Test various configuration scenarios
        
        // Test 1: Auto-detect hh.exe (simulated)
        var autoDetectResult = await hhExeService.CheckDefaultLocationAsync();
        
        // Test 2: Set custom path
        const string customPath = @"C:\CustomPath\hh.exe";
        var setResult = await hhExeService.SetHhExePathAsync(customPath, false);
        
        // Test 3: Retrieve configuration
        var configuredPath = await hhExeService.GetConfiguredHhExePathAsync();
        var isAutoDetected = await hhExeService.IsHhExePathAutoDetectedAsync();
        
        // Test 4: Get database stats
        var stats = await sqliteConfig.GetStatsAsync();

        // Assert
        await Assert.That(setResult).IsTrue();
        await Assert.That(configuredPath).IsEqualTo(customPath);
        await Assert.That(isAutoDetected).IsFalse();
        await Assert.That(stats.TotalItems).IsGreaterThan(0);
        await Assert.That(stats.DatabasePath).IsNotNull();
    }

    [Test]
    public async Task HhExeService_AutoDetection_StoresInConfigDatabase()
    {
        // Arrange
        using var hhExeService = new HhExeDetectionService(_configService, _logger);

        // Act
        var result = await hhExeService.CheckDefaultLocationAsync();
        var configuredPath = await hhExeService.GetConfiguredHhExePathAsync();
        var isAutoDetected = await hhExeService.IsHhExePathAutoDetectedAsync();

        // Assert
        if (File.Exists(@"C:\Windows\hh.exe"))
        {
            // If hh.exe exists at default location
            await Assert.That(result).IsTrue();
            await Assert.That(configuredPath).IsEqualTo(@"C:\Windows\hh.exe");
            await Assert.That(isAutoDetected).IsTrue();
        }
        else
        {
            // If hh.exe doesn't exist at default location
            await Assert.That(result).IsFalse();
            // Configuration should remain empty for first-time detection
            // (Only set on successful detection)
        }
    }

    [Test]
    public async Task HhExeService_ManualConfiguration_OverridesAutoDetection()
    {
        // Arrange
        using var hhExeService = new HhExeDetectionService(_configService, _logger);
        const string manualPath = @"C:\Manual\hh.exe";

        // Act
        // First do auto-detection
        await hhExeService.CheckDefaultLocationAsync();
        
        // Then set manual path
        await hhExeService.SetHhExePathAsync(manualPath, false);
        
        var configuredPath = await hhExeService.GetConfiguredHhExePathAsync();
        var isAutoDetected = await hhExeService.IsHhExePathAutoDetectedAsync();

        // Assert
        await Assert.That(configuredPath).IsEqualTo(manualPath);
        await Assert.That(isAutoDetected).IsFalse();
    }

    [Test]
    public async Task HhExeService_ClearConfiguration_RemovesStoredPath()
    {
        // Arrange
        using var hhExeService = new HhExeDetectionService(_configService, _logger);
        
        // Set a path first
        await hhExeService.SetHhExePathAsync(@"C:\Test\hh.exe", true);

        // Act
        await hhExeService.SetHhExePathAsync(null, false);

        // Assert
        var configuredPath = await hhExeService.GetConfiguredHhExePathAsync();
        await Assert.That(configuredPath).IsNull();
    }

    [Test]
    public async Task ConfigurationDatabase_Statistics_AccuratelyReflectData()
    {
        // Arrange
        using var sqliteConfig = _configService;
        using var hhExeService = new HhExeDetectionService(_configService, _logger);

        // Act
        // Add some configuration data
        await sqliteConfig.SetConfigurationAsync("test_key1", "value1", "application");
        await sqliteConfig.SetConfigurationAsync("test_key2", "value2", "application");
        await hhExeService.SetHhExePathAsync(@"C:\Test\hh.exe", false);

        var stats = await sqliteConfig.GetStatsAsync();

        // Assert
        await Assert.That(stats.TotalItems).IsGreaterThanOrEqualTo(3);
        await Assert.That(stats.TotalCategories).IsGreaterThanOrEqualTo(2);
        await Assert.That(stats.LastUpdate).IsNotNull();
        await Assert.That(stats.DatabasePath).IsEqualTo(_testDbPath);
    }

    [Test]
    public async Task ConfigurationDatabase_CategoryConfiguration_ReturnsCorrectData()
    {
        // Arrange
        using var sqliteConfig = _configService;
        using var hhExeService = new HhExeDetectionService(_configService, _logger);

        // Act
        await hhExeService.SetHhExePathAsync(@"C:\Test\hh.exe", true);
        
        var systemConfig = await sqliteConfig.GetCategoryConfigurationAsync("system");

        // Assert
        await Assert.That(systemConfig.ContainsKey("hh_exe_path")).IsTrue();
        await Assert.That(systemConfig.ContainsKey("hh_exe_auto_detected")).IsTrue();
        await Assert.That(systemConfig["hh_exe_path"]).IsEqualTo(@"C:\Test\hh.exe");
        await Assert.That(systemConfig["hh_exe_auto_detected"]).IsEqualTo("true");
    }

    [Test]
    public async Task HhExeService_DetectionHistory_RecordsAllAttempts()
    {
        // Arrange
        using var hhExeService = new HhExeDetectionService(_configService, _logger);

        // Act
        await hhExeService.CheckDefaultLocationAsync();
        await hhExeService.CheckDefaultLocationAsync();
        await hhExeService.CheckDefaultLocationAsync();

        var history = await hhExeService.GetDetectionHistoryAsync();

        // Assert
        await Assert.That(history).HasCount().EqualTo(3);
        
        // Verify all entries are for the default path
        foreach (var entry in history)
        {
            await Assert.That(entry.Path).IsEqualTo(@"C:\Windows\hh.exe");
            await Assert.That(entry.DetectedAt).IsLessThanOrEqualTo(DateTime.UtcNow);
            await Assert.That(entry.Notes).IsNotNull();
        }
    }

    [Test]
    public async Task HhExeService_LastSuccessfulDetection_ReturnsCorrectResult()
    {
        // Arrange
        using var hhExeService = new HhExeDetectionService(_configService, _logger);

        // Act
        await hhExeService.CheckDefaultLocationAsync();
        var lastSuccessful = await hhExeService.GetLastSuccessfulDetectionAsync();

        // Assert
        if (File.Exists(@"C:\Windows\hh.exe"))
        {
            await Assert.That(lastSuccessful).IsNotNull();
            await Assert.That(lastSuccessful!.Found).IsTrue();
            await Assert.That(lastSuccessful.Path).IsEqualTo(@"C:\Windows\hh.exe");
        }
        else
        {
            await Assert.That(lastSuccessful).IsNull();
        }
    }

    [Test]
    public async Task ConfigurationDatabase_ClearCategory_RemovesOnlySpecifiedCategory()
    {
        // Arrange
        using var sqliteConfig = _configService;

        // Act
        await sqliteConfig.SetConfigurationAsync("key1", "value1", "category1");
        await sqliteConfig.SetConfigurationAsync("key2", "value2", "category1");
        await sqliteConfig.SetConfigurationAsync("key3", "value3", "category2");

        var deletedCount = await sqliteConfig.ClearCategoryAsync("category1");
        
        var category1Config = await sqliteConfig.GetCategoryConfigurationAsync("category1");
        var category2Config = await sqliteConfig.GetCategoryConfigurationAsync("category2");

        // Assert
        await Assert.That(deletedCount).IsEqualTo(2);
        await Assert.That(category1Config).IsEmpty();
        await Assert.That(category2Config).HasCount().EqualTo(1);
        await Assert.That(category2Config.ContainsKey("key3")).IsTrue();
    }

    [Test]
    public async Task HhExeService_PathValidation_WorksCorrectly()
    {
        // Arrange
        using var hhExeService = new HhExeDetectionService(_configService, _logger);

        // Test valid paths (system-dependent)
        var validPaths = new[]
        {
            @"C:\Windows\hh.exe",
            @"C:\Program Files\HTML Help Workshop\hh.exe",
            @"C:\Custom\Path\hh.exe"
        };

        foreach (var path in validPaths)
        {
            // Act
            await hhExeService.SetHhExePathAsync(path, false);
            var configuredPath = await hhExeService.GetConfiguredHhExePathAsync();

            // Assert
            await Assert.That(configuredPath).IsEqualTo(path);
        }
    }

    [Test]
    public async Task ConfigurationServices_HandlesConcurrentAccess()
    {
        // Arrange
        using var sqliteConfig = _configService;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 5; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                // Use unique configuration keys for each concurrent task
                await sqliteConfig.SetConfigurationAsync($"test_key_{index}", $"value_{index}", "test_category");
                await sqliteConfig.SetConfigurationAsync($"another_key_{index}", $"another_value_{index}", "another_category");
            }));
        }

        await Task.WhenAll(tasks);

        // Give the database a moment to finalize all transactions
        await Task.Delay(100);

        // Assert
        var stats = await sqliteConfig.GetStatsAsync();
        var testConfig = await sqliteConfig.GetCategoryConfigurationAsync("test_category");
        var anotherConfig = await sqliteConfig.GetCategoryConfigurationAsync("another_category");

        await Assert.That(stats.TotalItems).IsGreaterThan(0);
        await Assert.That(testConfig.Count).IsGreaterThan(0);
        await Assert.That(anotherConfig.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task HhExeService_DetectionHistory_LimitsResults()
    {
        // Arrange
        using var hhExeService = new HhExeDetectionService(_configService, _logger);

        // Act - Create more than 100 detection attempts
        for (int i = 0; i < 105; i++)
        {
            await hhExeService.CheckDefaultLocationAsync();
        }

        var history = await hhExeService.GetDetectionHistoryAsync();

        // Assert
        await Assert.That(history).HasCount().EqualTo(100); // Should be limited to 100
    }

    [Test]
    public async Task ConfigurationServices_Dispose_HandledGracefully()
    {
        // Arrange
        var sqliteConfig = new SqliteConfigurationService(_logger);
        var hhExeService = new HhExeDetectionService(_logger);

        // Act
        await sqliteConfig.SetConfigurationAsync("test", "value");
        await hhExeService.SetHhExePathAsync(@"C:\Test\hh.exe", false);

        // Dispose multiple times
        sqliteConfig.Dispose();
        sqliteConfig.Dispose();
        hhExeService.Dispose();
        hhExeService.Dispose();

        // Assert - No exceptions should be thrown
    }
}
