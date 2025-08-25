using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using HlpAI.Models;
using HlpAI.MCP;
using HlpAI.Services;
using HlpAI.Utilities;
using HlpAI.VectorStores;
using HlpAI.FileExtractors;

namespace HlpAI;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly PromptService _promptService = new PromptService();

    /// <summary>
    /// Safely prompts for string input with default value handling
    /// </summary>
    private static string SafePromptForString(string prompt, string defaultValue = "")
    {
        return _promptService.PromptForString(prompt, defaultValue);
    }

    /// <summary>
    /// Safely prompts for yes/no input with default handling
    /// </summary>
    private static async Task<bool> SafePromptYesNoAsync(string prompt, bool defaultToYes = true)
    {
        return await _promptService.PromptYesNoAsync(prompt, defaultToYes);
    }

    public static async Task Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<EnhancedMcpRagServer>();

        // Parse command line arguments
        var cmdArgs = new CommandLineArgumentsService(args, logger);

        // Check for help first
        if (cmdArgs.ShouldShowHelp())
        {
            ShowUsage();
            return;
        }

        // Handle logging-only commands first
        if (cmdArgs.IsLoggingOnlyCommand())
        {
            using var loggingService = new ErrorLoggingService(logger);
            await cmdArgs.ApplyLoggingConfigurationAsync(loggingService);
            return;
        }

        // Handle extractor management commands
        if (cmdArgs.IsExtractorManagementCommand())
        {
            await cmdArgs.ApplyExtractorManagementConfigurationAsync();
            return;
        }

        // Handle AI provider management commands
        if (cmdArgs.IsAiProviderManagementCommand())
        {
            await cmdArgs.ApplyAiProviderConfigurationAsync();
            return;
        }

        // Add this check at the beginning for audit mode
        if (args.Length > 0 && args[0] == "--audit")
        {
            string auditPath = args.Length > 1 ? args[1] : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            FileAuditUtility.AuditDirectory(auditPath);
            return;
        }

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
                
                // Log the command line error
                using var cmdErrorLoggingService = new ErrorLoggingService(logger);
                await cmdErrorLoggingService.LogErrorAsync($"Directory does not exist: {rootPath}", null, "Command line mode - directory validation");
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
        
        // Initialize error logging service for main interactive mode
        using var mainErrorLoggingService = new ErrorLoggingService(logger);
        
        // Apply any logging configuration from command line
        await cmdArgs.ApplyLoggingConfigurationAsync(mainErrorLoggingService);

        try
        {
            Console.WriteLine("Checking AI provider connection...");
            if (await server._aiProvider.IsAvailableAsync())
            {
                var models = await server._aiProvider.GetModelsAsync();
                Console.WriteLine($"✅ {server._aiProvider.ProviderName} connected! Available models: {string.Join(", ", models)}");
                Console.WriteLine($"Using model: {ollamaModel}");
            }
            else
            {
                Console.WriteLine($"⚠️ {server._aiProvider.ProviderName} not available. AI features will show connection errors.");
                Console.WriteLine($"To use AI features, ensure {server._aiProvider.ProviderName} is running at {server._aiProvider.BaseUrl}");
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
                var input = SafePromptForString("\nEnter command (1-16, c, m, q)", "q"); // Default to quit if Enter pressed

                try
                {
                    switch (input?.ToLower())
                    {
                        case "1":
                            ClearScreen();
                            if (server != null) 
                                await DemoListFiles(server);
                            else
                            {
                                var serverError = "Server not available for list files command";
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                                await mainErrorLoggingService.LogErrorAsync(serverError, null, "Interactive command - list files");
                            }
                            break;
                        case "2":
                            ClearScreen();
                            if (server != null) 
                                await DemoReadFile(server);
                            else
                            {
                                var serverError = "Server not available for read file command";
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                                await mainErrorLoggingService.LogErrorAsync(serverError, null, "Interactive command - read file");
                            }
                            break;
                        case "3":
                            ClearScreen();
                            if (server != null) 
                                await DemoSearchFiles(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "4":
                            ClearScreen();
                            if (server != null) 
                                await DemoAskAI(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "5":
                            ClearScreen();
                            if (server != null) 
                                await DemoAnalyzeFile(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "6":
                            ClearScreen();
                            if (server != null) 
                                await DemoRagSearch(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "7":
                            ClearScreen();
                            if (server != null) 
                                await DemoRagAsk(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "8":
                            ClearScreen();
                            if (server != null) 
                                await DemoReindex(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "9":
                            ClearScreen();
                            if (server != null) 
                                await DemoShowModels(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "10":
                            ClearScreen();
                            if (server != null) 
                                await DemoShowStatus(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "11":
                            ClearScreen();
                            if (server != null) 
                                await DemoIndexingReport(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "12":
                        case "server":
                            ClearScreen();
                            if (server != null) 
                                await RunServerMode(server);
                            else
                                Console.WriteLine("❌ Server not available. Please restart the application.");
                            break;
                        case "13":
                        case "dir":
                        case "directory":
                            ClearScreen();
                            if (server != null)
                            {
                                server = await ChangeDirectoryAsync(server, logger, ollamaModel, mode);
                                if (server == null)
                                    running = false;
                            }
                            break;
                        case "14":
                        case "config":
                        case "configuration":
                            await ShowConfigurationMenuAsync();
                            ShowMenu(); // Restore main menu after sub-menu
                            break;
                        case "15":
                        case "logs":
                        case "errorlogs":
                            await ShowLogViewerAsync();
                            ShowMenu(); // Restore main menu after sub-menu
                            break;
                        case "16":
                        case "extractors":
                        case "extractor-management":
                            await ShowExtractorManagementMenuAsync();
                            ShowMenu(); // Restore main menu after sub-menu
                            break;
                        case "17":
                        case "ai":
                        case "ai-provider":
                            await ShowAiProviderMenuAsync();
                            ShowMenu(); // Restore main menu after sub-menu
                            break;
                        case "18":
                        case "vector":
                        case "vector-db":
                        case "vector-database":
                            await ShowVectorDatabaseManagementMenuAsync();
                            ShowMenu(); // Restore main menu after sub-menu
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
                    var errorMessage = $"Interactive mode error: {ex.Message}";
                    Console.WriteLine($"Error: {ex.Message}");
                    await mainErrorLoggingService.LogErrorAsync(errorMessage, ex, $"Interactive command: {input}");
                }
            }
        }
        finally
        {
            server?.Dispose();
        }
    }

    internal static OperationMode ParseOperationMode(string modeString)
    {
        if (string.IsNullOrEmpty(modeString))
            return OperationMode.Hybrid;
            
        return modeString.ToLower() switch
        {
            "mcp" => OperationMode.MCP,
            "rag" => OperationMode.RAG,
            "hybrid" => OperationMode.Hybrid,
            _ => OperationMode.Hybrid
        };
    }

    private sealed record SetupResult(string Directory, string Model, OperationMode Mode);

    private static async Task<SetupResult?> InteractiveSetupAsync(ILogger logger)
    {
        Console.WriteLine("🎯 HlpAI - Interactive Setup");
        Console.WriteLine("=============================================");
        Console.WriteLine();
        Console.WriteLine("Welcome! Let's configure your document intelligence system.");
        Console.WriteLine();
        
        // Initialize error logging service for interactive mode
        using var errorLoggingService = new ErrorLoggingService(logger);
        
        // Load saved configuration
        var config = ConfigurationService.LoadConfiguration(logger);

        // Step 1: Directory Selection
        Console.WriteLine("📁 Step 1: Document Directory");
        Console.WriteLine("------------------------------");
        
        string? directory = null;
        
        // Show last directory if available and remember setting is enabled
        if (config.RememberLastDirectory && !string.IsNullOrEmpty(config.LastDirectory))
        {
            Console.WriteLine($"💾 Last used directory: {config.LastDirectory}");
            if (Directory.Exists(config.LastDirectory))
            {
                using var lastDirPromptService = new PromptService();
                var useLastDir = await lastDirPromptService.PromptYesNoDefaultYesAsync($"Use last directory '{config.LastDirectory}'?");
                
                if (useLastDir)
                {
                    Console.WriteLine($"✅ Using directory: {config.LastDirectory}");
                    directory = config.LastDirectory;
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine($"⚠️  Last directory no longer exists: {config.LastDirectory}");
            }
            Console.WriteLine();
        }
        
        while (directory == null)
        {
            var input = SafePromptForString("Enter the path to your documents directory", "").Trim();
            
            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("❌ Directory path cannot be empty. Please try again.");
                continue;
            }
            
            if (input.Equals("quit", StringComparison.CurrentCultureIgnoreCase) || input.Equals("exit", StringComparison.CurrentCultureIgnoreCase))
            {
                return null;
            }
            
            if (!Directory.Exists(input))
            {
                Console.WriteLine($"❌ Directory '{input}' does not exist.");
                using var createDirPromptService = new PromptService();
                var createResponse = await createDirPromptService.PromptYesNoDefaultYesAsync("Would you like to create it?");
                
                if (createResponse)
                {
                    try
                    {
                        Directory.CreateDirectory(input);
                        Console.WriteLine($"✅ Created directory: {input}");
                        directory = input;
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Failed to create directory: {ex.Message}";
                        Console.WriteLine($"❌ {errorMessage}");
                        await errorLoggingService.LogErrorAsync(errorMessage, ex, "Interactive setup - directory creation");
                    }
                }
                else
                {
                    Console.WriteLine("Please enter a valid existing directory path, or type 'quit' to exit.");
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
        var model = await SelectModelAsync(logger, config);
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
        
        OperationMode selectedMode = config.RememberLastOperationMode ? config.LastOperationMode : OperationMode.Hybrid;
        
        // Show last operation mode if available
        bool skipModeSelection = false;
        if (config.RememberLastOperationMode)
        {
            Console.WriteLine($"💾 Last used operation mode: {config.LastOperationMode}");
            using var modePromptService = new PromptService();
            var useLastMode = await modePromptService.PromptYesNoDefaultYesAsync("Use last operation mode?");
            
            if (!useLastMode)
            {
                selectedMode = OperationMode.Hybrid; // Reset to default for new selection
            }
            else
            {
                Console.WriteLine($"✅ Using operation mode: {selectedMode}");
                skipModeSelection = true;
            }
        }
        
        if (!skipModeSelection)
        {
            while (true)
            {
                Console.Write("Select operation mode (1-3, default: 1): ");
                var modeInput = SafePromptForString("", "1").Trim();
                
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
                else if (modeInput.Equals("quit", StringComparison.CurrentCultureIgnoreCase) || modeInput.Equals("exit", StringComparison.CurrentCultureIgnoreCase))
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
        }
        // Summary
        Console.WriteLine("📋 Configuration Summary");
        Console.WriteLine("========================");
        Console.WriteLine($"Directory: {directory}");
        Console.WriteLine($"Model: {model}");
        Console.WriteLine($"Mode: {selectedMode}");
        Console.WriteLine();
        using var promptService = new PromptService();
        var confirmResponse = await promptService.PromptYesNoDefaultYesAsync("Continue with this configuration?");
        
        if (!confirmResponse)
        {
            Console.WriteLine("❌ Configuration cancelled.");
            return null;
        }

        Console.WriteLine("✅ Starting application with selected configuration...");
        Console.WriteLine();

        // Save the configuration for next time
        if (config.RememberLastDirectory)
            config.LastDirectory = directory;
        if (config.RememberLastModel)
            config.LastModel = model;
        if (config.RememberLastOperationMode)
            config.LastOperationMode = selectedMode;
            
        ConfigurationService.SaveConfiguration(config, logger);

        return new SetupResult(directory, model, selectedMode);
    }

    private static async Task<string> SelectModelAsync(ILogger logger, AppConfiguration? config = null)
    {
        Console.WriteLine("🤖 Model Selection");
        Console.WriteLine("==================");
        
        // Show last model if available and remember setting is enabled
        if (config?.RememberLastModel == true && !string.IsNullOrEmpty(config.LastModel))
        {
            Console.WriteLine($"💾 Last used model: {config.LastModel}");
            using var promptService = new PromptService();
            var useLastModel = await promptService.PromptYesNoDefaultYesAsync("Use last model?");
            
            if (useLastModel)
            {
                Console.WriteLine($"✅ Using model: {config.LastModel}");
                return config.LastModel;
            }
            Console.WriteLine();
        }
        
        // Create a temporary client to check Ollama availability and get models
        using var tempClient = new OllamaClient(logger: logger);
        
        if (!await tempClient.IsAvailableAsync())
        {
            Console.WriteLine("❌ Ollama is not available. Please ensure Ollama is running on localhost:11434");
            Console.WriteLine("   Install Ollama: https://ollama.ai");
            Console.WriteLine();
            using var promptService = new PromptService();
            var continueWithDefault = await promptService.PromptYesNoDefaultYesAsync("Would you like to continue with the default model anyway?");
            return continueWithDefault ? "llama3.2" : "";
        }

        var availableModels = await tempClient.GetModelsAsync();
        
        if (availableModels.Count == 0)
        {
            Console.WriteLine("❌ No models found in Ollama.");
            Console.WriteLine("   Install a model first: ollama pull llama3.2");
            Console.WriteLine();
            using var promptService = new PromptService();
            var continueWithDefault = await promptService.PromptYesNoDefaultYesAsync("Would you like to continue with 'llama3.2' anyway?");
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
            var input = SafePromptForString("", "b").Trim();
            
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
                    var customModel = SafePromptForString("Enter custom model name: ", "").Trim();
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
        
        // Extract resources for export functionality
        var resources = new List<ResourceInfo>();
        if (response.Result != null)
        {
            try
            {
                if (response.Result is ResourcesListResponse resourcesResponse)
                {
                    resources = resourcesResponse.Resources;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing resources for export: {ex.Message}");
            }
        }

        if (resources.Count > 0)
        {
            Console.WriteLine($"\nFound {resources.Count} files. Would you like to export this list?");
            Console.WriteLine("1. Export to CSV");
            Console.WriteLine("2. Export to JSON");
            Console.WriteLine("3. Export to TXT");
            Console.WriteLine("4. Export to XML");
            Console.WriteLine("5. Skip export");
            Console.WriteLine();
            
            Console.Write("Select option (1-5): ");
            var choice = SafePromptForString("", "b").Trim();
            
            if (!string.IsNullOrEmpty(choice) && choice != "5")
            {
                await HandleFileExportMenuChoice(choice, resources);
            }
        }
    }

    private static async Task HandleFileExportMenuChoice(string choice, List<ResourceInfo> resources)
    {
        try
        {
            using var exportService = new FileListExportService();
            
            var format = choice switch
            {
                "1" => FileExportFormat.Csv,
                "2" => FileExportFormat.Json,
                "3" => FileExportFormat.Txt,
                "4" => FileExportFormat.Xml,
                _ => FileExportFormat.Csv
            };

            Console.Write($"Enter output file name (default: file_list_{DateTime.Now:yyyyMMdd_HHmmss}.{format.ToString().ToLower()}): ");
            var fileName = SafePromptForString("", "export.json").Trim();
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"file_list_{DateTime.Now:yyyyMMdd_HHmmss}.{format.ToString().ToLower()}";
            }

            Console.Write("Include file metadata? (y/N): ");
            var metadataChoice = SafePromptForString("", "y").Trim().ToLower();
            var includeMetadata = metadataChoice == "y" || metadataChoice == "yes";

            Console.WriteLine($"\n🔄 Exporting {resources.Count} files to {format} format...");
            
            var result = await exportService.ExportFileListAsync(resources, format, fileName, includeMetadata);
            
            if (result.Success)
            {
                Console.WriteLine($"✅ Successfully exported {result.ExportedCount} files");
                Console.WriteLine($"📁 Output file: {result.OutputPath}");
                Console.WriteLine($"📏 File size: {result.FileSizeBytes:N0} bytes");
                Console.WriteLine($"🕐 Exported at: {result.ExportedAt:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                Console.WriteLine($"❌ Export failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during export: {ex.Message}");
        }
    }

    private static async Task DemoReadFile(EnhancedMcpRagServer server)
    {
        var uri = SafePromptForString("Enter file URI (e.g., file:///example.txt)", "");

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
        var query = SafePromptForString("Enter search query", "");

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
        var question = SafePromptForString("", "What is this document about?");

        if (string.IsNullOrEmpty(question))
        {
            Console.WriteLine("Question cannot be empty.");
            return;
        }

        Console.Write("Enter additional context (optional, press Enter to skip): ");
        var context = SafePromptForString("", "");

        Console.Write("Enter temperature (0.0-1.0, default 0.7): ");
        var tempInput = SafePromptForString("", "0.7");
        double temperature = 0.7;
        if (!string.IsNullOrEmpty(tempInput) && double.TryParse(tempInput, out var temp))
        {
            temperature = Math.Max(0.0, Math.Min(1.0, temp));
        }

        using var promptService = new PromptService();
        var useRag = await promptService.PromptYesNoDefaultYesAsync("Use RAG enhancement?");

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
        var uri = SafePromptForString("", "");

        if (string.IsNullOrEmpty(uri))
        {
            Console.WriteLine("URI cannot be empty.");
            return;
        }

        Console.WriteLine("Available analysis types: summary, key_points, questions, topics, technical, explanation");
        Console.Write("Enter analysis type (default: summary): ");
        var analysisType = SafePromptForString("", "summary");

        if (string.IsNullOrEmpty(analysisType))
        {
            analysisType = "summary";
        }

        Console.Write("Enter temperature (0.0-1.0, default 0.7): ");
        var tempInput = SafePromptForString("", "0.7");
        double temperature = 0.7;
        if (!string.IsNullOrEmpty(tempInput) && double.TryParse(tempInput, out var temp))
        {
            temperature = Math.Max(0.0, Math.Min(1.0, temp));
        }

        using var promptService = new PromptService();
        var useRag = await promptService.PromptYesNoDefaultYesAsync("Use RAG enhancement?");

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
        var query = SafePromptForString("", "search query");

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
        var question = SafePromptForString("", "What is the main topic?");

        if (string.IsNullOrEmpty(question))
        {
            Console.WriteLine("Question cannot be empty.");
            return;
        }

        Console.Write("Enter temperature (0.0-1.0, default 0.7): ");
        var tempInput = SafePromptForString("", "0.7");
        double temperature = 0.7;
        if (!string.IsNullOrEmpty(tempInput) && double.TryParse(tempInput, out var temp))
        {
            temperature = Math.Max(0.0, Math.Min(1.0, temp));
        }

        Console.Write("Number of context chunks to retrieve (default 5): ");
        var topKInput = SafePromptForString("", "5");
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
        
        // When user chooses reindex from menu, automatically use force: true
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = new { name = "reindex_documents", arguments = new { force = true } }
        };

        var response = await server.HandleRequestAsync(request);
        Console.WriteLine("\nReindexing Results:");
        DisplayResponse(response, "Reindexing Results");
    }

    private static async Task DemoShowModels(EnhancedMcpRagServer server)
    {
        if (await server._aiProvider.IsAvailableAsync())
        {
            var models = await server._aiProvider.GetModelsAsync();
            Console.WriteLine($"\nAvailable {server._aiProvider.ProviderName} models: {string.Join(", ", models)}");
        }
        else
        {
            Console.WriteLine($"\n{server._aiProvider.ProviderName} is not available. Cannot retrieve models.");
        }
    }

    private static async Task DemoShowStatus(EnhancedMcpRagServer server)
    {
        Console.WriteLine("\n=== System Status ===");
        Console.WriteLine($"Operation Mode: {server._operationMode}");
        Console.WriteLine($"{server._aiProvider.ProviderName} Available: {(await server._aiProvider.IsAvailableAsync() ? "✅ Yes" : "❌ No")}");
        
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

    private static async Task ShowConfigurationMenuAsync()
    {
        using var sqliteConfig = new SqliteConfigurationService();
        using var hhExeService = new HhExeDetectionService();
        var config = ConfigurationService.LoadConfiguration();
        bool configRunning = true;
        
        while (configRunning)
        {
            ClearScreen();
            // Get current hh.exe configuration from SQLite
            var configuredHhPath = await hhExeService.GetConfiguredHhExePathAsync();
            var isAutoDetected = await hhExeService.IsHhExePathAutoDetectedAsync();
            
            Console.WriteLine("\n⚙️ Configuration Settings");
            Console.WriteLine("==========================");
            Console.WriteLine($"1. Remember last directory: {(config.RememberLastDirectory ? "✅ Enabled" : "❌ Disabled")}");
            if (!string.IsNullOrEmpty(config.LastDirectory))
            {
                Console.WriteLine($"   Last directory: {config.LastDirectory}");
            }
            Console.WriteLine($"2. Remember last model: {(config.RememberLastModel ? "✅ Enabled" : "❌ Disabled")}");
            if (!string.IsNullOrEmpty(config.LastModel))
            {
                Console.WriteLine($"   Last model: {config.LastModel}");
            }
            Console.WriteLine($"3. Remember last operation mode: {(config.RememberLastOperationMode ? "✅ Enabled" : "❌ Disabled")}");
            Console.WriteLine($"   Last operation mode: {config.LastOperationMode}");
            Console.WriteLine();
            Console.WriteLine("🔧 hh.exe Configuration (SQLite Database):");
            if (!string.IsNullOrEmpty(configuredHhPath))
            {
                Console.WriteLine($"   Current path: {configuredHhPath}");
                Console.WriteLine($"   Auto-detected: {(isAutoDetected ? "✅ Yes" : "❌ No (Manually set)")}");
                var pathExists = File.Exists(configuredHhPath);
                Console.WriteLine($"   Path valid: {(pathExists ? "✅ Yes" : "❌ No")}");
            }
            else
            {
                Console.WriteLine($"   Current path: ❌ Not configured");
            }
            Console.WriteLine("4. Configure hh.exe path");
            Console.WriteLine("5. Configure prompt defaults");
            Console.WriteLine("6. Configure error logging");
            Console.WriteLine();
            Console.WriteLine("7. View configuration database details");
            Console.WriteLine("8. Reset all settings to defaults");
            Console.WriteLine("9. Delete configuration database");
            Console.WriteLine("10. Change AI model");
            Console.WriteLine("b - Back to main menu");
            Console.WriteLine();
            
            Console.Write("Select option (1-10, b): ");
            var input = SafePromptForString("", "b").ToLower().Trim();
            
            switch (input)
            {
                case "1":
                    config.RememberLastDirectory = !config.RememberLastDirectory;
                    ConfigurationService.SaveConfiguration(config);
                    Console.WriteLine($"✅ Remember last directory: {(config.RememberLastDirectory ? "Enabled" : "Disabled")}");
                    break;
                    
                case "2":
                    config.RememberLastModel = !config.RememberLastModel;
                    ConfigurationService.SaveConfiguration(config);
                    Console.WriteLine($"✅ Remember last model: {(config.RememberLastModel ? "Enabled" : "Disabled")}");
                    break;
                    
                case "3":
                    config.RememberLastOperationMode = !config.RememberLastOperationMode;
                    ConfigurationService.SaveConfiguration(config);
                    Console.WriteLine($"✅ Remember last operation mode: {(config.RememberLastOperationMode ? "Enabled" : "Disabled")}");
                    break;
                    
                case "4":
                    await ConfigureHhExePathAsync(hhExeService);
                    break;
                    
                case "5":
                    await ConfigurePromptDefaultsAsync();
                    break;
                    
                case "6":
                    await ConfigureErrorLoggingAsync();
                    break;
                    
                case "7":
                    await ShowConfigurationDatabaseDetailsAsync(sqliteConfig);
                    break;
                    
                case "8":
                    {
                        Console.WriteLine("\n🔄 Reset Settings");
                        Console.WriteLine("==================");
                        using var resetPromptService = new PromptService();
                        var resetConfirm = await resetPromptService.PromptYesNoDefaultNoAsync("Are you sure you want to reset all settings to defaults?");
                        if (resetConfirm)
                        {
                            // Reset JSON config
                            config = new AppConfiguration();
                            ConfigurationService.SaveConfiguration(config);
                            
                            // Reset SQLite config
                            await sqliteConfig.ClearCategoryAsync("system");
                            await sqliteConfig.ClearCategoryAsync("application");
                            await sqliteConfig.ClearCategoryAsync("logging");
                            await sqliteConfig.ClearCategoryAsync("error_logs");
                            
                            Console.WriteLine("✅ All settings have been reset to defaults.");
                        }
                        else
                        {
                            Console.WriteLine("❌ Reset cancelled.");
                        }
                        break;
                    }
                    
                case "9":
                    {
                        Console.WriteLine("\n🗑️ Delete Configuration Database");
                        Console.WriteLine("=================================");
                        using var deletePromptService = new PromptService();
                        var deleteConfirm = await deletePromptService.PromptYesNoDefaultNoAsync("Are you sure you want to delete the configuration database?");
                        if (deleteConfirm)
                        {
                            try
                            {
                                var dbPath = sqliteConfig.DatabasePath;
                                sqliteConfig.Dispose();
                                hhExeService.Dispose();
                                
                                if (File.Exists(dbPath))
                                {
                                    File.Delete(dbPath);
                                    Console.WriteLine($"✅ Configuration database deleted: {dbPath}");
                                }
                                else
                                {
                                    Console.WriteLine("ℹ️ Configuration database does not exist.");
                                }
                                
                                // Recreate services for continued use
                                using var newSqliteConfig = new SqliteConfigurationService();
                                using var newHhExeService = new HhExeDetectionService();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Error deleting configuration database: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("❌ Delete cancelled.");
                        }
                        break;
                    }
                    
                case "10":
                    await ChangeAiModelAsync(sqliteConfig);
                    break;
                    
                case "b":
                case "back":
                    configRunning = false;
                    break;
                    
                default:
                    Console.WriteLine("❌ Invalid option. Please try again.");
                    break;
            }
            
            if (configRunning)
            {
                await Task.Delay(1500); // Brief pause to let user see the result
            }
        }
    }

    private static async Task ChangeAiModelAsync(SqliteConfigurationService sqliteConfig)
    {
        Console.WriteLine("\n🤖 Change AI Model");
        Console.WriteLine("==================");
        
        var config = ConfigurationService.LoadConfiguration();
        
        // Get current AI provider configuration from SQLite
        var currentConfig = await sqliteConfig.GetAiProviderConfigurationAsync();
        if (currentConfig.HasValue)
        {
            Console.WriteLine($"Current Provider: {currentConfig.Value.ProviderType}");
            Console.WriteLine($"Current Model: {currentConfig.Value.Model}");
        }
        else
        {
            Console.WriteLine($"Current Provider: {config.LastProvider}");
            Console.WriteLine($"Current Model: {config.LastModel ?? "Not set"}");
        }
        
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("1. List available models for current provider");
        Console.WriteLine("2. Select a different model");
        Console.WriteLine("3. Change AI provider (opens AI provider menu)");
        Console.WriteLine("b. Back to configuration menu");
        Console.WriteLine();
        
        Console.Write("Select option (1-3, b): ");
        var choice = SafePromptForString("", "b").ToLower().Trim();
        
        switch (choice)
        {
            case "1":
                await ListAvailableModelsAsync(config);
                break;
                
            case "2":
                await SelectModelFromConfigMenuAsync(config);
                break;
                
            case "3":
                await ShowAiProviderMenuAsync();
                break;
                
            case "b":
            case "back":
                break;
                
            default:
                Console.WriteLine("❌ Invalid option. Please try again.");
                break;
        }
        
        if (choice != "b" && choice != "back")
        {
            await Task.Delay(2000); // Brief pause to let user see the result
        }
    }

    private static async Task ListAvailableModelsAsync(AppConfiguration config)
    {
        Console.WriteLine("\n📋 Available Models");
        Console.WriteLine("==================");
        
        try
        {
            var provider = AiProviderFactory.CreateProvider(
                config.LastProvider,
                config.LastModel ?? "default",
                GetProviderUrl(config, config.LastProvider)
            );
            
            Console.WriteLine($"Checking models for {provider.ProviderName}...");
            
            var isAvailable = await provider.IsAvailableAsync();
            if (!isAvailable)
            {
                Console.WriteLine($"❌ {provider.ProviderName} is not available at {provider.BaseUrl}");
                Console.WriteLine("Make sure the provider is running and accessible.");
                provider.Dispose();
                return;
            }
            
            var models = await provider.GetModelsAsync();
            if (models.Count > 0)
            {
                Console.WriteLine($"\n✅ Found {models.Count} models:");
                for (int i = 0; i < models.Count; i++)
                {
                    var isCurrent = models[i] == config.LastModel;
                    var status = isCurrent ? " ✅ (Current)" : "";
                    Console.WriteLine($"{i + 1}. {models[i]}{status}");
                }
            }
            else
            {
                Console.WriteLine("⚠️ No models found (provider may be running but no models loaded)");
            }
            
            provider.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error listing models: {ex.Message}");
        }
    }

    private static async Task SelectModelFromConfigMenuAsync(AppConfiguration config)
    {
        Console.WriteLine("\n🎯 Select Model");
        Console.WriteLine("===============");
        
        try
        {
            var provider = AiProviderFactory.CreateProvider(
                config.LastProvider,
                config.LastModel ?? "default",
                GetProviderUrl(config, config.LastProvider)
            );
            
            Console.WriteLine($"Getting models from {provider.ProviderName}...");
            
            var isAvailable = await provider.IsAvailableAsync();
            if (!isAvailable)
            {
                Console.WriteLine($"❌ {provider.ProviderName} is not available at {provider.BaseUrl}");
                Console.WriteLine("Make sure the provider is running and accessible.");
                provider.Dispose();
                return;
            }
            
            var models = await provider.GetModelsAsync();
            if (models.Count == 0)
            {
                Console.WriteLine("⚠️ No models found. You can still enter a model name manually.");
                Console.Write("Enter model name: ");
                var manualModel = SafePromptForString("", "").Trim();
                if (!string.IsNullOrEmpty(manualModel))
                {
                    UpdateModelConfiguration(config, manualModel);
                }
                provider.Dispose();
                return;
            }
            
            Console.WriteLine($"\nAvailable models:");
            for (int i = 0; i < models.Count; i++)
            {
                var isCurrent = models[i] == config.LastModel;
                var status = isCurrent ? " ✅ (Current)" : "";
                Console.WriteLine($"{i + 1}. {models[i]}{status}");
            }
            
            Console.WriteLine($"{models.Count + 1}. Enter custom model name");
            Console.WriteLine();
            
            var input = SafePromptForString($"Select model (1-{models.Count + 1}): ", "1").Trim();
            
            if (int.TryParse(input, out int selection))
            {
                if (selection >= 1 && selection <= models.Count)
                {
                    var selectedModel = models[selection - 1];
                    UpdateModelConfiguration(config, selectedModel);
                }
                else if (selection == models.Count + 1)
                {
                    Console.Write("Enter custom model name: ");
                    var customModel = SafePromptForString("", "").Trim();
                    if (!string.IsNullOrEmpty(customModel))
                    {
                        UpdateModelConfiguration(config, customModel);
                    }
                }
                else
                {
                    Console.WriteLine("❌ Invalid selection.");
                }
            }
            else
            {
                Console.WriteLine("❌ Invalid input. Please enter a number.");
            }
            
            provider.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error selecting model: {ex.Message}");
        }
    }

    private static void UpdateModelConfiguration(AppConfiguration config, string newModel)
    {
        try
        {
            // Update both JSON and SQLite configuration
            var success = ConfigurationService.UpdateAiProviderConfiguration(config.LastProvider, newModel);
            
            if (success)
            {
                Console.WriteLine($"✅ Model updated successfully: {newModel}");
                Console.WriteLine($"✅ Configuration saved to both JSON and SQLite database");
            }
            else
            {
                Console.WriteLine($"❌ Failed to update model configuration");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error updating model configuration: {ex.Message}");
        }
    }

    private static async Task ConfigureHhExePathAsync(HhExeDetectionService hhExeService)
    {
        Console.WriteLine("\n🔧 Configure hh.exe Path (SQLite Database)");
        Console.WriteLine("===========================================");
        
        // Show current status
        var currentPath = await hhExeService.GetConfiguredHhExePathAsync();
        var isAutoDetected = await hhExeService.IsHhExePathAutoDetectedAsync();
        
        if (!string.IsNullOrEmpty(currentPath))
        {
            Console.WriteLine($"Current configured path: {currentPath}");
            Console.WriteLine($"Auto-detected: {(isAutoDetected ? "✅ Yes" : "❌ No (Manually set)")}");
            var isValid = File.Exists(currentPath);
            Console.WriteLine($"Path valid: {(isValid ? "✅ Yes" : "❌ No")}");
        }
        else
        {
            Console.WriteLine("Current configured path: ❌ Not set");
        }
        
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("1. Auto-detect hh.exe location");
        Console.WriteLine("2. Enter custom path");
        Console.WriteLine("3. Clear configured path");
        Console.WriteLine("4. Test current path");
        Console.WriteLine("5. View detection history");
        Console.WriteLine("b. Back to configuration menu");
        Console.WriteLine();
        
        Console.Write("Select option (1-5, b): ");
        var choice = SafePromptForString("", "b").ToLower().Trim();
        
        switch (choice)
        {
            case "1":
                Console.WriteLine("\n🔍 Auto-detecting hh.exe...");
                var found = await hhExeService.CheckDefaultLocationAsync();
                if (found)
                {
                    var detectedPath = await hhExeService.GetConfiguredHhExePathAsync();
                    Console.WriteLine($"✅ Found and configured hh.exe at: {detectedPath}");
                }
                else
                {
                    Console.WriteLine("❌ hh.exe not found at default location (C:\\Windows\\hh.exe).");
                    Console.WriteLine("Please ensure HTML Help Workshop is installed or try option 2 to enter a custom path.");
                }
                break;
                
            case "2":
                Console.WriteLine("\n📝 Enter Custom Path");
                Console.WriteLine("====================");
                Console.Write("Enter the full path to hh.exe: ");
                var customPath = SafePromptForString("", "").Trim();
                
                if (!string.IsNullOrEmpty(customPath))
                {
                    if (File.Exists(customPath) && customPath.EndsWith("hh.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        await hhExeService.SetHhExePathAsync(customPath, false);
                        Console.WriteLine("✅ Valid path saved to configuration database.");
                    }
                    else
                    {
                        Console.WriteLine("❌ Invalid path or file not found.");
                        using var savePromptService = new PromptService();
                        var saveAnyway = await savePromptService.PromptYesNoDefaultNoAsync("Save anyway?");
                        if (saveAnyway)
                        {
                            await hhExeService.SetHhExePathAsync(customPath, false);
                            Console.WriteLine("⚠️  Path saved (validation failed).");
                        }
                    }
                }
                break;
                
            case "3":
                Console.WriteLine("\n🗑️ Clearing configured path...");
                await hhExeService.SetHhExePathAsync(null, false);
                Console.WriteLine("✅ Configured path cleared from database.");
                break;
                
            case "4":
                Console.WriteLine("\n🧪 Testing current path...");
                var testPath = await hhExeService.GetConfiguredHhExePathAsync();
                if (string.IsNullOrEmpty(testPath))
                {
                    Console.WriteLine("❌ No path configured to test.");
                    break;
                }
                
                try
                {
                    Console.WriteLine($"Testing path: {testPath}");
                    
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = testPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var process = Process.Start(processInfo);
                    if (process != null)
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        try
                        {
                            await process.WaitForExitAsync(cts.Token);
                            Console.WriteLine("✅ hh.exe responded successfully.");
                        }
                        catch (OperationCanceledException)
                        {
                            process.Kill();
                            Console.WriteLine("⚠️  hh.exe did not respond within 5 seconds.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ Could not start hh.exe process.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error testing hh.exe: {ex.Message}");
                }
                break;
                
            case "5":
                await ShowHhExeDetectionHistoryAsync(hhExeService);
                break;
                
            case "b":
            case "back":
                return;
                
            default:
                Console.WriteLine("❌ Invalid option. Please try again.");
                break;
        }
        
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    private static async Task ShowConfigurationDatabaseDetailsAsync(SqliteConfigurationService configService)
    {
        Console.WriteLine("\n📄 Configuration Database Details");
        Console.WriteLine("==================================");
        
        var stats = await configService.GetStatsAsync();
        Console.WriteLine($"Database Path: {stats.DatabasePath}");
        Console.WriteLine($"Total Configuration Items: {stats.TotalItems}");
        Console.WriteLine($"Total Categories: {stats.TotalCategories}");
        Console.WriteLine($"Last Update: {stats.LastUpdate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never"}");
        Console.WriteLine();
        
        // Show configurations by category
        var systemConfig = await configService.GetCategoryConfigurationAsync("system");
        var appConfig = await configService.GetCategoryConfigurationAsync("application");
        
        if (systemConfig.Count != 0)
        {
            Console.WriteLine("System Configuration:");
            foreach (var kvp in systemConfig)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value ?? "null"}");
            }
            Console.WriteLine();
        }
        
        if (appConfig.Count != 0)
        {
            Console.WriteLine("Application Configuration:");
            foreach (var kvp in appConfig)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value ?? "null"}");
            }
            Console.WriteLine();
        }
        
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    private static async Task ShowHhExeDetectionHistoryAsync(HhExeDetectionService hhExeService)
    {
        Console.WriteLine("\n📜 hh.exe Detection History");
        Console.WriteLine("============================");
        
        var history = await hhExeService.GetDetectionHistoryAsync();
        
        if (history.Count == 0)
        {
            Console.WriteLine("No detection attempts recorded.");
        }
        else
        {
            Console.WriteLine($"Showing {Math.Min(history.Count, 10)} most recent detection attempts:");
            Console.WriteLine();
            
            var recentHistory = history.Take(10);
            foreach (var entry in recentHistory)
            {
                var status = entry.Found ? "✅ Found" : "❌ Not Found";
                Console.WriteLine($"{entry.DetectedAt:yyyy-MM-dd HH:mm:ss} - {status}");
                Console.WriteLine($"  Path: {entry.Path}");
                if (!string.IsNullOrEmpty(entry.Notes))
                {
                    Console.WriteLine($"  Notes: {entry.Notes}");
                }
                Console.WriteLine();
            }
            
            if (history.Count > 10)
            {
                Console.WriteLine($"... and {history.Count - 10} more entries in database");
            }
        }
        
        var lastSuccessful = await hhExeService.GetLastSuccessfulDetectionAsync();
        if (lastSuccessful != null)
        {
            Console.WriteLine("Last Successful Detection:");
            Console.WriteLine($"  Path: {lastSuccessful.Path}");
            Console.WriteLine($"  Date: {lastSuccessful.DetectedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  Notes: {lastSuccessful.Notes}");
        }
        else
        {
            Console.WriteLine("No successful detections recorded.");
        }
        
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    private static async Task ConfigurePromptDefaultsAsync()
    {
        using var promptService = new PromptService();
        
        Console.WriteLine("\n🎯 Configure Prompt Defaults");
        Console.WriteLine("=============================");
        
        await promptService.ShowPromptConfigurationAsync();
        
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("1. Always default to 'Yes' when Enter is pressed");
        Console.WriteLine("2. Always default to 'No' when Enter is pressed");
        Console.WriteLine("3. Use individual prompt defaults (recommended)");
        Console.WriteLine("4. Test current prompt behavior");
        Console.WriteLine("b. Back to configuration menu");
        Console.WriteLine();
        
        Console.Write("Select option (1-4, b): ");
        var choice = SafePromptForString("", "b").ToLower().Trim();
        
        switch (choice)
        {
            case "1":
                await promptService.SetDefaultPromptBehaviorAsync(true);
                Console.WriteLine("✅ Configured to default to 'Yes' for all prompts.");
                break;
                
            case "2":
                await promptService.SetDefaultPromptBehaviorAsync(false);
                Console.WriteLine("✅ Configured to default to 'No' for all prompts.");
                break;
                
            case "3":
                await promptService.SetDefaultPromptBehaviorAsync(null);
                Console.WriteLine("✅ Configured to use individual prompt defaults.");
                break;
                
            case "4":
                Console.WriteLine("\n🧪 Testing Prompt Behavior");
                Console.WriteLine("==========================");
                
                // Test different types of prompts
                var testResult1 = await promptService.PromptYesNoAsync("Continue with operation?", true);
                Console.WriteLine($"Result 1 (default yes): {testResult1}");
                
                var testResult2 = await promptService.PromptYesNoAsync("Delete all data?", false);
                Console.WriteLine($"Result 2 (default no): {testResult2}");
                
                Console.WriteLine("Test completed.");
                break;
                
            case "b":
            case "back":
                return;
                
            default:
                Console.WriteLine("❌ Invalid option. Please try again.");
                await Task.Delay(1000);
                await ConfigurePromptDefaultsAsync();
                return;
        }
        
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    private static async Task ConfigureErrorLoggingAsync()
    {
        using var errorLoggingService = new ErrorLoggingService();
        
        Console.WriteLine("\n📊 Configure Error Logging");
        Console.WriteLine("===========================");
        
        // Display current configuration
        var isEnabled = await errorLoggingService.IsLoggingEnabledAsync();
        var logLevel = await errorLoggingService.GetMinimumLogLevelAsync();
        var retentionDays = await errorLoggingService.GetLogRetentionDaysAsync();
        var stats = await errorLoggingService.GetLogStatisticsAsync();
        
        Console.WriteLine("Current Configuration:");
        Console.WriteLine($"  Logging enabled: {(isEnabled ? "✅ Yes" : "❌ No")}");
        Console.WriteLine($"  Minimum log level: {logLevel}");
        Console.WriteLine($"  Log retention: {retentionDays} days");
        Console.WriteLine($"  Total logs: {stats.TotalLogs}");
        Console.WriteLine($"  Errors (24h): {stats.ErrorsLast24Hours}");
        Console.WriteLine($"  Warnings (24h): {stats.WarningsLast24Hours}");
        Console.WriteLine();
        
        Console.WriteLine("Configuration Options:");
        Console.WriteLine($"1. {(isEnabled ? "Disable" : "Enable")} error logging");
        Console.WriteLine("2. Set minimum log level");
        Console.WriteLine("3. Set log retention period");
        Console.WriteLine("4. View recent error logs");
        Console.WriteLine("5. View detailed log statistics");
        Console.WriteLine("6. Clear all error logs");
        Console.WriteLine("7. Test error logging");
        Console.WriteLine("b. Back to configuration menu");
        Console.WriteLine();
        
        var choice = SafePromptForString("Select option (1-7, b): ", "b").ToLower().Trim();
        
        switch (choice)
        {
            case "1":
                var newState = !isEnabled;
                await errorLoggingService.SetLoggingEnabledAsync(newState);
                Console.WriteLine($"✅ Error logging {(newState ? "enabled" : "disabled")}.");
                break;
                
            case "2":
                await ConfigureLogLevelAsync(errorLoggingService);
                break;
                
            case "3":
                await ConfigureLogRetentionAsync(errorLoggingService);
                break;
                
            case "4":
                await ViewRecentLogsAsync(errorLoggingService);
                break;
                
            case "5":
                await ViewDetailedLogStatisticsAsync(errorLoggingService);
                break;
                
            case "6":
                await ClearErrorLogsAsync(errorLoggingService);
                break;
                
            case "7":
                await TestErrorLoggingAsync(errorLoggingService);
                break;
                
            case "b":
            case "back":
                return;
                
            default:
                Console.WriteLine("❌ Invalid option. Please try again.");
                await Task.Delay(1000);
                await ConfigureErrorLoggingAsync();
                return;
        }
        
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    private static async Task ConfigureLogLevelAsync(ErrorLoggingService loggingService)
    {
        Console.WriteLine("\n📝 Set Minimum Log Level");
        Console.WriteLine("========================");
        Console.WriteLine("1. Error - Only log errors");
        Console.WriteLine("2. Warning - Log warnings and errors");
        Console.WriteLine("3. Information - Log all messages (most verbose)");
        Console.WriteLine();
        
        Console.Write("Select log level (1-3): ");
        var choice = SafePromptForString("", "b").Trim();
        
        LogLevel newLevel = choice switch
        {
            "1" => LogLevel.Error,
            "2" => LogLevel.Warning,
            "3" => LogLevel.Information,
            _ => LogLevel.Warning
        };
        
        if (choice is "1" or "2" or "3")
        {
            await loggingService.SetMinimumLogLevelAsync(newLevel);
            Console.WriteLine($"✅ Minimum log level set to {newLevel}.");
        }
        else
        {
            Console.WriteLine("❌ Invalid choice. Keeping current setting.");
        }
    }

    private static async Task ConfigureLogRetentionAsync(ErrorLoggingService loggingService)
    {
        Console.WriteLine("\n🗂️ Set Log Retention Period");
        Console.WriteLine("============================");
        Console.WriteLine("How many days should error logs be kept?");
        Console.WriteLine("(Older logs will be automatically deleted)");
        Console.WriteLine();
        
        var currentRetention = await loggingService.GetLogRetentionDaysAsync();
        Console.Write($"Enter retention days (current: {currentRetention}): ");
        var input = SafePromptForString("", "b").Trim();
        
        if (int.TryParse(input, out var days) && days > 0)
        {
            await loggingService.SetLogRetentionDaysAsync(days);
            Console.WriteLine($"✅ Log retention set to {days} days.");
        }
        else
        {
            Console.WriteLine("❌ Invalid input. Please enter a positive number.");
        }
    }

    private static async Task ViewRecentLogsAsync(ErrorLoggingService loggingService)
    {
        Console.WriteLine("\n📋 Recent Error Logs");
        Console.WriteLine("====================");
        Console.Write("How many recent logs to show (default 10)? ");
        var input = SafePromptForString("", "30").Trim();
        
        var count = 10;
        if (int.TryParse(input, out var parsedCount) && parsedCount > 0)
        {
            count = parsedCount;
        }
        
        var logs = await loggingService.GetRecentLogsAsync(count);
        
        if (logs.Count == 0)
        {
            Console.WriteLine("No error logs found.");
        }
        else
        {
            Console.WriteLine($"\nShowing {logs.Count} most recent logs:");
            Console.WriteLine("=====================================");
            
            foreach (var log in logs)
            {
                Console.WriteLine($"[{log.Timestamp:yyyy-MM-dd HH:mm:ss}] {log.LogLevel}: {log.Message}");
                if (!string.IsNullOrEmpty(log.Context))
                    Console.WriteLine($"  Context: {log.Context}");
                if (!string.IsNullOrEmpty(log.ExceptionType))
                    Console.WriteLine($"  Exception: {log.ExceptionType}");
                Console.WriteLine();
            }
        }
    }

    private static async Task ViewDetailedLogStatisticsAsync(ErrorLoggingService loggingService)
    {
        Console.WriteLine("\n📊 Detailed Log Statistics");
        Console.WriteLine("===========================");
        
        var stats = await loggingService.GetLogStatisticsAsync();
        
        Console.WriteLine($"Total logs: {stats.TotalLogs}");
        Console.WriteLine();
        Console.WriteLine("Last 24 hours:");
        Console.WriteLine($"  Errors: {stats.ErrorsLast24Hours}");
        Console.WriteLine($"  Warnings: {stats.WarningsLast24Hours}");
        Console.WriteLine();
        Console.WriteLine("Last 7 days:");
        Console.WriteLine($"  Errors: {stats.ErrorsLast7Days}");
        Console.WriteLine($"  Warnings: {stats.WarningsLast7Days}");
        Console.WriteLine();
        
        if (stats.OldestLogDate.HasValue)
            Console.WriteLine($"Oldest log: {stats.OldestLogDate:yyyy-MM-dd HH:mm:ss}");
        if (stats.NewestLogDate.HasValue)
            Console.WriteLine($"Newest log: {stats.NewestLogDate:yyyy-MM-dd HH:mm:ss}");
            
        // Get breakdown by log level
        var allLogs = await loggingService.GetRecentLogsAsync(1000);
        var errorCount = allLogs.Count(l => l.LogLevel == "Error");
        var warningCount = allLogs.Count(l => l.LogLevel == "Warning");
        var infoCount = allLogs.Count(l => l.LogLevel == "Information");
        
        Console.WriteLine();
        Console.WriteLine("Breakdown by level:");
        Console.WriteLine($"  Errors: {errorCount}");
        Console.WriteLine($"  Warnings: {warningCount}");
        Console.WriteLine($"  Information: {infoCount}");
    }

    private static async Task ClearErrorLogsAsync(ErrorLoggingService loggingService)
    {
        Console.WriteLine("\n🗑️ Clear All Error Logs");
        Console.WriteLine("========================");
        
        using var promptService = new PromptService();
        var confirm = await promptService.PromptYesNoDefaultNoAsync("Are you sure you want to delete all error logs? This cannot be undone.");
        
        if (confirm)
        {
            var success = await loggingService.ClearAllLogsAsync();
            if (success)
            {
                Console.WriteLine("✅ All error logs have been cleared.");
            }
            else
            {
                Console.WriteLine("❌ Failed to clear error logs.");
            }
        }
        else
        {
            Console.WriteLine("❌ Clear operation cancelled.");
        }
    }

    private static async Task TestErrorLoggingAsync(ErrorLoggingService loggingService)
    {
        Console.WriteLine("\n🧪 Test Error Logging");
        Console.WriteLine("=====================");
        
        Console.WriteLine("Creating test log entries...");
        
        await loggingService.LogInformationAsync("Test information message", "Menu system test");
        await loggingService.LogWarningAsync("Test warning message", "Menu system test");
        await loggingService.LogErrorAsync("Test error message", 
            new InvalidOperationException("Test exception"), 
            "Menu system test");
        
        Console.WriteLine("✅ Test log entries created successfully.");
        Console.WriteLine("You can view them using the 'View recent error logs' option.");
    }

    private static async Task ShowLogViewerAsync()
    {
        using var errorLoggingService = new ErrorLoggingService();
        
        // Get total log count for pagination
        var allLogs = await errorLoggingService.GetRecentLogsAsync(10000); // Get a large number to count total
        var totalLogs = allLogs.Count;
        
        if (totalLogs == 0)
        {
            ClearScreen();
            Console.WriteLine("📊 Error Log Viewer");
            Console.WriteLine("===================");
            Console.WriteLine("No error logs found.");
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
            return;
        }
        
        const int pageSize = 10;
        var totalPages = (int)Math.Ceiling((double)totalLogs / pageSize);
        var currentPage = 1;
        var filterLevel = "";
        var running = true;
        
        while (running)
        {
            ClearScreen();
            Console.WriteLine("📊 Error Log Viewer");
            Console.WriteLine("===================");
            
            // Display current filter and pagination info
            var filterText = string.IsNullOrEmpty(filterLevel) ? "All levels" : $"Level: {filterLevel}";
            Console.WriteLine($"Filter: {filterText} | Page {currentPage}/{totalPages} | Total logs: {totalLogs}");
            Console.WriteLine();
            
            // Get logs for current page
            var startIndex = (currentPage - 1) * pageSize;
            var logs = string.IsNullOrEmpty(filterLevel) 
                ? await errorLoggingService.GetRecentLogsAsync(totalLogs)
                : await errorLoggingService.GetRecentLogsAsync(totalLogs, Enum.Parse<LogLevel>(filterLevel));
            
            // Apply pagination
            var pagedLogs = logs.Skip(startIndex).Take(pageSize).ToList();
            
            if (pagedLogs.Count == 0)
            {
                Console.WriteLine("No logs found for the current filter and page.");
            }
            else
            {
                await DisplayLogPageAsync(pagedLogs, startIndex + 1);
            }
            
            Console.WriteLine();
            Console.WriteLine("Navigation:");
            Console.WriteLine("  n/next    - Next page");
            Console.WriteLine("  p/prev    - Previous page");
            Console.WriteLine("  f/filter  - Filter by log level");
            Console.WriteLine("  c/clear   - Clear filter");
            Console.WriteLine("  r/refresh - Refresh logs");
            Console.WriteLine("  s/stats   - Show statistics");
            Console.WriteLine("  d/detail  - View log details");
            Console.WriteLine("  q/quit    - Return to main menu");
            Console.WriteLine();
            
            Console.Write("Enter command: ");
            var input = SafePromptForString("", "b").ToLower().Trim();
            
            switch (input)
            {
                case "n":
                case "next":
                    if (currentPage < totalPages)
                        currentPage++;
                    else
                        Console.WriteLine("Already on last page.");
                    break;
                    
                case "p":
                case "prev":
                case "previous":
                    if (currentPage > 1)
                        currentPage--;
                    else
                        Console.WriteLine("Already on first page.");
                    break;
                    
                case "f":
                case "filter":
                    filterLevel = await GetLogLevelFilterAsync();
                    // Recalculate pagination with filter
                    var filteredLogs = string.IsNullOrEmpty(filterLevel) 
                        ? await errorLoggingService.GetRecentLogsAsync(10000)
                        : await errorLoggingService.GetRecentLogsAsync(10000, Enum.Parse<LogLevel>(filterLevel));
                    totalLogs = filteredLogs.Count;
                    totalPages = (int)Math.Ceiling((double)totalLogs / pageSize);
                    currentPage = 1; // Reset to first page
                    break;
                    
                case "c":
                case "clear":
                    filterLevel = "";
                    // Recalculate pagination without filter
                    allLogs = await errorLoggingService.GetRecentLogsAsync(10000);
                    totalLogs = allLogs.Count;
                    totalPages = (int)Math.Ceiling((double)totalLogs / pageSize);
                    currentPage = 1;
                    Console.WriteLine("Filter cleared.");
                    break;
                    
                case "r":
                case "refresh":
                    // Refresh log count
                    var refreshedLogs = string.IsNullOrEmpty(filterLevel) 
                        ? await errorLoggingService.GetRecentLogsAsync(10000)
                        : await errorLoggingService.GetRecentLogsAsync(10000, Enum.Parse<LogLevel>(filterLevel));
                    totalLogs = refreshedLogs.Count;
                    totalPages = (int)Math.Ceiling((double)totalLogs / pageSize);
                    Console.WriteLine("Logs refreshed.");
                    break;
                    
                case "s":
                case "stats":
                    await ShowLogViewerStatisticsAsync(errorLoggingService);
                    break;
                    
                case "d":
                case "detail":
                    await ShowLogDetailAsync(pagedLogs);
                    break;
                    
                case "q":
                case "quit":
                case "back":
                    running = false;
                    break;
                    
                default:
                    Console.WriteLine("Invalid command. Press any key to continue...");
                    Console.ReadKey(true);
                    break;
            }
            
            if (input != "s" && input != "stats" && input != "d" && input != "detail" && !string.IsNullOrEmpty(input) && input != "q" && input != "quit" && input != "back")
            {
                await Task.Delay(500); // Brief pause for user feedback
            }
        }
    }

    private static Task DisplayLogPageAsync(List<ErrorLogEntry> logs, int startIndex)
    {
        Console.WriteLine($"Showing logs {startIndex} to {startIndex + logs.Count - 1}:");
        Console.WriteLine(new string('=', 80));
        
        for (int i = 0; i < logs.Count; i++)
        {
            var log = logs[i];
            var logNumber = startIndex + i;
            
            // Color coding for different log levels
            var levelIcon = log.LogLevel switch
            {
                "Error" => "🔴",
                "Warning" => "🟡",
                "Information" => "🔵",
                _ => "⚪"
            };
            
            Console.WriteLine($"{logNumber:D3}. {levelIcon} [{log.Timestamp:yyyy-MM-dd HH:mm:ss}] {log.LogLevel}");
            Console.WriteLine($"     {log.Message}");
            
            if (!string.IsNullOrEmpty(log.Context))
                Console.WriteLine($"     Context: {log.Context}");
                
            if (!string.IsNullOrEmpty(log.ExceptionType))
                Console.WriteLine($"     Exception: {log.ExceptionType}");
                
            Console.WriteLine();
        }
        
        return Task.CompletedTask;
    }

    private static Task<string> GetLogLevelFilterAsync()
    {
        Console.WriteLine("\nSelect log level filter:");
        Console.WriteLine("1. Error");
        Console.WriteLine("2. Warning");
        Console.WriteLine("3. Information");
        Console.WriteLine("4. Cancel (no filter)");
        Console.WriteLine();
        
        Console.Write("Enter choice (1-4): ");
        var choice = SafePromptForString("", "b").Trim();
        
        return Task.FromResult(choice switch
        {
            "1" => "Error",
            "2" => "Warning",
            "3" => "Information",
            _ => ""
        });
    }

    private static async Task ShowLogViewerStatisticsAsync(ErrorLoggingService loggingService)
    {
        Console.WriteLine("\n📊 Detailed Log Statistics");
        Console.WriteLine("===========================");
        
        var stats = await loggingService.GetLogStatisticsAsync();
        var allLogs = await loggingService.GetRecentLogsAsync(10000);
        
        Console.WriteLine($"Total logs: {stats.TotalLogs}");
        Console.WriteLine();
        
        // Time-based statistics
        Console.WriteLine("📅 Time-based breakdown:");
        Console.WriteLine($"  Last 24 hours: {stats.ErrorsLast24Hours + stats.WarningsLast24Hours} logs");
        Console.WriteLine($"    Errors: {stats.ErrorsLast24Hours}");
        Console.WriteLine($"    Warnings: {stats.WarningsLast24Hours}");
        Console.WriteLine($"  Last 7 days: {stats.ErrorsLast7Days + stats.WarningsLast7Days} logs");
        Console.WriteLine($"    Errors: {stats.ErrorsLast7Days}");
        Console.WriteLine($"    Warnings: {stats.WarningsLast7Days}");
        Console.WriteLine();
        
        // Level-based statistics
        var errorCount = allLogs.Count(l => l.LogLevel == "Error");
        var warningCount = allLogs.Count(l => l.LogLevel == "Warning");
        var infoCount = allLogs.Count(l => l.LogLevel == "Information");
        
        Console.WriteLine("📊 Level breakdown:");
        Console.WriteLine($"  🔴 Errors: {errorCount} ({(errorCount * 100.0 / allLogs.Count):F1}%)");
        Console.WriteLine($"  🟡 Warnings: {warningCount} ({(warningCount * 100.0 / allLogs.Count):F1}%)");
        Console.WriteLine($"  🔵 Information: {infoCount} ({(infoCount * 100.0 / allLogs.Count):F1}%)");
        Console.WriteLine();
        
        // Context-based statistics
        var contextGroups = allLogs.Where(l => !string.IsNullOrEmpty(l.Context))
                                  .GroupBy(l => l.Context)
                                  .OrderByDescending(g => g.Count())
                                  .Take(5);
        
        Console.WriteLine("🏷️ Top contexts:");
        foreach (var group in contextGroups)
        {
            Console.WriteLine($"  {group.Key}: {group.Count()} logs");
        }
        
        if (stats.OldestLogDate.HasValue && stats.NewestLogDate.HasValue)
        {
            var timeSpan = stats.NewestLogDate.Value - stats.OldestLogDate.Value;
            Console.WriteLine();
            Console.WriteLine($"📈 Activity period: {timeSpan.Days} days, {timeSpan.Hours} hours");
            Console.WriteLine($"  First log: {stats.OldestLogDate:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  Latest log: {stats.NewestLogDate:yyyy-MM-dd HH:mm:ss}");
        }
        
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    private static Task ShowLogDetailAsync(List<ErrorLogEntry> currentPageLogs)
    {
        if (currentPageLogs.Count == 0)
        {
            Console.WriteLine("No logs available for detail view.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
            return Task.CompletedTask;
        }
        
        Console.WriteLine("\nSelect a log entry to view details:");
        for (int i = 0; i < currentPageLogs.Count; i++)
        {
            var log = currentPageLogs[i];
            var levelIcon = log.LogLevel switch
            {
                "Error" => "🔴",
                "Warning" => "🟡",
                "Information" => "🔵",
                _ => "⚪"
            };
            Console.WriteLine($"{i + 1}. {levelIcon} [{log.Timestamp:HH:mm:ss}] {log.Message}");
        }
        Console.WriteLine($"{currentPageLogs.Count + 1}. Cancel");
        Console.WriteLine();
        
        Console.Write($"Enter choice (1-{currentPageLogs.Count + 1}): ");
        var input = SafePromptForString("", "1").Trim();
        
        if (int.TryParse(input, out var choice) && choice >= 1 && choice <= currentPageLogs.Count)
        {
            var selectedLog = currentPageLogs[choice - 1];
            
            Console.WriteLine("\n📋 Log Entry Details");
            Console.WriteLine("===================");
            Console.WriteLine($"ID: {selectedLog.Id}");
            Console.WriteLine($"Timestamp: {selectedLog.Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC");
            Console.WriteLine($"Level: {selectedLog.LogLevel}");
            Console.WriteLine($"Source: {selectedLog.Source}");
            Console.WriteLine($"Message: {selectedLog.Message}");
            
            if (!string.IsNullOrEmpty(selectedLog.Context))
                Console.WriteLine($"Context: {selectedLog.Context}");
                
            if (!string.IsNullOrEmpty(selectedLog.ExceptionType))
            {
                Console.WriteLine($"Exception Type: {selectedLog.ExceptionType}");
                Console.WriteLine($"Exception Message: {selectedLog.ExceptionMessage}");
                
                if (!string.IsNullOrEmpty(selectedLog.StackTrace))
                {
                    Console.WriteLine("\nStack Trace:");
                    Console.WriteLine(new string('-', 40));
                    Console.WriteLine(selectedLog.StackTrace);
                }
            }
            
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }
        
        return Task.CompletedTask;
    }

    private static async Task ShowExtractorManagementMenuAsync()
    {
        using var extractorService = new ExtractorManagementService();
        
        ClearScreen();
        Console.WriteLine("\n🔧 File Extractor Management");
        Console.WriteLine("============================");
        
        var running = true;
        
        while (running)
        {
            try
            {
                Console.WriteLine("\nExtractor Management Options:");
                Console.WriteLine("1. List all extractors and their supported file types");
                Console.WriteLine("2. View extractor statistics");
                Console.WriteLine("3. Add file extension to an extractor");
                Console.WriteLine("4. Remove file extension from an extractor");
                Console.WriteLine("5. Test file extraction");
                Console.WriteLine("6. Reset extractor to default configuration");
                Console.WriteLine("7. View configuration audit");
                Console.WriteLine("b. Back to main menu");
                Console.WriteLine("q. Quit application");
                
                Console.Write("\nEnter your choice (1-7, b, q): ");
                var input = SafePromptForString("", "b").ToLower();
                
                switch (input)
                {
                    case "1":
                        await ShowExtractorListAsync(extractorService);
                        break;
                    case "2":
                        await ShowExtractorStatsAsync(extractorService);
                        break;
                    case "3":
                        await AddFileExtensionAsync(extractorService);
                        break;
                    case "4":
                        await RemoveFileExtensionAsync(extractorService);
                        break;
                    case "5":
                        await TestFileExtractionAsync(extractorService);
                        break;
                    case "6":
                        await ResetExtractorAsync(extractorService);
                        break;
                    case "7":
                        await ShowConfigurationAuditAsync(extractorService);
                        break;
                    case "b":
                    case "back":
                        running = false;
                        break;
                    case "q":
                    case "quit":
                        Environment.Exit(0);
                        break;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        await Task.Delay(1000);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }
    }

    private static async Task ShowVectorDatabaseManagementMenuAsync()
    {
        using var cleanupService = new CleanupService();
        using var embeddingService = new EmbeddingService();
        using var vectorStore = new SqliteVectorStore(embeddingService);
        
        ClearScreen();
        Console.WriteLine("\n🗄️ Vector Database Management");
        Console.WriteLine("==============================");
        
        var running = true;
        
        while (running)
        {
            try
            {
                Console.WriteLine("\nVector Database Options:");
                Console.WriteLine("1. View database status");
                Console.WriteLine("2. Clear vector index (keep database)");
                Console.WriteLine("3. Delete vector database (with backup)");
                Console.WriteLine("4. Delete vector database (no backup)");
                Console.WriteLine("5. Reindex all documents");
                Console.WriteLine("6. View database statistics");
                Console.WriteLine("b. Back to main menu");
                Console.WriteLine("q. Quit application");
                
                Console.Write("\nEnter your choice (1-6, b, q): ");
                var input = SafePromptForString("", "b").ToLower();
                
                switch (input)
                {
                    case "1":
                        await ShowVectorDatabaseStatusAsync(vectorStore);
                        break;
                    case "2":
                        await ClearVectorIndexAsync(vectorStore);
                        break;
                    case "3":
                        await DeleteVectorDatabaseAsync(cleanupService, true);
                        break;
                    case "4":
                        await DeleteVectorDatabaseAsync(cleanupService, false);
                        break;
                    case "5":
                        await ReindexDocumentsAsync();
                        break;
                    case "6":
                        await ShowVectorDatabaseStatsAsync(vectorStore);
                        break;
                    case "b":
                    case "back":
                        running = false;
                        break;
                    case "q":
                    case "quit":
                        Environment.Exit(0);
                        break;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        await Task.Delay(1000);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }
    }

    private static async Task ShowVectorDatabaseStatusAsync(SqliteVectorStore vectorStore)
    {
        Console.WriteLine("\n📊 Vector Database Status");
        Console.WriteLine("==========================");
        
        try
        {
            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "vectors.db");
            var exists = File.Exists(dbPath);
            
            Console.WriteLine($"Database file: {(exists ? "✅ Exists" : "❌ Not found")}");
            
            if (exists)
            {
                var fileInfo = new FileInfo(dbPath);
                Console.WriteLine($"File size: {fileInfo.Length / 1024.0:F2} KB");
                Console.WriteLine($"Last modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                
                // Try to get document count
                try
                {
                    var count = await vectorStore.GetChunkCountAsync();
                    Console.WriteLine($"Document chunks: {count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Could not read document count: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Database will be created when documents are first indexed.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error checking database status: {ex.Message}");
        }
        
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    private static async Task ClearVectorIndexAsync(SqliteVectorStore vectorStore)
    {
        Console.WriteLine("\n🗑️ Clear Vector Index");
        Console.WriteLine("=====================");
        
        Console.WriteLine("This will remove all document chunks from the vector database.");
        Console.WriteLine("The database file will remain but will be empty.");
        Console.Write("\nAre you sure you want to continue? (y/N): ");
        
        var confirmation = SafePromptForString("", "n").ToLower();
        if (confirmation == "y" || confirmation == "yes")
        {
            try
            {
                await vectorStore.ClearIndexAsync();
                Console.WriteLine("✅ Vector index cleared successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error clearing index: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Operation cancelled.");
        }
        
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    private static async Task DeleteVectorDatabaseAsync(CleanupService cleanupService, bool createBackup)
    {
        Console.WriteLine($"\n🗑️ Delete Vector Database {(createBackup ? "(with backup)" : "(no backup)")}");
        Console.WriteLine("======================================");
        
        Console.WriteLine("This will completely remove the vector database file.");
        if (createBackup)
        {
            Console.WriteLine("A backup will be created before deletion.");
        }
        else
        {
            Console.WriteLine("⚠️ WARNING: No backup will be created. This action cannot be undone!");
        }
        
        Console.Write("\nAre you sure you want to continue? (y/N): ");
        
        var confirmation = SafePromptForString("", "n").ToLower();
        if (confirmation == "y" || confirmation == "yes")
        {
            try
            {
                var options = new CleanupOptions
                {
                    CleanVectorDatabase = true,
                    VectorDatabasePath = "vector.db",
                    CleanErrorLogs = false,
                    CleanExportLogs = false,
                    CleanTempFiles = false,
                    CleanOutdatedCache = false,
                    OptimizeDatabase = false
                };
                var result = await cleanupService.PerformCleanupAsync(options);
                if (result.Success)
                {
                    Console.WriteLine("✅ Vector database deleted successfully.");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to delete vector database: {result.Details.GetValueOrDefault("Vector Database", "Unknown error")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error deleting database: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Operation cancelled.");
        }
        
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

private static async Task ReindexDocumentsAsync()
{
    Console.WriteLine("\n🔄 Reindex Documents");
    Console.WriteLine("====================");

    if (!await ConfirmReindex())
    {
        Console.WriteLine("Operation cancelled.");
        await WaitForKeyPress();
        return;
    }

    try
    {
        await ExecuteReindex();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error during reindex: {ex.Message}");
    }

    await WaitForKeyPress();
}

private static Task<bool> ConfirmReindex()
{
    Console.WriteLine("This will rebuild the entire vector index from scratch.");
    Console.WriteLine("All existing document chunks will be replaced.");
    Console.Write("\nAre you sure you want to continue? (y/N): ");
        
    var confirmation = SafePromptForString("", "n").ToLower();
    return Task.FromResult(confirmation == "y" || confirmation == "yes");
}

private static async Task ExecuteReindex()
{
    Console.WriteLine("\n🔄 Starting reindex process...");
    
    var config = ConfigurationService.LoadConfiguration();
    var currentDirectory = GetCurrentDirectory(config);
    
    using var embeddingService = new EmbeddingService();
    using var vectorStore = new SqliteVectorStore(embeddingService);
    
    var result = await IndexDocumentsAsync(currentDirectory, vectorStore);
    
    await DisplayIndexingResults(result);
}

private static string GetCurrentDirectory(AppConfiguration config)
{
    return config.RememberLastDirectory && !string.IsNullOrEmpty(config.LastDirectory)
        ? config.LastDirectory
        : Directory.GetCurrentDirectory();
}

private static async Task DisplayIndexingResults(IndexingResult result)
{
    Console.WriteLine($"\n✅ Reindex completed!");
    Console.WriteLine($"📄 Files indexed: {result.IndexedFiles.Count}");
    Console.WriteLine($"⚠️ Files skipped: {result.SkippedFiles.Count}");
    Console.WriteLine($"❌ Files failed: {result.FailedFiles.Count}");
    Console.WriteLine($"⏱️ Duration: {result.Duration}");

    await DisplaySkippedFiles(result.SkippedFiles);
    await DisplayFailedFiles(result.FailedFiles);
}

private static Task DisplaySkippedFiles(List<SkippedFile> skippedFiles)
{
    if (skippedFiles.Count == 0) return Task.CompletedTask;

    Console.WriteLine("\n📋 Skipped files:");
    foreach (var skipped in skippedFiles.Take(10))
    {
        Console.WriteLine($"  • {Path.GetFileName(skipped.FilePath)}: {skipped.Reason}");
    }
    
    if (skippedFiles.Count > 10)
    {
        Console.WriteLine($"  ... and {skippedFiles.Count - 10} more files");
    }
    
    return Task.CompletedTask;
}

private static Task DisplayFailedFiles(List<FailedFile> failedFiles)
{
    if (failedFiles.Count == 0) return Task.CompletedTask;

    Console.WriteLine("\n❌ Failed files:");
    foreach (var failed in failedFiles.Take(10))
    {
        Console.WriteLine($"  • {Path.GetFileName(failed.FilePath)}: {failed.Error}");
    }
    
    if (failedFiles.Count > 10)
    {
        Console.WriteLine($"  ... and {failedFiles.Count - 10} more files");
    }
    
    return Task.CompletedTask;
}

private static Task WaitForKeyPress()
{
    Console.WriteLine("\nPress any key to continue...");
    Console.ReadKey(true);
    return Task.CompletedTask;
}

    static async Task<IndexingResult> IndexDocumentsAsync(string rootPath, SqliteVectorStore vectorStore)
    {
        var result = new IndexingResult
        {
            IndexingStarted = DateTime.UtcNow
        };

        // Initialize extractors
        var extractors = new List<IFileExtractor>
        {
            new TextFileExtractor(),
            new HtmlFileExtractor(),
            new PdfFileExtractor(),
            new ChmFileExtractor(),
            new HhcFileExtractor()
        };

        var allFiles = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories);
        
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
                var extractor = extractors.FirstOrDefault(e => e.CanHandle(file));
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

                await vectorStore.IndexDocumentAsync(file, content, metadata);
                result.IndexedFiles.Add(file);
            }
            catch (Exception ex)
            {
                result.FailedFiles.Add(new FailedFile
                {
                    FilePath = file,
                    Error = ex.Message,
                    ExtractorType = extractors.FirstOrDefault(e => e.CanHandle(file))?.GetType().Name
                });
            }
        }

        result.IndexingCompleted = DateTime.UtcNow;
        return result;
    }

    static bool ShouldSkipFile(string filePath, FileInfo fileInfo)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        // Skip hidden files
        if (fileName.StartsWith('.'))
            return true;
            
        // Skip system files
        if (fileInfo.Attributes.HasFlag(FileAttributes.System) || 
            fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
            return true;
            
        // Skip binary files
        var binaryExtensions = new[] { ".exe", ".dll", ".bin", ".obj", ".lib", ".so", ".dylib", 
                                      ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".tiff",
                                      ".mp3", ".mp4", ".avi", ".mov", ".wav", ".flac",
                                      ".zip", ".rar", ".7z", ".tar", ".gz" };
        if (binaryExtensions.Contains(extension))
            return true;
            
        // Skip very large files (>50MB)
        if (fileInfo.Length > 50 * 1024 * 1024)
            return true;
            
        return false;
    }
    
    static string GetSkipReason(string filePath, FileInfo fileInfo)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        if (fileName.StartsWith('.'))
            return "Hidden file";
            
        if (fileInfo.Attributes.HasFlag(FileAttributes.System))
            return "System file";
            
        if (fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
            return "Hidden file attribute";
            
        var binaryExtensions = new[] { ".exe", ".dll", ".bin", ".obj", ".lib", ".so", ".dylib" };
        if (binaryExtensions.Contains(extension))
            return "Binary executable file";
            
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".tiff" };
        if (imageExtensions.Contains(extension))
            return "Image file";
            
        var mediaExtensions = new[] { ".mp3", ".mp4", ".avi", ".mov", ".wav", ".flac" };
        if (mediaExtensions.Contains(extension))
            return "Media file";
            
        var archiveExtensions = new[] { ".zip", ".rar", ".7z", ".tar", ".gz" };
        if (archiveExtensions.Contains(extension))
            return "Archive file";
            
        if (fileInfo.Length > 50 * 1024 * 1024)
            return "File too large (>50MB)";
            
        return "Unknown reason";
    }

    static async Task ShowVectorDatabaseStatsAsync(SqliteVectorStore vectorStore)
    {
        Console.WriteLine("\n📊 Vector Database Statistics");
        Console.WriteLine("=============================");
        
        try
        {
            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "vectors.db");
            var exists = File.Exists(dbPath);
            
            if (!exists)
            {
                Console.WriteLine("❌ Vector database does not exist.");
                Console.WriteLine("Run document indexing to create the database.");
            }
            else
            {
                var fileInfo = new FileInfo(dbPath);
                Console.WriteLine($"📁 Database file: vectors.db");
                Console.WriteLine($"📏 File size: {fileInfo.Length / 1024.0:F2} KB");
                Console.WriteLine($"📅 Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"📝 Last modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                
                try
                {
                    var count = await vectorStore.GetChunkCountAsync();
                    Console.WriteLine($"📄 Document chunks: {count:N0}");
                    
                    if (count > 0)
                    {
                        Console.WriteLine($"💾 Average chunk size: {(fileInfo.Length / count):F0} bytes");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Could not read document statistics: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error retrieving statistics: {ex.Message}");
        }
        
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    static async Task ShowExtractorListAsync(ExtractorManagementService service)
    {
        Console.WriteLine("\n📦 Available File Extractors:");
        Console.WriteLine("=============================");
        
        var extractors = await service.GetExtractorsAsync();
        
        foreach (var (key, extractor) in extractors)
        {
            Console.WriteLine($"\n🔧 {extractor.Name} ({key})");
            Console.WriteLine($"   Type: {extractor.Type}");
            Console.WriteLine($"   MIME Type: {extractor.MimeType}");
            Console.WriteLine($"   Description: {extractor.Description}");
            Console.WriteLine($"   Extensions: {string.Join(", ", extractor.CustomExtensions)}");
            
            var customCount = extractor.CustomExtensions.Count - extractor.DefaultExtensions.Count;
            if (customCount > 0)
            {
                Console.WriteLine($"   ⚡ Custom extensions added: {customCount}");
                var customExtensions = extractor.CustomExtensions.Except(extractor.DefaultExtensions).ToList();
                if (customExtensions.Count != 0)
                {
                    Console.WriteLine($"   📎 Custom: {string.Join(", ", customExtensions)}");
                }
            }
        }
        
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    static async Task ShowExtractorStatsAsync(ExtractorManagementService service)
    {
        Console.WriteLine("\n📊 Extractor Statistics:");
        Console.WriteLine("========================");
        
        var stats = await service.GetExtractionStatisticsAsync();
        
        Console.WriteLine($"Total extractors: {stats.TotalExtractors}");
        Console.WriteLine($"Total supported extensions: {stats.TotalSupportedExtensions}");
        Console.WriteLine();
        
        foreach (var (_, extractorStats) in stats.ExtractorStats)
        {
            Console.WriteLine($"🔧 {extractorStats.Name}:");
            Console.WriteLine($"   Supported extensions: {extractorStats.SupportedExtensionCount}");
            Console.WriteLine($"   Default extensions: {extractorStats.DefaultExtensionCount}");
            Console.WriteLine($"   Custom extensions: {extractorStats.CustomExtensionCount}");
            
            if (extractorStats.SupportedExtensions.Count != 0)
            {
                Console.WriteLine($"   Extensions: {string.Join(", ", extractorStats.SupportedExtensions)}");
            }
            Console.WriteLine();
        }
        
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    static async Task AddFileExtensionAsync(ExtractorManagementService service)
    {
        Console.WriteLine("\n➕ Add File Extension to Extractor");
        Console.WriteLine("===================================");
        
        var extractors = await service.GetExtractorsAsync();
        
        Console.WriteLine("Available extractors:");
        var extractorList = extractors.ToList();
        for (int i = 0; i < extractorList.Count; i++)
        {
            var (key, extractor) = extractorList[i];
            Console.WriteLine($"{i + 1}. {extractor.Name} ({key}) - Current: {string.Join(", ", extractor.CustomExtensions)}");
        }
        
        Console.Write("\nSelect extractor (1-" + extractorList.Count + "): ");
        var extractorChoice = SafePromptForString("", "1");
        
        if (!int.TryParse(extractorChoice, out int extractorIndex) || 
            extractorIndex < 1 || extractorIndex > extractorList.Count)
        {
            Console.WriteLine("❌ Invalid extractor selection.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
            return;
        }
        
        var selectedExtractor = extractorList[extractorIndex - 1];
        
        Console.Write("Enter file extension(s) to add (e.g., '.docx' or 'docx,rtf'): ");
        var extensionsInput = SafePromptForString("", "");
        
        if (string.IsNullOrWhiteSpace(extensionsInput))
        {
            Console.WriteLine("❌ No extensions specified.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
            return;
        }
        
        var extensions = extensionsInput.Split(',').Select(e => e.Trim()).ToArray();
        int successCount = 0;
        
        foreach (var extension in extensions)
        {
            var success = await service.AddFileExtensionAsync(selectedExtractor.Key, extension);
            if (success)
            {
                successCount++;
                Console.WriteLine($"✅ Added extension {extension} to {selectedExtractor.Value.Name}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to add extension {extension} to {selectedExtractor.Value.Name}");
            }
        }
        
        Console.WriteLine($"\n✨ Successfully added {successCount} of {extensions.Length} extensions.");
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    static async Task RemoveFileExtensionAsync(ExtractorManagementService service)
    {
        Console.WriteLine("\n➖ Remove File Extension from Extractor");
        Console.WriteLine("=======================================");
        
        var extractors = await service.GetExtractorsAsync();
        
        Console.WriteLine("Available extractors:");
        var extractorList = extractors.ToList();
        for (int i = 0; i < extractorList.Count; i++)
        {
            var (key, extractor) = extractorList[i];
            Console.WriteLine($"{i + 1}. {extractor.Name} ({key}) - Current: {string.Join(", ", extractor.CustomExtensions)}");
        }
        
        Console.Write("\nSelect extractor (1-" + extractorList.Count + "): ");
        var extractorChoice = SafePromptForString("", "1");
        
        if (!int.TryParse(extractorChoice, out int extractorIndex) || 
            extractorIndex < 1 || extractorIndex > extractorList.Count)
        {
            Console.WriteLine("❌ Invalid extractor selection.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
            return;
        }
        
        var selectedExtractor = extractorList[extractorIndex - 1];
        
        if (selectedExtractor.Value.CustomExtensions.Count == 0)
        {
            Console.WriteLine($"❌ {selectedExtractor.Value.Name} has no extensions to remove.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
            return;
        }
        
        Console.Write("Enter file extension(s) to remove (e.g., '.docx' or 'docx,rtf'): ");
        var extensionsInput = SafePromptForString("", "");
        
        if (string.IsNullOrWhiteSpace(extensionsInput))
        {
            Console.WriteLine("❌ No extensions specified.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
            return;
        }
        
        var extensions = extensionsInput.Split(',').Select(e => e.Trim()).ToArray();
        int successCount = 0;
        
        foreach (var extension in extensions)
        {
            var success = await service.RemoveFileExtensionAsync(selectedExtractor.Key, extension);
            if (success)
            {
                successCount++;
                Console.WriteLine($"✅ Removed extension {extension} from {selectedExtractor.Value.Name}");
            }
            else
            {
                Console.WriteLine($"❌ Extension {extension} was not found in {selectedExtractor.Value.Name}");
            }
        }
        
        Console.WriteLine($"\n✨ Successfully removed {successCount} of {extensions.Length} extensions.");
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    static async Task TestFileExtractionAsync(ExtractorManagementService service)
    {
        Console.WriteLine("\n🧪 Test File Extraction");
        Console.WriteLine("=======================");
        
        Console.Write("Enter file path to test: ");
        var filePath = SafePromptForString("", "");
        
        if (string.IsNullOrWhiteSpace(filePath))
        {
            Console.WriteLine("❌ No file path specified.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
            return;
        }
        
        // Remove quotes if present
        filePath = filePath.Trim('"');
        
        Console.WriteLine("\n🔄 Testing extraction...");
        var result = await service.TestFileExtractionAsync(filePath);
        
        Console.WriteLine("\n📋 Test Results:");
        Console.WriteLine("================");
        Console.WriteLine($"File: {Path.GetFileName(result.FilePath)}");
        Console.WriteLine($"Extension: {result.FileExtension}");
        
        if (result.Success)
        {
            Console.WriteLine("✅ Status: Success");
            Console.WriteLine($"🔧 Extractor used: {result.ExtractorUsed}");
            Console.WriteLine($"📏 Content length: {result.ContentLength:N0} characters");
            Console.WriteLine($"⏱️ Extraction time: {result.ExtractionTimeMs}ms");
            Console.WriteLine($"💾 File size: {result.FileSizeBytes:N0} bytes");
            
            if (!string.IsNullOrEmpty(result.ContentPreview))
            {
                Console.WriteLine("\n📖 Content Preview:");
                Console.WriteLine("===================");
                var preview = result.ContentPreview.Length > 500 ? result.ContentPreview[..500] + "..." : result.ContentPreview;
                Console.WriteLine(preview);
            }
        }
        else
        {
            Console.WriteLine("❌ Status: Failed");
            Console.WriteLine($"🚨 Error: {result.ErrorMessage}");
            if (!string.IsNullOrEmpty(result.ExtractorUsed))
            {
                Console.WriteLine($"🔧 Attempted extractor: {result.ExtractorUsed}");
            }
        }
        
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    static async Task ResetExtractorAsync(ExtractorManagementService service)
    {
        Console.WriteLine("\n🔄 Reset Extractor to Default Configuration");
        Console.WriteLine("==========================================");
        
        var extractors = await service.GetExtractorsAsync();
        
        Console.WriteLine("Available extractors:");
        var extractorList = extractors.ToList();
        for (int i = 0; i < extractorList.Count; i++)
        {
            var (key, extractor) = extractorList[i];
            var customCount = extractor.CustomExtensions.Count - extractor.DefaultExtensions.Count;
            var status = customCount > 0 ? $"({customCount} custom)" : "(default)";
            Console.WriteLine($"{i + 1}. {extractor.Name} ({key}) {status}");
        }
        
        Console.Write("\nSelect extractor to reset (1-" + extractorList.Count + "): ");
        var extractorChoice = SafePromptForString("", "1");
        
        if (!int.TryParse(extractorChoice, out int extractorIndex) || 
            extractorIndex < 1 || extractorIndex > extractorList.Count)
        {
            Console.WriteLine("❌ Invalid extractor selection.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
            return;
        }
        
        var selectedExtractor = extractorList[extractorIndex - 1];
        
        Console.WriteLine($"\n⚠️  This will reset {selectedExtractor.Value.Name} to its default configuration.");
        Console.WriteLine($"Current extensions: {string.Join(", ", selectedExtractor.Value.CustomExtensions)}");
        Console.WriteLine($"Default extensions: {string.Join(", ", selectedExtractor.Value.DefaultExtensions)}");
        
        var confirmation = SafePromptForString("Are you sure? (y/N): ", "n").ToLower();
        
        if (confirmation == "y" || confirmation == "yes")
        {
            var success = await service.ResetExtractorToDefaultAsync(selectedExtractor.Key);
            if (success)
            {
                Console.WriteLine($"✅ {selectedExtractor.Value.Name} has been reset to default configuration.");
            }
            else
            {
                Console.WriteLine($"❌ Failed to reset {selectedExtractor.Value.Name}.");
            }
        }
        else
        {
            Console.WriteLine("Reset cancelled.");
        }
        
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    static async Task ShowConfigurationAuditAsync(ExtractorManagementService service)
    {
        Console.WriteLine("\n🔍 Extractor Configuration Audit");
        Console.WriteLine("================================");
        
        var extractors = await service.GetExtractorsAsync();
        var stats = await service.GetExtractionStatisticsAsync();
        
        Console.WriteLine($"📊 Summary: {stats.TotalExtractors} extractors managing {stats.TotalSupportedExtensions} file extensions");
        Console.WriteLine();
        
        foreach (var (key, extractor) in extractors)
        {
            Console.WriteLine($"🔧 {extractor.Name} ({key})");
            Console.WriteLine($"   Default extensions: {string.Join(", ", extractor.DefaultExtensions)}");
            
            var customExtensions = extractor.CustomExtensions.Except(extractor.DefaultExtensions).ToList();
            if (customExtensions.Count != 0)
            {
                Console.WriteLine($"   ➕ Added: {string.Join(", ", customExtensions)}");
            }
            
            var removedDefaults = extractor.DefaultExtensions.Except(extractor.CustomExtensions).ToList();
            if (removedDefaults.Count != 0)
            {
                Console.WriteLine($"   ➖ Removed: {string.Join(", ", removedDefaults)}");
            }
            
            if (customExtensions.Count == 0 && removedDefaults.Count == 0)
            {
                Console.WriteLine($"   ✅ Using default configuration");
            }
            
            Console.WriteLine();
        }
        
        // Check for potential issues
        Console.WriteLine("🔍 Configuration Analysis:");
        Console.WriteLine("==========================");
        
        var allExtensions = extractors.SelectMany(e => e.Value.CustomExtensions).ToList();
        var duplicateExtensions = allExtensions.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        
        if (duplicateExtensions.Count != 0)
        {
            Console.WriteLine("⚠️  WARNING: Duplicate extensions found:");
            foreach (var duplicate in duplicateExtensions)
            {
                var handlers = extractors.Where(e => e.Value.CustomExtensions.Contains(duplicate)).Select(e => e.Value.Name);
                Console.WriteLine($"   {duplicate}: handled by {string.Join(", ", handlers)}");
            }
        }
        else
        {
            Console.WriteLine("✅ No duplicate extensions found - all extensions are uniquely mapped.");
        }
        
        var totalCustomizations = extractors.Values.Sum(e => Math.Abs(e.CustomExtensions.Count - e.DefaultExtensions.Count));
        if (totalCustomizations > 0)
        {
            Console.WriteLine($"📝 Total customizations: {totalCustomizations}");
        }
        else
        {
            Console.WriteLine("📝 All extractors are using default configurations.");
        }
        
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    static Task RunServerMode(EnhancedMcpRagServer server)
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

    static async Task<EnhancedMcpRagServer?> ChangeDirectoryAsync(
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
        var newPath = SafePromptForString("", "").Trim();
        
        if (string.IsNullOrEmpty(newPath) || newPath.Equals("cancel", StringComparison.CurrentCultureIgnoreCase))
        {
            Console.WriteLine("Directory change cancelled.");
            return currentServer;
        }
        
        // Validate the new directory
        if (!Directory.Exists(newPath))
        {
            Console.WriteLine($"❌ Error: Directory '{newPath}' does not exist.");
            using var promptService = new PromptService();
            var createResponse = await promptService.PromptYesNoDefaultYesAsync("Would you like to create it?");
            
            if (createResponse)
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
            using var promptService = new PromptService();
            var continueResponse = await promptService.PromptYesNoDefaultYesAsync("Continue anyway?");
            
            if (!continueResponse)
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
            
            Console.WriteLine("Checking AI provider connection...");
            if (await newServer._aiProvider.IsAvailableAsync())
            {
                var models = await newServer._aiProvider.GetModelsAsync();
                Console.WriteLine($"✅ {newServer._aiProvider.ProviderName} connected! Available models: {string.Join(", ", models)}");
                Console.WriteLine($"Using model: {ollamaModel}");
            }
            else
            {
                Console.WriteLine($"⚠️ {newServer._aiProvider.ProviderName} not available. AI features will show connection errors.");
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
            
            // Save the new directory to configuration
            ConfigurationService.UpdateLastDirectory(newPath, logger);
            
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

    public static void ShowMenu()
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
        Console.WriteLine("  14 - Configuration settings");
        Console.WriteLine("  15 - View error logs");
        Console.WriteLine("  16 - File extractor management");
        Console.WriteLine("  17 - AI provider management");
        Console.WriteLine("  18 - Vector database management");
        Console.WriteLine("  c - Clear screen");
        Console.WriteLine("  m - Show this menu");
        Console.WriteLine("  q - Quit");
    }

    public static void ClearScreen()
    {
        try
        {
            // Only clear console if not in test environment
            if (!IsTestEnvironment())
            {
                Console.Clear();
            }
        }
        catch
        {
            // Ignore console clear errors in test environments
        }
        
        Console.WriteLine("🎯 HlpAI");
        Console.WriteLine("========================");
    }
    
    /// <summary>
    /// Determines if running in a test environment to avoid console blocking
    /// </summary>
    static bool IsTestEnvironment()
    {
        return System.Diagnostics.Process.GetCurrentProcess().ProcessName.Contains("testhost") ||
               System.Diagnostics.Process.GetCurrentProcess().ProcessName.Contains("dotnet") ||
               Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
               AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName?.Contains("TUnit") == true);
    }

    public static void ShowUsage()
    {
        Console.WriteLine("🎯 MCP RAG Extended Demo");
        Console.WriteLine("========================");
        Console.WriteLine();
        Console.WriteLine("USAGE:");
        Console.WriteLine("  dotnet run                              # Interactive setup mode");
        Console.WriteLine("  dotnet run <directory> [model] [mode]  # Command line mode");
        Console.WriteLine("  dotnet run -- --audit <directory>      # Audit mode");
        Console.WriteLine("  dotnet run -- [logging options]        # Logging configuration");
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
        Console.WriteLine("LOGGING OPTIONS:");
        Console.WriteLine("  --help, -h                              Show this help message");
        Console.WriteLine("  --enable-logging                       Enable error logging");
        Console.WriteLine("  --disable-logging                      Disable error logging");
        Console.WriteLine("  --log-level <level>                    Set minimum log level (Error, Warning, Information)");
        Console.WriteLine("  --log-retention-days <days>            Set log retention period in days (default: 30)");
        Console.WriteLine("  --clear-logs                           Clear all existing error logs");
        Console.WriteLine("  --show-log-stats                       Display error log statistics");
        Console.WriteLine("  --show-recent-logs [count]             Show recent error logs (default: 10)");
        Console.WriteLine();
        Console.WriteLine("FILE EXPORT OPTIONS:");
        Console.WriteLine("  --export-files [output_path]          Export file list to specified path");
        Console.WriteLine("  --list-files-export [output_path]     Display and export file list");
        Console.WriteLine("  --export-format <format>              Export format: csv, json, txt, xml (default: csv)");
        Console.WriteLine("  --export-metadata                     Include file metadata in export (default: true)");
        Console.WriteLine();
        Console.WriteLine("EXTRACTOR MANAGEMENT OPTIONS:");
        Console.WriteLine("  --list-extractors                      List all available file extractors");
        Console.WriteLine("  --extractor-stats                      Show extractor statistics and extension counts");
        Console.WriteLine("  --add-file-type <key:ext,ext>         Add file extensions to extractor (e.g., text:docx,rtf)");
        Console.WriteLine("  --remove-file-type <key:ext,ext>      Remove file extensions from extractor");
        Console.WriteLine("  --test-extraction <file_path>         Test file extraction with current configuration");
        Console.WriteLine("  --reset-extractor <key>               Reset extractor to default configuration");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine("  dotnet run                                       # Interactive setup");
        Console.WriteLine("  dotnet run \"C:\\MyDocuments\"                    # Will prompt for model selection");
        Console.WriteLine("  dotnet run \"C:\\MyDocuments\" \"llama3.1\"          # Use specific model");
        Console.WriteLine("  dotnet run \"C:\\MyDocuments\" \"llama3.2\" \"rag\"    # Use specific model and mode");
        Console.WriteLine("  dotnet run -- --audit \"C:\\MyDocuments\"         # Audit mode");
        Console.WriteLine("  dotnet run -- --show-log-stats                  # View error log statistics");
        Console.WriteLine("  dotnet run -- --clear-logs                      # Clear all error logs");
        Console.WriteLine("  dotnet run -- --log-level Error                 # Set log level to Error only");
        Console.WriteLine("  dotnet run -- --enable-logging                  # Enable error logging");
        Console.WriteLine("  dotnet run \"C:\\MyDocs\" --log-level Information  # Run with detailed logging");
        Console.WriteLine("  dotnet run \"C:\\MyDocs\" --export-files myfiles.csv # Export file list to CSV");
        Console.WriteLine("  dotnet run \"C:\\MyDocs\" --list-files-export        # Show and export file list");
        Console.WriteLine("  dotnet run \"C:\\MyDocs\" --export-format json       # Export as JSON format");
        Console.WriteLine("  dotnet run -- --list-extractors                     # List available file extractors");
        Console.WriteLine("  dotnet run -- --extractor-stats                     # Show extractor statistics");
        Console.WriteLine("  dotnet run -- --add-file-type text:docx,rtf         # Add .docx and .rtf to text extractor");
        Console.WriteLine("  dotnet run -- --test-extraction \"C:\\test.docx\"      # Test extraction of specific file");
        Console.WriteLine("  dotnet run -- --reset-extractor text                # Reset text extractor to defaults");
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

    static void DisplayResponse(McpResponse response, string fallbackTitle = "Response")
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

    static async Task ShowAiProviderMenuAsync()
    {
        var config = ConfigurationService.LoadConfiguration();
        bool running = true;
        
        while (running)
        {
            ClearScreen();
            Console.WriteLine("\n🤖 AI Provider Configuration");
            Console.WriteLine("============================");
            Console.WriteLine($"Current Provider: {config.LastProvider}");
            Console.WriteLine($"Current Model: {config.LastModel ?? "Not set"}");
            Console.WriteLine();
            Console.WriteLine("Provider Settings:");
            Console.WriteLine($"1. Ollama URL: {config.OllamaUrl}");
            Console.WriteLine($"2. LM Studio URL: {config.LmStudioUrl}");
            Console.WriteLine($"3. Open Web UI URL: {config.OpenWebUiUrl}");
            Console.WriteLine();
            Console.WriteLine("Default Models:");
            Console.WriteLine($"4. Ollama Default: {config.OllamaDefaultModel}");
            Console.WriteLine($"5. LM Studio Default: {config.LmStudioDefaultModel}");
            Console.WriteLine($"6. Open Web UI Default: {config.OpenWebUiDefaultModel}");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("7. Select AI Provider");
            Console.WriteLine("8. Test Provider Connection");
            Console.WriteLine("9. List Available Models");
            Console.WriteLine("10. Detect Available Providers");
            Console.WriteLine("b. Back to main menu");
            Console.WriteLine();
            
            Console.Write("Select option (1-10, b): ");
            var input = SafePromptForString("", "b").ToLower().Trim();
            
            switch (input)
            {
                case "1":
                    await ConfigureProviderUrlAsync("Ollama", url => config.OllamaUrl = url);
                    break;
                case "2":
                    await ConfigureProviderUrlAsync("LM Studio", url => config.LmStudioUrl = url);
                    break;
                case "3":
                    await ConfigureProviderUrlAsync("Open Web UI", url => config.OpenWebUiUrl = url);
                    break;
                case "4":
                    await ConfigureDefaultModelAsync("Ollama", model => config.OllamaDefaultModel = model);
                    break;
                case "5":
                    await ConfigureDefaultModelAsync("LM Studio", model => config.LmStudioDefaultModel = model);
                    break;
                case "6":
                    await ConfigureDefaultModelAsync("Open Web UI", model => config.OpenWebUiDefaultModel = model);
                    break;
                case "7":
                    await SelectAiProviderAsync(config);
                    break;
                case "8":
                    await TestProviderConnectionAsync(config);
                    break;
                case "9":
                    await ListAvailableModelsAsync(config);
                    break;
                case "10":
                    await DetectAvailableProvidersAsync();
                    break;
                case "b":
                case "back":
                    running = false;
                    break;
                default:
                    Console.WriteLine("❌ Invalid option. Please try again.");
                    break;
            }
            
            if (running)
            {
                await Task.Delay(1000); // Brief pause to let user see the result
            }
        }
        
        // Save configuration after changes
        ConfigurationService.SaveConfiguration(config);
    }

    static Task ConfigureProviderUrlAsync(string providerName, Action<string> setUrlAction)
    {
        Console.WriteLine($"\n🔧 Configure {providerName} URL");
        Console.WriteLine("==============================");
        
        Console.Write($"Enter {providerName} URL (press Enter to keep current): ");
        var url = SafePromptForString("", "http://localhost:3000").Trim();
        
        if (!string.IsNullOrEmpty(url))
        {
            setUrlAction(url);
            Console.WriteLine($"✅ {providerName} URL updated to: {url}");
        }
        else
        {
            Console.WriteLine("✅ URL unchanged.");
        }
        
        return Task.CompletedTask;
    }

    static Task ConfigureDefaultModelAsync(string providerName, Action<string> setModelAction)
    {
        Console.WriteLine($"\n🔧 Configure {providerName} Default Model");
        Console.WriteLine("========================================");
        
        Console.Write($"Enter {providerName} default model (press Enter to keep current): ");
        var model = SafePromptForString("", "llama3.2").Trim();
        
        if (!string.IsNullOrEmpty(model))
        {
            setModelAction(model);
            Console.WriteLine($"✅ {providerName} default model updated to: {model}");
        }
        else
        {
            Console.WriteLine("✅ Default model unchanged.");
        }
        
        return Task.CompletedTask;
    }

    static Task SelectAiProviderAsync(AppConfiguration config)
    {
        Console.WriteLine("\n🤖 Select AI Provider");
        Console.WriteLine("=====================");
        
        var providerDescriptions = AiProviderFactory.GetProviderDescriptions();
        var providers = providerDescriptions.Keys.ToList();
        
        for (int i = 0; i < providers.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {providerDescriptions[providers[i]]}");
        }
        
        Console.Write($"Select provider (1-{providers.Count}): ");
        var input = SafePromptForString("", "b").Trim();
        
        if (int.TryParse(input, out int selection) && selection >= 1 && selection <= providers.Count)
        {
            var selectedProvider = providers[selection - 1];
            config.LastProvider = selectedProvider;
            
            // Set appropriate default model based on provider
            config.LastModel = selectedProvider switch
            {
                AiProviderType.Ollama => config.OllamaDefaultModel,
                AiProviderType.LmStudio => config.LmStudioDefaultModel,
                AiProviderType.OpenWebUi => config.OpenWebUiDefaultModel,
                _ => "default"
            };
            
            Console.WriteLine($"✅ Selected provider: {selectedProvider}");
            Console.WriteLine($"✅ Default model set to: {config.LastModel}");
        }
        else
        {
            Console.WriteLine("❌ Invalid selection.");
        }
        
        return Task.CompletedTask;
    }

    static async Task TestProviderConnectionAsync(AppConfiguration config)
    {
        Console.WriteLine("\n🔌 Test Provider Connection");
        Console.WriteLine("============================");
        
        try
        {
            var provider = AiProviderFactory.CreateProvider(
                config.LastProvider,
                config.LastModel ?? "default",
                GetProviderUrl(config, config.LastProvider)
            );
            
            Console.WriteLine($"Testing connection to {provider.ProviderName}...");
            
            var isAvailable = await provider.IsAvailableAsync();
            if (isAvailable)
            {
                Console.WriteLine($"✅ {provider.ProviderName} is available at {provider.BaseUrl}");
                
                var models = await provider.GetModelsAsync();
                if (models.Count > 0)
                {
                    Console.WriteLine($"Available models: {string.Join(", ", models)}");
                }
                else
                {
                    Console.WriteLine("⚠️ No models found (provider may be running but no models loaded)");
                }
            }
            else
            {
                Console.WriteLine($"❌ {provider.ProviderName} is not available at {provider.BaseUrl}");
                Console.WriteLine("Make sure the provider is running and accessible.");
            }
            
            provider.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error testing provider: {ex.Message}");
        }
    }



    static async Task DetectAvailableProvidersAsync()
    {
        Console.WriteLine("\n🔍 Detect Available Providers");
        Console.WriteLine("==============================");
        
        Console.WriteLine("Scanning for available AI providers...");
        
        try
        {
            var availableProviders = await AiProviderFactory.DetectAvailableProvidersAsync();
            
            Console.WriteLine("\nProvider Availability:");
            foreach (var (providerType, isAvailable) in availableProviders)
            {
                var status = isAvailable ? "✅ Available" : "❌ Not available";
                var info = AiProviderFactory.GetProviderInfo(providerType);
                Console.WriteLine($"{info.Name}: {status} ({info.DefaultUrl})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error detecting providers: {ex.Message}");
        }
    }

    static string? GetProviderUrl(AppConfiguration config, AiProviderType providerType)
    {
        return providerType switch
        {
            AiProviderType.Ollama => config.OllamaUrl,
            AiProviderType.LmStudio => config.LmStudioUrl,
            AiProviderType.OpenWebUi => config.OpenWebUiUrl,
            _ => null
        };
    }
}
