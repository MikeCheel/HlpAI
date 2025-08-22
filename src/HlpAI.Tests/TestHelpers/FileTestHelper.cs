namespace HlpAI.Tests.TestHelpers;

/// <summary>
/// Helper utilities for file-based tests
/// </summary>
public static class FileTestHelper
{
    /// <summary>
    /// Creates a temporary test directory with optional subdirectories and files
    /// </summary>
    public static string CreateTempDirectory(string? prefix = null)
    {
        var dirName = prefix ?? "test";
        var tempDir = Path.Combine(Path.GetTempPath(), $"{dirName}_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    /// <summary>
    /// Creates a temporary file with specified content and extension
    /// </summary>
    public static string CreateTempFile(string content, string extension = ".txt", string? directory = null)
    {
        var dir = directory ?? Path.GetTempPath();
        var fileName = $"test_{Guid.NewGuid().ToString()[..8]}{extension}";
        var filePath = Path.Combine(dir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    /// <summary>
    /// Creates a temporary binary file with specified bytes
    /// </summary>
    public static string CreateTempBinaryFile(byte[] content, string extension, string? directory = null)
    {
        var dir = directory ?? Path.GetTempPath();
        var fileName = $"test_{Guid.NewGuid().ToString()[..8]}{extension}";
        var filePath = Path.Combine(dir, fileName);
        File.WriteAllBytes(filePath, content);
        return filePath;
    }

    /// <summary>
    /// Safely deletes a directory and all its contents
    /// </summary>
    public static void SafeDeleteDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            try
            {
                Directory.Delete(directoryPath, true);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }

    /// <summary>
    /// Safely deletes a file
    /// </summary>
    public static void SafeDeleteFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }

    /// <summary>
    /// Creates a test directory structure with various file types
    /// </summary>
    public static string CreateTestFileStructure()
    {
        var rootDir = CreateTempDirectory("filestructure");
        
        // Create various file types
        CreateTempFile("This is a text file content", ".txt", rootDir);
        CreateTempFile("# Markdown Header\nThis is markdown content", ".md", rootDir);
        CreateTempFile("<html><body><h1>HTML Content</h1></body></html>", ".html", rootDir);
        CreateTempFile("Log entry: Application started", ".log", rootDir);
        CreateTempFile("CSV,Header,Values\n1,2,3", ".csv", rootDir);
        
        // Create unsupported files
        CreateTempBinaryFile([0xFF, 0xD8, 0xFF], ".jpg", rootDir); // Fake JPEG
        CreateTempBinaryFile([0x50, 0x4B], ".zip", rootDir); // Fake ZIP
        CreateTempBinaryFile([0x4D, 0x5A], ".exe", rootDir); // Fake EXE
        
        // Create hidden file
        CreateTempFile("Hidden content", "", rootDir); 
        var hiddenFile = Path.Combine(rootDir, ".hidden");
        File.Move(Path.Combine(rootDir, $"test_{hiddenFile.Split('_')[1]}"), hiddenFile);
        
        // Create subdirectory with files
        var subDir = Path.Combine(rootDir, "subdirectory");
        Directory.CreateDirectory(subDir);
        CreateTempFile("Subdirectory text file", ".txt", subDir);
        CreateTempFile("Subdirectory HTML file", ".html", subDir);
        
        return rootDir;
    }

    /// <summary>
    /// Creates sample HTML content for testing
    /// </summary>
    public static string CreateSampleHtml(bool includeScripts = false, bool includeStyles = false)
    {
        var html = """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <title>Test HTML Document</title>
            """;

        if (includeStyles)
        {
            html += """
                <style>
                    body { font-family: Arial, sans-serif; }
                    .header { color: blue; }
                </style>
                """;
        }

        if (includeScripts)
        {
            html += """
                <script>
                    function testFunction() {
                        console.log("This is a test script");
                    }
                </script>
                """;
        }

        html += """
            </head>
            <body>
                <header>
                    <h1>Main Heading</h1>
                    <nav>Navigation content</nav>
                </header>
                <main>
                    <section>
                        <h2>Section Title</h2>
                        <p>This is a paragraph with <strong>bold</strong> and <em>italic</em> text.</p>
                        <ul>
                            <li>List item 1</li>
                            <li>List item 2</li>
                        </ul>
                    </section>
                </main>
                <footer>
                    <p>Footer content</p>
                </footer>
            """;

        if (includeScripts)
        {
            html += """
                <script>
                    document.addEventListener('DOMContentLoaded', function() {
                        testFunction();
                    });
                </script>
                """;
        }

        html += """
            </body>
            </html>
            """;

        return html;
    }

    /// <summary>
    /// Creates sample markdown content for testing
    /// </summary>
    public static string CreateSampleMarkdown()
    {
        return """
            # Main Title
            
            This is a markdown document with various elements.
            
            ## Section 1
            
            This section contains **bold text** and *italic text*.
            
            ### Subsection
            
            Here's a list:
            - Item 1
            - Item 2
            - Item 3
            
            ## Section 2
            
            Here's some code:
            
            ```csharp
            public class Example
            {
                public string Name { get; set; }
            }
            ```
            
            And a [link](https://example.com).
            
            > This is a blockquote.
            
            ## Conclusion
            
            This concludes the sample markdown.
            """;
    }
}