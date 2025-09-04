# HlpAI - MCP Server Mode Documentation

> **Model Context Protocol server for external integration and automation**

The MCP (Model Context Protocol) Server Mode enables HlpAI to run as a service that can be integrated with external tools, applications, and AI assistants. This mode provides programmatic access to all HlpAI capabilities through a standardized protocol, making it ideal for automation, integration, and building document intelligence into other systems.

## 🎯 Overview

**MCP Server Mode** is designed for:
- ✅ **Integration with AI tools** like Claude Desktop, Cursor, and other MCP-compatible applications
- ✅ **Automation workflows** where document processing needs to be scripted or scheduled
- ✅ **Microservices architecture** where HlpAI capabilities are exposed as APIs
- ✅ **Custom applications** that need to leverage document intelligence programmatically
- ✅ **Headless operation** where no user interface is required

## 🚀 Getting Started

### Starting MCP Server Mode

#### From Interactive Mode
```bash
# Start interactive mode first
dotnet run

# Then use Command 12 to switch to MCP server mode
Command: 12
```

#### Direct Command Line Startup
```bash
# Start directly in MCP server mode with parameters
dotnet run "C:\YourDocuments" "llama3.2" "mcp"

# Or let the app prompt for model selection
dotnet run "C:\YourDocuments" "mcp"
```

### Audit Mode for Document Analysis

Before running in MCP server mode, you can use audit mode to analyze your document directory:

```bash
# Audit a directory to understand its contents
dotnet run -- --audit "C:\YourDocuments"

# Example output:
# 📊 File Audit Report for: C:\YourDocuments
# ==========================================
# 📁 Total Files: 156
# 📄 Supported Types: 134 (85.9%)
# ⚠️  Unsupported Types: 22 (14.1%)
# 💾 Total Size: 45.2 MB
# 🔒 Access Issues: 3 files
```

**Audit Features:**
- **File type analysis** - Identifies supported vs unsupported formats
- **Size analysis** - Shows file size distribution and totals
- **Permission analysis** - Detects access-restricted files
- **Recommendations** - Suggests optimizations for better processing
- **Recursive scanning** - Analyzes all subdirectories

**When to use audit mode:**
- Before setting up a new document directory
- To troubleshoot indexing issues
- To understand document collection composition
- To identify potential access problems

#### Configuration Parameters
```bash
# Full parameter format
dotnet run <document-directory> <model-name> <operation-mode>

# Examples:
dotnet run "C:\Docs" "llama3.2" "mcp"
dotnet run "/home/user/documents" "codellama" "mcp"
dotnet run "~/Documents" "mcp"  # Prompts for model selection
```

#### AI Provider Configuration

**Timeout Settings:**
Each AI provider has configurable timeout settings (default: 5 minutes):
- **OpenAI**: `OpenAiTimeoutMinutes`
- **Anthropic**: `AnthropicTimeoutMinutes`
- **DeepSeek**: `DeepSeekTimeoutMinutes`
- **LM Studio**: `LmStudioTimeoutMinutes`
- **OpenWebUI**: `OpenWebUiTimeoutMinutes`

**Max Tokens Settings:**
Token limits can be configured per provider:
- **OpenAI**: `OpenAiMaxTokens` (default: 4000)
- **Anthropic**: `AnthropicMaxTokens` (default: 4000)
- **DeepSeek**: `DeepSeekMaxTokens` (default: 4000)
- **LM Studio**: `LmStudioMaxTokens` (default: 4096)
- **OpenWebUI**: `OpenWebUiMaxTokens` (default: 4096)

**Configuration Methods:**
1. **Interactive Mode**: Use the provider switching menu to configure settings
2. **Command Line**: Pass configuration arguments during startup
3. **Configuration File**: Settings are persisted in SQLite database
4. **Environment Variables**: Can be set via system environment

**Command-Line Arguments:**

*Timeout Arguments:*
```bash
# AI Provider timeouts (in minutes)
--ai-provider-timeout 10        # General AI provider timeout
--openai-timeout 5              # OpenAI specific timeout
--anthropic-timeout 5           # Anthropic specific timeout
--deepseek-timeout 5            # DeepSeek specific timeout
--ollama-timeout 10             # Ollama specific timeout
--lmstudio-timeout 10           # LM Studio specific timeout
--openwebui-timeout 10          # OpenWebUI specific timeout
--embedding-timeout 10          # Embedding service timeout
```

*Max Token Arguments:*
```bash
# Token limits per provider
--openai-max-tokens 4000        # OpenAI token limit
--anthropic-max-tokens 4000     # Anthropic token limit
--deepseek-max-tokens 4000      # DeepSeek token limit
--lmstudio-max-tokens 4096      # LM Studio token limit
--openwebui-max-tokens 4096     # OpenWebUI token limit
```

**Example Configurations:**
```bash
# Basic setup with custom OpenAI settings
dotnet run "C:\Docs" "gpt-4" "mcp" --openai-timeout 10 --openai-max-tokens 8000

# Multiple provider configuration
dotnet run "C:\Docs" "mcp" --openai-timeout 8 --anthropic-timeout 12 --deepseek-timeout 6

# High-performance setup with increased limits
dotnet run "C:\Docs" "claude-3-sonnet" "mcp" --anthropic-timeout 15 --anthropic-max-tokens 8000

# Local provider configuration
dotnet run "C:\Docs" "llama3.2" "mcp" --ollama-timeout 20 --lmstudio-timeout 15
```

**Configuration Validation:**
- Timeout values must be positive integers (minutes)
- Max tokens must be within provider limits
- Invalid configurations will use default values
- Settings are validated during provider switching

#### IAiProvider Interface Usage

The `IAiProvider` interface is the core abstraction for all AI providers in HlpAI. It provides a unified API for interacting with different AI services, whether local (Ollama, LM Studio, OpenWebUI) or cloud-based (OpenAI, Anthropic, DeepSeek).

**Interface Definition:**
```csharp
public interface IAiProvider : IDisposable
{
    // Core Methods
    Task<string> GenerateAsync(string prompt, string? context = null, double temperature = 0.7);
    Task<bool> IsAvailableAsync();
    Task<List<string>> GetModelsAsync();
    
    // Provider Information
    AiProviderType ProviderType { get; }
    string ProviderName { get; }
    string DefaultModel { get; }
    string BaseUrl { get; }
    string CurrentModel { get; }
    
    // Capability Flags
    bool SupportsDynamicModelSelection { get; }
    bool SupportsEmbedding { get; }
}
```

**Provider Types:**
```csharp
public enum AiProviderType
{
    Ollama,      // Local model runner
    LmStudio,    // Local API server with GUI
    OpenWebUi,   // Web-based model management
    OpenAI,      // Cloud-based AI service (GPT models)
    Anthropic,   // Cloud-based AI service (Claude models)
    DeepSeek     // Cloud-based AI service
}
```

**Creating Provider Instances:**

*Local Providers (no API key required):*
```csharp
// Using AiProviderFactory
var ollamaProvider = AiProviderFactory.CreateProvider(
    AiProviderType.Ollama,
    "llama3.2",
    "http://localhost:11434",
    logger,
    config);

var lmStudioProvider = AiProviderFactory.CreateProvider(
    AiProviderType.LmStudio,
    "default",
    "http://localhost:1234",
    logger,
    config);

var openWebUiProvider = AiProviderFactory.CreateProvider(
    AiProviderType.OpenWebUi,
    "default",
    "http://localhost:3000",
    logger,
    config);
```

*Cloud Providers (API key required):*
```csharp
// Using AiProviderFactory with API key
var openAiProvider = AiProviderFactory.CreateProvider(
    AiProviderType.OpenAI,
    "gpt-4o-mini",
    "https://api.openai.com",
    "your-api-key",
    logger,
    config);

var anthropicProvider = AiProviderFactory.CreateProvider(
    AiProviderType.Anthropic,
    "claude-3-5-haiku-20241022",
    "https://api.anthropic.com",
    "your-api-key",
    logger,
    config);

var deepSeekProvider = AiProviderFactory.CreateProvider(
    AiProviderType.DeepSeek,
    "deepseek-chat",
    "https://api.deepseek.com/v1",
    "your-api-key",
    logger,
    config);
```

**Basic Usage Examples:**

*Text Generation:*
```csharp
// Simple text generation
string response = await provider.GenerateAsync("Explain quantum computing");

// With context and temperature
string contextualResponse = await provider.GenerateAsync(
    "What are the key benefits?",
    "Previous discussion about cloud computing advantages",
    0.3  // Lower temperature for more focused responses
);
```

*Provider Availability Check:*
```csharp
// Check if provider is available before use
if (await provider.IsAvailableAsync())
{
    var response = await provider.GenerateAsync("Hello, AI!");
    Console.WriteLine($"Response: {response}");
}
else
{
    Console.WriteLine($"Provider {provider.ProviderName} is not available");
}
```

*Model Management:*
```csharp
// Get available models (if supported)
if (provider.SupportsDynamicModelSelection)
{
    var models = await provider.GetModelsAsync();
    Console.WriteLine($"Available models: {string.Join(", ", models)}");
}
else
{
    Console.WriteLine($"Using fixed model: {provider.CurrentModel}");
}
```

**Provider-Specific Features:**

*Local Providers:*
- **Ollama**: Supports dynamic model selection, can pull models on-demand
- **LM Studio**: GUI-based model management, supports dynamic selection
- **OpenWebUI**: Web interface for model management, dynamic selection

*Cloud Providers:*
- **OpenAI**: Dynamic model selection, rate limiting, usage tracking
- **Anthropic**: Fixed model configuration, advanced reasoning capabilities
- **DeepSeek**: Dynamic model selection, competitive pricing

**Advanced Usage with Extensions:**

*Creating Operation Context:*
```csharp
// Create context from app configuration
var context = provider.CreateContextFromConfig(appConfig, "Your prompt here");

// Context includes provider-specific timeouts and token limits
Console.WriteLine($"Max Tokens: {context.MaxTokens}");
Console.WriteLine($"Timeout: {context.TimeoutMs}ms");
```

*Provider Information:*
```csharp
// Get detailed provider information
var providerInfo = AiProviderFactory.GetProviderInfo(provider.ProviderType);
Console.WriteLine($"Name: {providerInfo.Name}");
Console.WriteLine($"Description: {providerInfo.Description}");
Console.WriteLine($"Default URL: {providerInfo.DefaultUrl}");
Console.WriteLine($"Default Model: {providerInfo.DefaultModel}");
```

**Error Handling Best Practices:**

