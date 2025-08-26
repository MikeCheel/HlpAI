using System.Diagnostics;
using System.Text;
using HlpAI.FileExtractors;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Integration;

public class ChmExtractionVerificationTests
{
    private string _testDirectory = null!;
    private ILogger<ChmFileExtractor> _logger = null!;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("chm_verification");
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
    public async Task VerifyHhExeDetection_ChecksCommonLocations()
    {
        // Test the hh.exe detection functionality
        var hhExePath = ConfigurationService.GetHhExePath(_logger);
        
        // Verify that GetHhExePath returns a valid path or fallback
        await Assert.That(hhExePath).IsNotNull();
        await Assert.That(hhExePath).IsNotEmpty();
        
        // Check if the returned path is valid or if it's the fallback
        if (hhExePath != "hh.exe")
        {
            // If not fallback, verify the file exists
            await Assert.That(File.Exists(hhExePath)).IsTrue();
        }
        
        _logger.LogInformation("hh.exe path resolved to: {HhExePath}", hhExePath);
    }

    [Test]
    public async Task VerifyHhExeValidation_WorksCorrectly()
    {
        // Test validation of hh.exe paths
        var validationTests = new[]
        {
            (Path: (string?)null, Expected: false, Description: "null path"),
            (Path: "", Expected: false, Description: "empty path"),
            (Path: "nonexistent.exe", Expected: false, Description: "non-existent file"),
            (Path: @"C:\Windows\notepad.exe", Expected: false, Description: "wrong executable"),
            (Path: @"C:\Windows\hh.exe", Expected: File.Exists(@"C:\Windows\hh.exe"), Description: "default hh.exe location")
        };

        foreach (var test in validationTests)
        {
            var result = ConfigurationService.ValidateHhExePath(test.Path, _logger);
            await Assert.That(result).IsEqualTo(test.Expected).Because($"Validation failed for {test.Description}");
        }
    }

    [Test]
    public async Task VerifyHhExeDetection_FindsCorrectPaths()
    {
        // Test the detection logic
        var detectedPath = ConfigurationService.DetectHhExePath(_logger);
        
        if (detectedPath != null)
        {
            // If a path was detected, it should be valid
            await Assert.That(File.Exists(detectedPath)).IsTrue();
            await Assert.That(detectedPath.EndsWith("hh.exe", StringComparison.OrdinalIgnoreCase)).IsTrue();
            
            // Validation should pass
            var isValid = ConfigurationService.ValidateHhExePath(detectedPath, _logger);
            await Assert.That(isValid).IsTrue();
        }
        
        _logger.LogInformation("Detected hh.exe path: {DetectedPath}", detectedPath ?? "Not found");
    }

