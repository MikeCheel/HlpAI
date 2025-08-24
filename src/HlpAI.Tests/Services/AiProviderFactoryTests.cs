using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HlpAI.Services;
using HlpAI.Models;
using TUnit.Assertions;
using TUnit.Core;
using System.Threading.Tasks;

namespace HlpAI.Tests.Services;

public class AiProviderFactoryTests
{
    private readonly ILogger<AiProviderFactoryTests> _logger = new NullLogger<AiProviderFactoryTests>();

    [Test]
    public async Task GetProviderDescriptions_ReturnsAllProviders()
    {
        // Act
        var descriptions = AiProviderFactory.GetProviderDescriptions();

        // Assert
        await Assert.That(descriptions).IsNotNull();
        await Assert.That(descriptions.Count).IsEqualTo(3);
        await Assert.That(descriptions.ContainsKey(AiProviderType.Ollama)).IsTrue();
        await Assert.That(descriptions.ContainsKey(AiProviderType.LmStudio)).IsTrue();
        await Assert.That(descriptions.ContainsKey(AiProviderType.OpenWebUi)).IsTrue();
        
        await Assert.That(descriptions[AiProviderType.Ollama]).IsEqualTo("Ollama - Local model runner (recommended)");
        await Assert.That(descriptions[AiProviderType.LmStudio]).IsEqualTo("LM Studio - Local API server with GUI");
        await Assert.That(descriptions[AiProviderType.OpenWebUi]).IsEqualTo("Open Web UI - Web-based model management");
    }

    [Test]
    public async Task GetProviderInfo_ReturnsCorrectInfoForOllama()
    {
        // Act
        var info = AiProviderFactory.GetProviderInfo(AiProviderType.Ollama);

        // Assert
        await Assert.That(info).IsNotNull();
        await Assert.That(info.Name).IsEqualTo("Ollama");
        await Assert.That(info.DefaultUrl).IsEqualTo("http://localhost:11434");
        await Assert.That(info.Description).IsEqualTo("Local model runner");
    }

    [Test]
    public async Task GetProviderInfo_ReturnsCorrectInfoForLmStudio()
    {
        // Act
        var info = AiProviderFactory.GetProviderInfo(AiProviderType.LmStudio);

        // Assert
        await Assert.That(info).IsNotNull();
        await Assert.That(info.Name).IsEqualTo("LM Studio");
        await Assert.That(info.DefaultUrl).IsEqualTo("http://localhost:1234");
        await Assert.That(info.Description).IsEqualTo("Local API server with GUI");
    }

    [Test]
    public async Task GetProviderInfo_ReturnsCorrectInfoForOpenWebUi()
    {
        // Act
        var info = AiProviderFactory.GetProviderInfo(AiProviderType.OpenWebUi);

        // Assert
        await Assert.That(info).IsNotNull();
        await Assert.That(info.Name).IsEqualTo("Open Web UI");
        await Assert.That(info.DefaultUrl).IsEqualTo("http://localhost:3000");
        await Assert.That(info.Description).IsEqualTo("Web-based model management");
    }

    [Test]
    public async Task CreateProvider_WithOllamaType_ReturnsOllamaClient()
    {
        // Act
        var provider = AiProviderFactory.CreateProvider(AiProviderType.Ollama, "test-model", "http://localhost:11434");

        // Assert
        await Assert.That(provider).IsNotNull();
        await Assert.That(provider.GetType()).IsEqualTo(typeof(OllamaClient));
        await Assert.That(provider.ProviderName).IsEqualTo("Ollama");
        await Assert.That(provider.BaseUrl).IsEqualTo("http://localhost:11434");
        await Assert.That(provider.CurrentModel).IsEqualTo("test-model");
    }

    [Test]
    public async Task CreateProvider_WithLmStudioType_ReturnsLmStudioProvider()
    {
        // Act
        var provider = AiProviderFactory.CreateProvider(AiProviderType.LmStudio, "test-model", "http://localhost:1234");

        // Assert
        await Assert.That(provider).IsNotNull();
        await Assert.That(provider.GetType()).IsEqualTo(typeof(LmStudioProvider));
        await Assert.That(provider.ProviderName).IsEqualTo("LM Studio");
        await Assert.That(provider.BaseUrl).IsEqualTo("http://localhost:1234");
        await Assert.That(provider.CurrentModel).IsEqualTo("test-model");
    }

    [Test]
    public async Task CreateProvider_WithOpenWebUiType_ReturnsOpenWebUiProvider()
    {
        // Act
        var provider = AiProviderFactory.CreateProvider(AiProviderType.OpenWebUi, "test-model", "http://localhost:3000");

        // Assert
        await Assert.That(provider).IsNotNull();
        await Assert.That(provider.GetType()).IsEqualTo(typeof(OpenWebUiProvider));
        await Assert.That(provider.ProviderName).IsEqualTo("Open Web UI");
        await Assert.That(provider.BaseUrl).IsEqualTo("http://localhost:3000");
        await Assert.That(provider.CurrentModel).IsEqualTo("test-model");
    }

    [Test]
    public async Task CreateProvider_WithNullUrl_UsesDefaultUrl()
    {
        // Act
        var provider = AiProviderFactory.CreateProvider(AiProviderType.Ollama, "test-model", null);

        // Assert
        await Assert.That(provider).IsNotNull();
        await Assert.That(provider.BaseUrl).IsEqualTo("http://localhost:11434");
    }

