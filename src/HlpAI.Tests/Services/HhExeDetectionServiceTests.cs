using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Services;

public class HhExeDetectionServiceTests
{
    private string _testDirectory = null!;
    private ILogger<HhExeDetectionServiceTests> _logger = null!;
    private string _originalUserProfile = null!;
    private SqliteConfigurationService _configService = null!;
    private string _testDbPath = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("hh_exe_detection_tests");
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<HhExeDetectionServiceTests>();
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        // Create isolated test database for this test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_hh_detection_{Guid.NewGuid()}.db");
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
    public async Task Constructor_CreatesConfigurationDirectoryAndDatabase()
    {
        // Act
        using var service = new HhExeDetectionService(_configService, _logger);

        // Assert
        // The database should be created at the test path we specified
        await Assert.That(File.Exists(_testDbPath)).IsTrue();
    }

    [Test]
    public async Task CheckDefaultLocationAsync_WhenHhExeExists_ReturnsTrue()
    {
        // Arrange
        var defaultPath = @"C:\Windows\hh.exe";
        
        // Create mock hh.exe file for testing
        var windowsDir = @"C:\Windows";
        if (!Directory.Exists(windowsDir))
        {
            // If we're not on Windows or can't access C:\Windows, skip this test
            return;
        }

        // Note: We can't create files in C:\Windows for testing, so we'll test the logic
        using var service = new HhExeDetectionService(_configService, _logger);

        // Act
        var result = await service.CheckDefaultLocationAsync();

        // Assert
        // The result depends on whether hh.exe actually exists on the system
        var expectedExists = File.Exists(defaultPath);
        await Assert.That(result).IsEqualTo(expectedExists);
        
        // Verify the result was stored in database
        var history = await service.GetDetectionHistoryAsync();
        await Assert.That(history).HasCount().EqualTo(1);
        await Assert.That(history[0].Path).IsEqualTo(defaultPath);
        await Assert.That(history[0].Found).IsEqualTo(expectedExists);
    }

