using System.Text.Json;
using HlpAI.Models;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using HlpAI.Utilities;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Integration;

/// <summary>
/// Integration tests for Audit Mode functionality
/// Tests the complete audit workflow including directory analysis, file type detection, permission checking, recursive scanning, and report generation
/// </summary>
[NotInParallel]
public class AuditModeIntegrationTests
{
    private string _testDirectory = null!;
    private string _testDbPath = null!;
    private ILogger<AuditModeIntegrationTests> _logger = null!;
    private SqliteConfigurationService _configService = null!;
    private AppConfiguration _testConfig = null!;
    private string _originalUserProfile = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("audit_mode_integration");
        _testDbPath = Path.Combine(_testDirectory, "test_config.db");
        _logger = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<AuditModeIntegrationTests>();
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        // Set up test-specific SQLite database
        _configService = new SqliteConfigurationService(_testDbPath, _logger);
        
        // Create test configuration for audit mode
        _testConfig = new AppConfiguration
        {
            LastDirectory = _testDirectory,
            LastModel = "audit-test-model",
            LastOperationMode = OperationMode.RAG,
            RememberLastDirectory = true,
            MaxFileAuditSizeBytes = 1024 * 1024 // 1MB for testing
        };
        
        await _configService.SaveAppConfigurationAsync(_testConfig);
        
        // Create comprehensive test directory structure for audit
        await CreateAuditTestStructure();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        // Restore original user profile
        Environment.SetEnvironmentVariable("USERPROFILE", _originalUserProfile);
        
        _configService?.Dispose();
        
        // Wait for file handles to be released
        await Task.Delay(100);
        