```csharp
try
{
    // Always check availability first
    if (!await provider.IsAvailableAsync())
    {
        throw new InvalidOperationException($"Provider {provider.ProviderName} is not available");
    }
    
    // Generate response with timeout handling
    var response = await provider.GenerateAsync(prompt, context, temperature);
    return response;
}
catch (HttpRequestException ex)
{
    // Network connectivity issues
    logger?.LogError(ex, "Network error communicating with {Provider}", provider.ProviderName);
    throw;
}
catch (TaskCanceledException ex)
{
    // Timeout issues
    logger?.LogError(ex, "Timeout communicating with {Provider}", provider.ProviderName);
    throw;
}
catch (ArgumentException ex)
{
    // Invalid API key or configuration
    logger?.LogError(ex, "Configuration error for {Provider}", provider.ProviderName);
    throw;
}
finally
{
    // Proper disposal
    provider?.Dispose();
}
```

**Cloud Provider Specific Interface:**

For cloud providers, there's an extended interface `ICloudAiProvider`:

```csharp
public interface ICloudAiProvider : IAiProvider
{
    string ApiKey { get; }
    Task<bool> ValidateApiKeyAsync();
    Task<ApiUsageInfo?> GetUsageInfoAsync();
    Task<RateLimitInfo?> GetRateLimitInfoAsync();
}
```

*Usage with Cloud Providers:*
```csharp
if (provider is ICloudAiProvider cloudProvider)
{
    // Validate API key
    bool isValid = await cloudProvider.ValidateApiKeyAsync();
    if (!isValid)
    {
        throw new UnauthorizedAccessException("Invalid API key");
    }
    
    // Check usage limits
    var usage = await cloudProvider.GetUsageInfoAsync();
    if (usage != null)
    {
        Console.WriteLine($"Requests used: {usage.RequestsUsed}/{usage.RequestsLimit}");
        Console.WriteLine($"Tokens used: {usage.TokensUsed}/{usage.TokensLimit}");
    }
    
    // Check rate limits
    var rateLimit = await cloudProvider.GetRateLimitInfoAsync();
    if (rateLimit != null)
    {
        Console.WriteLine($"Requests remaining: {rateLimit.RequestsRemaining}");
        Console.WriteLine($"Tokens remaining: {rateLimit.TokensRemaining}");
    }
}
```

**Integration with HlpAI Services:**

The IAiProvider interface integrates seamlessly with other HlpAI services:

```csharp
// Integration with RAG service
var ragService = new RagService(provider, vectorStore, embeddingService);
var ragResponse = await ragService.AskWithContextAsync("Your question", documents);

// Integration with document analysis
var analysisService = new DocumentAnalysisService(provider);
var analysis = await analysisService.AnalyzeDocumentAsync(documentContent);

// Integration with MCP server
var mcpServer = new EnhancedMcpRagServer(provider, ragService, documentService);
await mcpServer.StartAsync();
```

### Provider Auto-Detection and Fallback Mechanisms

HlpAI includes sophisticated provider auto-detection and fallback mechanisms to ensure reliable AI service availability across different environments and configurations.

#### Automatic Provider Detection

The `AiProviderFactory.DetectAvailableProvidersAsync()` method automatically scans and tests all configured AI providers:

```csharp
// Detect all available providers
var availableProviders = await AiProviderFactory.DetectAvailableProvidersAsync(logger);

foreach (var (providerType, isAvailable) in availableProviders)
{
    var info = AiProviderFactory.GetProviderInfo(providerType);
    Console.WriteLine($"{info.Name}: {(isAvailable ? "✅ Available" : "❌ Unavailable")}");
}
```

**Detection Process:**
1. **Local Provider Scanning**: Tests connectivity to local services (Ollama, LM Studio, OpenWebUI)
2. **Cloud Provider Validation**: Validates API keys and service availability for cloud providers
3. **Model Availability Check**: Verifies that configured models are accessible
4. **Connection Testing**: Performs actual connectivity tests with timeout handling
5. **Status Reporting**: Returns detailed availability status for each provider

**Detection Output Example:**
```
🔍 Detecting Available AI Providers...
✅ Ollama: Available (http://localhost:11434) - Model: llama3.2
❌ LM Studio: Unavailable (Connection refused)
❌ OpenWebUI: Unavailable (Service not running)
✅ OpenAI: Available - Model: gpt-4o-mini
✅ Anthropic: Available - Model: claude-3-5-haiku-20241022
❌ DeepSeek: Unavailable (Invalid API key)

Found 2 available providers out of 6 configured.
```

#### Provider Fallback Mechanisms

**Startup Fallback Strategy:**

When the configured default provider is unavailable, HlpAI implements intelligent fallback:

```csharp
// Startup flow with fallback
var config = await ConfigurationService.LoadConfigurationAsync();
var primaryProvider = AiProviderFactory.CreateProvider(config.LastProvider, /* ... */);

if (!await primaryProvider.IsAvailableAsync())
{
    logger.LogWarning("Primary provider {Provider} unavailable, attempting fallback", 
        config.LastProvider);
    
    // Detect available alternatives
    var availableProviders = await AiProviderFactory.DetectAvailableProvidersAsync(logger);
    var fallbackProvider = SelectBestFallbackProvider(availableProviders);
    
    if (fallbackProvider != null)
    {
        logger.LogInformation("Falling back to {Provider}", fallbackProvider.ProviderType);
        return fallbackProvider;
    }
    else
    {
        logger.LogError("No available AI providers found");
        throw new InvalidOperationException("No AI providers available");
    }
}
```

**Fallback Priority Order:**
1. **Local Providers First**: Ollama → LM Studio → OpenWebUI
2. **Cloud Providers**: OpenAI → Anthropic → DeepSeek
3. **Provider Preference**: Respects user's last successful provider choice
4. **Model Compatibility**: Considers model availability and compatibility

#### Runtime Provider Management

**Dynamic Provider Switching:**

```csharp
// Runtime provider switching with validation
public async Task<bool> SwitchProviderAsync(AiProviderType newProviderType)
{
    var newProvider = AiProviderFactory.CreateProvider(newProviderType, /* ... */);
    
    // Validate new provider before switching
    if (!await newProvider.IsAvailableAsync())
    {
        logger.LogWarning("Cannot switch to {Provider}: not available", newProviderType);
        return false;
    }
    
    // Test with a simple query
    try
    {
        await newProvider.GenerateAsync("Test connection");
        
        // Switch successful - update configuration
        _currentProvider?.Dispose();
        _currentProvider = newProvider;
        
        await ConfigurationService.UpdateProviderAsync(newProviderType);
        logger.LogInformation("Successfully switched to {Provider}", newProviderType);
        return true;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to switch to {Provider}", newProviderType);
        newProvider.Dispose();
        return false;
    }
}
```

**Provider Health Monitoring:**

```csharp
// Continuous health monitoring
public async Task MonitorProviderHealthAsync(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        if (!await _currentProvider.IsAvailableAsync())
        {
            logger.LogWarning("Current provider {Provider} became unavailable", 
                _currentProvider.ProviderType);
            
            // Attempt automatic failover
            var fallbackSuccess = await AttemptAutomaticFailoverAsync();
            if (!fallbackSuccess)
            {
                logger.LogError("No fallback providers available - service degraded");
            }
        }
        
        await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
    }
}
```

#### Configuration Persistence

**Provider Selection Persistence:**

Successful provider switches are automatically persisted:

```csharp
// Configuration update after successful switch
public async Task UpdateProviderConfigurationAsync(AiProviderType providerType, string model)
{
    var config = await ConfigurationService.LoadConfigurationAsync();
    config.LastProvider = providerType;
    config.LastModel = model;
    config.LastSuccessfulConnection = DateTime.UtcNow;
    
    await ConfigurationService.SaveConfigurationAsync(config);
    logger.LogInformation("Provider configuration updated: {Provider} with model {Model}", 
        providerType, model);
}
```

**Startup Provider Resolution:**

```csharp
// Intelligent startup provider selection
public async Task<IAiProvider> ResolveStartupProviderAsync()
{
    var config = await ConfigurationService.LoadConfigurationAsync();
    
    // Try last successful provider first
    if (config.LastProvider.HasValue)
    {
        var lastProvider = AiProviderFactory.CreateProvider(config.LastProvider.Value, /* ... */);
        if (await lastProvider.IsAvailableAsync())
        {
            logger.LogInformation("Using last successful provider: {Provider}", 
                config.LastProvider.Value);
            return lastProvider;
        }
        lastProvider.Dispose();
    }
    
    // Fallback to auto-detection
    logger.LogInformation("Last provider unavailable, detecting alternatives...");
    var availableProviders = await AiProviderFactory.DetectAvailableProvidersAsync(logger);
    
    return SelectOptimalProvider(availableProviders, config.PreferredProviderOrder);
}
```

#### Error Handling and Recovery

**Graceful Degradation:**

```csharp
// Graceful handling of provider failures
public async Task<string> GenerateWithFallbackAsync(string prompt, string? context = null)
{
    var maxRetries = 3;
    var currentRetry = 0;
    
    while (currentRetry < maxRetries)
    {
        try
        {
            if (await _currentProvider.IsAvailableAsync())
            {
                return await _currentProvider.GenerateAsync(prompt, context);
            }
            else
            {
                logger.LogWarning("Provider {Provider} unavailable, attempting fallback", 
                    _currentProvider.ProviderType);
                
                if (await AttemptAutomaticFailoverAsync())
                {
                    continue; // Retry with new provider
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error with provider {Provider}, attempt {Retry}/{MaxRetries}", 
                _currentProvider.ProviderType, currentRetry + 1, maxRetries);
            
            if (currentRetry == maxRetries - 1)
            {
                throw; // Re-throw on final attempt
            }
            
            // Try fallback on error
            await AttemptAutomaticFailoverAsync();
        }
        
        currentRetry++;
        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, currentRetry))); // Exponential backoff
    }
    
    throw new InvalidOperationException("All provider fallback attempts failed");
}
```

**Command-Line Provider Detection:**

```bash
# Detect available providers
dotnet run --detect-providers

# Output:
🔍 AI Provider Detection Results:
✅ Ollama (http://localhost:11434) - llama3.2
❌ LM Studio (http://localhost:1234) - Connection refused
❌ OpenWebUI (http://localhost:3000) - Service not running  
✅ OpenAI - gpt-4o-mini (API key valid)
✅ Anthropic - claude-3-5-haiku-20241022 (API key valid)
❌ DeepSeek - Invalid or missing API key

Recommendation: Use Ollama (local) or OpenAI (cloud) for best performance.
```

**Best Practices for Provider Management:**

