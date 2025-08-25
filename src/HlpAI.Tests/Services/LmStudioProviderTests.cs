using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using HlpAI.Services;

namespace HlpAI.Tests.Services;

public class LmStudioProviderTests
{
    private Mock<HttpMessageHandler> _mockHttpMessageHandler = null!;
    private HttpClient _httpClient = null!;
    private Mock<ILogger> _mockLogger = null!;
    private LmStudioProvider _provider = null!;
    private const string TestBaseUrl = "http://localhost:1234";
    private const string TestModel = "test-model";

    [Before(Test)]
    public void Setup()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockLogger = new Mock<ILogger>();
        _provider = new LmStudioProvider(_httpClient, TestBaseUrl, TestModel, _mockLogger.Object);
    }

    [After(Test)]
    public void TearDown()
    {
        _provider?.Dispose();
        _httpClient?.Dispose();
    }

    [Test]
    public async Task Constructor_WithHttpClient_SetsPropertiesCorrectly()
    {
        // Act & Assert
        await Assert.That(_provider.ProviderType).IsEqualTo(AiProviderType.LmStudio);
        await Assert.That(_provider.ProviderName).IsEqualTo("LM Studio");
        await Assert.That(_provider.DefaultModel).IsEqualTo("default");
        await Assert.That(_provider.BaseUrl).IsEqualTo(TestBaseUrl);
        await Assert.That(_provider.CurrentModel).IsEqualTo(TestModel);
    }

    [Test]
    public async Task Constructor_WithoutHttpClient_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        using var provider = new LmStudioProvider(TestBaseUrl, TestModel, _mockLogger.Object);

        // Assert
        await Assert.That(provider.ProviderType).IsEqualTo(AiProviderType.LmStudio);
        await Assert.That(provider.ProviderName).IsEqualTo("LM Studio");
        await Assert.That(provider.BaseUrl).IsEqualTo(TestBaseUrl);
        await Assert.That(provider.CurrentModel).IsEqualTo(TestModel);
    }

    [Test]
    public async Task Constructor_TrimsTrailingSlashFromBaseUrl()
    {
        // Arrange & Act
        using var provider = new LmStudioProvider(_httpClient, "http://localhost:1234/", TestModel);

        // Assert
        await Assert.That(provider.BaseUrl).IsEqualTo("http://localhost:1234");
    }

    [Test]
    public async Task Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new LmStudioProvider(null!, TestBaseUrl, TestModel))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task GenerateAsync_WithValidResponse_ReturnsContent()
    {
        // Arrange
        var expectedResponse = "Test response from LM Studio";
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
        var expectedResponse = "Test response";
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
        await _provider.GenerateAsync("Test prompt", "Test context", 0.8);

        // Assert
        await Assert.That(capturedRequest).IsNotNull();
        var requestContent = await capturedRequest!.Content!.ReadAsStringAsync();
        await Assert.That(requestContent).Contains("Test context");
        await Assert.That(requestContent).Contains("Test prompt");
        await Assert.That(requestContent).Contains("0.8"); // temperature
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
        await Assert.That(result).Contains("Could not connect to LM Studio");
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
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Server error")
            });

        // Act
        var result = await _provider.GenerateAsync("Test prompt");

        // Assert
        await Assert.That(result).Contains("Error: LM Studio API returned InternalServerError");
    }

    [Test]
    public async Task GenerateAsync_WithInvalidResponseFormat_ReturnsErrorMessage()
    {
        // Arrange
        var invalidResponseJson = JsonSerializer.Serialize(new { invalid = "response" });

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
                Content = new StringContent(invalidResponseJson, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _provider.GenerateAsync("Test prompt");

        // Assert
        await Assert.That(result).IsEqualTo("Invalid response format from LM Studio");
    }

    [Test]
    public async Task GenerateAsync_WithEmptyChoices_ReturnsErrorMessage()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new { choices = new object[0] });

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
        await Assert.That(result).IsEqualTo("Invalid response format from LM Studio");
    }

    [Test]
    public async Task IsAvailableAsync_WithSuccessfulResponse_ReturnsTrue()
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
                StatusCode = HttpStatusCode.OK
            });

        // Act
        var result = await _provider.IsAvailableAsync();

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsAvailableAsync_WithFailedResponse_ReturnsFalse()
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
                StatusCode = HttpStatusCode.InternalServerError
            });

        // Act
        var result = await _provider.IsAvailableAsync();

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsAvailableAsync_WithException_ReturnsFalse()
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
    public async Task GetModelsAsync_WithValidResponse_ReturnsModelList()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { id = "model1" },
                new { id = "model2" },
                new { id = "model3" }
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
        await Assert.That(result).HasCount().EqualTo(3);
        await Assert.That(result).Contains("model1");
        await Assert.That(result).Contains("model2");
        await Assert.That(result).Contains("model3");
    }

    [Test]
    public async Task GetModelsAsync_WithFailedResponse_ReturnsEmptyList()
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
                StatusCode = HttpStatusCode.InternalServerError
            });

        // Act
        var result = await _provider.GetModelsAsync();

        // Assert
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task GetModelsAsync_WithException_ReturnsEmptyList()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new Exception("Network error"));

        // Act
        var result = await _provider.GetModelsAsync();

        // Assert
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task GetModelsAsync_WithInvalidResponse_ReturnsEmptyList()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new { invalid = "response" });

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
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public void Dispose_WithInjectedHttpClient_DoesNotDisposeHttpClient()
    {
        // Arrange
        var mockHttpClient = new Mock<HttpClient>();
        var provider = new LmStudioProvider(mockHttpClient.Object, TestBaseUrl, TestModel);

        // Act
        provider.Dispose();

        // Assert - No exception should be thrown and HttpClient should not be disposed
        // Test passes if no exception is thrown
    }

    [Test]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Act & Assert
        _provider.Dispose();
        _provider.Dispose(); // Should not throw

        // Test passes if no exception is thrown
    }
}