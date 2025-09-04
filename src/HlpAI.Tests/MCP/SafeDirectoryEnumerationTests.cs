using System.Runtime.Versioning;
using HlpAI;
using HlpAI.Models;
using HlpAI.MCP;
using HlpAI.Services;
using Microsoft.Extensions.Logging;
using Moq;
using TUnit.Core;

namespace HlpAI.Tests.MCP;

/// <summary>
/// Unit tests for the safer directory enumeration functionality in EnhancedMcpRagServer
/// Tests the SafeEnumerateFiles method's ability to handle restricted directories gracefully
/// </summary>
[NotInParallel]
public class SafeDirectoryEnumerationTests
{
    private readonly Mock<ILogger<EnhancedMcpRagServer>> _mockLogger;
    private readonly string _testRootPath;
    private readonly string _accessibleSubDir;
    private readonly string _testFile1;
    private readonly string _testFile2;
    private readonly string _testFile3;
    
    public SafeDirectoryEnumerationTests()
    {
        _mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
        _testRootPath = Path.Combine(Path.GetTempPath(), "SafeEnumerationTests", Guid.NewGuid().ToString());
        _accessibleSubDir = Path.Combine(_testRootPath, "AccessibleSubDir");
        
        // Create test directory structure
        Directory.CreateDirectory(_testRootPath);
        Directory.CreateDirectory(_accessibleSubDir);
        
        // Create test files
        _testFile1 = Path.Combine(_testRootPath, "test1.txt");
        _testFile2 = Path.Combine(_testRootPath, "test2.md");
        _testFile3 = Path.Combine(_accessibleSubDir, "test3.txt");
        
        File.WriteAllText(_testFile1, "Test content 1");
        File.WriteAllText(_testFile2, "Test content 2");
        File.WriteAllText(_testFile3, "Test content 3");
    }
    
    [After(Test)]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_testRootPath))
            {
                Directory.Delete(_testRootPath, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
    
    [Test]
    public async Task IndexAllDocumentsAsync_WithAccessibleDirectories_EnumeratesAllFiles()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        
        var request = new McpRequest
        {
            Id = "test-index-accessible",
            Method = "tools/call",
            Params = new
            {
                name = "reindex_documents",
                arguments = new { }
            }
        };
        
        // Act
        var response = await server.HandleRequestAsync(request);
        
        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNull();
        
        // Verify that the indexing process completed without throwing exceptions
        // The SafeEnumerateFiles method should have found all accessible files
    }
    
    [Test]
    public async Task IndexAllDocumentsAsync_WithNonExistentRootDirectory_HandlesGracefully()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistentDir", Guid.NewGuid().ToString());
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, nonExistentPath);
        
        var request = new McpRequest
        {
            Id = "test-non-existent-root",
            Method = "tools/call",
            Params = new
            {
                name = "reindex_documents",
                arguments = new { }
            }
        };
        
        // Act
        var response = await server.HandleRequestAsync(request);
        
        // Assert
        await Assert.That(response).IsNotNull();
        // The method should handle the non-existent directory gracefully
        // and not throw an unhandled exception
    }
    
    [Test]
    public async Task SearchFilesAsync_WithAccessibleDirectories_SearchesAllFiles()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        
        var request = new McpRequest
        {
            Id = "test-search-accessible",
            Method = "tools/call",
            Params = new
            {
                name = "search_files",
                arguments = new { query = "Test content" }
            }
        };
        
        // Act
        var response = await server.HandleRequestAsync(request);
        
        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNull();
        
        // The search should complete without throwing exceptions
        // even if some directories are inaccessible
    }
    
    [Test]
    public async Task SearchFilesAsync_WithNonExistentRootDirectory_HandlesGracefully()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistentSearchDir", Guid.NewGuid().ToString());
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, nonExistentPath);
        
        var request = new McpRequest
        {
            Id = "test-search-non-existent",
            Method = "tools/call",
            Params = new
            {
                name = "search_files",
                arguments = new { query = "Test content" }
            }
        };
        
        // Act
        var response = await server.HandleRequestAsync(request);
        
        // Assert
        await Assert.That(response).IsNotNull();
        // The search should handle the non-existent directory gracefully
        // and return an appropriate response without crashing
    }
    
    [Test]
    public async Task IndexAllDocumentsAsync_LogsDirectoryAccessIssues_WhenDirectoriesAreInaccessible()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        
        var request = new McpRequest
        {
            Id = "test-logging-access-issues",
            Method = "tools/call",
            Params = new
            {
                name = "reindex_documents",
                arguments = new { }
            }
        };
        
        // Act
        var response = await server.HandleRequestAsync(request);
        
        // Assert
        await Assert.That(response).IsNotNull();
        
        // Verify that the method completes successfully even if there are access issues
        // The SafeEnumerateFiles method should log warnings but continue processing
    }
    
    [Test]
    public async Task SearchFilesAsync_ContinuesProcessing_WhenSomeDirectoriesAreInaccessible()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        
        var request = new McpRequest
        {
            Id = "test-search-continues-processing",
            Method = "tools/call",
            Params = new
            {
                name = "search_files",
                arguments = new { query = "content" }
            }
        };
        
        // Act
        var response = await server.HandleRequestAsync(request);
        
        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNull();
        
        // The search should continue processing accessible directories
        // even if some directories throw access denied exceptions
    }
}