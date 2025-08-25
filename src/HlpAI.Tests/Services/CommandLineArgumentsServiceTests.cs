using HlpAI.MCP;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Services;

public class CommandLineArgumentsServiceTests
{
    private string _testDirectory = null!;
    private ILogger<CommandLineArgumentsServiceTests> _logger = null!;
    private string _originalUserProfile = null!;
    private SqliteConfigurationService _configService = null!;
    private string _testDbPath = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("cmd_args_service_tests");
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<CommandLineArgumentsServiceTests>();
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        // Create isolated test database for this test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_cmd_args_{Guid.NewGuid()}.db");
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
    public async Task ParseArguments_WithBasicFlags_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "--enable-logging", "--show-log-stats" };

        // Act
        var service = new CommandLineArgumentsService(args, _logger);

        // Assert
        await Assert.That(service.HasArgument("enable-logging")).IsTrue();
        await Assert.That(service.HasArgument("show-log-stats")).IsTrue();
        await Assert.That(service.GetBooleanArgument("enable-logging")).IsTrue();
        await Assert.That(service.GetBooleanArgument("show-log-stats")).IsTrue();
    }

    [Test]
    public async Task ParseArguments_WithValueArguments_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "--log-level", "Error", "--log-retention-days", "14" };

        // Act
        var service = new CommandLineArgumentsService(args, _logger);

        // Assert
        await Assert.That(service.GetArgument("log-level")).IsEqualTo("Error");
        await Assert.That(service.GetIntegerArgument("log-retention-days")).IsEqualTo(14);
        await Assert.That(service.GetLogLevelArgument("log-level")).IsEqualTo(LogLevel.Error);
    }

    [Test]
    public async Task ParseArguments_WithPositionalArguments_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "C:\\MyDocs", "llama3.2", "hybrid", "--log-level", "Warning" };

        // Act
        var service = new CommandLineArgumentsService(args, _logger);

        // Assert
        await Assert.That(service.HasPositionalArguments()).IsTrue();
        var positional = service.GetPositionalArguments();
        await Assert.That(positional.Count).IsEqualTo(3);
        await Assert.That(positional[0]).IsEqualTo("C:\\MyDocs");
        await Assert.That(positional[1]).IsEqualTo("llama3.2");
        await Assert.That(positional[2]).IsEqualTo("hybrid");
        await Assert.That(service.GetLogLevelArgument("log-level")).IsEqualTo(LogLevel.Warning);
    }

    [Test]
    public async Task ParseArguments_WithSingleCharacterFlags_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "-h", "-v", "verbose" };

        // Act
        var service = new CommandLineArgumentsService(args, _logger);

        // Assert
        await Assert.That(service.HasArgument("h")).IsTrue();
        await Assert.That(service.GetBooleanArgument("h")).IsTrue();
        await Assert.That(service.GetArgument("v")).IsEqualTo("verbose");
    }

    [Test]
    public async Task ParseArguments_WithMixedCase_IsCaseInsensitive()
    {
        // Arrange
        var args = new[] { "--Enable-Logging", "--LOG-LEVEL", "ERROR" };

        // Act
        var service = new CommandLineArgumentsService(args, _logger);

        // Assert
        await Assert.That(service.HasArgument("enable-logging")).IsTrue();
        await Assert.That(service.HasArgument("ENABLE-LOGGING")).IsTrue();
        await Assert.That(service.GetLogLevelArgument("log-level")).IsEqualTo(LogLevel.Error);
    }

    [Test]
    public async Task GetBooleanArgument_WithVariousValues_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { 
            "--flag1", "true", 
            "--flag2", "false", 
            "--flag3", "1", 
            "--flag4", "0", 
            "--flag5", "yes", 
            "--flag6", "no",
            "--flag7" // boolean flag without value
        };

        // Act
        var service = new CommandLineArgumentsService(args, _logger);

        // Assert
        await Assert.That(service.GetBooleanArgument("flag1")).IsTrue();
        await Assert.That(service.GetBooleanArgument("flag2")).IsFalse();
        await Assert.That(service.GetBooleanArgument("flag3")).IsTrue();
        await Assert.That(service.GetBooleanArgument("flag4")).IsFalse();
        await Assert.That(service.GetBooleanArgument("flag5")).IsTrue();
        await Assert.That(service.GetBooleanArgument("flag6")).IsFalse();
        await Assert.That(service.GetBooleanArgument("flag7")).IsTrue();
    }

    [Test]
    public async Task GetIntegerArgument_WithValidAndInvalidValues_HandlesCorrectly()
    {
        // Arrange
        var args = new[] { "--number1", "42", "--number2", "invalid", "--number3", "-5" };

        // Act
        var service = new CommandLineArgumentsService(args, _logger);

        // Assert
        await Assert.That(service.GetIntegerArgument("number1")).IsEqualTo(42);
        await Assert.That(service.GetIntegerArgument("number2", 100)).IsEqualTo(100); // Default for invalid
        await Assert.That(service.GetIntegerArgument("number3")).IsEqualTo(-5);
        await Assert.That(service.GetIntegerArgument("nonexistent", 999)).IsEqualTo(999);
    }

    [Test]
    public async Task GetLogLevelArgument_WithVariousLevels_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { 
            "--level1", "Error", 
            "--level2", "warning", 
            "--level3", "INFORMATION",
            "--level4", "invalid"
        };

        // Act
        var service = new CommandLineArgumentsService(args, _logger);

        // Assert
        await Assert.That(service.GetLogLevelArgument("level1")).IsEqualTo(LogLevel.Error);
        await Assert.That(service.GetLogLevelArgument("level2")).IsEqualTo(LogLevel.Warning);
        await Assert.That(service.GetLogLevelArgument("level3")).IsEqualTo(LogLevel.Information);
        await Assert.That(service.GetLogLevelArgument("level4", LogLevel.Critical)).IsEqualTo(LogLevel.Critical); // Default for invalid
    }

    [Test]
    public async Task ShouldShowHelp_WithHelpFlags_ReturnsTrue()
    {
        // Test various help flag formats
        var helpFlags = new[] { 
            new[] { "--help" },
            new[] { "-h" },
            new[] { "--?" },
            new[] { "directory", "--help" }
        };

        foreach (var args in helpFlags)
        {
            var service = new CommandLineArgumentsService(args, _logger);
            await Assert.That(service.ShouldShowHelp()).IsTrue();
        }
    }

    [Test]
    public async Task ShouldShowHelp_WithoutHelpFlags_ReturnsFalse()
    {
        // Arrange
        var args = new[] { "--log-level", "Error", "directory" };

        // Act
        var service = new CommandLineArgumentsService(args, _logger);

        // Assert
        await Assert.That(service.ShouldShowHelp()).IsFalse();
    }

    [Test]
    public async Task IsLoggingOnlyCommand_WithLoggingFlags_ReturnsTrue()
    {
        var loggingOnlyCommands = new[]
        {
            new[] { "--show-log-stats" },
            new[] { "--clear-logs" },
            new[] { "--show-recent-logs" },
            new[] { "--enable-logging" },
            new[] { "--disable-logging" },
            new[] { "--log-level", "Error" },
            new[] { "--log-retention-days", "30" }
        };

        foreach (var args in loggingOnlyCommands)
        {
            var service = new CommandLineArgumentsService(args, _logger);
            await Assert.That(service.IsLoggingOnlyCommand()).IsTrue();
        }
    }

    [Test]
    public async Task IsLoggingOnlyCommand_WithPositionalArguments_ReturnsFalse()
    {
        // Arrange
        var args = new[] { "directory", "--log-level", "Error" };

        // Act
        var service = new CommandLineArgumentsService(args, _logger);

        // Assert
        await Assert.That(service.IsLoggingOnlyCommand()).IsFalse();
        await Assert.That(service.HasPositionalArguments()).IsTrue();
    }

    [Test]
    public async Task ApplyLoggingConfigurationAsync_WithEnableLogging_ConfiguresCorrectly()
    {
        // Arrange
        var args = new[] { "--enable-logging" };
        var service = new CommandLineArgumentsService(args, _logger);
        using var loggingService = new ErrorLoggingService(_configService, _logger);

        // Act
        var config = await service.ApplyLoggingConfigurationAsync(loggingService);

        // Assert
        await Assert.That(config.HasChanges).IsTrue();
        await Assert.That(config.LoggingEnabled).IsTrue();
        await Assert.That(await loggingService.IsLoggingEnabledAsync()).IsTrue();
    }

    [Test]
    public async Task ApplyLoggingConfigurationAsync_WithDisableLogging_ConfiguresCorrectly()
    {
        // Arrange
        var args = new[] { "--disable-logging" };
        var service = new CommandLineArgumentsService(args, _logger);
        using var loggingService = new ErrorLoggingService(_configService, _logger);

        // Act
        var config = await service.ApplyLoggingConfigurationAsync(loggingService);

        // Assert
        await Assert.That(config.HasChanges).IsTrue();
        await Assert.That(config.LoggingEnabled).IsFalse();
        await Assert.That(await loggingService.IsLoggingEnabledAsync()).IsFalse();
    }

    [Test]
    public async Task ApplyLoggingConfigurationAsync_WithLogLevel_ConfiguresCorrectly()
    {
        // Arrange
        var args = new[] { "--log-level", "Error" };
        var service = new CommandLineArgumentsService(args, _logger);
        using var loggingService = new ErrorLoggingService(_configService, _logger);

        // Act
        var config = await service.ApplyLoggingConfigurationAsync(loggingService);

        // Assert
        await Assert.That(config.HasChanges).IsTrue();
        await Assert.That(config.LogLevel).IsEqualTo(LogLevel.Error);
        await Assert.That(await loggingService.GetMinimumLogLevelAsync()).IsEqualTo(LogLevel.Error);
    }

    [Test]
    public async Task ApplyLoggingConfigurationAsync_WithRetentionDays_ConfiguresCorrectly()
    {
        // Arrange
        var args = new[] { "--log-retention-days", "14" };
        var service = new CommandLineArgumentsService(args, _logger);
        using var loggingService = new ErrorLoggingService(_configService, _logger);

        // Act
        var config = await service.ApplyLoggingConfigurationAsync(loggingService);

        // Assert
        await Assert.That(config.HasChanges).IsTrue();
        await Assert.That(config.RetentionDays).IsEqualTo(14);
        await Assert.That(await loggingService.GetLogRetentionDaysAsync()).IsEqualTo(14);
    }

    [Test]
    public async Task ApplyLoggingConfigurationAsync_WithInvalidRetentionDays_IgnoresInvalidValue()
    {
        // Arrange
        var args = new[] { "--log-retention-days", "0" };
        var service = new CommandLineArgumentsService(args, _logger);
        using var loggingService = new ErrorLoggingService(_configService, _logger);
        var originalRetention = await loggingService.GetLogRetentionDaysAsync();

        // Act
        var config = await service.ApplyLoggingConfigurationAsync(loggingService);

        // Assert
        await Assert.That(config.HasChanges).IsFalse();
        await Assert.That(config.RetentionDays).IsNull();
        await Assert.That(await loggingService.GetLogRetentionDaysAsync()).IsEqualTo(originalRetention);
    }

    [Test]
    public async Task ApplyLoggingConfigurationAsync_WithClearLogs_ClearsLogs()
    {
        // Arrange
        using var loggingService = new ErrorLoggingService(_configService, _logger);
        await loggingService.LogErrorAsync("Test error before clear");
        
        var args = new[] { "--clear-logs" };
        var service = new CommandLineArgumentsService(args, _logger);

        // Act
        var config = await service.ApplyLoggingConfigurationAsync(loggingService);

        // Assert
        await Assert.That(config.HasChanges).IsTrue();
        await Assert.That(config.LogsCleared).IsTrue();
        
        var logs = await loggingService.GetRecentLogsAsync(10);
        await Assert.That(logs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ApplyLoggingConfigurationAsync_WithShowLogStats_ShowsStatistics()
    {
        // Arrange
        using var loggingService = new ErrorLoggingService(_configService, _logger);
        await loggingService.LogErrorAsync("Test error for stats");
        
        var args = new[] { "--show-log-stats" };
        var service = new CommandLineArgumentsService(args, _logger);

        // Act
        var config = await service.ApplyLoggingConfigurationAsync(loggingService);

        // Assert
        await Assert.That(config.HasChanges).IsTrue();
        await Assert.That(config.ShowStats).IsTrue();
    }

    [Test]
    public async Task ApplyLoggingConfigurationAsync_WithShowRecentLogs_ShowsLogs()
    {
        // Arrange
        using var loggingService = new ErrorLoggingService(_configService, _logger);
        await loggingService.LogErrorAsync("Test error for recent logs");
        
        var args = new[] { "--show-recent-logs", "5" };
        var service = new CommandLineArgumentsService(args, _logger);

        // Act
        var config = await service.ApplyLoggingConfigurationAsync(loggingService);

        // Assert
        await Assert.That(config.HasChanges).IsTrue();
        await Assert.That(config.ShowRecentLogs).IsEqualTo(5);
    }

    [Test]
    public async Task ApplyLoggingConfigurationAsync_WithMultipleOptions_AppliesAll()
    {
        // Arrange
        var args = new[] { 
            "--enable-logging", 
            "--log-level", "Information", 
            "--log-retention-days", "7"
        };
        var service = new CommandLineArgumentsService(args, _logger);
        using var loggingService = new ErrorLoggingService(_configService, _logger);

        // Act
        var config = await service.ApplyLoggingConfigurationAsync(loggingService);

        // Assert
        await Assert.That(config.HasChanges).IsTrue();
        await Assert.That(config.LoggingEnabled).IsTrue();
        await Assert.That(config.LogLevel).IsEqualTo(LogLevel.Information);
        await Assert.That(config.RetentionDays).IsEqualTo(7);
        
        await Assert.That(await loggingService.IsLoggingEnabledAsync()).IsTrue();
        await Assert.That(await loggingService.GetMinimumLogLevelAsync()).IsEqualTo(LogLevel.Information);
        await Assert.That(await loggingService.GetLogRetentionDaysAsync()).IsEqualTo(7);
    }

    [Test]
    public async Task ApplyLoggingConfigurationAsync_WithNoLoggingOptions_MakesNoChanges()
    {
        // Arrange
        var args = new[] { "directory", "model" };
        var service = new CommandLineArgumentsService(args, _logger);
        using var loggingService = new ErrorLoggingService(_configService, _logger);

        // Act
        var config = await service.ApplyLoggingConfigurationAsync(loggingService);

        // Assert
        await Assert.That(config.HasChanges).IsFalse();
        await Assert.That(config.LoggingEnabled).IsNull();
        await Assert.That(config.LogLevel).IsNull();
        await Assert.That(config.RetentionDays).IsNull();
    }

    [Test]
    public async Task GetAllArguments_ReturnsAllParsedArguments()
    {
        // Arrange
        var args = new[] { "--flag1", "value1", "--flag2" };
        var service = new CommandLineArgumentsService(args, _logger);

        // Act
        var allArgs = service.GetAllArguments();

        // Assert
        await Assert.That(allArgs.Count).IsEqualTo(2);
        await Assert.That(allArgs["flag1"]).IsEqualTo("value1");
        await Assert.That(allArgs["flag2"]).IsEqualTo("true");
    }

    [Test]
    public async Task ServiceWithNullLogger_WorksCorrectly()
    {
        // Arrange & Act
        var args = new[] { "--enable-logging", "--log-level", "Error" };
        var service = new CommandLineArgumentsService(args, null);

        // Assert
        await Assert.That(service.HasArgument("enable-logging")).IsTrue();
        await Assert.That(service.GetLogLevelArgument("log-level")).IsEqualTo(LogLevel.Error);
    }

    [Test]
    public async Task EmptyArguments_HandledCorrectly()
    {
        // Arrange & Act
        var service = new CommandLineArgumentsService(Array.Empty<string>(), _logger);

        // Assert
        await Assert.That(service.HasPositionalArguments()).IsFalse();
        await Assert.That(service.ShouldShowHelp()).IsFalse();
        await Assert.That(service.IsLoggingOnlyCommand()).IsFalse();
        await Assert.That(service.GetAllArguments().Count).IsEqualTo(0);
    }

    [Test]
    public async Task IsFileExportCommand_WithExportFiles_ReturnsTrue()
    {
        // Arrange
        var args = new[] { "--export-files", "output.csv" };
        var service = new CommandLineArgumentsService(args, _logger);

        // Act & Assert
        await Assert.That(service.IsFileExportCommand()).IsTrue();
    }

    [Test]
    public async Task IsFileExportCommand_WithListFilesExport_ReturnsTrue()
    {
        // Arrange
        var args = new[] { "--list-files-export" };
        var service = new CommandLineArgumentsService(args, _logger);

        // Act & Assert
        await Assert.That(service.IsFileExportCommand()).IsTrue();
    }

    [Test]
    public async Task IsFileExportCommand_WithoutExportCommands_ReturnsFalse()
    {
        // Arrange
        var args = new[] { "--log-level", "Error" };
        var service = new CommandLineArgumentsService(args, _logger);

        // Act & Assert
        await Assert.That(service.IsFileExportCommand()).IsFalse();
    }

    [Test]
    public async Task GetFileExportFormat_WithValidFormat_ReturnsCorrectFormat()
    {
        // Arrange
        var testCases = new[]
        {
            new { Args = new[] { "--export-format", "json" }, Expected = FileExportFormat.Json },
            new { Args = new[] { "--export-format", "XML" }, Expected = FileExportFormat.Xml },
            new { Args = new[] { "--export-format", "txt" }, Expected = FileExportFormat.Txt },
            new { Args = new[] { "--export-format", "CSV" }, Expected = FileExportFormat.Csv }
        };

        foreach (var testCase in testCases)
        {
            var service = new CommandLineArgumentsService(testCase.Args, _logger);
            
            // Act
            var format = service.GetFileExportFormat();
            
            // Assert
            await Assert.That(format).IsEqualTo(testCase.Expected);
        }
    }

    [Test]
    public async Task GetFileExportFormat_WithInvalidFormat_ReturnsDefault()
    {
        // Arrange
        var args = new[] { "--export-format", "invalid" };
        var service = new CommandLineArgumentsService(args, _logger);

        // Act
        var format = service.GetFileExportFormat(FileExportFormat.Json);

        // Assert
        await Assert.That(format).IsEqualTo(FileExportFormat.Json);
    }

    [Test]
    public async Task ApplyFileExportConfigurationAsync_WithExportFiles_ExportsCorrectly()
    {
        // Arrange
        var args = new[] { "--export-files", "test_output.csv", "--export-format", "csv" };
        var service = new CommandLineArgumentsService(args, _logger);
        using var exportService = new FileListExportService(_logger);
        
        var resources = new List<ResourceInfo>
        {
            new ResourceInfo { Uri = "file:///test.txt", Name = "test.txt", Description = "Test file", MimeType = "text/plain" }
        };

        var outputPath = Path.Combine(_testDirectory, "test_output.csv");

        // Act
        var config = await service.ApplyFileExportConfigurationAsync(exportService, resources);

        // Assert
        await Assert.That(config.HasExport).IsTrue();
        await Assert.That(config.ExportResult).IsNotNull();
        await Assert.That(config.ExportResult!.Success).IsTrue();
        await Assert.That(config.ExportResult.ExportedCount).IsEqualTo(1);
    }

    [Test]
    public async Task ApplyFileExportConfigurationAsync_WithListFilesExport_ExportsAndDisplays()
    {
        // Arrange
        var args = new[] { "--list-files-export", "test_list.json", "--export-format", "json" };
        var service = new CommandLineArgumentsService(args, _logger);
        using var exportService = new FileListExportService(_logger);
        
        var resources = new List<ResourceInfo>
        {
            new ResourceInfo { Uri = "file:///doc1.pdf", Name = "doc1.pdf", Description = "PDF document", MimeType = "application/pdf" },
            new ResourceInfo { Uri = "file:///doc2.txt", Name = "doc2.txt", Description = "Text document", MimeType = "text/plain" }
        };

        // Act
        var config = await service.ApplyFileExportConfigurationAsync(exportService, resources);

        // Assert
        await Assert.That(config.HasExport).IsTrue();
        await Assert.That(config.ShouldDisplay).IsTrue();
        await Assert.That(config.ExportResult).IsNotNull();
        await Assert.That(config.ExportResult!.Success).IsTrue();
        await Assert.That(config.ExportResult.ExportedCount).IsEqualTo(2);
        await Assert.That(config.ExportResult.Format).IsEqualTo(FileExportFormat.Json);
    }

    [Test]
    public async Task ApplyFileExportConfigurationAsync_WithExportMetadata_IncludesMetadata()
    {
        // Arrange
        var args = new[] { "--export-files", "metadata_test.csv", "--export-metadata", "true" };
        var service = new CommandLineArgumentsService(args, _logger);
        using var exportService = new FileListExportService(_configService, _logger);
        
        var resources = new List<ResourceInfo>
        {
            new ResourceInfo { Uri = "file:///test.txt", Name = "test.txt", Description = "Test file", MimeType = "text/plain" }
        };

        // Act
        var config = await service.ApplyFileExportConfigurationAsync(exportService, resources);

        // Assert
        await Assert.That(config.ExportResult).IsNotNull();
        await Assert.That(config.ExportResult!.Success).IsTrue();
        
        // Verify the file contains metadata by checking its content
        var outputPath = "metadata_test.csv"; // File is created in current directory
        await Assert.That(File.Exists(outputPath)).IsTrue();
        var content = await File.ReadAllTextAsync(outputPath);
        await Assert.That(content).Contains("Description");
        await Assert.That(content).Contains("MimeType");
        
        // Clean up the created file
        if (File.Exists(outputPath))
        {
            try { File.Delete(outputPath); } catch { /* Ignore cleanup errors */ }
        }
    }

    [Test]
    public async Task ApplyFileExportConfigurationAsync_WithoutExportMetadata_ExcludesMetadata()
    {
        // Arrange
        var args = new[] { "--export-files", "no_metadata_test.csv", "--export-metadata", "false" };
        var service = new CommandLineArgumentsService(args, _logger);
        using var exportService = new FileListExportService(_configService, _logger);
        
        var resources = new List<ResourceInfo>
        {
            new ResourceInfo { Uri = "file:///test.txt", Name = "test.txt", Description = "Test file", MimeType = "text/plain" }
        };

        // Act
        var config = await service.ApplyFileExportConfigurationAsync(exportService, resources);

        // Assert
        await Assert.That(config.ExportResult).IsNotNull();
        await Assert.That(config.ExportResult!.Success).IsTrue();
        
        // Verify the file doesn't contain metadata
        var outputPath = "no_metadata_test.csv"; // File is created in current directory
        await Assert.That(File.Exists(outputPath)).IsTrue();
        var content = await File.ReadAllTextAsync(outputPath);
        await Assert.That(content).DoesNotContain("Description");
        await Assert.That(content).DoesNotContain("MimeType");
        
        // Clean up the created file
        if (File.Exists(outputPath))
        {
            try { File.Delete(outputPath); } catch { /* Ignore cleanup errors */ }
        }
    }

    [Test]
    public async Task ApplyFileExportConfigurationAsync_WithEmptyResourceList_HandlesGracefully()
    {
        // Arrange
        var args = new[] { "--export-files", "empty_test.csv" };
        var service = new CommandLineArgumentsService(args, _logger);
        using var exportService = new FileListExportService(_logger);
        
        var resources = new List<ResourceInfo>();

        // Act
        var config = await service.ApplyFileExportConfigurationAsync(exportService, resources);

        // Assert
        await Assert.That(config.HasExport).IsTrue();
        await Assert.That(config.ExportResult).IsNotNull();
        await Assert.That(config.ExportResult!.Success).IsTrue();
        await Assert.That(config.ExportResult.ExportedCount).IsEqualTo(0);
    }

    [Test]
    public async Task ApplyFileExportConfigurationAsync_WithoutExportArgs_ReturnsEmptyConfig()
    {
        // Arrange
        var args = new[] { "--log-level", "Error" };
        var service = new CommandLineArgumentsService(args, _logger);
        using var exportService = new FileListExportService(_logger);
        
        var resources = new List<ResourceInfo>
        {
            new ResourceInfo { Uri = "file:///test.txt", Name = "test.txt", Description = "Test file", MimeType = "text/plain" }
        };

        // Act
        var config = await service.ApplyFileExportConfigurationAsync(exportService, resources);

        // Assert
        await Assert.That(config.HasExport).IsFalse();
        await Assert.That(config.ShouldDisplay).IsFalse();
        await Assert.That(config.ExportResult).IsNull();
    }
}
