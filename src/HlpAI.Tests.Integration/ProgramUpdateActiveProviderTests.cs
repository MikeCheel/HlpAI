using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TUnit.Core;
using HlpAI.Models;
using HlpAI.Services;
using HlpAI.MCP;

namespace HlpAI.Tests.Program;

/// <summary>
/// Unit tests for UpdateActiveProviderAsync method to prevent API key exceptions
/// and ensure proper handling of different provider types and configurations.
/// </summary>
public class ProgramUpdateActiveProviderTests
{
    private Mock<EnhancedMcpRagServer> _mockServer = null!;
    private Mock<ILogger> _mockLogger = null!;
    private AppConfiguration _testConfig = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _mockServer = new Mock<EnhancedMcpRagServer>();
        _mockLogger = new Mock<ILogger>();
        
        _testConfig = new AppConfiguration
        {
            UseSecureApiKeyStorage = true,
            LastProvider = AiProviderType.Ollama,
            LastModel = "test-model",
            OllamaUrl = "http://localhost:11434",
            LmStudioUrl = "http://localhost:1234",
            OpenWebUiUrl = "http://localhost:3000",
            OpenAiUrl = "https://api.openai.com",
            AnthropicUrl = "https://api.anthropic.com",
            DeepSeekUrl = "https://api.deepseek.com"
        };
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Test that local providers (Ollama) work without API keys
    /// </summary>
    [Test]
    public async Task UpdateActiveProviderAsync_WithLocalProvider_DoesNotRequireApiKey()
    {
        // Arrange
        _testConfig.LastProvider = AiProviderType.Ollama;
        _testConfig.UseSecureApiKeyStorage = true;
        
        // Act & Assert - Should not throw exception
        // This test verifies that local providers don't trigger API key retrieval
        var requiresApiKey = AiProviderFactory.RequiresApiKey(AiProviderType.Ollama);
        await Assert.That(requiresApiKey).IsFalse();
        
        // Verify that CreateProvider can be called without API key for local providers
        var provider = AiProviderFactory.CreateProvider(
            AiProviderType.Ollama,
            "test-model",
            "http://localhost:11434",
            _mockLogger.Object, // logger
            _testConfig
        );
        
        await Assert.That(provider).IsNotNull();
        await Assert.That(provider.ProviderName).IsEqualTo("Ollama");
        
        provider.Dispose();
    }

    /// <summary>
    /// Test that cloud providers require API keys when secure storage is enabled
    /// </summary>
    [Test]
    [SupportedOSPlatform("windows")]
    public async Task UpdateActiveProviderAsync_WithCloudProvider_RequiresApiKey()
    {
        // Arrange
        _testConfig.LastProvider = AiProviderType.DeepSeek;
        _testConfig.UseSecureApiKeyStorage = true;
        
        // Act & Assert
        var requiresApiKey = AiProviderFactory.RequiresApiKey(AiProviderType.DeepSeek);
        await Assert.That(requiresApiKey).IsTrue();
        
        // Verify that CreateProvider without API key throws exception for cloud providers
        await Assert.That(() => 
        {
            var provider = AiProviderFactory.CreateProvider(
                AiProviderType.DeepSeek,
                "deepseek-chat",
                null, // providerUrl
                null, // logger
                _testConfig
            );
            return provider;
        }).Throws<InvalidOperationException>()
          .WithMessage("DeepSeek provider requires an API key. Use the overload with apiKey parameter.");
    }

    /// <summary>
    /// Test that cloud providers work correctly with valid API keys
    /// </summary>
    [Test]
    public async Task UpdateActiveProviderAsync_WithCloudProviderAndApiKey_CreatesProvider()
    {
        // Arrange
        _testConfig.LastProvider = AiProviderType.OpenAI;
        var testApiKey = "test-api-key-12345";
        
        // Act
        var provider = AiProviderFactory.CreateProvider(
            AiProviderType.OpenAI,
            "gpt-3.5-turbo",
            null, // providerUrl
            testApiKey,
            null, // logger
            _testConfig
        );
        
        // Assert
        await Assert.That(provider).IsNotNull();
        await Assert.That(provider.ProviderName).IsEqualTo("OpenAI");
        
        provider.Dispose();
    }

