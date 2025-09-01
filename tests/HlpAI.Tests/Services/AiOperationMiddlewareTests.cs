using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using HlpAI.Services;
using HlpAI.Models;
using HlpAI.Extensions;
using System.Net;

namespace HlpAI.Tests.Services;

public class AiOperationMiddlewareTests
{
    private readonly Mock<ILogger<AiOperationMiddleware>> _mockLogger;
    private readonly AiOperationMiddleware _middleware;
    private readonly AiOperationConfiguration _config;

    public AiOperationMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<AiOperationMiddleware>>();
        _config = new AiOperationConfiguration
        {
            MaxRetries = 2,
            BaseRetryDelayMs = 100,
            MaxRetryDelayMs = 1000,
            EnableRateLimiting = true,
            MaxRequestsPerWindow = 5,
            RateLimitWindowMinutes = 1,
            MaxPromptLength = 1000
        };
        _middleware = new AiOperationMiddleware(_mockLogger.Object, _config);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulOperation_ReturnsSuccess()
    {
        // Arrange
        var expectedResult = "test result";
        var operation = () => Task.FromResult(expectedResult);
        
        // Act
        var result = await _middleware.ExecuteAsync(
            operation, 
            "TestOperation", 
            AiProviderType.OpenAI);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedResult, result.Data);
        Assert.Null(result.Error);
        Assert.Equal("TestOperation", result.OperationName);
        Assert.Equal("OpenAI", result.ProviderName);
    }

    [Fact]
    public async Task ExecuteAsync_OperationThrowsException_ReturnsFailure()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test error");
        var operation = () => Task.FromException<string>(expectedException);
        
        // Act
        var result = await _middleware.ExecuteAsync(
            operation, 
            "TestOperation", 
            AiProviderType.OpenAI);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.NotNull(result.Error);
        Assert.Equal(AiOperationErrorType.ConfigurationError, result.Error.ErrorType);
        Assert.Contains("Test error", result.Error.Message);
    }

    [Fact]
    public async Task ExecuteAsync_HttpRequestException_ReturnsNetworkError()
    {
        // Arrange
        var httpException = new HttpRequestException("Connection timeout");
        var operation = () => Task.FromException<string>(httpException);
        
        // Act
        var result = await _middleware.ExecuteAsync(
            operation, 
            "TestOperation", 
            AiProviderType.OpenAI);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(AiOperationErrorType.NetworkError, result.Error!.ErrorType);
        Assert.True(result.Error.IsRetryable);
    }

    [Fact]
    public async Task ExecuteAsync_TaskCanceledException_ReturnsTimeoutError()
    {
        // Arrange
        var timeoutException = new TaskCanceledException("Operation timed out");
        var operation = () => Task.FromException<string>(timeoutException);
        
        // Act
        var result = await _middleware.ExecuteAsync(
            operation, 
            "TestOperation", 
            AiProviderType.OpenAI);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(AiOperationErrorType.Timeout, result.Error!.ErrorType);
        Assert.True(result.Error.IsRetryable);
    }

    [Fact]
    public async Task ExecuteAsync_UnauthorizedAccessException_ReturnsAuthenticationError()
    {
        // Arrange
        var authException = new UnauthorizedAccessException("Invalid API key");
        var operation = () => Task.FromException<string>(authException);
        
        // Act
        var result = await _middleware.ExecuteAsync(
            operation, 
            "TestOperation", 
            AiProviderType.OpenAI);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(AiOperationErrorType.AuthenticationError, result.Error!.ErrorType);
        Assert.False(result.Error.IsRetryable);
    }

    [Fact]
    public async Task ExecuteAsync_ArgumentException_ReturnsValidationError()
    {
        // Arrange
        var argException = new ArgumentException("Invalid parameter");
        var operation = () => Task.FromException<string>(argException);
        
        // Act
        var result = await _middleware.ExecuteAsync(
            operation, 
            "TestOperation", 
            AiProviderType.OpenAI);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(AiOperationErrorType.ValidationError, result.Error!.ErrorType);
        Assert.False(result.Error.IsRetryable);
    }

    [Fact]
    public async Task ExecuteAsync_RetryableException_RetriesOperation()
    {
        // Arrange
        var callCount = 0;
        var operation = () =>
        {
            callCount++;
            if (callCount <= 2)
            {
                throw new HttpRequestException("Temporary network error");
            }
            return Task.FromResult("success");
        };
        
        // Act
        var result = await _middleware.ExecuteAsync(
            operation, 
            "TestOperation", 
            AiProviderType.OpenAI);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("success", result.Data);
        Assert.Equal(3, callCount); // Initial attempt + 2 retries
    }

    [Fact]
    public async Task ExecuteAsync_ExceedsMaxRetries_ReturnsFailure()
    {
        // Arrange
        var operation = () => Task.FromException<string>(new HttpRequestException("Persistent error"));
        
        // Act
        var result = await _middleware.ExecuteAsync(
            operation, 
            "TestOperation", 
            AiProviderType.OpenAI);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(AiOperationErrorType.NetworkError, result.Error!.ErrorType);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidOperationName_ReturnsValidationError()
    {
        // Arrange
        var operation = () => Task.FromResult("result");
        
        // Act
        var result = await _middleware.ExecuteAsync(
            operation, 
            "", // Empty operation name
            AiProviderType.OpenAI);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Operation validation failed", result.Error!.Message);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidContext_ReturnsValidationError()
    {
        // Arrange
        var operation = () => Task.FromResult("result");
        var context = new AiOperationContext
        {
            MaxTokens = -1, // Invalid
            TimeoutMs = -1  // Invalid
        };
        
        // Act
        var result = await _middleware.ExecuteAsync(
            operation, 
            "TestOperation", 
            AiProviderType.OpenAI,
            context);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("MaxTokens must be greater than 0", result.Error!.Message);
    }

    [Fact]
    public async Task ExecuteAsync_PromptTooLong_ReturnsValidationError()
    {
        // Arrange
        var operation = () => Task.FromResult("result");
        var longPrompt = new string('a', _config.MaxPromptLength + 1);
        var context = new AiOperationContext
        {
            Prompt = longPrompt,
            MaxTokens = 100,
            TimeoutMs = 1000
        };
        
        // Act
        var result = await _middleware.ExecuteAsync(
            operation, 
            "TestOperation", 
            AiProviderType.OpenAI,
            context);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Prompt length", result.Error!.Message);
        Assert.Contains("exceeds maximum allowed", result.Error!.Message);
    }

    [Fact]
    public async Task ExecuteAsync_RateLimitExceeded_ReturnsRateLimitError()
    {
        // Arrange
        var operation = () => Task.FromResult("result");
        
        // Execute operations up to the limit
        for (int i = 0; i < _config.MaxRequestsPerWindow; i++)
        {
            await _middleware.ExecuteAsync(operation, "TestOperation", AiProviderType.OpenAI);
        }
        
        // Act - This should exceed the rate limit
        var result = await _middleware.ExecuteAsync(operation, "TestOperation", AiProviderType.OpenAI);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Rate limit exceeded", result.Error!.Message);
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectStatistics()
    {
        // Act
        var stats = _middleware.GetStatistics();
        
        // Assert
        Assert.NotNull(stats);
        Assert.Equal(0, stats.TotalRetries);
        Assert.Equal(0, stats.OperationsWithRetries);
        Assert.Equal(0, stats.ActiveRateLimitKeys);
    }

    [Fact]
    public void ClearStatistics_ClearsAllStatistics()
    {
        // Arrange - Execute some operations to generate statistics
        var operation = () => Task.FromResult("result");
        _middleware.ExecuteAsync(operation, "TestOperation", AiProviderType.OpenAI).Wait();
        
        // Act
        _middleware.ClearStatistics();
        var stats = _middleware.GetStatistics();
        
        // Assert
        Assert.Equal(0, stats.TotalRetries);
        Assert.Equal(0, stats.OperationsWithRetries);
        Assert.Equal(0, stats.ActiveRateLimitKeys);
    }

    [Fact]
    public void AiOperationConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new AiOperationConfiguration();
        
        // Assert
        Assert.Equal(3, config.MaxRetries);
        Assert.Equal(1000, config.BaseRetryDelayMs);
        Assert.Equal(30000, config.MaxRetryDelayMs);
        Assert.True(config.EnableRateLimiting);
        Assert.Equal(60, config.MaxRequestsPerWindow);
        Assert.Equal(1, config.RateLimitWindowMinutes);
        Assert.Equal(100000, config.MaxPromptLength);
    }

    [Fact]
    public void AiOperationResult_Success_CreatesCorrectResult()
    {
        // Arrange
        var data = "test data";
        var operationName = "TestOp";
        var providerName = "TestProvider";
        var duration = TimeSpan.FromMilliseconds(100);
        
        // Act
        var result = AiOperationResult<string>.Success(data, operationName, providerName, duration);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(data, result.Data);
        Assert.Null(result.Error);
        Assert.Equal(operationName, result.OperationName);
        Assert.Equal(providerName, result.ProviderName);
        Assert.Equal(duration, result.Duration);
    }

    [Fact]
    public void AiOperationResult_Failure_CreatesCorrectResult()
    {
        // Arrange
        var error = new AiOperationException("Test error");
        var operationName = "TestOp";
        var providerName = "TestProvider";
        var duration = TimeSpan.FromMilliseconds(100);
        
        // Act
        var result = AiOperationResult<string>.Failure(error, operationName, providerName, duration);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(error, result.Error);
        Assert.Equal(operationName, result.OperationName);
        Assert.Equal(providerName, result.ProviderName);
        Assert.Equal(duration, result.Duration);
    }

    [Fact]
    public void AiOperationException_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var message = "Test error";
        var errorType = AiOperationErrorType.NetworkError;
        var isRetryable = true;
        
        // Act
        var exception = new AiOperationException(message, errorType, isRetryable);
        
        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(errorType, exception.ErrorType);
        Assert.Equal(isRetryable, exception.IsRetryable);
    }

    [Fact]
    public void AiOperationException_WithInnerException_SetsPropertiesCorrectly()
    {
        // Arrange
        var message = "Test error";
        var innerException = new InvalidOperationException("Inner error");
        var errorType = AiOperationErrorType.ConfigurationError;
        var isRetryable = false;
        
        // Act
        var exception = new AiOperationException(message, innerException, errorType, isRetryable);
        
        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(innerException, exception.InnerException);
        Assert.Equal(errorType, exception.ErrorType);
        Assert.Equal(isRetryable, exception.IsRetryable);
    }

    [Fact]
    public void OperationValidationResult_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var isValid = false;
        var errors = new List<string> { "Error 1", "Error 2" };
        
        // Act
        var result = new OperationValidationResult(isValid, errors);
        
        // Assert
        Assert.Equal(isValid, result.IsValid);
        Assert.Equal(errors, result.Errors);
    }

    [Theory]
    [InlineData(AiOperationErrorType.UnknownError)]
    [InlineData(AiOperationErrorType.NetworkError)]
    [InlineData(AiOperationErrorType.Timeout)]
    [InlineData(AiOperationErrorType.AuthenticationError)]
    [InlineData(AiOperationErrorType.ValidationError)]
    [InlineData(AiOperationErrorType.ConfigurationError)]
    [InlineData(AiOperationErrorType.RateLimitExceeded)]
    [InlineData(AiOperationErrorType.ModelNotAvailable)]
    [InlineData(AiOperationErrorType.InsufficientQuota)]
    public void AiOperationErrorType_AllValuesAreDefined(AiOperationErrorType errorType)
    {
        // Act & Assert
        Assert.True(Enum.IsDefined(typeof(AiOperationErrorType), errorType));
    }

    [Fact]
    public async Task ExecuteAsync_DisabledRateLimit_DoesNotEnforceRateLimit()
    {
        // Arrange
        var configWithoutRateLimit = new AiOperationConfiguration
        {
            EnableRateLimiting = false,
            MaxRequestsPerWindow = 1 // Very low limit
        };
        var middlewareWithoutRateLimit = new AiOperationMiddleware(_mockLogger.Object, configWithoutRateLimit);
        var operation = () => Task.FromResult("result");
        
        // Act - Execute multiple operations that would exceed rate limit if enabled
        var result1 = await middlewareWithoutRateLimit.ExecuteAsync(operation, "TestOperation", AiProviderType.OpenAI);
        var result2 = await middlewareWithoutRateLimit.ExecuteAsync(operation, "TestOperation", AiProviderType.OpenAI);
        
        // Assert - Both should succeed
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_DifferentProviders_SeparateRateLimits()
    {
        // Arrange
        var operation = () => Task.FromResult("result");
        
        // Execute operations up to the limit for OpenAI
        for (int i = 0; i < _config.MaxRequestsPerWindow; i++)
        {
            await _middleware.ExecuteAsync(operation, "TestOperation", AiProviderType.OpenAI);
        }
        
        // Act - Try with a different provider (should not be rate limited)
        var result = await _middleware.ExecuteAsync(operation, "TestOperation", AiProviderType.Anthropic);
        
        // Assert
        Assert.True(result.IsSuccess);
    }
}