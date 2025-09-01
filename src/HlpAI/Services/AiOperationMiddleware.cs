using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace HlpAI.Services;

/// <summary>
/// Standardized error handling middleware for AI operations
/// Provides consistent error handling, logging, retry logic, and monitoring for all AI provider operations
/// </summary>
public class AiOperationMiddleware
{
    private readonly ILogger<AiOperationMiddleware>? _logger;
    private readonly SecurityAuditService _auditService;
    private readonly AiOperationConfiguration _config;
    private readonly Dictionary<string, List<DateTime>> _rateLimitTracker = new();
    private readonly Dictionary<string, int> _retryCountTracker = new();
    private readonly object _lockObject = new();

    public AiOperationMiddleware(ILogger<AiOperationMiddleware>? logger = null, AiOperationConfiguration? config = null)
    {
        _logger = logger;
        _auditService = new SecurityAuditService(logger as ILogger<SecurityAuditService>);
        _config = config ?? new AiOperationConfiguration();
    }

    /// <summary>
    /// Execute an AI operation with standardized error handling and monitoring
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">The AI operation to execute</param>
    /// <param name="operationName">Name of the operation for logging and monitoring</param>
    /// <param name="providerType">AI provider type</param>
    /// <param name="context">Additional context for the operation</param>
    /// <returns>Result of the operation wrapped in AiOperationResult</returns>
    public async Task<AiOperationResult<T>> ExecuteAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        AiProviderType providerType,
        AiOperationContext? context = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var operationId = Guid.NewGuid().ToString("N")[..8];
        var providerName = providerType.ToString();
        
        _logger?.LogInformation("[{OperationId}] Starting AI operation: {OperationName} with provider: {Provider}", 
            operationId, operationName, providerName);

