using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using HlpAI.Models;

namespace HlpAI.Services;

/// <summary>
/// Factory for creating AI provider instances
/// </summary>
public static class AiProviderFactory
{
    /// <summary>
    /// Create an AI provider instance
    /// </summary>
    public static IAiProvider CreateProvider(
        AiProviderType providerType,
        string model,
        string? baseUrl = null,
        ILogger? logger = null,
        AppConfiguration? config = null)
    {
        // Handle empty or whitespace URLs by using default
        string GetEffectiveUrl(string defaultUrl) => 
            string.IsNullOrWhiteSpace(baseUrl) ? defaultUrl : baseUrl;

        return providerType switch
        {
            AiProviderType.Ollama => new OllamaClient(
                GetEffectiveUrl("http://localhost:11434"),
                model,
                logger,
                config),

            AiProviderType.LmStudio => new LmStudioProvider(
                GetEffectiveUrl("http://localhost:1234"),
                model,
                logger,
                config),

            AiProviderType.OpenWebUi => new OpenWebUiProvider(
                GetEffectiveUrl("http://localhost:3000"),
                model,
                logger,
                config),

            AiProviderType.OpenAI => throw new InvalidOperationException("OpenAI provider requires an API key. Use the overload with apiKey parameter."),

            AiProviderType.Anthropic => throw new InvalidOperationException("Anthropic provider requires an API key. Use the overload with apiKey parameter."),

            AiProviderType.DeepSeek => throw new InvalidOperationException("DeepSeek provider requires an API key. Use the overload with apiKey parameter."),

            _ => throw new ArgumentException($"Unknown provider type: {providerType}")
        };
    }

    /// <summary>
    /// Create an AI provider instance with API key for cloud providers
    /// </summary>
    public static IAiProvider CreateProvider(
        AiProviderType providerType,
        string model,
        string? baseUrl = null,
        string? apiKey = null,
        ILogger? logger = null,
        AppConfiguration? config = null)
    {
        // Handle empty or whitespace URLs by using default
        string GetEffectiveUrl(string defaultUrl) => 
            string.IsNullOrWhiteSpace(baseUrl) ? defaultUrl : baseUrl;

        return providerType switch
        {
            AiProviderType.Ollama => new OllamaClient(
                GetEffectiveUrl("http://localhost:11434"),
                model,
                logger,
                config),

            AiProviderType.LmStudio => new LmStudioProvider(
                GetEffectiveUrl("http://localhost:1234"),
                model,
                logger,
                config),

            AiProviderType.OpenWebUi => new OpenWebUiProvider(
                GetEffectiveUrl("http://localhost:3000"),
                model,
                logger,
                config),

            AiProviderType.OpenAI => new OpenAiProvider(
                apiKey ?? throw new ArgumentNullException(nameof(apiKey), "OpenAI provider requires an API key"),
                model,
                GetEffectiveUrl("https://api.openai.com"),
                logger,
                config),

            AiProviderType.Anthropic => new AnthropicProvider(
                apiKey ?? throw new ArgumentNullException(nameof(apiKey), "Anthropic provider requires an API key"),
                model,
                GetEffectiveUrl("https://api.anthropic.com"),
                logger,
                config),

            AiProviderType.DeepSeek => new DeepSeekProvider(
                apiKey ?? throw new ArgumentNullException(nameof(apiKey), "DeepSeek provider requires an API key"),
                model,
                GetEffectiveUrl("https://api.deepseek.com/v1"),
                logger,
                config),

            _ => throw new ArgumentException($"Unknown provider type: {providerType}")
        };
    }

    /// <summary>
    /// Get provider information for display purposes
    /// </summary>
    public static ProviderInfo GetProviderInfo(AiProviderType providerType)
    {
        return providerType switch
        {
            AiProviderType.Ollama => new ProviderInfo(
                "Ollama",
                "Local model runner",
                "http://localhost:11434",
                "llama3.2"),

            AiProviderType.LmStudio => new ProviderInfo(
                "LM Studio",
                "Local API server with GUI",
                "http://localhost:1234",
                "default"),

            AiProviderType.OpenWebUi => new ProviderInfo(
                "Open Web UI",
                "Web-based model management",
                "http://localhost:3000",
                "default"),

            AiProviderType.OpenAI => new ProviderInfo(
                "OpenAI",
                "Cloud-based AI service (GPT models)",
                "https://api.openai.com/v1",
                "gpt-4o-mini"),

            AiProviderType.Anthropic => new ProviderInfo(
                "Anthropic",
                "Cloud-based AI service (Claude models)",
                "https://api.anthropic.com/v1",
                "claude-3-5-haiku-20241022"),

            AiProviderType.DeepSeek => new ProviderInfo(
                "DeepSeek",
                "Cloud-based AI service",
                "https://api.deepseek.com/v1",
                "deepseek-chat"),

            _ => throw new ArgumentException($"Unknown provider type: {providerType}")
        };
    }

