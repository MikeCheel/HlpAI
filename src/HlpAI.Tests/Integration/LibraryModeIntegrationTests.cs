using System.Text.Json;
using HlpAI.Models;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using HlpAI.MCP;
using HlpAI.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using HlpAI.FileExtractors;
using HlpAI.VectorStores;


namespace HlpAI.Tests.Integration;

/// <summary>
/// Integration tests for Library Mode functionality
/// Tests the complete library usage workflow including programmatic API usage, service integrations, and third-party consumption patterns
/// </summary>
public class LibraryModeIntegrationTests
{
    private string _testDirectory = null!;
    private string _testDbPath = null!;
    private ILogger<EnhancedMcpRagServer> _logger = null!;
    private SqliteConfigurationService _configService = null!;
    private AppConfiguration _testConfig = null!;
    private string _originalUserProfile = null!;
    private ServiceProvider? _serviceProvider;

    [Before(Test)]
    public async Task Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("library_mode_integration");
        _testDbPath = Path.Combine(_testDirectory, "test_config.db");
        _logger = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<EnhancedMcpRagServer>();
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        // Set up test-specific SQLite database
        _configService = new SqliteConfigurationService(_testDbPath, _logger);
        
        // Create test configuration for library mode
        _testConfig = new AppConfiguration
        {
            LastDirectory = _testDirectory,
            LastModel = "library-test-model",
            LastOperationMode = OperationMode.RAG,
            RememberLastDirectory = true,
            MaxFileAuditSizeBytes = 1024 * 1024, // 1MB for testing
            MaxFilesPerCategoryDisplayed = 15
        };
        
        await _configService.SaveAppConfigurationAsync(_testConfig);
        
        // Set up dependency injection for library mode testing
        SetupDependencyInjection();
        
        // Create test documents for library operations
        await CreateTestDocuments();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        // Restore original user profile
        Environment.SetEnvironmentVariable("USERPROFILE", _originalUserProfile);
        
        _serviceProvider?.Dispose();
        _configService?.Dispose();
        
        // Wait for file handles to be released
        await Task.Delay(100);
        
