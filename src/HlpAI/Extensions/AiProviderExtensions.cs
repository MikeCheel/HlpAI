using Microsoft.Extensions.Logging;
using HlpAI.Models;
using HlpAI.Services;

namespace HlpAI.Extensions;

/// <summary>
/// Extension methods for AI providers to integrate with standardized error handling middleware
/// </summary>
public static class AiProviderExtensions
{
    private static readonly Dictionary<string, AiOperationMiddleware> _middlewareInstances = new();
    private static readonly object _lockObject = new();

    /// <summary>
    /// Execute an AI provider operation with standardized error handling
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="provider">AI provider instance</param>
    /// <param name="operation">The operation to execute</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <param name="context">Optional operation context</param>
    /// <param name="logger">Optional logger instance</param>
    /// <param name="config">Optional middleware configuration</param>
    /// <returns>Result of the operation wrapped in AiOperationResult</returns>
    public static async Task<AiOperationResult<T>> ExecuteWithMiddlewareAsync<T>(
        this IAiProvider provider,
        Func<Task<T>> operation,
        string operationName,
        AiOperationContext? context = null,
        ILogger? logger = null,
        AiOperationConfiguration? config = null)
    {
        var middleware = GetOrCreateMiddleware(provider.ProviderType.ToString(), logger, config);
        return await middleware.ExecuteAsync(operation, operationName, provider.ProviderType, context);
    }

    /// <summary>
    /// Execute GenerateAsync with middleware
    /// </summary>
    public static async Task<AiOperationResult<string>> GenerateWithMiddlewareAsync(
        this IAiProvider provider,
        string prompt,
        int maxTokens = 4000,
        ILogger? logger = null,
        AiOperationConfiguration? config = null)
    {
        var context = new AiOperationContext
        {
            MaxTokens = maxTokens,
            Prompt = prompt,
            TimeoutMs = 300000 // 5 minutes default
        };

        return await provider.ExecuteWithMiddlewareAsync(
            () => provider.GenerateAsync(prompt),
            "GenerateAsync",
            context,
            logger,
            config);
    }

    /// <summary>
    /// Execute IsAvailableAsync with middleware
    /// </summary>
    public static async Task<AiOperationResult<bool>> IsAvailableWithMiddlewareAsync(
        this IAiProvider provider,
        ILogger? logger = null,
        AiOperationConfiguration? config = null)
    {
        var context = new AiOperationContext
        {
            TimeoutMs = 30000 // 30 seconds for availability check
        };

        return await provider.ExecuteWithMiddlewareAsync(
            () => provider.IsAvailableAsync(),
            "IsAvailableAsync",
            context,
            logger,
            config);
    }

    /// <summary>
    /// Execute GetModelsAsync with middleware
    /// </summary>
    public static async Task<AiOperationResult<List<string>>> GetModelsWithMiddlewareAsync(
        this IAiProvider provider,
        ILogger? logger = null,
        AiOperationConfiguration? config = null)
    {
        var context = new AiOperationContext
        {
            TimeoutMs = 60000 // 1 minute for model listing
        };

        return await provider.ExecuteWithMiddlewareAsync(
            () => provider.GetModelsAsync(),
            "GetModelsAsync",
            context,
            logger,
            config);
    }

    /// <summary>
    /// Execute ValidateApiKeyAsync with middleware (for cloud providers)
    /// </summary>
    public static async Task<AiOperationResult<bool>> ValidateApiKeyWithMiddlewareAsync(
        this ICloudAiProvider provider,
        ILogger? logger = null,
        AiOperationConfiguration? config = null)
    {
        var context = new AiOperationContext
        {
            ApiKeyId = provider.GetType().Name, // Use provider type as key identifier
            TimeoutMs = 30000 // 30 seconds for API key validation
        };

        return await provider.ExecuteWithMiddlewareAsync(
            () => provider.ValidateApiKeyAsync(),
            "ValidateApiKeyAsync",
            context,
            logger,
            config);
    }

    /// <summary>
    /// Execute GetUsageInfoAsync with middleware (for cloud providers)
    /// </summary>
    public static async Task<AiOperationResult<ApiUsageInfo?>> GetUsageInfoWithMiddlewareAsync(
        this ICloudAiProvider provider,
        ILogger? logger = null,
        AiOperationConfiguration? config = null)
    {
        var context = new AiOperationContext
        {
            ApiKeyId = provider.GetType().Name,
            TimeoutMs = 30000
        };

        return await provider.ExecuteWithMiddlewareAsync(
            () => provider.GetUsageInfoAsync(),
            "GetUsageInfoAsync",
            context,
            logger,
            config);
    }

    /// <summary>
    /// Execute GetRateLimitInfoAsync with middleware (for cloud providers)
    /// </summary>
    public static async Task<AiOperationResult<RateLimitInfo?>> GetRateLimitInfoWithMiddlewareAsync(
        this ICloudAiProvider provider,
        ILogger? logger = null,
        AiOperationConfiguration? config = null)
    {
        var context = new AiOperationContext
        {
            ApiKeyId = provider.GetType().Name,
            TimeoutMs = 30000
        };

        return await provider.ExecuteWithMiddlewareAsync(
            () => provider.GetRateLimitInfoAsync(),
            "GetRateLimitInfoAsync",
            context,
            logger,
            config);
    }

