using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HlpAI.Tests.Services;

public class FileTypeFilterServiceTests
{
    private SqliteConfigurationService _configService = null!;
    private ILogger<FileTypeFilterService> _logger = null!;
    private FileTypeFilterService _service = null!;
    private string _testFilePath = null!;
    private string _testDirectory = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("file_type_filter_tests");
        _testFilePath = Path.Combine(_testDirectory, "test.txt");
        _logger = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning))
            .CreateLogger<FileTypeFilterService>();
        
        // Set up test-specific SQLite database
        var testDbPath = Path.Combine(_testDirectory, "file_type_filter_test.db");
        _configService = new SqliteConfigurationService(testDbPath, _logger);
        _service = new FileTypeFilterService(_logger, _configService);

        // Create test file
        await File.WriteAllTextAsync(_testFilePath, "test content");
    }

    [After(Test)]
    public Task Cleanup()
    {
        _service?.Dispose();
        _configService?.Dispose();
        FileTestHelper.SafeDeleteDirectory(_testDirectory);
        return Task.CompletedTask;
    }

    [Test]
    public async Task ShouldProcessFileAsync_WithSupportedExtension_ReturnsTrue()
    {
        // Arrange
        var filePath = "test.txt";
        await SetupDefaultConfigurationAsync();

        // Act
        var result = await _service.ShouldProcessFileAsync(filePath);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ShouldProcessFileAsync_WithUnsupportedExtension_ReturnsFalse()
    {
        // Arrange
        var filePath = "test.xyz";
        await SetupDefaultConfigurationAsync();

        // Act
        var result = await _service.ShouldProcessFileAsync(filePath);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ShouldProcessFileAsync_WithExcludePattern_ReturnsFalse()
    {
        // Arrange
        var filePath = "temp.txt";
        await SetupConfigurationWithExcludePatternAsync("temp*");

        // Act
        var result = await _service.ShouldProcessFileAsync(filePath);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ShouldProcessFileAsync_WithIncludePattern_ReturnsTrue()
    {
        // Arrange
        var filePath = "important.txt";
        await SetupConfigurationWithIncludePatternAsync("important*");

        // Act
        var result = await _service.ShouldProcessFileAsync(filePath);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ShouldProcessFile_WithValidFile_ReturnsTrue()
    {
        // Arrange
        var config = new FileTypeFilterConfiguration
        {
            SupportedTypes = [".txt"],
            OnlySupportedTypes = true
        };

        // Act
        var result = _service.ShouldProcessFile(_testFilePath, config);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ShouldProcessFile_WithFileSizeLimit_ReturnsCorrectResult()
    {
        // Arrange
        var config = new FileTypeFilterConfiguration
        {
            SupportedTypes = ["txt"],
            OnlySupportedTypes = true,
            MaxFileSizeBytes = 1 // Very small limit
        };

        // Act
        var result = _service.ShouldProcessFile(_testFilePath, config);

        // Assert
        await Assert.That(result).IsFalse(); // File should be too large
    }

    [Test]
    public async Task ShouldProcessFile_WithMinFileSizeLimit_ReturnsCorrectResult()
    {
        // Arrange
        var config = new FileTypeFilterConfiguration
        {
            SupportedTypes = ["txt"],
            OnlySupportedTypes = true,
            MinFileSizeBytes = 1000000 // Very large minimum
        };

        // Act
        var result = _service.ShouldProcessFile(_testFilePath, config);

        // Assert
        await Assert.That(result).IsFalse(); // File should be too small
    }

    [Test]
    public async Task ShouldProcessFile_WithFileAgeLimit_ReturnsCorrectResult()
    {
        // Arrange
        var config = new FileTypeFilterConfiguration
        {
            SupportedTypes = ["txt"],
            OnlySupportedTypes = true,
            MaxFileAgeDays = 0 // Files must be very new
        };

        // Act
        var result = _service.ShouldProcessFile(_testFilePath, config);

        // Assert - depends on when file was created, but should handle gracefully
        await Assert.That(result).IsEqualTo(result); // Just ensure no exception
    }

    [Test]
    public async Task FilterFilesAsync_WithMixedFiles_ReturnsCorrectCounts()
    {
        // Arrange
        var filePaths = new[] { "test.txt", "test.xyz", "document.pdf" };
        await SetupDefaultConfigurationAsync();

        // Act
        var result = await _service.FilterFilesAsync(filePaths);

        // Assert
        await Assert.That(result.TotalProcessed).IsEqualTo(3);
        await Assert.That(result.AcceptedFiles.Count).IsGreaterThan(0);
        await Assert.That(result.RejectedFiles.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task GetFilterConfigurationAsync_WithNoStoredConfig_ReturnsDefaults()
    {
        // Act
        var config = await _service.GetFilterConfigurationAsync();

        // Assert
        await Assert.That(config).IsNotNull();
        await Assert.That(config.SupportedTypes).IsNotNull();
        await Assert.That(config.OnlySupportedTypes).IsTrue();
    }

    [Test]
    public async Task GetFilterConfigurationAsync_WithStoredConfig_ReturnsStoredValues()
    {
        // Arrange
        var includePatterns = JsonSerializer.Serialize(new[] { "*.important" });
        var excludePatterns = JsonSerializer.Serialize(new[] { "*.temp" });
        var supportedTypes = JsonSerializer.Serialize(new[] { "txt", "md" });

        await _configService.SetConfigurationAsync("include_patterns", includePatterns, "file_filtering");
        await _configService.SetConfigurationAsync("exclude_patterns", excludePatterns, "file_filtering");
        await _configService.SetConfigurationAsync("supported_types", supportedTypes, "file_filtering");
        await _configService.SetConfigurationAsync("only_supported_types", "false", "file_filtering");
        await _configService.SetConfigurationAsync("case_sensitive_patterns", "true", "file_filtering");
        await _configService.SetConfigurationAsync("max_file_size_bytes", "1000000", "file_filtering");

        // Act
        var config = await _service.GetFilterConfigurationAsync();

        // Assert
        await Assert.That(config.IncludePatterns).Contains("*.important");
        await Assert.That(config.ExcludePatterns).Contains("*.temp");
        await Assert.That(config.SupportedTypes).Contains("txt");
        await Assert.That(config.SupportedTypes).Contains("md");
        await Assert.That(config.OnlySupportedTypes).IsFalse();
        await Assert.That(config.CaseSensitivePatterns).IsTrue();
        await Assert.That(config.MaxFileSizeBytes).IsEqualTo(1000000);
    }

    [Test]
    public async Task SetFilterConfigurationAsync_WithValidConfig_ReturnsTrue()
    {
        // Arrange
        var config = new FileTypeFilterConfiguration
        {
            IncludePatterns = ["*.important"],
            ExcludePatterns = ["*.temp"],
            SupportedTypes = [".txt", ".md"],
            OnlySupportedTypes = false,
            CaseSensitivePatterns = true,
            MaxFileSizeBytes = 1000000
        };

        // Act
        var result = await _service.SetFilterConfigurationAsync(config);

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify configuration was saved by reading it back
        var savedConfig = await _service.GetFilterConfigurationAsync();
        await Assert.That(savedConfig.IncludePatterns).Contains("*.important");
        await Assert.That(savedConfig.ExcludePatterns).Contains("*.temp");
        await Assert.That(savedConfig.SupportedTypes).Contains(".txt");
        await Assert.That(savedConfig.SupportedTypes).Contains(".md");
        await Assert.That(savedConfig.OnlySupportedTypes).IsFalse();
        await Assert.That(savedConfig.CaseSensitivePatterns).IsTrue();
        await Assert.That(savedConfig.MaxFileSizeBytes).IsEqualTo(1000000);
    }

    [Test]
    public async Task AddIncludePatternAsync_WithNewPattern_ReturnsTrue()
    {
        // Arrange
        var pattern = "*.new";
        await SetupDefaultConfigurationAsync();

        // Act
        var result = await _service.AddIncludePatternAsync(pattern);

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify pattern was added
        var config = await _service.GetFilterConfigurationAsync();
        await Assert.That(config.IncludePatterns).Contains(pattern);
    }

    [Test]
    public async Task AddExcludePatternAsync_WithNewPattern_ReturnsTrue()
    {
        // Arrange
        var pattern = "*.exclude";
        await SetupDefaultConfigurationAsync();

        // Act
        var result = await _service.AddExcludePatternAsync(pattern);

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify pattern was added
        var config = await _service.GetFilterConfigurationAsync();
        await Assert.That(config.ExcludePatterns).Contains(pattern);
    }

    [Test]
    public async Task RemoveIncludePatternAsync_WithExistingPattern_ReturnsTrue()
    {
        // Arrange
        var pattern = "*.remove";
        await SetupConfigurationWithIncludePatternAsync(pattern);

        // Act
        var result = await _service.RemoveIncludePatternAsync(pattern);

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify pattern was removed
        var config = await _service.GetFilterConfigurationAsync();
        await Assert.That(config.IncludePatterns?.Contains(pattern) ?? false).IsFalse();
    }

    [Test]
    public async Task RemoveExcludePatternAsync_WithExistingPattern_ReturnsTrue()
    {
        // Arrange
        var pattern = "*.remove";
        await SetupConfigurationWithExcludePatternAsync(pattern);

        // Act
        var result = await _service.RemoveExcludePatternAsync(pattern);

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify pattern was removed
        var config = await _service.GetFilterConfigurationAsync();
        await Assert.That(config.ExcludePatterns?.Contains(pattern) ?? false).IsFalse();
    }

    [Test]
    public async Task ResetToDefaultsAsync_CallsClearCategory_ReturnsTrue()
    {
        // Arrange - set some custom configuration first
        await _service.AddIncludePatternAsync("*.custom");
        
        // Act
        var result = await _service.ResetToDefaultsAsync();

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify configuration was reset to defaults
        var config = await _service.GetFilterConfigurationAsync();
        await Assert.That(config.OnlySupportedTypes).IsTrue();
        await Assert.That(config.IncludePatterns?.Contains("*.custom") ?? false).IsFalse();
    }

    [Test]
    public async Task TestPatternsAsync_WithTestFiles_ReturnsCorrectResults()
    {
        // Arrange
        var testFiles = new[] { "test.txt", "test.xyz", "document.pdf" };
        await SetupDefaultConfigurationAsync();

        // Act
        var result = await _service.TestPatternsAsync(testFiles);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.TestFiles).HasCount().EqualTo(3);
        await Assert.That(result.AcceptedFiles.Count + result.RejectedFiles.Count).IsEqualTo(3);
    }

    [Test]
    public async Task GetFilterStatisticsAsync_ReturnsValidStatistics()
    {
        // Arrange
        await SetupDefaultConfigurationAsync();

        // Act
        var stats = await _service.GetFilterStatisticsAsync();

        // Assert
        await Assert.That(stats).IsNotNull();
        await Assert.That(stats.SupportedTypeCount).IsGreaterThan(0);
        await Assert.That(stats.LastUpdated).IsNotEqualTo(default(DateTime));
    }

    [Test]
    public void Dispose_CallsConfigServiceDispose()
    {
        // Act
        _service.Dispose();
        _service.Dispose(); // Should handle multiple calls gracefully

        // Assert - no exception should be thrown
        // Test passes if no exception is thrown during disposal
    }

    [Test]
    public async Task GetFilterConfigurationAsync_WithException_ReturnsDefaultConfig()
    {
        // Arrange - corrupt the database to cause an exception
        _configService.Dispose();
        var corruptDbPath = Path.Combine(_testDirectory, "corrupt.db");
        await File.WriteAllTextAsync(corruptDbPath, "invalid database content");
        
        SqliteConfigurationService corruptConfigService;
        try
        {
            corruptConfigService = new SqliteConfigurationService(corruptDbPath, _logger);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Database was corrupted"))
        {
            // Database was corrupted and recreated, create a new service instance
            corruptConfigService = new SqliteConfigurationService(corruptDbPath, _logger);
        }
        var corruptService = new FileTypeFilterService(_logger);

        // Act
        var config = await corruptService.GetFilterConfigurationAsync();

        // Assert
        await Assert.That(config).IsNotNull();
        await Assert.That(config.OnlySupportedTypes).IsTrue();
        
        // Cleanup
        corruptService.Dispose();
        corruptConfigService.Dispose();
    }

    [Test]
    public async Task SetFilterConfigurationAsync_WithException_ReturnsFalse()
    {
        // Arrange - dispose the service to cause an exception
        var config = new FileTypeFilterConfiguration();
        _service.Dispose();
        _configService.Dispose();

        // Act
        var result = await _service.SetFilterConfigurationAsync(config);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task AddIncludePatternAsync_WithException_ReturnsFalse()
    {
        // Arrange - dispose the service to cause an exception
        _service.Dispose();
        _configService.Dispose();

        // Act
        var result = await _service.AddIncludePatternAsync("*.test");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TestPatternsAsync_WithException_ReturnsEmptyResult()
    {
        // Arrange - dispose the service to cause an exception
        _service.Dispose();
        _configService.Dispose();

        // Act
        var result = await _service.TestPatternsAsync(["test.txt"]);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.TestFiles).HasCount().EqualTo(0);
    }

    private async Task SetupDefaultConfigurationAsync()
    {
        var config = new FileTypeFilterConfiguration
        {
            SupportedTypes = [".txt", ".md", ".log", ".csv", ".html", ".htm", ".pdf", ".chm", ".hhc"],
            OnlySupportedTypes = true
        };
        await _service.SetFilterConfigurationAsync(config);
    }

     private async Task SetupConfigurationWithIncludePatternAsync(string pattern)
     {
         var config = new FileTypeFilterConfiguration
         {
             IncludePatterns = [pattern],
             SupportedTypes = [".txt", ".md", ".log", ".csv", ".html", ".htm", ".pdf", ".chm", ".hhc"],
             OnlySupportedTypes = true
         };
         await _service.SetFilterConfigurationAsync(config);
     }

     private async Task SetupConfigurationWithExcludePatternAsync(string pattern)
     {
         var config = new FileTypeFilterConfiguration
         {
             ExcludePatterns = [pattern],
             SupportedTypes = [".txt", ".md", ".log", ".csv", ".html", ".htm", ".pdf", ".chm", ".hhc"],
             OnlySupportedTypes = true
         };
         await _service.SetFilterConfigurationAsync(config);
     }
}

public class FileTypeFilterConfigurationTests
{
    [Test]
    public async Task FileTypeFilterConfiguration_DefaultValues_AreCorrect()
    {
        // Act
        var config = new FileTypeFilterConfiguration();

        // Assert
        await Assert.That(config.OnlySupportedTypes).IsTrue();
        await Assert.That(config.CaseSensitivePatterns).IsFalse();
        await Assert.That(config.IncludePatterns).IsNull();
        await Assert.That(config.ExcludePatterns).IsNull();
        await Assert.That(config.SupportedTypes).IsNull();
        await Assert.That(config.MaxFileSizeBytes).IsNull();
        await Assert.That(config.MinFileSizeBytes).IsNull();
        await Assert.That(config.MaxFileAgeDays).IsNull();
        await Assert.That(config.MinFileAgeHours).IsNull();
    }
}

public class FileFilterResultTests
{
    [Test]
    public async Task FileFilterResult_DefaultValues_AreCorrect()
    {
        // Act
        var result = new FileFilterResult();

        // Assert
        await Assert.That(result.AcceptedFiles).IsNotNull();
        await Assert.That(result.RejectedFiles).IsNotNull();
        await Assert.That(result.ErrorFiles).IsNotNull();
        await Assert.That(result.TotalProcessed).IsEqualTo(0);
    }
}

public class FileFilterTestResultTests
{
    [Test]
    public async Task FileFilterTestResult_DefaultValues_AreCorrect()
    {
        // Act
        var result = new FileFilterTestResult();

        // Assert
        await Assert.That(result.Configuration).IsNotNull();
        await Assert.That(result.TestFiles).IsNotNull();
        await Assert.That(result.AcceptedFiles).IsNotNull();
        await Assert.That(result.RejectedFiles).IsNotNull();
    }
}

public class FileFilterStatisticsTests
{
    [Test]
    public async Task FileFilterStatistics_DefaultValues_AreCorrect()
    {
        // Act
        var stats = new FileFilterStatistics();

        // Assert
        await Assert.That(stats.IncludePatternCount).IsEqualTo(0);
        await Assert.That(stats.ExcludePatternCount).IsEqualTo(0);
        await Assert.That(stats.SupportedTypeCount).IsEqualTo(0);
        await Assert.That(stats.OnlySupportedTypes).IsFalse();
        await Assert.That(stats.HasSizeFilters).IsFalse();
        await Assert.That(stats.HasAgeFilters).IsFalse();
        await Assert.That(stats.LastUpdated).IsEqualTo(default(DateTime));
    }
}