1. **Always Check Availability**: Use `IsAvailableAsync()` before making requests
2. **Implement Retry Logic**: Handle transient failures with exponential backoff
3. **Monitor Provider Health**: Regularly check provider status in long-running applications
4. **Configure Multiple Providers**: Set up both local and cloud providers for redundancy
5. **Handle API Key Rotation**: Implement secure API key management and rotation
6. **Log Provider Events**: Maintain detailed logs for troubleshooting and monitoring
7. **Test Provider Switching**: Regularly test fallback mechanisms in development
8. **Consider Cost Implications**: Balance between local (free) and cloud (paid) providers

### Model Compatibility Handling

HlpAI provides comprehensive model compatibility handling to ensure seamless operation across different AI providers with varying model support capabilities.

**Dynamic vs. Fixed Model Selection:**

Providers are categorized based on their model selection capabilities:

*Dynamic Model Selection (Supports `GetModelsAsync()`):
- **Ollama**: Can fetch available models from local instance
- **LM Studio**: Retrieves models from GUI-managed collection
- **OpenWebUI**: Accesses models through web interface API
- **OpenAI**: Lists available GPT models via API
- **DeepSeek**: Provides model list through API

*Fixed Model Configuration:
- **Anthropic**: Uses predefined model (claude-3-5-haiku-20241022)

**Model Validation Process:**

```csharp
// Check if provider supports dynamic model selection
if (provider.SupportsDynamicModelSelection)
{
    var models = await provider.GetModelsAsync();
    Console.WriteLine($"Available models: {string.Join(", ", models)}");
    
    // Validate current model is available
    if (!models.Contains(provider.CurrentModel))
    {
        Console.WriteLine($"⚠️ Warning: Model '{provider.CurrentModel}' not available");
        Console.WriteLine($"Consider switching to: {string.Join(", ", models.Take(3))}");
    }
}
else
{
    Console.WriteLine($"Using fixed model: {provider.CurrentModel}");
}
```

**Provider Switching with Model Compatibility:**

When switching providers, the system automatically handles model compatibility:

```csharp
// Automatic model assignment during provider switch
config.LastModel = selectedProvider switch
{
    AiProviderType.Ollama => config.OllamaDefaultModel ?? "llama3.2",
    AiProviderType.LmStudio => config.LmStudioDefaultModel ?? "default",
    AiProviderType.OpenWebUi => config.OpenWebUiDefaultModel ?? "default",
    AiProviderType.OpenAI => config.OpenAiDefaultModel ?? "gpt-4o-mini",
    AiProviderType.Anthropic => config.AnthropicDefaultModel ?? "claude-3-5-haiku-20241022",
    AiProviderType.DeepSeek => config.DeepSeekDefaultModel ?? "deepseek-chat",
    _ => "default"
};
```

**Model Compatibility Warnings:**

The system provides intelligent warnings when model compatibility issues are detected:

```
🔌 Testing connection to OpenAI...
✅ OpenAI connected successfully!
Fetching available models...
Available models (15): gpt-4o, gpt-4o-mini, gpt-4-turbo, gpt-3.5-turbo, text-embedding-3-large...
⚠️ Warning: Configured model 'llama3.2' is not available on this provider
Consider switching to one of the available models.
```

**Interactive Model Selection:**

For providers supporting dynamic model selection, users can interactively choose models:

```
🤖 Change AI Model
==================
✅ Ollama connected! Available models:

  1. llama3.2 ✅ (Current)
  2. llama3.1
  3. codellama
  4. mistral
  5. Enter custom model name

Select a model (1-5, or 'q' to quit): 3
✅ Model changed from 'llama3.2' to 'codellama'
```

**Menu Adaptation Based on Provider Capabilities:**

The configuration menu intelligently adapts based on provider capabilities:

```csharp
// Menu items are conditionally displayed
if (provider.SupportsDynamicModelSelection)
{
    Console.WriteLine("2. Change AI Model (Current: {0})", config.LastModel);
}
// Option 2 is skipped for providers like Anthropic
```

**Model Compatibility Matrix:**

| Provider | Dynamic Selection | Default Model | Model Source |
|----------|------------------|---------------|---------------|
| Ollama | ✅ Yes | llama3.2 | Local instance API |
| LM Studio | ✅ Yes | default | GUI model manager |
| OpenWebUI | ✅ Yes | default | Web interface API |
| OpenAI | ✅ Yes | gpt-4o-mini | OpenAI API |
| Anthropic | ❌ No | claude-3-5-haiku-20241022 | Fixed configuration |
| DeepSeek | ✅ Yes | deepseek-chat | DeepSeek API |

**Error Handling for Model Issues:**

```csharp
try
{
    var models = await provider.GetModelsAsync();
    if (models.Count == 0)
    {
        Console.WriteLine("⚠️ No models found (provider may be running but no models loaded)");
        return;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error retrieving models: {ex.Message}");
    Console.WriteLine("Troubleshooting:");
    Console.WriteLine("  • Ensure provider service is running");
    Console.WriteLine("  • Check network connectivity");
    Console.WriteLine("  • Verify API credentials (for cloud providers)");
}
```

**Best Practices for Model Compatibility:**

1. **Always Check Model Support**: Verify `SupportsDynamicModelSelection` before attempting model operations
2. **Handle Model Unavailability**: Implement fallback logic when configured models aren't available
3. **Provide User Feedback**: Show clear warnings and suggestions for model compatibility issues
4. **Test Model Switching**: Regularly test model changes across different providers
5. **Configure Appropriate Defaults**: Set sensible default models for each provider type
6. **Monitor Model Availability**: Check model availability during provider health checks
7. **Document Model Requirements**: Clearly specify model requirements for different use cases

### Configuration Persistence

HlpAI automatically saves and restores user preferences across application sessions using a SQLite database for reliable configuration persistence.

**Persistent Configuration Settings:**

```csharp
public class AppConfiguration
{
    // Directory and File Management
    public string? LastDirectory { get; set; }
    public bool RememberLastDirectory { get; set; } = true;
    
    // AI Provider Settings
    public AiProviderType LastProvider { get; set; } = AiProviderType.Ollama;
    public string? LastModel { get; set; }
    public bool RememberLastProvider { get; set; } = true;
    public bool RememberLastModel { get; set; } = true;
    
    // Provider URLs
    public string? OllamaUrl { get; set; } = "http://localhost:11434";
    public string? LmStudioUrl { get; set; } = "http://localhost:1234";
    public string? OpenWebUiUrl { get; set; } = "http://localhost:3000";
    
    // API Keys (encrypted storage)
    public string? OpenAiApiKey { get; set; }
    public string? AnthropicApiKey { get; set; }
    public string? DeepSeekApiKey { get; set; }
    
    // Operation Mode
    public OperationMode LastOperationMode { get; set; } = OperationMode.RAG;
    public bool RememberLastOperationMode { get; set; } = true;
    
    // Timeout and Token Limits
    public int AiProviderTimeoutMinutes { get; set; } = 10;
    public int OpenAiMaxTokens { get; set; } = 4000;
    public int AnthropicMaxTokens { get; set; } = 4000;
    // ... additional provider-specific settings
}
```

**Automatic Configuration Saving:**

Configuration is automatically saved when:
- User selects a new AI provider
- User changes AI model
- User switches operation mode
- User updates provider URLs or API keys
- User modifies timeout or token settings

```csharp
// Configuration is saved after successful provider switch
if (validationResult.IsValid)
{
    config.LastProvider = newProvider;
    config.LastModel = selectedModel;
    ConfigurationService.SaveConfiguration(config);
    Console.WriteLine("✅ Configuration saved. Settings will be remembered for next session.");
}
```

**Configuration Loading on Startup:**

```csharp
// Load saved configuration
var config = ConfigurationService.LoadConfiguration(logger);

// Apply remembered settings if enabled
if (config.RememberLastDirectory && !string.IsNullOrEmpty(config.LastDirectory))
{
    Console.WriteLine($"💡 Last used directory: {config.LastDirectory}");
    Console.WriteLine("Press Enter to use this directory, or type a new path:");
}

if (config.RememberLastProvider)
{
    Console.WriteLine($"🤖 Using remembered AI provider: {config.LastProvider}");
}

if (config.RememberLastModel && !string.IsNullOrEmpty(config.LastModel))
{
    Console.WriteLine($"🧠 Using remembered model: {config.LastModel}");
}
```

**SQLite Database Storage:**

Configuration is stored in a SQLite database with the following features:
- **Encrypted API Keys**: Sensitive data is encrypted using AES-256
- **Atomic Updates**: Configuration changes are saved atomically
- **Version Management**: Configuration format versioning for future migrations
- **Backup and Recovery**: Database can be backed up and restored

```csharp
// Database location
var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "HlpAI",
    "config.db"
);

// Configuration service handles all persistence
public static bool SaveConfiguration(AppConfiguration config, ILogger? logger = null)
{
    var sqliteConfig = SqliteConfigurationService.GetInstance(logger);
    config.LastUpdated = DateTime.UtcNow;
    return sqliteConfig.SaveAppConfigurationAsync(config).GetAwaiter().GetResult();
}
```

**Configuration Categories:**

Settings are organized into logical categories:

| Category | Settings | Purpose |
|----------|----------|----------|
| `general` | LastDirectory, RememberLastDirectory | File and directory preferences |
| `ai_provider` | LastProvider, LastModel, Remember* flags | AI provider and model settings |
| `provider_urls` | OllamaUrl, LmStudioUrl, OpenWebUiUrl | Provider connection endpoints |
| `api_keys` | OpenAiApiKey, AnthropicApiKey, DeepSeekApiKey | Encrypted API credentials |
| `timeouts` | Provider-specific timeout values | Connection timeout settings |
| `tokens` | Provider-specific max token limits | Token limit configurations |
| `operation` | LastOperationMode, RememberLastOperationMode | Application operation preferences |

**Memory Management Options:**

Users can control what settings are remembered:

```bash
# Configuration menu options
1. Remember last directory: Yes/No
2. Remember last AI provider: Yes/No  
3. Remember last AI model: Yes/No
4. Remember last operation mode: Yes/No
```

**Configuration Validation:**

```csharp
// Validate configuration on load
var config = ConfigurationService.LoadConfiguration();

// Check if remembered provider is still available
if (config.RememberLastProvider)
{
    var provider = AiProviderFactory.CreateProvider(config.LastProvider, config);
    if (!await provider.IsAvailableAsync())
    {
        Console.WriteLine($"⚠️ Remembered provider {config.LastProvider} is not available");
        Console.WriteLine("Falling back to auto-detection...");
        await DetectAndConfigureAvailableProviderAsync(config);
    }
}
```

**Configuration Export/Import:**

