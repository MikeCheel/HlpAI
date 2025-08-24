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
        ILogger? logger = null)
    {
        // Handle empty or whitespace URLs by using default
        string GetEffectiveUrl(string defaultUrl) => 
            string.IsNullOrWhiteSpace(baseUrl) ? defaultUrl : baseUrl;

        return providerType switch
        {
            AiProviderType.Ollama => new OllamaClient(
                GetEffectiveUrl("http://localhost:11434"),
                model,
                logger),

            AiProviderType.LmStudio => new LmStudioProvider(
                GetEffectiveUrl("http://localhost:1234"),
                model,
                logger),

            AiProviderType.OpenWebUi => new OpenWebUiProvider(
                GetEffectiveUrl("http://localhost:3000"),
                model,
                logger),

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
            [AiProviderType.OpenWebUi] = "Open Web UI - Web-based model management"
        };
    }

    /// <summary>
    /// Try to detect which providers are available
    /// </summary>
    public static async Task<Dictionary<AiProviderType, bool>> DetectAvailableProvidersAsync(ILogger? logger = null)
    {
        var results = new Dictionary<AiProviderType, bool>();
        var providers = Enum.GetValues<AiProviderType>();

        foreach (var providerType in providers)
        {
            try
            {
                var info = GetProviderInfo(providerType);
                var provider = CreateProvider(providerType, info.DefaultModel, info.DefaultUrl, logger);
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
