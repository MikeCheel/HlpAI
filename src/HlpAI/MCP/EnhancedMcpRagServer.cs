using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using SystemPath = System.IO.Path;
using HlpAI.Models;
using HlpAI.Services;
using HlpAI.VectorStores;
using HlpAI.FileExtractors;

namespace HlpAI.MCP
{
    // Enhanced Server with RAG capabilities
    public class EnhancedMcpRagServer : IEnhancedMcpRagServer
    {
        private static readonly string[] RequiredQueryFields = ["query"];
        private static readonly string[] RequiredQuestionFields = ["question"];
        private static readonly string[] RequiredFileUriAndAnalysisFields = ["file_uri", "analysis_type"];

    private readonly ILogger<EnhancedMcpRagServer> _logger;
    private List<IFileExtractor>? _extractors;
    private readonly string _rootPath;
    public IAiProvider _aiProvider;
    private readonly EmbeddingService _embeddingService;
    public IVectorStore? _vectorStore;
    public readonly OperationMode _operationMode;
    private readonly AppConfiguration _config;
    
    public string RootPath => _rootPath;
    private bool _disposed = false;

    public EnhancedMcpRagServer(ILogger<EnhancedMcpRagServer> logger, string rootPath, string aiModel = "llama3.2", OperationMode mode = OperationMode.Hybrid)
    {
        _logger = logger;
        _rootPath = rootPath;
        _operationMode = mode;
        
        // Load configuration to get provider settings
        _config = ConfigurationService.LoadConfiguration(logger);
        
        // Retrieve API key if required for cloud providers
        string? apiKey = null;
        if (AiProviderFactory.RequiresApiKey(_config.LastProvider))
        {
            if (_config.UseSecureApiKeyStorage && OperatingSystem.IsWindows())
            {
                var apiKeyStorage = new SecureApiKeyStorage(logger);
                apiKey = apiKeyStorage.RetrieveApiKey(_config.LastProvider.ToString());
            }
        }
        
        // Create AI provider based on configuration
        _aiProvider = AiProviderFactory.CreateProvider(
            _config.LastProvider,
            aiModel,
            GetProviderUrl(_config, _config.LastProvider),
            apiKey,
            logger,
            _config
        );
        
        _embeddingService = new EmbeddingService(logger: logger, config: _config);

        InitializeVectorStore();
        InitializeExtractors();
    }

    // Constructor that accepts pre-loaded configuration to avoid duplicate loading
    public EnhancedMcpRagServer(ILogger<EnhancedMcpRagServer> logger, string rootPath, AppConfiguration config, string aiModel = "llama3.2", OperationMode mode = OperationMode.Hybrid)
    {
        _logger = logger;
        _rootPath = rootPath;
        _operationMode = mode;
        _config = config;
        
        // Retrieve API key if required for cloud providers
        string? apiKey = null;
        if (AiProviderFactory.RequiresApiKey(_config.LastProvider))
        {
            if (_config.UseSecureApiKeyStorage && OperatingSystem.IsWindows())
            {
                var apiKeyStorage = new SecureApiKeyStorage(logger);
                apiKey = apiKeyStorage.RetrieveApiKey(_config.LastProvider.ToString());
            }
        }
        
        // Create AI provider based on configuration
        _aiProvider = AiProviderFactory.CreateProvider(
            _config.LastProvider,
            aiModel,
            GetProviderUrl(_config, _config.LastProvider),
            apiKey,
            logger,
            _config
        );
        
        _embeddingService = new EmbeddingService(logger: logger, config: _config);

        InitializeVectorStore();
        InitializeExtractors();
    }

    private void InitializeVectorStore()
    {
        // Use optimized SQLite-backed vector store with MD5 checksum optimization
        var dbPath = Path.Combine(_rootPath, "vectors.db");
        var connectionString = $"Data Source={dbPath}";
        var changeDetectionService = new FileChangeDetectionService(null);
        _vectorStore = new OptimizedSqliteVectorStore(connectionString, _embeddingService, changeDetectionService, _config);
    }

    private void InitializeExtractors()
    {
        _extractors = [
            new TextFileExtractor(),
            new HtmlFileExtractor(),
            new PdfFileExtractor(),
            new ChmFileExtractor(_logger),
            new HhcFileExtractor()
        ];
    }
        
