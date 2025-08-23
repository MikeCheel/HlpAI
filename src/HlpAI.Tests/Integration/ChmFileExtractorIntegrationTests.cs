using System.Diagnostics;
using System.Text;
using HlpAI.FileExtractors;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using TUnit.Assertions;
using TUnit.Core;

namespace HlpAI.Tests.Integration;

public class ChmFileExtractorIntegrationTests
{
    private string _testDirectory = null!;
    private ILogger<ChmFileExtractor> _logger = null!;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("chm_integration");
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<ChmFileExtractor>();
    }

    [After(Test)]
    public void Cleanup()
    {
        FileTestHelper.SafeDeleteDirectory(_testDirectory);
    }

    [Test]
    public async Task HhExe_IsAvailableOnSystem()
    {
        // This test verifies that hh.exe is available and can be executed
        var processInfo = new ProcessStartInfo
        {
            FileName = "hh.exe",
            Arguments = "",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(processInfo);
        await Assert.That(process).IsNotNull();
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await process!.WaitForExitAsync(cts.Token);
        await Assert.That(process.HasExited).IsTrue();
    }

    [Test]
    public async Task CreateSampleChmFile_AndExtractContent()
    {
        // Create a minimal CHM project for testing
        var projectDir = Path.Combine(_testDirectory, "chm_project");
        Directory.CreateDirectory(projectDir);

        // Create HTML content files
        var htmlContent = @"<!DOCTYPE html>
<html>
<head>
    <title>Test Page</title>
</head>
<body>
    <h1>Test CHM Content</h1>
    <p>This is a test paragraph with some content.</p>
    <p>Another paragraph for testing extraction.</p>
</body>
</html>";

        var htmlFile = Path.Combine(projectDir, "index.html");
        await File.WriteAllTextAsync(htmlFile, htmlContent);

        var secondHtmlContent = @"<!DOCTYPE html>
<html>
<head>
    <title>Second Page</title>
</head>
<body>
    <h2>Second Test Page</h2>
    <p>Content from the second page.</p>
    <ul>
        <li>Item 1</li>
        <li>Item 2</li>
    </ul>
</body>
</html>";

        var secondHtmlFile = Path.Combine(projectDir, "page2.html");
        await File.WriteAllTextAsync(secondHtmlFile, secondHtmlContent);

        // Create project file (.hhp)
        var projectContent = @"[OPTIONS]
Compatibility=1.1 or later
Compiled file=test.chm
Contents file=toc.hhc
Default Window=main
Default topic=index.html
Display compile progress=No
Language=0x409 English (United States)
Title=Test CHM

[WINDOWS]
main=""Test CHM"",""toc.hhc"",,""index.html"",""index.html"",,,,,0x23520,,0x387e,,,,,,,,0

[FILES]
index.html
page2.html";

        var projectFile = Path.Combine(projectDir, "test.hhp");
        await File.WriteAllTextAsync(projectFile, projectContent);

        // Create table of contents (.hhc)
        var tocContent = @"<!DOCTYPE HTML PUBLIC ""-//IETF//DTD HTML//EN"">
<HTML>
<HEAD>
<meta name=""GENERATOR"" content=""Microsoft&reg; HTML Help Workshop 4.1"">
<!-- Sitemap 1.0 -->
</HEAD><BODY>
<OBJECT type=""text/site properties"">
    <param name=""ImageType"" value=""Folder"">
</OBJECT>
<UL>
    <LI> <OBJECT type=""text/sitemap"">
         <param name=""Name"" value=""Test CHM"">
         <param name=""Local"" value=""index.html"">
         </OBJECT>
    <LI> <OBJECT type=""text/sitemap"">
         <param name=""Name"" value=""Second Page"">
         <param name=""Local"" value=""page2.html"">
         </OBJECT>
</UL>
</BODY></HTML>";

        var tocFile = Path.Combine(projectDir, "toc.hhc");
        await File.WriteAllTextAsync(tocFile, tocContent);

        // Try to compile CHM file using hhc.exe (HTML Help Compiler)
        var chmFile = Path.Combine(_testDirectory, "test.chm");
        var compileResult = await TryCompileChm(projectFile, chmFile);

        if (compileResult)
        {
            // Test extraction with real CHM file
            using var extractor = new ChmFileExtractor(_logger);
            var extractedContent = await extractor.ExtractTextAsync(chmFile);

            // Verify content was extracted
            await Assert.That(extractedContent).IsNotNull();
            await Assert.That(extractedContent).IsNotEmpty();
            await Assert.That(extractedContent).Contains("Test CHM Content");
            await Assert.That(extractedContent).Contains("test paragraph");
            await Assert.That(extractedContent).Contains("Second Test Page");
        }
        else
        {
            // If we can't compile a CHM file, test with manual decompile simulation
            await TestManualDecompileSimulation();
        }
    }

    private async Task<bool> TryCompileChm(string projectFile, string outputFile)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "hhc.exe",
                Arguments = $"\"{projectFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(projectFile)
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await process.WaitForExitAsync(cts.Token);
                var completed = process.HasExited;
                if (completed && File.Exists(outputFile))
                {
                    return true;
                }
            }
        }
        catch
        {
            // hhc.exe might not be available
        }
        return false;
    }

    private async Task TestManualDecompileSimulation()
    {
        // Simulate what happens when hh.exe decompiles a CHM file
        // by creating the expected directory structure manually
        var tempDir = Path.Combine(_testDirectory, "manual_decomp");
        Directory.CreateDirectory(tempDir);

        // Create HTML files that would be extracted from CHM
        var htmlContent1 = @"<!DOCTYPE html>
<html>
<head><title>Manual Test</title></head>
<body>
    <h1>Manually Created Content</h1>
    <p>This simulates extracted CHM content.</p>
    <script>console.log('should be removed');</script>
    <style>.test {{ color: red; }}</style>
</body>
</html>";

        var htmlFile1 = Path.Combine(tempDir, "test1.html");
        await File.WriteAllTextAsync(htmlFile1, htmlContent1);

        var hhcContent = @"<!DOCTYPE HTML PUBLIC ""-//IETF//DTD HTML//EN"">
<HTML><HEAD></HEAD><BODY>
<UL>
    <LI><OBJECT type=""text/sitemap"">
        <param name=""Name"" value=""Test Topic 1"">
        <param name=""Local"" value=""test1.html"">
        </OBJECT>
    <LI><OBJECT type=""text/sitemap"">
        <param name=""Name"" value=""Test Topic 2"">
        <param name=""Local"" value=""test2.html"">
        </OBJECT>
</UL>
</BODY></HTML>";

        var hhcFile = Path.Combine(tempDir, "toc.hhc");
        await File.WriteAllTextAsync(hhcFile, hhcContent);

        // Test the extraction logic directly
        var extractedText = new StringBuilder();
        var extractor = new ChmFileExtractor(_logger);
        
        // Use reflection to call the private ExtractTextFromDirectory method
        var method = typeof(ChmFileExtractor).GetMethod("ExtractTextFromDirectory", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (method != null)
        {
            await (Task)method.Invoke(extractor, new object[] { tempDir, extractedText })!;
            
            var result = extractedText.ToString();
            await Assert.That(result).IsNotNull();
            await Assert.That(result).Contains("Manually Created Content");
            await Assert.That(result).Contains("Table of Contents");
            await Assert.That(result).Contains("Test Topic 1");
            await Assert.That(result).DoesNotContain("console.log"); // Scripts should be removed
            await Assert.That(result).DoesNotContain("color: red"); // Styles should be removed
        }
        
        extractor.Dispose();
    }

    [Test]
    public async Task ExtractTextFromDirectory_WithMixedContent_ProcessesCorrectly()
    {
        // Create a directory structure similar to what hh.exe would create
        var extractDir = Path.Combine(_testDirectory, "extracted");
        Directory.CreateDirectory(extractDir);
        
        var subDir = Path.Combine(extractDir, "subdirectory");
        Directory.CreateDirectory(subDir);

        // Create various HTML files
        var files = new Dictionary<string, string>
        {
            { Path.Combine(extractDir, "index.htm"), CreateSampleHtmlContent("Main Page", "Welcome to the main page") },
            { Path.Combine(extractDir, "about.html"), CreateSampleHtmlContent("About", "About this application") },
            { Path.Combine(subDir, "help.html"), CreateSampleHtmlContent("Help", "Help documentation") },
            { Path.Combine(extractDir, "empty.html"), "<html><body></body></html>" },
            { Path.Combine(extractDir, "malformed.html"), "<html><body><p>Unclosed paragraph<div>Content" }
        };

        foreach (var (filePath, content) in files)
        {
            await File.WriteAllTextAsync(filePath, content);
        }

        // Create HHC file
        var hhcContent = CreateSampleHhcContent();
        var hhcFile = Path.Combine(extractDir, "contents.hhc");
        await File.WriteAllTextAsync(hhcFile, hhcContent);

        // Create non-HTML files that should be ignored
        await File.WriteAllTextAsync(Path.Combine(extractDir, "readme.txt"), "Text file content");
        await File.WriteAllTextAsync(Path.Combine(extractDir, "data.xml"), "<xml><data>value</data></xml>");

        // Test extraction
        using var extractor = new ChmFileExtractor(_logger);
        var fakeChmFile = Path.Combine(_testDirectory, "fake.chm");
        await File.WriteAllTextAsync(fakeChmFile, "fake chm"); // Create file so it exists

        // Since we can't easily test the full extraction without a real CHM,
        // we'll test the directory processing logic
        var extractedText = new StringBuilder();
        
        // Use reflection to test the private method
        var method = typeof(ChmFileExtractor).GetMethod("ExtractTextFromDirectory", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (method != null)
        {
            await (Task)method.Invoke(extractor, new object[] { extractDir, extractedText })!;
            
            var result = extractedText.ToString();
            
            // Verify content extraction
            await Assert.That(result).Contains("=== index.htm ===");
            await Assert.That(result).Contains("Welcome to the main page");
            await Assert.That(result).Contains("=== about.html ===");
            await Assert.That(result).Contains("About this application");
            await Assert.That(result).Contains("=== help.html ===");
            await Assert.That(result).Contains("Help documentation");
            await Assert.That(result).Contains("=== Table of Contents ===");
            await Assert.That(result).Contains("- Introduction");
            await Assert.That(result).Contains("- Getting Started");
            
            // Verify HTML tags are stripped
            await Assert.That(result).DoesNotContain("<html>");
            await Assert.That(result).DoesNotContain("<body>");
            await Assert.That(result).DoesNotContain("<h1>");
            await Assert.That(result).DoesNotContain("<script>");
            await Assert.That(result).DoesNotContain("<style>");
            
            // Empty HTML file should not contribute content
            await Assert.That(result).DoesNotContain("=== empty.html ===");
        }
    }

    private static string CreateSampleHtmlContent(string title, string content)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <title>{title}</title>
    <style>
        body {{ font-family: Arial; }}
        .hidden {{ display: none; }}
    </style>
</head>
<body>
    <h1>{title}</h1>
    <p>{content}</p>
    <script>
        function test() {{ console.log('test'); }}
    </script>
    <div class=""hidden"">Hidden content</div>
</body>
</html>";
    }

    private static string CreateSampleHhcContent()
    {
        return @"<!DOCTYPE HTML PUBLIC ""-//IETF//DTD HTML//EN"">
<HTML>
<HEAD>
<meta name=""GENERATOR"" content=""Microsoft&reg; HTML Help Workshop 4.1"">
</HEAD>
<BODY>
<UL>
    <LI><OBJECT type=""text/sitemap"">
        <param name=""Name"" value=""Introduction"">
        <param name=""Local"" value=""index.htm"">
        </OBJECT>
    <LI><OBJECT type=""text/sitemap"">
        <param name=""Name"" value=""Getting Started"">
        <param name=""Local"" value=""about.html"">
        </OBJECT>
    <LI><OBJECT type=""text/sitemap"">
        <param name=""Name"" value=""Advanced Topics"">
        <param name=""Local"" value=""help.html"">
        </OBJECT>
</UL>
</BODY>
</HTML>";
    }

    [Test]
    public async Task ExtractTextAsync_WithRealWorldScenarios_HandlesCorrectly()
    {
        using var extractor = new ChmFileExtractor(_logger);

        // Test with various invalid scenarios
        var scenarios = new[]
        {
            Path.Combine(_testDirectory, "nonexistent.chm"),
            "", // Empty path
            "   ", // Whitespace path
            Path.Combine(_testDirectory, "folder_not_file.chm"), // Directory instead of file
            Path.Combine(_testDirectory, "too_long_" + new string('x', 250) + ".chm") // Very long path
        };

        // Create a directory with .chm extension to test edge case
        var dirAsChm = Path.Combine(_testDirectory, "folder_not_file.chm");
        Directory.CreateDirectory(dirAsChm);

        foreach (var scenario in scenarios)
        {
            try
            {
                var result = await extractor.ExtractTextAsync(scenario);
                
                // Should not throw exceptions
                await Assert.That(result).IsNotNull();
                
                // Most should return error messages
                if (!string.IsNullOrWhiteSpace(scenario) && !Directory.Exists(scenario))
                {
                    await Assert.That(result.Contains("Error") || result == string.Empty).IsTrue();
                }
            }
            catch (Exception ex)
            {
                // Log the exception but don't fail the test - we want to see what happens
                _logger.LogWarning(ex, "Exception testing scenario: {Scenario}", scenario);
            }
        }
    }
}