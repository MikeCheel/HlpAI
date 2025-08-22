using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using TUnit.Assertions;
using TUnit.Core;

namespace HlpAI.Tests.Services;

public class EmbeddingServiceTests
{
    private Mock<ILogger> _mockLogger = null!;
    private MockHttpMessageHandler _mockHandler = null!;
    private HttpClient _httpClient = null!;
    private EmbeddingService _embeddingService = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
        _mockHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHandler);
        
        // Use the constructor that accepts HttpClient for testing
        _embeddingService = new EmbeddingService(_httpClient, "http://localhost:11434", "nomic-embed-text", _mockLogger.Object);
    }

    [After(Test)]
    public void Cleanup()
    {
        _embeddingService?.Dispose();
        _httpClient?.Dispose();
        _mockHandler?.Dispose();
    }

    [Test]
    public async Task Constructor_WithDefaults_SetsCorrectValues()
    {
        // Act
        using var service = new EmbeddingService();

        // Assert - We can't directly test private fields, but we can test behavior
        await Assert.That(service).IsNotNull();
    }

    [Test]
    public async Task Constructor_WithCustomParameters_SetsCorrectValues()
    {
        // Act
        using var service = new EmbeddingService("http://custom:8080", "custom-model", _mockLogger.Object);

        // Assert
        await Assert.That(service).IsNotNull();
    }

    [Test]
    public async Task GetEmbeddingAsync_WithSimpleText_ReturnsEmbedding()
    {
        // Arrange
        const string testText = "test content";
        var mockEmbedding = new float[384]; // Create a mock embedding array
        for (int i = 0; i < 384; i++)
        {
            mockEmbedding[i] = 0.1f + (i * 0.001f); // Generate some test values
        }
        
        var embeddingJson = "[" + string.Join(",", mockEmbedding) + "]";
        _mockHandler.SetupResponse("/api/embeddings", 
            "{\"embedding\":" + embeddingJson + "}");

        // Act
        var result = await _embeddingService.GetEmbeddingAsync(testText);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Length).IsEqualTo(384);
        await Assert.That(result.All(x => !float.IsNaN(x))).IsTrue();
        await Assert.That(result[0]).IsEqualTo(0.1f);
    }

    [Test]
    public async Task GetEmbeddingAsync_WithEmptyText_ReturnsEmbedding()
    {
        // Arrange
        var mockEmbedding = new float[384];
        for (int i = 0; i < 384; i++)
        {
            mockEmbedding[i] = 0.05f;
        }
        
        var embeddingJson = "[" + string.Join(",", mockEmbedding) + "]";
        _mockHandler.SetupResponse("/api/embeddings", 
            "{\"embedding\":" + embeddingJson + "}");

        // Act
        var result = await _embeddingService.GetEmbeddingAsync("");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Length).IsEqualTo(384);
    }

    [Test]
    public async Task GetEmbeddingAsync_WithLongText_ReturnsEmbedding()
    {
        // Arrange
        var longText = string.Concat(Enumerable.Repeat("This is a long text. ", 1000));
        var mockEmbedding = new float[384];
        for (int i = 0; i < 384; i++)
        {
            mockEmbedding[i] = 0.2f + (i * 0.0001f);
        }
        
        var embeddingJson = "[" + string.Join(",", mockEmbedding) + "]";
        _mockHandler.SetupResponse("/api/embeddings", 
            "{\"embedding\":" + embeddingJson + "}");

        // Act
        var result = await _embeddingService.GetEmbeddingAsync(longText);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Length).IsEqualTo(384);
    }

    [Test]
    public async Task GetEmbeddingAsync_WithSpecialCharacters_ReturnsEmbedding()
    {
        // Arrange
        const string specialText = "Special chars: ñáéíóú 你好世界 🌍 @#$%^&*()";
        var mockEmbedding = new float[384];
        for (int i = 0; i < 384; i++)
        {
            mockEmbedding[i] = 0.3f + (i * 0.00001f);
        }
        
        var embeddingJson = "[" + string.Join(",", mockEmbedding) + "]";
        _mockHandler.SetupResponse("/api/embeddings", 
            "{\"embedding\":" + embeddingJson + "}");

        // Act
        var result = await _embeddingService.GetEmbeddingAsync(specialText);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Length).IsEqualTo(384);
    }

    [Test]
    public async Task CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        // Arrange
        float[] vector = [1.0f, 2.0f, 3.0f, 4.0f];

        // Act
        var similarity = EmbeddingService.CosineSimilarity(vector, vector);

        // Assert
        await Assert.That(Math.Abs(similarity - 1.0f) < 0.0001f).IsTrue();
    }

    [Test]
    public async Task CosineSimilarity_OppositeVectors_ReturnsNegativeOne()
    {
        // Arrange
        float[] vector1 = [1.0f, 2.0f, 3.0f];
        float[] vector2 = [-1.0f, -2.0f, -3.0f];

        // Act
        var similarity = EmbeddingService.CosineSimilarity(vector1, vector2);

        // Assert
        await Assert.That(Math.Abs(similarity - (-1.0f)) < 0.0001f).IsTrue();
    }

    [Test]
    public async Task CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        // Arrange
        float[] vector1 = [1.0f, 0.0f];
        float[] vector2 = [0.0f, 1.0f];

        // Act
        var similarity = EmbeddingService.CosineSimilarity(vector1, vector2);

        // Assert
        await Assert.That(Math.Abs(similarity - 0.0f) < 0.0001f).IsTrue();
    }

    [Test]
    public async Task CosineSimilarity_DifferentLengthVectors_ReturnsZero()
    {
        // Arrange
        float[] vector1 = [1.0f, 2.0f, 3.0f];
        float[] vector2 = [1.0f, 2.0f];

        // Act
        var similarity = EmbeddingService.CosineSimilarity(vector1, vector2);

        // Assert
        await Assert.That(similarity).IsEqualTo(0.0f);
    }

    [Test]
    public async Task CosineSimilarity_ZeroVectors_ReturnsZero()
    {
        // Arrange
        float[] vector1 = [0.0f, 0.0f, 0.0f];
        float[] vector2 = [1.0f, 2.0f, 3.0f];

        // Act
        var similarity = EmbeddingService.CosineSimilarity(vector1, vector2);

        // Assert
        await Assert.That(similarity).IsEqualTo(0.0f);
    }

    [Test]
    public async Task CosineSimilarity_BothZeroVectors_ReturnsZero()
    {
        // Arrange
        float[] vector1 = [0.0f, 0.0f, 0.0f];
        float[] vector2 = [0.0f, 0.0f, 0.0f];

        // Act
        var similarity = EmbeddingService.CosineSimilarity(vector1, vector2);

        // Assert
        await Assert.That(similarity).IsEqualTo(0.0f);
    }

    [Test]
    public async Task CosineSimilarity_NormalizedVectors_CalculatesCorrectly()
    {
        // Arrange
        float[] vector1 = [0.6f, 0.8f]; // Length = 1
        float[] vector2 = [0.8f, 0.6f]; // Length = 1

        // Act
        var similarity = EmbeddingService.CosineSimilarity(vector1, vector2);

        // Assert
        // Dot product = 0.6*0.8 + 0.8*0.6 = 0.48 + 0.48 = 0.96
        await Assert.That(Math.Abs(similarity - 0.96f) < 0.0001f).IsTrue();
    }

    [Test]
    public async Task GetEmbeddingAsync_ConsistentForSameInput_ReturnsIdenticalResults()
    {
        // Arrange
        const string testText = "consistent test input";
        var mockEmbedding = new float[384];
        for (int i = 0; i < 384; i++)
        {
            mockEmbedding[i] = 0.5f;
        }
        
        var embeddingJson = "[" + string.Join(",", mockEmbedding) + "]";
        _mockHandler.SetupResponse("/api/embeddings", 
            "{\"embedding\":" + embeddingJson + "}");

        // Act
        var result1 = await _embeddingService.GetEmbeddingAsync(testText);
        var result2 = await _embeddingService.GetEmbeddingAsync(testText);

        // Assert
        await Assert.That(result1.Length).IsEqualTo(result2.Length);
        for (int i = 0; i < result1.Length; i++)
        {
            await Assert.That(Math.Abs(result1[i] - result2[i]) < 0.0001f).IsTrue();
        }
    }

    [Test]
    public async Task GetEmbeddingAsync_DifferentInputs_ReturnsDifferentResults()
    {
        // Arrange
        var mockEmbedding1 = new float[384];
        var mockEmbedding2 = new float[384];
        for (int i = 0; i < 384; i++)
        {
            mockEmbedding1[i] = 0.4f + (i * 0.001f);
            mockEmbedding2[i] = 0.6f + (i * 0.002f); // Different values
        }
        
        var embeddingJson1 = "[" + string.Join(",", mockEmbedding1) + "]";
        var embeddingJson2 = "[" + string.Join(",", mockEmbedding2) + "]";
        
        // Setup different responses for different calls
        _mockHandler.SetupResponse("/api/embeddings", 
            "{\"embedding\":" + embeddingJson1 + "}");

        // Act
        var result1 = await _embeddingService.GetEmbeddingAsync("first text");
        
        // Setup second response
        _mockHandler.SetupResponse("/api/embeddings", 
            "{\"embedding\":" + embeddingJson2 + "}");
        
        var result2 = await _embeddingService.GetEmbeddingAsync("second text");

        // Assert
        await Assert.That(result1.Length).IsEqualTo(result2.Length);
        
        // Should have at least some different values
        var differentCount = 0;
        for (int i = 0; i < result1.Length; i++)
        {
            if (Math.Abs(result1[i] - result2[i]) > 0.0001f)
                differentCount++;
        }
        
        await Assert.That(differentCount > 0).IsTrue();
    }

    [Test]
    public void Dispose_CallMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var service = new EmbeddingService();

        // Act & Assert
        service.Dispose();
        service.Dispose(); // Should not throw on multiple calls
    }

    [Test]
    public async Task GetEmbeddingAsync_AfterDispose_StillWorks()
    {
        // Arrange
        var mockEmbedding = new float[384];
        for (int i = 0; i < 384; i++)
        {
            mockEmbedding[i] = 0.1f;
        }
        
        var embeddingJson = "[" + string.Join(",", mockEmbedding) + "]";
        _mockHandler.SetupResponse("/api/embeddings", 
            "{\"embedding\":" + embeddingJson + "}");
        
        _embeddingService.Dispose();

        // Act & Assert - This should still work as it falls back to simple embedding
        var result = await _embeddingService.GetEmbeddingAsync("test");
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Length).IsEqualTo(384);
    }
}