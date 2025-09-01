using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using HlpAI.Services;

namespace HlpAI.Tests.Services;

public class OpenAiProviderTests
{
    private Mock<HttpMessageHandler> _mockHttpMessageHandler = null!;
    private HttpClient _httpClient = null!;
    private Mock<ILogger> _mockLogger = null!;
    private OpenAiProvider _provider = null!;
    
    private const string TestApiKey = "test-api-key";
    private const string TestModel = "gpt-4o-mini";
    private const string TestBaseUrl = "https://api.openai.com";

    [Before(Test)]
    public void Setup()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(TestBaseUrl)
        };
        _mockLogger = new Mock<ILogger>();
        _provider = new OpenAiProvider(TestApiKey, TestModel, null, _mockLogger.Object, _httpClient);
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
        await Assert.That(_provider.ProviderType).IsEqualTo(AiProviderType.OpenAI);
        await Assert.That(_provider.ProviderName).IsEqualTo("OpenAI");
        await Assert.That(_provider.BaseUrl).IsEqualTo(TestBaseUrl);
        await Assert.That(_provider.CurrentModel).IsEqualTo(TestModel);
        await Assert.That(_provider.ApiKey).IsEqualTo(TestApiKey);
        await Assert.That(_provider.DefaultModel).IsEqualTo("gpt-4o-mini");
    }

    [Test]
    public async Task Constructor_WithNullApiKey_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => new OpenAiProvider(null!, TestModel))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_WithEmptyApiKey_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => new OpenAiProvider("", TestModel))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_WithNullModel_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => new OpenAiProvider(TestApiKey, null!))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task GenerateAsync_WithValidResponse_ReturnsContent()
    {
        // Arrange
        var prompt = "Test prompt";
        var expectedResponse = "Test response from OpenAI";
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
        var result = await _provider.GenerateAsync(prompt);

        // Assert
        await Assert.That(result).IsEqualTo(expectedResponse);
    }

    [Test]
    public async Task GenerateAsync_WithContext_IncludesContext()
    {
        // Arrange
        var prompt = "Test prompt";
        var context = "Test context";
        var expectedResponse = "Test response with context";
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
        var result = await _provider.GenerateAsync(prompt, context);

        // Assert
        await Assert.That(result).IsEqualTo(expectedResponse);
    }



    [Test]
    public async Task GenerateAsync_WithHttpRequestException_ThrowsHttpRequestException()
    {
        // Arrange
        var prompt = "Test prompt";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        await Assert.That(async () => await _provider.GenerateAsync(prompt))
            .Throws<HttpRequestException>();
    }

    [Test]
    public async Task GenerateAsync_WithNonSuccessStatusCode_ThrowsHttpRequestException()
    {
        // Arrange
        var prompt = "Test prompt";

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
                Content = new StringContent("{\"error\": {\"message\": \"Invalid API key\"}}", Encoding.UTF8, "application/json")
            });

        // Act & Assert
        await Assert.That(async () => await _provider.GenerateAsync(prompt))
            .Throws<HttpRequestException>();
    }

    [Test]
    public async Task IsAvailableAsync_WithSuccessfulConnection_ReturnsTrue()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { id = "gpt-3.5-turbo", @object = "model" }
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
    public async Task IsAvailableAsync_WithFailedConnection_ReturnsFalse()
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
        var result = await _provider.IsAvailableAsync();

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task GetModelsAsync_WithSuccessfulResponse_ReturnsModels()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { id = "gpt-3.5-turbo", @object = "model" },
                new { id = "gpt-4", @object = "model" }
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
        await Assert.That(result).Contains("gpt-3.5-turbo");
        await Assert.That(result).Contains("gpt-4");
    }

    [Test]
    public async Task GetModelsAsync_WithFailedResponse_ReturnsDefaultModels()
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
        var result = await _provider.GetModelsAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task ValidateApiKeyAsync_WithValidKey_ReturnsTrue()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { id = "gpt-3.5-turbo", @object = "model" }
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
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("{\"error\": {\"message\": \"Invalid API key\"}}", Encoding.UTF8, "application/json")
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
    public async Task GetRateLimitInfoAsync_WithFailedResponse_ReturnsNull()
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
        var result = await _provider.GetRateLimitInfoAsync();

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetRateLimitInfoAsync_WithRateLimitHeaders_ReturnsInfo()
    {
        // Arrange
        var response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK
        };
        response.Headers.Add("x-ratelimit-limit-requests", "1000");
        response.Headers.Add("x-ratelimit-remaining-requests", "999");
        response.Headers.Add("x-ratelimit-limit-tokens", "50000");
        response.Headers.Add("x-ratelimit-remaining-tokens", "49000");
        response.Headers.Add("x-ratelimit-reset-requests", "1h");

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
                new { message = new { content = "test" } }
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
                new { message = new { content = "test" } }
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
}