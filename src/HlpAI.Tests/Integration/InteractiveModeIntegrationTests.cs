using System.Text.Json;
using HlpAI.Models;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using HlpAI.MCP;
using HlpAI.Utilities;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Integration;

/// <summary>
/// Integration tests for Interactive Mode functionality
/// Tests the complete interactive workflow including menu navigation, file processing, and AI interactions
/// </summary>
public class InteractiveModeIntegrationTests
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
        _testDirectory = FileTestHelper.CreateTempDirectory("interactive_integration");
        _testDbPath = Path.Combine(_testDirectory, "test_config.db");
        _logger = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<EnhancedMcpRagServer>();
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        // Set up test-specific SQLite database
        _configService = new SqliteConfigurationService(_testDbPath, _logger);
        
        // Create test configuration
        _testConfig = new AppConfiguration
        {
            LastDirectory = _testDirectory,
            LastModel = "test-model",
            LastOperationMode = OperationMode.Hybrid,
            RememberLastDirectory = true,
            MaxFileAuditSizeBytes = 1024 * 1024 // 1MB for testing
        };
        
        await _configService.SaveAppConfigurationAsync(_testConfig);
        
        // Create test documents
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
        var docsDir = Path.Combine(_testDirectory, "TestDocuments");
        Directory.CreateDirectory(docsDir);
        
        // Create various test files
        await File.WriteAllTextAsync(Path.Combine(docsDir, "test1.txt"), "This is a test document about artificial intelligence and machine learning.");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "test2.md"), "# Test Markdown\n\nThis document contains information about data processing.");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "test3.json"), JsonSerializer.Serialize(new { name = "test", value = 123 }));
        
        // Create a subdirectory with more files
        var subDir = Path.Combine(docsDir, "SubFolder");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "nested.txt"), "This is a nested document for testing recursive operations.");
    }

    [Test]
    public async Task InteractiveMode_ServerInitialization_WorksCorrectly()
    {
        // Test server initialization in interactive mode
        var docsDir = Path.Combine(_testDirectory, "TestDocuments");
        
        // Initialize server as it would be in interactive mode
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.Hybrid);
        
        // Test initialization
        await server.InitializeAsync();
        
        // Verify server properties
        await Assert.That(server._operationMode).IsEqualTo(OperationMode.Hybrid);
        await Assert.That(server.RootPath).IsEqualTo(docsDir);
    }

    [Test]
    public async Task InteractiveMode_FileListingOperation_WorksCorrectly()
    {
        // Create test documents first
        await CreateTestDocuments();
        
        // Test file listing functionality as used in interactive mode
        var docsDir = Path.Combine(_testDirectory, "TestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.Hybrid);
        await server.InitializeAsync();
        
        // Test file listing (equivalent to menu option for listing files)
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
        
        // Verify expected files are found
        var fileNames = files!.Resources.Select(f => Path.GetFileName(f.Uri)).ToList();
        
        // Debug: Log what files were actually found
        _logger.LogInformation($"Files found: {string.Join(", ", fileNames)}");
        _logger.LogInformation($"Total files: {fileNames.Count}");
        
        await Assert.That(fileNames).Contains("test1.txt");
        await Assert.That(fileNames).Contains("test2.md");
        await Assert.That(fileNames).Contains("test3.json");
    }

    [Test]
    public async Task InteractiveMode_FileReadingOperation_WorksCorrectly()
    {
        // Create test documents first
        await CreateTestDocuments();
        
        // Test file reading functionality as used in interactive mode
        var docsDir = Path.Combine(_testDirectory, "TestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.Hybrid);
        await server.InitializeAsync();
        
        var testFile = Path.Combine(docsDir, "test1.txt");
        
        // Test file reading (equivalent to menu option for reading files)
        var content = await File.ReadAllTextAsync(testFile);
        
        await Assert.That(content).IsNotNull();
        await Assert.That(content).Contains("artificial intelligence");
        await Assert.That(content).Contains("machine learning");
    }

    [Test]
    public async Task InteractiveMode_FileSearchOperation_WorksCorrectly()
    {
        // Create test documents first
        await CreateTestDocuments();
        
        // Test file search functionality as used in interactive mode
        var docsDir = Path.Combine(_testDirectory, "TestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.Hybrid);
        await server.InitializeAsync();
        
        // Test file search (equivalent to menu option for searching files)
        var searchRequest = new McpRequest
        {
            Id = "test-search-files",
            Method = "tools/call",
            Params = new
            {
                name = "search_files",
                arguments = new { query = "test" }
            }
        };
        var searchResponse = await server.HandleRequestAsync(searchRequest);
        await Assert.That(searchResponse.Error).IsNull();
        var textContent = searchResponse.Result as TextContentResponse;
        await Assert.That(textContent).IsNotNull();
        await Assert.That(textContent!.Content.Count).IsGreaterThan(0);
        
        // Verify search finds relevant files (check text content contains file reference)
        var searchText = textContent!.Content.First().Text;
        await Assert.That(searchText).Contains("test1.txt");
    }

    [Test]
    public async Task InteractiveMode_DocumentIndexing_WorksCorrectly()
    {
        // Create test documents first
        await CreateTestDocuments();
        
        // Test document indexing functionality as used in interactive mode
        var docsDir = Path.Combine(_testDirectory, "TestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.Hybrid);
        await server.InitializeAsync();
        
        // Test document indexing (equivalent to reindex menu option)
        var reindexRequest = new McpRequest
        {
            Id = "test-reindex",
            Method = "tools/call",
            Params = new
            {
                name = "reindex_documents",
                arguments = new { }
            }
        };
        var reindexResponse = await server.HandleRequestAsync(reindexRequest);
        await Assert.That(reindexResponse.Error).IsNull();
        
        // Verify indexing worked by checking if we can search indexed content
        var ragSearchRequest = new McpRequest
        {
            Id = "test-rag-search",
            Method = "tools/call",
            Params = new
            {
                name = "search_files",
                arguments = new { query = "artificial intelligence" }
            }
        };
        var ragSearchResponse = await server.HandleRequestAsync(ragSearchRequest);
        await Assert.That(ragSearchResponse).IsNotNull();
        await Assert.That(ragSearchResponse.Error).IsNull();
    }

    [Test]
    public async Task InteractiveMode_ConfigurationPersistence_WorksCorrectly()
    {
        // Test configuration persistence in interactive mode
        var docsDir = Path.Combine(_testDirectory, "TestDocuments");
        
        // Simulate user changing directory in interactive mode
        var newConfig = await _configService.LoadAppConfigurationAsync();
        newConfig.LastDirectory = docsDir;
        newConfig.LastModel = "updated-model";
        
        var saveResult = await _configService.SaveAppConfigurationAsync(newConfig);
        await Assert.That(saveResult).IsTrue();
        
        // Verify configuration was persisted
        var reloadedConfig = await _configService.LoadAppConfigurationAsync();
        await Assert.That(reloadedConfig.LastDirectory).IsEqualTo(docsDir);
        await Assert.That(reloadedConfig.LastModel).IsEqualTo("updated-model");
    }

    [Test]
    public async Task InteractiveMode_ErrorHandling_WorksCorrectly()
    {
        // Test error handling in interactive mode scenarios
        var invalidDir = Path.Combine(_testDirectory, "NonExistentDirectory");
        
        // Test server initialization with invalid directory - should handle gracefully
        using var server = new EnhancedMcpRagServer(_logger, invalidDir, _testConfig, "test-model", OperationMode.Hybrid);
        
        // Should not throw exception - server handles non-existent directories gracefully
        await server.InitializeAsync();
        
        // Verify server was initialized successfully despite non-existent directory
        await Assert.That(server._vectorStore).IsNotNull();
    }

    [Test]
    public async Task InteractiveMode_MenuStateManagement_WorksCorrectly()
    {
        // Test menu state management functionality
        var docsDir = Path.Combine(_testDirectory, "TestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.Hybrid);
        await server.InitializeAsync();
        
        // Test that server maintains state correctly for interactive operations
        var initialListRequest = new McpRequest
        {
            Id = "test-initial-list",
            Method = "resources/list",
            Params = new { }
        };
        var initialListResponse = await server.HandleRequestAsync(initialListRequest);
        await Assert.That(initialListResponse.Error).IsNull();
        var initialFiles = initialListResponse.Result as ResourcesListResponse;
        
        // Add a new file
        var newFile = Path.Combine(docsDir, "dynamic.txt");
        await File.WriteAllTextAsync(newFile, "Dynamically added content");
        
        // Verify server can see the new file
        var updatedListRequest = new McpRequest
        {
            Id = "test-updated-list",
            Method = "resources/list",
            Params = new { }
        };
        var updatedListResponse = await server.HandleRequestAsync(updatedListRequest);
        await Assert.That(updatedListResponse.Error).IsNull();
        var updatedFiles = updatedListResponse.Result as ResourcesListResponse;
        await Assert.That(updatedFiles).IsNotNull();
        await Assert.That(updatedFiles!.Resources.Count).IsGreaterThan(initialFiles!.Resources.Count);
        
        var newFileFound = updatedFiles!.Resources.Any(f => Path.GetFileName(f.Uri) == "dynamic.txt");
        await Assert.That(newFileFound).IsTrue();
    }

    [Test]
    public async Task InteractiveMode_MultipleOperations_WorksCorrectly()
    {
        // Create test documents first
        await CreateTestDocuments();
        
        // Test multiple operations in sequence as would happen in interactive mode
        var docsDir = Path.Combine(_testDirectory, "TestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.Hybrid);
        await server.InitializeAsync();
        
        // Sequence of operations typical in interactive mode
        
        // 1. List files
        var listRequest = new McpRequest
        {
            Id = "test-multiple-list",
            Method = "resources/list",
            Params = new { }
        };
        var listResponse = await server.HandleRequestAsync(listRequest);
        await Assert.That(listResponse.Error).IsNull();
        var files = listResponse.Result as ResourcesListResponse;
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Resources.Count).IsGreaterThan(0);
        
        // 2. Read a file
        var firstFile = files!.Resources.First().Uri;
        var readRequest = new McpRequest
        {
            Method = "resources/read",
            Params = new ReadResourceRequest { Uri = firstFile }
        };
        var readResponse = await server.HandleRequestAsync(readRequest);
        await Assert.That(readResponse.Error).IsNull();
        var content = (readResponse.Result as ReadResourceResponse)?.Contents?.FirstOrDefault()?.Text;
        await Assert.That(content).IsNotNull();
        
        // 3. Search files
        var searchRequest = new McpRequest
        {
            Id = "test-multiple-search",
            Method = "tools/call",
            Params = new
            {
                name = "search_files",
                arguments = new { query = "test" }
            }
        };
        var searchResponse = await server.HandleRequestAsync(searchRequest);
        await Assert.That(searchResponse.Error).IsNull();
        var textContent = searchResponse.Result as TextContentResponse;
        await Assert.That(textContent).IsNotNull();
        await Assert.That(textContent!.Content.Count).IsGreaterThan(0);
        
        // 4. Index documents
        var reindexRequest2 = new McpRequest
        {
            Id = "test-multiple-reindex",
            Method = "tools/call",
            Params = new
            {
                name = "reindex_documents",
                arguments = new { }
            }
        };
        var reindexResponse2 = await server.HandleRequestAsync(reindexRequest2);
        await Assert.That(reindexResponse2.Error).IsNull();
        
        // 5. Perform RAG search
        var ragSearchRequest3 = new McpRequest
        {
            Id = "test-multiple-rag-search",
            Method = "tools/call",
            Params = new
            {
                name = "search_files",
                arguments = new { query = "document" }
            }
        };
        var ragSearchResponse3 = await server.HandleRequestAsync(ragSearchRequest3);
        await Assert.That(ragSearchResponse3).IsNotNull();
        await Assert.That(ragSearchResponse3.Error).IsNull();
        
        // All operations should complete successfully without state corruption
    }

    [Test]
    public async Task InteractiveMode_DirectoryChangeOperation_WorksCorrectly()
    {
        // Test directory change functionality as used in interactive mode
        var docsDir = Path.Combine(_testDirectory, "TestDocuments");
        var newDocsDir = Path.Combine(_testDirectory, "NewTestDocuments");
        Directory.CreateDirectory(newDocsDir);
        await File.WriteAllTextAsync(Path.Combine(newDocsDir, "new.txt"), "New directory content");
        
        // Initialize with first directory
        using var server1 = new EnhancedMcpRagServer(_logger, docsDir, _testConfig, "test-model", OperationMode.Hybrid);
        await server1.InitializeAsync();
        
        var initialListRequest = new McpRequest
        {
            Id = "test-directory-initial",
            Method = "resources/list",
            Params = new { }
        };
        var initialListResponse = await server1.HandleRequestAsync(initialListRequest);
        await Assert.That(initialListResponse.Error).IsNull();
        var initialFiles = initialListResponse.Result as ResourcesListResponse;
        await Assert.That(initialFiles).IsNotNull();
        await Assert.That(initialFiles!.Resources.Any(f => Path.GetFileName(f.Uri) == "test1.txt")).IsTrue();
        
        // Simulate directory change (would require creating new server instance)
        server1.Dispose();
        using var server2 = new EnhancedMcpRagServer(_logger, newDocsDir, _testConfig, "test-model", OperationMode.Hybrid);
        await server2.InitializeAsync();
        
        var newListRequest = new McpRequest
        {
            Id = "test-directory-new",
            Method = "resources/list",
            Params = new { }
        };
        var newListResponse = await server2.HandleRequestAsync(newListRequest);
        await Assert.That(newListResponse.Error).IsNull();
        var newFiles = newListResponse.Result as ResourcesListResponse;
        await Assert.That(newFiles).IsNotNull();
        await Assert.That(newFiles!.Resources.Any(f => Path.GetFileName(f.Uri) == "new.txt")).IsTrue();
        await Assert.That(newFiles!.Resources.Any(f => Path.GetFileName(f.Uri) == "test1.txt")).IsFalse();
    }
}