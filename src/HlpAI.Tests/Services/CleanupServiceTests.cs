using Microsoft.Extensions.Logging;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;

namespace HlpAI.Tests.Services;

public class CleanupServiceTests
{
    private string _testDirectory = null!;
    private string _testDbPath = null!;
    private CleanupService _cleanupService = null!;
    private ILogger _logger = null!;
    private string? _originalUserProfile;
    private SqliteConfigurationService _configService = null!;

    [Before(Test)]
    public void Setup()
    {
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE");
        _testDirectory = FileTestHelper.CreateTempDirectory($"cleanup_tests_{Guid.NewGuid():N}");
        
        // Create isolated test database for this test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_cleanup_{Guid.NewGuid()}.db");
        
        // Set test environment
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<CleanupServiceTests>();
        _configService = new SqliteConfigurationService(_testDbPath, _logger);
        _cleanupService = new CleanupService(_logger, _configService);
    }

    [After(Test)]
    public void Cleanup()
    {
        // Restore original user profile
        Environment.SetEnvironmentVariable("USERPROFILE", _originalUserProfile);
        
        _cleanupService?.Dispose();
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
    public async Task Constructor_WithLogger_InitializesCorrectly()
    {
        // Act
        using var service = new CleanupService(_logger);

        // Assert
        await Assert.That(service).IsNotNull();
    }

    [Test]
    public async Task Constructor_WithoutLogger_InitializesCorrectly()
    {
        // Act
        using var service = new CleanupService();

        // Assert
        await Assert.That(service).IsNotNull();
    }

    [Test]
    public async Task PerformCleanupAsync_WithDefaultOptions_ReturnsSuccessfulResult()
    {
        // Arrange
        var options = new CleanupOptions();

        // Act
        var result = await _cleanupService.PerformCleanupAsync(options);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.StartedAt).IsLessThanOrEqualTo(DateTime.UtcNow);
        await Assert.That(result.CompletedAt).IsGreaterThanOrEqualTo(result.StartedAt);
        await Assert.That(result.Duration).IsGreaterThanOrEqualTo(TimeSpan.Zero);
        await Assert.That(result.Options).IsEqualTo(options);
        await Assert.That(result.Details).IsNotNull();
    }