    [Test]
    public async Task CreateProvider_WithEmptyUrl_UsesDefaultUrl()
    {
        // Act
        var provider = AiProviderFactory.CreateProvider(AiProviderType.Ollama, "test-model", "");

        // Assert
        await Assert.That(provider).IsNotNull();
        await Assert.That(provider.BaseUrl).IsEqualTo("http://localhost:11434");
    }

    [Test]
    public async Task CreateProvider_WithWhitespaceUrl_UsesDefaultUrl()
    {
        // Act
        var provider = AiProviderFactory.CreateProvider(AiProviderType.Ollama, "test-model", "   ");

        // Assert
        await Assert.That(provider).IsNotNull();
        await Assert.That(provider.BaseUrl).IsEqualTo("http://localhost:11434");
    }

    [Test]
    public async Task CreateProvider_WithCustomUrl_UsesCustomUrl()
    {
        // Act
        var provider = AiProviderFactory.CreateProvider(AiProviderType.Ollama, "test-model", "http://custom:8080");

        // Assert
        await Assert.That(provider).IsNotNull();
        await Assert.That(provider.BaseUrl).IsEqualTo("http://custom:8080");
    }

    [Test]
    public async Task DetectAvailableProvidersAsync_ReturnsProviderAvailability()
    {
        // Act
        var availableProviders = await AiProviderFactory.DetectAvailableProvidersAsync();

        // Assert
        await Assert.That(availableProviders).IsNotNull();
        await Assert.That(availableProviders.Count).IsEqualTo(3);
        await Assert.That(availableProviders.ContainsKey(AiProviderType.Ollama)).IsTrue();
        await Assert.That(availableProviders.ContainsKey(AiProviderType.LmStudio)).IsTrue();
        await Assert.That(availableProviders.ContainsKey(AiProviderType.OpenWebUi)).IsTrue();
        
        // We can't predict availability, but we can check that all providers are present
        foreach (var (providerType, isAvailable) in availableProviders)
        {
            await Assert.That(providerType == AiProviderType.Ollama || 
                        providerType == AiProviderType.LmStudio || 
                        providerType == AiProviderType.OpenWebUi).IsTrue();
        }
    }

    [Test]
    public async Task CreateProvider_WithLogger_UsesProvidedLogger()
    {
        // Arrange
        var logger = new NullLogger<OllamaClient>();

        // Act
        var provider = AiProviderFactory.CreateProvider(
            AiProviderType.Ollama, 
            "test-model", 
            "http://localhost:11434",
            logger
        );

        // Assert
        await Assert.That(provider).IsNotNull();
        await Assert.That(provider.GetType()).IsEqualTo(typeof(OllamaClient));
    }

    [Test]
    public async Task CreateProvider_WithNullLogger_UsesNullLogger()
    {
        // Act
        var provider = AiProviderFactory.CreateProvider(
            AiProviderType.Ollama, 
            "test-model", 
            "http://localhost:11434",
            null
        );

        // Assert
        await Assert.That(provider).IsNotNull();
        await Assert.That(provider.GetType()).IsEqualTo(typeof(OllamaClient));
    }

    [Test]
    public async Task GetDefaultModelForProvider_ReturnsCorrectModels()
    {
        // Arrange
        var config = new AppConfiguration
        {
            OllamaDefaultModel = "ollama-default",
            LmStudioDefaultModel = "lmstudio-default",
            OpenWebUiDefaultModel = "openwebui-default"
        };

        // Act & Assert
        await Assert.That(AiProviderFactory.GetDefaultModelForProvider(AiProviderType.Ollama, config)).IsEqualTo("ollama-default");
        await Assert.That(AiProviderFactory.GetDefaultModelForProvider(AiProviderType.LmStudio, config)).IsEqualTo("lmstudio-default");
        await Assert.That(AiProviderFactory.GetDefaultModelForProvider(AiProviderType.OpenWebUi, config)).IsEqualTo("openwebui-default");
        await Assert.That(AiProviderFactory.GetDefaultModelForProvider((AiProviderType)999, config)).IsEqualTo("default"); // Unknown provider
    }

    [Test]
    public async Task GetProviderUrl_ReturnsCorrectUrls()
    {
        // Arrange
        var config = new AppConfiguration
        {
            OllamaUrl = "http://ollama-custom:11434",
            LmStudioUrl = "http://lmstudio-custom:1234",
            OpenWebUiUrl = "http://openwebui-custom:3000"
        };

        // Act & Assert
        await Assert.That(AiProviderFactory.GetProviderUrl(config, AiProviderType.Ollama)).IsEqualTo("http://ollama-custom:11434");
        await Assert.That(AiProviderFactory.GetProviderUrl(config, AiProviderType.LmStudio)).IsEqualTo("http://lmstudio-custom:1234");
        await Assert.That(AiProviderFactory.GetProviderUrl(config, AiProviderType.OpenWebUi)).IsEqualTo("http://openwebui-custom:3000");
        await Assert.That(AiProviderFactory.GetProviderUrl(config, (AiProviderType)999)).IsNull(); // Unknown provider
    }
}