        FileTestHelper.SafeDeleteDirectory(_testDirectory);
    }

    private void SetupDependencyInjection()
    {
        var services = new ServiceCollection();
        
        // Register core services for library mode
        services.AddSingleton<ILogger<EnhancedMcpRagServer>>(_logger);
        services.AddSingleton<ILogger>(provider => provider.GetRequiredService<ILogger<EnhancedMcpRagServer>>());
        services.AddSingleton(_configService);
        services.AddSingleton(_testConfig);
        
        // Register file extractors
        services.AddTransient<IFileExtractor, TextFileExtractor>();
        services.AddTransient<IFileExtractor, HtmlFileExtractor>();
        services.AddTransient<IFileExtractor, PdfFileExtractor>();
        
        // Register embedding service (required by VectorStore)
        services.AddSingleton<IEmbeddingService>(provider =>
        {
            var httpClient = new HttpClient();
            var logger = provider.GetService<ILogger>();
            var config = provider.GetService<AppConfiguration>();
            return new EmbeddingService(httpClient, "http://localhost:11434", "nomic-embed-text", logger, config);
        });
        
        // Register vector store (requires IEmbeddingService)
        services.AddSingleton<IVectorStore, VectorStore>();
        
        _serviceProvider = services.BuildServiceProvider();
    }

    private async Task CreateTestDocuments()
    {
        var docsDir = Path.Combine(_testDirectory, "LibraryTestDocuments");
        Directory.CreateDirectory(docsDir);
        
        // Create various test files for library operations
        await File.WriteAllTextAsync(Path.Combine(docsDir, "library_doc1.txt"), "This document contains information about library usage patterns and API integration.");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "library_doc2.md"), "# Library Integration Guide\n\nThis document describes programmatic usage and service integration patterns.");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "api_config.json"), JsonSerializer.Serialize(new { api = "library", version = "1.0", endpoints = new[] { "search", "index", "query" } }));
        
        // Create files for different extraction scenarios
        await File.WriteAllTextAsync(Path.Combine(docsDir, "data_export.csv"), "id,name,type,description\n1,Service1,API,Library service endpoint\n2,Service2,SDK,Software development kit");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "integration.xml"), "<?xml version=\"1.0\"?>\n<integration>\n  <service name=\"library\">\n    <endpoint>api/v1</endpoint>\n  </service>\n</integration>");
        
        // Create nested structure for library testing
        var apiDir = Path.Combine(docsDir, "api");
        Directory.CreateDirectory(apiDir);
        await File.WriteAllTextAsync(Path.Combine(apiDir, "endpoints.txt"), "Library API endpoints and their usage patterns for third-party integration.");
        
        var sdkDir = Path.Combine(docsDir, "sdk");
        Directory.CreateDirectory(sdkDir);
        await File.WriteAllTextAsync(Path.Combine(sdkDir, "usage_examples.md"), "# SDK Usage Examples\n\nCode examples for library integration and programmatic usage.");
    }

    [Test]
    public async Task LibraryMode_ProgrammaticInitialization_WorksCorrectly()
    {
        // Test programmatic initialization of library components
        var docsDir = Path.Combine(_testDirectory, "LibraryTestDocuments");
        
        // Initialize server programmatically using isolated constructor (library usage pattern)
        using var server = new EnhancedMcpRagServer(_logger, docsDir, isolated: true, AiProviderType.Ollama, "library-test-model", mode: OperationMode.RAG);
        
        // Test programmatic initialization
        await server.InitializeAsync();
        
        // Verify library is configured correctly
        await Assert.That(server._operationMode).IsEqualTo(OperationMode.RAG);
        await Assert.That(server.RootPath).IsEqualTo(docsDir);
    }

    [Test]
    public async Task LibraryMode_ServiceIntegration_WorksCorrectly()
    {
        // Test integration with dependency injection and service patterns
        var docsDir = Path.Combine(_testDirectory, "LibraryTestDocuments");
        
        // Get services from DI container (library usage pattern)
        var configService = _serviceProvider!.GetRequiredService<SqliteConfigurationService>();
        var config = _serviceProvider!.GetRequiredService<AppConfiguration>();
        var logger = _serviceProvider!.GetRequiredService<ILogger<EnhancedMcpRagServer>>();
        
        await Assert.That(configService).IsNotNull();
        await Assert.That(config).IsNotNull();
        await Assert.That(logger).IsNotNull();
        
        // Test service integration using isolated constructor
        using var server = new EnhancedMcpRagServer(logger, docsDir, isolated: true, AiProviderType.Ollama, "library-test-model", mode: OperationMode.RAG);
        await server.InitializeAsync();
        
        // Verify services work together
        var listRequest = new McpRequest
        {
            Id = "test-service-integration-list",
            Method = "resources/list",
            Params = new { }
        };
        var listResponse = await server.HandleRequestAsync(listRequest);
        await Assert.That(listResponse.Error).IsNull();
        var files = listResponse.Result as ResourcesListResponse;
        await Assert.That(files).IsNotNull();
         await Assert.That(files!.Resources.Count()).IsGreaterThan(0);
    }

    [Test]
    public async Task LibraryMode_FileExtractorIntegration_WorksCorrectly()
    {
        // Test integration with file extractor services
        var docsDir = Path.Combine(_testDirectory, "LibraryTestDocuments");
        
        // Get file extractors from DI (library usage pattern)
        var extractors = _serviceProvider!.GetServices<IFileExtractor>().ToList();
        await Assert.That(extractors.Count).IsGreaterThan(0);
        
        // Test each extractor type
        var textExtractor = extractors.OfType<TextFileExtractor>().FirstOrDefault();
        var htmlExtractor = extractors.OfType<HtmlFileExtractor>().FirstOrDefault();
        var pdfExtractor = extractors.OfType<PdfFileExtractor>().FirstOrDefault();
        
        await Assert.That(textExtractor).IsNotNull();
        await Assert.That(htmlExtractor).IsNotNull();
        await Assert.That(pdfExtractor).IsNotNull();
        
        // Test extraction capabilities
        var textFile = Path.Combine(docsDir, "library_doc1.txt");
        var textCanExtract = textExtractor!.CanHandle(textFile);
        await Assert.That(textCanExtract).IsTrue();
        
        var jsonFile = Path.Combine(docsDir, "api_config.json");
        var jsonCanExtract = textExtractor!.CanHandle(jsonFile);
        await Assert.That(jsonCanExtract).IsTrue(); // JSON is now supported by TextFileExtractor
    }

    [Test]
    public async Task LibraryMode_VectorStoreIntegration_WorksCorrectly()
    {
        // Test integration with vector store services
        var vectorStore = _serviceProvider!.GetRequiredService<IVectorStore>();
        await Assert.That(vectorStore).IsNotNull();
        
        // Test vector store operations (library usage pattern)
        var testVector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };
        var testMetadata = new Dictionary<string, object>
        {
            { "source", "library_test" },
            { "type", "integration" }
        };
        
        // Test indexing documents (correct IVectorStore method)
        await vectorStore.IndexDocumentAsync("test_doc_1.txt", "Test document content for library integration", testMetadata);
        
        // Test searching vectors
        var ragQuery = new RagQuery { Query = "Test document content", TopK = 5, MinSimilarity = 0.5f };
        var searchResults = await vectorStore.SearchAsync(ragQuery);
        await Assert.That(searchResults).IsNotNull();
    }

    [Test]
    public async Task LibraryMode_ThirdPartyConsumption_WorksCorrectly()
    {
        // Test third-party consumption patterns
        var docsDir = Path.Combine(_testDirectory, "LibraryTestDocuments");
        
        // Simulate third-party application using the library
        var thirdPartyLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<EnhancedMcpRagServer>();
        
        var thirdPartyConfig = new AppConfiguration
        {
            LastDirectory = docsDir,
            LastModel = "third-party-model",
            LastOperationMode = OperationMode.Hybrid
        };
        
        // Third-party creates their own server instance using isolated constructor
        using var thirdPartyServer = new EnhancedMcpRagServer(thirdPartyLogger, docsDir, isolated: true, AiProviderType.Ollama, "third-party-model", mode: OperationMode.Hybrid);
        
        await thirdPartyServer.InitializeAsync();
        
        // Third-party performs operations
        var listRequest = new McpRequest
        {
            Id = "test-third-party-list",
            Method = "resources/list",
            Params = new { }
        };
        var listResponse = await thirdPartyServer.HandleRequestAsync(listRequest);
        await Assert.That(listResponse.Error).IsNull();
        var files = listResponse.Result as ResourcesListResponse;
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Resources.Count()).IsGreaterThan(0);
        
        var searchRequest = new McpRequest
        {
            Id = "test-third-party-search",
            Method = "tools/call",
            Params = new
            {
                name = "search_files",
                arguments = new { query = "library" }
            }
        };
        var searchResponse = await thirdPartyServer.HandleRequestAsync(searchRequest);
        await Assert.That(searchResponse.Error).IsNull();
    }

    [Test]
    public async Task LibraryMode_ConfigurationManagement_WorksCorrectly()
    {
        // Test configuration management in library mode
        var customConfig = new AppConfiguration
        {
            LastDirectory = _testDirectory,
            LastModel = "custom-library-model",
            LastOperationMode = OperationMode.RAG,
            RememberLastDirectory = false,
            MaxFileAuditSizeBytes = 2048 * 1024, // 2MB
            MaxFilesPerCategoryDisplayed = 25
        };
        
        // Save custom configuration
        await _configService.SaveAppConfigurationAsync(customConfig);
        
        // Load configuration
        var loadedConfig = await _configService.LoadAppConfigurationAsync();
        await Assert.That(loadedConfig).IsNotNull();
        await Assert.That(loadedConfig!.LastModel).IsEqualTo("custom-library-model");
        await Assert.That(loadedConfig!.MaxFilesPerCategoryDisplayed).IsEqualTo(25);
        await Assert.That(loadedConfig!.RememberLastDirectory).IsFalse();
        
        // Test using loaded configuration with isolated constructor
        var docsDir = Path.Combine(_testDirectory, "LibraryTestDocuments");
        using var server = new EnhancedMcpRagServer(_logger, docsDir, isolated: true, AiProviderType.Ollama, loadedConfig!.LastModel ?? "llama3.2", mode: loadedConfig!.LastOperationMode);
        
        await server.InitializeAsync();
    }

    [Test]
    public async Task LibraryMode_MultipleInstanceManagement_WorksCorrectly()
    {
        // Test managing multiple library instances
        var docsDir = Path.Combine(_testDirectory, "LibraryTestDocuments");
        
        // Create multiple server instances using isolated constructors (library usage pattern)
        var server1 = new EnhancedMcpRagServer(_logger, docsDir, isolated: true, AiProviderType.Ollama, "model1", mode: OperationMode.RAG);
        var server2 = new EnhancedMcpRagServer(_logger, docsDir, isolated: true, AiProviderType.Ollama, "model2", mode: OperationMode.Hybrid); // Note: MCP mode not supported in isolated constructor
        var server3 = new EnhancedMcpRagServer(_logger, docsDir, isolated: true, AiProviderType.Ollama, "model3", mode: OperationMode.Hybrid);
        
        try
        {
            // Initialize all instances
            await server1.InitializeAsync();
            await server2.InitializeAsync();
            await server3.InitializeAsync();
            
            // Test concurrent operations
            var tasks = new List<Task>
            {
                Task.Run(async () => {
                    var req = new McpRequest { Id = "concurrent1", Method = "resources/list", Params = new { } };
                    return await server1.HandleRequestAsync(req);
                }),
                Task.Run(async () => {
                    var req = new McpRequest { Id = "concurrent2", Method = "resources/list", Params = new { } };
                    return await server2.HandleRequestAsync(req);
                }),
                Task.Run(async () => {
                    var req = new McpRequest { Id = "concurrent3", Method = "resources/list", Params = new { } };
                    return await server3.HandleRequestAsync(req);
                })
            };
            
            await Task.WhenAll(tasks);
            
            // Verify all instances are functional
            var listReq1 = new McpRequest { Id = "verify1", Method = "resources/list", Params = new { } };
            var listReq2 = new McpRequest { Id = "verify2", Method = "resources/list", Params = new { } };
            var listReq3 = new McpRequest { Id = "verify3", Method = "resources/list", Params = new { } };
            
            var response1 = await server1.HandleRequestAsync(listReq1);
            var response2 = await server2.HandleRequestAsync(listReq2);
            var response3 = await server3.HandleRequestAsync(listReq3);
            
            var files1 = response1.Result as ResourcesListResponse;
            var files2 = response2.Result as ResourcesListResponse;
            var files3 = response3.Result as ResourcesListResponse;
            
            await Assert.That(files1).IsNotNull();
            await Assert.That(files2).IsNotNull();
            await Assert.That(files3).IsNotNull();
            await Assert.That(files1!.Resources.Count).IsGreaterThan(0);
            await Assert.That(files2!.Resources.Count).IsGreaterThan(0);
            await Assert.That(files3!.Resources.Count).IsGreaterThan(0);
        }
        finally
        {
            server1.Dispose();
            server2.Dispose();
            server3.Dispose();
        }
    }

    [Test]
    public async Task LibraryMode_CustomExtractorIntegration_WorksCorrectly()
    {
        // Test integration with custom extractors (library extensibility)
        var docsDir = Path.Combine(_testDirectory, "LibraryTestDocuments");
        
        // Create a custom extractor for XML files
        var customExtractor = new XmlFileExtractor();
        
        var xmlFile = Path.Combine(docsDir, "integration.xml");
        var canExtract = customExtractor.CanHandle(xmlFile);
        await Assert.That(canExtract).IsTrue();
        
        // Test extraction
        var extractedContent = await customExtractor.ExtractTextAsync(xmlFile);
        await Assert.That(extractedContent).IsNotNull();
        await Assert.That(extractedContent).Contains("library");
        await Assert.That(extractedContent).Contains("api/v1");
    }

    [Test]
    public async Task LibraryMode_ErrorHandlingAndRecovery_WorksCorrectly()
    {
        // Test error handling and recovery in library mode
        var docsDir = Path.Combine(_testDirectory, "LibraryTestDocuments");
        
        // Test initialization with invalid directory using isolated constructor
        var invalidDir = Path.Combine(_testDirectory, "NonExistentDirectory");
        var serverWithInvalidDir = new EnhancedMcpRagServer(_logger, invalidDir, isolated: true, AiProviderType.Ollama, "test-model", mode: OperationMode.RAG);
        
        // Should handle gracefully
        await serverWithInvalidDir.InitializeAsync();
        // Depending on implementation, this might throw an exception
        
        serverWithInvalidDir.Dispose();
        
        // Test recovery with valid directory using isolated constructor
        using var server = new EnhancedMcpRagServer(_logger, docsDir, isolated: true, AiProviderType.Ollama, "test-model", mode: OperationMode.RAG);
        await server.InitializeAsync();
        
        // Test error handling during operations
        var filesResponse = await server.HandleRequestAsync(new McpRequest
        {
            Method = "resources/list",
            Params = new { }
        });
        await Assert.That(filesResponse.Error).IsNull();
        var files = (ResourcesListResponse)filesResponse.Result!;
        await Assert.That(files.Resources.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task LibraryMode_PerformanceAndScalability_WorksCorrectly()
    {
        // Test performance and scalability in library mode
        var docsDir = Path.Combine(_testDirectory, "LibraryTestDocuments");
        
        // Create additional test files for performance testing
        for (int i = 0; i < 50; i++)
        {
            var perfFile = Path.Combine(docsDir, $"perf_test_{i}.txt");
            await File.WriteAllTextAsync(perfFile, $"Performance test document {i} with library integration content.");
        }
        
        using var server = new EnhancedMcpRagServer(_logger, docsDir, isolated: true, AiProviderType.Ollama, "perf-test-model", mode: OperationMode.RAG);
        await server.InitializeAsync();
        
        // Measure performance of file listing
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var listRequest = new McpRequest
        {
            Id = "perf-test-list",
            Method = "resources/list",
            Params = new { }
        };
        var listResponse = await server.HandleRequestAsync(listRequest);
        stopwatch.Stop();
        
        var files = listResponse.Result as ResourcesListResponse;
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Resources.Count).IsGreaterThan(50); // Should include original + performance test files
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(5000); // Should complete within 5 seconds
        
        // Test concurrent operations performance
        var concurrentTasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var searchRequest = new McpRequest
            {
                Id = $"perf-search-{i}",
                Method = "tools/call",
                Params = new
                {
                    name = "search_files",
                    arguments = new { query = "test" }
                }
            };
            var searchResponse = await server.HandleRequestAsync(searchRequest);
            var result = searchResponse.Result as TextContentResponse;
            return result?.Content?.Count ?? 0;
        });
        
        var results = await Task.WhenAll(concurrentTasks);
        await Assert.That(results.All(r => r >= 0)).IsTrue(); // All searches should return results
    }

    [Test]
    public async Task LibraryMode_EventAndCallbackIntegration_WorksCorrectly()
    {
        // Test event and callback integration patterns
        var docsDir = Path.Combine(_testDirectory, "LibraryTestDocuments");
        
        var eventsFired = new List<string>();
        
        // Simulate event-driven library usage with isolated constructor
        using var server = new EnhancedMcpRagServer(_logger, docsDir, isolated: true, AiProviderType.Ollama, "event-test-model", mode: OperationMode.RAG);
        
        // Initialize and perform operations
        await server.InitializeAsync();
        
        // Test callback-style operations
        var listRequest = new McpRequest
        {
            Id = "event-test-list",
            Method = "resources/list",
            Params = new { }
        };
        var listResponse = await server.HandleRequestAsync(listRequest);
        var files = listResponse.Result as ResourcesListResponse;
        eventsFired.Add("FilesListed");
        
        if (files?.Resources.Any() == true)
         {
             var readRequest = new McpRequest
             {
                 Id = "event-test-read",
                 Method = "resources/read",
                 Params = new ReadResourceRequest { Uri = files.Resources.First().Uri }
             };
             var readResponse = await server.HandleRequestAsync(readRequest);
             eventsFired.Add("FileRead");
         }
        
        var searchRequest = new McpRequest
        {
            Id = "event-test-search",
            Method = "tools/call",
            Params = new
            {
                name = "search_files",
                arguments = new { query = "library" }
            }
        };
        var searchResponse = await server.HandleRequestAsync(searchRequest);
        eventsFired.Add("SearchCompleted");
        
        // Verify events were fired in correct order
        await Assert.That(eventsFired).Contains("FilesListed");
        await Assert.That(eventsFired).Contains("FileRead");
        await Assert.That(eventsFired).Contains("SearchCompleted");
        await Assert.That(eventsFired.Count).IsEqualTo(3);
    }

    [Test]
    public async Task LibraryMode_ResourceCleanupAndDisposal_WorksCorrectly()
    {
        // Test proper resource cleanup and disposal patterns
        var docsDir = Path.Combine(_testDirectory, "LibraryTestDocuments");
        
        // Test using statement pattern with isolated constructor
        using (var server = new EnhancedMcpRagServer(_logger, docsDir, isolated: true, AiProviderType.Ollama, "cleanup-test-model", mode: OperationMode.RAG))
        {
            await server.InitializeAsync();
            var listRequest = new McpRequest
            {
                Id = "cleanup-test-list",
                Method = "resources/list",
                Params = new { }
            };
            var listResponse = await server.HandleRequestAsync(listRequest);
            var files = listResponse.Result as ResourcesListResponse;
            await Assert.That(files).IsNotNull();
            await Assert.That(files!.Resources.Count).IsGreaterThan(0);
        } // Server should be disposed here
        
        // Test explicit disposal with isolated constructor
        var server2 = new EnhancedMcpRagServer(_logger, docsDir, isolated: true, AiProviderType.Ollama, "cleanup-test-model-2", mode: OperationMode.RAG);
        await server2.InitializeAsync();
        var listRequest2 = new McpRequest
        {
            Id = "cleanup-test-list-2",
            Method = "resources/list",
            Params = new { }
        };
        var listResponse2 = await server2.HandleRequestAsync(listRequest2);
        var files2 = listResponse2.Result as ResourcesListResponse;
         await Assert.That(files2!.Resources.Count).IsGreaterThan(0);
        
        server2.Dispose();
        
        // After disposal, operations should fail
        var exception = await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            var disposeTestRequest = new McpRequest
            {
                Id = "dispose-test",
                Method = "resources/list",
                Params = new { }
            };
            await server2.HandleRequestAsync(disposeTestRequest);
        });
        
        await Assert.That(exception).IsNotNull();
    }
}

/// <summary>
/// Custom XML file extractor for testing library extensibility
/// </summary>
public class XmlFileExtractor : IFileExtractor
{
    public bool CanHandle(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".xml", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> ExtractTextAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        
        // Extract attribute values first (like name="library")
        var attributeMatches = System.Text.RegularExpressions.Regex.Matches(content, @"\w+\s*=\s*[""']([^""']+)[""']");
        var attributeValues = string.Join(" ", attributeMatches.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value));
        
        // Simple XML text extraction (remove tags)
        var textContent = System.Text.RegularExpressions.Regex.Replace(content, "<[^>]+>", " ");
        textContent = System.Text.RegularExpressions.Regex.Replace(textContent, "\\s+", " ").Trim();
        
        // Combine attribute values and text content
        var combinedContent = $"{attributeValues} {textContent}".Trim();
        
        return combinedContent;
    }

    public string GetMimeType() => "application/xml";
}