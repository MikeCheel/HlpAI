using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using HlpAI.Services;

namespace HlpAI.Tests.Services;

public class AnthropicProviderTests
{
    private Mock<HttpMessageHandler> _mockHttpMessageHandler = null!;
    private HttpClient _httpClient = null!;
    private Mock<ILogger> _mockLogger = null!;
    private AnthropicProvider _provider = null!;
    
    private const string TestApiKey = "test-api-key";
    private const string TestModel = "claude-3-5-haiku-20241022";
    private const string TestBaseUrl = "https://api.anthropic.com";

    [Before(Test)]
    public void Setup()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(TestBaseUrl)
        };
        _mockLogger = new Mock<ILogger>();
        _provider = new AnthropicProvider(TestApiKey, TestModel, _httpClient, _mockLogger.Object);
    }

    [After(Test)]
    public void TearDown()
    {
        _provider?.Dispose();
        _httpClient?.Dispose();
    }

    [Test]
    public async Task Constructor_SetsPropertiesCorrectly()
    {
        // Assert
        await Assert.That(_provider.ProviderType).IsEqualTo(AiProviderType.Anthropic);
        await Assert.That(_provider.ProviderName).IsEqualTo("Anthropic");
        await Assert.That(_provider.BaseUrl).IsEqualTo(TestBaseUrl);
        await Assert.That(_provider.CurrentModel).IsEqualTo(TestModel);
        await Assert.That(_provider.ApiKey).IsEqualTo(TestApiKey);
        await Assert.That(_provider.DefaultModel).IsEqualTo("claude-3-5-haiku-20241022");
    }

    [Test]
    public async Task Constructor_WithNullApiKey_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => new AnthropicProvider(null!, TestModel))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_WithEmptyApiKey_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => new AnthropicProvider("", TestModel))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_WithNullModel_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => new AnthropicProvider(TestApiKey, null!))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task GenerateAsync_WithValidResponse_ReturnsContent()
    {
        // Arrange
        var expectedResponse = "Test response from Anthropic";
        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { text = expectedResponse }
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
        var result = await _provider.GenerateAsync("Test prompt");

        // Assert
        await Assert.That(result).IsEqualTo(expectedResponse);
    }

    [Test]
    public async Task GenerateAsync_WithContext_IncludesSystemMessage()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { text = "Response" }
            }
        });

        HttpRequestMessage? capturedRequest = null;
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        await _provider.GenerateAsync("Test prompt", "Test context");

        // Assert
        await Assert.That(capturedRequest).IsNotNull();
        var requestContent = await capturedRequest!.Content!.ReadAsStringAsync();
        await Assert.That(requestContent).Contains("Test context");
        await Assert.That(requestContent).Contains("system");
    }

    [Test]
    public async Task GenerateAsync_WithHttpRequestException_ReturnsErrorMessage()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act
        var result = await _provider.GenerateAsync("Test prompt");

        // Assert
        await Assert.That(result).Contains("Could not connect to Anthropic");
    }

    [Test]
    public async Task GenerateAsync_WithNonSuccessStatusCode_ReturnsErrorMessage()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("Unauthorized")
            });

        // Act
        var result = await _provider.GenerateAsync("Test prompt");

        // Assert
        await Assert.That(result).Contains("Error: Anthropic API returned Unauthorized");
    }

    [Test]
    public async Task IsAvailableAsync_WithSuccessResponse_ReturnsTrue()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { text = "Test" }
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
        var result = await _provider.IsAvailableAsync();

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsAvailableAsync_WithFailureResponse_ReturnsFalse()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized
            });

        // Act
        var result = await _provider.IsAvailableAsync();

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task GetModelsAsync_ReturnsDefaultModels()
    {
        // Act
        var result = await _provider.GetModelsAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsGreaterThan(0);
        await Assert.That(result).Contains("claude-3-5-haiku-20241022");
        await Assert.That(result).Contains("claude-3-5-sonnet-20241022");
        await Assert.That(result).Contains("claude-3-opus-20240229");
    }

    [Test]
    public async Task ValidateApiKeyAsync_WithValidKey_ReturnsTrue()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { text = "Test" }
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
        var result = await _provider.ValidateApiKeyAsync();

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ValidateApiKeyAsync_WithInvalidKey_ReturnsFalse()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized
            });

        // Act
        var result = await _provider.ValidateApiKeyAsync();

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task GetUsageInfoAsync_ReturnsNull()
    {
        // Act
        var result = await _provider.GetUsageInfoAsync();

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetRateLimitInfoAsync_WithRateLimitHeaders_ReturnsInfo()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { text = "Test" }
            }
        });

        var response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
        response.Headers.Add("anthropic-ratelimit-requests-limit", "1000");
        response.Headers.Add("anthropic-ratelimit-requests-remaining", "999");
        response.Headers.Add("anthropic-ratelimit-tokens-limit", "50000");
        response.Headers.Add("anthropic-ratelimit-tokens-remaining", "49000");
        response.Headers.Add("anthropic-ratelimit-requests-reset", "2024-01-01T00:00:00Z");

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(response);

        // Act
        var result = await _provider.GetRateLimitInfoAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.RequestsPerMinute).IsEqualTo(1000);
        await Assert.That(result.RequestsRemaining).IsEqualTo(999);
        await Assert.That(result.TokensPerMinute).IsEqualTo(50000);
        await Assert.That(result.TokensRemaining).IsEqualTo(49000);
    }

    [Test]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Act & Assert
        _provider.Dispose();
        _provider.Dispose(); // Should not throw

        // Test passes if no exception is thrown
    }

    [Test]
    public async Task GenerateAsync_UsesCorrectEndpoint()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { text = "test" }
            }
        });
        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        await _provider.GenerateAsync("Test prompt");

        // Assert
        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.RequestUri!.ToString()).Contains("/v1/messages");
        await Assert.That(capturedRequest.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(capturedRequest.Headers.Contains("x-api-key")).IsTrue();
        await Assert.That(capturedRequest.Headers.GetValues("x-api-key").First()).IsEqualTo(TestApiKey);
    }

    [Test]
    public async Task GenerateAsync_WithTemperature_UsesCorrectValue()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { text = "test" }
            }
        });
        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        await _provider.GenerateAsync("Test prompt", temperature: 0.9);

        // Assert
        await Assert.That(capturedRequest).IsNotNull();
        var requestContent = await capturedRequest!.Content!.ReadAsStringAsync();
        await Assert.That(requestContent).Contains("\"temperature\":0.9");
    }

    [Test]
    public async Task GenerateAsync_UsesCorrectHeaders()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { text = "test" }
            }
        });
        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        await _provider.GenerateAsync("Test prompt");

        // Assert
        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.Headers.Contains("x-api-key")).IsTrue();
        await Assert.That(capturedRequest.Headers.Contains("anthropic-version")).IsTrue();
        await Assert.That(capturedRequest.Headers.Contains("User-Agent")).IsTrue();
        await Assert.That(capturedRequest.Headers.GetValues("anthropic-version").First()).IsEqualTo("2023-06-01");
        await Assert.That(capturedRequest.Headers.GetValues("User-Agent").First()).IsEqualTo("HlpAI/1.0");
    }
}