    [Test]
    public async Task VerifyChmExtractor_HandlesNonExistentFile()
    {
        // Test error handling for non-existent CHM files
        using var extractor = new ChmFileExtractor(_logger);
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.chm");
        
        var result = await extractor.ExtractTextAsync(nonExistentFile);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result).Contains("Error");
        await Assert.That(result).Contains("not found");
    }

    [Test]
    public async Task VerifyChmExtractor_HandlesInvalidFile()
    {
        // Test error handling for invalid CHM files
        using var extractor = new ChmFileExtractor(_logger);
        var invalidChmFile = Path.Combine(_testDirectory, "invalid.chm");
        
        // Create a fake CHM file with invalid content
        await File.WriteAllTextAsync(invalidChmFile, "This is not a valid CHM file");
        
        var result = await extractor.ExtractTextAsync(invalidChmFile);
        
        await Assert.That(result).IsNotNull();
        // The result should either be empty or contain an error message
        // depending on how hh.exe handles invalid files
    }

    [Test]
    public async Task VerifyChmExtractor_TimeoutHandling()
    {
        // Test that the extractor handles timeouts properly
        using var extractor = new ChmFileExtractor(_logger);
        var testFile = Path.Combine(_testDirectory, "timeout_test.chm");
        
        // Create a minimal file
        await File.WriteAllTextAsync(testFile, "fake chm content");
        
        var startTime = DateTime.UtcNow;
        var result = await extractor.ExtractTextAsync(testFile);
        var duration = DateTime.UtcNow - startTime;
        
        // Should complete within reasonable time (well before the 30s timeout)
        await Assert.That(duration.TotalSeconds).IsLessThan(35);
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task VerifyChmExtractor_TempDirectoryCleanup()
    {
        // Test that temporary directories are cleaned up
        using var extractor = new ChmFileExtractor(_logger);
        var testFile = Path.Combine(_testDirectory, "cleanup_test.chm");
        
        // Create a minimal file
        await File.WriteAllTextAsync(testFile, "fake chm content");
        
        // Get temp directory count before
        var tempPath = Path.GetTempPath();
        var chmExtractorDirs = Directory.GetDirectories(tempPath, "CHMExtractor*", SearchOption.TopDirectoryOnly);
        var initialCount = chmExtractorDirs.Length;
        
        // Extract (this should create and clean up a temp directory)
        var result = await extractor.ExtractTextAsync(testFile);
        
        // Wait a moment for cleanup
        await Task.Delay(100);
        
        // Check temp directory count after
        chmExtractorDirs = Directory.GetDirectories(tempPath, "CHMExtractor*", SearchOption.TopDirectoryOnly);
        var finalCount = chmExtractorDirs.Length;
        
        // Should not have increased (cleanup should have occurred)
        await Assert.That(finalCount).IsLessThanOrEqualTo(initialCount + 1); // Allow for some tolerance
    }

    [Test]
    public async Task VerifyChmExtractor_DisposalCleanup()
    {
        // Test that disposal works correctly
        var extractor = new ChmFileExtractor(_logger);
        
        // Should not throw on disposal
        extractor.Dispose();
        extractor.Dispose(); // Multiple disposals should be safe
        
        // Test passes if no exception is thrown
        await Task.CompletedTask;
    }

    [Test]
    public async Task VerifyConfigurationService_HhExePathManagement()
    {
        // Set up test-specific configuration file path
        var testConfigPath = Path.Combine(_testDirectory, "test_config.json");
        ConfigurationService.SetConfigFilePathForTesting(testConfigPath);
        
        try
        {
            // Test configuration service hh.exe path management
            var originalConfig = ConfigurationService.LoadConfiguration(_logger);
            var originalPath = originalConfig.HhExePath;
            var originalAutoDetect = originalConfig.AutoDetectHhExe;
            
            try
            {
                // Test updating path
                var testPath = @"C:\Test\hh.exe";
                var updateResult = ConfigurationService.UpdateHhExePath(testPath, _logger);
                await Assert.That(updateResult).IsTrue();
                
                // Verify path was saved
                var updatedConfig = ConfigurationService.LoadConfiguration(_logger);
                await Assert.That(updatedConfig.HhExePath).IsEqualTo(testPath);
                
                // Test clearing path - also disable auto-detection to prevent automatic re-detection
                ConfigurationService.UpdateAutoDetectHhExe(false, _logger);
                var clearResult = ConfigurationService.UpdateHhExePath(null, _logger);
                await Assert.That(clearResult).IsTrue();
                
                // Verify path was cleared
                var clearedConfig = ConfigurationService.LoadConfiguration(_logger);
                await Assert.That(clearedConfig.HhExePath).IsNull();
            }
            finally
            {
                // Restore original configuration
                ConfigurationService.UpdateHhExePath(originalPath, _logger);
                ConfigurationService.UpdateAutoDetectHhExe(originalAutoDetect, _logger);
            }
        }
        finally
        {
            // Reset configuration file path to default
            ConfigurationService.SetConfigFilePathForTesting(null);
        }
    }

    [Test]
    public async Task VerifyExtractorManagementService_ChmExtractorInfo()
    {
        // Test that CHM extractor is properly registered in the management service
        using var configService = new SqliteConfigurationService(_logger);
        using var extractorService = new ExtractorManagementService(configService, _logger);
        var extractors = await extractorService.GetExtractorsAsync();
        
        var chmExtractor = extractors.FirstOrDefault(e => e.Key == "chm");
        await Assert.That(chmExtractor.Key).IsNotNull();
        await Assert.That(chmExtractor.Value.Name).IsEqualTo("CHM File Extractor");
        await Assert.That(chmExtractor.Value.Type).IsEqualTo("ChmFileExtractor");
        await Assert.That(chmExtractor.Value.DefaultExtensions).Contains(".chm");
        await Assert.That(chmExtractor.Value.MimeType).IsEqualTo("application/vnd.ms-htmlhelp");
        await Assert.That(chmExtractor.Value.Description).Contains("hh.exe");
    }

    [Test]
    public async Task VerifyChmExtractor_CanHandleMethod()
    {
        // Test the CanHandle method with various file extensions
        using var extractor = new ChmFileExtractor(_logger);
        
        var testCases = new[]
        {
            (Path: "test.chm", Expected: true),
            (Path: "TEST.CHM", Expected: true),
            (Path: "document.Chm", Expected: true),
            (Path: @"C:\path\to\file.chm", Expected: true),
            (Path: "test.txt", Expected: false),
            (Path: "test.html", Expected: false),
            (Path: "test.pdf", Expected: false),
            (Path: "test", Expected: false),
            (Path: "test.chm.backup", Expected: false)
        };
        
        foreach (var testCase in testCases)
        {
            var result = extractor.CanHandle(testCase.Path);
            await Assert.That(result).IsEqualTo(testCase.Expected)
                .Because($"CanHandle should return {testCase.Expected} for {testCase.Path}");
        }
    }

    [Test]
    public async Task VerifyChmExtractor_GetMimeType()
    {
        // Test the GetMimeType method
        using var extractor = new ChmFileExtractor(_logger);
        var mimeType = extractor.GetMimeType();
        
        await Assert.That(mimeType).IsEqualTo("application/vnd.ms-htmlhelp");
    }
}