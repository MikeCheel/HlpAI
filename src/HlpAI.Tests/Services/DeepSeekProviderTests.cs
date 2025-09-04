using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using HlpAI.Services;

namespace HlpAI.Tests.Services;

public class DeepSeekProviderTests
{
    private Mock<HttpMessageHandler> _mockHttpMessageHandler = null!;
    private HttpClient _httpClient = null!;
    private Mock<ILogger> _mockLogger = null!;
    private DeepSeekProvider _provider = null!;
    
    private const string TestApiKey = "test-api-key";
    private const string TestModel = "deepseek-chat";
    private const string TestBaseUrl = "https://api.deepseek.com";

    [Before(Test)]
    public void Setup()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(TestBaseUrl)
        };
        _mockLogger = new Mock<ILogger>();
        _provider = new DeepSeekProvider(TestApiKey, TestModel, _httpClient, _mockLogger.Object);
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
        await Assert.That(_provider.ProviderType).IsEqualTo(AiProviderType.DeepSeek);
        await Assert.That(_provider.ProviderName).IsEqualTo("DeepSeek");
        await Assert.That(_provider.BaseUrl).IsEqualTo(TestBaseUrl);
        await Assert.That(_provider.CurrentModel).IsEqualTo(TestModel);
        await Assert.That(_provider.ApiKey).IsEqualTo(TestApiKey);
        await Assert.That(_provider.DefaultModel).IsEqualTo("deepseek-chat");
    }

    [Test]
    public async Task Constructor_WithNullApiKey_CreatesProvider()
    {
        // Act - Now allows null API key for local/testing scenarios
        var provider = new DeepSeekProvider(null!, TestModel);
        
        // Assert
        await Assert.That(provider).IsNotNull();
        await Assert.That(provider.CurrentModel).IsEqualTo(TestModel);
    }

    [Test]
    public async Task Constructor_WithEmptyApiKey_CreatesProvider()
    {
        // Act - Now allows empty API key for local/testing scenarios
        var provider = new DeepSeekProvider("", TestModel);
        
        // Assert
        await Assert.That(provider).IsNotNull();
        await Assert.That(provider.CurrentModel).IsEqualTo(TestModel);
    }

    [Test]
    public async Task Constructor_WithNullModel_UsesDefaultModel()
    {
        // Act
        var provider = new DeepSeekProvider(TestApiKey, null!);
        
        // Assert
        await Assert.That(provider).IsNotNull();
        await Assert.That(provider.CurrentModel).IsEqualTo("deepseek-chat");
    }

    [Test]
    public async Task GenerateAsync_WithValidResponse_ReturnsContent()
    {
        // Arrange
        var expectedResponse = "Test response from DeepSeek";
        var responseJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = expectedResponse
                    }
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
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = "Response"
                    }
                }
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
        await Assert.That(result).Contains("Could not connect to DeepSeek");
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
        await Assert.That(result).Contains("Error: DeepSeek API returned Unauthorized");
    }

    [Test]
    public async Task IsAvailableAsync_WithSuccessResponse_ReturnsTrue()
    {
        // Arrange
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
    public async Task GetModelsAsync_WithSuccessResponse_ReturnsModels()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new
                {
                    id = "deepseek-chat",
                    @object = "model"
                },
                new
                {
                    id = "deepseek-coder",
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
        var result = await _provider.GetModelsAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result).Contains("deepseek-chat");
        await Assert.That(result).Contains("deepseek-coder");
    }

    [Test]
    public async Task GetModelsAsync_WithFailureResponse_ReturnsDefaultModels()
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
        var result = await _provider.GetModelsAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsGreaterThan(0);
        await Assert.That(result).Contains("deepseek-chat");
        await Assert.That(result).Contains("deepseek-coder");
    }

    [Test]
    public async Task ValidateApiKeyAsync_WithValidKey_ReturnsTrue()
    {
        // Arrange
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
            data = new[]
            {
                new
                {
                    id = "deepseek-chat",
                    @object = "model"
                }
            }
        });

        var response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
        response.Headers.Add("x-ratelimit-limit-requests", "1000");
        response.Headers.Add("x-ratelimit-remaining-requests", "999");
        response.Headers.Add("x-ratelimit-limit-tokens", "50000");
        response.Headers.Add("x-ratelimit-remaining-tokens", "49000");
        response.Headers.Add("x-ratelimit-reset-requests", "2024-01-01T00:00:00Z");

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
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = "test"
                    }
                }
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
        await Assert.That(capturedRequest!.RequestUri!.ToString()).Contains("/v1/chat/completions");
        await Assert.That(capturedRequest.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(capturedRequest.Headers.Authorization).IsNotNull();
        await Assert.That(capturedRequest.Headers.Authorization!.Scheme).IsEqualTo("Bearer");
        await Assert.That(capturedRequest.Headers.Authorization.Parameter).IsEqualTo(TestApiKey);
    }

    [Test]
    public async Task GenerateAsync_WithTemperature_UsesCorrectValue()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = "test"
                    }
                }
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
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = "test"
                    }
                }
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
        await Assert.That(capturedRequest!.Headers.Authorization).IsNotNull();
        await Assert.That(capturedRequest.Headers.Authorization!.Scheme).IsEqualTo("Bearer");
        await Assert.That(capturedRequest.Headers.UserAgent.ToString()).Contains("HlpAI/1.0");
    }


}