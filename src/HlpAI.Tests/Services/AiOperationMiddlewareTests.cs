using Microsoft.Extensions.Logging;
using Moq;
using TUnit.Core;
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

    [Test]
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
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Data).IsEqualTo(expectedResult);
        await Assert.That(result.Error).IsNull();
        await Assert.That(result.OperationName).IsEqualTo("TestOperation");
        await Assert.That(result.ProviderName).IsEqualTo("OpenAI");
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Data).IsNull();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.ErrorType).IsEqualTo(AiOperationErrorType.ConfigurationError);
        await Assert.That(result.Error!.Message).Contains("Test error");
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error!.ErrorType).IsEqualTo(AiOperationErrorType.NetworkError);
        await Assert.That(result.Error.IsRetryable).IsTrue();
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error!.ErrorType).IsEqualTo(AiOperationErrorType.Timeout);
        await Assert.That(result.Error.IsRetryable).IsTrue();
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error!.ErrorType).IsEqualTo(AiOperationErrorType.AuthenticationError);
        await Assert.That(result.Error.IsRetryable).IsFalse();
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error!.ErrorType).IsEqualTo(AiOperationErrorType.ValidationError);
        await Assert.That(result.Error.IsRetryable).IsFalse();
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Data).IsEqualTo("success");
        await Assert.That(callCount).IsEqualTo(3); // Initial attempt + 2 retries
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error!.ErrorType).IsEqualTo(AiOperationErrorType.NetworkError);
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Operation validation failed");
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error!.Message).Contains("MaxTokens must be greater than 0");
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Prompt length");
        await Assert.That(result.Error!.Message).Contains("exceeds maximum allowed");
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Rate limit exceeded");
    }

    [Test]
    public async Task GetStatistics_ReturnsCorrectStatistics()
    {
        // Act
        var stats = _middleware.GetStatistics();
        
        // Assert
        await Assert.That(stats).IsNotNull();
        await Assert.That(stats.TotalRetries).IsEqualTo(0);
        await Assert.That(stats.OperationsWithRetries).IsEqualTo(0);
        await Assert.That(stats.ActiveRateLimitKeys).IsEqualTo(0);
    }

    [Test]
    public async Task ClearStatistics_ClearsAllStatistics()
    {
        // Arrange - Execute some operations to generate statistics
        var operation = () => Task.FromResult("result");
        _middleware.ExecuteAsync(operation, "TestOperation", AiProviderType.OpenAI).Wait();
        
        // Act
        _middleware.ClearStatistics();
        var stats = _middleware.GetStatistics();
        
        // Assert
        await Assert.That(stats.TotalRetries).IsEqualTo(0);
        await Assert.That(stats.OperationsWithRetries).IsEqualTo(0);
        await Assert.That(stats.ActiveRateLimitKeys).IsEqualTo(0);
    }

    [Test]
    public async Task AiOperationConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new AiOperationConfiguration();
        
        // Assert
        await Assert.That(config.MaxRetries).IsEqualTo(3);
        await Assert.That(config.BaseRetryDelayMs).IsEqualTo(1000);
        await Assert.That(config.MaxRetryDelayMs).IsEqualTo(30000);
        await Assert.That(config.EnableRateLimiting).IsTrue();
        await Assert.That(config.MaxRequestsPerWindow).IsEqualTo(60);
        await Assert.That(config.RateLimitWindowMinutes).IsEqualTo(1);
        await Assert.That(config.MaxPromptLength).IsEqualTo(100000);
    }

    [Test]
    public async Task AiOperationResult_Success_CreatesCorrectResult()
    {
        // Arrange
        var data = "test data";
        var operationName = "TestOp";
        var providerName = "TestProvider";
        var duration = TimeSpan.FromMilliseconds(100);
        
        // Act
        var result = AiOperationResult<string>.Success(data, operationName, providerName, duration);
        
        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Data).IsEqualTo(data);
        await Assert.That(result.Error).IsNull();
        await Assert.That(result.OperationName).IsEqualTo(operationName);
        await Assert.That(result.ProviderName).IsEqualTo(providerName);
        await Assert.That(result.Duration).IsEqualTo(duration);
    }

    [Test]
    public async Task AiOperationResult_Failure_CreatesCorrectResult()
    {
        // Arrange
        var error = new AiOperationException("Test error");
        var operationName = "TestOp";
        var providerName = "TestProvider";
        var duration = TimeSpan.FromMilliseconds(100);
        
        // Act
        var result = AiOperationResult<string>.Failure(error, operationName, providerName, duration);
        
        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Data).IsNull();
        await Assert.That(result.Error).IsEqualTo(error);
        await Assert.That(result.OperationName).IsEqualTo(operationName);
        await Assert.That(result.ProviderName).IsEqualTo(providerName);
        await Assert.That(result.Duration).IsEqualTo(duration);
    }

    [Test]
    public async Task AiOperationException_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var message = "Test error";
        var errorType = AiOperationErrorType.NetworkError;
        var isRetryable = true;
        
        // Act
        var exception = new AiOperationException(message, errorType, isRetryable);
        
        // Assert
        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(exception.ErrorType).IsEqualTo(errorType);
        await Assert.That(exception.IsRetryable).IsEqualTo(isRetryable);
    }

    [Test]
    public async Task AiOperationException_WithInnerException_SetsPropertiesCorrectly()
    {
        // Arrange
        var message = "Test error";
        var innerException = new InvalidOperationException("Inner error");
        var errorType = AiOperationErrorType.ConfigurationError;
        var isRetryable = false;
        
        // Act
        var exception = new AiOperationException(message, innerException, errorType, isRetryable);
        
        // Assert
        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(exception.InnerException).IsEqualTo(innerException);
        await Assert.That(exception.ErrorType).IsEqualTo(errorType);
        await Assert.That(exception.IsRetryable).IsEqualTo(isRetryable);
    }

    [Test]
    public async Task OperationValidationResult_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var isValid = false;
        var errors = new List<string> { "Error 1", "Error 2" };
        
        // Act
        var result = new OperationValidationResult(isValid, errors);
        
        // Assert
        await Assert.That(result.IsValid).IsEqualTo(isValid);
        await Assert.That(result.Errors).IsEqualTo(errors);
    }

    [Test]
    [Arguments(AiOperationErrorType.UnknownError)]
    [Arguments(AiOperationErrorType.NetworkError)]
    [Arguments(AiOperationErrorType.Timeout)]
    [Arguments(AiOperationErrorType.AuthenticationError)]
    [Arguments(AiOperationErrorType.ValidationError)]
    [Arguments(AiOperationErrorType.ConfigurationError)]
    [Arguments(AiOperationErrorType.RateLimitExceeded)]
    [Arguments(AiOperationErrorType.ModelNotAvailable)]
    [Arguments(AiOperationErrorType.InsufficientQuota)]
    public async Task AiOperationErrorType_AllValuesAreDefined(AiOperationErrorType errorType)
    {
        // Act & Assert
        await Assert.That(Enum.IsDefined(typeof(AiOperationErrorType), errorType)).IsTrue();
    }

    [Test]
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
        await Assert.That(result1.IsSuccess).IsTrue();
        await Assert.That(result2.IsSuccess).IsTrue();
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsTrue();
    }
}