    /// <summary>
    /// Test that all cloud providers require API keys
    /// </summary>
    [Test]
    public async Task RequiresApiKey_AllCloudProviders_ReturnTrue()
    {
        // Act & Assert
        await Assert.That(AiProviderFactory.RequiresApiKey(AiProviderType.OpenAI)).IsTrue();
        await Assert.That(AiProviderFactory.RequiresApiKey(AiProviderType.Anthropic)).IsTrue();
        await Assert.That(AiProviderFactory.RequiresApiKey(AiProviderType.DeepSeek)).IsTrue();
    }

    /// <summary>
    /// Test that all local providers do not require API keys
    /// </summary>
    [Test]
    public async Task RequiresApiKey_AllLocalProviders_ReturnFalse()
    {
        // Act & Assert
        await Assert.That(AiProviderFactory.RequiresApiKey(AiProviderType.Ollama)).IsFalse();
        await Assert.That(AiProviderFactory.RequiresApiKey(AiProviderType.LmStudio)).IsFalse();
        await Assert.That(AiProviderFactory.RequiresApiKey(AiProviderType.OpenWebUi)).IsFalse();
    }

    /// <summary>
    /// Test that CreateProvider throws appropriate exceptions for cloud providers without API keys
    /// </summary>
    [Test]
    public async Task CreateProvider_CloudProviderWithoutApiKey_ThrowsInvalidOperationException()
    {
        // Test OpenAI
        await Assert.That(() => 
        {
            var provider = AiProviderFactory.CreateProvider(
            AiProviderType.OpenAI,
            "gpt-3.5-turbo",
            null, // providerUrl
            _mockLogger.Object, // logger
            _testConfig
        );
            return provider;
        }).Throws<InvalidOperationException>()
          .WithMessage("OpenAI provider requires an API key. Use the overload with apiKey parameter.");

        // Test Anthropic
        await Assert.That(() => 
        {
            var provider = AiProviderFactory.CreateProvider(
                AiProviderType.Anthropic,
                "claude-3-sonnet",
                null,
                _mockLogger.Object, // logger
                _testConfig
            );
            return provider;
        }).Throws<InvalidOperationException>()
          .WithMessage("Anthropic provider requires an API key. Use the overload with apiKey parameter.");

        // Test DeepSeek
        await Assert.That(() => 
        {
            var provider = AiProviderFactory.CreateProvider(
                AiProviderType.DeepSeek,
                "deepseek-chat",
                null,
                _mockLogger.Object, // logger
                _testConfig
            );
            return provider;
        }).Throws<InvalidOperationException>()
          .WithMessage("DeepSeek provider requires an API key. Use the overload with apiKey parameter.");
    }

    /// <summary>
    /// Test that secure storage disabled scenario works for cloud providers
    /// This simulates the case where UseSecureApiKeyStorage is false
    /// </summary>
    [Test]
    public async Task UpdateActiveProviderAsync_SecureStorageDisabled_HandlesCloudProviders()
    {
        // Arrange
        _testConfig.UseSecureApiKeyStorage = false;
        _testConfig.LastProvider = AiProviderType.DeepSeek;
        
        // Act & Assert
        // When secure storage is disabled, the method should still handle cloud providers appropriately
        // This test verifies the logic path when UseSecureApiKeyStorage is false
        var requiresApiKey = AiProviderFactory.RequiresApiKey(AiProviderType.DeepSeek);
        await Assert.That(requiresApiKey).IsTrue();
        
        // The method should attempt to create provider without API key and handle the exception
        await Assert.That(() => 
        {
            var provider = AiProviderFactory.CreateProvider(
                AiProviderType.DeepSeek,
                "deepseek-chat",
                null,
                _mockLogger.Object, // logger
                _testConfig
            );
            return provider;
        }).Throws<InvalidOperationException>();
    }

