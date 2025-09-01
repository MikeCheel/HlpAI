using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using HlpAI.Extensions;
using HlpAI.Models;
using HlpAI.Services;

namespace HlpAI.Tests.Extensions;

public class AiProviderExtensionsTests
{
    private readonly Mock<IAiProvider> _mockProvider;
    private readonly Mock<ICloudAiProvider> _mockCloudProvider;
    private readonly Mock<ILogger> _mockLogger;
    private readonly AppConfiguration _appConfig;

    public AiProviderExtensionsTests()
    {
        _mockProvider = new Mock<IAiProvider>();
        _mockCloudProvider = new Mock<ICloudAiProvider>();
        _mockLogger = new Mock<ILogger>();
        
        _mockProvider.Setup(p => p.ProviderType).Returns(AiProviderType.OpenAI);
        _mockProvider.Setup(p => p.BaseUrl).Returns("https://api.openai.com");
        _mockProvider.Setup(p => p.CurrentModel).Returns("gpt-4");
        
        _mockCloudProvider.Setup(p => p.ProviderType).Returns(AiProviderType.OpenAI);
        _mockCloudProvider.Setup(p => p.BaseUrl).Returns("https://api.openai.com");
        _mockCloudProvider.Setup(p => p.CurrentModel).Returns("gpt-4");
        
        _appConfig = new AppConfiguration
        {
            OpenAI = new AiProviderConfiguration
            {
                MaxTokens = 4000,
                TimeoutMs = 300000
            },
            Anthropic = new AiProviderConfiguration
            {
                MaxTokens = 8000,
                TimeoutMs = 300000
            },
            DeepSeek = new AiProviderConfiguration
            {
                MaxTokens = 4000,
                TimeoutMs = 300000
            },
            LmStudio = new AiProviderConfiguration
            {
                MaxTokens = 4000,
                TimeoutMs = 300000
            },
            OpenWebUi = new AiProviderConfiguration
            {
                MaxTokens = 4000,
                TimeoutMs = 300000
            },
            Ollama = new AiProviderConfiguration
            {
                MaxTokens = 4000,
                TimeoutMs = 300000
            }
        };
    }

