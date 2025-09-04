using System.Text.Json;
using HlpAI.Models;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using HlpAI.MCP;
using HlpAI.Utilities;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Integration;

/// <summary>
/// Integration tests for MCP Server Mode functionality
/// Tests the complete MCP server workflow including initialization, document operations, and AI provider integrations
/// </summary>
public class McpServerModeIntegrationTests
{
    private string _testDirectory = null!;
    private string _testDbPath = null!;
    private ILogger<EnhancedMcpRagServer> _logger = null!;
    private SqliteConfigurationService _configService = null!;
    private AppConfiguration _testConfig = null!;
    private string _originalUserProfile = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("mcp_server_integration");
        _testDbPath = Path.Combine(_testDirectory, "test_config.db");
        _logger = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<EnhancedMcpRagServer>();
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        // Set up test-specific SQLite database
        _configService = new SqliteConfigurationService(_testDbPath, _logger);
        
        // Create test configuration for MCP server mode
        _testConfig = new AppConfiguration
        {
            LastDirectory = _testDirectory,
            LastModel = "test-model",
            LastOperationMode = OperationMode.MCP,
            RememberLastDirectory = true,
            MaxFileAuditSizeBytes = 1024 * 1024, // 1MB for testing
            MaxFilesPerCategoryDisplayed = 10
        };
        
        await _configService.SaveAppConfigurationAsync(_testConfig);
        
        // Create test documents for MCP server operations
        await CreateTestDocuments();
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

