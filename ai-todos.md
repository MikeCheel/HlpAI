# AI Provider Availability Handling and Fallback Mechanisms

## Current Implementation

### Current Todo

1. [Completed] Update application title from 'HlpAI - Enhanced MCP RAG Server' to 'HlpAI - Intelligent Document Assistant'
2. [Completed] Document AI provider switching process in README-INTERACTIVE.md
3. [Completed] Add configuration details for timeout and max tokens settings to README-MCP.md
4. [Completed] Implement standardized error handling middleware for AI operations
5. [Pending] Extend AiProviderFactory to support additional provider types
6. [Pending] Ensure menu items are appropriate for current provider (hide model-related options unless supported)
7. [Pending] Document AI tool definitions (tools/list and tools/call) in README-MCP.md
8. [Pending] Add ask_ai and analyze_file functionality documentation
9. [Pending] Document RAG-enhanced questioning features
10. [Pending] Add semantic search API documentation
11. [Pending] Document command-line arguments for AI provider timeouts (OpenAI, Anthropic, DeepSeek)
12. [Pending] Document max token limit configurations for all supported providers
13. [Pending] Add documentation for cosine similarity calculation in SemanticSearchService
14. [Pending] Document IAiProvider interface usage
15. [Pending] Add configuration details for secure API key storage
16. [Pending] Document provider auto-detection and fallback mechanisms
17. [Pending] Add documentation for model compatibility handling between providers
18. [Pending] Document configuration persistence features

## Archived Content

### Previous Todos

1. [Question] How to add new tool integrations to the MCP system?
   - Suggestion: Document the tool registration process in EnhancedMcpRagServer.cs
   - Answer: Use this suggestion for now.

2. [Question] Documentation for RAG configuration options like similarity thresholds, chunk sizes, or indexing parameters?
   - Suggestion: Add configuration section to README-MCP.md
   - Answer: Use this suggestion.

3. [Question] Best practices for error recovery and logging configuration?
   - Suggestion: Implement standardized error handling middleware
   - Answer: Use this suggestion.

4. [Question] Documentation of configurable parameters for indexing thresholds (file size limits, skip rules)?
   - Suggestion: Add to ConfigurationService documentation
   - Answer: Use this suggestion.

5. [Question] How to add new AI provider types or modify detection behavior?
   - Suggestion: Extend AiProviderFactory class
   - Answer: 

6. [Question] How to configure timeout settings for different AI providers?
   - Suggestion: Document in CommandLineArgumentsService.cs
   - Answer: Use this suggestion.

7. [Question] How to set maximum token limits per provider?
   - Suggestion: Add to AppConfiguration documentation
   - Answer: Use this suggestion.

8. [Question] How to implement semantic search functionality?
   - Suggestion: Document SemanticSearchService usage
   - Answer: Use this suggestion.

9. [Question] How to add support for additional file types?
   - Suggestion: Extend file extractor management system
   - Answer: Use this suggestion.

10. [Question] How to customize AI interaction APIs?
    - Suggestion: Document IAiProvider interface
    - Answer: Use this suggestion.

### From vibe-docs/ai-provider-availability.md

## System Startup Behavior

### When No AI Providers Are Available

The HlpAI system is designed to handle scenarios where no AI providers are available at startup gracefully:

1. **Default Provider Initialization**: The system attempts to initialize the default AI provider (Ollama) based on the configuration
2. **Fallback to Configuration Defaults**: If no configuration exists, the system uses built-in defaults
3. **Graceful Startup**: The application starts successfully even if no providers are immediately available
4. **Runtime Detection**: The system continuously checks for provider availability during operation

### Startup Flow

```
Application Start
       ↓
Load Configuration (ConfigurationService.LoadConfiguration)
       ↓
Create AI Provider (AiProviderFactory.CreateProvider)
       ↓
Initialize EnhancedMcpRagServer
       ↓
Check Provider Availability (IsAvailableAsync)
       ↓
[Available] → Normal Operation
[Unavailable] → Graceful Degradation Mode
```

## Default Configuration

### Built-in Defaults

When no configuration file exists or configuration loading fails, the system uses these defaults:

- **Default Provider**: `AiProviderType.Ollama`
- **Default Ollama URL**: `http://localhost:11434`
- **Default Ollama Model**: `llama3.2:3b`
- **Default LM Studio URL**: `http://localhost:1234`
- **Default Open Web UI URL**: `http://localhost:3000`
- **Remember Last Provider**: `true`

### Configuration Sources

1. **Primary**: `appsettings.json` file
2. **Fallback**: Built-in default values in `AppConfiguration` class
3. **Override**: Command-line arguments (highest priority)

## Provider Detection and Management

### Automatic Provider Detection

The `AiProviderFactory.DetectAvailableProvidersAsync()` method:

1. **Scans All Configured Providers**: Attempts to connect to each provider type
2. **Tests Connectivity**: Uses `IsAvailableAsync()` to verify each provider
3. **Returns Availability Status**: Provides a list of available providers
4. **Logs Results**: Records detection results for debugging

### Provider Creation Process

```csharp
// AiProviderFactory.CreateProvider implementation
switch (config.LastProvider)
{
    case AiProviderType.Ollama:
        return new OllamaClient(config.OllamaUrl, config.OllamaModel, logger);
    case AiProviderType.LmStudio:
        return new LmStudioProvider(config.LmStudioUrl, config.LmStudioModel, logger);
    case AiProviderType.OpenWebUi:
        return new OpenWebUiProvider(config.OpenWebUiUrl, config.OpenWebUiModel, logger);
    default:
        throw new ArgumentException($"Unknown AI provider type: {config.LastProvider}
