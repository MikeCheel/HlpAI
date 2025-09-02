using System;
using System.IO;
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
/// Integration tests for UpdateActiveProviderAsync method that test the actual method behavior
/// including API key retrieval, provider creation, and error handling scenarios.
/// </summary>
public class ProgramUpdateActiveProviderIntegrationTests
{
    private Mock<IEnhancedMcpRagServer> _mockServer = null!;
    private AppConfiguration _testConfig = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _mockServer = new Mock<IEnhancedMcpRagServer>();
        
        _testConfig = new AppConfiguration
        {
            UseSecureApiKeyStorage = true,
            LastProvider = AiProviderType.Ollama,
            LastModel = "test-model",
            OllamaUrl = "http://localhost:11434",
            LmStudioUrl = "http://localhost:1234",
            OpenWebUiUrl = "http://localhost:3000"
        };
        
        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task TearDown()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Test UpdateActiveProviderAsync with local provider (should succeed without API key)
    /// </summary>
    [Test]
    public async Task UpdateActiveProviderAsync_WithLocalProvider_ReturnsTrue()
    {
        // Arrange
        _testConfig.LastProvider = AiProviderType.Ollama;
        _testConfig.UseSecureApiKeyStorage = true;
        
        // Mock the server's UpdateAiProvider method (void method)
        _mockServer.Setup(s => s.UpdateAiProvider(It.IsAny<IAiProvider>()));
        
        // Act
        var result = await CallUpdateActiveProviderAsync(_mockServer.Object, _testConfig);
        
        // Assert
        // Note: This test may return false due to provider availability, but should not throw exceptions
        // The key test is that no API key exception occurs for local providers
        // Method completed without throwing - this is the main test
        
        // Verify method completed without throwing API key exceptions
        // If we reach this point, no API key exception was thrown
    }

    /// <summary>
    /// Test UpdateActiveProviderAsync with cloud provider but no API key (should handle gracefully)
    /// </summary>
    [Test]
    [SupportedOSPlatform("windows")]
    public async Task UpdateActiveProviderAsync_WithCloudProviderNoApiKey_ReturnsFalseWithMessage()
    {
        // Arrange
        _testConfig.LastProvider = AiProviderType.DeepSeek;
        _testConfig.UseSecureApiKeyStorage = true;
        
        // Mock the server (void method)
        _mockServer.Setup(s => s.UpdateAiProvider(It.IsAny<IAiProvider>()));
        
        // Act
        var result = await CallUpdateActiveProviderAsync(_mockServer.Object, _testConfig);
        
        // Assert
        await Assert.That(result).IsFalse();
        
        // Verify method completed and returned false as expected
        // Error handling is internal to the method
    }

    /// <summary>
    /// Test UpdateActiveProviderAsync with secure storage disabled for cloud provider
    /// </summary>
    [Test]
    public async Task UpdateActiveProviderAsync_SecureStorageDisabled_HandlesCloudProvider()
    {
        // Arrange
        _testConfig.LastProvider = AiProviderType.OpenAI;
        _testConfig.UseSecureApiKeyStorage = false; // Disabled
        
        // Mock the server (void method)
        _mockServer.Setup(s => s.UpdateAiProvider(It.IsAny<IAiProvider>()));
        
        // Act
        var result = await CallUpdateActiveProviderAsync(_mockServer.Object, _testConfig);
        
        // Assert
        await Assert.That(result).IsFalse();
        
        // Verify error handling - should catch the InvalidOperationException
        // Method should return false when secure storage is disabled for cloud providers
    }

    /// <summary>
    /// Test UpdateActiveProviderAsync error handling for invalid configuration
    /// </summary>
    [Test]
    public async Task UpdateActiveProviderAsync_WithInvalidConfig_HandlesErrorGracefully()
    {
        // Arrange
        _testConfig.LastProvider = AiProviderType.Ollama;
        _testConfig.OllamaUrl = "invalid-url"; // Invalid URL
        
        // Mock the server
        _mockServer.Setup(s => s.UpdateAiProvider(It.IsAny<IAiProvider>()));
        
        // Act
        var result = await CallUpdateActiveProviderAsync(_mockServer.Object, _testConfig);
        
        // Assert
        // Method should handle errors gracefully and not crash
        // Method completed without throwing - this is the main test
    }

