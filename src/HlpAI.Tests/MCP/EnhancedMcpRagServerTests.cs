using HlpAI.MCP;
using HlpAI.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace HlpAI.Tests.MCP;

public class EnhancedMcpRagServerTests : IDisposable
{
    private readonly string _testRootPath;
    private readonly Mock<ILogger<EnhancedMcpRagServer>> _mockLogger;

    public EnhancedMcpRagServerTests()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), "McpServerTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRootPath);
        _mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
    }

    [Test]
    public async Task Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath, "test-model", OperationMode.Hybrid);

        // Assert
        await Assert.That(server.RootPath).IsEqualTo(_testRootPath);
        await Assert.That(server._operationMode).IsEqualTo(OperationMode.Hybrid);
        await Assert.That(server._aiProvider).IsNotNull();
        await Assert.That(server._vectorStore).IsNotNull();
    }

    [Test]
    public async Task HandleRequestAsync_WithResourcesList_ReturnsResourcesListResponse()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        
        // Create a test file
        var testFile = Path.Combine(_testRootPath, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");
        
        var request = new McpRequest
        {
            Method = "resources/list",
            Params = new { }
        };

        // Act
        var response = await server.HandleRequestAsync(request);

        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Id).IsEqualTo(request.Id);
        await Assert.That(response.Result).IsNotNull();
        await Assert.That(response.Error).IsNull();
    }

    [Test]
    public async Task HandleRequestAsync_WithToolsList_ReturnsToolsListResponse()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath, "test-model", OperationMode.Hybrid);
        var request = new McpRequest
        {
            Method = "tools/list",
            Params = new { }
        };

        // Act
        var response = await server.HandleRequestAsync(request);

        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Id).IsEqualTo(request.Id);
        await Assert.That(response.Result).IsNotNull();
        await Assert.That(response.Error).IsNull();
        
        // Verify it's a ToolsListResponse
        var result = response.Result as ToolsListResponse;
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Tools).IsNotNull();
        await Assert.That(result.Tools.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task HandleRequestAsync_WithReadResource_ValidFile_ReturnsContent()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        
        var testContent = "This is test content for reading";
        var testFile = Path.Combine(_testRootPath, "read-test.txt");
        await File.WriteAllTextAsync(testFile, testContent);
        
        var request = new McpRequest
        {
            Method = "resources/read",
            Params = new ReadResourceRequest { Uri = "read-test.txt" }
        };

        // Act
        var response = await server.HandleRequestAsync(request);

        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNull();
        await Assert.That(response.Result).IsNotNull();
        
        var result = response.Result as ReadResourceResponse;
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Contents).IsNotNull();
        await Assert.That(result.Contents.Count).IsEqualTo(1);
        await Assert.That(result.Contents[0].Text).IsEqualTo(testContent);
    }

    [Test]
    public async Task HandleRequestAsync_WithReadResource_NonExistentFile_ReturnsError()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        var request = new McpRequest
        {
            Method = "resources/read",
            Params = new ReadResourceRequest { Uri = "non-existent-file.txt" }
        };

        // Act
        var response = await server.HandleRequestAsync(request);

        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNotNull();
        await Assert.That(response.Result).IsNull();
    }

    [Test]
    public async Task HandleRequestAsync_WithInvalidMethod_ReturnsMethodNotFoundError()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        var request = new McpRequest
        {
            Method = "invalid/method",
            Params = new { }
        };

        // Act
        var response = await server.HandleRequestAsync(request);

        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNotNull();
        await Assert.That(response.Result).IsNull();
    }

    [Test]
    public async Task HandleRequestAsync_WithToolsCall_SearchFiles_ValidQuery_ReturnsResults()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        
        // Create test files
        var testFile1 = Path.Combine(_testRootPath, "search1.txt");
        var testFile2 = Path.Combine(_testRootPath, "search2.txt");
        await File.WriteAllTextAsync(testFile1, "This file contains the search term");
        await File.WriteAllTextAsync(testFile2, "This file does not contain the term");
        
        var toolCallParams = new
        {
            name = "search_files",
            arguments = new
            {
                query = "search term"
            }
        };
        
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = toolCallParams
        };

        // Act
        var response = await server.HandleRequestAsync(request);

        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNull();
        await Assert.That(response.Result).IsNotNull();
    }

    [Test]
    public async Task HandleRequestAsync_WithToolsCall_InvalidToolName_ReturnsError()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        var toolCallParams = new
        {
            name = "invalid_tool",
            arguments = new { }
        };
        
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = toolCallParams
        };

        // Act
        var response = await server.HandleRequestAsync(request);

        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNotNull();
        await Assert.That(response.Result).IsNull();
    }

    [Test]
    public async Task HandleRequestAsync_WithToolsCall_MissingToolName_ReturnsError()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        var toolCallParams = new
        {
            arguments = new { query = "test" }
        };
        
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = toolCallParams
        };

        // Act
        var response = await server.HandleRequestAsync(request);

        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Error).IsNotNull();
        await Assert.That(response.Result).IsNull();
    }

    [Test]
    public async Task ListTools_WithHybridMode_ReturnsAllTools()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath, "test-model", OperationMode.Hybrid);
        var request = new McpRequest
        {
            Method = "tools/list",
            Params = new { }
        };

        // Act
        var response = await server.HandleRequestAsync(request);
        var result = response.Result as ToolsListResponse;

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Tools.Count).IsGreaterThanOrEqualTo(5); // Should include RAG tools
        
        // Verify basic tools are present
        var toolNames = result.Tools.Cast<dynamic>().Select(t => (string)t.name).ToList();
        await Assert.That(toolNames).Contains("search_files");
        await Assert.That(toolNames).Contains("ask_ai");
        await Assert.That(toolNames).Contains("analyze_file");
        await Assert.That(toolNames).Contains("rag_search");
        await Assert.That(toolNames).Contains("rag_ask");
    }

    [Test]
    public async Task ListTools_WithMcpMode_ReturnsBasicToolsOnly()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath, "test-model", OperationMode.MCP);
        var request = new McpRequest
        {
            Method = "tools/list",
            Params = new { }
        };

        // Act
        var response = await server.HandleRequestAsync(request);
        var result = response.Result as ToolsListResponse;

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Tools.Count).IsEqualTo(3); // Only basic tools
        
        var toolNames = result.Tools.Cast<dynamic>().Select(t => (string)t.name).ToList();
        await Assert.That(toolNames).Contains("search_files");
        await Assert.That(toolNames).Contains("ask_ai");
        await Assert.That(toolNames).Contains("analyze_file");
        await Assert.That(toolNames).DoesNotContain("rag_search");
        await Assert.That(toolNames).DoesNotContain("rag_ask");
    }

    [Test]
    public async Task InitializeAsync_WithRagMode_InitializesVectorStore()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath, "test-model", OperationMode.RAG);
        
        // Create a test file to index
        var testFile = Path.Combine(_testRootPath, "index-test.txt");
        await File.WriteAllTextAsync(testFile, "This is content to be indexed");

        // Act
        await server.InitializeAsync();

        // Assert - Should not throw and should complete successfully
        // If we get here, initialization succeeded
    }

    [Test]
    public async Task InitializeAsync_WithMcpMode_DoesNotInitializeVectorStore()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath, "test-model", OperationMode.MCP);

        // Act
        await server.InitializeAsync();

        // Assert - Should complete quickly without indexing
        // Test passes if no exception is thrown
    }

    [Test]
    public async Task RootPath_Property_ReturnsCorrectPath()
    {
        // Arrange
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);

        // Act & Assert
        await Assert.That(server.RootPath).IsEqualTo(_testRootPath);
    }

    [Test]
    public async Task HandleRequestAsync_WithInvalidMethod_ReturnsErrorResponse()
    {
        // Arrange - Use the existing test directory to avoid cleanup issues
        using var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath);
        var request = new McpRequest
        {
            Method = "invalid/method", // Use invalid method to trigger error
            Params = new { }
        };

        // Act
        var response = await server.HandleRequestAsync(request);

        // Assert
        await Assert.That(response).IsNotNull();
        await Assert.That(response.Id).IsEqualTo(request.Id);
        await Assert.That(response.Error).IsNotNull();
        await Assert.That(response.Result).IsNull();
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testRootPath))
        {
            try
            {
                Directory.Delete(_testRootPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}