```csharp
// Export configuration (excluding sensitive data)
public static string ExportConfiguration(bool includeSensitiveData = false)
{
    var config = LoadConfiguration();
    if (!includeSensitiveData)
    {
        // Remove API keys and other sensitive data
        config.OpenAiApiKey = null;
        config.AnthropicApiKey = null;
        config.DeepSeekApiKey = null;
    }
    return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
}

// Import configuration
public static bool ImportConfiguration(string jsonConfig)
{
    var config = JsonSerializer.Deserialize<AppConfiguration>(jsonConfig);
    return SaveConfiguration(config);
}
```

**Best Practices for Configuration Management:**

1. **Regular Backups**: Backup the configuration database periodically
2. **Secure API Keys**: Always use encrypted storage for sensitive data
3. **Validate on Load**: Check configuration validity when loading
4. **Graceful Degradation**: Fall back to defaults when configuration is invalid
5. **User Control**: Allow users to control what settings are remembered
6. **Version Management**: Handle configuration format changes gracefully
7. **Atomic Updates**: Ensure configuration changes are saved atomically
8. **Error Recovery**: Provide recovery options when configuration is corrupted

### Server Initialization

**Typical Startup Output:**
```
🎯 HlpAI - MCP Server Mode
==========================
📁 Document Directory: C:\YourDocuments
🤖 AI Provider: Ollama (http://localhost:11434)
🧠 Model: llama3.2
🎯 Operation Mode: MCP Server

Initializing document processing...
Found 156 files to process
✅ RAG initialization complete. Indexed 234 chunks from 45 files.

🖥️  MCP Server Ready
====================
Server is now running and ready for MCP requests.
Available on stdio for MCP protocol communication.

📋 Available MCP Methods:
  • resources/list     - List all available document resources
  • resources/read     - Read content of a specific document
  • tools/list         - List all available AI tools
  • tools/call         - Execute an AI tool

🛠️ Available Tools:
  • search_files       - Search files by text content
  • ask_ai             - Ask AI questions with optional RAG
  • analyze_file       - AI-powered file analysis
  • rag_search         - Semantic search using vectors (RAG/Hybrid modes)
  • rag_ask            - RAG-enhanced AI questioning (RAG/Hybrid modes)
  • reindex_documents  - Rebuild vector index (RAG/Hybrid modes)
  • indexing_report    - Get indexing status report (RAG/Hybrid modes)

✅ Server initialized successfully and ready for requests.
```

## 📋 MCP Protocol Implementation

### Supported MCP Methods

#### **resources/list**
List all available document resources in the current directory.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": "1",
  "method": "resources/list"
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "1",
  "result": {
    "resources": [
      {
        "file_uri": "file:///user-manual.pdf",
        "name": "user-manual.pdf",
        "description": "PDF document - User Manual",
        "type": "document",
        "size": 2150000,
        "indexed": true
      },
      {
        "uri": "file:///api-docs.html", 
        "name": "api-docs.html",
        "description": "HTML document - API Documentation",
        "type": "document",
        "size": 450000,
        "indexed": true
      }
    ]
  }
}
```

#### **resources/read**
Read content of a specific document.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": "2",
  "method": "resources/read",
  "params": {
    "uri": "file:///user-manual.pdf"
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "2",
  "result": {
    "uri": "file:///user-manual.pdf",
    "name": "user-manual.pdf",
    "content": "User Manual\n===========\n\nThis document provides comprehensive guidance for setting up and using the application...",
    "mimeType": "text/plain",
    "size": 2150000
  }
}
```

#### **tools/list**
List all available AI tools.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": "3",
  "method": "tools/list"
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "3",
  "result": {
    "tools": [
      {
        "name": "search_files",
        "description": "Search for files containing specific text",
        "inputSchema": {
          "type": "object",
          "properties": {
            "query": {"type": "string", "description": "Search query"},
            "fileTypes": {"type": "array", "items": {"type": "string"}, "description": "File extensions to search"}
          },
          "required": ["query"]
        }
      },
      {
        "name": "ask_ai",
        "description": "Ask AI a question about file contents using the configured AI provider",
        "inputSchema": {
          "type": "object",
          "properties": {
            "question": {"type": "string", "description": "Question to ask the AI"},
            "context": {"type": "string", "description": "Optional context or file content to provide to the AI"},
            "temperature": {"type": "number", "description": "Temperature for AI response (0.0-1.0)", "default": 0.7},
            "use_rag": {"type": "boolean", "description": "Whether to use RAG for context retrieval", "default": true}
          },
          "required": ["question"]
        }
      },
      {
         "name": "analyze_file",
         "description": "Analyze a specific file using AI",
         "inputSchema": {
           "type": "object",
           "properties": {
             "file_uri": {"type": "string", "description": "URI of the file to analyze"},
             "analysis_type": {"type": "string", "description": "Type of analysis (summary, key_points, questions, etc.)"},
             "use_rag": {"type": "boolean", "description": "Whether to use RAG for enhanced context", "default": true}
           },
           "required": ["file_uri", "analysis_type"]
         }
       }
    ]
  }
}
```

**Note:** In RAG or Hybrid modes, additional tools are available:
- `rag_search` - Semantic search using RAG vector store
- `rag_ask` - Ask AI with RAG-enhanced context retrieval  
- `reindex_documents` - Rebuild the RAG vector store index
- `indexing_report` - Get detailed report of indexed and non-indexed files

#### **tools/call**
Execute an AI tool with specific parameters.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": "4",
  "method": "tools/call",
  "params": {
    "name": "ask_ai",
    "arguments": {
      "question": "How do I configure the database?",
      "temperature": 0.3,
      "use_rag": true
    }
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "4",
  "result": {
    "content": "Based on your documentation, configure the database by:\n1. Edit the config.json file...\n2. Set the connection string...\n3. Ensure the database service is running...",
    "tool": "ask_ai",
    "success": true
  }
}
```

**Error Handling:**
```json
{
  "jsonrpc": "2.0",
  "error": {
    "code": -32602,
    "message": "Invalid params",
    "data": "File URI is required"
  }
}
```

**Common Error Scenarios:**
- Missing required `file_uri` parameter
- Invalid file URI format (must start with "file:///")
- File not found or inaccessible
- Unsupported file type
- File extraction failure (corrupted file, permission issues)
- Invalid `analysis_type` (not in supported list)
- Invalid `temperature` value (outside 0.0-2.0 range)
- AI provider timeout or unavailability
- RAG enhancement unavailable when requested

## 🛠️ Available Tools Reference

### **search_files** - Text-based File Search
Search files by text content with configurable result limits.

**Parameters:**
- `query` (required): Search query text
- `maxResults` (optional): Maximum number of results to return (default: 10)

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "search_files",
    "arguments": {
      "query": "authentication setup",
      "maxResults": 5
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "results": [
      {
        "file": "security-guide.pdf",
        "matches": 3,
        "snippet": "User authentication is handled through JWT tokens...",
        "page": 12
      },
      {
        "file": "api-docs.html",
        "matches": 2, 
        "snippet": "Authentication endpoints: /auth/login, /auth/verify...",
        "line": 45
      }
    ],
    "totalMatches": 8,
    "filesSearched": 156
  }
}
```

### **search_files**
Search for files containing specific text.

**Parameters:**
- `query` (string, required): Search query
- `fileTypes` (array, optional): File extensions to search

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "id": "11",
  "method": "tools/call",
  "params": {
    "name": "search_files",
    "arguments": {
      "query": "authentication",
      "fileTypes": [".md", ".txt", ".cs"]
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "11",
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Found 3 files containing 'authentication':\n\n1. docs/security.md - Line 15: 'User authentication is required'\n2. src/Auth.cs - Line 42: 'public class AuthenticationService'\n3. README.md - Line 8: 'Authentication setup instructions'"
      }
    ]
  }
}
```

### **ask_ai** - AI-Powered Question Answering
Ask AI questions about file contents using the configured AI provider with optional RAG enhancement.

**Parameters:**
- `question` (required): Question to ask the AI (string)
- `context` (optional): Additional context or file content to provide to the AI (string)
- `temperature` (optional): Controls response creativity and randomness (number, 0.0-2.0, default: 0.7)
  - `0.0-0.3`: Factual, deterministic responses
  - `0.4-0.7`: Balanced creativity and accuracy
  - `0.8-2.0`: More creative and varied responses
- `use_rag` (optional): Enable RAG for enhanced context retrieval from indexed documents (boolean, default: true)

