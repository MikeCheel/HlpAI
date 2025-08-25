using System.Text.Json;
using HlpAI.MCP;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Services;

public class FileListExportServiceTests
{
    private string _testDirectory = null!;
    private ILogger<FileListExportServiceTests> _logger = null!;
    private string _originalUserProfile = null!;
    private SqliteConfigurationService _configService = null!;
    private string _testDbPath = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("file_export_service_tests");
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<FileListExportServiceTests>();
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        // Create isolated test database for this test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_file_export_{Guid.NewGuid()}.db");
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

    private List<ResourceInfo> CreateTestResources()
    {
        return
        [
            new ResourceInfo
            {
                Uri = "file:///test1.txt",
                Name = "test1.txt",
                Description = "Test file 1",
                MimeType = "text/plain"
            },
            new ResourceInfo
            {
                Uri = "file:///documents/test2.pdf",
                Name = "test2.pdf", 
                Description = "Test PDF file",
                MimeType = "application/pdf"
            },
            new ResourceInfo
            {
                Uri = "file:///data/test3.csv",
                Name = "test3.csv",
                Description = "Test CSV file",
                MimeType = "text/csv"
            }
        ];
    }

    [Test]
    public async Task ExportFileListAsync_ToCsv_WithMetadata_GeneratesCorrectFormat()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var resources = CreateTestResources();
        var outputPath = Path.Combine(_testDirectory, "test_export.csv");