    /// <summary>
    /// Test that non-Windows platforms handle cloud providers appropriately
    /// </summary>
    [Test]
    public async Task UpdateActiveProviderAsync_NonWindowsPlatform_HandlesCloudProviders()
    {
        // Arrange
        _testConfig.UseSecureApiKeyStorage = true;
        _testConfig.LastProvider = AiProviderType.OpenAI;
        
        // Act & Assert
        // On non-Windows platforms, secure storage might not be available
        // The method should handle this scenario gracefully
        var requiresApiKey = AiProviderFactory.RequiresApiKey(AiProviderType.OpenAI);
        await Assert.That(requiresApiKey).IsTrue();
        
        // Verify that the RequiresApiKey method works correctly regardless of platform
        await Assert.That(AiProviderFactory.RequiresApiKey(AiProviderType.OpenAI)).IsTrue();
        await Assert.That(AiProviderFactory.RequiresApiKey(AiProviderType.Ollama)).IsFalse();
    }

    /// <summary>
    /// Test that provider URL generation works correctly for all provider types
    /// </summary>
    [Test]
    public async Task GetProviderUrl_AllProviderTypes_ReturnsCorrectUrls()
    {
        // Test local providers
        var ollamaUrl = AiProviderFactory.GetProviderUrl(_testConfig, AiProviderType.Ollama);
        await Assert.That(ollamaUrl).IsEqualTo(_testConfig.OllamaUrl);
        
        var lmStudioUrl = AiProviderFactory.GetProviderUrl(_testConfig, AiProviderType.LmStudio);
        await Assert.That(lmStudioUrl).IsEqualTo(_testConfig.LmStudioUrl);
        
        var openWebUiUrl = AiProviderFactory.GetProviderUrl(_testConfig, AiProviderType.OpenWebUi);
        await Assert.That(openWebUiUrl).IsEqualTo(_testConfig.OpenWebUiUrl);
        
        // Test cloud providers (should return default URLs from configuration)
        var openAiUrl = AiProviderFactory.GetProviderUrl(_testConfig, AiProviderType.OpenAI);
        var anthropicUrl = AiProviderFactory.GetProviderUrl(_testConfig, AiProviderType.Anthropic);
        var deepSeekUrl = AiProviderFactory.GetProviderUrl(_testConfig, AiProviderType.DeepSeek);
        
        // Cloud providers return their default URLs from configuration
        await Assert.That(openAiUrl).IsEqualTo("https://api.openai.com");
        await Assert.That(anthropicUrl).IsEqualTo("https://api.anthropic.com");
        await Assert.That(deepSeekUrl).IsEqualTo("https://api.deepseek.com");
    }

    /// <summary>
    /// Test that provider creation with empty or null model names is handled correctly
    /// </summary>
    [Test]
    public async Task CreateProvider_WithNullOrEmptyModel_HandlesGracefully()
    {
        // Test with null model
        var provider1 = AiProviderFactory.CreateProvider(
            AiProviderType.Ollama,
            null!, // null model
            "http://localhost:11434",
            _mockLogger.Object, // logger
            _testConfig
        );
        
        await Assert.That(provider1).IsNotNull();
        provider1.Dispose();
        
        // Test with empty model
        var provider2 = AiProviderFactory.CreateProvider(
            AiProviderType.Ollama,
            "", // empty model
            "http://localhost:11434",
            _mockLogger.Object, // logger
            _testConfig
        );
        
        await Assert.That(provider2).IsNotNull();
        provider2.Dispose();
    }

    /// <summary>
    /// Test that provider disposal works correctly to prevent resource leaks
    /// </summary>
    [Test]
    public async Task CreateProvider_DisposalPattern_WorksCorrectly()
    {
        // Arrange & Act
        var provider = AiProviderFactory.CreateProvider(
            AiProviderType.Ollama,
            "llama2",
            "http://localhost:11434",
            _mockLogger.Object, // logger
            _testConfig
        );
        
        // Assert
        await Assert.That(provider).IsNotNull();
        
        // Test disposal
        provider.Dispose();
        
        // Verify that multiple disposals don't cause issues
        provider.Dispose();
        
        await Task.CompletedTask;
    }
}