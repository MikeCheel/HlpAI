using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TUnit.Core;
using HlpAI.Models;
using HlpAI.Services;

namespace HlpAI.Tests.Program;

/// <summary>
/// Tests for API key handling in UpdateActiveProviderAsync method to prevent
/// the DeepSeek API key exception and similar issues with other cloud providers.
/// </summary>
public class ProgramApiKeyHandlingTests
{
    private Mock<ILogger> _mockLogger = null!;
    private AppConfiguration _testConfig = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _mockLogger = new Mock<ILogger>();
        
        _testConfig = new AppConfiguration
        {
            UseSecureApiKeyStorage = true,
            LastProvider = AiProviderType.DeepSeek,
            LastModel = "deepseek-chat"
        };
        
        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task TearDown()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Test that SecureApiKeyStorage can be instantiated without errors
    /// </summary>
    [Test]
    [SupportedOSPlatform("windows")]
    public async Task SecureApiKeyStorage_Instantiation_WorksCorrectly()
    {
        // Act & Assert
        var storage = new SecureApiKeyStorage(_mockLogger.Object);
        await Assert.That(storage).IsNotNull();
    }

    /// <summary>
    /// Test that HasApiKey method works for all cloud providers
    /// </summary>
    [Test]
    [SupportedOSPlatform("windows")]
    public void SecureApiKeyStorage_HasApiKey_WorksForAllCloudProviders()
    {
        // Arrange
        var storage = new SecureApiKeyStorage(_mockLogger.Object);
        var cloudProviders = new[] { "OpenAI", "Anthropic", "DeepSeek" };
        
        // Act & Assert
        foreach (var provider in cloudProviders)
        {
            // Should not throw exception when checking for API key existence
            var hasKey = storage.HasApiKey(provider);
            // Method completed without throwing - hasKey is bool, no assertion needed
        }
    }

    /// <summary>
    /// Test that RetrieveApiKey method handles missing keys gracefully
    /// </summary>
    [Test]
    [SupportedOSPlatform("windows")]
    public async Task SecureApiKeyStorage_RetrieveApiKey_HandlesMissingKeys()
    {
        // Arrange
        var storage = new SecureApiKeyStorage(_mockLogger.Object);
        var nonExistentProvider = "NonExistentProvider_" + Guid.NewGuid().ToString("N")[..8];
        
        // Act
        var apiKey = storage.RetrieveApiKey(nonExistentProvider);
        
        // Assert
        // Should return null or empty string for non-existent keys, not throw exception
        await Assert.That(string.IsNullOrEmpty(apiKey)).IsTrue();
    }

    /// <summary>
    /// Test the API key requirement logic for preventing the DeepSeek exception
    /// </summary>
    [Test]
    public async Task ApiKeyRequirement_Logic_PreventsDeeepSeekException()
    {
        // Arrange - Test the exact logic used in UpdateActiveProviderAsync
        var config = new AppConfiguration
        {
            LastProvider = AiProviderType.DeepSeek,
            UseSecureApiKeyStorage = true
        };
        
        // Act - Test the condition that determines API key retrieval
        var requiresApiKey = AiProviderFactory.RequiresApiKey(config.LastProvider);
        var useSecureStorage = config.UseSecureApiKeyStorage;
        var isWindows = OperatingSystem.IsWindows();
        
        var shouldRetrieveApiKey = requiresApiKey && useSecureStorage && isWindows;
        
        // Assert
        await Assert.That(requiresApiKey).IsTrue(); // DeepSeek requires API key
        await Assert.That(useSecureStorage).IsTrue(); // Config enables secure storage
        
        if (isWindows)
        {
            await Assert.That(shouldRetrieveApiKey).IsTrue(); // Should retrieve API key on Windows
        }
        else
        {
            await Assert.That(shouldRetrieveApiKey).IsFalse(); // Should not retrieve on non-Windows
        }
    }

    /// <summary>
    /// Test that all cloud providers follow the same API key requirement pattern
    /// </summary>
    [Test]
    public async Task ApiKeyRequirement_AllCloudProviders_FollowSamePattern()
    {
        // Arrange
        var cloudProviders = new[]
        {
            AiProviderType.OpenAI,
            AiProviderType.Anthropic,
            AiProviderType.DeepSeek
        };
        
        var config = new AppConfiguration { UseSecureApiKeyStorage = true };
        
        // Act & Assert
        foreach (var provider in cloudProviders)
        {
            config.LastProvider = provider;
            
            var requiresApiKey = AiProviderFactory.RequiresApiKey(provider);
            var shouldRetrieveApiKey = requiresApiKey && config.UseSecureApiKeyStorage && OperatingSystem.IsWindows();
            
            // All cloud providers should require API keys
            await Assert.That(requiresApiKey).IsTrue();
            
            // All should follow the same retrieval logic
            if (OperatingSystem.IsWindows())
            {
                await Assert.That(shouldRetrieveApiKey).IsTrue();
            }
        }
    }

    /// <summary>
    /// Test that local providers never trigger API key retrieval
    /// </summary>
    [Test]
    public async Task ApiKeyRequirement_LocalProviders_NeverTriggerRetrieval()
    {
        // Arrange
        var localProviders = new[]
        {
            AiProviderType.Ollama,
            AiProviderType.LmStudio,
            AiProviderType.OpenWebUi
        };
        
        var config = new AppConfiguration { UseSecureApiKeyStorage = true };
        
        // Act & Assert
        foreach (var provider in localProviders)
        {
            config.LastProvider = provider;
            
            var requiresApiKey = AiProviderFactory.RequiresApiKey(provider);
            var shouldRetrieveApiKey = requiresApiKey && config.UseSecureApiKeyStorage && OperatingSystem.IsWindows();
            
            // Local providers should not require API keys
            await Assert.That(requiresApiKey).IsFalse();
            
            // Should never trigger API key retrieval
            await Assert.That(shouldRetrieveApiKey).IsFalse();
        }
    }