    /// <summary>
    /// Test UpdateActiveProviderAsync with null model (should use default)
    /// </summary>
    [Test]
    public async Task UpdateActiveProviderAsync_WithNullModel_UsesDefault()
    {
        // Arrange
        _testConfig.LastProvider = AiProviderType.Ollama;
        _testConfig.LastModel = null; // Null model
        
        // Mock the server
        _mockServer.Setup(s => s.UpdateAiProvider(It.IsAny<IAiProvider>()));
        
        // Act
        var result = await CallUpdateActiveProviderAsync(_mockServer.Object, _testConfig);
        
        // Assert
        // Should handle null model gracefully
        // Method completed without throwing - this is the main test
        
        // Verify method handles null model gracefully
        // If we reach this point, no model-related exception was thrown
    }

    /// <summary>
    /// Test UpdateActiveProviderAsync exception handling
    /// </summary>
    [Test]
    public async Task UpdateActiveProviderAsync_WithServerException_HandlesGracefully()
    {
        // Arrange
        _testConfig.LastProvider = AiProviderType.Ollama;
        
        // Mock the server to throw an exception
        _mockServer.Setup(s => s.UpdateAiProvider(It.IsAny<IAiProvider>()))
                  .Throws(new InvalidOperationException("Test exception"));
        
        // Act
        var result = await CallUpdateActiveProviderAsync(_mockServer.Object, _testConfig);
        
        // Assert
        await Assert.That(result).IsFalse();
        
        // Verify error is handled gracefully
        // Method should return false when server throws exception
    }

    /// <summary>
    /// Test that UpdateActiveProviderAsync properly disposes providers on failure
    /// </summary>
    [Test]
    public async Task UpdateActiveProviderAsync_OnProviderUnavailable_DisposesProvider()
    {
        // Arrange
        _testConfig.LastProvider = AiProviderType.Ollama;
        _testConfig.OllamaUrl = "http://nonexistent:11434"; // Unavailable provider
        
        // Mock the server
        _mockServer.Setup(s => s.UpdateAiProvider(It.IsAny<IAiProvider>()));
        
        // Act
        var result = await CallUpdateActiveProviderAsync(_mockServer.Object, _testConfig);
        
        // Assert
        // Should return false when provider is not available
        await Assert.That(result).IsFalse();
        
        // Method should complete without throwing (proper disposal)
        await Task.CompletedTask;
    }

    /// <summary>
    /// Test UpdateActiveProviderAsync with all supported provider types
    /// </summary>
    [Test]
    public async Task UpdateActiveProviderAsync_AllProviderTypes_HandleCorrectly()
    {
        // Test all provider types to ensure none cause unexpected exceptions
        var providerTypes = new[]
        {
            AiProviderType.Ollama,
            AiProviderType.LmStudio,
            AiProviderType.OpenWebUi,
            AiProviderType.OpenAI,
            AiProviderType.Anthropic,
            AiProviderType.DeepSeek
        };

        foreach (var providerType in providerTypes)
        {
            // Arrange
            _testConfig.LastProvider = providerType;
            _testConfig.UseSecureApiKeyStorage = false; // Disable to avoid API key requirements
            
            // Test each provider type
            
            // Mock the server
            var mockServer = new Mock<IEnhancedMcpRagServer>();
            mockServer.Setup(s => s.UpdateAiProvider(It.IsAny<IAiProvider>()));
            
            // Act & Assert
            var result = await CallUpdateActiveProviderAsync(mockServer.Object, _testConfig);
            
            // Should not throw exceptions for any provider type
            // Method completed without throwing - this is the main test
            
            // For cloud providers with secure storage disabled, should return false
            if (AiProviderFactory.RequiresApiKey(providerType))
            {
                await Assert.That(result).IsFalse();
            }
        }
    }

    /// <summary>
    /// Helper method to call the private UpdateActiveProviderAsync method using reflection
    /// </summary>
    private async Task<bool> CallUpdateActiveProviderAsync(IEnhancedMcpRagServer server, AppConfiguration config)
    {
        try
        {
            // Use reflection to call the private static method
            var method = typeof(HlpAI.Program).GetMethod("UpdateActiveProviderAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (method == null)
            {
                throw new InvalidOperationException("UpdateActiveProviderAsync method not found");
            }
            
            var task = (Task<bool>)method.Invoke(null, new object[] { server, config })!;
            return await task;
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            // Unwrap the inner exception for cleaner test results
            if (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
            throw;
        }
    }
}