    /// <summary>
    /// Get all available provider types with descriptions
    /// </summary>
    public static Dictionary<AiProviderType, string> GetProviderDescriptions()
    {
        return new Dictionary<AiProviderType, string>
        {
            [AiProviderType.Ollama] = "Ollama - Local model runner (recommended)",
            [AiProviderType.LmStudio] = "LM Studio - Local API server with GUI",
            [AiProviderType.OpenWebUi] = "Open Web UI - Web-based model management",
            [AiProviderType.OpenAI] = "OpenAI - Cloud-based AI service (GPT models)",
            [AiProviderType.Anthropic] = "Anthropic - Cloud-based AI service (Claude models)",
            [AiProviderType.DeepSeek] = "DeepSeek - Cloud-based AI service"
        };
    }

    /// <summary>
    /// Try to detect which providers are available
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static async Task<Dictionary<AiProviderType, bool>> DetectAvailableProvidersAsync(ILogger? logger = null)
    {
        var results = new Dictionary<AiProviderType, bool>();
        var providers = Enum.GetValues<AiProviderType>();
        var apiKeyStorage = new SecureApiKeyStorage();

        foreach (var providerType in providers)
        {
            try
            {
                var info = GetProviderInfo(providerType);
                IAiProvider provider;
                
                // For cloud providers, check if API key is available
                if (providerType == AiProviderType.OpenAI || 
                    providerType == AiProviderType.Anthropic || 
                    providerType == AiProviderType.DeepSeek)
                {
                    var hasApiKey = apiKeyStorage.HasApiKey(providerType.ToString());
                    if (hasApiKey)
                    {
                        var apiKey = apiKeyStorage.RetrieveApiKey(providerType.ToString());
                        provider = CreateProvider(providerType, info.DefaultModel, info.DefaultUrl, apiKey, logger, null);
                    }
                    else
                    {
                        // No API key available for cloud provider
                        results[providerType] = false;
                        continue;
                    }
                }
                else
                {
                    // Local providers don't need API keys
                    provider = CreateProvider(providerType, info.DefaultModel, info.DefaultUrl, logger, null);
                }
                
                var isAvailable = await provider.IsAvailableAsync();
                results[providerType] = isAvailable;
                provider.Dispose();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to detect availability for {Provider}", providerType);
                results[providerType] = false;
            }
        }

        return results;
    }

    /// <summary>
    /// Get the default model for a provider from configuration
    /// </summary>
    public static string GetDefaultModelForProvider(AiProviderType providerType, AppConfiguration config)
    {
        return providerType switch
        {
            AiProviderType.Ollama => config.OllamaDefaultModel ?? "llama3.2",
            AiProviderType.LmStudio => config.LmStudioDefaultModel ?? "default",
            AiProviderType.OpenWebUi => config.OpenWebUiDefaultModel ?? "default",
            AiProviderType.OpenAI => config.OpenAiDefaultModel ?? "gpt-4o-mini",
            AiProviderType.Anthropic => config.AnthropicDefaultModel ?? "claude-3-5-haiku-20241022",
            AiProviderType.DeepSeek => config.DeepSeekDefaultModel ?? "deepseek-chat",
            _ => "default"
        };
    }

    /// <summary>
    /// Get the provider URL from configuration
    /// </summary>
    public static string? GetProviderUrl(AppConfiguration config, AiProviderType providerType)
    {
        return providerType switch
        {
            AiProviderType.Ollama => config.OllamaUrl,
            AiProviderType.LmStudio => config.LmStudioUrl,
            AiProviderType.OpenWebUi => config.OpenWebUiUrl,
            AiProviderType.OpenAI => config.OpenAiUrl,
            AiProviderType.Anthropic => config.AnthropicUrl,
            AiProviderType.DeepSeek => config.DeepSeekUrl,
            _ => null
        };
    }
}

/// <summary>
/// Information about an AI provider
/// </summary>
public record ProviderInfo(
    string Name,
    string Description,
    string DefaultUrl,
    string DefaultModel);