**Validation Rules:**
- `question`: Must be non-empty string
- `temperature`: Must be between 0.0 and 2.0
- `context`: Optional, no length restrictions
- `use_rag`: Boolean value, automatically enabled in RAG/Hybrid modes

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "ask_ai",
    "arguments": {
      "question": "What are the security best practices?",
      "context": "Focus on authentication and authorization",
      "temperature": 0.2,
      "use_rag": true
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "answer": "Based on your security documentation, here are the recommended best practices:\n\n1. **Authentication**: Use JWT tokens with strong secret keys (minimum 256 bits)\n2. **Authorization**: Implement role-based access control...\n3. **Encryption**: Enable TLS 1.3 for all communications...\n\nSpecific recommendations from your documents:\n• Rotate JWT secrets every 90 days (security-guide.pdf)\n• Use environment variables for sensitive configuration...",
    "sources": [
      "security-guide.pdf",
      "config-reference.txt"
    ],
    "temperature": 0.2,
    "ragEnhanced": true
  }
}
```

**Error Handling:**
```json
{
  "jsonrpc": "2.0",
  "error": {
    "code": -32602,
    "message": "Invalid params",
    "data": "Question is required"
  }
}
```

**Common Error Scenarios:**
- Missing required `question` parameter
- Invalid `temperature` value (outside 0.0-2.0 range)
- AI provider timeout or unavailability
- RAG index not available when `use_rag` is true
- Network connectivity issues with AI provider

## 🧠 RAG-Enhanced Questioning Features

RAG (Retrieval-Augmented Generation) enhances AI responses by incorporating relevant context from your indexed documents. This section covers the comprehensive RAG capabilities available in HlpAI.

### **RAG Architecture Overview**

**Components:**
1. **Vector Store**: Stores document embeddings for semantic search
2. **Embedding Model**: Converts text to high-dimensional vectors
3. **Similarity Engine**: Performs cosine similarity calculations
4. **Context Retrieval**: Finds and ranks relevant document chunks
5. **AI Integration**: Combines context with user queries for enhanced responses

**RAG Workflow:**
```
User Query → Vector Embedding → Semantic Search → Context Retrieval → AI Enhancement → Response
```

### **RAG-Enhanced Tools Integration**

**1. Direct RAG Search (`rag_search`)**
- Pure semantic search without AI generation
- Returns ranked document chunks with similarity scores
- Ideal for finding specific information or exploring content
- Supports file filtering and similarity thresholds

**2. RAG-Enhanced AI Questioning (`rag_ask`)**
- Combines semantic search with AI generation
- Automatically retrieves relevant context for questions
- Provides AI-generated answers enhanced with document knowledge
- Best for getting comprehensive, contextual answers

**3. AI Tools with RAG Enhancement (`ask_ai`, `analyze_file`)**
- Optional RAG enhancement via `use_rag` parameter
- Maintains tool functionality while adding document context
- Seamless integration with existing workflows
- Flexible RAG activation based on needs

### **RAG Configuration & Optimization**

**Key Parameters:**
- `top_k`: Controls amount of context retrieved (1-50 for search, 1-20 for questioning)
- `min_similarity`: Filters context by relevance threshold (0.0-1.0)
- `temperature`: Balances creativity vs. factual accuracy in AI responses
- `file_filters`: Restricts search to specific file types or patterns

**Performance Tuning:**
- **High Precision**: `min_similarity=0.7+`, `top_k=3-5`
- **Balanced Retrieval**: `min_similarity=0.5-0.7`, `top_k=5-8`
- **Broad Context**: `min_similarity=0.3-0.5`, `top_k=8-15`
- **Exploratory Search**: `min_similarity=0.1-0.3`, `top_k=10-20`

### **RAG Best Practices**

**Query Optimization:**
- Use specific, well-formed questions for better context retrieval
- Include domain-specific terminology when available
- Combine multiple concepts for richer context (e.g., "SSL certificate installation process")
- Avoid overly broad queries that may retrieve irrelevant context

**Context Management:**
- Monitor similarity scores to assess context relevance
- Adjust `min_similarity` based on document quality and query specificity
- Use `file_filters` to focus on relevant document types
- Balance `top_k` to avoid context overload while ensuring completeness

**Response Quality:**
- Lower `temperature` (0.1-0.3) for factual, technical questions
- Higher `temperature` (0.7-1.0) for creative or analytical responses
- Use RAG enhancement for domain-specific questions
- Disable RAG for general knowledge or creative writing tasks

### **RAG Limitations & Considerations**

**Technical Limitations:**
- Context window limits may truncate large document chunks
- Embedding quality depends on document content and structure
- Semantic search may miss exact keyword matches in some cases
- Performance scales with document collection size

**Content Considerations:**
- RAG works best with well-structured, informative documents
- Poor quality or fragmented documents may reduce effectiveness
- Regular reindexing recommended for frequently updated documents
- File format limitations apply (see supported file types)

**Usage Guidelines:**
- Verify document indexing status before relying on RAG
- Monitor response quality and adjust parameters as needed
- Consider document freshness for time-sensitive information
- Use error handling for RAG unavailability scenarios

### **analyze_file** - AI-Powered File Analysis
Analyze specific files using AI with multiple analysis types and optional RAG enhancement.

**Parameters:**
- `file_uri` (required): URI of the file to analyze (string, format: "file:///path/to/file")
- `analysis_type` (optional): Type of analysis to perform (string, default: "summary")
- `temperature` (optional): Controls response creativity and randomness (number, 0.0-2.0, default: 0.7)
- `use_rag` (optional): Enable RAG for enhanced context from related documents (boolean, default: true)

**Supported Analysis Types:**
- `summary`: Comprehensive overview of file content
- `key_points`: Extract main points and important information
- `questions`: Generate relevant questions based on content
- `topics`: Identify and categorize main topics
- `technical`: Focus on technical details and specifications
- `explanation`: Provide detailed explanations of complex concepts

**Supported File Types:**
- Text files: `.txt`, `.md`, `.log`, `.csv`, `.docx`
- HTML files: `.html`, `.htm`
- PDF files: `.pdf`
- Help files: `.hhc`, `.chm` (Windows only)

**Validation Rules:**
- `file_uri`: Must be valid URI format starting with "file:///"
- `analysis_type`: Must be one of the supported analysis types
- `temperature`: Must be between 0.0 and 2.0
- File must exist and be readable
- File type must be supported by configured extractors

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "analyze_file",
    "arguments": {
      "file_uri": "file:///user-manual.pdf",
      "analysis_type": "key_points",
      "temperature": 0.4,
      "use_rag": true
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "analysis": {
      "type": "key_points",
      "file": "user-manual.pdf",
      "keyPoints": [
        "System requirements: 8GB RAM minimum, 16GB recommended",
        "Installation process requires administrative privileges",
        "Configuration should be done through config.json file",
        "Database connection string must be set in environment variables",
        "API endpoints require authentication tokens"
      ],
      "summary": "Comprehensive user manual covering installation, configuration, and usage with emphasis on security and performance best practices.",
      "recommendations": [
        "Review hardware requirements before installation",
        "Backup configuration before making changes",
        "Test in development environment before production deployment"
      ]
    },
    "success": true
  }
}
```

### **rag_search** - Semantic Vector Search (RAG/Hybrid modes)
Perform semantic search using vector embeddings to find contextually relevant content based on meaning rather than exact keyword matches.

**Parameters:**
- `query` (required): Search query or question
- `top_k` (optional): Maximum number of results to return (default: 5, range: 1-50)
- `min_similarity` (optional): Minimum cosine similarity threshold (0.0-1.0, default: 0.5)
- `file_filters` (optional): Array of file extensions or patterns to filter results

**Validation Rules:**
- `query`: Must be non-empty string, maximum 1000 characters
- `top_k`: Integer between 1 and 50
- `min_similarity`: Float between 0.0 and 1.0 (higher values = more strict matching)
- `file_filters`: Array of strings (e.g., [".pdf", ".txt", "*security*"])

**Semantic Search Features:**
- **Contextual Understanding**: Finds content based on meaning, not just keywords
- **Multi-language Support**: Works with documents in different languages
- **Fuzzy Matching**: Handles typos and variations in terminology
- **Concept Mapping**: Connects related concepts (e.g., "login" matches "authentication")
- **Ranking by Relevance**: Results sorted by semantic similarity scores

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "rag_search",
    "arguments": {
      "query": "authentication setup",
      "top_k": 3,
      "min_similarity": 0.6,
      "file_filters": [".pdf", ".md"]
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "content": [{
      "type": "text",
      "text": "Found 3 relevant chunks (similarity ≥ 0.6):\n\n**[From security-guide.pdf - Similarity: 0.89]**\nUser authentication is handled through JWT tokens. Configure the auth service by setting the secret key in your environment variables...\n\n**[From api-docs.md - Similarity: 0.76]**\nAuthentication endpoints are available at /auth/login and /auth/verify. These endpoints require valid API keys...\n\n**[From setup-guide.pdf - Similarity: 0.68]**\nInitial authentication setup requires creating an admin user account. Use the following command to create the first user..."
    }]
  }
}
```

**Search Tips:**
- **Broad Queries**: Use general terms for exploratory search (e.g., "security")
- **Specific Queries**: Use detailed questions for targeted results (e.g., "How to configure SSL certificates?")
- **Concept Search**: Search for concepts rather than exact phrases (e.g., "user management" vs "add user")
- **Multi-term Queries**: Combine related terms for better context (e.g., "database backup restore")

**Similarity Score Interpretation:**
- **0.9-1.0**: Highly relevant, direct matches
- **0.7-0.9**: Very relevant, strong semantic connection
- **0.5-0.7**: Moderately relevant, related concepts
- **0.3-0.5**: Loosely related, may contain useful context
- **0.0-0.3**: Weakly related, likely not useful

### **rag_ask** - RAG-Enhanced AI Questioning (RAG/Hybrid modes)
Ask questions enhanced with document context using semantic search and vector embeddings.

**Parameters:**
- `question` (required): Question to ask the AI
- `top_k` (optional): Number of context chunks to retrieve (default: 5, range: 1-20)
- `temperature` (optional): AI creativity level (0.0-2.0, default: 0.7)
- `min_similarity` (optional): Minimum similarity threshold for context chunks (0.0-1.0, default: 0.1)

**Validation Rules:**
- `question`: Must be non-empty string, maximum 2000 characters
- `top_k`: Integer between 1 and 20
- `temperature`: Float between 0.0 and 2.0 (0.0 = deterministic, 2.0 = highly creative)
- `min_similarity`: Float between 0.0 and 1.0 (higher values = more relevant context)

**How RAG Enhancement Works:**
1. **Query Processing**: The question is converted to vector embeddings
2. **Context Retrieval**: Semantic search finds the most relevant document chunks
3. **Context Ranking**: Results are ranked by cosine similarity scores
4. **Context Integration**: Top-K chunks are combined with the original question
5. **AI Generation**: Enhanced prompt is sent to the AI provider for response

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "rag_ask",
    "arguments": {
      "question": "How do I set up SSL certificates?",
      "top_k": 3,
      "temperature": 0.3,
      "min_similarity": 0.6
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "content": [{
      "type": "text",
      "text": "RAG-Enhanced Response (using 3 context chunks):\n\nBased on your security documentation, SSL certificate setup involves:\n\n1. **Certificate Generation**: Use OpenSSL to generate certificates as described in the security guide (page 8)\n2. **Configuration**: Set the certificate paths in config.json under the 'Security' section\n3. **Verification**: Use the provided test script to verify certificate validity\n\nSpecific steps from your documents:\n• Certificate files should be placed in /etc/ssl/certs/ (security-guide.pdf)\n• Private key must have 600 permissions (config-reference.txt)\n• Certificate chain must include intermediate certificates (api-docs.html)"
    }]
  }
}
```

**Context Sources Information:**
The response includes context from retrieved document chunks, formatted as:
- `[From filename - Similarity: 0.XXX]` headers indicate source and relevance
- Higher similarity scores (closer to 1.0) indicate more relevant context
- Context chunks are automatically ranked and filtered by similarity threshold

**Best Practices:**
- Use `top_k=3-5` for focused, specific questions
- Use `top_k=8-10` for broad, exploratory questions
- Set `min_similarity=0.6+` for highly relevant context only
- Set `min_similarity=0.3-0.5` for broader context inclusion
- Use `temperature=0.1-0.3` for factual, technical questions
- Use `temperature=0.7-1.0` for creative or analytical questions

**Error Handling for RAG Tools:**
```json
{
  "jsonrpc": "2.0",
  "error": {
    "code": -32603,
    "message": "Internal error",
    "data": "Vector store not initialized. Please run reindex_documents first."
  }
}
```

**Common RAG Error Scenarios:**
- **Vector Store Not Available**: RAG index hasn't been created or is corrupted
- **Empty Search Results**: No documents match the similarity threshold
- **Invalid Query Parameters**: `top_k` out of range or invalid `min_similarity`
- **Embedding Generation Failed**: Unable to convert query to vector embeddings
- **AI Provider Unavailable**: RAG search succeeded but AI generation failed (rag_ask only)
- **File Filter Errors**: Invalid file patterns or no matching files
- **Context Window Exceeded**: Retrieved context too large for AI provider
- **Indexing In Progress**: Vector store is being rebuilt and temporarily unavailable

