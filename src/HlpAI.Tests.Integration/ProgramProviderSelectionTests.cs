using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HlpAI.Models;
using HlpAI.Services;

namespace HlpAI.Tests;

/// <summary>
/// Tests for provider selection functionality in Program.cs
/// </summary>
public class ProgramProviderSelectionTests
{
    private readonly ILogger<ProgramProviderSelectionTests> _logger = new NullLogger<ProgramProviderSelectionTests>();
    private readonly AppConfiguration _config;

    public ProgramProviderSelectionTests()
    {
        _config = new AppConfiguration
        {
            LastProvider = AiProviderType.Ollama,
            OllamaUrl = "http://localhost:11434",
            LastModel = "llama3.2:3b"
        };
    }

    [Test]
    public async Task RequiresApiKey_ShouldReturnTrueForCloudProviders()
    {
        // Test that cloud providers are correctly identified as requiring API keys
        await Assert.That(AiProviderFactory.RequiresApiKey(AiProviderType.OpenAI)).IsTrue();
        await Assert.That(AiProviderFactory.RequiresApiKey(AiProviderType.Anthropic)).IsTrue();
        await Assert.That(AiProviderFactory.RequiresApiKey(AiProviderType.DeepSeek)).IsTrue();
    }

    [Test]
    public async Task RequiresApiKey_ShouldReturnFalseForLocalProviders()
    {
        // Test that local providers are correctly identified as not requiring API keys
        await Assert.That(AiProviderFactory.RequiresApiKey(AiProviderType.Ollama)).IsFalse();
        await Assert.That(AiProviderFactory.RequiresApiKey(AiProviderType.LmStudio)).IsFalse();
        await Assert.That(AiProviderFactory.RequiresApiKey(AiProviderType.OpenWebUi)).IsFalse();
    }

    [Test]
    public async Task ProviderEnumeration_ShouldNotThrowForCloudProvidersWithoutApiKeys()
    {
        // Test that enumerating providers doesn't throw exceptions for cloud providers without API keys
        var providerDescriptions = AiProviderFactory.GetProviderDescriptions();
        var providers = providerDescriptions.Keys.ToList();

        foreach (var provider in providers)
        {
            if (AiProviderFactory.RequiresApiKey(provider))
            {
                // For cloud providers without API keys, should not throw
                var apiKeyStorage = new SecureApiKeyStorage();
                var hasApiKey = apiKeyStorage.HasApiKey(provider.ToString());
                
                // This should not throw an exception
                await Assert.That(hasApiKey || true).IsTrue(); // Always passes, just testing no exception
            }
            else
            {
                // For local providers, should be able to create (though may not be available)
                Exception? exception = null;
                try
                {
                    var tempProvider = AiProviderFactory.CreateProvider(
                        provider,
                        "default",
                        GetProviderUrl(provider) ?? string.Empty,
                        _logger,
                        _config
                    );
                    await tempProvider.IsAvailableAsync();
                    tempProvider.Dispose();
                }
                catch (HttpRequestException)
                {
                    // Connection failures are expected for unavailable services
                }
                catch (TaskCanceledException)
                {
                    // Timeout exceptions are expected for unavailable services
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                
                // Should not throw InvalidOperationException about API keys
                await Assert.That(exception == null || !(exception is InvalidOperationException)).IsTrue();
            }
        }
    }

    private string? GetProviderUrl(AiProviderType provider)
    {
        return provider switch
        {
            AiProviderType.None => null,
            AiProviderType.Ollama => _config.OllamaUrl,
            AiProviderType.LmStudio => _config.LmStudioUrl,
            AiProviderType.OpenWebUi => _config.OpenWebUiUrl,
            AiProviderType.OpenAI => "https://api.openai.com/v1",
            AiProviderType.Anthropic => "https://api.anthropic.com/v1",
            AiProviderType.DeepSeek => "https://api.deepseek.com/v1",
            _ => null
        };
    }
}