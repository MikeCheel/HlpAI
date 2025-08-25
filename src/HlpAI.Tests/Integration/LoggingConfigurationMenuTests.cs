using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Integration;

public class LoggingConfigurationMenuTests
{
    private string _testDirectory = null!;
    private ILogger<LoggingConfigurationMenuTests> _logger = null!;
    private string _originalUserProfile = null!;
    private SqliteConfigurationService _configService = null!;
    private string _testDbPath = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("logging_config_menu_tests");
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<LoggingConfigurationMenuTests>();
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        // Create isolated test database for this test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_logging_config_{Guid.NewGuid()}.db");
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
    public async Task ErrorLoggingService_ConfigurationPersistence_WorksCorrectly()
    {
        // Arrange & Act
        using (var service1 = new ErrorLoggingService(_configService, _logger))
        {
            await service1.SetLoggingEnabledAsync(true);
            await service1.SetMinimumLogLevelAsync(LogLevel.Information);
            await service1.SetLogRetentionDaysAsync(14);
        }

        using var service2 = new ErrorLoggingService(_configService, _logger);
        
        // Assert
        await Assert.That(await service2.IsLoggingEnabledAsync()).IsTrue();
        await Assert.That(await service2.GetMinimumLogLevelAsync()).IsEqualTo(LogLevel.Information);
        await Assert.That(await service2.GetLogRetentionDaysAsync()).IsEqualTo(14);
    }

    [Test]
    public async Task ErrorLoggingService_EnableDisableToggle_WorksCorrectly()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        
        // Test initial state (should be enabled by default)
        var initialState = await service.IsLoggingEnabledAsync();
        await Assert.That(initialState).IsTrue();

        // Act & Assert - Disable
        await service.SetLoggingEnabledAsync(false);
        await Assert.That(await service.IsLoggingEnabledAsync()).IsFalse();