### **reindex_documents** - Rebuild Vector Index (RAG/Hybrid modes)
Rebuild the vector store index.

**Parameters:**
- `force` (optional): Force reindex of all files, not just changed ones (default: false)

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "reindex_documents",
    "arguments": {
      "force": true
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "status": "completed",
    "filesProcessed": 156,
    "chunksCreated": 234,
    "newFiles": 12,
    "modifiedFiles": 8,
    "timeTaken": "00:01:23.45",
    "forceReindex": true
  }
}
```

### **indexing_report** - Get Indexing Status Report (RAG/Hybrid modes)
Get comprehensive report of indexed vs. non-indexed files.

**Parameters:**
- `show_details` (optional): Include detailed file information (default: true)

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "indexing_report",
    "arguments": {
      "show_details": true
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "summary": {
      "totalFiles": 156,
      "indexable": 89,
      "notIndexable": 45,
      "tooLarge": 4,
      "passwordProtected": 3,
      "indexedChunks": 234
    },
    "byFileType": {
      ".pdf": {"total": 34, "indexable": 34, "indexed": 34},
      ".txt": {"total": 28, "indexable": 28, "indexed": 28},
      ".html": {"total": 18, "indexable": 18, "indexed": 18},
      ".jpg": {"total": 23, "indexable": 0, "indexed": 0},
      ".exe": {"total": 8, "indexable": 0, "indexed": 0}
    },
    "details": [
      {
        "file": "security-guide.pdf",
        "size": 3400000,
        "indexable": true,
        "indexed": true,
        "chunks": 34,
        "lastIndexed": "2024-01-15T14:30:22Z"
      },
      {
        "file": "presentation.pptx", 
        "size": 215000000,
        "indexable": false,
        "indexed": false,
        "reason": "File too large (>100MB)",
        "lastChecked": "2024-01-15T14:30:25Z"
      }
    ]
  }
}
```

## 🔍 Semantic Search API Documentation

### Overview

The Semantic Search API provides programmatic access to HlpAI's vector-based document search capabilities. This API enables applications to perform meaning-based searches across indexed documents using advanced embedding models and cosine similarity calculations.

### Core Components

#### **IEmbeddingService Interface**

The primary interface for semantic search operations:

```csharp
public interface IEmbeddingService
{
    Task<float[]> GetEmbeddingAsync(string text);
    Task<float[][]> GetEmbeddingsAsync(IEnumerable<string> texts);
    Task<IEnumerable<SearchResult>> SearchAsync(string query, int topK = 5);
    Task<int> GetEmbeddingDimensionAsync();
}
```

**Method Descriptions:**
- `GetEmbeddingAsync`: Convert text to vector embedding
- `GetEmbeddingsAsync`: Batch convert multiple texts to embeddings
- `SearchAsync`: Perform semantic search with similarity ranking
- `GetEmbeddingDimensionAsync`: Get the dimension size of embeddings

#### **SearchResult Model**

Represents a single search result with metadata:

```csharp
public class SearchResult
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public float SimilarityScore { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

#### **RagQuery Model**

Defines search parameters for advanced queries:

```csharp
public class RagQuery
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
    public float MinSimilarity { get; set; } = 0.1f;
    public List<string> FileFilters { get; set; } = new();
    public float[] QueryEmbedding { get; set; } = Array.Empty<float>();
}
```

### API Usage Examples

#### **Basic Semantic Search**

```csharp
public class DocumentSearchService
{
    private readonly IEmbeddingService _embeddingService;
    
    public DocumentSearchService(IEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }
    
    public async Task<IEnumerable<SearchResult>> SearchDocumentsAsync(
        string query, int maxResults = 5)
    {
        return await _embeddingService.SearchAsync(query, maxResults);
    }
}
```

#### **Advanced Search with Filters**

```csharp
public async Task<IEnumerable<SearchResult>> AdvancedSearchAsync(
    string query, float minSimilarity = 0.6f, string[] fileTypes = null)
{
    var ragQuery = new RagQuery
    {
        Query = query,
        TopK = 10,
        MinSimilarity = minSimilarity,
        FileFilters = fileTypes?.ToList() ?? new List<string>()
    };
    
    // Convert to embedding
    ragQuery.QueryEmbedding = await _embeddingService.GetEmbeddingAsync(query);
    
    // Perform search using vector store
    return await _vectorStore.SearchAsync(ragQuery);
}
```

#### **Cosine Similarity Calculation**

HlpAI uses cosine similarity to measure semantic similarity between text embeddings. Cosine similarity calculates the cosine of the angle between two vectors, providing a value between -1 and 1, where 1 indicates identical direction (highest similarity) and -1 indicates opposite direction.

**Mathematical Foundation:**
```
cosine_similarity(A, B) = (A · B) / (||A|| × ||B||)

Where:
- A · B = dot product of vectors A and B
- ||A|| = magnitude (Euclidean norm) of vector A
- ||B|| = magnitude (Euclidean norm) of vector B
```

**Implementation:**

```csharp
public async Task<float> CalculateTextSimilarityAsync(string text1, string text2)
{
    var embedding1 = await _embeddingService.GetEmbeddingAsync(text1);
    var embedding2 = await _embeddingService.GetEmbeddingAsync(text2);
    
    return CalculateCosineSimilarity(embedding1, embedding2);
}

/// <summary>
/// Calculates cosine similarity between two embedding vectors.
/// Returns a value between -1 and 1, where:
/// - 1.0: Vectors point in the same direction (highest similarity)
/// - 0.0: Vectors are orthogonal (no similarity)
/// - -1.0: Vectors point in opposite directions (lowest similarity)
/// </summary>
/// <param name="vector1">First embedding vector</param>
/// <param name="vector2">Second embedding vector</param>
/// <returns>Cosine similarity score</returns>
private static float CalculateCosineSimilarity(float[] vector1, float[] vector2)
{
    // Handle edge cases
    if (vector1.Length != vector2.Length) return 0f;
    if (vector1.Length == 0) return 0f;
    
    float dotProduct = 0f;
    float magnitude1 = 0f;
    float magnitude2 = 0f;
    
    // Calculate dot product and magnitudes in single pass
    for (int i = 0; i < vector1.Length; i++)
    {
        dotProduct += vector1[i] * vector2[i];
        magnitude1 += vector1[i] * vector1[i];
        magnitude2 += vector2[i] * vector2[i];
    }
    
    // Handle zero vectors (avoid division by zero)
    if (magnitude1 == 0f || magnitude2 == 0f) return 0f;
    
    // Calculate final similarity
    magnitude1 = (float)Math.Sqrt(magnitude1);
    magnitude2 = (float)Math.Sqrt(magnitude2);
    
    return dotProduct / (magnitude1 * magnitude2);
}
```

**Similarity Score Interpretation:**
- **0.9-1.0**: Nearly identical semantic meaning
- **0.7-0.9**: Strong semantic similarity, highly relevant
- **0.5-0.7**: Moderate similarity, related concepts
- **0.3-0.5**: Weak similarity, loosely related
- **0.0-0.3**: Little to no semantic relationship
- **Negative values**: Rare in text embeddings, indicates opposing concepts

**Performance Characteristics:**
- **Time Complexity**: O(n) where n is the embedding dimension
- **Space Complexity**: O(1) additional space
- **Typical Embedding Dimensions**: 384-1536 (depending on model)
- **Calculation Time**: ~0.1ms for 768-dimensional vectors

**Edge Case Handling:**
```csharp
// Different vector lengths
if (vector1.Length != vector2.Length) return 0f;

// Empty vectors
if (vector1.Length == 0) return 0f;

// Zero vectors (no magnitude)
if (magnitude1 == 0f || magnitude2 == 0f) return 0f;

// NaN/Infinity protection
var result = dotProduct / (magnitude1 * magnitude2);
return float.IsNaN(result) || float.IsInfinity(result) ? 0f : result;
```

### REST API Integration

#### **ASP.NET Core Controller Example**

```csharp
[ApiController]
[Route("api/[controller]")]
public class SemanticSearchController : ControllerBase
{
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<SemanticSearchController> _logger;
    
