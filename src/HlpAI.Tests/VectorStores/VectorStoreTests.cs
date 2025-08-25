using HlpAI.VectorStores;
using HlpAI.Services;
using HlpAI.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace HlpAI.Tests.VectorStores;

public class VectorStoreTests
{
    private Mock<IEmbeddingService> _mockEmbeddingService = null!;
    private Mock<ILogger> _mockLogger = null!;
    private VectorStore _vectorStore = null!;
    private readonly float[] _sampleEmbedding = [0.1f, 0.2f, 0.3f, 0.4f, 0.5f];
    private readonly float[] _differentEmbedding = [0.9f, 0.8f, 0.7f, 0.6f, 0.5f];

    [Before(Test)]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();

        // Setup mock to return hash-based embedding for consistent but varied test similarities
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync((string text) => GenerateSimpleEmbedding(text));

        _vectorStore = new VectorStore(_mockEmbeddingService.Object, _mockLogger.Object);
    }

    private static float[] GenerateSimpleEmbedding(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));

        var embedding = new float[384];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (hash[i % hash.Length] - 128f) / 128f;
        }

        return embedding;
    }

    [Before(Test)]
    public async Task EnsureCleanState()
    {
        await _vectorStore.ClearIndexAsync();
    }

    [After(Test)]
    public void Cleanup()
    {
        _vectorStore?.Dispose();
    }

    [Test]
    public async Task IndexDocumentAsync_WithValidContent_IndexesSuccessfully()
    {
        // Arrange
        var filePath = "test.txt";
        var content = "This is a test document with some content to be indexed.";
        var metadata = new Dictionary<string, object> { ["author"] = "test" };

        // Act
        await _vectorStore.IndexDocumentAsync(filePath, content, metadata);

        // Assert
        await Assert.That(_vectorStore.GetChunkCount()).IsGreaterThan(0);
        await Assert.That(_vectorStore.GetIndexedFiles()).Contains(filePath);
        
        // Verify content was indexed (embedding service was used)
        await Assert.That(_vectorStore.GetChunkCount()).IsGreaterThan(0);
    }

    [Test]
    public async Task IndexDocumentAsync_WithLongContent_CreatesMultipleChunks()
    {
        // Arrange
        var filePath = "long_test.txt";
        var content = string.Join(" ", Enumerable.Repeat("word", 2000)); // Create long content

        // Act
        await _vectorStore.IndexDocumentAsync(filePath, content);

        // Assert
        await Assert.That(_vectorStore.GetChunkCount()).IsGreaterThan(1);
        await Assert.That(_vectorStore.GetIndexedFiles()).Contains(filePath);
    }

    [Test]
    public async Task IndexDocumentAsync_WithEmbeddingServiceException_HandlesGracefully()
    {
        // Arrange - Setup mock to throw exception
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Embedding service error"));

        var filePath = "error_test.txt";
        var content = "This will cause an error.";

        // Act & Assert - Should not throw, but should not add chunks
        await _vectorStore.IndexDocumentAsync(filePath, content);
        
        await Assert.That(_vectorStore.GetChunkCount()).IsEqualTo(0);
    }

    [Test]
    public async Task SearchAsync_WithEmbeddingServiceException_ReturnsEmptyResults()
    {
        // Arrange - Setup mock to throw for search query
        await _vectorStore.IndexDocumentAsync("test.txt", "Test content");
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Embedding service error"));

        var query = new RagQuery { Query = "test", TopK = 5, MinSimilarity = 0.0f };

        // Act
        var results = await _vectorStore.SearchAsync(query);

        // Assert
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task SearchAsync_WithEmptyIndex_ReturnsEmptyResults()
    {
        // Arrange
        var query = new RagQuery { Query = "test query", TopK = 5, MinSimilarity = 0.5f };

        // Act
        var results = await _vectorStore.SearchAsync(query);

        // Assert
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task SearchAsync_WithIndexedContent_ReturnsResults()
    {
        // Arrange
        var filePath = "search_test.txt";
        var content = "This is a test document for searching functionality.";

        // Override mock for indexing to use sample embedding
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(_sampleEmbedding);

        await _vectorStore.IndexDocumentAsync(filePath, content);
        
        var query = new RagQuery { Query = "test document", TopK = 5, MinSimilarity = 0.0f };

        // Override mock for query to match sample for high similarity
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(query.Query))
            .ReturnsAsync(_sampleEmbedding);

        // Act
        var results = await _vectorStore.SearchAsync(query);

        // Assert
        await Assert.That(results).IsNotEmpty();
        await Assert.That(results[0].Chunk.SourceFile).IsEqualTo(filePath);
        await Assert.That(results[0].Similarity).IsGreaterThanOrEqualTo(0.0f);
    }

    [Test]
    public async Task SearchAsync_WithFileFilters_FiltersCorrectly()
    {
        // Arrange
        await _vectorStore.IndexDocumentAsync("file1.txt", "Content for file one");
        await _vectorStore.IndexDocumentAsync("file2.txt", "Content for file two");
                
        var query = new RagQuery 
        { 
            Query = "content", 
            TopK = 5, 
            MinSimilarity = 0.0f,
            FileFilters = ["file1"]
        };

        // Debug output before search
        Console.WriteLine($"Total chunks before search: {await _vectorStore.GetChunkCountAsync()}");
        Console.WriteLine($"Indexed files: {string.Join(", ", await _vectorStore.GetIndexedFilesAsync())}");
        Console.WriteLine($"Query: {query.Query}");
        Console.WriteLine($"File filters: {string.Join(", ", query.FileFilters ?? [])}");
        
        // Act
        var results = await _vectorStore.SearchAsync(query);
        
        // Debug output after search
        Console.WriteLine($"Results count: {results.Count}");
        if (results.Count > 0)
        {
            Console.WriteLine($"First result file: {results[0].Chunk.SourceFile}, similarity: {results[0].Similarity}");
        }
        else
        {
            Console.WriteLine("No results found!");
        }

        // Assert
        await Assert.That(results).IsNotEmpty();
        await Assert.That(results).HasCount().EqualTo(1);
        await Assert.That(results[0].Chunk.SourceFile).IsEqualTo("file1.txt");
        await Assert.That(results.All(r => r.Chunk.SourceFile.Contains("file1"))).IsTrue();
    }

    [Test]
    public async Task SearchAsync_WithMinSimilarityFilter_FiltersLowSimilarity()
    {
        // Arrange
        var filePath = "similarity_test.txt";
        var content = "This is a test document.";

        // Override mock for indexing to use sample embedding
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(_sampleEmbedding);

        await _vectorStore.IndexDocumentAsync(filePath, content);
        
        var query = new RagQuery { Query = "completely different query", TopK = 5, MinSimilarity = 0.9f };

        // Override mock for query to use different embedding for low similarity
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(query.Query))
            .ReturnsAsync(_differentEmbedding);

        // Act
        var results = await _vectorStore.SearchAsync(query);

        // Assert - Should return empty results due to high similarity threshold
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task SearchAsync_WithTopKLimit_LimitsResults()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _vectorStore.IndexDocumentAsync($"file{i}.txt", $"Content for file {i}");
        }
        
        var query = new RagQuery { Query = "content", TopK = 3, MinSimilarity = 0.0f };

        // Act
        var results = await _vectorStore.SearchAsync(query);

        // Assert
        await Assert.That(results.Count).IsLessThanOrEqualTo(3);
    }



    [Test]
    public async Task GetChunkCount_WithIndexedContent_ReturnsCorrectCount()
    {
        // Arrange
        var filePath = "count_test.txt";
        var content = "Short content";
        await _vectorStore.IndexDocumentAsync(filePath, content);

        // Act
        var count = _vectorStore.GetChunkCount();

        // Assert
        await Assert.That(count).IsGreaterThan(0);
    }

    [Test]
    public async Task GetChunkCountAsync_WithIndexedContent_ReturnsCorrectCount()
    {
        // Arrange
        var filePath = "async_count_test.txt";
        var content = "Short content";
        await _vectorStore.IndexDocumentAsync(filePath, content);

        // Act
        var count = await _vectorStore.GetChunkCountAsync();

        // Assert
        await Assert.That(count).IsGreaterThan(0);
    }

    [Test]
    public async Task GetIndexedFiles_WithMultipleFiles_ReturnsAllFiles()
    {
        // Arrange
        await _vectorStore.IndexDocumentAsync("file1.txt", "Content 1");
        await _vectorStore.IndexDocumentAsync("file2.txt", "Content 2");

        // Act
        var files = _vectorStore.GetIndexedFiles();

        // Assert
        await Assert.That(files).Contains("file1.txt");
        await Assert.That(files).Contains("file2.txt");
        await Assert.That(files.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetIndexedFilesAsync_WithMultipleFiles_ReturnsAllFiles()
    {
        // Arrange
        await _vectorStore.IndexDocumentAsync("async_file1.txt", "Content 1");
        await _vectorStore.IndexDocumentAsync("async_file2.txt", "Content 2");

        // Act
        var files = await _vectorStore.GetIndexedFilesAsync();

        // Assert
        await Assert.That(files).Contains("async_file1.txt");
        await Assert.That(files).Contains("async_file2.txt");
        await Assert.That(files.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ClearIndex_WithIndexedContent_ClearsAllChunks()
    {
        // Arrange
        await _vectorStore.IndexDocumentAsync("clear_test.txt", "Content to clear");
        await Assert.That(_vectorStore.GetChunkCount()).IsGreaterThan(0);

        // Act
        _vectorStore.ClearIndex();

        // Assert
        await Assert.That(_vectorStore.GetChunkCount()).IsEqualTo(0);
        await Assert.That(_vectorStore.GetIndexedFiles()).IsEmpty();
    }

    [Test]
    public async Task ClearIndexAsync_WithIndexedContent_ClearsAllChunks()
    {
        // Arrange
        await _vectorStore.IndexDocumentAsync("async_clear_test.txt", "Content to clear");
        await Assert.That(_vectorStore.GetChunkCount()).IsGreaterThan(0);

        // Act
        await _vectorStore.ClearIndexAsync();

        // Assert
        await Assert.That(_vectorStore.GetChunkCount()).IsEqualTo(0);
        await Assert.That(_vectorStore.GetIndexedFiles()).IsEmpty();
    }

    [Test]
    public async Task Dispose_DisposesResourcesProperly()
    {
        // Arrange
        await _vectorStore.IndexDocumentAsync("dispose_test.txt", "Content");
        await Assert.That(_vectorStore.GetChunkCount()).IsGreaterThan(0);

        // Act
        _vectorStore.Dispose();

        // Assert - After disposal, chunks should be cleared
        await Assert.That(_vectorStore.GetChunkCount()).IsEqualTo(0);
    }
}