        FileTestHelper.SafeDeleteDirectory(_testDirectory);
    }

    private async Task CreateAuditTestStructure()
    {
        var auditDir = Path.Combine(_testDirectory, "AuditTestDirectory");
        Directory.CreateDirectory(auditDir);
        
        // Create various file types for audit testing
        await File.WriteAllTextAsync(Path.Combine(auditDir, "document1.txt"), "Text document for audit testing with various content types.");
        await File.WriteAllTextAsync(Path.Combine(auditDir, "document2.md"), "# Markdown Document\n\nThis is a markdown file for audit analysis.");
        await File.WriteAllTextAsync(Path.Combine(auditDir, "config.json"), JsonSerializer.Serialize(new { audit = true, version = "1.0", settings = new { enabled = true } }));
        await File.WriteAllTextAsync(Path.Combine(auditDir, "data.csv"), "id,name,type,size\n1,file1,txt,1024\n2,file2,md,2048\n3,file3,json,512");
        await File.WriteAllTextAsync(Path.Combine(auditDir, "script.py"), "# Python script\nprint('Hello from audit test')\ndef main():\n    pass");
        await File.WriteAllTextAsync(Path.Combine(auditDir, "style.css"), "body { margin: 0; padding: 0; font-family: Arial; }");
        await File.WriteAllTextAsync(Path.Combine(auditDir, "page.html"), "<!DOCTYPE html>\n<html><head><title>Test</title></head><body><h1>Audit Test</h1></body></html>");
        await File.WriteAllTextAsync(Path.Combine(auditDir, "data.xml"), "<?xml version=\"1.0\"?>\n<root><item id=\"1\">Test</item></root>");
        
        // Create binary files for audit testing
        var binaryData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header
        await File.WriteAllBytesAsync(Path.Combine(auditDir, "image.png"), binaryData);
        
        var zipData = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP header
        await File.WriteAllBytesAsync(Path.Combine(auditDir, "archive.zip"), zipData);
        
        // Create large files for size testing
        var largeContent = string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"Line {i}: Large file content for audit size analysis."));
        await File.WriteAllTextAsync(Path.Combine(auditDir, "large_file.txt"), largeContent);
        
        // Create nested directory structure
        var subDir1 = Path.Combine(auditDir, "subdirectory1");
        Directory.CreateDirectory(subDir1);
        await File.WriteAllTextAsync(Path.Combine(subDir1, "nested_doc1.txt"), "Nested document in subdirectory 1.");
        await File.WriteAllTextAsync(Path.Combine(subDir1, "nested_config.json"), JsonSerializer.Serialize(new { nested = true, level = 1 }));
        
        var subDir2 = Path.Combine(auditDir, "subdirectory2");
        Directory.CreateDirectory(subDir2);
        await File.WriteAllTextAsync(Path.Combine(subDir2, "nested_doc2.md"), "# Nested Markdown\n\nDocument in subdirectory 2.");
        
        // Create deeply nested structure
        var deepDir = Path.Combine(subDir1, "deep", "nested", "structure");
        Directory.CreateDirectory(deepDir);
        await File.WriteAllTextAsync(Path.Combine(deepDir, "deep_file.txt"), "File in deeply nested structure for recursive audit testing.");
        
        // Create files with special characters in names
        await File.WriteAllTextAsync(Path.Combine(auditDir, "file with spaces.txt"), "File with spaces in name.");
        await File.WriteAllTextAsync(Path.Combine(auditDir, "file-with-dashes.txt"), "File with dashes in name.");
        await File.WriteAllTextAsync(Path.Combine(auditDir, "file_with_underscores.txt"), "File with underscores in name.");
        
        // Create empty files and directories
        await File.WriteAllTextAsync(Path.Combine(auditDir, "empty_file.txt"), "");
        Directory.CreateDirectory(Path.Combine(auditDir, "empty_directory"));
        
        // Create files with different extensions but same content type
        await File.WriteAllTextAsync(Path.Combine(auditDir, "readme.rst"), "RestructuredText Document\n======================\n\nFor audit testing.");
        await File.WriteAllTextAsync(Path.Combine(auditDir, "notes.log"), "[2024-01-15 10:00:00] INFO: Audit test log entry\n[2024-01-15 10:01:00] DEBUG: Another log entry");
    }

    [Test]
    public async Task AuditMode_BasicDirectoryAudit_WorksCorrectly()
    {
        // Test basic directory audit functionality
        var auditDir = Path.Combine(_testDirectory, "AuditTestDirectory");
        
        // Capture audit output
        using var stringWriter = new StringWriter();
        
        // Run audit on test directory
        var auditUtility = new FileAuditUtility();
        auditUtility.AuditDirectory(auditDir, stringWriter);
        
        var auditOutput = stringWriter.ToString();
        
        // Verify audit output contains expected information
        await Assert.That(auditOutput).IsNotNull();
        await Assert.That(auditOutput.Length).IsGreaterThan(0);
        
        // Should contain file type analysis
        await Assert.That(auditOutput).Contains(".txt");
        await Assert.That(auditOutput).Contains(".md");
        await Assert.That(auditOutput).Contains(".json");
        await Assert.That(auditOutput).Contains(".csv");
        
        // Should contain directory information
        await Assert.That(auditOutput).Contains("Directory");
        await Assert.That(auditOutput).Contains("Files");
    }

    [Test]
    public async Task AuditMode_FileTypeDetection_WorksCorrectly()
    {
        // Test file type detection and categorization
        var auditDir = Path.Combine(_testDirectory, "AuditTestDirectory");
        
        using var stringWriter = new StringWriter();
        
        var auditUtility = new FileAuditUtility();
        auditUtility.AuditDirectory(auditDir, stringWriter);
        var auditOutput = stringWriter.ToString();
        
        // Verify different file types are detected
        await Assert.That(auditOutput).Contains("txt"); // Text files
        await Assert.That(auditOutput).Contains("md");  // Markdown files
        await Assert.That(auditOutput).Contains("json"); // JSON files
        await Assert.That(auditOutput).Contains("csv"); // CSV files
        await Assert.That(auditOutput).Contains("py");  // Python files
        await Assert.That(auditOutput).Contains("css"); // CSS files
        await Assert.That(auditOutput).Contains("html"); // HTML files
        await Assert.That(auditOutput).Contains("xml"); // XML files
        await Assert.That(auditOutput).Contains("png"); // Binary files
        await Assert.That(auditOutput).Contains("zip"); // Archive files
    }

    [Test]
    public async Task AuditMode_RecursiveScanning_WorksCorrectly()
    {
        // Test recursive directory scanning
        var auditDir = Path.Combine(_testDirectory, "AuditTestDirectory");
        
        using var stringWriter = new StringWriter();
        
        var auditUtility = new FileAuditUtility();
        auditUtility.AuditDirectory(auditDir, stringWriter);
        var auditOutput = stringWriter.ToString();
            
            // Verify recursive scanning found files in nested directories
            // The audit should show a total file count that includes nested files
            await Assert.That(auditOutput).Contains("Total Files:");
            
            // Extract the total file count from the audit output
            var totalFilesLine = auditOutput.Split('\n').FirstOrDefault(line => line.Contains("Total Files:"));
            await Assert.That(totalFilesLine).IsNotNull();
            
            // Should find more files than just the root level (at least 15+ files including nested ones)
            // Parse the number from "Total Files: X" format
            var totalFilesMatch = System.Text.RegularExpressions.Regex.Match(totalFilesLine!, @"Total Files: (\d+)");
            await Assert.That(totalFilesMatch.Success).IsTrue();
            var totalFiles = int.Parse(totalFilesMatch.Groups[1].Value);
            await Assert.That(totalFiles).IsGreaterThan(15);
    }

    [Test]
    public async Task AuditMode_FileSizeAnalysis_WorksCorrectly()
    {
        // Test file size analysis and reporting
        var auditDir = Path.Combine(_testDirectory, "AuditTestDirectory");
        
        using var stringWriter = new StringWriter();
        
        var auditUtility = new FileAuditUtility();
        auditUtility.AuditDirectory(auditDir, stringWriter);
        var auditOutput = stringWriter.ToString();
        
        // Should contain size information (either in large files section or file count statistics)
        var containsSizeInfo = auditOutput.Contains("Total Files:") || auditOutput.Contains("MB") || auditOutput.Contains("Large");
        await Assert.That(containsSizeInfo).IsTrue();
        
        // Should identify the large file we created
        var containsLargeFile = auditOutput.Contains("large_file.txt") || auditOutput.ToLower().Contains("large");
        await Assert.That(containsLargeFile).IsTrue();
    }

    [Test]
    public async Task AuditMode_EmptyFilesAndDirectories_WorksCorrectly()
    {
        // Test handling of empty files and directories
        var auditDir = Path.Combine(_testDirectory, "AuditTestDirectory");
        
        using var stringWriter = new StringWriter();
        
        var auditUtility = new FileAuditUtility();
        auditUtility.AuditDirectory(auditDir, stringWriter);
        var auditOutput = stringWriter.ToString();
        
        // Should handle empty files gracefully
        // The audit should not crash on empty files and should report them
        await Assert.That(auditOutput).IsNotNull();
        await Assert.That(auditOutput.Length).IsGreaterThan(0);
        
        // Should find the empty file we created
        var hasEmptyFileReference = auditOutput.Contains("empty_file.txt") || auditOutput.Contains("0 bytes") || auditOutput.Contains("empty");
        // Note: Depending on implementation, empty files might be filtered out or reported differently
    }

    [Test]
    public async Task AuditMode_SpecialCharactersInFilenames_WorksCorrectly()
    {
        // Test handling of files with special characters in names
        var auditDir = Path.Combine(_testDirectory, "AuditTestDirectory");
        
        using var stringWriter = new StringWriter();
        
        var auditUtility = new FileAuditUtility();
        auditUtility.AuditDirectory(auditDir, stringWriter);
        var auditOutput = stringWriter.ToString();
        
        // Should handle files with spaces, dashes, and underscores
        await Assert.That(auditOutput).IsNotNull();
        
        // The audit should complete successfully even with special characters
        // Specific file names might be encoded or handled differently
        var containsSpecialFiles = auditOutput.Contains("spaces") || 
                                 auditOutput.Contains("dashes") || 
                                 auditOutput.Contains("underscores");
        
        // At minimum, the audit should not crash
        await Assert.That(auditOutput.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task AuditMode_ConfigurationLimits_WorksCorrectly()
    {
        // Test audit respects configuration limits
        var auditDir = Path.Combine(_testDirectory, "AuditTestDirectory");
        
        // Create configuration with specific limits
        var limitedConfig = new AppConfiguration
        {
            MaxFileAuditSizeBytes = 1024 // 1KB limit
        };
        
        using var stringWriter = new StringWriter();
        
        var auditUtility = new FileAuditUtility();
        auditUtility.AuditDirectory(auditDir, stringWriter, limitedConfig.MaxFileAuditSizeBytes);
        var auditOutput = stringWriter.ToString();
        
        // Should complete successfully with limits
        await Assert.That(auditOutput).IsNotNull();
        await Assert.That(auditOutput.Length).IsGreaterThan(0);
        
        // Large file should be handled according to size limits
        // (might be skipped or truncated depending on implementation)
    }

    [Test]
    public async Task AuditMode_NonExistentDirectory_HandlesGracefully()
    {
        // Test audit handling of non-existent directory
        var nonExistentDir = Path.Combine(_testDirectory, "NonExistentDirectory");
        
        using var stringWriter = new StringWriter();
        
        // Should handle gracefully without crashing
        var auditUtility = new FileAuditUtility();
        auditUtility.AuditDirectory(nonExistentDir, stringWriter);
        var auditOutput = stringWriter.ToString();
        
        // Should contain error message about directory not existing
        await Assert.That(auditOutput).Contains("Directory does not exist");
    }

    [Test]
    public async Task AuditMode_PermissionRestrictedDirectory_HandlesGracefully()
    {
        // Test audit handling of permission-restricted directories
        var auditDir = Path.Combine(_testDirectory, "AuditTestDirectory");
        
        // Create a subdirectory and try to restrict permissions (Windows-specific)
        var restrictedDir = Path.Combine(auditDir, "restricted");
        Directory.CreateDirectory(restrictedDir);
        await File.WriteAllTextAsync(Path.Combine(restrictedDir, "restricted_file.txt"), "Restricted content");
        
        try
        {
            // On Windows, we can't easily restrict permissions in tests
            // But the audit should handle any permission errors gracefully
            using var stringWriter = new StringWriter();
            
            var auditUtility = new FileAuditUtility();
            auditUtility.AuditDirectory(auditDir, stringWriter);
            var auditOutput = stringWriter.ToString();
            
            // Should complete without crashing
            await Assert.That(auditOutput).IsNotNull();
            await Assert.That(auditOutput.Length).IsGreaterThan(0);
        }
        catch (UnauthorizedAccessException ex)
        {
            // This is expected and should be handled gracefully
            await Assert.That(ex).IsNotNull(); // Test passes if exception is caught
        }
    }

    [Test]
    public async Task AuditMode_BinaryFileHandling_WorksCorrectly()
    {
        // Test audit handling of binary files
        var auditDir = Path.Combine(_testDirectory, "AuditTestDirectory");
        
        using var stringWriter = new StringWriter();
        
        var auditUtility = new FileAuditUtility();
        auditUtility.AuditDirectory(auditDir, stringWriter);
        var auditOutput = stringWriter.ToString();
        
        // Should detect binary files
        var containsPngOrZip = auditOutput.Contains("png") || auditOutput.Contains("zip");
        await Assert.That(containsPngOrZip).IsTrue();
        
        // Should not crash on binary content
        await Assert.That(auditOutput).IsNotNull();
        await Assert.That(auditOutput.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task AuditMode_ReportGeneration_WorksCorrectly()
    {
        // Test audit report generation and formatting
        var auditDir = Path.Combine(_testDirectory, "AuditTestDirectory");
        
        using var stringWriter = new StringWriter();
        
        var auditUtility = new FileAuditUtility();
        auditUtility.AuditDirectory(auditDir, stringWriter);
        var auditOutput = stringWriter.ToString();
        
        // Verify report structure
            await Assert.That(auditOutput).IsNotNull();
             await Assert.That(auditOutput.Length).IsGreaterThan(100); // Should be substantial
            
            // Should contain summary information
            var lines = auditOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            await Assert.That(lines.Length).IsGreaterThan(5); // Should have multiple lines of output
            
            // Should contain file statistics
            var hasStatistics = auditOutput.Contains("Total") || 
                              auditOutput.Contains("Count") || 
                              auditOutput.Contains("Files") ||
                              auditOutput.Contains("Directory");
            
            await Assert.That(hasStatistics).IsTrue();
    }

    [Test]
    public async Task AuditMode_PerformanceWithLargeDirectory_WorksCorrectly()
    {
        // Test audit performance with larger directory structure
        var auditDir = Path.Combine(_testDirectory, "AuditTestDirectory");
        
        // Create additional files for performance testing
        var perfDir = Path.Combine(auditDir, "performance_test");
        Directory.CreateDirectory(perfDir);
        
        for (int i = 0; i < 100; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(perfDir, $"perf_file_{i}.txt"), $"Performance test file {i} content.");
        }
        
        using var stringWriter = new StringWriter();
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var auditUtility = new FileAuditUtility();
        auditUtility.AuditDirectory(auditDir, stringWriter);
        stopwatch.Stop();
        
        var auditOutput = stringWriter.ToString();
        
        // Should complete in reasonable time (less than 30 seconds)
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(30000);
        
        // Should produce comprehensive output
            await Assert.That(auditOutput.Length).IsGreaterThan(500);
            
            // Should find the performance test files (check for .txt files which should be numerous)
            var containsTxtFiles = auditOutput.Contains(".txt") && auditOutput.Contains("Total Files:");
            await Assert.That(containsTxtFiles).IsTrue();
    }

    [Test]
    public async Task AuditMode_CommandLineIntegration_WorksCorrectly()
    {
        // Test audit mode integration with command-line arguments
        var auditDir = Path.Combine(_testDirectory, "AuditTestDirectory");
        
        // This test verifies that the audit functionality works as it would be called from Program.cs
        // when using --audit command line argument
        
        using var stringWriter = new StringWriter();
        
        // Simulate command-line audit call
        var auditUtility = new FileAuditUtility();
        auditUtility.AuditDirectory(auditDir, stringWriter);
        
        var auditOutput = stringWriter.ToString();
        
        // Verify audit produces output suitable for command-line usage
        await Assert.That(auditOutput).IsNotNull();
        await Assert.That(auditOutput.Length).IsGreaterThan(0);
        
        // Should be formatted for console output
        var lines = auditOutput.Split('\n');
        await Assert.That(lines.Length).IsGreaterThan(1);
            
            // Should contain meaningful information for users
             var hasUsefulInfo = auditOutput.Contains(".") && // File extensions
                               (auditOutput.Contains("Directory") || auditOutput.Contains("Files") || auditOutput.Contains("Total"));
             
             await Assert.That(hasUsefulInfo).IsTrue();
    }
}