    public SemanticSearchController(IEmbeddingService embeddingService, 
        ILogger<SemanticSearchController> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;
    }
    
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        [FromQuery] int topK = 5,
        [FromQuery] float minSimilarity = 0.5f,
        [FromQuery] string[] fileTypes = null)
    {
        try
        {
            var results = await _embeddingService.SearchAsync(query, topK);
            
            // Filter by similarity threshold
            var filteredResults = results
                .Where(r => r.SimilarityScore >= minSimilarity)
                .ToList();
            
            return Ok(new
            {
                query,
                results = filteredResults,
                total = filteredResults.Count,
                parameters = new { topK, minSimilarity, fileTypes }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query: {Query}", query);
            return StatusCode(500, new { error = ex.Message });
        }
    }
    
    [HttpPost("similarity")]
    public async Task<IActionResult> CalculateSimilarity(
        [FromBody] SimilarityRequest request)
    {
        try
        {
            var embedding1 = await _embeddingService.GetEmbeddingAsync(request.Text1);
            var embedding2 = await _embeddingService.GetEmbeddingAsync(request.Text2);
            
            var similarity = CalculateCosineSimilarity(embedding1, embedding2);
            
            return Ok(new
            {
                text1 = request.Text1,
                text2 = request.Text2,
                similarity,
                interpretation = GetSimilarityInterpretation(similarity)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Similarity calculation failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }
    
    private static string GetSimilarityInterpretation(float similarity)
    {
        return similarity switch
        {
            >= 0.9f => "Highly similar",
            >= 0.7f => "Very similar",
            >= 0.5f => "Moderately similar",
            >= 0.3f => "Somewhat similar",
            _ => "Not similar"
        };
    }
}

public record SimilarityRequest(string Text1, string Text2);
```

### Configuration and Optimization

#### **Embedding Service Configuration**

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IEmbeddingService>(provider =>
    {
        var config = provider.GetRequiredService<AppConfiguration>();
        var logger = provider.GetRequiredService<ILogger<EmbeddingService>>();
        
        return new EmbeddingService(
            baseUrl: config.EmbeddingServiceUrl ?? "http://localhost:11434",
            embeddingModel: config.DefaultEmbeddingModel ?? "nomic-embed-text",
            logger: logger,
            config: config
        );
    });
}
```

#### **Performance Optimization**

```csharp
public class OptimizedSemanticSearchService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _semaphore;
    
    public OptimizedSemanticSearchService(
        IEmbeddingService embeddingService,
        IMemoryCache cache)
    {
        _embeddingService = embeddingService;
        _cache = cache;
        _semaphore = new SemaphoreSlim(5, 5); // Limit concurrent requests
    }
    
    public async Task<IEnumerable<SearchResult>> SearchWithCachingAsync(
        string query, int topK = 5)
    {
        var cacheKey = $"search:{query}:{topK}";
        
        if (_cache.TryGetValue(cacheKey, out IEnumerable<SearchResult> cachedResults))
        {
            return cachedResults;
        }
        
        await _semaphore.WaitAsync();
        try
        {
            var results = await _embeddingService.SearchAsync(query, topK);
            
            _cache.Set(cacheKey, results, TimeSpan.FromMinutes(10));
            return results;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

### Error Handling and Monitoring

#### **Comprehensive Error Handling**

```csharp
public class RobustSemanticSearchService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<RobustSemanticSearchService> _logger;
    private readonly IMetrics _metrics;
    
    public async Task<SearchResponse> SearchWithErrorHandlingAsync(
        string query, SearchOptions options = null)
    {
        options ??= new SearchOptions();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Starting semantic search for query: {Query}", query);
            
            // Validate input
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query cannot be empty", nameof(query));
            
            if (query.Length > 1000)
                throw new ArgumentException("Query too long (max 1000 characters)", nameof(query));
            
            // Perform search
            var results = await _embeddingService.SearchAsync(query, options.TopK);
            
            // Filter and process results
            var filteredResults = results
                .Where(r => r.SimilarityScore >= options.MinSimilarity)
                .Take(options.MaxResults)
                .ToList();
            
            _metrics.Counter("semantic_search_success").Increment();
            _metrics.Histogram("semantic_search_duration").Record(stopwatch.ElapsedMilliseconds);
            
            return new SearchResponse
            {
                Success = true,
                Results = filteredResults,
                Query = query,
                TotalResults = filteredResults.Count,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Semantic search failed for query: {Query}", query);
            _metrics.Counter("semantic_search_error").Increment();
            
            return new SearchResponse
            {
                Success = false,
                Error = ex.Message,
                Query = query,
                Duration = stopwatch.Elapsed
            };
        }
    }
}

public class SearchOptions
{
    public int TopK { get; set; } = 5;
    public float MinSimilarity { get; set; } = 0.5f;
    public int MaxResults { get; set; } = 10;
    public string[] FileTypes { get; set; } = Array.Empty<string>();
}

public class SearchResponse
{
    public bool Success { get; set; }
    public IEnumerable<SearchResult> Results { get; set; } = Enumerable.Empty<SearchResult>();
    public string Query { get; set; } = string.Empty;
    public int TotalResults { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Error { get; set; }
}
```

### Testing and Validation

#### **Unit Testing Example**

```csharp
[TestFixture]
public class SemanticSearchApiTests
{
    private Mock<IEmbeddingService> _mockEmbeddingService;
    private SemanticSearchController _controller;
    
    [SetUp]
    public void Setup()
    {
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        var logger = new Mock<ILogger<SemanticSearchController>>();
        _controller = new SemanticSearchController(_mockEmbeddingService.Object, logger.Object);
    }
    
    [Test]
    public async Task Search_ValidQuery_ReturnsResults()
    {
        // Arrange
        var query = "test query";
        var expectedResults = new List<SearchResult>
        {
            new() { FileName = "test.pdf", SimilarityScore = 0.8f, Content = "test content" }
        };
        
        _mockEmbeddingService
            .Setup(s => s.SearchAsync(query, 5))
            .ReturnsAsync(expectedResults);
        
        // Act
        var result = await _controller.Search(query);
        
        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        Assert.That(okResult.Value, Is.Not.Null);
    }
    
    [Test]
    public async Task Search_EmptyQuery_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.Search("");
        
        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }
}
```

## 🔌 Integration Examples

### Integration with Claude Desktop

**Claude Desktop Configuration:**
```json
{
  "mcpServers": {
    "hlpai": {
      "command": "dotnet",
      "args": [
        "run",
        "C:\\YourDocuments",
        "llama3.2", 
        "mcp"
      ],
      "env": {
        "DOTNET_ROOT": "/usr/local/share/dotnet"
      }
    }
  }
}
```

**Usage in Claude:**
```
@hlpai search_files query="authentication setup" maxResults=3
@hlpai ask_ai question="How do I configure SSL?" temperature=0.3 useRag=true
@hlpai analyze_file uri="file:///user-manual.pdf" analysisType="summary"
```

### Programmatic Integration in .NET

**Using EnhancedMcpRagServer Directly:**
```csharp
using HlpAI.MCP;

// Create and initialize server
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<EnhancedMcpRagServer>();
var server = new EnhancedMcpRagServer(logger, "C:\\YourDocuments", "llama3.2", OperationMode.MCP);

await server.InitializeAsync();

// Make MCP requests
var request = new McpRequest 
{
    JsonRpc = "2.0",
    Id = "1",
    Method = "tools/call",
    Params = new 
    {
        name = "ask_ai",
        arguments = new 
        {
            question = "What are the security best practices?",
            temperature = 0.7,
            useRag = true
        }
    }
};

var response = await server.HandleRequestAsync(request);
Console.WriteLine(response.Result.Content);
```

### HTTP API Wrapper Example

**Simple HTTP Wrapper:**
```csharp
// Program.cs
using HlpAI.MCP;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<EnhancedMcpRagServer>();

var app = builder.Build();

// Initialize server at startup
var server = app.Services.GetRequiredService<EnhancedMcpRagServer>();
await server.InitializeAsync();

// HTTP endpoints
app.MapPost("/api/ask", async ([FromBody] AskRequest request) =>
{
    var mcpRequest = new McpRequest
    {
        JsonRpc = "2.0",
        Id = Guid.NewGuid().ToString(),
        Method = "tools/call",
        Params = new
        {
            name = "ask_ai",
            arguments = request
        }
    };
    
    var response = await server.HandleRequestAsync(mcpRequest);
    return Results.Ok(response.Result);
});

app.MapPost("/api/search", async ([FromBody] SearchRequest request) =>
{
    var mcpRequest = new McpRequest
    {
        JsonRpc = "2.0",
        Id = Guid.NewGuid().ToString(),
        Method = "tools/call", 
        Params = new
        {
            name = "search_files",
            arguments = request
        }
    };
    
    var response = await server.HandleRequestAsync(mcpRequest);
    return Results.Ok(response.Result);
});

app.Run();

public record AskRequest(string Question, double Temperature = 0.7, bool UseRag = false);
public record SearchRequest(string Query, int MaxResults = 10);
```

### Python Integration Example

**Using subprocess for MCP communication:**
```python
import json
import subprocess
import threading

class HlpAIClient:
    def __init__(self, document_dir, model_name="llama3.2"):
        self.process = subprocess.Popen(
            ["dotnet", "run", document_dir, model_name, "mcp"],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True
        )
        self.request_id = 1
        
    def send_request(self, method, params):
        request = {
            "jsonrpc": "2.0",
            "id": self.request_id,
            "method": method,
            "params": params
        }
        self.request_id += 1
        
        # Send request
        self.process.stdin.write(json.dumps(request) + "\n")
        self.process.stdin.flush()
        
        # Read response
        response_line = self.process.stdout.readline()
        return json.loads(response_line)
    
    def ask_ai(self, question, temperature=0.7, use_rag=False):
        return self.send_request("tools/call", {
            "name": "ask_ai",
            "arguments": {
                "question": question,
                "temperature": temperature,
                "useRag": use_rag
            }
        })
    
    def search_files(self, query, max_results=10):
        return self.send_request("tools/call", {
            "name": "search_files", 
            "arguments": {
                "query": query,
                "maxResults": max_results
            }
        })

# Usage
client = HlpAIClient("/path/to/documents")
response = client.ask_ai("How do I set up the database?")
print(response["result"]["content"])
```

## 🚀 Deployment & Operations

### Running as a Service

**Windows Service (using NSSM):**
```bash
# Install as service
nssm install HlpAI-MCP "C:\Program Files\dotnet\dotnet.exe" "run C:\Documents llama3.2 mcp"

# Start service
nssm start HlpAI-MCP

# Monitor logs
nssm get HlpAI-MCP AppStderr
```

**Linux Systemd Service:**
```ini
# /etc/systemd/system/hlpai-mcp.service
[Unit]
Description=HlpAI MCP Server
After=network.target

[Service]
Type=simple
User=hlpai
WorkingDirectory=/opt/hlpai
ExecStart=/usr/bin/dotnet run /home/hlpai/documents llama3.2 mcp
Restart=always
RestartSec=5
Environment=DOTNET_ROOT=/usr/share/dotnet

[Install]
WantedBy=multi-user.target
```

**Docker Deployment:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore && dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app .
VOLUME /documents
ENTRYPOINT ["dotnet", "HlpAI.dll", "/documents", "llama3.2", "mcp"]
```

```bash
# Build and run
docker build -t hlpai-mcp .
docker run -d -v /path/to/documents:/documents -p 11434:11434 hlpai-mcp
```

### Performance Optimization

**Memory Management:**
```bash
# Increase memory limits
export DOTNET_GCHeapCount=4
export DOTNET_GCHeapHardLimit=0x100000000  # 4GB

# Or use runtimeconfig.json
{
  "runtimeOptions": {
    "configProperties": {
      "System.GC.HeapHardLimit": 4294967296,
      "System.GC.HeapCount": 4
    }
  }
}
```

**Concurrency Settings:**
```csharp
// In your startup configuration
services.Configure<ConcurrencyOptions>(options =>
{
    options.MaxConcurrentRequests = 10;
    options.RequestTimeout = TimeSpan.FromMinutes(2);
    options.MaxMemoryUsage = 1024 * 1024 * 1024; // 1GB
});
```

### Monitoring & Logging

**Structured Logging Configuration:**
```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/hlpai-mcp-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

**Health Check Endpoint:**
```csharp
app.MapGet("/health", async (EnhancedMcpRagServer server) =>
{
    try
    {
        var status = await server.GetStatusAsync();
        return Results.Ok(new {
            status = "healthy",
            documents = status.TotalFiles,
            indexed = status.IndexedFiles,
            memory = GC.GetTotalMemory(false) / 1024 / 1024
        });
    }
    catch
    {
        return Results.StatusCode(503);
    }
});
```

## 🔧 Advanced Configuration

### Custom MCP Server Settings

**appsettings.json Configuration:**
```json
{
  "McpServer": {
    "Name": "HlpAI",
    "Version": "1.0.0",
    "Capabilities": {
      "Resources": true,
      "Tools": true,
      "Logging": true
    },
    "Limits": {
      "MaxConcurrentRequests": 10,
      "MaxMemoryUsage": 1073741824,
      "RequestTimeout": "00:02:00"
    },
    "DocumentProcessing": {
      "ChunkSize": 1000,
      "ChunkOverlap": 200,
      "MaxFileSize": 104857600,
      "SupportedExtensions": [".txt", ".md", ".pdf", ".html", ".htm"]
    }
  }
}
```

### Authentication & Security

**API Key Authentication:**
```csharp
// Add authentication middleware
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.TryGetValue("X-API-Key", out var apiKey) ||
        apiKey != Configuration["ApiKey"])
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Invalid API key");
        return;
    }
    await next();
});
```

**Rate Limiting:**
```csharp
// Add rate limiting
services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString(),
            partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