    /// <summary>
    /// Get middleware statistics for a specific provider
    /// </summary>
    public static AiOperationStatistics? GetMiddlewareStatistics(this IAiProvider provider)
    {
        lock (_lockObject)
        {
            var key = provider.ProviderType.ToString();
            return _middlewareInstances.TryGetValue(key, out var middleware) 
                ? middleware.GetStatistics() 
                : null;
        }
    }

    /// <summary>
    /// Clear middleware statistics for a specific provider
    /// </summary>
    public static void ClearMiddlewareStatistics(this IAiProvider provider)
    {
        lock (_lockObject)
        {
            var key = provider.ProviderType.ToString();
            if (_middlewareInstances.TryGetValue(key, out var middleware))
            {
                middleware.ClearStatistics();
            }
        }
    }

    /// <summary>
    /// Get or create middleware instance for a provider
    /// </summary>
    private static AiOperationMiddleware GetOrCreateMiddleware(
        string providerKey,
        ILogger? logger,
        AiOperationConfiguration? config)
    {
        lock (_lockObject)
        {
            if (!_middlewareInstances.TryGetValue(providerKey, out var middleware))
            {
                middleware = new AiOperationMiddleware(logger as ILogger<AiOperationMiddleware>, config);
                _middlewareInstances[providerKey] = middleware;
            }
            return middleware;
        }
    }

    /// <summary>
    /// Create operation context from app configuration
    /// </summary>
    public static AiOperationContext CreateContextFromConfig(
        this IAiProvider provider,
        AppConfiguration appConfig,
        string? prompt = null)
    {
        var (maxTokens, timeoutMs) = provider.ProviderType switch
        {
            AiProviderType.OpenAI => (appConfig.OpenAiMaxTokens, appConfig.OpenAiTimeoutMinutes * 60000),
            AiProviderType.Anthropic => (appConfig.AnthropicMaxTokens, appConfig.AnthropicTimeoutMinutes * 60000),
            AiProviderType.DeepSeek => (appConfig.DeepSeekMaxTokens, appConfig.DeepSeekTimeoutMinutes * 60000),
            AiProviderType.LmStudio => (appConfig.LmStudioMaxTokens, appConfig.LmStudioTimeoutMinutes * 60000),
            AiProviderType.OpenWebUi => (appConfig.OpenWebUiMaxTokens, appConfig.OpenWebUiTimeoutMinutes * 60000),
            AiProviderType.Ollama => (4096, appConfig.OllamaTimeoutMinutes * 60000),
            _ => throw new ArgumentException($"Unknown provider type: {provider.ProviderType}")
        };

        return new AiOperationContext
        {
            MaxTokens = maxTokens,
            TimeoutMs = timeoutMs,
            Prompt = prompt,
            ApiKeyId = provider.GetType().Name,
            Metadata = new Dictionary<string, object>
            {
                ["ProviderType"] = provider.ProviderType.ToString(),
                ["BaseUrl"] = provider.BaseUrl ?? "N/A",
                ["CurrentModel"] = provider.CurrentModel ?? "N/A"
            }
        };
    }

    /// <summary>
    /// Execute operation with configuration-based context
    /// </summary>
    public static async Task<AiOperationResult<T>> ExecuteWithConfigContextAsync<T>(
        this IAiProvider provider,
        Func<Task<T>> operation,
        string operationName,
        AppConfiguration appConfig,
        string? prompt = null,
        ILogger? logger = null,
        AiOperationConfiguration? middlewareConfig = null)
    {
        var context = provider.CreateContextFromConfig(appConfig, prompt);
        return await provider.ExecuteWithMiddlewareAsync(operation, operationName, context, logger, middlewareConfig);
    }
}

/// <summary>
/// Builder for creating AI operation configurations
/// </summary>
public class AiOperationConfigurationBuilder
{
    private readonly AiOperationConfiguration _config = new();

    public AiOperationConfigurationBuilder WithMaxRetries(int maxRetries)
    {
        _config.MaxRetries = maxRetries;
        return this;
    }

    public AiOperationConfigurationBuilder WithRetryDelay(int baseDelayMs, int maxDelayMs)
    {
        _config.BaseRetryDelayMs = baseDelayMs;
        _config.MaxRetryDelayMs = maxDelayMs;
        return this;
    }

    public AiOperationConfigurationBuilder WithRateLimit(int maxRequests, int windowMinutes)
    {
        _config.EnableRateLimiting = true;
        _config.MaxRequestsPerWindow = maxRequests;
        _config.RateLimitWindowMinutes = windowMinutes;
        return this;
    }

    public AiOperationConfigurationBuilder DisableRateLimit()
    {
        _config.EnableRateLimiting = false;
        return this;
    }

    public AiOperationConfigurationBuilder WithMaxPromptLength(int maxLength)
    {
        _config.MaxPromptLength = maxLength;
        return this;
    }

    public AiOperationConfiguration Build() => _config;

    public static AiOperationConfigurationBuilder Create() => new();
}