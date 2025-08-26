namespace HlpAI.Services;

/// <summary>
/// Interface for cloud-based AI providers that require API key authentication
/// </summary>
public interface ICloudAiProvider : IAiProvider
{
    /// <summary>
    /// API key for authentication with the cloud service
    /// </summary>
    string ApiKey { get; }

    /// <summary>
    /// Validate the API key with the cloud service
    /// </summary>
    /// <returns>True if the API key is valid and the service is accessible</returns>
    Task<bool> ValidateApiKeyAsync();

    /// <summary>
    /// Get usage information for the API key (if supported by the provider)
    /// </summary>
    /// <returns>Usage information or null if not supported</returns>
    Task<ApiUsageInfo?> GetUsageInfoAsync();

    /// <summary>
    /// Get rate limit information for the API key (if supported by the provider)
    /// </summary>
    /// <returns>Rate limit information or null if not supported</returns>
    Task<RateLimitInfo?> GetRateLimitInfoAsync();
}

/// <summary>
/// API usage information for cloud providers
/// </summary>
public record ApiUsageInfo(
    int RequestsUsed,
    int RequestsLimit,
    int TokensUsed,
    int TokensLimit,
    DateTime ResetDate
);

/// <summary>
/// Rate limit information for cloud providers
/// </summary>
public record RateLimitInfo(
    int RequestsPerMinute,
    int RequestsRemaining,
    int TokensPerMinute,
    int TokensRemaining,
    DateTime ResetTime
);