        /// <summary>
        /// Updates the AI provider instance for real-time provider switching
        /// </summary>
        /// <param name="newProvider">The new AI provider instance</param>
        public virtual void UpdateAiProvider(IAiProvider newProvider)
        {
            if (newProvider == null)
            {
                throw new ArgumentNullException(nameof(newProvider));
            }
            
            // Dispose the old provider if it implements IDisposable
            if (_aiProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            
            _aiProvider = newProvider;
            _logger?.LogInformation("AI provider switched to {ProviderName} using model {Model}", 
                newProvider.ProviderName, newProvider.CurrentModel);
        }

        private static string? GetProviderUrl(AppConfiguration config, AiProviderType providerType)
        {
            return providerType switch
            {
                AiProviderType.Ollama => config.OllamaUrl,
                AiProviderType.LmStudio => config.LmStudioUrl,
                AiProviderType.OpenWebUi => config.OpenWebUiUrl,
                _ => null
            };
        }

        public async Task InitializeAsync()
        {
            if (_operationMode == OperationMode.RAG || _operationMode == OperationMode.Hybrid)
            {
                _logger?.LogInformation("Initializing RAG vector store...");
                var result = await IndexAllDocumentsAsync();
                _logger?.LogInformation("RAG initialization complete. Indexed {ChunkCount} chunks from {FileCount} files in {Duration}.",
                    await _vectorStore!.GetChunkCountAsync(), result.IndexedFiles.Count, result.Duration);
            }
        }

        // Enhanced IndexAllDocumentsAsync with detailed reporting
        private async Task<IndexingResult> IndexAllDocumentsAsync()
        {
            var result = new IndexingResult
            {
                IndexingStarted = DateTime.UtcNow
            };

            var allFiles = Directory.GetFiles(_rootPath, "*.*", SearchOption.AllDirectories);
            _logger?.LogInformation("Found {TotalFiles} files to process in {RootPath}", allFiles.Length, _rootPath);

            foreach (var file in allFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    var fileExtension = Path.GetExtension(file);

                    // Skip system files and directories
                    if (ShouldSkipFile(file, fileInfo))
                    {
                        result.SkippedFiles.Add(new SkippedFile
                        {
                            FilePath = file,
                            Reason = GetSkipReason(file, fileInfo),
                            FileExtension = fileExtension,
                            FileSize = fileInfo.Length
                        });
                        continue;
                    }

                    // Find appropriate extractor
                    var extractor = _extractors?.FirstOrDefault(e => e.CanHandle(file));
                    if (extractor == null)
                    {
                        result.SkippedFiles.Add(new SkippedFile
                        {
                            FilePath = file,
                            Reason = $"No extractor available for file type '{fileExtension}'",
                            FileExtension = fileExtension,
                            FileSize = fileInfo.Length
                        });
                        continue;
                    }

                    // Try to extract content
                    var content = await extractor.ExtractTextAsync(file);
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        result.SkippedFiles.Add(new SkippedFile
                        {
                            FilePath = file,
                            Reason = "File contains no extractable text content",
                            FileExtension = fileExtension,
                            FileSize = fileInfo.Length
                        });
                        continue;
                    }

                    // Index the document
                    var metadata = new Dictionary<string, object>
                    {
                        ["mime_type"] = extractor.GetMimeType(),
                        ["file_size"] = fileInfo.Length,
                        ["last_modified"] = File.GetLastWriteTime(file),
                        ["extractor_type"] = extractor.GetType().Name
                    };

                    await _vectorStore!.IndexDocumentAsync(file, content, metadata);
                    result.IndexedFiles.Add(file);

                    _logger?.LogDebug("Successfully indexed {File} using {Extractor}",
                        Path.GetFileName(file), extractor.GetType().Name);
                }
                catch (Exception ex)
                {
                    result.FailedFiles.Add(new FailedFile
                    {
                        FilePath = file,
                        Error = ex.Message,
                        ExtractorType = _extractors?.FirstOrDefault(e => e.CanHandle(file))?.GetType().Name
                    });
                    _logger?.LogWarning(ex, "Failed to index file {File}", file);
                }
            }

            result.IndexingCompleted = DateTime.UtcNow;

            // Log comprehensive summary
            LogIndexingSummary(result);

            return result;
        }