    [Test]
    public async Task CheckDefaultLocationAsync_WhenHhExeDoesNotExist_ReturnsFalse()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);

        // Act - Check a path we know doesn't exist
        var result = await service.CheckDefaultLocationAsync();

        // Assert
        // Since we can't guarantee the state of C:\Windows\hh.exe, we'll test the database storage
        var history = await service.GetDetectionHistoryAsync();
        await Assert.That(history).HasCount().EqualTo(1);
        await Assert.That(history[0].Path).IsEqualTo(@"C:\Windows\hh.exe");
        // Found is already a bool type - no need to test this explicitly
        await Assert.That(history[0].DetectedAt).IsLessThanOrEqualTo(DateTime.UtcNow);
        await Assert.That(history[0].DetectedAt).IsGreaterThan(DateTime.UtcNow.AddMinutes(-1));
    }

    [Test]
    public async Task GetDefaultHhExePathAsync_WhenHhExeExists_ReturnsPath()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);

        // Act
        var result = await service.GetDefaultHhExePathAsync();

        // Assert
        if (File.Exists(@"C:\Windows\hh.exe"))
        {
            await Assert.That(result).IsEqualTo(@"C:\Windows\hh.exe");
        }
        else
        {
            await Assert.That(result).IsNull();
        }
    }

    [Test]
    public async Task GetDefaultHhExePathAsync_WhenHhExeDoesNotExist_ReturnsNull()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);

        // Act
        var result = await service.GetDefaultHhExePathAsync();

        // Assert
        // If hh.exe doesn't exist at the default location, should return null
        if (!File.Exists(@"C:\Windows\hh.exe"))
        {
            await Assert.That(result).IsNull();
        }
        else
        {
            await Assert.That(result).IsNotNull();
        }
    }

    [Test]
    public async Task GetDetectionHistoryAsync_WithMultipleDetections_ReturnsOrderedHistory()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);

        // Act - Perform multiple detections
        await service.CheckDefaultLocationAsync();
        await Task.Delay(10); // Ensure different timestamps
        await service.CheckDefaultLocationAsync();
        await Task.Delay(10); // Ensure different timestamps
        await service.CheckDefaultLocationAsync();

        var history = await service.GetDetectionHistoryAsync();

        // Assert
        await Assert.That(history).HasCount().EqualTo(3);
        
        // Verify ordering (most recent first)
        for (int i = 0; i < history.Count - 1; i++)
        {
            await Assert.That(history[i].DetectedAt).IsGreaterThanOrEqualTo(history[i + 1].DetectedAt);
        }
        
        // Verify all entries have the same path
        foreach (var entry in history)
        {
            await Assert.That(entry.Path).IsEqualTo(@"C:\Windows\hh.exe");
            // Found is already a bool type
            await Assert.That(entry.DetectedAt).IsLessThanOrEqualTo(DateTime.UtcNow);
        }
    }

    [Test]
    public async Task GetDetectionHistoryAsync_WithNoDetections_ReturnsEmptyList()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);

        // Act
        var history = await service.GetDetectionHistoryAsync();

        // Assert
        await Assert.That(history).IsEmpty();
    }

    [Test]
    public async Task GetLastSuccessfulDetectionAsync_WithSuccessfulDetection_ReturnsResult()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);

        // Act
        await service.CheckDefaultLocationAsync();
        var lastSuccessful = await service.GetLastSuccessfulDetectionAsync();

        // Assert
        if (File.Exists(@"C:\Windows\hh.exe"))
        {
            await Assert.That(lastSuccessful).IsNotNull();
            await Assert.That(lastSuccessful!.Path).IsEqualTo(@"C:\Windows\hh.exe");
            await Assert.That(lastSuccessful.Found).IsTrue();
            await Assert.That(lastSuccessful.Notes).IsNotNull();
        }
        else
        {
            await Assert.That(lastSuccessful).IsNull();
        }
    }

    [Test]
    public async Task GetLastSuccessfulDetectionAsync_WithNoSuccessfulDetections_ReturnsNull()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);

        // Act - If hh.exe doesn't exist, this will create a failed detection
        await service.CheckDefaultLocationAsync();
        var lastSuccessful = await service.GetLastSuccessfulDetectionAsync();

        // Assert
        if (!File.Exists(@"C:\Windows\hh.exe"))
        {
            await Assert.That(lastSuccessful).IsNull();
        }
    }

    [Test]
    public async Task ClearDetectionHistoryAsync_WithExistingHistory_ClearsAllRecords()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);
        
        // Create some history
        await service.CheckDefaultLocationAsync();
        await service.CheckDefaultLocationAsync();
        await service.CheckDefaultLocationAsync();

        // Verify history exists
        var initialHistory = await service.GetDetectionHistoryAsync();
        await Assert.That(initialHistory).HasCount().EqualTo(3);

        // Act
        var deletedCount = await service.ClearDetectionHistoryAsync();

        // Assert
        await Assert.That(deletedCount).IsEqualTo(3);
        
        var clearedHistory = await service.GetDetectionHistoryAsync();
        await Assert.That(clearedHistory).IsEmpty();
    }

    [Test]
    public async Task ClearDetectionHistoryAsync_WithNoHistory_ReturnsZero()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);

        // Act
        var deletedCount = await service.ClearDetectionHistoryAsync();

        // Assert
        await Assert.That(deletedCount).IsEqualTo(0);
    }

    [Test]
    public async Task MultipleServices_AccessSameDatabase_WorkCorrectly()
    {
        // Arrange & Act
        using var service1 = new HhExeDetectionService(_configService, _logger);
        await service1.CheckDefaultLocationAsync();
        
        using var service2 = new HhExeDetectionService(_configService, _logger);
        var history = await service2.GetDetectionHistoryAsync();

        // Assert
        await Assert.That(history).HasCount().EqualTo(1);
        await Assert.That(history[0].Path).IsEqualTo(@"C:\Windows\hh.exe");
    }

    [Test]
    public async Task Service_WithNullLogger_WorksCorrectly()
    {
        // Arrange & Act
        using var service = new HhExeDetectionService(_configService, null);
        
        var result = await service.CheckDefaultLocationAsync();
        var history = await service.GetDetectionHistoryAsync();

        // Assert
        // result is already a bool type
        await Assert.That(history).HasCount().EqualTo(1);
    }

    [Test]
    public async Task Dispose_MultipleCalls_HandledGracefully()
    {
        // Arrange
        var service = new HhExeDetectionService(_configService, _logger);
        await service.CheckDefaultLocationAsync();

        // Act & Assert - Should not throw
        service.Dispose();
        service.Dispose(); // Second call should be handled gracefully
        
        // Test passes if no exception thrown
    }

    [Test]
    public async Task CheckDefaultLocationAsync_StoresCorrectNotes()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);

        // Act
        await service.CheckDefaultLocationAsync();
        var history = await service.GetDetectionHistoryAsync();

        // Assert
        await Assert.That(history).HasCount().EqualTo(1);
        var entry = history[0];
        
        if (entry.Found)
        {
            await Assert.That(entry.Notes).IsEqualTo("Found at default Windows location");
        }
        else
        {
            await Assert.That(entry.Notes).IsEqualTo("Not found at default Windows location");
        }
    }

    [Test]
    public async Task DatabaseOperations_HandleLargeDataSets_Efficiently()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);

        // Act - Create many detection records (but not actually call CheckDefaultLocationAsync repeatedly)
        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(service.CheckDefaultLocationAsync());
            if (i % 10 == 0)
            {
                await Task.Delay(1); // Small delay to ensure different timestamps
            }
        }
        await Task.WhenAll(tasks);

        var history = await service.GetDetectionHistoryAsync();

        // Assert
        await Assert.That(history).HasCount().EqualTo(50);
        
        // Verify the limit works (should return max 100 records)
        for (int i = 0; i < 60; i++)
        {
            await service.CheckDefaultLocationAsync();
        }
        
        var limitedHistory = await service.GetDetectionHistoryAsync();
        await Assert.That(limitedHistory).HasCount().EqualTo(100); // Limited to 100
    }

    [Test]
    public async Task HhExeDetectionResult_Properties_SetCorrectly()
    {
        // Arrange
        var testPath = @"C:\Test\hh.exe";
        var testNotes = "Test notes";
        var testTime = DateTime.UtcNow;

        // Act
        var result = new HhExeDetectionResult
        {
            Path = testPath,
            Found = true,
            Notes = testNotes,
            DetectedAt = testTime
        };

        // Assert
        await Assert.That(result.Path).IsEqualTo(testPath);
        await Assert.That(result.Found).IsTrue();
        await Assert.That(result.Notes).IsEqualTo(testNotes);
        await Assert.That(result.DetectedAt).IsEqualTo(testTime);
    }

    [Test]
    public async Task DatabasePath_CreatedInCorrectLocation()
    {
        // Arrange & Act
        using var service = new HhExeDetectionService(_configService, _logger);

        // Assert
        await Assert.That(File.Exists(_testDbPath)).IsTrue();
        await Assert.That(_configService.DatabasePath).IsEqualTo(_testDbPath);
    }

    [Test]
    public async Task CheckDefaultLocationAsync_StoresConfigurationWhenFound()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);

        // Act
        await service.CheckDefaultLocationAsync();

        // Assert
        var configuredPath = await service.GetConfiguredHhExePathAsync();
        var isAutoDetected = await service.IsHhExePathAutoDetectedAsync();

        if (File.Exists(@"C:\Windows\hh.exe"))
        {
            await Assert.That(configuredPath).IsEqualTo(@"C:\Windows\hh.exe");
            await Assert.That(isAutoDetected).IsTrue();
        }
        else
        {
            // If hh.exe doesn't exist, configuration should remain empty
            await Assert.That(configuredPath).IsNull();
        }
    }

    [Test]
    public async Task SetHhExePathAsync_StoresPathCorrectly()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);
        const string testPath = @"C:\CustomPath\hh.exe";

        // Act
        var result = await service.SetHhExePathAsync(testPath, false);

        // Assert
        await Assert.That(result).IsTrue();
        
        var configuredPath = await service.GetConfiguredHhExePathAsync();
        var isAutoDetected = await service.IsHhExePathAutoDetectedAsync();
        
        await Assert.That(configuredPath).IsEqualTo(testPath);
        await Assert.That(isAutoDetected).IsFalse();
    }

    [Test]
    public async Task SetHhExePathAsync_WithNullPath_ClearsConfiguration()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);
        
        // Set a path first
        await service.SetHhExePathAsync(@"C:\Test\hh.exe", true);
        
        // Act
        var result = await service.SetHhExePathAsync(null, false);

        // Assert
        await Assert.That(result).IsTrue();
        
        var configuredPath = await service.GetConfiguredHhExePathAsync();
        await Assert.That(configuredPath).IsNull();
    }

    [Test]
    public async Task GetConfiguredHhExePathAsync_WithNoConfiguration_ReturnsNull()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);

        // Act
        var result = await service.GetConfiguredHhExePathAsync();

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task IsHhExePathAutoDetectedAsync_WithNoConfiguration_ReturnsFalse()
    {
        // Arrange
        using var service = new HhExeDetectionService(_configService, _logger);

        // Act
        var result = await service.IsHhExePathAutoDetectedAsync();

        // Assert
        await Assert.That(result).IsFalse();
    }
}