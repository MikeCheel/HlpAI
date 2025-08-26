using Microsoft.Extensions.Logging;
using HlpAI.Services;

namespace HlpAI.Tests.Services;

/// <summary>
/// Unit tests for ExtractorManagementService
/// Tests configuration management, file extension operations, and extraction testing
/// </summary>
public class ExtractorManagementServiceTests
{
    private ExtractorManagementService? _service;
    private ILogger<ExtractorManagementService>? _logger;
    private SqliteConfigurationService? _configService;
    private string _testDbPath = string.Empty;

    [Before(Test)]
    public Task Setup()
    {
        // Create a test-specific database path
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_extractor_mgmt_{Guid.NewGuid()}.db");
        
        // Create logger for testing
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<ExtractorManagementService>();
        
        // Create isolated configuration service for testing
        _configService = new SqliteConfigurationService(_testDbPath, _logger);
        _service = new ExtractorManagementService(_configService, _logger);
        
        return Task.CompletedTask;
    }

    [After(Test)]
    public void Cleanup()
    {
        _service?.Dispose();
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
    }

    [Test]
    public async Task GetExtractorsAsync_ShouldReturnDefaultExtractors_WhenNoCustomConfiguration()
    {
        // Act
        var extractors = await _service!.GetExtractorsAsync();

        // Assert
        await Assert.That(extractors).IsNotNull();
        await Assert.That(extractors.Count).IsEqualTo(5);
        
        await Assert.That(extractors.ContainsKey("text")).IsTrue();
        await Assert.That(extractors.ContainsKey("html")).IsTrue();
        await Assert.That(extractors.ContainsKey("pdf")).IsTrue();
        await Assert.That(extractors.ContainsKey("chm")).IsTrue();
        await Assert.That(extractors.ContainsKey("hhc")).IsTrue();
        
        // Check default extensions for text extractor
        var textExtractor = extractors["text"];
        await Assert.That(textExtractor.DefaultExtensions.Contains(".txt")).IsTrue();
        await Assert.That(textExtractor.DefaultExtensions.Contains(".md")).IsTrue();
        await Assert.That(textExtractor.DefaultExtensions.Contains(".log")).IsTrue();
        await Assert.That(textExtractor.DefaultExtensions.Contains(".csv")).IsTrue();
        
        // Initially, custom extensions should match default extensions
        await Assert.That(textExtractor.CustomExtensions.Count).IsEqualTo(textExtractor.DefaultExtensions.Count);
    }

    [Test]
    public async Task AddFileExtensionAsync_ShouldAddExtension_WhenValidExtractorAndExtension()
    {
        // Act
        var result = await _service!.AddFileExtensionAsync("text", "docx");

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify extension was added
        var extractors = await _service.GetExtractorsAsync();
        var textExtractor = extractors["text"];
        await Assert.That(textExtractor.CustomExtensions.Contains(".docx")).IsTrue();
    }

    [Test]
    public async Task AddFileExtensionAsync_ShouldNormalizeExtension_WhenExtensionWithoutDot()
    {
        // Act
        var result = await _service!.AddFileExtensionAsync("text", "docx");

        // Assert
        await Assert.That(result).IsTrue();
        
        var extractors = await _service.GetExtractorsAsync();
        var textExtractor = extractors["text"];
        await Assert.That(textExtractor.CustomExtensions.Contains(".docx")).IsTrue(); // Should have dot added
    }

    [Test]
    public async Task AddFileExtensionAsync_ShouldNormalizeExtension_WhenExtensionWithDot()
    {
        // Act
        var result = await _service!.AddFileExtensionAsync("text", ".docx");

        // Assert
        await Assert.That(result).IsTrue();
        
        var extractors = await _service.GetExtractorsAsync();
        var textExtractor = extractors["text"];
        await Assert.That(textExtractor.CustomExtensions.Contains(".docx")).IsTrue();
    }