        private static bool ShouldSkipFile(string filePath, FileInfo fileInfo)
        {
            var fileName = Path.GetFileName(filePath);
            var fileExtension = Path.GetExtension(filePath);

            // Skip hidden files
            if (fileName.StartsWith('.'))
                return true;

            // Skip system files
            if (fileInfo.Attributes.HasFlag(FileAttributes.System))
                return true;

            // Skip very large files (>100MB by default)
            if (fileInfo.Length > 100 * 1024 * 1024)
                return true;

            // Skip database files
            if (fileName.EndsWith(".db") || fileName.EndsWith(".sqlite"))
                return true;

            // Skip binary executables
            var binaryExtensions = new[] { ".exe", ".dll", ".bin", ".so", ".dylib" };
            if (binaryExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
                return true;

            // Skip image files (unless we add image processing later)
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
            if (imageExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
                return true;

            // Skip video/audio files
            var mediaExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mp3", ".wav", ".flac" };
            if (mediaExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
                return true;

            // Skip archive files
            var archiveExtensions = new[] { ".zip", ".rar", ".7z", ".tar", ".gz" };
            if (archiveExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static string GetSkipReason(string filePath, FileInfo fileInfo)
        {
            var fileName = Path.GetFileName(filePath);
            var fileExtension = Path.GetExtension(filePath);

            if (fileName.StartsWith('.'))
                return "Hidden file";

            if (fileInfo.Attributes.HasFlag(FileAttributes.System))
                return "System file";

            if (fileInfo.Length > 100 * 1024 * 1024)
                return $"File too large ({fileInfo.Length / (1024 * 1024):F1} MB)";

            if (fileName.EndsWith(".db") || fileName.EndsWith(".sqlite"))
                return "Database file";

            var binaryExtensions = new[] { ".exe", ".dll", ".bin", ".so", ".dylib" };
            if (binaryExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
                return "Binary executable";

            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
            if (imageExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
                return "Image file (not supported)";

            var mediaExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mp3", ".wav", ".flac" };
            if (mediaExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
                return "Media file (not supported)";

            var archiveExtensions = new[] { ".zip", ".rar", ".7z", ".tar", ".gz" };
            if (archiveExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
                return "Archive file (not supported)";

            return "Unknown reason";
        }

        private void LogIndexingSummary(IndexingResult result)
        {
            _logger?.LogInformation("=== INDEXING SUMMARY ===");
            _logger?.LogInformation("Duration: {Duration}", result.Duration);
            _logger?.LogInformation("Successfully indexed: {IndexedCount} files", result.IndexedFiles.Count);
            _logger?.LogInformation("Skipped: {SkippedCount} files", result.SkippedFiles.Count);
            _logger?.LogInformation("Failed: {FailedCount} files", result.FailedFiles.Count);

            if (result.SkippedFiles.Count > 0)
            {
                _logger?.LogInformation("\n=== SKIPPED FILES ===");

                // Group by reason
                var groupedSkipped = result.SkippedFiles.GroupBy(f => f.Reason);
                foreach (var group in groupedSkipped.OrderByDescending(g => g.Count()))
                {
                    _logger?.LogInformation("{Reason}: {Count} files", group.Key, group.Count());
                    foreach (var file in group.Take(_config.MaxFilesPerCategoryDisplayed)) // Show first files in each category
                    {
                        _logger?.LogInformation("  - {FileName} ({FileSize:N0} bytes)",
                            Path.GetFileName(file.FilePath), file.FileSize);
                    }
                    if (group.Count() > _config.MaxFilesPerCategoryDisplayed)
                    {
                        _logger?.LogInformation("  ... and {MoreCount} more files", group.Count() - _config.MaxFilesPerCategoryDisplayed);
                    }
                }
            }

            if (result.FailedFiles.Count > 0)
            {
                _logger?.LogWarning("\n=== FAILED FILES ===");
                foreach (var failed in result.FailedFiles.Take(_config.MaxFailedFilesDisplayed))
                {
                    _logger?.LogWarning("❌ {FileName}: {Error}",
                        Path.GetFileName(failed.FilePath), failed.Error);
                }
                if (result.FailedFiles.Count > _config.MaxFailedFilesDisplayed)
                {
                    _logger?.LogWarning("... and {MoreFailures} more failures", result.FailedFiles.Count - _config.MaxFailedFilesDisplayed);
                }
            }

            // File type summary
            var supportedExtensions = result.IndexedFiles
                .Select(f => Path.GetExtension(f))
                .GroupBy(ext => ext, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count());

            if (supportedExtensions.Any())
            {
                _logger?.LogInformation("\n=== INDEXED FILE TYPES ===");
                foreach (var extGroup in supportedExtensions)
                {
                    _logger?.LogInformation("{Extension}: {Count} files",
                        string.IsNullOrEmpty(extGroup.Key) ? "(no extension)" : extGroup.Key,
                        extGroup.Count());
                }
            }
        }

        public async Task<McpResponse> HandleRequestAsync(McpRequest request)
        {
            try
            {
                return request.Method switch
                {
                    "resources/list" => await ListResourcesAsync(request),
                    "resources/read" => await ReadResourceAsync(request),
                    "tools/list" => ListTools(request),
                    "tools/call" => await CallToolAsync(request),
                    _ => CreateErrorResponse(request.Id, "Method not found")
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling MCP request");
                return CreateErrorResponse(request.Id, ex.Message);
            }
        }

        private Task<McpResponse> ListResourcesAsync(McpRequest request)
        {
            var resources = new List<ResourceInfo>();

            foreach (var file in Directory.GetFiles(_rootPath, "*.*", SearchOption.AllDirectories))
            {
                var extractor = _extractors?.FirstOrDefault(e => e.CanHandle(file));
                if (extractor != null)
                {
                    var relativePath = SystemPath.GetRelativePath(_rootPath, file);
                    resources.Add(new ResourceInfo
                    {
                        Uri = $"file:///{relativePath.Replace('\\', '/')}",
                        Name = SystemPath.GetFileName(file),
                        Description = $"File: {relativePath}",
                        MimeType = extractor.GetMimeType()
                    });
                }
            }

            return Task.FromResult(new McpResponse
            {
                Id = request.Id,
                Result = new ResourcesListResponse { Resources = resources }
            });
        }

        private async Task<McpResponse> ReadResourceAsync(McpRequest request)
        {
            var readRequest = JsonSerializer.Deserialize<ReadResourceRequest>(
                JsonSerializer.Serialize(request.Params));

            if (readRequest?.Uri == null)
            {
                return CreateErrorResponse(request.Id, "Invalid request parameters");
            }

            var uri = readRequest.Uri;
            var filePath = uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase)
                ? SystemPath.Combine(_rootPath, uri[8..].Replace('/', '\\'))
                : SystemPath.Combine(_rootPath, uri);

            if (!File.Exists(filePath))
            {
                return CreateErrorResponse(request.Id, "File not found");
            }

            var extractor = _extractors?.FirstOrDefault(e => e.CanHandle(filePath));
            if (extractor == null)
            {
                return CreateErrorResponse(request.Id, "Unsupported file type");
            }

            var content = await extractor.ExtractTextAsync(filePath);

            return new McpResponse
            {
                Id = request.Id,
                Result = new ReadResourceResponse
                {
                    Contents = new List<ResourceContent>
                    {
                        new ResourceContent
                        {
                            Uri = readRequest.Uri,
                            MimeType = extractor.GetMimeType(),
                            Text = content
                        }
                    }
                }
            };
        }

        private McpResponse ListTools(McpRequest request)
        {
            var tools = new List<object>
            {
                new
                {
                    name = "search_files",
                    description = "Search for files containing specific text",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string", description = "Search query" },
                            fileTypes = new { type = "array", items = new { type = "string" }, description = "File extensions to search" }
                        },
                        required = RequiredQueryFields
                    }
                },
                new
                {
                    name = "ask_ai",
                    description = "Ask AI a question about file contents using the configured AI provider",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            question = new { type = "string", description = "Question to ask the AI" },
                            context = new { type = "string", description = "Optional context or file content to provide to the AI" },
                            temperature = new { type = "number", description = "Temperature for AI response (0.0-1.0)", @default = 0.7 },
                            use_rag = new { type = "boolean", description = "Whether to use RAG for context retrieval", @default = true }
                        },
                        required = RequiredQuestionFields
                    }
                },
                new
                {
                    name = "analyze_file",
                    description = "Analyze a specific file using AI",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            file_uri = new { type = "string", description = "URI of the file to analyze" },
                            analysis_type = new { type = "string", description = "Type of analysis (summary, key_points, questions, etc.)" },
                            use_rag = new { type = "boolean", description = "Whether to use RAG for enhanced context", @default = true }
                        },
                        required = RequiredFileUriAndAnalysisFields
                    }
                }
            };

            if (_operationMode == OperationMode.RAG || _operationMode == OperationMode.Hybrid)
            {
                tools.AddRange(
                [
                    new
                    {
                        name = "rag_search",
                        description = "Semantic search using RAG vector store",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                query = new { type = "string", description = "Search query for semantic search" },
                                top_k = new { type = "integer", description = "Number of top results to return", @default = 5 },
                                min_similarity = new { type = "number", description = "Minimum similarity threshold (0.0-1.0)", @default = 0.1 },
                                file_filters = new { type = "array", items = new { type = "string" }, description = "Filter by file names or paths" }
                            },
                            required = RequiredQueryFields
                        }
                    },
                    new
                    {
                        name = "rag_ask",
                        description = "Ask AI with RAG-enhanced context retrieval",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                question = new { type = "string", description = "Question to ask" },
                                top_k = new { type = "integer", description = "Number of context chunks to retrieve", @default = 5 },
                                temperature = new { type = "number", description = "AI response temperature", @default = 0.7 }
                            },
                            required = RequiredQuestionFields
                        }
                    },
                    new
                    {
                        name = "reindex_documents",
                        description = "Rebuild the RAG vector store index",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                force = new { type = "boolean", description = "Force reindexing even if index exists", @default = true }
                            }
                        }
                    },
                    new
                    {
                        name = "indexing_report",
                        description = "Get detailed report of indexed and non-indexed files",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                show_details = new { type = "boolean", description = "Show detailed file lists", @default = true }
                            }
                        }
                    }
                ]);
            }

            return new McpResponse
            {
                Id = request.Id,
                Result = new ToolsListResponse { Tools = tools }
            };
        }

        private async Task<McpResponse> CallToolAsync(McpRequest request)
        {
            try
            {
                var paramsJson = JsonSerializer.Serialize(request.Params);
                var toolCall = JsonSerializer.Deserialize<JsonElement>(paramsJson);

                if (!toolCall.TryGetProperty("name", out JsonElement nameElement))
                {
                    return CreateErrorResponse(request.Id, "Invalid tool call parameters - missing 'name'");
                }

                string? toolName = nameElement.GetString();

                if (string.IsNullOrEmpty(toolName))
                {
                    return CreateErrorResponse(request.Id, "Invalid tool call parameters - empty 'name'");
                }

                return toolName switch
                {
                    "search_files" => await SearchFilesAsync(request, toolCall),
                    "ask_ai" => await AskAIAsync(request, toolCall),
                    "analyze_file" => await AnalyzeFileAsync(request, toolCall),
                    "rag_search" => await RagSearchAsync(request, toolCall),
                    "rag_ask" => await RagAskAsync(request, toolCall),
                    "reindex_documents" => await ReindexDocumentsAsync(request, toolCall),
                    "indexing_report" => await ShowIndexingReportAsync(request, toolCall),
                    _ => CreateErrorResponse(request.Id, $"Unknown tool: {toolName}")
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(request.Id, $"Error processing tool call: {ex.Message}");
            }
        }

        // Add the new indexing report method
        private async Task<McpResponse> ShowIndexingReportAsync(McpRequest request, JsonElement _)
        {
            var chunkCount = await _vectorStore!.GetChunkCountAsync();
            var indexedFiles = await _vectorStore!.GetIndexedFilesAsync();

            var allFiles = Directory.GetFiles(_rootPath, "*.*", SearchOption.AllDirectories);
            var notIndexedFiles = allFiles.Where(f => !indexedFiles.Any(indexed =>
                Path.GetFullPath(indexed).Equals(Path.GetFullPath(f), StringComparison.OrdinalIgnoreCase))).ToList();

            var report = new StringBuilder();
            report.AppendLine("=== COMPREHENSIVE INDEXING REPORT ===\n");

            report.AppendLine($"📁 Root Directory: {_rootPath}");
            report.AppendLine($"⏰ Last Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

            report.AppendLine("📊 SUMMARY:");
            report.AppendLine($"  Total Files Found: {allFiles.Length}");
            report.AppendLine($"  Successfully Indexed: {indexedFiles.Count}");
            report.AppendLine($"  Not Indexed: {notIndexedFiles.Count}");
            report.AppendLine($"  Total Chunks: {chunkCount}\n");

            if (indexedFiles.Count > 0)
            {
                report.AppendLine("✅ INDEXED FILES:");
                var indexedByType = indexedFiles
                    .GroupBy(f => Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count());

                foreach (var group in indexedByType)
                {
                    var ext = string.IsNullOrEmpty(group.Key) ? "(no extension)" : group.Key;
                    report.AppendLine($"  {ext}: {group.Count()} files");
                }
                report.AppendLine();
            }

            if (notIndexedFiles.Count > 0)
            {
                report.AppendLine("❌ NOT INDEXED FILES:");

                foreach (var file in notIndexedFiles.Take(_config.MaxNotIndexedFilesDisplayed))
                {
                    var fileInfo = new FileInfo(file);
                    var reason = "Unknown";

                    if (ShouldSkipFile(file, fileInfo))
                    {
                        reason = GetSkipReason(file, fileInfo);
                    }
                    else if (_extractors?.All(e => !e.CanHandle(file)) == true)
                    {
                        reason = $"No extractor for {Path.GetExtension(file)}";
                    }
                    else
                    {
                        reason = "Processing failed (check logs)";
                    }

                    report.AppendLine($"  📄 {Path.GetFileName(file)} - {reason}");
                }

                if (notIndexedFiles.Count > _config.MaxNotIndexedFilesDisplayed)
                {
                    report.AppendLine($"  ... and {notIndexedFiles.Count - _config.MaxNotIndexedFilesDisplayed} more files\n");
                }

                // Group not-indexed files by reason
                var reasonGroups = new Dictionary<string, int>();
                foreach (var file in notIndexedFiles)
                {
                    var fileInfo = new FileInfo(file);
                    var reason = "Unknown";

                    if (ShouldSkipFile(file, fileInfo))
                    {
                        reason = GetSkipReason(file, fileInfo);
                    }
                    else if (_extractors!.All(e => !e.CanHandle(file)))
                    {
                        reason = $"Unsupported file type";
                    }
                    else
                    {
                        reason = "Processing error";
                    }

                    reasonGroups[reason] = reasonGroups.GetValueOrDefault(reason, 0) + 1;
                }

                report.AppendLine("\n📈 NOT INDEXED - BY REASON:");
                foreach (var group in reasonGroups.OrderByDescending(g => g.Value))
                {
                    report.AppendLine($"  {group.Key}: {group.Value} files");
                }
            }

            return new McpResponse
            {
                Id = request.Id,
                Result = new TextContentResponse
                {
                    Content = new List<TextContent>
                    {
                        new TextContent
                        {
                            Type = "text",
                            Text = report.ToString()
                        }
                    }
                }
            };
        }

        private async Task<McpResponse> SearchFilesAsync(McpRequest request, JsonElement toolCall)
        {
            if (!toolCall.TryGetProperty("arguments", out JsonElement arguments))
            {
                return CreateErrorResponse(request.Id, "Missing arguments");
            }

            if (!arguments.TryGetProperty("query", out JsonElement queryElement))
            {
                return CreateErrorResponse(request.Id, "Query is required");
            }

            string? query = queryElement.GetString();

            if (string.IsNullOrEmpty(query))
            {
                return CreateErrorResponse(request.Id, "Query is required");
            }

            var results = new List<object>();

            foreach (var file in Directory.GetFiles(_rootPath, "*.*", SearchOption.AllDirectories))
            {
                var extractor = _extractors?.FirstOrDefault(e => e.CanHandle(file));
                if (extractor != null)
                {
                    try
                    {
                        var content = await extractor.ExtractTextAsync(file);
                        if (content.Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(new
                            {
                                file = SystemPath.GetRelativePath(_rootPath, file),
                                matches = CountOccurrences(content, query)
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error searching file {File}", file);
                    }
                }
            }

            return new McpResponse
            {
                Id = request.Id,
                Result = new TextContentResponse
                {
                    Content = new List<TextContent>
                    {
                        new TextContent
                        {
                            Type = "text",
                            Text = $"Found {results.Count} files containing '{query}':\n" +
                                   string.Join("\n", results.Select(r => $"- {((dynamic)r).file} ({((dynamic)r).matches} matches)"))
                        }
                    }
                }
            };
        }

        private async Task<McpResponse> AskAIAsync(McpRequest request, JsonElement toolCall)
        {
            if (!toolCall.TryGetProperty("arguments", out JsonElement arguments))
            {
                return CreateErrorResponse(request.Id, "Missing arguments");
            }

            if (!arguments.TryGetProperty("question", out JsonElement questionElement))
            {
                return CreateErrorResponse(request.Id, "Question is required");
            }

            string? question = questionElement.GetString();
            string? explicitContext = arguments.TryGetProperty("context", out JsonElement contextProp)
                ? contextProp.GetString()
                : "";
            double temperature = arguments.TryGetProperty("temperature", out JsonElement tempProp)
                ? tempProp.GetDouble()
                : 0.7;
            bool useRag = arguments.TryGetProperty("use_rag", out JsonElement ragProp)
                ? ragProp.GetBoolean()
                : (_operationMode == OperationMode.RAG || _operationMode == OperationMode.Hybrid);

            if (string.IsNullOrEmpty(question))
            {
                return CreateErrorResponse(request.Id, "Question is required");
            }

            if (!await _aiProvider.IsAvailableAsync())
            {
                return CreateErrorResponse(request.Id, GetProviderUnavailableMessage());
            }

            string? context = explicitContext;

            if (useRag && (_operationMode == OperationMode.RAG || _operationMode == OperationMode.Hybrid))
            {
                var ragQuery = new RagQuery
                {
                    Query = question,
                    TopK = 3,
                    MinSimilarity = 0.1f
                };

                var searchResults = await _vectorStore!.SearchAsync(ragQuery);
                if (searchResults.Count > 0)
                {
                    var ragContext = string.Join("\n\n", searchResults.Select(r =>
                        $"[From {SystemPath.GetFileName(r.Chunk.SourceFile ?? "Unknown")}] {r.Chunk.Content}"));

                    context = string.IsNullOrEmpty(explicitContext)
                        ? ragContext
                        : $"{explicitContext}\n\nAdditional context from documents:\n{ragContext}";
                }
            }

            var aiResponse = await _aiProvider.GenerateAsync(question, context, temperature);

            return new McpResponse
            {
                Id = request.Id,
                Result = new TextContentResponse
                {
                    Content = new List<TextContent>
                    {
                        new TextContent
                        {
                            Type = "text",
                            Text = aiResponse
                        }
                    }
                }
            };
        }

        private async Task<McpResponse> AnalyzeFileAsync(McpRequest request, JsonElement toolCall)
        {
            if (!toolCall.TryGetProperty("arguments", out JsonElement arguments))
            {
                return CreateErrorResponse(request.Id, "Missing arguments");
            }

            if (!arguments.TryGetProperty("file_uri", out JsonElement fileUriElement))
            {
                return CreateErrorResponse(request.Id, "File URI is required");
            }

            if (!arguments.TryGetProperty("analysis_type", out JsonElement analysisTypeElement))
            {
                return CreateErrorResponse(request.Id, "Analysis type is required");
            }

            string? fileUri = fileUriElement.GetString();
            string? analysisType = analysisTypeElement.GetString();
            bool useRag = arguments.TryGetProperty("use_rag", out JsonElement ragProp)
                ? ragProp.GetBoolean()
                : (_operationMode == OperationMode.RAG || _operationMode == OperationMode.Hybrid);

            if (string.IsNullOrEmpty(fileUri) || string.IsNullOrEmpty(analysisType))
            {
                return CreateErrorResponse(request.Id, "File URI and analysis type are required");
            }

            var readRequest = new McpRequest
            {
                Method = "resources/read",
                Params = new ReadResourceRequest { Uri = fileUri }
            };

            var readResponse = await ReadResourceAsync(readRequest);
            if (readResponse.Error != null)
            {
                return readResponse;
            }

            var result = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(readResponse.Result));
            var contents = result.GetProperty("contents").EnumerateArray().First();
            var fileContent = contents.GetProperty("text").GetString();

            var analysisPrompts = new Dictionary<string, string>
            {
                ["summary"] = "Please provide a concise summary of the following content:",
                ["key_points"] = "Please extract the key points from the following content:",
                ["questions"] = "Based on the following content, what questions might someone have?",
                ["topics"] = "What are the main topics covered in the following content?",
                ["technical"] = "Provide a technical analysis of the following content:",
                ["explanation"] = "Please explain the following content in simple terms:"
            };

            var prompt = analysisPrompts.TryGetValue(analysisType, out var specificPrompt)
                ? specificPrompt
                : $"Please analyze the following content for {analysisType}:";

            string? context = fileContent;

            if (useRag && (_operationMode == OperationMode.RAG || _operationMode == OperationMode.Hybrid))
            {
                var ragQuery = new RagQuery
                {
                    Query = $"{analysisType} {SystemPath.GetFileNameWithoutExtension(fileUri)}",
                    TopK = 3,
                    MinSimilarity = 0.2f
                };

                var searchResults = await _vectorStore!.SearchAsync(ragQuery);
                if (searchResults.Count > 0)
                {
                    var ragContext = string.Join("\n\n", searchResults
                        .Where(r => r.Chunk.SourceFile != null && !r.Chunk.SourceFile.EndsWith(fileUri.Replace("file:///", ""), StringComparison.OrdinalIgnoreCase))
                        .Select(r => $"[Related content from {SystemPath.GetFileName(r.Chunk.SourceFile ?? "Unknown")}] {r.Chunk.Content}"));

                    if (!string.IsNullOrEmpty(ragContext))
                    {
                        context = $"{fileContent}\n\nRelated information from other documents:\n{ragContext}";
                    }
                }
            }

            if (!await _aiProvider.IsAvailableAsync())
            {
                return CreateErrorResponse(request.Id, GetProviderUnavailableMessage());
            }

            var analysis = await _aiProvider.GenerateAsync(prompt, context, 0.3);

            return new McpResponse
            {
                Id = request.Id,
                Result = new TextContentResponse
                {
                    Content = new List<TextContent>
                    {
                        new TextContent
                        {
                            Type = "text",
                            Text = $"File: {fileUri}\nAnalysis Type: {analysisType}\nMode: {(_operationMode == OperationMode.RAG || _operationMode == OperationMode.Hybrid ? "RAG-Enhanced" : "Standard")}\n\n{analysis}"
                        }
                    }
                }
            };
        }

        private async Task<McpResponse> RagSearchAsync(McpRequest request, JsonElement toolCall)
        {
            if (_operationMode == OperationMode.MCP)
            {
                return CreateErrorResponse(request.Id, "RAG search is not available in MCP-only mode");
            }

            if (!toolCall.TryGetProperty("arguments", out JsonElement arguments))
            {
                return CreateErrorResponse(request.Id, "Missing arguments");
            }

            if (!arguments.TryGetProperty("query", out JsonElement queryElement))
            {
                return CreateErrorResponse(request.Id, "Query is required");
            }

            string? query = queryElement.GetString();
            int topK = arguments.TryGetProperty("top_k", out JsonElement topKProp) ? topKProp.GetInt32() : 5;
            float minSimilarity = arguments.TryGetProperty("min_similarity", out JsonElement simProp) ? simProp.GetSingle() : 0.1f;

            if (string.IsNullOrEmpty(query))
            {
                return CreateErrorResponse(request.Id, "Query is required");
            }

            var fileFilters = new List<string>();
            if (arguments.TryGetProperty("file_filters", out JsonElement filtersProp))
            {
                fileFilters = [.. filtersProp.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => s != null)
                    .Cast<string>()];
            }

            var ragQuery = new RagQuery
            {
                Query = query,
                TopK = topK,
                MinSimilarity = minSimilarity,
                FileFilters = fileFilters
            };

            var searchResults = await _vectorStore!.SearchAsync(ragQuery);

            var resultText = new StringBuilder();
            resultText.AppendLine($"RAG Search Results for: '{query}'");
            resultText.AppendLine($"Found {searchResults.Count} relevant chunks:\n");

            foreach (var result in searchResults)
            {
                resultText.AppendLine($"📄 {SystemPath.GetFileName(result.Chunk.SourceFile ?? "Unknown")} (Similarity: {result.Similarity:F3})");
                resultText.AppendLine($"   {result.Chunk.Content[..Math.Min(200, result.Chunk.Content.Length)]}...");
                resultText.AppendLine();
            }

            return new McpResponse
            {
                Id = request.Id,
                Result = new TextContentResponse
                {
                    Content = new List<TextContent>
                    {
                        new TextContent
                        {
                            Type = "text",
                            Text = resultText.ToString()
                        }
                    }
                }
            };
        }

        private async Task<McpResponse> RagAskAsync(McpRequest request, JsonElement toolCall)
        {
            if (_operationMode == OperationMode.MCP)
            {
                return CreateErrorResponse(request.Id, "RAG ask is not available in MCP-only mode");
            }

            if (!toolCall.TryGetProperty("arguments", out JsonElement arguments))
            {
                return CreateErrorResponse(request.Id, "Missing arguments");
            }

            if (!arguments.TryGetProperty("question", out JsonElement questionElement))
            {
                return CreateErrorResponse(request.Id, "Question is required");
            }

            string? question = questionElement.GetString();
            int topK = arguments.TryGetProperty("top_k", out JsonElement topKProp) ? topKProp.GetInt32() : 5;
            double temperature = arguments.TryGetProperty("temperature", out JsonElement tempProp) ? tempProp.GetDouble() : 0.7;

            if (string.IsNullOrEmpty(question))
            {
                return CreateErrorResponse(request.Id, "Question is required");
            }

            if (!await _aiProvider.IsAvailableAsync())
            {
                return CreateErrorResponse(request.Id, GetProviderUnavailableMessage());
            }

            var ragQuery = new RagQuery
            {
                Query = question,
                TopK = topK,
                MinSimilarity = 0.1f
            };

            var searchResults = await _vectorStore!.SearchAsync(ragQuery);
            string context = "";

            if (searchResults.Count > 0)
            {
                context = string.Join("\n\n", searchResults.Select(r =>
                    $"[From {SystemPath.GetFileName(r.Chunk.SourceFile ?? "Unknown")} - Similarity: {r.Similarity:F3}]\n{r.Chunk.Content}"));
            }

            var aiResponse = await _aiProvider.GenerateAsync(question, context, temperature);

            return new McpResponse
            {
                Id = request.Id,
                Result = new TextContentResponse
                {
                    Content = new List<TextContent>
                    {
                        new TextContent
                        {
                            Type = "text",
                            Text = $"RAG-Enhanced Response (using {searchResults.Count} context chunks):\n\n{aiResponse}"
                        }
                    }
                }
            };
        }

        private async Task<McpResponse> ReindexDocumentsAsync(McpRequest request, JsonElement toolCall)
        {
            try
            {
                if (_operationMode == OperationMode.MCP)
                {
                    return CreateErrorResponse(request.Id, "Document indexing is not available in MCP-only mode");
                }

                if (!toolCall.TryGetProperty("arguments", out JsonElement arguments))
                {
                    return CreateErrorResponse(request.Id, "Missing arguments");
                }

                // Always force reindexing by default to prevent system conflicts
                await _vectorStore!.ClearIndexAsync();
                var result = await IndexAllDocumentsAsync();

                return new McpResponse
                {
                    Id = request.Id,
                    Result = new TextContentResponse
                    {
                        Content = new List<TextContent>
                        {
                            new TextContent
                            {
                                Type = "text",
                                Text = $"Successfully reindexed {await _vectorStore.GetChunkCountAsync()} chunks from {result.IndexedFiles.Count} files in {result.Duration}."
                            }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during document reindexing");
                return CreateErrorResponse(request.Id, $"Reindexing failed: {ex.Message}");
            }
        }

        private static int CountOccurrences(string text, string search)
        {
            return (text.Length - text.Replace(search, "", StringComparison.OrdinalIgnoreCase).Length) / search.Length;
        }

        private static McpResponse CreateErrorResponse(string id, string message)
        {
            return new McpResponse
            {
                Id = id,
                Error = new ErrorResponse { Code = -1, Message = message }
            };
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _aiProvider?.Dispose();
                _embeddingService?.Dispose();
                _vectorStore?.Dispose();
                foreach (var extractor in _extractors?.OfType<IDisposable>() ?? Enumerable.Empty<IDisposable>())
                {
                    extractor?.Dispose();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Get appropriate error message for provider unavailability based on provider type
        /// </summary>
        private string GetProviderUnavailableMessage()
        {
            var isCloudProvider = _aiProvider.ProviderType == AiProviderType.OpenAI ||
                                 _aiProvider.ProviderType == AiProviderType.Anthropic ||
                                 _aiProvider.ProviderType == AiProviderType.DeepSeek;

            if (isCloudProvider)
            {
                return $"{_aiProvider.ProviderName} is not available. Please check your API key configuration and internet connection.";
            }
            else
            {
                return $"{_aiProvider.ProviderName} is not available. Please ensure the provider is running at {_aiProvider.BaseUrl}";
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