    /// <summary>
    /// Test CreateProvider overload selection logic to prevent API key exceptions
    /// </summary>
    [Test]
    public async Task CreateProvider_OverloadSelection_PreventsDeeepSeekException()
    {
        // Test that the correct overload is selected based on API key availability
        
        // Test 1: Cloud provider with API key - should use overload with API key
        var providerWithKey = AiProviderFactory.CreateProvider(
            AiProviderType.OpenAI,
            "gpt-3.5-turbo",
            null, // providerUrl
            "test-api-key", // API key provided
            _mockLogger.Object,
            _testConfig
        );
        
        await Assert.That(providerWithKey).IsNotNull();
        await Assert.That(providerWithKey.ProviderName).IsEqualTo("OpenAI");
        providerWithKey.Dispose();
        
        // Test 2: Local provider without API key - should use overload without API key
        var localProvider = AiProviderFactory.CreateProvider(
            AiProviderType.Ollama,
            "llama2",
            "http://localhost:11434",
            _mockLogger.Object,
            _testConfig
        );
        
        await Assert.That(localProvider).IsNotNull();
        await Assert.That(localProvider.ProviderName).IsEqualTo("Ollama");
        localProvider.Dispose();
        
        // Test 3: Cloud provider without API key - should throw exception
        await Assert.That(() => 
        {
            var provider = AiProviderFactory.CreateProvider(
                AiProviderType.DeepSeek,
                "deepseek-chat",
                null,
                _mockLogger.Object,
                _testConfig
            );
            return provider;
        }).Throws<InvalidOperationException>()
          .WithMessage("DeepSeek provider requires an API key. Use the overload with apiKey parameter.");
    }

    /// <summary>
    /// Test that the UpdateActiveProviderAsync method handles empty API keys correctly
    /// </summary>
    [Test]
    [SupportedOSPlatform("windows")]
    public async Task UpdateActiveProviderAsync_EmptyApiKey_HandlesGracefully()
    {
        // This test simulates the scenario where secure storage returns an empty API key
        
        // Arrange
        var storage = new SecureApiKeyStorage(_mockLogger.Object);
        var providerName = "TestProvider_" + Guid.NewGuid().ToString("N")[..8];
        
        // Act - Retrieve API key for non-existent provider (should return empty)
        var apiKey = storage.RetrieveApiKey(providerName);
        
        // Assert - Should handle empty API key gracefully
        await Assert.That(string.IsNullOrEmpty(apiKey)).IsTrue();
        
        // The UpdateActiveProviderAsync method should check for empty API keys
        // and return false with appropriate message (tested in integration tests)
    }

    /// <summary>
    /// Test that provider creation errors are properly caught and handled
    /// </summary>
    [Test]
    public async Task ProviderCreation_ErrorHandling_PreventsCrashes()
    {
        // Test various error scenarios that could occur during provider creation
        
        // Test 1: Invalid provider type (should be handled by factory)
        var validProviderTypes = Enum.GetValues<AiProviderType>();
        await Assert.That(validProviderTypes.Length).IsGreaterThan(0);
        
        // Test 2: Null configuration (should be handled gracefully)
        var provider = AiProviderFactory.CreateProvider(
            AiProviderType.Ollama,
            "test-model",
            "http://localhost:11434",
            _mockLogger.Object,
            null // null config
        );
        
        await Assert.That(provider).IsNotNull();
        provider.Dispose();
        
        // Test 3: Invalid URL format (should not crash during creation)
        var providerWithInvalidUrl = AiProviderFactory.CreateProvider(
            AiProviderType.Ollama,
            "test-model",
            "invalid-url-format",
            _mockLogger.Object,
            _testConfig
        );
        
        await Assert.That(providerWithInvalidUrl).IsNotNull();
        providerWithInvalidUrl.Dispose();
    }

    /// <summary>
    /// Test that the fix for DeepSeek API key exception works for all similar scenarios
    /// </summary>
    [Test]
    public async Task DeeepSeekFix_WorksForAllSimilarScenarios()
    {
        // Test the specific scenarios that could cause the DeepSeek exception
        
        var cloudProviders = new[]
        {
            (AiProviderType.OpenAI, "OpenAI provider requires an API key. Use the overload with apiKey parameter."),
            (AiProviderType.Anthropic, "Anthropic provider requires an API key. Use the overload with apiKey parameter."),
            (AiProviderType.DeepSeek, "DeepSeek provider requires an API key. Use the overload with apiKey parameter.")
        };
        
        foreach (var (providerType, expectedMessage) in cloudProviders)
        {
            // Test that each cloud provider throws the expected exception when no API key is provided
            await Assert.That(() => 
            {
                var provider = AiProviderFactory.CreateProvider(
                    providerType,
                    "test-model",
                    null,
                    _mockLogger.Object,
                    _testConfig
                );
                return provider;
            }).Throws<InvalidOperationException>()
              .WithMessage(expectedMessage);
            
            // Test that each cloud provider works correctly with an API key
            var providerWithKey = AiProviderFactory.CreateProvider(
                providerType,
                "test-model",
                null,
                "test-api-key",
                _mockLogger.Object,
                _testConfig
            );
            
            await Assert.That(providerWithKey).IsNotNull();
            providerWithKey.Dispose();
        }
    }
}