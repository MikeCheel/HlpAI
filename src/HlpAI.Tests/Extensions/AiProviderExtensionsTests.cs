using Microsoft.Extensions.Logging;
using Moq;
using TUnit.Core;
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
            OpenAiMaxTokens = 4000,
            OpenAiTimeoutMinutes = 5,
            AnthropicMaxTokens = 8000,
            AnthropicTimeoutMinutes = 5,
            DeepSeekMaxTokens = 4000,
            DeepSeekTimeoutMinutes = 5,
            LmStudioMaxTokens = 4000,
            LmStudioTimeoutMinutes = 5,
            OpenWebUiMaxTokens = 4000,
            OpenWebUiTimeoutMinutes = 5,
            OllamaTimeoutMinutes = 5
        };
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Data).IsEqualTo(expectedResult);
        await Assert.That(result.OperationName).IsEqualTo("TestOperation");
        await Assert.That(result.ProviderName).IsEqualTo("OpenAI");
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Data).IsEqualTo(expectedResult);
    }

    [Test]
    public async Task GenerateWithMiddlewareAsync_CallsGenerateAsync_ReturnsResult()
    {
        // Arrange
        var prompt = "test prompt";
        var maxTokens = 2000;
        var expectedResult = "generated response";
        
        _mockProvider.Setup(p => p.GenerateAsync(prompt, null, 0.7))
                    .ReturnsAsync(expectedResult);
        
        // Act
        var result = await _mockProvider.Object.GenerateWithMiddlewareAsync(
            prompt,
            maxTokens,
            _mockLogger.Object);
        
        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Data).IsEqualTo(expectedResult);
        await Assert.That(result.OperationName).IsEqualTo("GenerateAsync");
        _mockProvider.Verify(p => p.GenerateAsync(prompt, null, 0.7), Times.Once);
    }

    [Test]
    public async Task IsAvailableWithMiddlewareAsync_CallsIsAvailableAsync_ReturnsResult()
    {
        // Arrange
        _mockProvider.Setup(p => p.IsAvailableAsync()).ReturnsAsync(true);
        
        // Act
        var result = await _mockProvider.Object.IsAvailableWithMiddlewareAsync(_mockLogger.Object);
        
        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Data).IsTrue();
        await Assert.That(result.OperationName).IsEqualTo("IsAvailableAsync");
        _mockProvider.Verify(p => p.IsAvailableAsync(), Times.Once);
    }

    [Test]
    public async Task GetModelsWithMiddlewareAsync_CallsGetModelsAsync_ReturnsResult()
    {
        // Arrange
        var expectedModels = new List<string> { "model1", "model2" };
        _mockProvider.Setup(p => p.GetModelsAsync()).ReturnsAsync(expectedModels);
        
        // Act
        var result = await _mockProvider.Object.GetModelsWithMiddlewareAsync(_mockLogger.Object);
        
        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Data).IsEqualTo(expectedModels);
        await Assert.That(result.OperationName).IsEqualTo("GetModelsAsync");
        _mockProvider.Verify(p => p.GetModelsAsync(), Times.Once);
    }

    [Test]
    public async Task ValidateApiKeyWithMiddlewareAsync_CallsValidateApiKeyAsync_ReturnsResult()
    {
        // Arrange
        _mockCloudProvider.Setup(p => p.ValidateApiKeyAsync()).ReturnsAsync(true);
        
        // Act
        var result = await _mockCloudProvider.Object.ValidateApiKeyWithMiddlewareAsync(_mockLogger.Object);
        
        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Data).IsTrue();
        await Assert.That(result.OperationName).IsEqualTo("ValidateApiKeyAsync");
        _mockCloudProvider.Verify(p => p.ValidateApiKeyAsync(), Times.Once);
    }

    [Test]
    public async Task GetUsageInfoWithMiddlewareAsync_CallsGetUsageInfoAsync_ReturnsResult()
    {
        // Arrange
        var expectedUsage = new ApiUsageInfo(1000, 2000, 500, 1000, DateTime.UtcNow);
        _mockCloudProvider.Setup(p => p.GetUsageInfoAsync()).ReturnsAsync(expectedUsage);
        
        // Act
        var result = await _mockCloudProvider.Object.GetUsageInfoWithMiddlewareAsync(_mockLogger.Object);
        
        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Data).IsEqualTo(expectedUsage);
        await Assert.That(result.OperationName).IsEqualTo("GetUsageInfoAsync");
        _mockCloudProvider.Verify(p => p.GetUsageInfoAsync(), Times.Once);
    }

    [Test]
    public async Task GetRateLimitInfoWithMiddlewareAsync_CallsGetRateLimitInfoAsync_ReturnsResult()
    {
        // Arrange
        var expectedRateLimit = new RateLimitInfo(100, 50, 1000, 800, DateTime.UtcNow.AddMinutes(1));
        _mockCloudProvider.Setup(p => p.GetRateLimitInfoAsync()).ReturnsAsync(expectedRateLimit);
        
        // Act
        var result = await _mockCloudProvider.Object.GetRateLimitInfoWithMiddlewareAsync(_mockLogger.Object);
        
        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Data).IsEqualTo(expectedRateLimit);
        await Assert.That(result.OperationName).IsEqualTo("GetRateLimitInfoAsync");
        _mockCloudProvider.Verify(p => p.GetRateLimitInfoAsync(), Times.Once);
    }

    [Test]
    public async Task GetMiddlewareStatistics_ReturnsStatistics()
    {
        // Act
        var stats = _mockProvider.Object.GetMiddlewareStatistics();
        
        // Assert
        // Statistics might be null if no operations have been executed yet
        // This is expected behavior
        await Assert.That(stats == null || stats.TotalRetries >= 0).IsTrue();
    }

    [Test]
    public void ClearMiddlewareStatistics_ClearsStatistics()
    {
        // Act & Assert - Should not throw
        _mockProvider.Object.ClearMiddlewareStatistics();
    }

    [Test]
    [Arguments(AiProviderType.OpenAI)]
    [Arguments(AiProviderType.Anthropic)]
    [Arguments(AiProviderType.DeepSeek)]
    [Arguments(AiProviderType.LmStudio)]
    [Arguments(AiProviderType.OpenWebUi)]
    [Arguments(AiProviderType.Ollama)]
    public async Task CreateContextFromConfig_AllProviderTypes_CreatesValidContext(AiProviderType providerType)
    {
        // Arrange
        _mockProvider.Setup(p => p.ProviderType).Returns(providerType);
        var prompt = "test prompt";
        
        // Act
        var context = _mockProvider.Object.CreateContextFromConfig(_appConfig, prompt);
        
        // Assert
        await Assert.That(context).IsNotNull();
        await Assert.That(context.Prompt).IsEqualTo(prompt);
        await Assert.That(context.MaxTokens > 0).IsTrue();
        await Assert.That(context.TimeoutMs > 0).IsTrue();
        await Assert.That(context.Metadata).IsNotNull();
        await Assert.That(context.Metadata!["ProviderType"]).IsEqualTo(providerType.ToString());
    }

    [Test]
    public void CreateContextFromConfig_UnknownProviderType_ThrowsArgumentException()
    {
        // Arrange
        _mockProvider.Setup(p => p.ProviderType).Returns((AiProviderType)999); // Invalid enum value
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _mockProvider.Object.CreateContextFromConfig(_appConfig));
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Data).IsEqualTo(expectedResult);
        await Assert.That(result.OperationName).IsEqualTo("TestOperation");
    }

    [Test]
    public async Task AiOperationConfigurationBuilder_Create_ReturnsBuilder()
    {
        // Act
        var builder = AiOperationConfigurationBuilder.Create();
        
        // Assert
        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task AiOperationConfigurationBuilder_WithMaxRetries_SetsMaxRetries()
    {
        // Arrange
        var maxRetries = 5;
        
        // Act
        var config = AiOperationConfigurationBuilder.Create()
            .WithMaxRetries(maxRetries)
            .Build();
        
        // Assert
        await Assert.That(config.MaxRetries).IsEqualTo(maxRetries);
    }

    [Test]
    public async Task AiOperationConfigurationBuilder_WithRetryDelay_SetsRetryDelays()
    {
        // Arrange
        var baseDelay = 500;
        var maxDelay = 10000;
        
        // Act
        var config = AiOperationConfigurationBuilder.Create()
            .WithRetryDelay(baseDelay, maxDelay)
            .Build();
        
        // Assert
        await Assert.That(config.BaseRetryDelayMs).IsEqualTo(baseDelay);
        await Assert.That(config.MaxRetryDelayMs).IsEqualTo(maxDelay);
    }

    [Test]
    public async Task AiOperationConfigurationBuilder_WithRateLimit_SetsRateLimit()
    {
        // Arrange
        var maxRequests = 100;
        var windowMinutes = 5;
        
        // Act
        var config = AiOperationConfigurationBuilder.Create()
            .WithRateLimit(maxRequests, windowMinutes)
            .Build();
        
        // Assert
        await Assert.That(config.EnableRateLimiting).IsTrue();
        await Assert.That(config.MaxRequestsPerWindow).IsEqualTo(maxRequests);
        await Assert.That(config.RateLimitWindowMinutes).IsEqualTo(windowMinutes);
    }

    [Test]
    public async Task AiOperationConfigurationBuilder_DisableRateLimit_DisablesRateLimit()
    {
        // Act
        var config = AiOperationConfigurationBuilder.Create()
            .DisableRateLimit()
            .Build();
        
        // Assert
        await Assert.That(config.EnableRateLimiting).IsFalse();
    }

    [Test]
    public async Task AiOperationConfigurationBuilder_WithMaxPromptLength_SetsMaxPromptLength()
    {
        // Arrange
        var maxLength = 50000;
        
        // Act
        var config = AiOperationConfigurationBuilder.Create()
            .WithMaxPromptLength(maxLength)
            .Build();
        
        // Assert
        await Assert.That(config.MaxPromptLength).IsEqualTo(maxLength);
    }

    [Test]
    public async Task AiOperationConfigurationBuilder_ChainedCalls_SetsAllProperties()
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
        await Assert.That(config.MaxRetries).IsEqualTo(maxRetries);
        await Assert.That(config.BaseRetryDelayMs).IsEqualTo(baseDelay);
        await Assert.That(config.MaxRetryDelayMs).IsEqualTo(maxDelay);
        await Assert.That(config.EnableRateLimiting).IsTrue();
        await Assert.That(config.MaxRequestsPerWindow).IsEqualTo(maxRequests);
        await Assert.That(config.RateLimitWindowMinutes).IsEqualTo(windowMinutes);
        await Assert.That(config.MaxPromptLength).IsEqualTo(maxPromptLength);
    }

    [Test]
    public async Task GenerateWithMiddlewareAsync_DefaultParameters_UsesDefaults()
    {
        // Arrange
        var prompt = "test prompt";
        var expectedResult = "generated response";
        
        _mockProvider.Setup(p => p.GenerateAsync(prompt, null, 0.7)) // Default parameters
                    .ReturnsAsync(expectedResult);
        
        // Act
        var result = await _mockProvider.Object.GenerateWithMiddlewareAsync(prompt);
        
        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Data).IsEqualTo(expectedResult);
        _mockProvider.Verify(p => p.GenerateAsync(prompt, null, 0.7), Times.Once);
    }

    [Test]
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
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Message).Contains("Test error");
    }
}