    [Test]
    public async Task PerformCleanupAsync_WithAllOptionsEnabled_ExecutesAllCleanupOperations()
    {
        // Arrange
        var tempDbPath = Path.Combine(_testDirectory, "test_vector.db");
        var options = new CleanupOptions
        {
            CleanVectorDatabase = true,
            CleanErrorLogs = true,
            CleanExportLogs = true,
            CleanTempFiles = true,
            CleanOutdatedCache = true,
            OptimizeDatabase = true,
            VectorDatabasePath = tempDbPath
        };

        // Create a test vector database file
        await File.WriteAllTextAsync(tempDbPath, "test data");

        // Act
        var result = await _cleanupService.PerformCleanupAsync(options);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Details.ContainsKey("Vector Database")).IsTrue();
        await Assert.That(result.Details.ContainsKey("Error Logs")).IsTrue();
        await Assert.That(result.Details.ContainsKey("Export Logs")).IsTrue();
        await Assert.That(result.Details.ContainsKey("Temporary Files")).IsTrue();
        await Assert.That(result.Details.ContainsKey("Cache")).IsTrue();
        await Assert.That(result.Details.ContainsKey("Database Optimization")).IsTrue();
    }

    [Test]
    public async Task PerformCleanupAsync_WithVectorDatabaseCleanup_CleansVectorDatabase()
    {
        // Arrange
        var tempDbPath = Path.Combine(_testDirectory, "test_vector.db");
        await File.WriteAllTextAsync(tempDbPath, "test vector data");
        
        var options = new CleanupOptions
        {
            CleanVectorDatabase = true,
            VectorDatabasePath = tempDbPath,
            CleanErrorLogs = false,
            CleanExportLogs = false,
            CleanTempFiles = false,
            CleanOutdatedCache = false,
            OptimizeDatabase = false
        };

        // Act
        var result = await _cleanupService.PerformCleanupAsync(options);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.VectorDatabaseCleaned).IsTrue();
        await Assert.That(result.Details.ContainsKey("Vector Database")).IsTrue();
        await Assert.That(File.Exists(tempDbPath)).IsFalse();
    }

    [Test]
    public async Task PerformCleanupAsync_WithErrorLogsCleanup_CleansErrorLogs()
    {
        // Arrange
        var options = new CleanupOptions
        {
            CleanErrorLogs = true,
            ErrorLogRetentionDays = 1,
            CleanVectorDatabase = false,
            CleanExportLogs = false,
            CleanTempFiles = false,
            CleanOutdatedCache = false,
            OptimizeDatabase = false
        };

        // Act
        var result = await _cleanupService.PerformCleanupAsync(options);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ErrorLogsCleaned).IsTrue();
        await Assert.That(result.Details.ContainsKey("Error Logs")).IsTrue();
    }

    [Test]
    public async Task PerformCleanupAsync_WithExportLogsCleanup_CleansExportLogs()
    {
        // Arrange
        var options = new CleanupOptions
        {
            CleanExportLogs = true,
            ExportLogRetentionDays = 1,
            CleanVectorDatabase = false,
            CleanErrorLogs = false,
            CleanTempFiles = false,
            CleanOutdatedCache = false,
            OptimizeDatabase = false
        };

        // Act
        var result = await _cleanupService.PerformCleanupAsync(options);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ExportLogsCleaned).IsTrue();
        await Assert.That(result.Details.ContainsKey("Export Logs")).IsTrue();
    }

    [Test]
    public async Task PerformCleanupAsync_WithTempFilesCleanup_CleansTempFiles()
    {
        // Arrange
        var tempDir = Path.Combine(_testDirectory, "temp");
        Directory.CreateDirectory(tempDir);
        
        // Create some test temp files
        var tempFile1 = Path.Combine(tempDir, "test.tmp");
        var tempFile2 = Path.Combine(tempDir, "old.temp");
        await File.WriteAllTextAsync(tempFile1, "temp data 1");
        await File.WriteAllTextAsync(tempFile2, "temp data 2");
        
        // Make files old
        File.SetLastWriteTime(tempFile1, DateTime.UtcNow.AddHours(-25));
        File.SetLastWriteTime(tempFile2, DateTime.UtcNow.AddHours(-25));
        
        var options = new CleanupOptions
        {
            CleanTempFiles = true,
            TempFileAgeHours = 24,
            CleanVectorDatabase = false,
            CleanErrorLogs = false,
            CleanExportLogs = false,
            CleanOutdatedCache = false,
            OptimizeDatabase = false
        };

        // Act
        var result = await _cleanupService.PerformCleanupAsync(options);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.TempFilesCleaned).IsTrue();
        await Assert.That(result.Details.ContainsKey("Temporary Files")).IsTrue();
    }

    [Test]
    public async Task PerformCleanupAsync_WithCacheCleanup_CleansOutdatedCache()
    {
        // Arrange
        var options = new CleanupOptions
        {
            CleanOutdatedCache = true,
            CacheRetentionDays = 1,
            CleanVectorDatabase = false,
            CleanErrorLogs = false,
            CleanExportLogs = false,
            CleanTempFiles = false,
            OptimizeDatabase = false
        };

        // Act
        var result = await _cleanupService.PerformCleanupAsync(options);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.CacheCleaned).IsTrue();
        await Assert.That(result.Details.ContainsKey("Cache")).IsTrue();
    }

    [Test]
    public async Task PerformCleanupAsync_WithDatabaseOptimization_OptimizesDatabase()
    {
        // Arrange
        var options = new CleanupOptions
        {
            OptimizeDatabase = true,
            CleanVectorDatabase = false,
            CleanErrorLogs = false,
            CleanExportLogs = false,
            CleanTempFiles = false,
            CleanOutdatedCache = false
        };

        // Act
        var result = await _cleanupService.PerformCleanupAsync(options);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.DatabaseOptimized).IsTrue();
        await Assert.That(result.Details.ContainsKey("Database Optimization")).IsTrue();
    }

    [Test]
    public async Task GetCleanupStatisticsAsync_ReturnsValidStatistics()
    {
        // Act
        var stats = await _cleanupService.GetCleanupStatisticsAsync();

        // Assert
        await Assert.That(stats).IsNotNull();
        await Assert.That(stats.LastCalculated).IsLessThanOrEqualTo(DateTime.UtcNow);
        await Assert.That(stats.VectorDatabaseSize).IsGreaterThanOrEqualTo(0);
        await Assert.That(stats.ConfigurationDatabaseSize).IsGreaterThanOrEqualTo(0);
        await Assert.That(stats.ErrorLogCount).IsGreaterThanOrEqualTo(0);
        await Assert.That(stats.ExportLogCount).IsGreaterThanOrEqualTo(0);
        await Assert.That(stats.TempFileCount).IsGreaterThanOrEqualTo(0);
        await Assert.That(stats.TempFileSize).IsGreaterThanOrEqualTo(0);
        await Assert.That(stats.CacheEntryCount).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task GetCleanupHistoryAsync_ReturnsCleanupHistory()
    {
        // Arrange - Perform a cleanup to create history
        var options = new CleanupOptions { CleanTempFiles = true };
        var cleanupResult = await _cleanupService.PerformCleanupAsync(options);
        
        // Verify the cleanup operation itself succeeded
        await Assert.That(cleanupResult.Success).IsTrue();
        await Assert.That(cleanupResult.ErrorMessage).IsNull();

        // Act
        var history = await _cleanupService.GetCleanupHistoryAsync(10);

        // Assert
        await Assert.That(history).IsNotNull();
        await Assert.That(history.Count).IsGreaterThanOrEqualTo(1);
        
        var latestEntry = history.First();
        await Assert.That(latestEntry.Success).IsTrue();
        await Assert.That(latestEntry.Options).IsNotNull();
    }

    [Test]
    public void Dispose_DisposesResourcesCorrectly()
    {
        // Arrange
        var service = new CleanupService(_logger);

        // Act & Assert - Should not throw
        service.Dispose();
        
        // Should be able to dispose multiple times
        service.Dispose();
    }

    [Test]
    public async Task CleanupOptions_DefaultValues_AreCorrect()
    {
        // Act
        var options = new CleanupOptions();

        // Assert
        await Assert.That(options.CleanVectorDatabase).IsFalse();
        await Assert.That(options.CleanErrorLogs).IsTrue();
        await Assert.That(options.CleanExportLogs).IsTrue();
        await Assert.That(options.CleanTempFiles).IsTrue();
        await Assert.That(options.CleanOutdatedCache).IsTrue();
        await Assert.That(options.OptimizeDatabase).IsTrue();
        await Assert.That(options.ErrorLogRetentionDays).IsEqualTo(30);
        await Assert.That(options.ExportLogRetentionDays).IsEqualTo(90);
        await Assert.That(options.TempFileAgeHours).IsEqualTo(24);
        await Assert.That(options.CacheRetentionDays).IsEqualTo(7);
    }

    [Test]
    public async Task CleanupResult_InitialState_IsCorrect()
    {
        // Act
        var result = new CleanupResult();

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Options).IsNotNull();
        await Assert.That(result.Details).IsNotNull();
        await Assert.That(result.Details.Count).IsEqualTo(0);
        await Assert.That(result.VectorDatabaseCleaned).IsFalse();
        await Assert.That(result.ErrorLogsCleaned).IsFalse();
        await Assert.That(result.ExportLogsCleaned).IsFalse();
        await Assert.That(result.TempFilesCleaned).IsFalse();
        await Assert.That(result.CacheCleaned).IsFalse();
        await Assert.That(result.DatabaseOptimized).IsFalse();
    }

    [Test]
    public async Task CleanupOperationResult_InitialState_IsCorrect()
    {
        // Act
        var result = new CleanupOperationResult();

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Message).IsEqualTo(string.Empty);
        await Assert.That(result.ItemsProcessed).IsEqualTo(0);
        await Assert.That(result.SpaceFreed).IsEqualTo(0);
    }

    [Test]
    public async Task CleanupStatistics_InitialState_IsCorrect()
    {
        // Act
        var stats = new CleanupStatistics();

        // Assert
        await Assert.That(stats.VectorDatabaseSize).IsEqualTo(0);
        await Assert.That(stats.ConfigurationDatabaseSize).IsEqualTo(0);
        await Assert.That(stats.ErrorLogCount).IsEqualTo(0);
        await Assert.That(stats.ExportLogCount).IsEqualTo(0);
        await Assert.That(stats.TempFileCount).IsEqualTo(0);
        await Assert.That(stats.TempFileSize).IsEqualTo(0);
        await Assert.That(stats.CacheEntryCount).IsEqualTo(0);
        await Assert.That(stats.OldestErrorLog).IsNull();
        await Assert.That(stats.NewestErrorLog).IsNull();
        await Assert.That(stats.LastCalculated).IsEqualTo(default(DateTime));
    }

    [Test]
    public async Task PerformCleanupAsync_WithNonExistentVectorDatabase_HandlesGracefully()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.db");
        var options = new CleanupOptions
        {
            CleanVectorDatabase = true,
            VectorDatabasePath = nonExistentPath,
            CleanErrorLogs = false,
            CleanExportLogs = false,
            CleanTempFiles = false,
            CleanOutdatedCache = false,
            OptimizeDatabase = false
        };

        // Act
        var result = await _cleanupService.PerformCleanupAsync(options);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.VectorDatabaseCleaned).IsTrue();
        await Assert.That(result.Details.ContainsKey("Vector Database")).IsTrue();
    }

    [Test]
    public async Task PerformCleanupAsync_WithCustomRetentionPeriods_UsesCorrectValues()
    {
        // Arrange
        var options = new CleanupOptions
        {
            CleanErrorLogs = true,
            CleanExportLogs = true,
            CleanTempFiles = true,
            CleanOutdatedCache = true,
            ErrorLogRetentionDays = 15,
            ExportLogRetentionDays = 45,
            TempFileAgeHours = 12,
            CacheRetentionDays = 3,
            CleanVectorDatabase = false,
            OptimizeDatabase = false
        };

        // Act
        var result = await _cleanupService.PerformCleanupAsync(options);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ErrorLogsCleaned).IsTrue();
        await Assert.That(result.ExportLogsCleaned).IsTrue();
        await Assert.That(result.TempFilesCleaned).IsTrue();
        await Assert.That(result.CacheCleaned).IsTrue();
    }
}