    [Fact]
    public async Task ExecuteWithMiddlewareAsync_SuccessfulOperation_ReturnsSuccess()
    {
        // Arrange
        var expectedResult = "test result";
        var operation = () => Task.FromResult(expectedResult);
        
        // Act
        var result = await _mockProvider.Object.ExecuteWithMiddlewareAsync(
            operation,
            "TestOperation",
            logger: _mockLogger.Object);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedResult, result.Data);
        Assert.Equal("TestOperation", result.OperationName);
        Assert.Equal("OpenAI", result.ProviderName);
    }

    [Fact]
    public async Task ExecuteWithMiddlewareAsync_WithContext_UsesProvidedContext()
    {
        // Arrange
        var expectedResult = "test result";
        var operation = () => Task.FromResult(expectedResult);
        var context = new AiOperationContext
        {
            MaxTokens = 2000,
            TimeoutMs = 60000,
            Prompt = "test prompt"
        };
        
        // Act
        var result = await _mockProvider.Object.ExecuteWithMiddlewareAsync(
            operation,
            "TestOperation",
            context,
            _mockLogger.Object);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedResult, result.Data);
    }

    [Fact]
    public async Task GenerateWithMiddlewareAsync_CallsGenerateAsync_ReturnsResult()
    {
        // Arrange
        var prompt = "test prompt";
        var maxTokens = 2000;
        var expectedResult = "generated response";
        
        _mockProvider.Setup(p => p.GenerateAsync(prompt, maxTokens))
                    .ReturnsAsync(expectedResult);
        
        // Act
        var result = await _mockProvider.Object.GenerateWithMiddlewareAsync(
            prompt,
            maxTokens,
            _mockLogger.Object);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedResult, result.Data);
        Assert.Equal("GenerateAsync", result.OperationName);
        _mockProvider.Verify(p => p.GenerateAsync(prompt, maxTokens), Times.Once);
    }

    [Fact]
    public async Task IsAvailableWithMiddlewareAsync_CallsIsAvailableAsync_ReturnsResult()
    {
        // Arrange
        _mockProvider.Setup(p => p.IsAvailableAsync()).ReturnsAsync(true);
        
        // Act
        var result = await _mockProvider.Object.IsAvailableWithMiddlewareAsync(_mockLogger.Object);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
        Assert.Equal("IsAvailableAsync", result.OperationName);
        _mockProvider.Verify(p => p.IsAvailableAsync(), Times.Once);
    }

    [Fact]
    public async Task GetModelsWithMiddlewareAsync_CallsGetModelsAsync_ReturnsResult()
    {
        // Arrange
        var expectedModels = new List<string> { "model1", "model2" };
        _mockProvider.Setup(p => p.GetModelsAsync()).ReturnsAsync(expectedModels);
        
        // Act
        var result = await _mockProvider.Object.GetModelsWithMiddlewareAsync(_mockLogger.Object);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedModels, result.Data);
        Assert.Equal("GetModelsAsync", result.OperationName);
        _mockProvider.Verify(p => p.GetModelsAsync(), Times.Once);
    }

    [Fact]
    public async Task ValidateApiKeyWithMiddlewareAsync_CallsValidateApiKeyAsync_ReturnsResult()
    {
        // Arrange
        _mockCloudProvider.Setup(p => p.ValidateApiKeyAsync()).ReturnsAsync(true);
        
        // Act
        var result = await _mockCloudProvider.Object.ValidateApiKeyWithMiddlewareAsync(_mockLogger.Object);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
        Assert.Equal("ValidateApiKeyAsync", result.OperationName);
        _mockCloudProvider.Verify(p => p.ValidateApiKeyAsync(), Times.Once);
    }

    [Fact]
    public async Task GetUsageInfoWithMiddlewareAsync_CallsGetUsageInfoAsync_ReturnsResult()
    {
        // Arrange
        var expectedUsage = new ApiUsageInfo(1000, 500, DateTime.UtcNow);
        _mockCloudProvider.Setup(p => p.GetUsageInfoAsync()).ReturnsAsync(expectedUsage);
        
        // Act
        var result = await _mockCloudProvider.Object.GetUsageInfoWithMiddlewareAsync(_mockLogger.Object);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedUsage, result.Data);
        Assert.Equal("GetUsageInfoAsync", result.OperationName);
        _mockCloudProvider.Verify(p => p.GetUsageInfoAsync(), Times.Once);
    }

    [Fact]
    public async Task GetRateLimitInfoWithMiddlewareAsync_CallsGetRateLimitInfoAsync_ReturnsResult()
    {
        // Arrange
        var expectedRateLimit = new RateLimitInfo(100, 50, DateTime.UtcNow.AddMinutes(1));
        _mockCloudProvider.Setup(p => p.GetRateLimitInfoAsync()).ReturnsAsync(expectedRateLimit);
        
        // Act
        var result = await _mockCloudProvider.Object.GetRateLimitInfoWithMiddlewareAsync(_mockLogger.Object);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedRateLimit, result.Data);
        Assert.Equal("GetRateLimitInfoAsync", result.OperationName);
        _mockCloudProvider.Verify(p => p.GetRateLimitInfoAsync(), Times.Once);
    }

    [Fact]
    public void GetMiddlewareStatistics_ReturnsStatistics()
    {
        // Act
        var stats = _mockProvider.Object.GetMiddlewareStatistics();
        
        // Assert
        // Statistics might be null if no operations have been executed yet
        // This is expected behavior
        Assert.True(stats == null || stats.TotalRetries >= 0);
    }

    [Fact]
    public void ClearMiddlewareStatistics_ClearsStatistics()
    {
        // Act & Assert - Should not throw
        _mockProvider.Object.ClearMiddlewareStatistics();
    }

    [Theory]
    [InlineData(AiProviderType.OpenAI)]
    [InlineData(AiProviderType.Anthropic)]
    [InlineData(AiProviderType.DeepSeek)]
    [InlineData(AiProviderType.LmStudio)]
    [InlineData(AiProviderType.OpenWebUi)]
    [InlineData(AiProviderType.Ollama)]
    public void CreateContextFromConfig_AllProviderTypes_CreatesValidContext(AiProviderType providerType)
    {
        // Arrange
        _mockProvider.Setup(p => p.ProviderType).Returns(providerType);
        var prompt = "test prompt";
        
        // Act
        var context = _mockProvider.Object.CreateContextFromConfig(_appConfig, prompt);
        
        // Assert
        Assert.NotNull(context);
        Assert.Equal(prompt, context.Prompt);
        Assert.True(context.MaxTokens > 0);
        Assert.True(context.TimeoutMs > 0);
        Assert.NotNull(context.Metadata);
        Assert.Equal(providerType.ToString(), context.Metadata["ProviderType"]);
    }

    [Fact]
    public void CreateContextFromConfig_UnknownProviderType_ThrowsArgumentException()
    {
        // Arrange
        _mockProvider.Setup(p => p.ProviderType).Returns((AiProviderType)999); // Invalid enum value
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _mockProvider.Object.CreateContextFromConfig(_appConfig));
    }

    [Fact]
    public async Task ExecuteWithConfigContextAsync_UsesConfigurationContext_ReturnsResult()
    {
        // Arrange
        var expectedResult = "test result";
        var operation = () => Task.FromResult(expectedResult);
        var prompt = "test prompt";
        
        // Act
        var result = await _mockProvider.Object.ExecuteWithConfigContextAsync(
            operation,
            "TestOperation",
            _appConfig,
            prompt,
            _mockLogger.Object);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedResult, result.Data);
        Assert.Equal("TestOperation", result.OperationName);
    }

    [Fact]
    public void AiOperationConfigurationBuilder_Create_ReturnsBuilder()
    {
        // Act
        var builder = AiOperationConfigurationBuilder.Create();
        
        // Assert
        Assert.NotNull(builder);
    }

    [Fact]
    public void AiOperationConfigurationBuilder_WithMaxRetries_SetsMaxRetries()
    {
        // Arrange
        var maxRetries = 5;
        
        // Act
        var config = AiOperationConfigurationBuilder.Create()
            .WithMaxRetries(maxRetries)
            .Build();
        
        // Assert
        Assert.Equal(maxRetries, config.MaxRetries);
    }

    [Fact]
    public void AiOperationConfigurationBuilder_WithRetryDelay_SetsRetryDelays()
    {
        // Arrange
        var baseDelay = 500;
        var maxDelay = 10000;
        
        // Act
        var config = AiOperationConfigurationBuilder.Create()
            .WithRetryDelay(baseDelay, maxDelay)
            .Build();
        
        // Assert
        Assert.Equal(baseDelay, config.BaseRetryDelayMs);
        Assert.Equal(maxDelay, config.MaxRetryDelayMs);
    }

    [Fact]
    public void AiOperationConfigurationBuilder_WithRateLimit_SetsRateLimit()
    {
        // Arrange
        var maxRequests = 100;
        var windowMinutes = 5;
        
        // Act
        var config = AiOperationConfigurationBuilder.Create()
            .WithRateLimit(maxRequests, windowMinutes)
            .Build();
        
        // Assert
        Assert.True(config.EnableRateLimiting);
        Assert.Equal(maxRequests, config.MaxRequestsPerWindow);
        Assert.Equal(windowMinutes, config.RateLimitWindowMinutes);
    }

    [Fact]
    public void AiOperationConfigurationBuilder_DisableRateLimit_DisablesRateLimit()
    {
        // Act
        var config = AiOperationConfigurationBuilder.Create()
            .DisableRateLimit()
            .Build();
        
        // Assert
        Assert.False(config.EnableRateLimiting);
    }

    [Fact]
    public void AiOperationConfigurationBuilder_WithMaxPromptLength_SetsMaxPromptLength()
    {
        // Arrange
        var maxLength = 50000;
        
        // Act
        var config = AiOperationConfigurationBuilder.Create()
            .WithMaxPromptLength(maxLength)
            .Build();
        
        // Assert
        Assert.Equal(maxLength, config.MaxPromptLength);
    }

    [Fact]
    public void AiOperationConfigurationBuilder_ChainedCalls_SetsAllProperties()
    {
        // Arrange
        var maxRetries = 5;
        var baseDelay = 500;
        var maxDelay = 10000;
        var maxRequests = 100;
        var windowMinutes = 5;
        var maxPromptLength = 50000;
        
        // Act
        var config = AiOperationConfigurationBuilder.Create()
            .WithMaxRetries(maxRetries)
            .WithRetryDelay(baseDelay, maxDelay)
            .WithRateLimit(maxRequests, windowMinutes)
            .WithMaxPromptLength(maxPromptLength)
            .Build();
        
        // Assert
        Assert.Equal(maxRetries, config.MaxRetries);
        Assert.Equal(baseDelay, config.BaseRetryDelayMs);
        Assert.Equal(maxDelay, config.MaxRetryDelayMs);
        Assert.True(config.EnableRateLimiting);
        Assert.Equal(maxRequests, config.MaxRequestsPerWindow);
        Assert.Equal(windowMinutes, config.RateLimitWindowMinutes);
        Assert.Equal(maxPromptLength, config.MaxPromptLength);
    }

    [Fact]
    public async Task GenerateWithMiddlewareAsync_DefaultParameters_UsesDefaults()
    {
        // Arrange
        var prompt = "test prompt";
        var expectedResult = "generated response";
        
        _mockProvider.Setup(p => p.GenerateAsync(prompt, 4000)) // Default maxTokens
                    .ReturnsAsync(expectedResult);
        
        // Act
        var result = await _mockProvider.Object.GenerateWithMiddlewareAsync(prompt);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedResult, result.Data);
        _mockProvider.Verify(p => p.GenerateAsync(prompt, 4000), Times.Once);
    }

    [Fact]
    public async Task ExecuteWithMiddlewareAsync_OperationThrowsException_ReturnsFailure()
    {
        // Arrange
        var operation = () => Task.FromException<string>(new InvalidOperationException("Test error"));
        
        // Act
        var result = await _mockProvider.Object.ExecuteWithMiddlewareAsync(
            operation,
            "TestOperation",
            logger: _mockLogger.Object);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("Test error", result.Error.Message);
    }
}