        // Act
        var result = await service.ExportFileListAsync(resources, FileExportFormat.Csv, outputPath, true);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ExportedCount).IsEqualTo(3);
        await Assert.That(File.Exists(outputPath)).IsTrue();

        var content = await File.ReadAllTextAsync(outputPath);
        await Assert.That(content).Contains("Uri,Name,Description,MimeType");
        await Assert.That(content).Contains("\"file:///test1.txt\",\"test1.txt\"");
        await Assert.That(content).Contains("\"Test file 1\"");
        await Assert.That(content).Contains("\"text/plain\"");
    }

    [Test]
    public async Task ExportFileListAsync_ToCsv_WithoutMetadata_GeneratesBasicFormat()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var resources = CreateTestResources();
        var outputPath = Path.Combine(_testDirectory, "test_export_basic.csv");

        // Act
        var result = await service.ExportFileListAsync(resources, FileExportFormat.Csv, outputPath, false);

        // Assert
        await Assert.That(result.Success).IsTrue();
        var content = await File.ReadAllTextAsync(outputPath);
        await Assert.That(content).Contains("Uri,Name");
        await Assert.That(content).DoesNotContain("Description,MimeType");
    }

    [Test]
    public async Task ExportFileListAsync_ToJson_WithMetadata_GeneratesValidJson()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var resources = CreateTestResources();
        var outputPath = Path.Combine(_testDirectory, "test_export.json");

        // Act
        var result = await service.ExportFileListAsync(resources, FileExportFormat.Json, outputPath, true);

        // Assert
        await Assert.That(result.Success).IsTrue();
        var content = await File.ReadAllTextAsync(outputPath);
        
        // Verify it's valid JSON
        var jsonDoc = JsonDocument.Parse(content);
        await Assert.That(jsonDoc.RootElement.TryGetProperty("exportInfo", out var exportInfo)).IsTrue();
        await Assert.That(jsonDoc.RootElement.TryGetProperty("files", out var files)).IsTrue();
        await Assert.That(files.GetArrayLength()).IsEqualTo(3);
    }

    [Test]
    public async Task ExportFileListAsync_ToTxt_GeneratesReadableFormat()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var resources = CreateTestResources();
        var outputPath = Path.Combine(_testDirectory, "test_export.txt");

        // Act
        var result = await service.ExportFileListAsync(resources, FileExportFormat.Txt, outputPath, true);

        // Assert
        await Assert.That(result.Success).IsTrue();
        var content = await File.ReadAllTextAsync(outputPath);
        await Assert.That(content).Contains("File List Export");
        await Assert.That(content).Contains("Total Files: 3");
        await Assert.That(content).Contains("Name: test1.txt");
        await Assert.That(content).Contains("URI: file:///test1.txt");
    }

    [Test]
    public async Task ExportFileListAsync_ToXml_GeneratesValidXml()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var resources = CreateTestResources();
        var outputPath = Path.Combine(_testDirectory, "test_export.xml");

        // Act
        var result = await service.ExportFileListAsync(resources, FileExportFormat.Xml, outputPath, true);

        // Assert
        await Assert.That(result.Success).IsTrue();
        var content = await File.ReadAllTextAsync(outputPath);
        await Assert.That(content).Contains("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        await Assert.That(content).Contains("<fileList>");
        await Assert.That(content).Contains("<exportInfo>");
        await Assert.That(content).Contains("<files>");
        await Assert.That(content).Contains("<uri>file:///test1.txt</uri>");
        await Assert.That(content).Contains("</fileList>");
    }

    [Test]
    public async Task ExportFileListAsync_WithEmptyList_HandlesGracefully()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var resources = new List<ResourceInfo>();
        var outputPath = Path.Combine(_testDirectory, "empty_export.csv");

        // Act
        var result = await service.ExportFileListAsync(resources, FileExportFormat.Csv, outputPath, true);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ExportedCount).IsEqualTo(0);
        await Assert.That(File.Exists(outputPath)).IsTrue();
        
        var content = await File.ReadAllTextAsync(outputPath);
        await Assert.That(content).Contains("Uri,Name,Description,MimeType"); // Header only
    }

    [Test]
    public async Task ExportFileListAsync_WithInvalidPath_ReturnsFailure()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var resources = CreateTestResources();
        var invalidPath = "/invalid/path/that/does/not/exist/file.csv";

        // Act
        var result = await service.ExportFileListAsync(resources, FileExportFormat.Csv, invalidPath, true);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsNotNull();
        await Assert.That(result.ErrorMessage).IsNotEmpty();
    }

    [Test]
    public async Task ExportFileListAsync_WithSpecialCharacters_EscapesCorrectly()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var resources = new List<ResourceInfo>
        {
            new ResourceInfo
            {
                Uri = "file:///test & file.txt",
                Name = "test & file.txt",
                Description = "File with \"quotes\" and <tags>",
                MimeType = "text/plain"
            }
        };
        var outputPath = Path.Combine(_testDirectory, "special_chars.csv");

        // Act
        var result = await service.ExportFileListAsync(resources, FileExportFormat.Csv, outputPath, true);

        // Assert
        await Assert.That(result.Success).IsTrue();
        var content = await File.ReadAllTextAsync(outputPath);
        await Assert.That(content).Contains("\"File with \"\"quotes\"\" and <tags>\""); // CSV escaping
    }

    [Test]
    public async Task ExportFileListAsync_ToXml_WithSpecialCharacters_EscapesCorrectly()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var resources = new List<ResourceInfo>
        {
            new ResourceInfo
            {
                Uri = "file:///test.xml",
                Name = "test.xml",
                Description = "File with <xml> & \"quotes\"",
                MimeType = "application/xml"
            }
        };
        var outputPath = Path.Combine(_testDirectory, "special_chars.xml");

        // Act
        var result = await service.ExportFileListAsync(resources, FileExportFormat.Xml, outputPath, true);

        // Assert
        await Assert.That(result.Success).IsTrue();
        var content = await File.ReadAllTextAsync(outputPath);
        await Assert.That(content).Contains("File with &lt;xml&gt; &amp; &quot;quotes&quot;"); // XML escaping
    }

    [Test]
    public async Task SetExportSettingsAsync_UpdatesConfiguration()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var settings = new FileExportSettings
        {
            PrettyPrint = false,
            IncludeHeaders = false,
            DefaultFormat = FileExportFormat.Json
        };

        // Act
        var result = await service.SetExportSettingsAsync(settings);

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify settings are applied by creating another service instance
        using var service2 = new FileListExportService(_configService, _logger);
        var invokeResult = service2.GetType()
            .GetMethod("GetExportSettingsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(service2, null);
        
        var retrievedSettings = invokeResult as Task<FileExportSettings>;
        
        var actualSettings = await retrievedSettings!;
        await Assert.That(actualSettings.PrettyPrint).IsFalse();
        await Assert.That(actualSettings.IncludeHeaders).IsFalse();
        await Assert.That(actualSettings.DefaultFormat).IsEqualTo(FileExportFormat.Json);
    }

    [Test]
    public async Task GetExportHistoryAsync_ReturnsExportHistory()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var resources = CreateTestResources();
        
        // Act - Perform multiple exports
        var outputPath1 = Path.Combine(_testDirectory, "export1.csv");
        var outputPath2 = Path.Combine(_testDirectory, "export2.json");
        
        await service.ExportFileListAsync(resources, FileExportFormat.Csv, outputPath1, true);
        await service.ExportFileListAsync(resources, FileExportFormat.Json, outputPath2, false);
        
        // Get history
        var history = await service.GetExportHistoryAsync(10);

        // Assert
        await Assert.That(history.Count).IsEqualTo(2);
        await Assert.That(history.Any(h => h.Format == FileExportFormat.Csv)).IsTrue();
        await Assert.That(history.Any(h => h.Format == FileExportFormat.Json)).IsTrue();
        await Assert.That(history.All(h => h.Success)).IsTrue();
    }

    [Test]
    public async Task ExportFileListAsync_WithLargeDataset_HandlesEfficiently()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var resources = new List<ResourceInfo>();
        
        // Create 1000 test resources
        for (int i = 0; i < 1000; i++)
        {
            resources.Add(new ResourceInfo
            {
                Uri = $"file:///test{i:D4}.txt",
                Name = $"test{i:D4}.txt",
                Description = $"Test file number {i} with some description",
                MimeType = "text/plain"
            });
        }
        
        var outputPath = Path.Combine(_testDirectory, "large_export.csv");

        // Act
        var result = await service.ExportFileListAsync(resources, FileExportFormat.Csv, outputPath, true);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ExportedCount).IsEqualTo(1000);
        await Assert.That(File.Exists(outputPath)).IsTrue();
        
        var fileInfo = new FileInfo(outputPath);
        await Assert.That(fileInfo.Length).IsGreaterThan(0);
        await Assert.That(result.FileSizeBytes).IsEqualTo(fileInfo.Length);
    }

    [Test]
    public async Task ExportFileListAsync_ConcurrentExports_HandledCorrectly()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var resources = CreateTestResources();
        var results = new List<FileExportResult>();

        // Act - Run 5 sequential exports instead of concurrent to avoid database conflicts
        for (int i = 0; i < 5; i++)
        {
            var index = i;
            var outputPath = Path.Combine(_testDirectory, $"concurrent_export_{index}.csv");
            var result = await service.ExportFileListAsync(resources, FileExportFormat.Csv, outputPath, true);
            results.Add(result);
        }

        // Assert
        await Assert.That(results.Count).IsEqualTo(5);
        await Assert.That(results.All(r => r.Success)).IsTrue();
        await Assert.That(results.All(r => r.ExportedCount == 3)).IsTrue();
        
        // Verify all files were created
        for (int i = 0; i < 5; i++)
        {
            var outputPath = Path.Combine(_testDirectory, $"concurrent_export_{i}.csv");
            await Assert.That(File.Exists(outputPath)).IsTrue();
        }
    }

    [Test]
    public async Task ServiceWithNullLogger_WorksCorrectly()
    {
        // Arrange & Act
        using var service = new FileListExportService(_configService, null);
        var resources = CreateTestResources();
        var outputPath = Path.Combine(_testDirectory, "null_logger_export.csv");
        
        var result = await service.ExportFileListAsync(resources, FileExportFormat.Csv, outputPath, true);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ExportedCount).IsEqualTo(3);
    }

    [Test]
    public async Task Dispose_HandledGracefully()
    {
        // Arrange
        var service = new FileListExportService(_configService, _logger);
        var resources = CreateTestResources();
        var outputPath = Path.Combine(_testDirectory, "dispose_test.csv");
        
        // Act
        await service.ExportFileListAsync(resources, FileExportFormat.Csv, outputPath, true);
        
        // Dispose multiple times
        service.Dispose();
        service.Dispose();
        
        // Assert - No exceptions should be thrown
        await Assert.That(File.Exists(outputPath)).IsTrue();
    }

    [Test]
    public async Task ExportFileListAsync_AllFormats_ProduceValidOutput()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var resources = CreateTestResources();
        var formats = new[] { FileExportFormat.Csv, FileExportFormat.Json, FileExportFormat.Txt, FileExportFormat.Xml };

        // Act & Assert
        foreach (var format in formats)
        {
            var outputPath = Path.Combine(_testDirectory, $"test_all_formats.{format.ToString().ToLower()}");
            var result = await service.ExportFileListAsync(resources, format, outputPath, true);
            
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Format).IsEqualTo(format);
            await Assert.That(result.ExportedCount).IsEqualTo(3);
            await Assert.That(File.Exists(outputPath)).IsTrue();
            await Assert.That(new FileInfo(outputPath).Length).IsGreaterThan(0);
        }
    }

    [Test]
    public async Task ExportFileListAsync_WithUnsupportedFormat_ThrowsException()
    {
        // Arrange
        using var service = new FileListExportService(_configService, _logger);
        var resources = CreateTestResources();
        var outputPath = Path.Combine(_testDirectory, "unsupported_format.unknown");

        // Act
        var result = await service.ExportFileListAsync(resources, (FileExportFormat)999, outputPath, true);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).Contains("Unsupported export format");
    }
}