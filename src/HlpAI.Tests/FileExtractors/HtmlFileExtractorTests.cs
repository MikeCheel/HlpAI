using HlpAI.FileExtractors;
using TUnit.Assertions;
using TUnit.Core;

namespace HlpAI.Tests.FileExtractors;

public class HtmlFileExtractorTests
{
    private HtmlFileExtractor _extractor = null!;
    private string _testFilePath = null!;

    [Before(Test)]
    public void Setup()
    {
        _extractor = new HtmlFileExtractor();
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
    [Arguments(".html")]
    [Arguments(".htm")]
    [Arguments(".HTML")]
    [Arguments(".HTM")]
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
    [Arguments(".txt")]
    [Arguments(".pdf")]
    [Arguments(".docx")]
    [Arguments(".xml")]
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
    public async Task ExtractTextAsync_SimpleHtml_ReturnsTextContent()
    {
        // Arrange
        const string htmlContent = """
            <!DOCTYPE html>
            <html>
            <head>
                <title>Test Page</title>
            </head>
            <body>
                <h1>Main Heading</h1>
                <p>This is a paragraph with some text.</p>
                <div>Content in a div</div>
            </body>
            </html>
            """;
        
        await File.WriteAllTextAsync(_testFilePath, htmlContent);

        // Act
        var result = await _extractor.ExtractTextAsync(_testFilePath);

        // Assert
        await Assert.That(result).Contains("Test Page");
        await Assert.That(result).Contains("Main Heading");
        await Assert.That(result).Contains("This is a paragraph with some text.");
        await Assert.That(result).Contains("Content in a div");
    }

    [Test]
    public async Task ExtractTextAsync_HtmlWithScriptTags_RemovesScriptContent()
    {
        // Arrange
        const string htmlContent = """
            <html>
            <head>
                <script>
                    function someFunction() {
                        console.log("This should not appear in extracted text");
                    }
                </script>
            </head>
            <body>
                <p>Visible text content</p>
                <script>
                    alert("Another script");
                </script>
            </body>
            </html>
            """;
        
        await File.WriteAllTextAsync(_testFilePath, htmlContent);

        // Act
        var result = await _extractor.ExtractTextAsync(_testFilePath);

        // Assert
        await Assert.That(result).Contains("Visible text content");
        await Assert.That(result).DoesNotContain("someFunction");
        await Assert.That(result).DoesNotContain("console.log");
        await Assert.That(result).DoesNotContain("alert");
    }

    [Test]
    public async Task ExtractTextAsync_HtmlWithStyleTags_RemovesStyleContent()
    {
        // Arrange
        const string htmlContent = """
            <html>
            <head>
                <style>
                    body { font-family: Arial; }
                    .header { color: blue; }
                </style>
            </head>
            <body>
                <h1 class="header">Styled Heading</h1>
                <p>Paragraph text</p>
                <style>
                    .footer { margin-top: 20px; }
                </style>
            </body>
            </html>
            """;
        
        await File.WriteAllTextAsync(_testFilePath, htmlContent);

        // Act
        var result = await _extractor.ExtractTextAsync(_testFilePath);

        // Assert
        await Assert.That(result).Contains("Styled Heading");
        await Assert.That(result).Contains("Paragraph text");
        await Assert.That(result).DoesNotContain("font-family");
        await Assert.That(result).DoesNotContain("color: blue");
        await Assert.That(result).DoesNotContain("margin-top");
    }

    [Test]
    public async Task ExtractTextAsync_EmptyHtml_ReturnsEmptyContent()
    {
        // Arrange
        const string htmlContent = "<html><head></head><body></body></html>";
        await File.WriteAllTextAsync(_testFilePath, htmlContent);

        // Act
        var result = await _extractor.ExtractTextAsync(_testFilePath);

        // Assert
        await Assert.That(string.IsNullOrEmpty(result?.Trim())).IsTrue();
    }

    [Test]
    public async Task ExtractTextAsync_MalformedHtml_HandlesGracefully()
    {
        // Arrange
        const string htmlContent = """
            <html>
            <body>
                <p>Unclosed paragraph
                <div>Unclosed div
                <h1>Heading without closing tag
                Some loose text
            </body>
            """;
        
        await File.WriteAllTextAsync(_testFilePath, htmlContent);

        // Act
        var result = await _extractor.ExtractTextAsync(_testFilePath);

        // Assert
        await Assert.That(result).Contains("Unclosed paragraph");
        await Assert.That(result).Contains("Unclosed div");
        await Assert.That(result).Contains("Heading without closing tag");
        await Assert.That(result).Contains("Some loose text");
    }

    [Test]
    public async Task ExtractTextAsync_HtmlWithSpecialCharacters_PreservesCharacters()
    {
        // Arrange
        const string htmlContent = """
            <html>
            <body>
                <p>Special characters: &amp; &lt; &gt; &quot; &#39;</p>
                <p>Unicode: ñáéíóú 你好世界 🌍</p>
                <p>Symbols: © ® ™ € £ ¥</p>
            </body>
            </html>
            """;
        
        await File.WriteAllTextAsync(_testFilePath, htmlContent);

        // Act
        var result = await _extractor.ExtractTextAsync(_testFilePath);

        // Assert
        await Assert.That(result).Contains("Special characters");
        await Assert.That(result).Contains("Unicode");
        await Assert.That(result).Contains("Symbols");
        // Should decode HTML entities
        await Assert.That(result).Contains("&");
    }

    [Test]
    public async Task ExtractTextAsync_ComplexHtmlStructure_ExtractsAllText()
    {
        // Arrange
        const string htmlContent = """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <title>Complex Document</title>
                <script src="app.js"></script>
                <style>body { margin: 0; }</style>
            </head>
            <body>
                <header>
                    <h1>Document Title</h1>
                    <nav>
                        <ul>
                            <li><a href="#section1">Section 1</a></li>
                            <li><a href="#section2">Section 2</a></li>
                        </ul>
                    </nav>
                </header>
                <main>
                    <section id="section1">
                        <h2>First Section</h2>
                        <p>Content of first section</p>
                    </section>
                    <section id="section2">
                        <h2>Second Section</h2>
                        <article>
                            <h3>Article Title</h3>
                            <p>Article content with <strong>bold</strong> and <em>italic</em> text.</p>
                        </article>
                    </section>
                </main>
                <footer>
                    <p>Footer content</p>
                </footer>
            </body>
            </html>
            """;
        
        await File.WriteAllTextAsync(_testFilePath, htmlContent);

        // Act
        var result = await _extractor.ExtractTextAsync(_testFilePath);

        // Assert
        await Assert.That(result).Contains("Complex Document");
        await Assert.That(result).Contains("Document Title");
        await Assert.That(result).Contains("Section 1");
        await Assert.That(result).Contains("Section 2");
        await Assert.That(result).Contains("First Section");
        await Assert.That(result).Contains("Content of first section");
        await Assert.That(result).Contains("Second Section");
        await Assert.That(result).Contains("Article Title");
        await Assert.That(result).Contains("Article content");
        await Assert.That(result).Contains("bold");
        await Assert.That(result).Contains("italic");
        await Assert.That(result).Contains("Footer content");

        // Should not contain script or style content
        await Assert.That(result).DoesNotContain("app.js");
        await Assert.That(result).DoesNotContain("margin: 0");
    }

    [Test]
    public async Task ExtractTextAsync_HtmlWithComments_IgnoresComments()
    {
        // Arrange
        const string htmlContent = """
            <html>
            <body>
                <!-- This is a comment that should not appear -->
                <p>Visible text</p>
                <!-- Another comment -->
                <div>More visible text</div>
                <!-- Multi-line comment
                     that spans multiple
                     lines -->
            </body>
            </html>
            """;
        
        await File.WriteAllTextAsync(_testFilePath, htmlContent);

        // Act
        var result = await _extractor.ExtractTextAsync(_testFilePath);

        // Assert
        await Assert.That(result).Contains("Visible text");
        await Assert.That(result).Contains("More visible text");
        await Assert.That(result).DoesNotContain("This is a comment");
        await Assert.That(result).DoesNotContain("Another comment");
        await Assert.That(result).DoesNotContain("Multi-line comment");
    }

    [Test]
    public async Task GetMimeType_Always_ReturnsTextHtml()
    {
        // Act
        var result = _extractor.GetMimeType();

        // Assert
        await Assert.That(result).IsEqualTo("text/html");
    }

    [Test]
    public async Task ExtractTextAsync_NonExistentFile_ThrowsException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "non_existent_file.html");

        // Act & Assert
        await Assert.That(async () =>
        {
            await _extractor.ExtractTextAsync(nonExistentPath);
        }).Throws<FileNotFoundException>();
    }

    [Test]
    public async Task ExtractTextAsync_PlainTextContent_ReturnsAsIs()
    {
        // Arrange
        const string plainText = "This is just plain text without HTML tags";
        await File.WriteAllTextAsync(_testFilePath, plainText);

        // Act
        var result = await _extractor.ExtractTextAsync(_testFilePath);

        // Assert
        await Assert.That(result?.Trim()).IsEqualTo(plainText);
    }
}