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
/// Comprehensive unit tests for EnhancedMcpRagServer error handling functionality
/// Tests various error scenarios including UnauthorizedAccessException, DirectoryNotFoundException,
/// and other exception handling patterns in the server
/// </summary>
[NotInParallel]
public class EnhancedMcpRagServerErrorHandlingTests
{
    private readonly Mock<ILogger<EnhancedMcpRagServer>> _mockLogger;
    private readonly string _testRootPath;
    private readonly string _restrictedPath;
    private readonly string _nonExistentPath;
    
    public EnhancedMcpRagServerErrorHandlingTests()
    {
        _mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
        _testRootPath = Path.Combine(Path.GetTempPath(), "EnhancedMcpRagServerErrorTests", Guid.NewGuid().ToString());
        _restrictedPath = Path.Combine(Path.GetTempPath(), "RestrictedErrorTests", Guid.NewGuid().ToString());
        _nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistentErrorTests", Guid.NewGuid().ToString());
        
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
    public async Task InitializeAsync_WithNonExistentDirectory_HandlesDirectoryNotFoundGracefully()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _nonExistentPath, "test-model", OperationMode.RAG);
        
        // Act & Assert - Should not throw any exceptions
        await server.InitializeAsync();
        
        // Verify that the server completed initialization despite the non-existent directory
        await Assert.That(server._vectorStore).IsNotNull();
        
        // Verify that appropriate logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing RAG vector store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    
    [Test]
    public async Task HandleRequestAsync_WithNullParams_ReturnsErrorResponse()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        
        // Create a request with null parameters
        var request = new McpRequest
        {
            Id = "test-null-params",
            Method = "resources/read",
            Params = null // This gets handled gracefully without logging
        };
        
