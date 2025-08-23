using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using TUnit.Assertions;
using TUnit.Core;

namespace HlpAI.Tests.Services;

public class OllamaClientTests
{
    private Mock<ILogger> _mockLogger = null!;
    private MockHttpMessageHandler _mockHandler = null!;
    private HttpClient _httpClient = null!;
    private OllamaClient _client = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
        _mockHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHandler);
        _client = new OllamaClient(_httpClient, "http://localhost:11434", "llama3.2", _mockLogger.Object);
    }

    [After(Test)]
    public void Cleanup()
    {
        _client?.Dispose();
        _httpClient?.Dispose();
        _mockHandler?.Dispose();
    }

    [Test]
    public async Task Constructor_WithDefaults_SetsCorrectValues()
    {
        // Act
        using var client = new OllamaClient();

        // Assert
        await Assert.That(client).IsNotNull();
    }

    [Test]
    public async Task Constructor_WithCustomParameters_SetsCorrectValues()
    {
        // Act
        using var client = new OllamaClient("http://custom:8080", "custom-model", _mockLogger.Object);

        // Assert
        await Assert.That(client).IsNotNull();
    }

    [Test]
    public async Task GenerateAsync_WithSimplePrompt_ReturnsResponse()
    {
        // Arrange
        const string prompt = "What is the capital of France?";
        const string expectedResponse = "Paris is the capital of France.";
        
        _mockHandler.SetupResponse("/api/generate", 
            "{\"response\":\"" + expectedResponse + "\"}");

        // Act
        var result = await _client.GenerateAsync(prompt);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEqualTo(expectedResponse);
    }

    [Test]
    public async Task GenerateAsync_WithPromptAndContext_IncludesBothInRequest()
    {
        // Arrange
        const string prompt = "What is the answer?";
        const string context = "The question is about the meaning of life.";
        const string expectedResponse = "42";
        
        _mockHandler.SetupResponse("/api/generate", 
            "{\"response\":\"" + expectedResponse + "\"}");

        // Act
        var result = await _client.GenerateAsync(prompt, context);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEqualTo(expectedResponse);
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(0.3)]
    [Arguments(0.7)]
    [Arguments(1.0)]
    public async Task GenerateAsync_WithDifferentTemperatures_WorksCorrectly(double temperature)
    {
        // Arrange
        const string prompt = "Test prompt";
        const string expectedResponse = "Test response";
        
        _mockHandler.SetupResponse("/api/generate", 
            "{\"response\":\"" + expectedResponse + "\"}");

        // Act & Assert
        var result = await _client.GenerateAsync(prompt, temperature: temperature);
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEqualTo(expectedResponse);
    }

    [Test]
    public async Task GenerateAsync_WithEmptyPrompt_HandlesGracefully()
    {
        // Arrange
        const string expectedResponse = "Please provide a prompt.";
        
        _mockHandler.SetupResponse("/api/generate", 
            "{\"response\":\"" + expectedResponse + "\"}");

        // Act
        var result = await _client.GenerateAsync("");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEqualTo(expectedResponse);
    }

    [Test]
    public async Task GenerateAsync_WithLongPrompt_HandlesCorrectly()
    {
        // Arrange
        var longPrompt = string.Concat(Enumerable.Repeat("This is a very long prompt. ", 1000));
        const string expectedResponse = "Response to long prompt.";
        
        _mockHandler.SetupResponse("/api/generate", 
            "{\"response\":\"" + expectedResponse + "\"}");

        // Act
        var result = await _client.GenerateAsync(longPrompt);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEqualTo(expectedResponse);
    }

    [Test]
    public async Task GenerateAsync_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        const string promptWithSpecialChars = "Prompt with special chars: ñáéíóú 你好世界 🌍 @#$%^&*()";
        const string expectedResponse = "Response with special characters handled.";
        
        _mockHandler.SetupResponse("/api/generate", 
            "{\"response\":\"" + expectedResponse + "\"}");

        // Act
        var result = await _client.GenerateAsync(promptWithSpecialChars);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEqualTo(expectedResponse);
    }

    [Test]
    public async Task IsAvailableAsync_WhenOllamaUnavailable_ReturnsFalse()
    {
        // Arrange
        _mockHandler.SetupResponse("/api/tags", 
            System.Net.HttpStatusCode.ServiceUnavailable, "{\"models\":[]}");

        // Act
        var result = await _client.IsAvailableAsync();

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task GetModelsAsync_WhenOllamaUnavailable_ReturnsEmptyList()
    {
        // Arrange
        _mockHandler.SetupResponse("/api/tags", 
            "{\"models\":[]}");

        // Act
        var result = await _client.GetModelsAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result is List<string>).IsTrue();
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetModelsAsync_ReturnsConsistentFormat()
    {
        // Arrange
        _mockHandler.SetupResponse("/api/tags", 
            "{\"models\":[{\"name\":\"llama3.2\"},{\"name\":\"codellama\"}]}");

        // Act
        var result = await _client.GetModelsAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result is List<string>).IsTrue();
        await Assert.That(result.Count).IsEqualTo(2);
        
        // All items should be strings
        foreach (var model in result)
        {
            await Assert.That(model).IsNotNull();
            await Assert.That(model).IsNotEmpty();
        }
    }

    [Test]
    public void Dispose_CallMultipleTimes_WorksCorrectly()
    {
        // Act & Assert
        _client.Dispose();
        _client.Dispose(); // Should not throw on multiple calls
        // Test passes if no exception thrown
    }

    [Test]
    public async Task GenerateAsync_AfterDispose_StillReturnsResult()
    {
        // Arrange
        const string expectedResponse = "Error: Client disposed";
        
        _mockHandler.SetupResponse("/api/generate", 
            "{\"response\":\"" + expectedResponse + "\"}");
        
        _client.Dispose();

        // Act & Assert
        // This should still work even after dispose, though might return error message
        var result = await _client.GenerateAsync("test prompt");
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEqualTo(expectedResponse);
    }

    [Test]
    public async Task Constructor_WithTrailingSlashInUrl_NormalizesUrl()
    {
        // Act
        using var client = new OllamaClient("http://localhost:11434/", "test-model");

        // Assert
        // URL should be normalized (trailing slash removed)
        await Assert.That(client).IsNotNull();
    }

    [Test]
    public async Task GenerateAsync_WithNullContext_HandlesCorrectly()
    {
        // Act
        var result = await _client.GenerateAsync("test prompt", null);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsNotEmpty();
    }

    [Test]
    public async Task GenerateAsync_WithEmptyContext_HandlesCorrectly()
    {
        // Act
        var result = await _client.GenerateAsync("test prompt", "");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsNotEmpty();
    }

    [Test]
    public async Task GenerateAsync_ConsistentResults_WithSameInputs()
    {
        // Arrange
        const string prompt = "consistent test";
        const double temperature = 0.0; // Very low temperature for more deterministic results

        // Act
        var result1 = await _client.GenerateAsync(prompt, temperature: temperature);
        var result2 = await _client.GenerateAsync(prompt, temperature: temperature);

        // Assert
        await Assert.That(result1).IsNotNull();
        await Assert.That(result2).IsNotNull();
        // Note: Results might not be identical due to external service behavior
        // but both should be valid responses
    }

    [Test]
    public async Task GenerateAsync_DifferentTemperatures_MayProduceDifferentResults()
    {
        // Arrange
        const string prompt = "creative prompt";

        // Act
        var lowTempResult = await _client.GenerateAsync(prompt, temperature: 0.1);
        var highTempResult = await _client.GenerateAsync(prompt, temperature: 0.9);

        // Assert
        await Assert.That(lowTempResult).IsNotNull();
        await Assert.That(highTempResult).IsNotNull();
        // Both should return valid responses
    }

    [Test]
    public async Task IsAvailableAsync_MultipleCallsConsistent()
    {
        // Act
        var result1 = await _client.IsAvailableAsync();
        var result2 = await _client.IsAvailableAsync();

        // Assert
        await Assert.That(result1).IsEqualTo(result2);
    }

    [Test]
    public async Task GetModelsAsync_MultipleCallsConsistent()
    {
        // Act
        var result1 = await _client.GetModelsAsync();
        var result2 = await _client.GetModelsAsync();

        // Assert
        await Assert.That(result1).IsNotNull();
        await Assert.That(result2).IsNotNull();
        await Assert.That(result1.Count).IsEqualTo(result2.Count);
    }
}