        // Act & Assert - Re-enable
        await service.SetLoggingEnabledAsync(true);
        await Assert.That(await service.IsLoggingEnabledAsync()).IsTrue();
    }

    [Test]
    public async Task ErrorLoggingService_LogLevelConfiguration_WorksCorrectly()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);

        // Test all log levels
        var logLevels = new[] { LogLevel.Error, LogLevel.Warning, LogLevel.Information };

        foreach (var level in logLevels)
        {
            // Act
            await service.SetMinimumLogLevelAsync(level);
            
            // Assert
            await Assert.That(await service.GetMinimumLogLevelAsync()).IsEqualTo(level);
        }
    }

    [Test]
    public async Task ErrorLoggingService_RetentionConfiguration_WorksCorrectly()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        var retentionValues = new[] { 1, 7, 14, 30, 90, 365 };

        foreach (var retention in retentionValues)
        {
            // Act
            var result = await service.SetLogRetentionDaysAsync(retention);
            
            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(await service.GetLogRetentionDaysAsync()).IsEqualTo(retention);
        }
    }

    [Test]
    public async Task ErrorLoggingService_InvalidRetentionValues_HandledCorrectly()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        var originalRetention = await service.GetLogRetentionDaysAsync();
        var invalidValues = new[] { 0, -1, -10 };

        foreach (var invalidValue in invalidValues)
        {
            // Act
            var result = await service.SetLogRetentionDaysAsync(invalidValue);
            
            // Assert
            await Assert.That(result).IsFalse();
            await Assert.That(await service.GetLogRetentionDaysAsync()).IsEqualTo(originalRetention);
        }
    }

    [Test]
    public async Task ErrorLoggingService_ViewRecentLogs_WorksCorrectly()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        
        // Create test logs
        await service.LogErrorAsync("Test error 1", null, "Unit test");
        await service.LogWarningAsync("Test warning 1", "Unit test");
        await service.LogInformationAsync("Test info 1", "Unit test");

        // Act
        var logs = await service.GetRecentLogsAsync(10);

        // Assert
        await Assert.That(logs.Count).IsEqualTo(3);
        await Assert.That(logs.Any(l => l.Message == "Test error 1")).IsTrue();
        await Assert.That(logs.Any(l => l.Message == "Test warning 1")).IsTrue();
        await Assert.That(logs.Any(l => l.Message == "Test info 1")).IsTrue();
    }

    [Test]
    public async Task ErrorLoggingService_ViewRecentLogsWithCount_WorksCorrectly()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        
        // Create more logs than we'll request
        for (int i = 0; i < 10; i++)
        {
            await service.LogErrorAsync($"Test error {i}", null, "Unit test");
        }

        // Act
        var logs = await service.GetRecentLogsAsync(5);

        // Assert
        await Assert.That(logs.Count).IsEqualTo(5);
        // Should get the most recent ones (highest numbers)
        await Assert.That(logs.All(l => l.Message.Contains("Test error"))).IsTrue();
    }

    [Test]
    public async Task ErrorLoggingService_DetailedStatistics_WorksCorrectly()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        
        // Create diverse test logs
        await service.LogErrorAsync("Error 1", null, "Unit test");
        await service.LogErrorAsync("Error 2", null, "Unit test");
        await service.LogWarningAsync("Warning 1", "Unit test");
        await service.LogInformationAsync("Info 1", "Unit test");

        // Act
        var stats = await service.GetLogStatisticsAsync();

        // Assert
        await Assert.That(stats.TotalLogs).IsEqualTo(4);
        await Assert.That(stats.ErrorsLast24Hours).IsEqualTo(2);
        await Assert.That(stats.WarningsLast24Hours).IsEqualTo(1);
        await Assert.That(stats.ErrorsLast7Days).IsEqualTo(2);
        await Assert.That(stats.WarningsLast7Days).IsEqualTo(1);
        await Assert.That(stats.OldestLogDate).IsNotNull();
        await Assert.That(stats.NewestLogDate).IsNotNull();
    }

    [Test]
    public async Task ErrorLoggingService_ClearAllLogs_WorksCorrectly()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        
        // Create test logs
        await service.LogErrorAsync("Test error", null, "Unit test");
        await service.LogWarningAsync("Test warning", "Unit test");
        
        // Verify logs exist
        var logsBefore = await service.GetRecentLogsAsync(10);
        await Assert.That(logsBefore.Count).IsEqualTo(2);

        // Act
        var result = await service.ClearAllLogsAsync();

        // Assert
        await Assert.That(result).IsTrue();
        var logsAfter = await service.GetRecentLogsAsync(10);
        await Assert.That(logsAfter.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ErrorLoggingService_TestLogging_CreatesLogs()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        var initialCount = (await service.GetRecentLogsAsync(100)).Count;

        // Act - Simulate the test error logging functionality
        await service.LogInformationAsync("Test information message", "Menu system test");
        await service.LogWarningAsync("Test warning message", "Menu system test");
        await service.LogErrorAsync("Test error message", 
            new InvalidOperationException("Test exception"), 
            "Menu system test");

        // Assert
        var logs = await service.GetRecentLogsAsync(100);
        await Assert.That(logs.Count).IsEqualTo(initialCount + 3);
        
        var infoLog = logs.FirstOrDefault(l => l.Message == "Test information message");
        var warningLog = logs.FirstOrDefault(l => l.Message == "Test warning message");
        var errorLog = logs.FirstOrDefault(l => l.Message == "Test error message");
        
        await Assert.That(infoLog).IsNotNull();
        await Assert.That(warningLog).IsNotNull();
        await Assert.That(errorLog).IsNotNull();
        
        await Assert.That(infoLog!.LogLevel).IsEqualTo("Information");
        await Assert.That(warningLog!.LogLevel).IsEqualTo("Warning");
        await Assert.That(errorLog!.LogLevel).IsEqualTo("Error");
        await Assert.That(errorLog.ExceptionType).IsEqualTo("InvalidOperationException");
        await Assert.That(errorLog.Context).IsEqualTo("Menu system test");
    }

    [Test]
    public async Task ErrorLoggingService_LoggingWhenDisabled_DoesNotCreateLogs()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        await service.SetLoggingEnabledAsync(false);
        var initialCount = (await service.GetRecentLogsAsync(100)).Count;

        // Act
        await service.LogErrorAsync("This should not be logged", null, "Unit test");

        // Assert
        var logs = await service.GetRecentLogsAsync(100);
        await Assert.That(logs.Count).IsEqualTo(initialCount);
    }

    [Test]
    public async Task ErrorLoggingService_ConfigurationCategories_AreCorrect()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);

        // Act
        await service.SetLoggingEnabledAsync(true);
        await service.SetMinimumLogLevelAsync(LogLevel.Error);
        await service.SetLogRetentionDaysAsync(7);

        // Assert - Check that settings are stored in correct categories
        var loggingConfig = await _configService.GetCategoryConfigurationAsync("logging");
        await Assert.That(loggingConfig.ContainsKey("error_logging_enabled")).IsTrue();
        await Assert.That(loggingConfig.ContainsKey("minimum_log_level")).IsTrue();
        await Assert.That(loggingConfig.ContainsKey("log_retention_days")).IsTrue();
        
        await Assert.That(loggingConfig["error_logging_enabled"]).IsEqualTo("true");
        await Assert.That(loggingConfig["minimum_log_level"]).IsEqualTo("Error");
        await Assert.That(loggingConfig["log_retention_days"]).IsEqualTo("7");
    }

    [Test]
    public async Task ErrorLoggingService_MultipleInstances_ShareConfiguration()
    {
        // Arrange & Act
        using (var service1 = new ErrorLoggingService(_configService, _logger))
        {
            await service1.SetLoggingEnabledAsync(false);
            await service1.SetMinimumLogLevelAsync(LogLevel.Error);
            await service1.SetLogRetentionDaysAsync(21);
        }

        using (var service2 = new ErrorLoggingService(_configService, _logger))
        {
            // Assert
            await Assert.That(await service2.IsLoggingEnabledAsync()).IsFalse();
            await Assert.That(await service2.GetMinimumLogLevelAsync()).IsEqualTo(LogLevel.Error);
            await Assert.That(await service2.GetLogRetentionDaysAsync()).IsEqualTo(21);
        }
    }

    [Test]
    public async Task ErrorLoggingService_DatabaseIntegration_WorksWithSqliteConfigurationService()
    {
        // Arrange
        using var errorService = new ErrorLoggingService(_configService, _logger);

        // Act - Configure via ErrorLoggingService
        await errorService.SetLoggingEnabledAsync(true);
        await errorService.SetMinimumLogLevelAsync(LogLevel.Warning);
        await errorService.SetLogRetentionDaysAsync(45);

        // Assert - Verify via the same SqliteConfigurationService
        var enabled = await _configService.GetConfigurationAsync("error_logging_enabled", "logging");
        var level = await _configService.GetConfigurationAsync("minimum_log_level", "logging");
        var retention = await _configService.GetConfigurationAsync("log_retention_days", "logging");

        await Assert.That(enabled).IsEqualTo("true");
        await Assert.That(level).IsEqualTo("Warning");
        await Assert.That(retention).IsEqualTo("45");
    }

    [Test]
    public async Task ErrorLoggingService_ConfigurationDefaults_AreCorrect()
    {
        // Arrange & Act
        using var service = new ErrorLoggingService(_configService, _logger);

        // Assert default values
        await Assert.That(await service.IsLoggingEnabledAsync()).IsTrue();
        await Assert.That(await service.GetMinimumLogLevelAsync()).IsEqualTo(LogLevel.Warning);
        await Assert.That(await service.GetLogRetentionDaysAsync()).IsEqualTo(30);
    }

    [Test]
    public async Task ErrorLoggingService_ConcurrentConfiguration_HandledCorrectly()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - Multiple services configuring concurrently
        for (int i = 0; i < 5; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                using var service = new ErrorLoggingService(_configService, _logger);
                await service.SetLogRetentionDaysAsync(10 + index);
                await service.LogErrorAsync($"Concurrent test {index}", null, "Concurrency test");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        using var finalService = new ErrorLoggingService(_configService, _logger);
        var logs = await finalService.GetRecentLogsAsync(10);
        var retentionDays = await finalService.GetLogRetentionDaysAsync();

        // Should have logs from all concurrent operations
        await Assert.That(logs.Count(l => l.Context == "Concurrency test")).IsEqualTo(5);
        // Retention should be one of the set values
        await Assert.That(retentionDays).IsGreaterThanOrEqualTo(10);
        await Assert.That(retentionDays).IsLessThanOrEqualTo(14);
    }

    [Test]
    public async Task ErrorLoggingService_DatabaseCleanup_WorksCorrectly()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);

        // Create configuration and logs
        await service.SetLoggingEnabledAsync(true);
        await service.LogErrorAsync("Test before cleanup", null, "Cleanup test");

        // Act - Clear all categories using the same configuration service
        await _configService.ClearCategoryAsync("logging");
        await _configService.ClearCategoryAsync("error_logs");

        // Assert - Configuration should be reset to defaults
        await Assert.That(await service.IsLoggingEnabledAsync()).IsTrue(); // Default
        await Assert.That(await service.GetMinimumLogLevelAsync()).IsEqualTo(LogLevel.Warning); // Default
        await Assert.That(await service.GetLogRetentionDaysAsync()).IsEqualTo(30); // Default
        
        var logs = await service.GetRecentLogsAsync(10);
        await Assert.That(logs.Count).IsEqualTo(0);
    }
}
