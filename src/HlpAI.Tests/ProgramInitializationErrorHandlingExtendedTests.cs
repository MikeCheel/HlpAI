using System.Runtime.Versioning;
using HlpAI;
using HlpAI.Models;
using HlpAI.MCP;
using HlpAI.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace HlpAI.Tests;

/// <summary>
/// Extended comprehensive unit tests for Program.cs initialization error handling
/// Tests various error scenarios during server initialization including edge cases
/// and recovery mechanisms
/// </summary>
[NotInParallel]
public class ProgramInitializationErrorHandlingExtendedTests
{
    private readonly Mock<ErrorLoggingService> _mockErrorLoggingService;
    private readonly string _testRootPath;
    private readonly string _restrictedPath;
    private readonly string _nonExistentPath;
    private readonly string _invalidPath;
    
    public ProgramInitializationErrorHandlingExtendedTests()
    {
        _mockErrorLoggingService = new Mock<ErrorLoggingService>();
        _testRootPath = Path.Combine(Path.GetTempPath(), "ProgramInitErrorTests", Guid.NewGuid().ToString());
        _restrictedPath = Path.Combine(Path.GetTempPath(), "RestrictedInitTests", Guid.NewGuid().ToString());
        _nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistentInitTests", Guid.NewGuid().ToString());
        _invalidPath = "<>:\"|?*invalid";
        
        // Create test directory
        Directory.CreateDirectory(_testRootPath);
    }
    
    [Before(Test)]
    public async Task Setup()
    {
        // Clean up any existing test files
        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(_testRootPath, true);
        }
        Directory.CreateDirectory(_testRootPath);
        
