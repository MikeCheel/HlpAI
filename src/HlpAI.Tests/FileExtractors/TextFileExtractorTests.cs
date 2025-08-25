using HlpAI.FileExtractors;

namespace HlpAI.Tests.FileExtractors;

public class TextFileExtractorTests
{
    private TextFileExtractor _extractor = null!;
    private string _testFilePath = null!;

    [Before(Test)]
    public void Setup()
    {
        _extractor = new TextFileExtractor();
        _testFilePath = Path.GetTempFileName();
    }

    [After(Test)]
    public void Cleanup()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Test]
    [Arguments(".txt")]
    [Arguments(".md")]
    [Arguments(".log")]
    [Arguments(".csv")]
    [Arguments(".TXT")]
    [Arguments(".MD")]
    public async Task CanHandle_SupportedExtensions_ReturnsTrue(string extension)
    {
        // Arrange
        var filePath = $"test{extension}";

        // Act
        var result = _extractor.CanHandle(filePath);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    [Arguments(".pdf")]
    [Arguments(".html")]
    [Arguments(".docx")]
    [Arguments(".exe")]
    [Arguments("")]
    public async Task CanHandle_UnsupportedExtensions_ReturnsFalse(string extension)
    {
        // Arrange
        var filePath = $"test{extension}";

        // Act
        var result = _extractor.CanHandle(filePath);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task CanHandle_NullOrEmpty_ReturnsFalse()
    {
        // Act & Assert
        await Assert.That(_extractor.CanHandle("")).IsFalse();
        await Assert.That(_extractor.CanHandle("test")).IsFalse();
    }

    [Test]
    public async Task ExtractTextAsync_ValidFile_ReturnsContent()
    {
        // Arrange
        const string expectedContent = "This is test content\nWith multiple lines\nAnd special characters: ñáéíóú";
        await File.WriteAllTextAsync(_testFilePath, expectedContent);

        // Act
        var result = await _extractor.ExtractTextAsync(_testFilePath);

        // Assert
        await Assert.That(result).IsEqualTo(expectedContent);
    }

    [Test]
    public async Task ExtractTextAsync_EmptyFile_ReturnsEmptyString()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, "");

        // Act
        var result = await _extractor.ExtractTextAsync(_testFilePath);

        // Assert
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task ExtractTextAsync_FileWithUTF8_ReturnsCorrectContent()
    {
        // Arrange
        const string expectedContent = "UTF-8 content: 你好世界 🌍 ñáéíóú";
        await File.WriteAllTextAsync(_testFilePath, expectedContent, System.Text.Encoding.UTF8);

        // Act
        var result = await _extractor.ExtractTextAsync(_testFilePath);

        // Assert
        await Assert.That(result).IsEqualTo(expectedContent);
    }

    [Test]
    public async Task ExtractTextAsync_NonExistentFile_ThrowsException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "non_existent_file.txt");

        // Act & Assert
        await Assert.That(async () =>
        {
            await _extractor.ExtractTextAsync(nonExistentPath);
        }).Throws<FileNotFoundException>();
    }

    [Test]
    public async Task GetMimeType_Always_ReturnsTextPlain()
    {
        // Act
        var result = _extractor.GetMimeType();

        // Assert
        await Assert.That(result).IsEqualTo("text/plain");
    }

    [Test]
    public async Task ExtractTextAsync_LargeFile_HandlesCorrectly()
    {
        // Arrange
        var largeContent = string.Concat(Enumerable.Repeat("This is a line of text.\n", 10000));
        await File.WriteAllTextAsync(_testFilePath, largeContent);

        // Act
        var result = await _extractor.ExtractTextAsync(_testFilePath);

        // Assert
        await Assert.That(result).IsEqualTo(largeContent);
        await Assert.That(result.Length > 200000).IsTrue(); // Should be substantial
    }

    [Test]
    [Arguments("test.txt")]
    [Arguments("path/to/file.md")]
    [Arguments("C:\\Windows\\file.log")]
    [Arguments("/usr/local/file.csv")]
    public async Task CanHandle_VariousFilePaths_WorksCorrectly(string filePath)
    {
        // Act
        var result = _extractor.CanHandle(filePath);

        // Assert
        await Assert.That(result).IsTrue();
    }
}