        // Act
        var response = await server.HandleRequestAsync(request);
        
        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Id).IsEqualTo(request.Id);
        await Assert.That(response.Error).IsNotNull();
        await Assert.That(response.Result).IsNull();
        
        // Verify the error message indicates invalid parameters
        var errorResponse = (ErrorResponse)response.Error!;
        await Assert.That(errorResponse.Message).Contains("Invalid request parameters");
    }
    
    [Test]
    public async Task HandleRequestAsync_ReadResource_WithInvalidParameters_ReturnsError()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        var request = new McpRequest
        {
            Id = "test-invalid-params",
            Method = "resources/read",
            Params = new { invalidParam = "test" } // Missing required 'uri' parameter
        };
        
        // Act
        var response = await server.HandleRequestAsync(request);
        
        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNotNull();
        var errorResponse = (ErrorResponse)response.Error!;
        await Assert.That(errorResponse.Message).Contains("ialization for type 'HlpAI.MCP.ReadResourceRequest");
        await Assert.That(response.Result).IsNull();
    }
    
    [Test]
    public async Task HandleRequestAsync_ReadResource_WithNonExistentFile_ReturnsFileNotFoundError()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        var request = new McpRequest
        {
            Id = "test-file-not-found",
            Method = "resources/read",
            Params = new ReadResourceRequest { Uri = "non-existent-file.txt" }
        };
        
        // Act
        var response = await server.HandleRequestAsync(request);
        
        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNotNull();
        var errorResponse = (ErrorResponse)response.Error!;
        await Assert.That(errorResponse.Message).Contains("File not found");
        await Assert.That(response.Result).IsNull();
    }
    
    [Test]
    public async Task HandleRequestAsync_ReadResource_WithUnsupportedFileType_ReturnsUnsupportedError()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        
        // Create a file with unsupported extension
        var unsupportedFile = Path.Combine(_testRootPath, "test.unsupported");
        await File.WriteAllTextAsync(unsupportedFile, "test content");
        
        var request = new McpRequest
        {
            Id = "test-unsupported-file",
            Method = "resources/read",
            Params = new ReadResourceRequest { Uri = "test.unsupported" }
        };
        
        // Act
        var response = await server.HandleRequestAsync(request);
        
        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNotNull();
        var errorResponse = (ErrorResponse)response.Error!;
        await Assert.That(errorResponse.Message).Contains("Unsupported file type");
        await Assert.That(response.Result).IsNull();
    }
    
    [Test]
    public async Task HandleRequestAsync_ToolsCall_WithMissingArguments_ReturnsError()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        var request = new McpRequest
        {
            Id = "test-missing-arguments",
            Method = "tools/call",
            Params = new { name = "search_files" } // Missing 'arguments' property
        };
        
        // Act
        var response = await server.HandleRequestAsync(request);
        
        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNotNull();
        var errorResponse = (ErrorResponse)response.Error!;
        await Assert.That(errorResponse.Message).Contains("Missing arguments");
        await Assert.That(response.Result).IsNull();
    }
    
    [Test]
    public async Task HandleRequestAsync_ToolsCall_SearchFiles_WithMissingQuery_ReturnsError()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        var request = new McpRequest
        {
            Id = "test-missing-query",
            Method = "tools/call",
            Params = new
            {
                name = "search_files",
                arguments = new { } // Missing required 'query' parameter
            }
        };
        
        // Act
        var response = await server.HandleRequestAsync(request);
        
        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNotNull();
        var errorResponse = (ErrorResponse)response.Error!;
        await Assert.That(errorResponse.Message).Contains("Query is required");
        await Assert.That(response.Result).IsNull();
    }
    
    [Test]
    public async Task HandleRequestAsync_ToolsCall_SearchFiles_WithEmptyQuery_ReturnsError()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        var request = new McpRequest
        {
            Id = "test-empty-query",
            Method = "tools/call",
            Params = new
            {
                name = "search_files",
                arguments = new { query = "" } // Empty query
            }
        };
        
        // Act
        var response = await server.HandleRequestAsync(request);
        
        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNotNull();
        var errorResponse = (ErrorResponse)response.Error!;
        await Assert.That(errorResponse.Message).Contains("Query is required");
        await Assert.That(response.Result).IsNull();
    }
    
    [Test]
    public async Task HandleRequestAsync_ReindexDocuments_InMcpOnlyMode_ReturnsError()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath, "test-model", OperationMode.MCP);
        var request = new McpRequest
        {
            Id = "test-reindex-mcp-only",
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
        await Assert.That(response.Error).IsNotNull();
        var errorResponse = (ErrorResponse)response.Error!;
        await Assert.That(errorResponse.Message).Contains("Document indexing is not available in MCP-only mode");
        await Assert.That(response.Result).IsNull();
    }
    
    [Test]
    public async Task UpdateAiProvider_WithNullProvider_ThrowsArgumentNullException()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => server.UpdateAiProvider(null!));
        await Assert.That(exception.ParamName).IsEqualTo("newProvider");
    }
    
    [Test]
    public async Task InitializeAsync_WithMcpOnlyMode_SkipsVectorStoreInitialization()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath, "test-model", OperationMode.MCP);
        
        // Act
        await server.InitializeAsync();
        
        // Assert - In MCP-only mode, vector store initialization should be skipped
        // No logging about RAG initialization should occur
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing RAG vector store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
    
    [Test]
    public async Task HandleRequestAsync_WithInvalidJsonInParams_HandlesGracefully()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        
        // Create a request with malformed parameters that will cause JSON deserialization issues
        var request = new McpRequest
        {
            Id = "test-invalid-json",
            Method = "tools/call",
            Params = "invalid-json-string" // This gets handled gracefully without logging
        };
        
        // Act
        var response = await server.HandleRequestAsync(request);
        
        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Id).IsEqualTo(request.Id);
        await Assert.That(response.Error).IsNotNull();
        await Assert.That(response.Result).IsNull();
        
        // Verify the error message indicates tool call processing error
        var errorResponse = (ErrorResponse)response.Error!;
        await Assert.That(errorResponse.Message).Contains("Error processing tool call");
    }
    
    [Test]
    public void Dispose_CalledMultipleTimes_HandlesGracefully()
    {
        // Arrange
        var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        
        // Act - Call dispose multiple times
        server.Dispose();
        server.Dispose();
        server.Dispose();
        
        // Assert - Should not throw any exceptions
        // If we reach here, multiple dispose calls were handled gracefully
        // Test passes if no exception is thrown
    }
}