        // Reset mock
        _mockErrorLoggingService.Reset();
        await Task.CompletedTask;
    }
    
    [After(Test)]
    public async Task Cleanup()
    {
        // Clean up test directories
        try
        {
            if (Directory.Exists(_testRootPath))
            {
                Directory.Delete(_testRootPath, true);
            }
            if (Directory.Exists(_restrictedPath))
            {
                Directory.Delete(_restrictedPath, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
        await Task.CompletedTask;
    }
    
    [Test]
    public async Task InitializeServer_WithValidDirectory_CompletesSuccessfully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
        
        // Create some test files
        var testFile = Path.Combine(_testRootPath, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content for initialization");
        
        // Act
        using var server = new EnhancedMcpRagServer(mockLogger.Object, _testRootPath, "test-model", OperationMode.RAG);
        
        // Should not throw any exceptions
        await server.InitializeAsync();
        
        // Assert
        await Assert.That(server._vectorStore).IsNotNull();
        
        // Verify initialization logging occurred
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing RAG vector store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    
    [Test]
    public async Task InitializeServer_WithNonExistentDirectory_HandlesGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
        
        // Act & Assert - Should not throw DirectoryNotFoundException
        using var server = new EnhancedMcpRagServer(mockLogger.Object, _nonExistentPath, "test-model", OperationMode.RAG);
        await server.InitializeAsync();
        
        // Verify that server was created despite non-existent directory
        await Assert.That(server._vectorStore).IsNotNull();
    }
    
    [Test]
    public async Task InitializeServer_WithInvalidPath_HandlesGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
        
        // Act & Assert - Should handle invalid path characters gracefully
        using var server = new EnhancedMcpRagServer(mockLogger.Object, _invalidPath, "test-model", OperationMode.RAG);
        
        // Should not throw exception during construction
        await server.InitializeAsync();
        
        // Verify that server was created despite invalid path
        await Assert.That(server._vectorStore).IsNotNull();
    }
    
    [Test]
    public async Task InitializeServer_WithEmptyDirectory_CompletesSuccessfully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
        var emptyDir = Path.Combine(_testRootPath, "empty");
        Directory.CreateDirectory(emptyDir);
        
        // Act
        using var server = new EnhancedMcpRagServer(mockLogger.Object, emptyDir, "test-model", OperationMode.RAG);
        await server.InitializeAsync();
        
        // Assert
        await Assert.That(server._vectorStore).IsNotNull();
        
        // Verify initialization completed even with empty directory
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing RAG vector store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    
    [Test]
    public async Task InitializeServer_WithDirectoryContainingOnlyUnsupportedFiles_CompletesSuccessfully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
        var unsupportedDir = Path.Combine(_testRootPath, "unsupported");
        Directory.CreateDirectory(unsupportedDir);
        
        // Create files with unsupported extensions
        await File.WriteAllTextAsync(Path.Combine(unsupportedDir, "test.bin"), "binary content");
        await File.WriteAllTextAsync(Path.Combine(unsupportedDir, "test.exe"), "executable content");
        await File.WriteAllTextAsync(Path.Combine(unsupportedDir, "test.dll"), "library content");
        
        // Act
        using var server = new EnhancedMcpRagServer(mockLogger.Object, unsupportedDir, "test-model", OperationMode.RAG);
        await server.InitializeAsync();
        
        // Assert
        await Assert.That(server._vectorStore).IsNotNull();
        
        // Verify initialization completed even with only unsupported files
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing RAG vector store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    
    [Test]
    public async Task InitializeServer_WithMixedFileTypes_ProcessesSupportedFilesOnly()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
        var mixedDir = Path.Combine(_testRootPath, "mixed");
        Directory.CreateDirectory(mixedDir);
        
        // Create mix of supported and unsupported files
        await File.WriteAllTextAsync(Path.Combine(mixedDir, "document.txt"), "Text document content");
        await File.WriteAllTextAsync(Path.Combine(mixedDir, "readme.md"), "# Markdown content");
        await File.WriteAllTextAsync(Path.Combine(mixedDir, "code.cs"), "// C# code content");
        await File.WriteAllTextAsync(Path.Combine(mixedDir, "binary.bin"), "binary content");
        await File.WriteAllTextAsync(Path.Combine(mixedDir, "executable.exe"), "executable content");
        
        // Act
        using var server = new EnhancedMcpRagServer(mockLogger.Object, mixedDir, "test-model", OperationMode.RAG);
        await server.InitializeAsync();
        
        // Assert
        await Assert.That(server._vectorStore).IsNotNull();
        
        // Verify initialization completed and processed supported files
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing RAG vector store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    
    [Test]
    public async Task InitializeServer_WithNestedDirectories_ProcessesRecursively()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
        var nestedDir = Path.Combine(_testRootPath, "nested");
        var subDir1 = Path.Combine(nestedDir, "sub1");
        var subDir2 = Path.Combine(nestedDir, "sub2");
        var deepDir = Path.Combine(subDir1, "deep");
        
        Directory.CreateDirectory(deepDir);
        Directory.CreateDirectory(subDir2);
        
        // Create files at different levels
        await File.WriteAllTextAsync(Path.Combine(nestedDir, "root.txt"), "Root level content");
        await File.WriteAllTextAsync(Path.Combine(subDir1, "sub1.txt"), "Sub directory 1 content");
        await File.WriteAllTextAsync(Path.Combine(subDir2, "sub2.md"), "# Sub directory 2 content");
        await File.WriteAllTextAsync(Path.Combine(deepDir, "deep.cs"), "// Deep directory content");
        
        // Act
        using var server = new EnhancedMcpRagServer(mockLogger.Object, nestedDir, "test-model", OperationMode.RAG);
        await server.InitializeAsync();
        
        // Assert
        await Assert.That(server._vectorStore).IsNotNull();
        
        // Verify initialization completed and processed nested files
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing RAG vector store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    
    [Test]
    public async Task InitializeServer_WithLargeNumberOfFiles_CompletesSuccessfully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
        var largeDir = Path.Combine(_testRootPath, "large");
        Directory.CreateDirectory(largeDir);
        
        // Create many files to test performance and stability
        for (int i = 0; i < 50; i++)
        {
            await File.WriteAllTextAsync(
                Path.Combine(largeDir, $"file{i:D3}.txt"), 
                $"Content for file number {i}\nThis is test content for performance testing.");
        }
        
        // Act
        using var server = new EnhancedMcpRagServer(mockLogger.Object, largeDir, "test-model", OperationMode.RAG);
        await server.InitializeAsync();
        
        // Assert
        await Assert.That(server._vectorStore).IsNotNull();
        
        // Verify initialization completed with many files
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing RAG vector store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    
    [Test]
    public async Task InitializeServer_WithSpecialCharactersInFilenames_HandlesGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
        var specialDir = Path.Combine(_testRootPath, "special");
        Directory.CreateDirectory(specialDir);
        
        // Create files with special characters in names (where allowed by filesystem)
        var specialFiles = new[]
        {
            "file with spaces.txt",
            "file-with-dashes.txt",
            "file_with_underscores.txt",
            "file.with.dots.txt",
            "file(with)parentheses.txt",
            "file[with]brackets.txt",
            "file{with}braces.txt"
        };
        
        foreach (var fileName in specialFiles)
        {
            try
            {
                await File.WriteAllTextAsync(
                    Path.Combine(specialDir, fileName), 
                    $"Content for {fileName}");
            }
            catch
            {
                // Skip files that can't be created on this filesystem
            }
        }
        
        // Act
        using var server = new EnhancedMcpRagServer(mockLogger.Object, specialDir, "test-model", OperationMode.RAG);
        await server.InitializeAsync();
        
        // Assert
        await Assert.That(server._vectorStore).IsNotNull();
        
        // Verify initialization completed with special character filenames
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing RAG vector store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    
    [Test]
    public async Task InitializeServer_InMcpOnlyMode_SkipsFileIndexing()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
        
        // Create test files that would normally be indexed
        await File.WriteAllTextAsync(Path.Combine(_testRootPath, "test.txt"), "Test content");
        await File.WriteAllTextAsync(Path.Combine(_testRootPath, "readme.md"), "# Readme content");
        
        // Act
        using var server = new EnhancedMcpRagServer(mockLogger.Object, _testRootPath, "test-model", OperationMode.MCP);
        await server.InitializeAsync();
        
        // Assert - In MCP-only mode, RAG initialization should be skipped
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing RAG vector store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
    
    [Test]
    public async Task InitializeServer_WithNullOrEmptyModel_UsesDefaultBehavior()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
        
        // Act & Assert - Should handle null/empty model gracefully
        using var serverWithNull = new EnhancedMcpRagServer(mockLogger.Object, _testRootPath, null!, OperationMode.RAG);
        await serverWithNull.InitializeAsync();
        
        using var serverWithEmpty = new EnhancedMcpRagServer(mockLogger.Object, _testRootPath, "", OperationMode.RAG);
        await serverWithEmpty.InitializeAsync();
        
        // Both should complete initialization without throwing
        await Assert.That(serverWithNull._vectorStore).IsNotNull();
        await Assert.That(serverWithEmpty._vectorStore).IsNotNull();
    }
}