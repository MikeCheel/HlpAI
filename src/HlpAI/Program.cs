using System.Text.Json;
using Microsoft.Extensions.Logging;
using HlpAI.Models;
using HlpAI.MCP;
using HlpAI.Services;
using HlpAI.Utilities;

namespace HlpAI;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task Main(string[] args)
    {
        // Add this check at the beginning for audit mode
        if (args.Length > 0 && args[0] == "--audit")
        {
            string auditPath = args.Length > 1 ? args[1] : @"C:\Demo\Documents";
            FileAuditUtility.AuditDirectory(auditPath);
            return;
        }

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<EnhancedMcpRagServer>();

        string rootPath;
        string ollamaModel;
        OperationMode mode;

        if (args.Length == 0)
        {
            // Interactive setup mode
            var setupResult = await InteractiveSetupAsync(logger);
            if (setupResult == null)
            {
                Console.WriteLine("❌ Setup cancelled. Exiting.");
                return;
            }
            
            rootPath = setupResult.Directory;
            ollamaModel = setupResult.Model;
            mode = setupResult.Mode;
        }
        else
        {
            // Command line mode
            rootPath = args[0];
            mode = ParseOperationMode(args.Length > 2 ? args[2] : "hybrid");

            if (!Directory.Exists(rootPath))
            {
                Console.WriteLine($"❌ Error: Directory '{rootPath}' does not exist.");
                Console.WriteLine();
                ShowUsage();
                return;
            }

            // Handle model selection
            if (args.Length > 1)
            {
                ollamaModel = args[1];
            }
            else
            {
                ollamaModel = await SelectModelAsync(logger);
                if (string.IsNullOrEmpty(ollamaModel))
                {
                    Console.WriteLine("❌ No model selected. Exiting.");
                    return;
                }
            }
        }

        var server = new EnhancedMcpRagServer(logger, rootPath, ollamaModel, mode);

        try
        {
            Console.WriteLine("Checking Ollama connection...");
            if (await server._ollamaClient.IsAvailableAsync())
            {
                var models = await server._ollamaClient.GetModelsAsync();
                Console.WriteLine($"✅ Ollama connected! Available models: {string.Join(", ", models)}");
                Console.WriteLine($"Using model: {ollamaModel}");
            }
            else
            {
                Console.WriteLine("⚠️ Ollama not available. AI features will show connection errors.");
                Console.WriteLine("To use AI features, install and run Ollama: https://ollama.ai");
            }

            Console.WriteLine($"\nOperation Mode: {mode}");

            if (mode == OperationMode.RAG || mode == OperationMode.Hybrid)
            {
                Console.WriteLine("Initializing RAG system...");
                await server.InitializeAsync();
            }

            ShowMenu();

            bool running = true;
            while (running)
            {
                Console.Write($"\nEnter command (1-13, c, m, q): ");
                var input = Console.ReadLine();

                try
                {
                    switch (input?.ToLower())
                    {
                        case "1":
                            if (server != null) 
                                await DemoListFiles(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "2":
                            if (server != null) 
                                await DemoReadFile(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "3":
                            if (server != null) 
                                await DemoSearchFiles(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "4":
                            if (server != null) 
                                await DemoAskAI(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "5":
                            if (server != null) 
                                await DemoAnalyzeFile(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "6":
                            if (server != null) 
                                await DemoRagSearch(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "7":
                            if (server != null) 
                                await DemoRagAsk(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "8":
                            if (server != null) 
                                await DemoReindex(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "9":
                            if (server != null) 
                                await DemoShowModels(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "10":
                            if (server != null) 
                                await DemoShowStatus(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "11":
                            if (server != null) 
                                await DemoIndexingReport(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "12":
                        case "server":
                            if (server != null) 
                                await RunServerMode(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "13":
                        case "dir":
                        case "directory":
                            if (server != null)
                            {
                                server = await ChangeDirectoryAsync(server, logger, ollamaModel, mode);
                                if (server == null)
                                    running = false;
                            }
                            break;
                        case "c":
                        case "clear":
                            ClearScreen();
                            break;
                        case "m":
                        case "menu":
                            ShowMenu();
                            break;
                        case "q":
                        case "quit":
                        case "exit":
                            running = false;
                            break;
                        default:
                            Console.WriteLine("Invalid command. Please try again.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
        finally
        {
            server?.Dispose();
        }
    }

    private static OperationMode ParseOperationMode(string modeString)
    {
        return modeString.ToLower() switch
        {
            "mcp" => OperationMode.MCP,
            "rag" => OperationMode.RAG,
            "hybrid" => OperationMode.Hybrid,
            _ => OperationMode.Hybrid
        };
    }

    private record SetupResult(string Directory, string Model, OperationMode Mode);

    private static async Task<SetupResult?> InteractiveSetupAsync(ILogger logger)
    {
        Console.WriteLine("🎯 HlpAI - Interactive Setup");
        Console.WriteLine("=============================================");
        Console.WriteLine();
        Console.WriteLine("Welcome! Let's configure your document intelligence system.");
        Console.WriteLine();

        // Step 1: Directory Selection
        Console.WriteLine("📁 Step 1: Document Directory");
        Console.WriteLine("------------------------------");
        string? directory = null;
        
        while (directory == null)
        {
            Console.Write("Enter the path to your documents directory: ");
            var input = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("❌ Directory path cannot be empty. Please try again.");
                continue;
            }
            
            if (input.ToLower() == "quit" || input.ToLower() == "exit")
            {
                return null;
            }
            
            if (!Directory.Exists(input))
            {
                Console.WriteLine($"❌ Directory '{input}' does not exist.");
                Console.Write("Would you like to create it? (y/n): ");
                var createResponse = Console.ReadLine()?.ToLower();
                
                if (createResponse == "y" || createResponse == "yes")
                {
                    try
                    {
                        Directory.CreateDirectory(input);
                        Console.WriteLine($"✅ Created directory: {input}");
                        directory = input;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Failed to create directory: {ex.Message}");
                        continue;
                    }
                }
                else
                {
                    Console.WriteLine("Please enter a valid existing directory path, or type 'quit' to exit.");
                    continue;
                }
            }
            else
            {
                directory = input;
            }
        }

        Console.WriteLine($"✅ Using directory: {directory}");
        Console.WriteLine();

        // Step 2: Model Selection
        Console.WriteLine("🤖 Step 2: AI Model Selection");
        Console.WriteLine("------------------------------");
        var model = await SelectModelAsync(logger);
        if (string.IsNullOrEmpty(model))
        {
            Console.WriteLine("❌ Model selection cancelled.");
            return null;
        }
        Console.WriteLine();

        // Step 3: Operation Mode Selection
        Console.WriteLine("⚙️ Step 3: Operation Mode");
        Console.WriteLine("-------------------------");
        Console.WriteLine("Available modes:");
        Console.WriteLine("  1. Hybrid (recommended) - Full MCP + RAG capabilities");
        Console.WriteLine("  2. MCP - Model Context Protocol server only");
        Console.WriteLine("  3. RAG - Retrieval-Augmented Generation only");
        Console.WriteLine();
        
        OperationMode selectedMode = OperationMode.Hybrid;
        while (true)
        {
            Console.Write("Select operation mode (1-3, default: 1): ");
            var modeInput = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(modeInput) || modeInput == "1")
            {
                selectedMode = OperationMode.Hybrid;
                break;
            }
            else if (modeInput == "2")
            {
                selectedMode = OperationMode.MCP;
                break;
            }
            else if (modeInput == "3")
            {
                selectedMode = OperationMode.RAG;
                break;
            }
            else if (modeInput.ToLower() == "quit" || modeInput.ToLower() == "exit")
            {
                return null;
            }
            else
            {
                Console.WriteLine("❌ Invalid selection. Please enter 1, 2, or 3.");
            }
        }

        Console.WriteLine($"✅ Selected mode: {selectedMode}");
        Console.WriteLine();

        // Summary
        Console.WriteLine("📋 Configuration Summary");
        Console.WriteLine("========================");
        Console.WriteLine($"Directory: {directory}");
        Console.WriteLine($"Model: {model}");
        Console.WriteLine($"Mode: {selectedMode}");
        Console.WriteLine();
        Console.Write("Continue with this configuration? (y/n): ");
        
        var confirmResponse = Console.ReadLine()?.ToLower();
        if (confirmResponse != "y" && confirmResponse != "yes")
        {
            Console.WriteLine("❌ Configuration cancelled.");
            return null;
        }

        Console.WriteLine("✅ Starting application with selected configuration...");
        Console.WriteLine();

        return new SetupResult(directory, model, selectedMode);
    }

    private static async Task<string> SelectModelAsync(ILogger logger)
    {
        Console.WriteLine("🤖 Model Selection");
        Console.WriteLine("==================");
        
        // Create a temporary client to check Ollama availability and get models
        using var tempClient = new OllamaClient(logger: logger);
        
        if (!await tempClient.IsAvailableAsync())
        {
            Console.WriteLine("❌ Ollama is not available. Please ensure Ollama is running on localhost:11434");
            Console.WriteLine("   Install Ollama: https://ollama.ai");
            Console.WriteLine();
            Console.WriteLine("Would you like to continue with the default model anyway? (y/n): ");
            var continueWithDefault = Console.ReadLine()?.ToLower() == "y";
            return continueWithDefault ? "llama3.2" : "";
        }

        var availableModels = await tempClient.GetModelsAsync();
        
        if (availableModels.Count == 0)
        {
            Console.WriteLine("❌ No models found in Ollama.");
            Console.WriteLine("   Install a model first: ollama pull llama3.2");
            Console.WriteLine();
            Console.WriteLine("Would you like to continue with 'llama3.2' anyway? (y/n): ");
            var continueWithDefault = Console.ReadLine()?.ToLower() == "y";
            return continueWithDefault ? "llama3.2" : "";
        }

        Console.WriteLine("✅ Ollama connected! Available models:");
        Console.WriteLine();
        
        for (int i = 0; i < availableModels.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {availableModels[i]}");
        }
        
        Console.WriteLine($"  {availableModels.Count + 1}. Enter custom model name");
        Console.WriteLine();
        
        while (true)
        {
            Console.Write($"Select a model (1-{availableModels.Count + 1}, or 'q' to quit): ");
            var input = Console.ReadLine()?.Trim();
            
            if (input?.ToLower() == "q")
            {
                return "";
            }
            
            if (int.TryParse(input, out int selection))
            {
                if (selection >= 1 && selection <= availableModels.Count)
                {
                    var selectedModel = availableModels[selection - 1];
                    Console.WriteLine($"✅ Selected model: {selectedModel}");
                    return selectedModel;
                }
                else if (selection == availableModels.Count + 1)
                {
                    Console.Write("Enter custom model name: ");
                    var customModel = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(customModel))
                    {
                        Console.WriteLine($"✅ Selected custom model: {customModel}");
                        Console.WriteLine("⚠️  Note: Make sure this model exists in Ollama or the application may fail.");
                        return customModel;
                    }
                }
            }
            
            Console.WriteLine("❌ Invalid selection. Please try again.");
        }
    }

    private static async Task DemoListFiles(EnhancedMcpRagServer server)
    {
        var request = new McpRequest { Method = "resources/list", Params = new { } };
        var response = await server.HandleRequestAsync(request);

        Console.WriteLine("\nAvailable files:");
        Console.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static async Task DemoReadFile(EnhancedMcpRagServer server)
    {
        Console.Write("Enter file URI (e.g., file:///example.txt): ");
        var uri = Console.ReadLine();

        if (string.IsNullOrEmpty(uri))
        {
            Console.WriteLine("URI cannot be empty.");
            return;
        }

        var request = new McpRequest
        {
            Method = "resources/read",
            Params = new ReadResourceRequest { Uri = uri }
        };

        var response = await server.HandleRequestAsync(request);
        Console.WriteLine("\nFile content:");
        Console.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static async Task DemoSearchFiles(EnhancedMcpRagServer server)
    {
        Console.Write("Enter search query: ");
        var query = Console.ReadLine();

        if (string.IsNullOrEmpty(query))
        {
            Console.WriteLine("Query cannot be empty.");
            return;
        }

        var request = new McpRequest
        {
            Method = "tools/call",
            Params = new { name = "search_files", arguments = new { query, maxResults = 10 } }
        };

        var response = await server.HandleRequestAsync(request);
        Console.WriteLine("\nSearch results:");
        Console.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static async Task DemoAskAI(EnhancedMcpRagServer server)
    {
        Console.Write("Enter your question: ");
        var question = Console.ReadLine();

        if (string.IsNullOrEmpty(question))
        {
            Console.WriteLine("Question cannot be empty.");
            return;
        }

        Console.Write("Enter additional context (optional, press Enter to skip): ");
        var context = Console.ReadLine();

        Console.Write("Enter temperature (0.0-1.0, default 0.7): ");
        var tempInput = Console.ReadLine();
        double temperature = 0.7;
        if (!string.IsNullOrEmpty(tempInput) && double.TryParse(tempInput, out var temp))
        {
            temperature = Math.Max(0.0, Math.Min(1.0, temp));
        }

        Console.Write("Use RAG enhancement? (y/n): ");
        var useRag = Console.ReadLine()?.ToLower() == "y";

        var arguments = new { question, context, useRag, temperature };
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = new { name = "ask_ai", arguments }
        };

        var response = await server.HandleRequestAsync(request);
        Console.WriteLine("\nAI Response:");
        DisplayResponse(response, "AI Response");
    }

    private static async Task DemoAnalyzeFile(EnhancedMcpRagServer server)
    {
        Console.Write("Enter file URI (e.g., file:///example.txt): ");
        var uri = Console.ReadLine();

        if (string.IsNullOrEmpty(uri))
        {
            Console.WriteLine("URI cannot be empty.");
            return;
        }

        Console.WriteLine("Available analysis types: summary, key_points, questions, topics, technical, explanation");
        Console.Write("Enter analysis type (default: summary): ");
        var analysisType = Console.ReadLine();

        if (string.IsNullOrEmpty(analysisType))
        {
            analysisType = "summary";
        }

        Console.Write("Enter temperature (0.0-1.0, default 0.7): ");
        var tempInput = Console.ReadLine();
        double temperature = 0.7;
        if (!string.IsNullOrEmpty(tempInput) && double.TryParse(tempInput, out var temp))
        {
            temperature = Math.Max(0.0, Math.Min(1.0, temp));
        }

        Console.Write("Use RAG enhancement? (y/n): ");
        var useRag = Console.ReadLine()?.ToLower() == "y";

        var request = new McpRequest
        {
            Method = "tools/call",
            Params = new { name = "analyze_file", arguments = new { uri, analysisType, temperature, useRag } }
        };

        var response = await server.HandleRequestAsync(request);
        Console.WriteLine("\nFile Analysis:");
        DisplayResponse(response, "File Analysis");
    }

    private static async Task DemoRagSearch(EnhancedMcpRagServer server)
    {
        Console.Write("Enter semantic search query: ");
        var query = Console.ReadLine();

        if (string.IsNullOrEmpty(query))
        {
            Console.WriteLine("Query cannot be empty.");
            return;
        }

        var request = new McpRequest
        {
            Method = "tools/call",
            Params = new { name = "rag_search", arguments = new { query, topK = 5, minSimilarity = 0.1 } }
        };

        var response = await server.HandleRequestAsync(request);
        Console.WriteLine("\nRAG Search Results:");
        DisplayResponse(response, "RAG Search Results");
    }

    private static async Task DemoRagAsk(EnhancedMcpRagServer server)
    {
        Console.Write("Enter your question for RAG-enhanced AI: ");
        var question = Console.ReadLine();

        if (string.IsNullOrEmpty(question))
        {
            Console.WriteLine("Question cannot be empty.");
            return;
        }

        Console.Write("Enter temperature (0.0-1.0, default 0.7): ");
        var tempInput = Console.ReadLine();
        double temperature = 0.7;
        if (!string.IsNullOrEmpty(tempInput) && double.TryParse(tempInput, out var temp))
        {
            temperature = Math.Max(0.0, Math.Min(1.0, temp));
        }

        Console.Write("Number of context chunks to retrieve (default 5): ");
        var topKInput = Console.ReadLine();
        int topK = 5;
        if (!string.IsNullOrEmpty(topKInput) && int.TryParse(topKInput, out var k))
        {
            topK = Math.Max(1, Math.Min(20, k));
        }

        var request = new McpRequest
        {
            Method = "tools/call",
            Params = new { name = "rag_ask", arguments = new { question, topK, temperature } }
        };

        var response = await server.HandleRequestAsync(request);
        Console.WriteLine("\nRAG-Enhanced AI Response:");
        DisplayResponse(response, "RAG-Enhanced AI Response");
    }

    private static async Task DemoReindex(EnhancedMcpRagServer server)
    {
        Console.WriteLine("Reindexing documents...");
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = new { name = "reindex_documents", arguments = new { } }
        };

        var response = await server.HandleRequestAsync(request);
        Console.WriteLine("\nReindexing Results:");
        DisplayResponse(response, "Reindexing Results");
    }

    private static async Task DemoShowModels(EnhancedMcpRagServer server)
    {
        if (await server._ollamaClient.IsAvailableAsync())
        {
            var models = await server._ollamaClient.GetModelsAsync();
            Console.WriteLine($"\nAvailable Ollama models: {string.Join(", ", models)}");
        }
        else
        {
            Console.WriteLine("\nOllama is not available. Cannot retrieve models.");
        }
    }

    private static async Task DemoShowStatus(EnhancedMcpRagServer server)
    {
        Console.WriteLine("\n=== System Status ===");
        Console.WriteLine($"Operation Mode: {server._operationMode}");
        Console.WriteLine($"Ollama Available: {(await server._ollamaClient.IsAvailableAsync() ? "✅ Yes" : "❌ No")}");
        
        if (server._vectorStore != null)
        {
            var chunkCount = await server._vectorStore.GetChunkCountAsync();
            var indexedFiles = await server._vectorStore.GetIndexedFilesAsync();
            Console.WriteLine($"Vector Store: ✅ Active");
            Console.WriteLine($"Indexed Documents: {indexedFiles.Count}");
            Console.WriteLine($"Document Chunks: {chunkCount}");
        }
        else
        {
            Console.WriteLine($"Vector Store: ❌ Not initialized");
        }
    }

    private static async Task DemoIndexingReport(EnhancedMcpRagServer server)
    {
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = new { name = "indexing_report", arguments = new { } }
        };

        var response = await server.HandleRequestAsync(request);
        Console.WriteLine("\nIndexing Report:");
        DisplayResponse(response, "Indexing Report");
    }

    private static Task RunServerMode(EnhancedMcpRagServer server)
    {
        Console.WriteLine("\n🖥️  MCP Server Mode");
        Console.WriteLine("===================");
        Console.WriteLine("The application is now running as an MCP (Model Context Protocol) server.");
        Console.WriteLine("You can interact with it programmatically using MCP requests.");
        Console.WriteLine();
        Console.WriteLine("📋 Available MCP Methods:");
        Console.WriteLine("  • resources/list     - List all available document resources");
        Console.WriteLine("  • resources/read     - Read content of a specific document");
        Console.WriteLine("  • tools/list         - List all available AI tools");
        Console.WriteLine("  • tools/call         - Execute an AI tool");
        Console.WriteLine();
        Console.WriteLine("🛠️ Available Tools:");
        Console.WriteLine("  • search_files       - Search files by text content");
        Console.WriteLine("  • ask_ai             - Ask AI questions with optional RAG");
        Console.WriteLine("  • analyze_file       - AI-powered file analysis");
        
        if (server._operationMode == OperationMode.RAG || server._operationMode == OperationMode.Hybrid)
        {
            Console.WriteLine("  • rag_search         - Semantic search using vectors");
            Console.WriteLine("  • rag_ask            - RAG-enhanced AI questioning");
            Console.WriteLine("  • reindex_documents  - Rebuild vector index");
            Console.WriteLine("  • indexing_report    - Get indexing status report");
        }
        
        Console.WriteLine();
        Console.WriteLine("📖 Example MCP Request:");
        Console.WriteLine("```json");
        Console.WriteLine("{");
        Console.WriteLine("  \"jsonrpc\": \"2.0\",");
        Console.WriteLine("  \"id\": \"1\",");
        Console.WriteLine("  \"method\": \"tools/call\",");
        Console.WriteLine("  \"params\": {");
        Console.WriteLine("    \"name\": \"ask_ai\",");
        Console.WriteLine("    \"arguments\": {");
        Console.WriteLine("      \"question\": \"What is this document about?\",");
        Console.WriteLine("      \"temperature\": 0.7,");
        Console.WriteLine("      \"useRag\": true");
        Console.WriteLine("    }");
        Console.WriteLine("  }");
        Console.WriteLine("}");
        Console.WriteLine("```");
        Console.WriteLine();
        Console.WriteLine("💡 Integration Tips:");
        Console.WriteLine("  • Use as a library: new EnhancedMcpRagServer(logger, path, model, mode)");
        Console.WriteLine("  • Call server.HandleRequestAsync(mcpRequest) for each request");
        Console.WriteLine("  • Responses follow standard MCP format with result/error fields");
        Console.WriteLine("  • Server configuration (directory, model, mode) is set via command line only");
        Console.WriteLine();
        Console.WriteLine("🎯 Server is ready! Press any key to return to interactive mode...");
        
        // Wait for any key press to return to menu
        Console.ReadKey(true);
        Console.WriteLine("\n📱 Returning to interactive mode...");
        
        return Task.CompletedTask;
    }

    private static async Task<EnhancedMcpRagServer?> ChangeDirectoryAsync(
        EnhancedMcpRagServer currentServer, 
        ILogger<EnhancedMcpRagServer> logger, 
        string ollamaModel, 
        OperationMode mode)
    {
        Console.WriteLine("\n📁 Change Document Directory");
        Console.WriteLine("=============================");
        Console.WriteLine($"Current directory: {currentServer.RootPath}");
        Console.WriteLine();
        
        Console.Write("Enter new document directory path (or 'cancel' to abort): ");
        var newPath = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(newPath) || newPath.ToLower() == "cancel")
        {
            Console.WriteLine("Directory change cancelled.");
            return currentServer;
        }
        
        // Validate the new directory
        if (!Directory.Exists(newPath))
        {
            Console.WriteLine($"❌ Error: Directory '{newPath}' does not exist.");
            Console.Write("Would you like to create it? (y/n): ");
            var createResponse = Console.ReadLine()?.ToLower();
            
            if (createResponse == "y" || createResponse == "yes")
            {
                try
                {
                    Directory.CreateDirectory(newPath);
                    Console.WriteLine($"✅ Created directory: {newPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to create directory: {ex.Message}");
                    return currentServer;
                }
            }
            else
            {
                Console.WriteLine("Directory change cancelled.");
                return currentServer;
            }
        }
        
        // Check if directory has any supported files
        var supportedExtensions = new[] { ".txt", ".md", ".html", ".htm", ".pdf", ".chm", ".hhc", ".log", ".csv" };
        var files = Directory.GetFiles(newPath, "*.*", SearchOption.AllDirectories)
            .Where(f => supportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();
            
        Console.WriteLine($"📊 Found {files.Count} supported files in the new directory.");
        
        if (files.Count == 0)
        {
            Console.WriteLine("⚠️  Warning: No supported files found in the directory.");
            Console.Write("Continue anyway? (y/n): ");
            var continueResponse = Console.ReadLine()?.ToLower();
            
            if (continueResponse != "y" && continueResponse != "yes")
            {
                Console.WriteLine("Directory change cancelled.");
                return currentServer;
            }
        }
        
        Console.WriteLine("\n🔄 Switching to new directory...");
        Console.WriteLine("⚠️  This will dispose the current server and create a new one.");
        
        // Dispose the current server
        currentServer.Dispose();
        
        try
        {
            // Create new server with the new directory
            var newServer = new EnhancedMcpRagServer(logger, newPath, ollamaModel, mode);
            
            Console.WriteLine("Checking Ollama connection...");
            if (await newServer._ollamaClient.IsAvailableAsync())
            {
                var models = await newServer._ollamaClient.GetModelsAsync();
                Console.WriteLine($"✅ Ollama connected! Available models: {string.Join(", ", models)}");
                Console.WriteLine($"Using model: {ollamaModel}");
            }
            else
            {
                Console.WriteLine("⚠️ Ollama not available. AI features will show connection errors.");
            }
            
            Console.WriteLine($"\nOperation Mode: {mode}");
            
            if (mode == OperationMode.RAG || mode == OperationMode.Hybrid)
            {
                Console.WriteLine("Initializing RAG system...");
                await newServer.InitializeAsync();
                
                var chunkCount = await newServer._vectorStore.GetChunkCountAsync();
                var indexedFiles = await newServer._vectorStore.GetIndexedFilesAsync();
                Console.WriteLine($"✅ RAG initialization complete. Indexed {chunkCount} chunks from {indexedFiles.Count} files.");
            }
            
            Console.WriteLine($"\n✅ Successfully switched to directory: {newPath}");
            Console.WriteLine("📱 Returning to main menu...");
            
            return newServer;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error creating new server: {ex.Message}");
            Console.WriteLine("Returning null - application will exit.");
            return null;
        }
    }

    private static void ShowMenu()
    {
        Console.WriteLine("\n📚 HlpAI - Available Commands:");
        Console.WriteLine("📁 File Operations:");
        Console.WriteLine("  1 - List all available files");
        Console.WriteLine("  2 - Read specific file content");
        Console.WriteLine("  3 - Search files by text content");
        Console.WriteLine("\n🤖 AI Features:");
        Console.WriteLine("  4 - Ask AI questions (with optional RAG enhancement)");
        Console.WriteLine("  5 - Analyze specific files with AI");
        Console.WriteLine("\n🔍 RAG Features:");
        Console.WriteLine("  6 - Semantic search using vector embeddings");
        Console.WriteLine("  7 - RAG-enhanced AI questioning");
        Console.WriteLine("  8 - Reindex documents");
        Console.WriteLine("\n🛠️ System:");
        Console.WriteLine("  9 - Show available Ollama models");
        Console.WriteLine("  10 - Display system status");
        Console.WriteLine("  11 - Show comprehensive indexing report");
        Console.WriteLine("  12 - Run as MCP server (for integration)");
        Console.WriteLine("  13 - Change document directory");
        Console.WriteLine("  c - Clear screen");
        Console.WriteLine("  m - Show this menu");
        Console.WriteLine("  q - Quit");
    }

    private static void ClearScreen()
    {
        Console.Clear();
        Console.WriteLine("🎯 HlpAI");
        Console.WriteLine("========================");
    }

    private static void ShowUsage()
    {
        Console.WriteLine("🎯 MCP RAG Extended Demo");
        Console.WriteLine("========================");
        Console.WriteLine();
        Console.WriteLine("USAGE:");
        Console.WriteLine("  dotnet run                              # Interactive setup mode");
        Console.WriteLine("  dotnet run <directory> [model] [mode]  # Command line mode");
        Console.WriteLine("  dotnet run -- --audit <directory>      # Audit mode");
        Console.WriteLine();
        Console.WriteLine("INTERACTIVE MODE:");
        Console.WriteLine("  Run without parameters for guided setup:");
        Console.WriteLine("  • Choose document directory");
        Console.WriteLine("  • Select AI model from available options");
        Console.WriteLine("  • Configure operation mode");
        Console.WriteLine("  • Perfect for first-time users!");
        Console.WriteLine();
        Console.WriteLine("COMMAND LINE MODE:");
        Console.WriteLine("  <directory>  Required. Path to directory containing documents to process");
        Console.WriteLine("  [model]      Optional. Ollama model name (will prompt for selection if not provided)");
        Console.WriteLine("  [mode]       Optional. Operation mode: mcp, rag, hybrid (default: hybrid)");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine("  dotnet run                                       # Interactive setup");
        Console.WriteLine("  dotnet run \"C:\\MyDocuments\"                    # Will prompt for model selection");
        Console.WriteLine("  dotnet run \"C:\\MyDocuments\" \"llama3.1\"          # Use specific model");
        Console.WriteLine("  dotnet run \"C:\\MyDocuments\" \"llama3.2\" \"rag\"    # Use specific model and mode");
        Console.WriteLine("  dotnet run -- --audit \"C:\\MyDocuments\"         # Audit mode");
        Console.WriteLine();
        Console.WriteLine("OPERATION MODES:");
        Console.WriteLine("  hybrid   - Full MCP + RAG capabilities (recommended)");
        Console.WriteLine("  mcp      - Model Context Protocol server only");
        Console.WriteLine("  rag      - RAG (Retrieval-Augmented Generation) functionality only");
        Console.WriteLine();
        Console.WriteLine("SUPPORTED FILE TYPES:");
        Console.WriteLine("  📄 Text files: .txt, .md, .log, .csv");
        Console.WriteLine("  🌐 Web files: .html, .htm");
        Console.WriteLine("  📕 Documents: .pdf");
        Console.WriteLine("  📚 Help files: .hhc (all platforms), .chm (Windows only)");
        Console.WriteLine();
        Console.WriteLine("PREREQUISITES:");
        Console.WriteLine("  • .NET 9.0 SDK");
        Console.WriteLine("  • Ollama installed and running (for AI features)");
        Console.WriteLine("  • Models: ollama pull llama3.2 && ollama pull nomic-embed-text");
        Console.WriteLine();
        Console.WriteLine("TIP: Run with --audit first to analyze your documents before indexing!");
    }

    private static void DisplayResponse(McpResponse response, string fallbackTitle = "Response")
    {
        if (response.Result != null)
        {
            var resultJson = JsonSerializer.Serialize(response.Result);
            var resultElement = JsonSerializer.Deserialize<JsonElement>(resultJson);
            
            if (resultElement.TryGetProperty("content", out var contentArray) && 
                contentArray.ValueKind == JsonValueKind.Array &&
                contentArray.GetArrayLength() > 0)
            {
                var firstContent = contentArray[0];
                if (firstContent.TryGetProperty("text", out var textElement))
                {
                    Console.WriteLine(textElement.GetString());
                    return;
                }
            }
        }
        
        // Fallback to JSON if plain text extraction fails
        Console.WriteLine($"\n{fallbackTitle}:");
        Console.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
    }
}