        try
        {
            // Pre-operation validation
            var validationResult = ValidateOperation(operationName, providerType, context);
            if (!validationResult.IsValid)
            {
                return AiOperationResult<T>.Failure(
                    new AiOperationException($"Operation validation failed: {string.Join(", ", validationResult.Errors)}"),
                    operationName,
                    providerName,
                    stopwatch.Elapsed);
            }

            // Rate limiting check
            if (!CheckRateLimit(operationName, providerName))
            {
                _logger?.LogWarning("[{OperationId}] Rate limit exceeded for operation: {OperationName}", operationId, operationName);
                return AiOperationResult<T>.Failure(
                    new AiOperationException("Rate limit exceeded. Please try again later."),
                    operationName,
                    providerName,
                    stopwatch.Elapsed);
            }

            // Execute operation with retry logic
            var result = await ExecuteWithRetryAsync(operation, operationName, providerName, operationId);
            
            stopwatch.Stop();
            
            // Log successful operation
            _logger?.LogInformation("[{OperationId}] AI operation completed successfully: {OperationName} in {ElapsedMs}ms", 
                operationId, operationName, stopwatch.ElapsedMilliseconds);
            
            // Audit successful operation
            _auditService.LogApiKeyUsage(providerName, operationName, true, context?.ApiKeyId);
            
            return AiOperationResult<T>.Success(result, operationName, providerName, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Log and audit failed operation
            _logger?.LogError(ex, "[{OperationId}] AI operation failed: {OperationName} with provider: {Provider} after {ElapsedMs}ms", 
                operationId, operationName, providerName, stopwatch.ElapsedMilliseconds);
            
            _auditService.LogApiKeyUsage(providerName, operationName, false, context?.ApiKeyId);
            
            // Categorize and handle different types of exceptions
            var handledException = HandleException(ex, operationName, providerName);
            
            return AiOperationResult<T>.Failure(handledException, operationName, providerName, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Execute an AI operation with retry logic
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        string providerName,
        string operationId)
    {
        var maxRetries = _config.MaxRetries;
        var baseDelay = _config.BaseRetryDelayMs;
        
        for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            try
            {
                if (attempt > 1)
                {
                    var delay = CalculateRetryDelay(attempt - 1, baseDelay);
                    _logger?.LogInformation("[{OperationId}] Retrying operation: {OperationName} (attempt {Attempt}/{MaxAttempts}) after {DelayMs}ms", 
                        operationId, operationName, attempt, maxRetries + 1, delay);
                    await Task.Delay(delay);
                }
                
                return await operation();
            }
            catch (Exception ex) when (attempt <= maxRetries && IsRetryableException(ex))
            {
                _logger?.LogWarning(ex, "[{OperationId}] Retryable error on attempt {Attempt}: {ErrorMessage}", 
                    operationId, attempt, ex.Message);
                
                // Track retry count
                lock (_lockObject)
                {
                    var key = $"{providerName}:{operationName}";
                    _retryCountTracker[key] = _retryCountTracker.GetValueOrDefault(key, 0) + 1;
                }
            }
        }
        
        // This should never be reached due to the loop logic, but included for completeness
        throw new AiOperationException($"Operation failed after {maxRetries} retries");
    }

    /// <summary>
    /// Validate operation before execution
    /// </summary>
    private OperationValidationResult ValidateOperation(
        string operationName,
        AiProviderType providerType,
        AiOperationContext? context)
    {
        var errors = new List<string>();
        
        // Validate operation name
        if (string.IsNullOrWhiteSpace(operationName))
        {
            errors.Add("Operation name cannot be null or empty");
        }
        
        // Validate provider type
        if (!Enum.IsDefined(typeof(AiProviderType), providerType))
        {
            errors.Add($"Invalid provider type: {providerType}");
        }
        
        // Validate context if provided
        if (context != null)
        {
            if (context.MaxTokens <= 0)
            {
                errors.Add("MaxTokens must be greater than 0");
            }
            
            if (context.TimeoutMs <= 0)
            {
                errors.Add("TimeoutMs must be greater than 0");
            }
            
            if (!string.IsNullOrEmpty(context.Prompt) && context.Prompt.Length > _config.MaxPromptLength)
            {
                errors.Add($"Prompt length ({context.Prompt.Length}) exceeds maximum allowed ({_config.MaxPromptLength})");
            }
        }
        
        return new OperationValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Check if operation is within rate limits
    /// </summary>
    private bool CheckRateLimit(string operationName, string providerName)
    {
        if (!_config.EnableRateLimiting)
            return true;
            
        lock (_lockObject)
        {
            var key = $"{providerName}:{operationName}";
            var now = DateTime.UtcNow;
            var windowStart = now.AddMinutes(-_config.RateLimitWindowMinutes);
            
            if (!_rateLimitTracker.ContainsKey(key))
            {
                _rateLimitTracker[key] = new List<DateTime>();
            }
            
            var requests = _rateLimitTracker[key];
            
            // Remove old requests outside the window
            requests.RemoveAll(r => r < windowStart);
            
            // Check if we're within limits
            if (requests.Count >= _config.MaxRequestsPerWindow)
            {
                return false;
            }
            
            // Add current request
            requests.Add(now);
            return true;
        }
    }

    /// <summary>
    /// Calculate retry delay using exponential backoff with jitter
    /// </summary>
    private int CalculateRetryDelay(int attempt, int baseDelayMs)
    {
        var exponentialDelay = baseDelayMs * Math.Pow(2, attempt - 1);
        var maxDelay = _config.MaxRetryDelayMs;
        var delay = Math.Min(exponentialDelay, maxDelay);
        
        // Add jitter to prevent thundering herd
        var jitter = Random.Shared.NextDouble() * 0.1 * delay; // 10% jitter
        return (int)(delay + jitter);
    }

    /// <summary>
    /// Determine if an exception is retryable
    /// </summary>
    private bool IsRetryableException(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpEx => IsRetryableHttpException(httpEx),
            TaskCanceledException => true, // Timeout
            OperationCanceledException => true, // Cancellation
            AiOperationException aiEx => aiEx.IsRetryable,
            _ => false
        };
    }

    /// <summary>
    /// Determine if an HTTP exception is retryable
    /// </summary>
    private static bool IsRetryableHttpException(HttpRequestException ex)
    {
        var message = ex.Message.ToLowerInvariant();
        
        // Retryable HTTP conditions
        return message.Contains("timeout") ||
               message.Contains("connection reset") ||
               message.Contains("connection refused") ||
               message.Contains("network") ||
               message.Contains("502") || // Bad Gateway
               message.Contains("503") || // Service Unavailable
               message.Contains("504");   // Gateway Timeout
    }

    /// <summary>
    /// Handle and categorize exceptions
    /// </summary>
    private AiOperationException HandleException(Exception ex, string operationName, string providerName)
    {
        return ex switch
        {
            AiOperationException aiEx => aiEx,
            HttpRequestException httpEx => new AiOperationException(
                $"HTTP error in {operationName}: {httpEx.Message}", 
                httpEx, 
                AiOperationErrorType.NetworkError,
                IsRetryableHttpException(httpEx)),
            TaskCanceledException => new AiOperationException(
                $"Operation {operationName} timed out", 
                ex, 
                AiOperationErrorType.Timeout, 
                true),
            UnauthorizedAccessException => new AiOperationException(
                $"Authentication failed for {providerName}", 
                ex, 
                AiOperationErrorType.AuthenticationError, 
                false),
            ArgumentException argEx => new AiOperationException(
                $"Invalid argument in {operationName}: {argEx.Message}", 
                argEx, 
                AiOperationErrorType.ValidationError, 
                false),
            InvalidOperationException invOpEx => new AiOperationException(
                $"Invalid operation {operationName}: {invOpEx.Message}", 
                invOpEx, 
                AiOperationErrorType.ConfigurationError, 
                false),
            _ => new AiOperationException(
                $"Unexpected error in {operationName}: {ex.Message}", 
                ex, 
                AiOperationErrorType.UnknownError, 
                false)
        };
    }

    /// <summary>
    /// Get operation statistics
    /// </summary>
    public AiOperationStatistics GetStatistics()
    {
        lock (_lockObject)
        {
            var totalRetries = _retryCountTracker.Values.Sum();
            var operationsWithRetries = _retryCountTracker.Count;
            
            return new AiOperationStatistics
            {
                TotalRetries = totalRetries,
                OperationsWithRetries = operationsWithRetries,
                ActiveRateLimitKeys = _rateLimitTracker.Count,
                RetryCountByOperation = new Dictionary<string, int>(_retryCountTracker)
            };
        }
    }

    /// <summary>
    /// Clear statistics and rate limit data
    /// </summary>
    public void ClearStatistics()
    {
        lock (_lockObject)
        {
            _retryCountTracker.Clear();
            _rateLimitTracker.Clear();
        }
    }
}

/// <summary>
/// Configuration for AI operation middleware
/// </summary>
public class AiOperationConfiguration
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Base delay in milliseconds for retry attempts
    /// </summary>
    public int BaseRetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum delay in milliseconds for retry attempts
    /// </summary>
    public int MaxRetryDelayMs { get; set; } = 30000;

