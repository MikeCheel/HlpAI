using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using HlpAI.Services;

namespace HlpAI.Tests.Services;

/// <summary>
/// Tests specifically for DeepSeek provider availability checking to prevent regression
/// of the "DeepSeek not available" issue when API keys are missing or empty.
/// </summary>
public class DeepSeekProviderAvailabilityTests
{
    private Mock<HttpMessageHandler> _mockHttpMessageHandler = null!;
    private HttpClient _httpClient = null!;
    private Mock<ILogger> _mockLogger = null!;
    
    private const string TestBaseUrl = "https://api.deepseek.com";
    private const string TestModel = "deepseek-chat";

    [Before(Test)]
    public void Setup()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(TestBaseUrl)
        };
        _mockLogger = new Mock<ILogger>();
    }

    [After(Test)]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    /// <summary>
    /// Critical test: Ensures IsAvailableAsync returns false when API key is null.
    /// This prevents the "DeepSeek not available" issue from recurring.
    /// </summary>
    [Test]
    public async Task IsAvailableAsync_WithNullApiKey_ReturnsFalse()
    {
        // Arrange
        using var provider = new DeepSeekProvider(null!, TestModel, _httpClient, _mockLogger.Object);

        // Act
        var result = await provider.IsAvailableAsync();

        // Assert
        await Assert.That(result).IsFalse();
        
        // Note: No HTTP request should be made since API key is null
        // The IsAvailableAsync method should return false immediately
    }

    /// <summary>
    /// Critical test: Ensures IsAvailableAsync returns false when API key is empty.
    /// This prevents the "DeepSeek not available" issue from recurring.
    /// </summary>
    [Test]
    public async Task IsAvailableAsync_WithEmptyApiKey_ReturnsFalse()
    {
        // Arrange
        using var provider = new DeepSeekProvider("", TestModel, _httpClient, _mockLogger.Object);

        // Act
        var result = await provider.IsAvailableAsync();

        // Assert
        await Assert.That(result).IsFalse();
        
        // Note: No HTTP request should be made since API key is empty
        // The IsAvailableAsync method should return false immediately
    }

    /// <summary>
    /// Critical test: Ensures IsAvailableAsync returns false when API key is whitespace.
    /// This prevents the "DeepSeek not available" issue from recurring.
    /// </summary>
    [Test]
    public async Task IsAvailableAsync_WithWhitespaceApiKey_ReturnsFalse()
    {
        // Arrange
        using var provider = new DeepSeekProvider("   ", TestModel, _httpClient, _mockLogger.Object);

        // Act
        var result = await provider.IsAvailableAsync();

        // Assert
        await Assert.That(result).IsFalse();
        
        // Note: No HTTP request should be made since API key is whitespace
        // The IsAvailableAsync method should return false immediately
    }

    /// <summary>
    /// Regression test: Ensures IsAvailableAsync works correctly with valid API key.
    /// This verifies the fix doesn't break normal functionality.
    /// </summary>
    [Test]
    public async Task IsAvailableAsync_WithValidApiKey_MakesHttpRequest()
    {
        // Arrange
        var validApiKey = "sk-test-valid-api-key";
        using var provider = new DeepSeekProvider(validApiKey, TestModel, _httpClient, _mockLogger.Object);
        
        var responseJson = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new
                {
                    id = "deepseek-chat",
                    @object = "model"
                }
            }
        });

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await provider.IsAvailableAsync();

        // Assert
        await Assert.That(result).IsTrue();
        
        // Note: HTTP request should be made with valid API key
        // The mock setup ensures a successful response is returned
    }

    /// <summary>
    /// Integration test: Verifies the complete flow from UpdateActiveProviderAsync perspective.
    /// This ensures the availability check integrates correctly with the provider update logic.
    /// </summary>
    [Test]
    public async Task DeepSeekProvider_AvailabilityCheck_IntegratesWithProviderLogic()
    {
        // Arrange - Simulate the exact conditions that caused the original issue
        var providerType = AiProviderType.DeepSeek;
        var requiresApiKey = AiProviderFactory.RequiresApiKey(providerType);
        
        // Test scenario 1: No API key available (original issue scenario)
        using var providerWithoutKey = new DeepSeekProvider(null!, TestModel, _httpClient, _mockLogger.Object);
        
        // Act
        var availabilityWithoutKey = await providerWithoutKey.IsAvailableAsync();
        
        // Assert
        await Assert.That(requiresApiKey).IsTrue(); // DeepSeek requires API key
        await Assert.That(availabilityWithoutKey).IsFalse(); // Should be unavailable without key
        
        // Test scenario 2: Valid API key available
        var validApiKey = "sk-test-valid-key";
        using var providerWithKey = new DeepSeekProvider(validApiKey, TestModel, _httpClient, _mockLogger.Object);
        
        // Mock successful response for valid key
        var responseJson = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { id = "deepseek-chat", @object = "model" }
            }
        });

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        
        var availabilityWithKey = await providerWithKey.IsAvailableAsync();
        
        // Assert
        await Assert.That(availabilityWithKey).IsTrue(); // Should be available with valid key
    }
}