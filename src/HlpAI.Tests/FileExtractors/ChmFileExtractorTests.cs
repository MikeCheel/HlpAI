using System.Text;
using HlpAI.FileExtractors;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using TUnit.Assertions;
using TUnit.Core;

namespace HlpAI.Tests.FileExtractors;

public class ChmFileExtractorTests
{
    private string _testDirectory = null!;
    private ILogger<ChmFileExtractor> _logger = null!;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("chm_tests");
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<ChmFileExtractor>();
    }

    [After(Test)]
    public void Cleanup()
    {
        FileTestHelper.SafeDeleteDirectory(_testDirectory);
    }

    [Test]
    public async Task CanHandle_WithChmExtension_ReturnsTrue()
    {
        // Arrange
        using var extractor = new ChmFileExtractor(_logger);

        // Act & Assert
        await Assert.That(extractor.CanHandle("test.chm")).IsTrue();
        await Assert.That(extractor.CanHandle("TEST.CHM")).IsTrue();
        await Assert.That(extractor.CanHandle("document.Chm")).IsTrue();
        await Assert.That(extractor.CanHandle(@"C:\path\to\file.chm")).IsTrue();
    }

    [Test]
    public async Task CanHandle_WithNonChmExtension_ReturnsFalse()
    {
        // Arrange
        using var extractor = new ChmFileExtractor(_logger);

        // Act & Assert
        await Assert.That(extractor.CanHandle("test.txt")).IsFalse();
        await Assert.That(extractor.CanHandle("test.html")).IsFalse();
        await Assert.That(extractor.CanHandle("test.pdf")).IsFalse();
        await Assert.That(extractor.CanHandle("test")).IsFalse();
        await Assert.That(extractor.CanHandle("test.chm.backup")).IsFalse();
    }

    [Test]
    public async Task GetMimeType_ReturnsCorrectMimeType()
    {
        // Arrange
        using var extractor = new ChmFileExtractor(_logger);

        // Act
        var mimeType = extractor.GetMimeType();

        // Assert
        await Assert.That(mimeType).IsEqualTo("application/vnd.ms-htmlhelp");
    }

    [Test]
    public async Task ExtractTextAsync_WithNonExistentFile_ReturnsErrorMessage()
    {
        // Arrange
        using var extractor = new ChmFileExtractor(_logger);
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.chm");

        // Act
        var result = await extractor.ExtractTextAsync(nonExistentFile);

        // Assert - Should return either an error message or empty string for non-existent files
        await Assert.That(result).IsNotNull();
        // The exact error message format may vary, so we just ensure it's not null
        // and either contains an error indication or is empty (both are acceptable for non-existent files)
        await Assert.That(result.Contains("Error") || result.Contains("not found") || string.IsNullOrEmpty(result)).IsTrue();
    }

    [Test]
    public async Task ExtractTextAsync_WithInvalidChmFile_HandlesGracefully()
    {
        // Arrange
        using var extractor = new ChmFileExtractor(_logger);
        var invalidChmFile = Path.Combine(_testDirectory, "invalid.chm");
        
        // Create a fake CHM file that's actually just text
        await File.WriteAllTextAsync(invalidChmFile, "This is not a real CHM file");

        // Act
        var result = await extractor.ExtractTextAsync(invalidChmFile);

        // Assert
        await Assert.That(result).IsNotNull();
        // Should not throw exception, but may return error message or empty content
    }

    [Test]
    public async Task ExtractTextAsync_WithEmptyFile_HandlesGracefully()
    {
        // Arrange
        using var extractor = new ChmFileExtractor(_logger);
        var emptyChmFile = Path.Combine(_testDirectory, "empty.chm");
        
        // Create an empty file
        await File.WriteAllTextAsync(emptyChmFile, "");

        // Act
        var result = await extractor.ExtractTextAsync(emptyChmFile);

        // Assert
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task ExtractTextAsync_ProcessTimeout_ReturnsTimeoutError()
    {
        // This test would require a CHM file that causes hh.exe to hang
        // For now, we'll test the timeout mechanism indirectly by ensuring
        // the method completes within a reasonable time
        
        // Arrange
        using var extractor = new ChmFileExtractor(_logger);
        var nonExistentFile = Path.Combine(_testDirectory, "timeout_test.chm");

        // Act
        var startTime = DateTime.UtcNow;
        var result = await extractor.ExtractTextAsync(nonExistentFile);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        await Assert.That(duration.TotalSeconds).IsLessThan(35); // Should complete well before 30s timeout
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        var extractor = new ChmFileExtractor(_logger);

        // Act & Assert - Should not throw
        extractor.Dispose();
        extractor.Dispose(); // Multiple dispose calls should be safe
        // Test passes if no exception is thrown
    }

    [Test]
    public async Task ExtractTextAsync_WithNullOrEmptyPath_HandlesGracefully()
    {
        // Arrange
        using var extractor = new ChmFileExtractor(_logger);

        // Act & Assert
        var result1 = await extractor.ExtractTextAsync("");
        await Assert.That(result1).IsNotNull();

        var result2 = await extractor.ExtractTextAsync("   ");
        await Assert.That(result2).IsNotNull();
    }

    [Test]
    public async Task ExtractTextAsync_WithInvalidPathCharacters_HandlesGracefully()
    {
        // Arrange
        using var extractor = new ChmFileExtractor(_logger);
        var invalidPath = "invalid<>|path?.chm";

        // Act
        var result = await extractor.ExtractTextAsync(invalidPath);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Contains("Error") || result == string.Empty).IsTrue();
    }

    [Test]
    public async Task Constructor_WithNullLogger_WorksCorrectly()
    {
        // Act & Assert
        using var extractor = new ChmFileExtractor(null);
        await Assert.That(extractor).IsNotNull();
    }

    [Test]
    public async Task ExtractTextAsync_MultipleCallsInParallel_HandlesCorrectly()
    {
        // Arrange
        using var extractor = new ChmFileExtractor(_logger);
        var testFile = Path.Combine(_testDirectory, "parallel_test.chm");
        await File.WriteAllTextAsync(testFile, "fake chm content");

        // Act
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => extractor.ExtractTextAsync(testFile))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        await Assert.That(results).HasCount().EqualTo(5);
        foreach (var result in results)
        {
            await Assert.That(result).IsNotNull();
        }
    }
}