    /// <summary>
    /// Enable rate limiting for operations
    /// </summary>
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>
    /// Maximum requests per rate limit window
    /// </summary>
    public int MaxRequestsPerWindow { get; set; } = 60;

    /// <summary>
    /// Rate limit window in minutes
    /// </summary>
    public int RateLimitWindowMinutes { get; set; } = 1;

    /// <summary>
    /// Maximum allowed prompt length
    /// </summary>
    public int MaxPromptLength { get; set; } = 100000;
}

/// <summary>
/// Context information for AI operations
/// </summary>
public class AiOperationContext
{
    /// <summary>
    /// API key identifier for auditing
    /// </summary>
    public string? ApiKeyId { get; set; }

    /// <summary>
    /// Maximum tokens for the operation
    /// </summary>
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Timeout in milliseconds for the operation
    /// </summary>
    public int TimeoutMs { get; set; } = 300000; // 5 minutes

    /// <summary>
    /// Prompt text for validation
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// Additional metadata for the operation
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Result of an AI operation
/// </summary>
/// <typeparam name="T">Type of the operation result</typeparam>
public class AiOperationResult<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public AiOperationException? Error { get; private set; }
    public string OperationName { get; private set; }
    public string ProviderName { get; private set; }
    public TimeSpan Duration { get; private set; }
    public DateTime Timestamp { get; private set; }

    private AiOperationResult(bool isSuccess, T? data, AiOperationException? error, 
        string operationName, string providerName, TimeSpan duration)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        OperationName = operationName;
        ProviderName = providerName;
        Duration = duration;
        Timestamp = DateTime.UtcNow;
    }

    public static AiOperationResult<T> Success(T data, string operationName, string providerName, TimeSpan duration)
    {
        return new AiOperationResult<T>(true, data, null, operationName, providerName, duration);
    }

    public static AiOperationResult<T> Failure(AiOperationException error, string operationName, string providerName, TimeSpan duration)
    {
        return new AiOperationResult<T>(false, default, error, operationName, providerName, duration);
    }
}

/// <summary>
/// Custom exception for AI operations
/// </summary>
public class AiOperationException : Exception
{
    public AiOperationErrorType ErrorType { get; }
    public bool IsRetryable { get; }

    public AiOperationException(string message, AiOperationErrorType errorType = AiOperationErrorType.UnknownError, bool isRetryable = false) 
        : base(message)
    {
        ErrorType = errorType;
        IsRetryable = isRetryable;
    }

    public AiOperationException(string message, Exception innerException, AiOperationErrorType errorType = AiOperationErrorType.UnknownError, bool isRetryable = false) 
        : base(message, innerException)
    {
        ErrorType = errorType;
        IsRetryable = isRetryable;
    }
}

/// <summary>
/// Types of AI operation errors
/// </summary>
public enum AiOperationErrorType
{
    UnknownError,
    NetworkError,
    Timeout,
    AuthenticationError,
    ValidationError,
    ConfigurationError,
    RateLimitExceeded,
    ModelNotAvailable,
    InsufficientQuota
}

/// <summary>
/// Validation result for operations
/// </summary>
public record OperationValidationResult(bool IsValid, List<string> Errors);

/// <summary>
/// Statistics for AI operations
/// </summary>
public class AiOperationStatistics
{
    public int TotalRetries { get; set; }
    public int OperationsWithRetries { get; set; }
    public int ActiveRateLimitKeys { get; set; }
    public Dictionary<string, int> RetryCountByOperation { get; set; } = new();
}