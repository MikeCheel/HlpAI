using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using HlpAI.Models;

namespace HlpAI.Services;

/// <summary>
/// Result of connectivity testing
/// </summary>
public record ConnectivityResult(bool IsAvailable, long ResponseTime, string ErrorMessage);

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
                GetEffectiveUrl(AiProviderConstants.DefaultUrls.Ollama),
                model,
                logger,
                config),

            AiProviderType.LmStudio => new LmStudioProvider(
                GetEffectiveUrl(AiProviderConstants.DefaultUrls.LmStudio),
                model,
                logger,
                config),

            AiProviderType.OpenWebUi => new OpenWebUiProvider(
                GetEffectiveUrl(AiProviderConstants.DefaultUrls.OpenWebUi),
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
                GetEffectiveUrl(AiProviderConstants.DefaultUrls.Ollama),
                model,
                logger,
                config),

            AiProviderType.LmStudio => new LmStudioProvider(
                GetEffectiveUrl(AiProviderConstants.DefaultUrls.LmStudio),
                model,
                logger,
                config),

            AiProviderType.OpenWebUi => new OpenWebUiProvider(
                GetEffectiveUrl(AiProviderConstants.DefaultUrls.OpenWebUi),
                model,
                logger,
                config),

            AiProviderType.OpenAI => new OpenAiProvider(
                apiKey ?? throw new ArgumentNullException(nameof(apiKey), "OpenAI provider requires an API key"),
                model,
                GetEffectiveUrl(AiProviderConstants.DefaultUrls.OpenAi),
                logger,
                config),

            AiProviderType.Anthropic => new AnthropicProvider(
                apiKey ?? throw new ArgumentNullException(nameof(apiKey), "Anthropic provider requires an API key"),
                model,
                GetEffectiveUrl(AiProviderConstants.DefaultUrls.Anthropic),
                logger,
                config),

            AiProviderType.DeepSeek => new DeepSeekProvider(
                apiKey ?? string.Empty,
                model,
                GetEffectiveUrl(AiProviderConstants.DefaultUrls.DeepSeek),
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
                AiProviderConstants.DefaultUrls.Ollama,
                AiProviderConstants.DefaultModels.Ollama),

            AiProviderType.LmStudio => new ProviderInfo(
                "LM Studio",
                "Local API server with GUI",
                AiProviderConstants.DefaultUrls.LmStudio,
                AiProviderConstants.DefaultModels.LmStudio),

            AiProviderType.OpenWebUi => new ProviderInfo(
                "Open Web UI",
                "Web-based model management",
                AiProviderConstants.DefaultUrls.OpenWebUi,
                AiProviderConstants.DefaultModels.OpenWebUi),

            AiProviderType.OpenAI => new ProviderInfo(
                "OpenAI",
                "Cloud-based AI service (GPT models)",
                AiProviderConstants.DefaultUrls.OpenAiV1,
                AiProviderConstants.DefaultModels.OpenAi),

            AiProviderType.Anthropic => new ProviderInfo(
                "Anthropic",
                "Cloud-based AI service (Claude models)",
                AiProviderConstants.DefaultUrls.AnthropicV1,
                AiProviderConstants.DefaultModels.Anthropic),

            AiProviderType.DeepSeek => new ProviderInfo(
                "DeepSeek",
                "Cloud-based AI service",
                AiProviderConstants.DefaultUrls.DeepSeek,
                AiProviderConstants.DefaultModels.DeepSeek),

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
    /// Check if a provider requires an API key
    /// </summary>
    public static bool RequiresApiKey(AiProviderType providerType)
    {
        return providerType switch
        {
            AiProviderType.OpenAI => true,
            AiProviderType.Anthropic => true,
            AiProviderType.DeepSeek => true,
            _ => false
        };
    }

    /// <summary>
    /// Try to detect which providers are available with detailed error information
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static async Task<Dictionary<AiProviderType, ConnectivityResult>> DetectAvailableProvidersAsync(ILogger? logger = null)
    {
        var results = new Dictionary<AiProviderType, ConnectivityResult>();
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
                        results[providerType] = new ConnectivityResult(false, 0, "No API key configured");
                        continue;
                    }
                }
                else
                {
                    // Local providers don't need API keys
                    provider = CreateProvider(providerType, info.DefaultModel, info.DefaultUrl, null, logger, null);
                }
                
                var connectivityResult = await TestProviderConnectivityAsync(provider);
                results[providerType] = connectivityResult;
                provider.Dispose();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to detect availability for {Provider}", providerType);
                results[providerType] = new ConnectivityResult(false, 0, $"Configuration error: {ex.Message}");
            }
        }

        return results;
    }

    /// <summary>
    /// Tests provider connectivity with enhanced error reporting and timing
    /// </summary>
    private static async Task<ConnectivityResult> TestProviderConnectivityAsync(IAiProvider provider)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Test with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            var isAvailable = await provider.IsAvailableAsync();
            stopwatch.Stop();
            
            if (isAvailable)
            {
                return new ConnectivityResult(true, stopwatch.ElapsedMilliseconds, "Provider is available");
            }
            else
            {
                return new ConnectivityResult(false, stopwatch.ElapsedMilliseconds, "Provider is not responding or not available");
            }
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            return new ConnectivityResult(false, stopwatch.ElapsedMilliseconds, "Connection timeout (10 seconds)");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            return new ConnectivityResult(false, stopwatch.ElapsedMilliseconds, $"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ConnectivityResult(false, stopwatch.ElapsedMilliseconds, $"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the default model for a provider from configuration
    /// </summary>
    public static string GetDefaultModelForProvider(AiProviderType providerType, AppConfiguration config)
    {
        return providerType switch
        {
            AiProviderType.Ollama => config.OllamaDefaultModel ?? AiProviderConstants.DefaultModels.Ollama,
            AiProviderType.LmStudio => config.LmStudioDefaultModel ?? AiProviderConstants.DefaultModels.LmStudio,
            AiProviderType.OpenWebUi => config.OpenWebUiDefaultModel ?? AiProviderConstants.DefaultModels.OpenWebUi,
            AiProviderType.OpenAI => config.OpenAiDefaultModel ?? AiProviderConstants.DefaultModels.OpenAi,
            AiProviderType.Anthropic => config.AnthropicDefaultModel ?? AiProviderConstants.DefaultModels.Anthropic,
            AiProviderType.DeepSeek => config.DeepSeekDefaultModel ?? AiProviderConstants.DefaultModels.DeepSeek,
            _ => AiProviderConstants.DefaultModels.LmStudio
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
