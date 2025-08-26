using HlpAI.FileExtractors;
using HlpAI.Models;
using HlpAI.Tests.TestHelpers;

namespace HlpAI.Tests.Integration;

public class FileExtractorIntegrationTests
{
    private string _testDirectory = null!;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("integration");
    }

    [After(Test)]
    public void Cleanup()
    {
        FileTestHelper.SafeDeleteDirectory(_testDirectory);
    }

    [Test]
    public async Task AllExtractors_WithVariousFileTypes_ExtractContentCorrectly()
    {
        // Arrange
        var extractors = new IFileExtractor[]
        {
            new TextFileExtractor(),
            new HtmlFileExtractor()
        };

        var testFiles = new Dictionary<string, string>
        {
            { "document.txt", "This is plain text content with some details." },
            { "readme.md", FileTestHelper.CreateSampleMarkdown() },
            { "page.html", FileTestHelper.CreateSampleHtml(true, true) },
            { "data.log", "2024-01-01 10:00:00 INFO Application started\n2024-01-01 10:01:00 DEBUG Processing request" },
            { "data.csv", "Name,Age,City\nJohn,30,NYC\nJane,25,LA\nBob,35,Chicago" }
        };

        // Create test files
        foreach (var (fileName, content) in testFiles)
        {
            var filePath = Path.Combine(_testDirectory, fileName);
            await File.WriteAllTextAsync(filePath, content);
        }

        // Act & Assert
        foreach (var (fileName, originalContent) in testFiles)
        {
            var filePath = Path.Combine(_testDirectory, fileName);
            var extractor = extractors.FirstOrDefault(e => e.CanHandle(filePath));

            if (extractor != null)
            {
                var extractedContent = await extractor.ExtractTextAsync(filePath);
                
                await Assert.That(extractedContent).IsNotNull();
                await Assert.That(extractedContent).IsNotEmpty();
                
                // For text files, content should match exactly
                if (extractor is TextFileExtractor)
                {
                    await Assert.That(extractedContent).IsEqualTo(originalContent);
                }
                
                // For HTML files, should contain the main text without tags
                if (extractor is HtmlFileExtractor)
                {
                    await Assert.That(extractedContent).Contains("Main Heading");
                     await Assert.That(extractedContent).DoesNotContain("<h1>");
                     await Assert.That(extractedContent).DoesNotContain("font-family");
                     await Assert.That(extractedContent).DoesNotContain("console.log");
                }
            }
        }
    }

    [Test]
    public async Task ExtractorChain_SelectsCorrectExtractor_ForEachFileType()
    {
        // Arrange
        var extractors = new IFileExtractor[]
        {
            new TextFileExtractor(),
            new HtmlFileExtractor()
        };

        (string, Type)[] testCases = new[]
        {
            ("test.txt", typeof(TextFileExtractor)),
            ("test.md", typeof(TextFileExtractor)),
            ("test.log", typeof(TextFileExtractor)),
            ("test.csv", typeof(TextFileExtractor)),
            ("test.html", typeof(HtmlFileExtractor)),
            ("test.htm", typeof(HtmlFileExtractor))
        };

        // Act & Assert
        foreach (var (fileName, expectedExtractorType) in testCases)
        {
            var filePath = Path.Combine(_testDirectory, fileName);
            var selectedExtractor = extractors.FirstOrDefault(e => e.CanHandle(filePath));
            
            await Assert.That(selectedExtractor).IsNotNull();
            await Assert.That(selectedExtractor!.GetType()).IsEqualTo(expectedExtractorType);
        }
    }

    [Test]
    public async Task AllExtractors_WithLargeFiles_HandlePerformanceCorrectly()
    {
        // Arrange
        IFileExtractor[] extractors = new IFileExtractor[]
        {
            new TextFileExtractor(),
            new HtmlFileExtractor()
        };

        // Create large text file (about 1MB)
        var largeTextContent = string.Concat(Enumerable.Repeat("This is a line of text with some content to make it substantial.\n", 10000));
        var largeTextFile = Path.Combine(_testDirectory, "large.txt");
        await File.WriteAllTextAsync(largeTextFile, largeTextContent);

        // Create large HTML file
        var largeHtmlContent = FileTestHelper.CreateSampleHtml(true, true);
        largeHtmlContent = string.Concat(Enumerable.Repeat(largeHtmlContent, 100)); // Repeat to make it large
        var largeHtmlFile = Path.Combine(_testDirectory, "large.html");
        await File.WriteAllTextAsync(largeHtmlFile, largeHtmlContent);

        string[] testFiles = new[] { largeTextFile, largeHtmlFile };

        // Act & Assert
        foreach (var filePath in testFiles)
        {
            var extractor = extractors.FirstOrDefault(e => e.CanHandle(filePath));
            await Assert.That(extractor).IsNotNull();

            var startTime = DateTime.UtcNow;
            var extractedContent = await extractor!.ExtractTextAsync(filePath);
            var duration = DateTime.UtcNow - startTime;

            await Assert.That(extractedContent).IsNotNull();
             await Assert.That(extractedContent.Length > 1000).IsTrue();
         await Assert.That(duration.TotalSeconds < 30).IsTrue();
        }
    }

    [Test]
    public async Task ExtractorMimeTypes_ReturnCorrectValues()
    {
        // Arrange
        var extractors = new Dictionary<string, string>
        {
            { nameof(TextFileExtractor), "text/plain" },
            { nameof(HtmlFileExtractor), "text/html" }
        };

        // Act & Assert
        var textExtractor = new TextFileExtractor();
        await Assert.That(textExtractor.GetMimeType()).IsEqualTo("text/plain");

        var htmlExtractor = new HtmlFileExtractor();
        await Assert.That(htmlExtractor.GetMimeType()).IsEqualTo("text/html");
    }

    [Test]
    public async Task ExtractorChain_WithUnsupportedFiles_ReturnsNull()
    {
        // Arrange
        IFileExtractor[] extractors = new IFileExtractor[]
        {
            new TextFileExtractor(),
            new HtmlFileExtractor()
        };

        string[] unsupportedFiles = new[]
        {
            "image.jpg",
            "archive.zip",
            "program.exe",
            "document.pdf", // PDF extractor not included in this test
            "unknown.xyz"
        };

        // Act & Assert
        foreach (var fileName in unsupportedFiles)
        {
            var filePath = Path.Combine(_testDirectory, fileName);
            var selectedExtractor = extractors.FirstOrDefault(e => e.CanHandle(filePath));
            
            await Assert.That(selectedExtractor).IsNull();
        }
    }

    [Test]
    public async Task ExtractorChain_WithMalformedFiles_HandlesGracefully()
    {
        // Arrange
        IFileExtractor[] extractors = new IFileExtractor[]
        {
            new TextFileExtractor(),
            new HtmlFileExtractor()
        };

        // Create malformed HTML file
        var malformedHtml = """
            <html>
            <body>
                <p>Unclosed paragraph
                <div>Unclosed div
                Some loose text
                <script>broken javascript code {{{
            </body>
            """;
        
        var malformedHtmlFile = Path.Combine(_testDirectory, "malformed.html");
        await File.WriteAllTextAsync(malformedHtmlFile, malformedHtml);

        // Create text file with special characters
        var specialTextContent = "Special chars: ñáéíóú 你好世界 🌍 \0 \x01 \x02";
        var specialTextFile = Path.Combine(_testDirectory, "special.txt");
        await File.WriteAllTextAsync(specialTextFile, specialTextContent);

        string[] testFiles = new[] { malformedHtmlFile, specialTextFile };

        // Act & Assert
        foreach (var filePath in testFiles)
        {
            var extractor = extractors.FirstOrDefault(e => e.CanHandle(filePath));
            await Assert.That(extractor).IsNotNull();

            // Should not throw exception
            var extractedContent = await extractor!.ExtractTextAsync(filePath);
            await Assert.That(extractedContent).IsNotNull();
        }
    }

    [Test]
    public async Task ExtractorPerformance_WithMultipleFiles_CompletesWithinTimeLimit()
    {
        // Arrange
        IFileExtractor[] extractors = new IFileExtractor[]
        {
            new TextFileExtractor(),
            new HtmlFileExtractor()
        };

        // Create multiple test files
        var testFiles = new List<string>();
        for (int i = 0; i < 50; i++)
        {
            var content = $"Content for file {i} with some additional text to make it more substantial.";
            var extension = i % 2 == 0 ? ".txt" : ".html";
            if (extension == ".html")
            {
                content = $"<html><body><h1>File {i}</h1><p>{content}</p></body></html>";
            }
            
            var filePath = Path.Combine(_testDirectory, $"file{i}{extension}");
            await File.WriteAllTextAsync(filePath, content);
            testFiles.Add(filePath);
        }

        // Act
        var startTime = DateTime.UtcNow;
        var results = new List<string>();

        foreach (var filePath in testFiles)
        {
            var extractor = extractors.FirstOrDefault(e => e.CanHandle(filePath));
            if (extractor != null)
            {
                var content = await extractor.ExtractTextAsync(filePath);
                results.Add(content);
            }
        }

        var duration = DateTime.UtcNow - startTime;

        // Assert
        await Assert.That(results.Count).IsEqualTo(50);
         await Assert.That(duration.TotalSeconds).IsLessThan(10);
         
         foreach (var result in results)
         {
             await Assert.That(result).IsNotNull();
             await Assert.That(result).IsNotEmpty();
         }
    }
}