### Secure API Key Storage

HlpAI provides secure API key storage using Windows Data Protection API (DPAPI) for enhanced security. This feature encrypts API keys at rest and ensures they can only be decrypted by the same user account on the same machine.

**Configuration Options:**
```csharp
// Enable secure API key storage (default: true)
public bool UseSecureApiKeyStorage { get; set; } = true;

// Validate API keys on startup (default: true)
public bool ValidateApiKeysOnStartup { get; set; } = true;
```

**Supported Providers:**
- **OpenAI**: Secure storage for OpenAI API keys
- **Anthropic**: Secure storage for Anthropic API keys  
- **DeepSeek**: Secure storage for DeepSeek API keys
- **Cloud Providers**: Any provider requiring API key authentication

**Storage Implementation:**
```csharp
// SecureApiKeyStorage service usage
var secureStorage = new SecureApiKeyStorage(logger);

// Store API key securely
bool success = secureStorage.StoreApiKey("OpenAI", "sk-your-api-key-here");

// Retrieve API key securely
string? apiKey = secureStorage.RetrieveApiKey("OpenAI");

// Check if API key exists
bool hasKey = secureStorage.HasApiKey("OpenAI");

// Delete API key
bool deleted = secureStorage.DeleteApiKey("OpenAI");

// List all stored providers
var providers = secureStorage.ListStoredProviders();
```

**Interactive Configuration:**
Use the interactive menu system to manage API keys securely:

```
🔧 Configuration Menu
=====================
1. Configure OpenAI API Key
2. Configure Anthropic API Key  
3. Configure DeepSeek API Key
4. View Stored API Keys
5. Delete API Key
6. Toggle Secure Storage
7. Validate All API Keys
```

**Security Features:**

*Windows DPAPI Encryption:*
- Uses Windows Data Protection API for encryption
- Keys encrypted with user-specific entropy
- Can only be decrypted by the same user account
- Automatic key derivation from user credentials

*Storage Location:*
```
%APPDATA%\HlpAI\SecureKeys\
├── OpenAI.key      # Encrypted OpenAI API key
├── Anthropic.key   # Encrypted Anthropic API key
└── DeepSeek.key    # Encrypted DeepSeek API key
```

*Cross-Platform Support:*
```csharp
// Cross-platform data protection
public class CrossPlatformDataProtection : ICrossPlatformDataProtection
{
    // Windows: Uses DPAPI
    // Linux/macOS: Uses AES encryption with user-derived keys
    public byte[] Protect(byte[] data, string purpose);
    public byte[] Unprotect(byte[] encryptedData, string purpose);
}
```

**Configuration Examples:**

*Enable Secure Storage:*
```json
{
  "AppConfiguration": {
    "UseSecureApiKeyStorage": true,
    "ValidateApiKeysOnStartup": true
  }
}
```

*Command Line Configuration:*
```bash
# Enable secure storage
dotnet run --use-secure-storage true

# Disable validation on startup
dotnet run --validate-api-keys false

# Configure with secure storage
dotnet run "C:\\Docs" "gpt-4" "mcp" --use-secure-storage true
```

*Programmatic Configuration:*
```csharp
// Configure secure storage in application
var config = new AppConfiguration
{
    UseSecureApiKeyStorage = true,
    ValidateApiKeysOnStartup = true
};

// Register secure storage service
services.AddSingleton<SecureApiKeyStorage>();
services.AddSingleton<ICrossPlatformDataProtection, CrossPlatformDataProtection>();
```

**Security Best Practices:**

1. **Enable Secure Storage**: Always use secure storage for production deployments
2. **Regular Key Rotation**: Rotate API keys periodically for enhanced security
3. **Access Control**: Ensure only authorized users have access to the application
4. **Backup Strategy**: Secure storage keys are user-specific and cannot be transferred
5. **Monitoring**: Enable API key usage logging for security auditing

**Error Handling:**
```csharp
try
{
    var apiKey = secureStorage.RetrieveApiKey("OpenAI");
    if (apiKey == null)
    {
        logger.LogWarning("No API key found for OpenAI provider");
        // Prompt user to configure API key
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to retrieve API key for OpenAI");
    // Handle decryption failure or file corruption
}
```

**Validation and Testing:**
```csharp
// API key validation service
public class SecurityValidationService
{
    public bool ValidateApiKeyFormat(string provider, string apiKey);
    public async Task<bool> ValidateApiKeyAsync(string provider, string apiKey);
}

// Security audit logging
public class SecurityAuditService  
{
    public void LogApiKeyUsage(string provider, string operation);
    public void LogSecurityEvent(string eventType, string details);
}
```

**Migration from Plain Text:**
If you have existing plain text API keys, migrate them to secure storage:

```csharp
// Migration helper
public async Task MigrateToSecureStorageAsync()
{
    var plainTextKeys = GetExistingApiKeys(); // Your existing method
    var secureStorage = new SecureApiKeyStorage(logger);
    
    foreach (var (provider, key) in plainTextKeys)
    {
        if (secureStorage.StoreApiKey(provider, key))
        {
            logger.LogInformation("Migrated {Provider} API key to secure storage", provider);
            // Remove plain text key
            RemovePlainTextKey(provider);
        }
    }
}
```

## 🚨 Troubleshooting

### Enhanced Error Handling

HlpAI includes robust error handling for common issues:

**Directory Access Issues:**
- **Safe enumeration** - Continues processing when encountering restricted directories
- **Graceful degradation** - Logs access issues without crashing
- **Detailed reporting** - Shows which files/directories had access problems

**Initialization Protection:**
- **Startup validation** - Validates configuration before starting services
- **Graceful failure** - Provides clear error messages for startup issues
- **Recovery guidance** - Suggests solutions for common configuration problems

**Audit-First Approach:**
Use audit mode to identify potential issues before running MCP server:
```bash
# Run audit first to identify problems
dotnet run -- --audit "C:\YourDocuments"

# Look for:
# - Access denied errors
# - Unsupported file types
# - Large files that might cause issues
# - Directory structure problems
```

### Common MCP Server Issues

**Connection Problems:**
```bash
# Test server connectivity
curl -X POST http://localhost:11434/api/version
# Check if ports are open
netstat -an | find "11434"
# Verify firewall settings
netsh advfirewall firewall show rule name="HlpAI MCP"
```

**Performance Issues:**
```bash
# Monitor memory usage
dotnet-counters monitor --process-id <PID> --counters System.Runtime
# Check CPU usage
dotnet-trace collect --process-id <PID> --profile cpu-sampling
# Analyze memory dump
dotnet-dump collect --process-id <PID>
```

**Log Analysis:**
```bash
# View recent errors
grep -i error logs/hlpai-mcp-*.log
# Monitor live logs
tail -f logs/hlpai-mcp-$(date +%Y-%m-%d).log
# Search for specific patterns
grep -n "authentication" logs/hlpai-mcp-*.log
```

### Debugging MCP Requests

**Enable Debug Logging:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "HlpAI.MCP": "Debug",
      "System": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

**Request/Response Logging Middleware:**
```csharp
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    
    // Log request
    context.Request.EnableBuffering();
    var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
    context.Request.Body.Position = 0;
    
    logger.LogDebug("Request: {Method} {Path} {Body}", 
        context.Request.Method, context.Request.Path, requestBody);
    
    // Capture response
    var originalBody = context.Response.Body;
    using var responseBody = new MemoryStream();
    context.Response.Body = responseBody;
    
    await next();
    
    // Log response
    responseBody.Position = 0;
    var response = await new StreamReader(responseBody).ReadToEndAsync();
    responseBody.Position = 0;
    await responseBody.CopyToAsync(originalBody);
    
    logger.LogDebug("Response: {StatusCode} {Body}", 
        context.Response.StatusCode, response);
});
```

## 📊 Performance Benchmarks

### Typical Performance Metrics

**Indexing Performance:**
- **Small documents** (<100KB): 50-100 files/second
- **Medium documents** (100KB-1MB): 10-20 files/second  
- **Large documents** (1MB-10MB): 2-5 files/second
- **Very large documents** (>10MB): Processed in chunks, ~1 file/second

**Search Performance:**
- **Text search**: <100ms for most queries
- **Semantic search**: 200-500ms depending on index size
- **AI responses**: 1-5 seconds depending on model complexity

**Memory Usage:**
- **Base memory**: 50-100MB for runtime
- **Per document**: ~1MB per 1000 documents indexed
- **Vector store**: Additional 50-100MB for embeddings

### Scaling Recommendations

**For small deployments** (<10,000 documents):
- Single server instance
- 2-4GB RAM recommended
- 2 CPU cores minimum

**For medium deployments** (10,000-100,000 documents):
- Consider multiple instances with load balancing
- 8-16GB RAM recommended  
- 4-8 CPU cores recommended
- SSD storage strongly recommended

**For large deployments** (>100,000 documents):
- Distributed architecture with multiple specialized instances
- 32GB+ RAM per instance
- 8+ CPU cores per instance
- High-performance SSD storage required
- Consider database sharding for vector store

## 🎯 Next Steps

After setting up MCP Server Mode, consider:

1. **Integration with existing tools** like Claude Desktop, Cursor, or custom applications
2. **Automation workflows** for document processing and analysis
3. **Monitoring and alerting** for production deployments
4. **Performance optimization** based on your specific workload
5. **Security hardening** for exposed APIs

For more advanced usage, explore:
- **[Interactive Mode](README-INTERACTIVE.md)** - For manual exploration and testing
- **[Library Mode](README-LIBRARY.md)** - For .NET application integration
- **Custom extractors** - For specialized document types
- **Advanced RAG configurations** - For optimized search performance

---

**MCP Server Mode provides powerful programmatic access to HlpAI's document intelligence capabilities, enabling seamless integration with external tools, automation workflows, and custom applications through the standardized Model Context Protocol.**