    private async Task CreateTestDocuments()
    {
        var docsDir = Path.Combine(_testDirectory, "McpTestDocuments");
        Directory.CreateDirectory(docsDir);
        
        // Create various test files for MCP operations
        await File.WriteAllTextAsync(Path.Combine(docsDir, "mcp_doc1.txt"), "This document contains information about MCP Model Context Protocol and its implementation.");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "mcp_doc2.md"), "# MCP Server Documentation\n\nThis document describes server operations and client interactions.");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "config.json"), JsonSerializer.Serialize(new { server = "mcp", port = 8080, enabled = true }));
        
        // Create files with different extensions for testing
        await File.WriteAllTextAsync(Path.Combine(docsDir, "data.csv"), "name,value,type\ntest1,100,server\ntest2,200,client");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "readme.rst"), "MCP Server\n==========\n\nRestructured text document for testing.");
        
        // Create a subdirectory with nested files
        var subDir = Path.Combine(docsDir, "protocols");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "mcp_protocol.txt"), "Detailed MCP protocol specifications and implementation guidelines.");
    }

    [Test]
    public async Task McpServerMode_ServerInitialization_WorksCorrectly()
    {
        // Test MCP server initialization
        var docsDir = Path.Combine(_testDirectory, "McpTestDocuments");
        
        // Initialize server in MCP mode
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.MCP);
        
        // Test initialization
        await server.InitializeAsync();
        
        // Verify server is configured for MCP mode
        await Assert.That(server._operationMode).IsEqualTo(OperationMode.MCP);
        await Assert.That(server.RootPath).IsEqualTo(docsDir);
    }

    [Test]
    public async Task McpServerMode_DocumentDiscovery_WorksCorrectly()
    {
        // Test document discovery functionality in MCP server mode
        var docsDir = Path.Combine(_testDirectory, "McpTestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.MCP);
        await server.InitializeAsync();
        
        // Test document discovery (core MCP server functionality)
        var listRequest = new McpRequest
        {
            Id = "test-list-files",
            Method = "resources/list",
            Params = new { }
        };
        var listResponse = await server.HandleRequestAsync(listRequest);
        await Assert.That(listResponse.Error).IsNull();
        var files = listResponse.Result as ResourcesListResponse;
        
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Resources.Count).IsGreaterThan(0);
        
        // Verify MCP server can discover various file types
        var fileNames = files!.Resources.Select(f => Path.GetFileName(f.Uri)).ToList();
        await Assert.That(fileNames).Contains("mcp_doc1.txt");
        await Assert.That(fileNames).Contains("mcp_doc2.md");
        await Assert.That(fileNames).Contains("config.json");
        await Assert.That(fileNames).Contains("data.csv");
        await Assert.That(fileNames).Contains("readme.rst");
    }

    [Test]
    public async Task McpServerMode_DocumentRetrieval_WorksCorrectly()
    {
        // Test document retrieval functionality in MCP server mode
        var docsDir = Path.Combine(_testDirectory, "McpTestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.MCP);
        await server.InitializeAsync();
        
        var testFile = Path.Combine(docsDir, "mcp_doc1.txt");
        
        // Test document retrieval (core MCP server operation)
        var readRequest = new McpRequest
        {
            Method = "resources/read",
            Params = new ReadResourceRequest { Uri = testFile }
        };
        var readResponse = await server.HandleRequestAsync(readRequest);
        await Assert.That(readResponse.Error).IsNull();
        var content = (readResponse.Result as ReadResourceResponse)?.Contents?.FirstOrDefault()?.Text;
        
        await Assert.That(content).IsNotNull();
        await Assert.That(content).Contains("Model Context Protocol");
        await Assert.That(content).Contains("implementation");
    }

    [Test]
    public async Task McpServerMode_DocumentSearch_WorksCorrectly()
    {
        // Test document search functionality in MCP server mode
        var docsDir = Path.Combine(_testDirectory, "McpTestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.MCP);
        await server.InitializeAsync();
        
        // Test document search (MCP server search capability)
        var searchRequest = new McpRequest
        {
            Id = "test-search-files",
            Method = "tools/call",
            Params = new
            {
                name = "search_files",
                arguments = new { query = "MCP" }
            }
        };
        var searchResponse = await server.HandleRequestAsync(searchRequest);
        await Assert.That(searchResponse.Error).IsNull();
        var textContent = searchResponse.Result as TextContentResponse;
            await Assert.That(textContent).IsNotNull();
            var searchResults = textContent!.Content;
        
        await Assert.That(searchResults).IsNotNull();
        await Assert.That(searchResults.Count).IsGreaterThan(0);
        
        // Verify search finds MCP-related documents (check text content)
        var searchText = string.Join(" ", searchResults.Select(r => r.Text));
        

        
        await Assert.That(searchText).Contains("- mcp_doc1.txt");
        await Assert.That(searchText).Contains("- mcp_doc2.md");
    }

    [Test]
    public async Task McpServerMode_ClientProtocolOperations_WorkCorrectly()
    {
        // Test MCP client protocol operations
        var docsDir = Path.Combine(_testDirectory, "McpTestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.MCP);
        await server.InitializeAsync();
        
        // Test operations that would be called by MCP clients
        
        // 1. List available documents (client request)
        var listRequest = new McpRequest
        {
            Id = "test-list-available-files",
            Method = "resources/list",
            Params = new { }
        };
        var listResponse = await server.HandleRequestAsync(listRequest);
        await Assert.That(listResponse.Error).IsNull();
        var availableFiles = listResponse.Result as ResourcesListResponse;
        await Assert.That(availableFiles).IsNotNull();
        await Assert.That(availableFiles!.Resources.Count).IsGreaterThan(0);
        
        // 2. Retrieve specific document (client request)
        var firstFile = availableFiles!.Resources.First().Uri;
        var readRequest = new McpRequest
        {
            Method = "resources/read",
            Params = new ReadResourceRequest { Uri = firstFile }
        };
        var readResponse = await server.HandleRequestAsync(readRequest);
        await Assert.That(readResponse.Error).IsNull();
        var documentContent = (readResponse.Result as ReadResourceResponse)?.Contents?.FirstOrDefault()?.Text;
        await Assert.That(documentContent).IsNotNull();
        
        // 3. Search documents (client request)
        var searchRequest2 = new McpRequest
        {
            Id = "test-search-server",
            Method = "tools/call",
            Params = new
            {
                name = "search_files",
                arguments = new { query = "server" }
            }
        };
        var searchResponse2 = await server.HandleRequestAsync(searchRequest2);
        await Assert.That(searchResponse2.Error).IsNull();
        var searchResults = searchResponse2.Result;
        await Assert.That(searchResults).IsNotNull();
        
        // All operations should complete successfully for MCP clients
    }

    [Test]
    public async Task McpServerMode_ConcurrentOperations_WorkCorrectly()
    {
        // Test concurrent operations as would happen with multiple MCP clients
        var docsDir = Path.Combine(_testDirectory, "McpTestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.MCP);
        await server.InitializeAsync();
        
        // Simulate concurrent client requests
        var tasks = new List<Task>
        {
            Task.Run(async () => {
                var req = new McpRequest { Id = "concurrent-list-1", Method = "resources/list", Params = new { } };
                return await server.HandleRequestAsync(req);
            }),
            Task.Run(async () => {
                var req = new McpRequest { Id = "concurrent-search-1", Method = "tools/call", Params = new { name = "search_files", arguments = new { query = "test" } } };
                return await server.HandleRequestAsync(req);
            }),
            Task.Run(async () => {
                var req = new McpRequest { Id = "concurrent-list-2", Method = "resources/list", Params = new { } };
                return await server.HandleRequestAsync(req);
            }),
            Task.Run(async () => {
                var req = new McpRequest { Id = "concurrent-search-2", Method = "tools/call", Params = new { name = "search_files", arguments = new { query = "MCP" } } };
                return await server.HandleRequestAsync(req);
            })
        };
        
        // All concurrent operations should complete successfully
        await Task.WhenAll(tasks);
        
        // Verify server is still functional after concurrent operations
        var finalListRequest = new McpRequest
        {
            Id = "test-final-list",
            Method = "resources/list",
            Params = new { }
        };
        var finalListResponse = await server.HandleRequestAsync(finalListRequest);
        await Assert.That(finalListResponse.Error).IsNull();
        var finalFiles = finalListResponse.Result as ResourcesListResponse;
        await Assert.That(finalFiles).IsNotNull();
        await Assert.That(finalFiles!.Resources.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task McpServerMode_ErrorHandling_WorksCorrectly()
    {
        // Test error handling in MCP server mode
        var docsDir = Path.Combine(_testDirectory, "McpTestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.MCP);
        await server.InitializeAsync();
        
        // Test handling of invalid file requests (client error scenarios)
        var nonExistentFile = Path.Combine(docsDir, "nonexistent.txt");
        
        var readRequest = new McpRequest
        {
            Method = "resources/read",
            Params = new ReadResourceRequest { Uri = nonExistentFile }
        };
        var readResponse = await server.HandleRequestAsync(readRequest);
        await Assert.That(readResponse.Error).IsNotNull();
        
        // Server should remain functional after error
        var listRequest = new McpRequest
        {
            Id = "test-list-after-error",
            Method = "resources/list",
            Params = new { }
        };
        var listResponse = await server.HandleRequestAsync(listRequest);
        await Assert.That(listResponse.Error).IsNull();
        var files = listResponse.Result as ResourcesListResponse;
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Resources.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task McpServerMode_LargeDocumentHandling_WorksCorrectly()
    {
        // Test handling of large documents in MCP server mode
        var docsDir = Path.Combine(_testDirectory, "McpTestDocuments");
        
        // Create a larger test document
        var largeContent = string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"Line {i}: This is test content for MCP server large document handling."));
        var largeFile = Path.Combine(docsDir, "large_document.txt");
        await File.WriteAllTextAsync(largeFile, largeContent);
        
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.MCP);
        await server.InitializeAsync();
        
        // Test that MCP server can handle large documents
        var listRequest = new McpRequest
        {
            Id = "test-list-large-docs",
            Method = "resources/list",
            Params = new { }
        };
        var listResponse = await server.HandleRequestAsync(listRequest);
        await Assert.That(listResponse.Error).IsNull();
        var files = listResponse.Result as ResourcesListResponse;
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Resources.Any(f => Path.GetFileName(f.Uri) == "large_document.txt")).IsTrue();
        
        var readRequest = new McpRequest
        {
            Method = "resources/read",
            Params = new ReadResourceRequest { Uri = largeFile }
        };
        var readResponse = await server.HandleRequestAsync(readRequest);
        await Assert.That(readResponse.Error).IsNull();
        var content = (readResponse.Result as ReadResourceResponse)?.Contents?.FirstOrDefault()?.Text;
        await Assert.That(content).IsNotNull();
        await Assert.That(content!.Length).IsGreaterThan(10000); // Should be substantial content
        await Assert.That(content!).Contains("Line 1:");
        await Assert.That(content!).Contains("Line 1000:");
    }

    [Test]
    public async Task McpServerMode_FileTypeSupport_WorksCorrectly()
    {
        // Test MCP server support for various file types
        var docsDir = Path.Combine(_testDirectory, "McpTestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.MCP);
        await server.InitializeAsync();
        
        var listRequest = new McpRequest
        {
            Id = "test-list-file-types",
            Method = "resources/list",
            Params = new { }
        };
        var listResponse = await server.HandleRequestAsync(listRequest);
        await Assert.That(listResponse.Error).IsNull();
        var files = listResponse.Result as ResourcesListResponse;
        await Assert.That(files).IsNotNull();
        var fileExtensions = files!.Resources.Select(f => Path.GetExtension(f.Uri).ToLower()).Distinct().ToList();
        
        // Verify MCP server supports multiple file types
        await Assert.That(fileExtensions).Contains(".txt");
        await Assert.That(fileExtensions).Contains(".md");
        await Assert.That(fileExtensions).Contains(".json");
        await Assert.That(fileExtensions).Contains(".csv");
        await Assert.That(fileExtensions).Contains(".rst");
        
        // Test reading different file types
        var jsonFile = files.Resources.First(f => f.Uri.EndsWith(".json")).Uri;
        var jsonReadRequest = new McpRequest
        {
            Method = "resources/read",
            Params = new ReadResourceRequest { Uri = jsonFile }
        };
        var jsonReadResponse = await server.HandleRequestAsync(jsonReadRequest);
        await Assert.That(jsonReadResponse.Error).IsNull();
        var jsonContent = (jsonReadResponse.Result as ReadResourceResponse)?.Contents?.FirstOrDefault()?.Text;
        await Assert.That(jsonContent).Contains("server");
        await Assert.That(jsonContent).Contains("mcp");
        
        var csvFile = files.Resources.First(f => f.Uri.EndsWith(".csv")).Uri;
        var csvReadRequest = new McpRequest
        {
            Method = "resources/read",
            Params = new ReadResourceRequest { Uri = csvFile }
        };
        var csvReadResponse = await server.HandleRequestAsync(csvReadRequest);
        await Assert.That(csvReadResponse.Error).IsNull();
        var csvContent = (csvReadResponse.Result as ReadResourceResponse)?.Contents?.FirstOrDefault()?.Text;
        await Assert.That(csvContent).Contains("name,value,type");
    }

    [Test]
    public async Task McpServerMode_NestedDirectorySupport_WorksCorrectly()
    {
        // Test MCP server support for nested directories
        var docsDir = Path.Combine(_testDirectory, "McpTestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.MCP);
        await server.InitializeAsync();
        
        // Test that server can discover files in nested directories
        var listRequest = new McpRequest
        {
            Id = "test-list-nested",
            Method = "resources/list",
            Params = new { }
        };
        var listResponse = await server.HandleRequestAsync(listRequest);
        await Assert.That(listResponse.Error).IsNull();
        var files = listResponse.Result as ResourcesListResponse;
        await Assert.That(files).IsNotNull();
        var nestedFile = files!.Resources.FirstOrDefault(f => f.Uri.Contains("protocols") && f.Uri.Contains("mcp_protocol.txt"))?.Uri;
        
        await Assert.That(nestedFile).IsNotNull();
        
        // Test reading nested file
        var readRequest = new McpRequest
        {
            Method = "resources/read",
            Params = new ReadResourceRequest { Uri = nestedFile! }
        };
        var readResponse = await server.HandleRequestAsync(readRequest);
        await Assert.That(readResponse.Error).IsNull();
        var nestedContent = (readResponse.Result as ReadResourceResponse)?.Contents?.FirstOrDefault()?.Text;
        await Assert.That(nestedContent).IsNotNull();
        await Assert.That(nestedContent).Contains("MCP protocol specifications");
    }

    [Test]
    public async Task McpServerMode_ConfigurationIntegration_WorksCorrectly()
    {
        // Test MCP server integration with configuration system
        var docsDir = Path.Combine(_testDirectory, "McpTestDocuments");
        
        // Test with different operation modes
        var mcpConfig = new AppConfiguration
        {
            LastDirectory = docsDir,
            LastModel = "mcp-test-model",
            LastOperationMode = OperationMode.MCP,
            MaxFilesPerCategoryDisplayed = 5
        };
        
        using var server = new EnhancedMcpRagServer(_logger, docsDir, mcpConfig, "mcp-test-model", OperationMode.MCP);
        await server.InitializeAsync();
        
        // Verify configuration is applied
        await Assert.That(server._operationMode).IsEqualTo(OperationMode.MCP);
        
        // Test that server respects configuration limits
        var listRequest = new McpRequest
        {
            Id = "test-list-config",
            Method = "resources/list",
            Params = new { }
        };
        var listResponse = await server.HandleRequestAsync(listRequest);
        await Assert.That(listResponse.Error).IsNull();
        var files = listResponse.Result as ResourcesListResponse;
        await Assert.That(files).IsNotNull();
        
        // Server should function correctly with configuration
        var searchRequest = new McpRequest
        {
            Id = "test-search-config",
            Method = "tools/call",
            Params = new
            {
                name = "search_files",
                arguments = new { query = "test" }
            }
        };
        var searchResponse = await server.HandleRequestAsync(searchRequest);
        await Assert.That(searchResponse.Error).IsNull();
        var searchResults = searchResponse.Result;
        await Assert.That(searchResults).IsNotNull();
    }

    [Test]
    public async Task McpServerMode_ResourceManagement_WorksCorrectly()
    {
        // Test resource management in MCP server mode
        var docsDir = Path.Combine(_testDirectory, "McpTestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.MCP);
        await server.InitializeAsync();
        
        // Perform multiple operations to test resource management
        for (int i = 0; i < 10; i++)
        {
            var listRequest = new McpRequest
            {
                Id = $"test-list-resource-{i}",
                Method = "resources/list",
                Params = new { }
            };
            var listResponse = await server.HandleRequestAsync(listRequest);
            await Assert.That(listResponse.Error).IsNull();
            var files = listResponse.Result as ResourcesListResponse;
            await Assert.That(files).IsNotNull();
            await Assert.That(files!.Resources.Count).IsGreaterThan(0);
            
            if (files!.Resources.Any())
            {
                var readRequest = new McpRequest
                {
                    Method = "resources/read",
                    Params = new ReadResourceRequest { Uri = files.Resources.First().Uri }
                };
                var readResponse = await server.HandleRequestAsync(readRequest);
                await Assert.That(readResponse.Error).IsNull();
                var content = (readResponse.Result as ReadResourceResponse)?.Contents?.FirstOrDefault()?.Text;
                await Assert.That(content).IsNotNull();
            }
        }
        
        // Server should still be functional after multiple operations
        var finalListRequest = new McpRequest
        {
            Id = "test-final-resource-list",
            Method = "resources/list",
            Params = new { }
        };
        var finalListResponse = await server.HandleRequestAsync(finalListRequest);
        await Assert.That(finalListResponse.Error).IsNull();
        var finalFiles = finalListResponse.Result as ResourcesListResponse;
        await Assert.That(finalFiles).IsNotNull();
        await Assert.That(finalFiles!.Resources.Count).IsGreaterThan(0);
        
        // Test proper disposal - using statement will handle disposal automatically
        // Create a separate server instance to test disposal behavior
        var testServer = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.MCP);
        await testServer.InitializeAsync();
        testServer.Dispose();
        
        // After disposal, server should not be usable
        var exception = await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            var disposedRequest = new McpRequest
            {
                Id = "test-disposed",
                Method = "resources/list",
                Params = new { }
            };
            await testServer.HandleRequestAsync(disposedRequest);
        });
        
        await Assert.That(exception).IsNotNull();
    }
}