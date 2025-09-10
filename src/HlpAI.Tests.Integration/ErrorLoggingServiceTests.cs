using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Services;

public class ErrorLoggingServiceTests
{
    private string _testDirectory = null!;
    private ILogger<ErrorLoggingServiceTests> _logger = null!;
    private string _originalUserProfile = null!;
    private SqliteConfigurationService _configService = null!;
    private string _testDbPath = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("error_logging_service_tests");
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ErrorLoggingServiceTests>();
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        // Create isolated test database for this test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_error_logging_{Guid.NewGuid()}.db");
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
    public async Task LogErrorAsync_WithBasicError_StoresCorrectly()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        const string errorMessage = "Test error message";

        // Act
        await service.LogErrorAsync(errorMessage);

        // Assert
        var logs = await service.GetRecentLogsAsync(10);
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Message).IsEqualTo(errorMessage);
        await Assert.That(logs[0].LogLevel).IsEqualTo("Error");
        await Assert.That(logs[0].Source).IsEqualTo("HlpAI.Interactive");
    }

    [Test]
    public async Task LogErrorAsync_WithException_StoresExceptionDetails()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        const string errorMessage = "Test error with exception";
        var exception = new InvalidOperationException("Test exception message");

        // Act
        await service.LogErrorAsync(errorMessage, exception);

        // Assert
        var logs = await service.GetRecentLogsAsync(10);
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Message).IsEqualTo(errorMessage);
        await Assert.That(logs[0].ExceptionType).IsEqualTo("InvalidOperationException");
        await Assert.That(logs[0].ExceptionMessage).IsEqualTo("Test exception message");
        await Assert.That(logs[0].StackTrace).IsNotNull();
    }

    [Test]
    public async Task LogErrorAsync_WithContext_StoresContextInformation()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        const string errorMessage = "Test error with context";
        const string context = "Interactive mode - directory selection";

        // Act
        await service.LogErrorAsync(errorMessage, null, context);

        // Assert
        var logs = await service.GetRecentLogsAsync(10);
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Context).IsEqualTo(context);
    }

    [Test]
    public async Task LogErrorAsync_WithDifferentLogLevels_StoresCorrectly()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);

        // Act
        await service.LogErrorAsync("Error message", null, null, LogLevel.Error);
        await service.LogWarningAsync("Warning message");
        await service.LogInformationAsync("Info message");

        // Assert
        var logs = await service.GetRecentLogsAsync(10);
        await Assert.That(logs.Count).IsEqualTo(3);
        
        var errorLog = logs.FirstOrDefault(l => l.LogLevel == "Error");
        var warningLog = logs.FirstOrDefault(l => l.LogLevel == "Warning");
        var infoLog = logs.FirstOrDefault(l => l.LogLevel == "Information");
        
        await Assert.That(errorLog).IsNotNull();
        await Assert.That(warningLog).IsNotNull();
        await Assert.That(infoLog).IsNotNull();
    }

    [Test]
    public async Task LogErrorAsync_WhenLoggingDisabled_DoesNotStore()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        await service.SetLoggingEnabledAsync(false);

        // Act
        await service.LogErrorAsync("This should not be logged");

        // Assert
        var logs = await service.GetRecentLogsAsync(10);
        await Assert.That(logs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetRecentLogsAsync_WithLogLevelFilter_ReturnsFilteredResults()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        
        await service.LogErrorAsync("Error 1", null, null, LogLevel.Error);
        await service.LogErrorAsync("Warning 1", null, null, LogLevel.Warning);
        await service.LogErrorAsync("Error 2", null, null, LogLevel.Error);
        await service.LogErrorAsync("Info 1", null, null, LogLevel.Information);

        // Act
        var errorLogs = await service.GetRecentLogsAsync(10, LogLevel.Error);
        var warningLogs = await service.GetRecentLogsAsync(10, LogLevel.Warning);

        // Assert
        await Assert.That(errorLogs.Count).IsEqualTo(2);
        await Assert.That(warningLogs.Count).IsEqualTo(1);
        await Assert.That(errorLogs.All(l => l.LogLevel == "Error")).IsTrue();
        await Assert.That(warningLogs.All(l => l.LogLevel == "Warning")).IsTrue();
    }

    [Test]
    public async Task GetRecentLogsAsync_WithCountLimit_ReturnsLimitedResults()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        
        // Create more logs than the limit
        for (int i = 0; i < 10; i++)
        {
            await service.LogErrorAsync($"Error {i}");
        }

        // Act
        var logs = await service.GetRecentLogsAsync(5);

        // Assert
        await Assert.That(logs.Count).IsEqualTo(5);
        // Should return most recent logs (highest numbers)
        await Assert.That(logs[0].Message).IsEqualTo("Error 9");
        await Assert.That(logs[4].Message).IsEqualTo("Error 5");
    }

    [Test]
    public async Task GetLogStatisticsAsync_WithVariousLogs_ReturnsCorrectStatistics()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        
        // Create logs with different timestamps
        await service.LogErrorAsync("Error 1", null, null, LogLevel.Error);
        await service.LogErrorAsync("Warning 1", null, null, LogLevel.Warning);
        await service.LogErrorAsync("Error 2", null, null, LogLevel.Error);
        await service.LogErrorAsync("Info 1", null, null, LogLevel.Information);

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
    public async Task ClearAllLogsAsync_RemovesAllLogs()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        
        await service.LogErrorAsync("Error 1");
        await service.LogErrorAsync("Error 2");
        await service.LogErrorAsync("Error 3");

        // Verify logs exist
        var logsBefore = await service.GetRecentLogsAsync(10);
        await Assert.That(logsBefore.Count).IsEqualTo(3);

        // Act
        var result = await service.ClearAllLogsAsync();

        // Assert
        await Assert.That(result).IsTrue();
        var logsAfter = await service.GetRecentLogsAsync(10);
        await Assert.That(logsAfter.Count).IsEqualTo(0);
    }

    [Test]
    public async Task LoggingEnabledSettings_WorkCorrectly()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);

        // Test default enabled state
        var initialState = await service.IsLoggingEnabledAsync();
        await Assert.That(initialState).IsTrue();

        // Act & Assert - Disable logging
        var disableResult = await service.SetLoggingEnabledAsync(false);
        await Assert.That(disableResult).IsTrue();
        
        var disabledState = await service.IsLoggingEnabledAsync();
        await Assert.That(disabledState).IsFalse();

        // Act & Assert - Re-enable logging
        var enableResult = await service.SetLoggingEnabledAsync(true);
        await Assert.That(enableResult).IsTrue();
        
        var enabledState = await service.IsLoggingEnabledAsync();
        await Assert.That(enabledState).IsTrue();
    }

    [Test]
    public async Task LogRetentionSettings_WorkCorrectly()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);

        // Test default retention
        var defaultRetention = await service.GetLogRetentionDaysAsync();
        await Assert.That(defaultRetention).IsEqualTo(30);

        // Act & Assert - Set custom retention
        var setResult = await service.SetLogRetentionDaysAsync(14);
        await Assert.That(setResult).IsTrue();
        
        var newRetention = await service.GetLogRetentionDaysAsync();
        await Assert.That(newRetention).IsEqualTo(14);

        // Test invalid retention value
        var invalidResult = await service.SetLogRetentionDaysAsync(0);
        await Assert.That(invalidResult).IsFalse();
        
        var unchangedRetention = await service.GetLogRetentionDaysAsync();
        await Assert.That(unchangedRetention).IsEqualTo(14);
    }

    [Test]
    public async Task MinimumLogLevelSettings_WorkCorrectly()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);

        // Test default log level
        var defaultLevel = await service.GetMinimumLogLevelAsync();
        await Assert.That(defaultLevel).IsEqualTo(LogLevel.Warning);

        // Act & Assert - Set different log level
        var setResult = await service.SetMinimumLogLevelAsync(LogLevel.Error);
        await Assert.That(setResult).IsTrue();
        
        var newLevel = await service.GetMinimumLogLevelAsync();
        await Assert.That(newLevel).IsEqualTo(LogLevel.Error);

        // Test setting Information level
        var infoResult = await service.SetMinimumLogLevelAsync(LogLevel.Information);
        await Assert.That(infoResult).IsTrue();
        
        var infoLevel = await service.GetMinimumLogLevelAsync();
        await Assert.That(infoLevel).IsEqualTo(LogLevel.Information);
    }

    [Test]
    public async Task ErrorLogEntry_HasValidId()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);

        // Act
        await service.LogErrorAsync("Test error");

        // Assert
        var logs = await service.GetRecentLogsAsync(1);
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Id).IsNotEmpty();
        await Assert.That(Guid.TryParse(logs[0].Id, out _)).IsTrue();
    }

    [Test]
    public async Task ErrorLogEntry_HasValidTimestamp()
    {
        // Arrange
        using var service = new ErrorLoggingService(_configService, _logger);
        var beforeLogging = DateTime.UtcNow.AddSeconds(-1);

        // Act
        await service.LogErrorAsync("Test error");
        var afterLogging = DateTime.UtcNow.AddSeconds(1);

        // Assert
        var logs = await service.GetRecentLogsAsync(1);
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Timestamp).IsGreaterThan(beforeLogging);
        await Assert.That(logs[0].Timestamp).IsLessThan(afterLogging);
    }

    [Test]
    public async Task MultipleServiceInstances_ShareSameDatabase()
    {
        // Arrange & Act
        using (var service1 = new ErrorLoggingService(_configService, _logger))
        {
            await service1.LogErrorAsync("Error from service 1");
        }

        using var service2 = new ErrorLoggingService(_configService, _logger);
        var logs = await service2.GetRecentLogsAsync(10);

        // Assert
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Message).IsEqualTo("Error from service 1");
    }

    [Test]
    public async Task ServiceWithNullLogger_WorksCorrectly()
    {
        // Arrange & Act
        using var service = new ErrorLoggingService(_configService, null);
        
        await service.LogErrorAsync("Test error with null logger");
        var logs = await service.GetRecentLogsAsync(10);

        // Assert
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Message).IsEqualTo("Test error with null logger");
    }

    [Test]
    public async Task Dispose_HandledGracefully()
    {
        // Arrange
        var service = new ErrorLoggingService(_configService, _logger);
        await service.LogErrorAsync("Test error before disposal");

        // Act & Assert - Should not throw
        service.Dispose();
        service.Dispose(); // Second call should be handled gracefully
        
        // Verify we can still read logs with a new instance
        using var newService = new ErrorLoggingService(_configService, _logger);
        var logs = await newService.GetRecentLogsAsync(10);
        await Assert.That(logs.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ConcurrentAccess_HandledCorrectly()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 5; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                using var service = new ErrorLoggingService(_configService, _logger);
                await service.LogErrorAsync($"Concurrent error {index}");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        using var finalService = new ErrorLoggingService(_configService, _logger);
        var logs = await finalService.GetRecentLogsAsync(10);
        
        await Assert.That(logs.Count).IsEqualTo(5);
        // Verify all messages are present
        for (int i = 0; i < 5; i++)
        {
            await Assert.That(logs.Any(l => l.Message == $"Concurrent error {i}")).IsTrue();
        }
    }

    [Test]
    public async Task ConfigurationPersistence_AcrossServiceInstances()
    {
        // Arrange & Act
        using (var service1 = new ErrorLoggingService(_configService, _logger))
        {
            await service1.SetLoggingEnabledAsync(false);
            await service1.SetLogRetentionDaysAsync(7);
            await service1.SetMinimumLogLevelAsync(LogLevel.Error);
        }

        using var service2 = new ErrorLoggingService(_configService, _logger);
        
        // Assert
        await Assert.That(await service2.IsLoggingEnabledAsync()).IsFalse();
        await Assert.That(await service2.GetLogRetentionDaysAsync()).IsEqualTo(7);
        await Assert.That(await service2.GetMinimumLogLevelAsync()).IsEqualTo(LogLevel.Error);
    }
}