    [Test]
    public async Task AddFileExtensionAsync_ShouldReturnTrue_WhenExtensionAlreadyExists()
    {
        // Arrange
        await _service!.AddFileExtensionAsync("text", "docx");

        // Act
        var result = await _service.AddFileExtensionAsync("text", "docx");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task AddFileExtensionAsync_ShouldReturnFalse_WhenInvalidExtractorKey()
    {
        // Act
        var result = await _service!.AddFileExtensionAsync("invalid", "docx");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task AddFileExtensionAsync_ShouldReturnFalse_WhenEmptyExtractorKey()
    {
        // Act
        var result = await _service!.AddFileExtensionAsync("", "docx");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task AddFileExtensionAsync_ShouldReturnFalse_WhenEmptyExtension()
    {
        // Act
        var result = await _service!.AddFileExtensionAsync("text", "");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task AddFileExtensionAsync_ShouldReturnFalse_WhenExtensionAlreadyHandledByAnotherExtractor()
    {
        // Arrange
        await _service!.AddFileExtensionAsync("text", "xyz");

        // Act - Try to add same extension to different extractor
        var result = await _service.AddFileExtensionAsync("html", "xyz");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task RemoveFileExtensionAsync_ShouldRemoveExtension_WhenExtensionExists()
    {
        // Arrange
        await _service!.AddFileExtensionAsync("text", "docx");

        // Act
        var result = await _service.RemoveFileExtensionAsync("text", "docx");

        // Assert
        await Assert.That(result).IsTrue();
        
        var extractors = await _service.GetExtractorsAsync();
        var textExtractor = extractors["text"];
        await Assert.That(textExtractor.CustomExtensions.Contains(".docx")).IsFalse();
    }

    [Test]
    public async Task RemoveFileExtensionAsync_ShouldReturnFalse_WhenExtensionDoesNotExist()
    {
        // Act
        var result = await _service!.RemoveFileExtensionAsync("text", "nonexistent");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task RemoveFileExtensionAsync_ShouldReturnFalse_WhenInvalidExtractorKey()
    {
        // Act
        var result = await _service!.RemoveFileExtensionAsync("invalid", "txt");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TestFileExtractionAsync_ShouldReturnFailure_WhenFileDoesNotExist()
    {
        // Act
        var result = await _service!.TestFileExtractionAsync("nonexistent.txt");

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo("File does not exist");
        await Assert.That(result.FilePath).IsEqualTo("nonexistent.txt");
    }

    [Test]
    public async Task TestFileExtractionAsync_ShouldReturnFailure_WhenNoExtractorConfigured()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "test content");
        
        try
        {
            // Rename to unsupported extension
            var unsupportedFile = Path.ChangeExtension(tempFile, ".unsupported");
            File.Move(tempFile, unsupportedFile);

            // Act
            var result = await _service!.TestFileExtractionAsync(unsupportedFile);

            // Assert
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.ErrorMessage).Contains("No extractor configured for extension .unsupported");
            
            // Cleanup
            File.Delete(unsupportedFile);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Test]
    public async Task TestFileExtractionAsync_ShouldReturnSuccess_WhenTextFileCanBeExtracted()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var testContent = "This is test content for extraction.";
        File.WriteAllText(tempFile, testContent);
        
        try
        {
            // Rename to .txt extension
            var txtFile = Path.ChangeExtension(tempFile, ".txt");
            File.Move(tempFile, txtFile);

            // Act
            var result = await _service!.TestFileExtractionAsync(txtFile);

            // Assert
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.ExtractorUsed).IsEqualTo("Text File Extractor");
            await Assert.That(result.ContentLength).IsEqualTo(testContent.Length);
            await Assert.That(result.FileSizeBytes).IsGreaterThan(0);
            await Assert.That(result.ExtractionTimeMs).IsGreaterThanOrEqualTo(0);
            await Assert.That(result.ContentPreview).Contains(testContent);
            
            // Cleanup
            File.Delete(txtFile);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Test]
    public async Task GetExtractionStatisticsAsync_ShouldReturnCorrectStatistics_WhenDefaultConfiguration()
    {
        // Act
        var stats = await _service!.GetExtractionStatisticsAsync();

        // Assert
        await Assert.That(stats.TotalExtractors).IsEqualTo(5);
        await Assert.That(stats.TotalSupportedExtensions).IsEqualTo(9); // .txt,.md,.log,.csv,.html,.htm,.pdf,.chm,.hhc
        
        await Assert.That(stats.ExtractorStats.ContainsKey("text")).IsTrue();
        await Assert.That(stats.ExtractorStats.ContainsKey("html")).IsTrue();
        await Assert.That(stats.ExtractorStats.ContainsKey("pdf")).IsTrue();
        await Assert.That(stats.ExtractorStats.ContainsKey("chm")).IsTrue();
        await Assert.That(stats.ExtractorStats.ContainsKey("hhc")).IsTrue();
        
        var textStats = stats.ExtractorStats["text"];
        await Assert.That(textStats.SupportedExtensionCount).IsEqualTo(4);
        await Assert.That(textStats.DefaultExtensionCount).IsEqualTo(4);
        await Assert.That(textStats.CustomExtensionCount).IsEqualTo(0);
        await Assert.That(textStats.SupportedExtensions.Contains(".txt")).IsTrue();
        await Assert.That(textStats.SupportedExtensions.Contains(".md")).IsTrue();
        await Assert.That(textStats.SupportedExtensions.Contains(".log")).IsTrue();
        await Assert.That(textStats.SupportedExtensions.Contains(".csv")).IsTrue();
    }

    [Test]
    public async Task GetExtractionStatisticsAsync_ShouldReflectCustomizations_WhenExtensionsAdded()
    {
        // Arrange
        await _service!.AddFileExtensionAsync("text", "docx");
        await _service.AddFileExtensionAsync("html", "jsp");

        // Act
        var stats = await _service.GetExtractionStatisticsAsync();

        // Assert
        await Assert.That(stats.TotalSupportedExtensions).IsEqualTo(11); // 9 + 2 added
        
        var textStats = stats.ExtractorStats["text"];
        await Assert.That(textStats.SupportedExtensionCount).IsEqualTo(5);
        await Assert.That(textStats.CustomExtensionCount).IsEqualTo(1); // 1 added
        await Assert.That(textStats.SupportedExtensions.Contains(".docx")).IsTrue();
        
        var htmlStats = stats.ExtractorStats["html"];
        await Assert.That(htmlStats.SupportedExtensionCount).IsEqualTo(3);
        await Assert.That(htmlStats.CustomExtensionCount).IsEqualTo(1); // 1 added
        await Assert.That(htmlStats.SupportedExtensions.Contains(".jsp")).IsTrue();
    }

    [Test]
    public async Task ResetExtractorToDefaultAsync_ShouldResetConfiguration_WhenValidExtractorKey()
    {
        // Arrange
        await _service!.AddFileExtensionAsync("text", "docx");
        await _service.AddFileExtensionAsync("text", "rtf");
        
        // Verify extensions were added
        var extractorsBefore = await _service.GetExtractorsAsync();
        await Assert.That(extractorsBefore["text"].CustomExtensions.Contains(".docx")).IsTrue();
        await Assert.That(extractorsBefore["text"].CustomExtensions.Contains(".rtf")).IsTrue();

        // Act
        var result = await _service.ResetExtractorToDefaultAsync("text");

        // Assert
        await Assert.That(result).IsTrue();
        
        var extractorsAfter = await _service.GetExtractorsAsync();
        var textExtractor = extractorsAfter["text"];
        await Assert.That(textExtractor.CustomExtensions.Contains(".docx")).IsFalse();
        await Assert.That(textExtractor.CustomExtensions.Contains(".rtf")).IsFalse();
        await Assert.That(textExtractor.CustomExtensions.Count).IsEqualTo(textExtractor.DefaultExtensions.Count);
    }

    [Test]
    public async Task ResetExtractorToDefaultAsync_ShouldReturnFalse_WhenInvalidExtractorKey()
    {
        // Act
        var result = await _service!.ResetExtractorToDefaultAsync("invalid");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task GetConfiguredExtractorInstancesAsync_ShouldReturnWorkingExtractors()
    {
        // Act
        var extractors = await _service!.GetConfiguredExtractorInstancesAsync();

        // Assert
        await Assert.That(extractors).IsNotNull();
        await Assert.That(extractors.Count).IsEqualTo(5);
        
        // Test that each extractor can handle its configured extensions
        var textExtractor = extractors.FirstOrDefault(e => e.GetMimeType() == "text/plain");
        await Assert.That(textExtractor).IsNotNull();
        await Assert.That(textExtractor!.CanHandle("test.txt")).IsTrue();
        await Assert.That(textExtractor.CanHandle("test.md")).IsTrue();
        await Assert.That(textExtractor.CanHandle("test.pdf")).IsFalse(); // Should not handle PDF
    }

    [Test]
    public async Task PersistenceTest_ShouldMaintainConfiguration_AcrossServiceInstances()
    {
        // Arrange - Add custom extensions with first service instance
        await _service!.AddFileExtensionAsync("text", "docx");
        await _service.AddFileExtensionAsync("html", "php");
        _service.Dispose();

        // Act - Create new service instance with same config service to test persistence
        using var newConfigService = new SqliteConfigurationService(_testDbPath, _logger);
        using var newService = new ExtractorManagementService(newConfigService, _logger);
        var extractors = await newService.GetExtractorsAsync();

        // Assert - Configuration should persist
        await Assert.That(extractors["text"].CustomExtensions.Contains(".docx")).IsTrue();
        await Assert.That(extractors["html"].CustomExtensions.Contains(".php")).IsTrue();
    }

    [Test]
    public async Task ConfigurationIntegrityTest_ShouldMaintainDataIntegrity_WithComplexOperations()
    {
        // Arrange & Act - Perform complex sequence of operations
        await _service!.AddFileExtensionAsync("text", "docx");
        await _service.AddFileExtensionAsync("text", "rtf");
        await _service.AddFileExtensionAsync("html", "php");
        
        await _service.RemoveFileExtensionAsync("text", "log"); // Remove default extension
        await _service.RemoveFileExtensionAsync("html", "php"); // Remove custom extension
        
        var stats1 = await _service.GetExtractionStatisticsAsync();
        
        await _service.ResetExtractorToDefaultAsync("text");
        
        var stats2 = await _service.GetExtractionStatisticsAsync();
        var extractors = await _service.GetExtractorsAsync();

        // Assert
        // After reset, text extractor should be back to defaults
        await Assert.That(extractors["text"].CustomExtensions.Count).IsEqualTo(4); // Back to default count
        await Assert.That(extractors["text"].CustomExtensions.Contains(".log")).IsTrue(); // Default extension restored
        await Assert.That(extractors["text"].CustomExtensions.Contains(".docx")).IsFalse(); // Custom extension removed
        await Assert.That(extractors["text"].CustomExtensions.Contains(".rtf")).IsFalse(); // Custom extension removed
        
        // HTML extractor should maintain its state (php was removed)
        await Assert.That(extractors["html"].CustomExtensions.Contains(".php")).IsFalse();
    }

    [Test]
    public async Task EdgeCaseTest_ShouldHandleExtensionNormalization_WithVariousFormats()
    {
        // Act & Assert - Test various extension formats
        var result1 = await _service!.AddFileExtensionAsync("text", "DOCX"); // Uppercase
        await Assert.That(result1).IsTrue();
        var result2 = await _service.AddFileExtensionAsync("text", ".rtf");  // With dot
        await Assert.That(result2).IsTrue();
        var result3 = await _service.AddFileExtensionAsync("text", "TXT");   // Existing but different case
        await Assert.That(result3).IsTrue();
        
        var extractors = await _service.GetExtractorsAsync();
        var textExtractor = extractors["text"];
        
        // All should be normalized to lowercase with dot
        await Assert.That(textExtractor.CustomExtensions.Contains(".docx")).IsTrue();
        await Assert.That(textExtractor.CustomExtensions.Contains(".rtf")).IsTrue();
        await Assert.That(textExtractor.CustomExtensions.Contains(".txt")).IsTrue(); // Already existed
    }

    [Test]
    public async Task ExtractorInfoTest_ShouldContainCorrectMetadata()
    {
        // Act
        var extractors = await _service!.GetExtractorsAsync();

        // Assert - Check metadata for each extractor
        var textExtractor = extractors["text"];
        await Assert.That(textExtractor.Name).IsEqualTo("Text File Extractor");
        await Assert.That(textExtractor.Type).IsEqualTo("TextFileExtractor");
        await Assert.That(textExtractor.MimeType).IsEqualTo("text/plain");
        await Assert.That(textExtractor.Description).Contains("text-based files");

        var htmlExtractor = extractors["html"];
        await Assert.That(htmlExtractor.Name).IsEqualTo("HTML File Extractor");
        await Assert.That(htmlExtractor.Type).IsEqualTo("HtmlFileExtractor");
        await Assert.That(htmlExtractor.MimeType).IsEqualTo("text/html");
        await Assert.That(htmlExtractor.Description).Contains("HTML files");

        var pdfExtractor = extractors["pdf"];
        await Assert.That(pdfExtractor.Name).IsEqualTo("PDF File Extractor");
        await Assert.That(pdfExtractor.Type).IsEqualTo("PdfFileExtractor");
        await Assert.That(pdfExtractor.MimeType).IsEqualTo("application/pdf");
        await Assert.That(pdfExtractor.Description).Contains("PDF documents");
    }

    [Test]
    public void DisposalTest_ShouldCleanupResources_WhenDisposed()
    {
        // Act & Assert - Should not throw on dispose
        _service!.Dispose();
        
        // Should be able to dispose multiple times without error
        _service!.Dispose();
        
        // Test passes if no exception thrown
    }
}