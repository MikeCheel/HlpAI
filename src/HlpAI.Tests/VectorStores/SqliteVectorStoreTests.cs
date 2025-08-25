using HlpAI.VectorStores;
using HlpAI.Services;
using HlpAI.Models;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.Data.Sqlite;
using Moq.Protected;

namespace HlpAI.Tests.VectorStores;

public class SqliteVectorStoreTests
{
    private IEmbeddingService _embeddingService = null!;
    private Mock<ILogger> _mockLogger = null!;
    private SqliteVectorStore _vectorStore = null!;
    private string _testDbPath = null!;
    private readonly float[] _sampleEmbedding = [0.1f, 0.2f, 0.3f, 0.4f, 0.5f];
    private readonly float[] _differentEmbedding = [0.9f, 0.8f, 0.7f, 0.6f, 0.5f];
    private Mock<IEmbeddingService> _mockEmbeddingService = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_vectors_{Guid.NewGuid()}.db");
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync((string text) =>
            {
                var hash = text.GetHashCode();
                return Enumerable.Range(0, 384).Select(i => (float)hash / (i + 1)).ToArray();
            });
        _embeddingService = _mockEmbeddingService.Object;
        _vectorStore = new SqliteVectorStore(_embeddingService, _testDbPath, _mockLogger.Object);
    }

    [After(Test)]
    public void Cleanup()
    {
        _vectorStore?.Dispose();
        _embeddingService?.Dispose();
        if (File.Exists(_testDbPath))
        {
            try
            {
                File.Delete(_testDbPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Test]
    public async Task Constructor_CreatesDatabase_Successfully()
    {
        // Assert
        await Assert.That(File.Exists(_testDbPath)).IsTrue();
        
        // Verify database structure
        using var connection = new SqliteConnection($"Data Source={_testDbPath}");
        await connection.OpenAsync();
        
        var command = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='document_chunks'", connection);
        var result = await command.ExecuteScalarAsync();
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result?.ToString()).IsEqualTo("document_chunks");
    }

    [Test]
    public async Task Constructor_MarksVectorDatabaseAsHidden_Successfully()
    {
        // Assert - Database file should be marked as hidden
        await Assert.That(File.Exists(_testDbPath)).IsTrue();
        
        var attributes = File.GetAttributes(_testDbPath);
        await Assert.That(attributes.HasFlag(FileAttributes.Hidden)).IsTrue();
    }

    [Test]
    public async Task IndexDocumentAsync_WithValidContent_StoresInDatabase()
    {
        // Arrange
        var filePath = "test.txt";
        var content = "This is a test document with some content to be indexed.";
        var metadata = new Dictionary<string, object> { ["author"] = "test", ["category"] = "testing" };

        // Act
        await _vectorStore.IndexDocumentAsync(filePath, content, metadata);

        // Assert
        await Assert.That(_vectorStore.GetChunkCount()).IsGreaterThan(0);
        await Assert.That(_vectorStore.GetIndexedFiles()).Contains(filePath);
        
        // Verify data in database
        using var connection = new SqliteConnection($"Data Source={_testDbPath}");
        await connection.OpenAsync();
        
        var command = new SqliteCommand("SELECT COUNT(*) FROM document_chunks WHERE source_file = @file", connection);
        command.Parameters.AddWithValue("@file", filePath);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        
        await Assert.That(count).IsGreaterThan(0);
    }

    [Test]
    public async Task IndexDocumentAsync_WithExistingFile_UpdatesContent()
    {
        // Arrange
        var filePath = "update_test.txt";
        var originalContent = "Original content";
        var updatedContent = "Updated content with more information";

        // Act - Index original content
        await _vectorStore.IndexDocumentAsync(filePath, originalContent);
        var originalCount = _vectorStore.GetChunkCount();
        
        // Act - Update with new content
        await _vectorStore.IndexDocumentAsync(filePath, updatedContent);
        var updatedCount = _vectorStore.GetChunkCount();

        // Assert
        await Assert.That(_vectorStore.GetIndexedFiles()).Contains(filePath);
        await Assert.That(_vectorStore.GetIndexedFiles().Count).IsEqualTo(1); // Still only one file
        
        // Verify old chunks were replaced
        using var connection = new SqliteConnection($"Data Source={_testDbPath}");
        await connection.OpenAsync();
        
        var command = new SqliteCommand("SELECT content FROM document_chunks WHERE source_file = @file", connection);
        command.Parameters.AddWithValue("@file", filePath);
        
        using var reader = await command.ExecuteReaderAsync();
        var hasUpdatedContent = false;
        while (await reader.ReadAsync())
        {
            var content = reader.GetString(0); // content is the first column
            if (content.Contains("Updated"))
            {
                hasUpdatedContent = true;
                break;
            }
        }
        
        await Assert.That(hasUpdatedContent).IsTrue();
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
        
        // Verify chunks have correct indices
        using var connection = new SqliteConnection($"Data Source={_testDbPath}");
        await connection.OpenAsync();
        
        var command = new SqliteCommand("SELECT DISTINCT chunk_index FROM document_chunks WHERE source_file = @file ORDER BY chunk_index", connection);
        command.Parameters.AddWithValue("@file", filePath);
        
        var indices = new List<int>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indices.Add(reader.GetInt32(0)); // chunk_index is the first column
        }
        
        await Assert.That(indices).IsNotEmpty();
        await Assert.That(indices[0]).IsEqualTo(0); // First chunk should have index 0
        
        // Verify indices start from 0 and are consecutive
        var expectedIndices = Enumerable.Range(0, indices.Count).ToList();
        
        // Check each element individually (TUnit has issues with list equality)
        for (int i = 0; i < indices.Count; i++)
        {
            await Assert.That(indices[i]).IsEqualTo(expectedIndices[i]);
        }
    }

    [Test]
    public async Task IndexDocumentAsync_WithMetadata_StoresMetadataCorrectly()
    {
        // Arrange
        var filePath = "metadata_test.txt";
        var content = "Test content";
        var metadata = new Dictionary<string, object> 
        { 
            ["author"] = "John Doe",
            ["category"] = "documentation",
            ["version"] = 1.0,
            ["tags"] = new[] { "test", "sample" }
        };

        // Act
        await _vectorStore.IndexDocumentAsync(filePath, content, metadata);

        // Assert
        using var connection = new SqliteConnection($"Data Source={_testDbPath}");
        await connection.OpenAsync();
        
        var command = new SqliteCommand("SELECT metadata FROM document_chunks WHERE source_file = @file LIMIT 1", connection);
        command.Parameters.AddWithValue("@file", filePath);
        var metadataJson = await command.ExecuteScalarAsync() as string;
        
        await Assert.That(metadataJson).IsNotNull();
        await Assert.That(metadataJson).Contains("John Doe");
        await Assert.That(metadataJson).Contains("documentation");
    }

    [Test]
    public async Task SearchAsync_WithEmptyDatabase_ReturnsEmptyResults()
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
        await _vectorStore.IndexDocumentAsync(filePath, content);
        
        // Verify content was indexed
        var chunkCount = await _vectorStore.GetChunkCountAsync();
        await Assert.That(chunkCount).IsGreaterThan(0);
        
        var query = new RagQuery { Query = "test document", TopK = 5, MinSimilarity = 0.0f };

        // Override mock for this test to ensure high similarity
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(query.Query))
            .ReturnsAsync(_sampleEmbedding);

        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(_sampleEmbedding);

        // Act
        var results = await _vectorStore.SearchAsync(query);

        // Assert
        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.First().Chunk.SourceFile).IsEqualTo(filePath);
        await Assert.That(results.First().Similarity).IsGreaterThanOrEqualTo(0.0f);
        await Assert.That(results.First().Chunk.Content).Contains("test");
    }

    [Test]
    public async Task SearchAsync_WithFileFilters_FiltersCorrectly()
    {
        // Arrange - Override mock to return consistent embeddings for similar content
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync((string text) =>
            {
                // Return similar embeddings for content containing "content"
                if (text.ToLower().Contains("content"))
                {
                    return _sampleEmbedding;
                }
                return _differentEmbedding;
            });
        
        await _vectorStore.IndexDocumentAsync("file1.txt", "Content for file one");
        await _vectorStore.IndexDocumentAsync("file2.txt", "Content for file two");
        await _vectorStore.IndexDocumentAsync("other.txt", "Content for other file");
        
        // First test without file filters to ensure documents are indexed
        var queryWithoutFilters = new RagQuery 
        { 
            Query = "content", 
            TopK = 10, 
            MinSimilarity = 0.0f,
            FileFilters = []
        };
        
        var allResults = await _vectorStore.SearchAsync(queryWithoutFilters);
        await Assert.That(allResults).IsNotEmpty(); // This should pass if documents are indexed
        
        var query = new RagQuery 
        { 
            Query = "content", 
            TopK = 10, 
            MinSimilarity = 0.0f,
            FileFilters = ["file1", "file2"]
        };

        // Act
        var results = await _vectorStore.SearchAsync(query);

        // Assert
        await Assert.That(results).IsNotEmpty();
        await Assert.That(results.All(r => r.Chunk.SourceFile.Contains("file1") || r.Chunk.SourceFile.Contains("file2"))).IsTrue();
        await Assert.That(results.Any(r => r.Chunk.SourceFile.Contains("other"))).IsFalse();
    }

    [Test]
    public async Task SearchAsync_WithMinSimilarityFilter_FiltersLowSimilarity()
    {
        // Arrange
        var filePath = "similarity_test.txt";
        var content = "This is a test document.";
        await _vectorStore.IndexDocumentAsync(filePath, content);
        
        // Note: Using real EmbeddingService which will generate different embeddings for different queries
        
        var query = new RagQuery { Query = "completely different query", TopK = 5, MinSimilarity = 0.9f };

        // Override mock for this test to control similarity
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(query.Query))
            .ReturnsAsync(_differentEmbedding);

        // Assume content embedding is _sampleEmbedding for simplicity; adjust if needed
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(_sampleEmbedding);

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
            await _vectorStore.IndexDocumentAsync($"file{i}.txt", $"Content for file {i} with some additional text");
        }
        
        var query = new RagQuery { Query = "content", TopK = 3, MinSimilarity = 0.0f };

        // Act
        var results = await _vectorStore.SearchAsync(query);

        // Assert
        await Assert.That(results.Count).IsLessThanOrEqualTo(3);
    }

    [Test]
    public async Task SearchAsync_OrdersBySimilarity_ReturnsHighestFirst()
    {
        // Arrange - Override mock to return different embeddings for different similarity levels
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync((string text) =>
            {
                // Return high similarity for exact match content
                if (text.Contains("exactly what") || text.Contains("looking"))
                {
                    return _sampleEmbedding;
                }
                // Return medium similarity for somewhat related content
                else if (text.Contains("somewhat") || text.Contains("related"))
                {
                    return [0.1f, 0.2f, 0.3f, 0.3f, 0.4f]; // Similar but different
                }
                // Return low similarity for different content
                else
                {
                    return _differentEmbedding;
                }
            });
        
        await _vectorStore.IndexDocumentAsync("file1.txt", "This is exactly what we're looking for");
        await _vectorStore.IndexDocumentAsync("file2.txt", "This is somewhat related content");
        await _vectorStore.IndexDocumentAsync("file3.txt", "Completely different topic altogether");
        
        var query = new RagQuery { Query = "exactly what looking", TopK = 3, MinSimilarity = 0.0f };

        // Act
        var results = await _vectorStore.SearchAsync(query);

        // Assert
        await Assert.That(results).IsNotEmpty();
        
        // Results should be ordered by similarity (highest first)
        for (int i = 0; i < results.Count - 1; i++)
        {
            await Assert.That(results[i].Similarity).IsGreaterThanOrEqualTo(results[i + 1].Similarity);
        }
    }

    [Test]
    public async Task GetChunkCountAsync_WithIndexedContent_ReturnsCorrectCount()
    {
        // Arrange
        await _vectorStore.IndexDocumentAsync("count_test1.txt", "Short content");
        await _vectorStore.IndexDocumentAsync("count_test2.txt", "Another short content");

        // Act
        var count = await _vectorStore.GetChunkCountAsync();

        // Assert
        await Assert.That(count).IsEqualTo(2); // Two short documents should create 2 chunks
    }

    [Test]
    public async Task GetIndexedFilesAsync_WithMultipleFiles_ReturnsAllFiles()
    {
        // Arrange
        var files = new[] { "file1.txt", "file2.txt", "file3.txt" };
        foreach (var file in files)
        {
            await _vectorStore.IndexDocumentAsync(file, $"Content for {file}");
        }

        // Act
        var indexedFiles = await _vectorStore.GetIndexedFilesAsync();

        // Assert
        await Assert.That(indexedFiles.Count).IsEqualTo(files.Length);
        foreach (var file in files)
        {
            await Assert.That(indexedFiles).Contains(file);
        }
    }

    [Test]
    public async Task ClearIndexAsync_WithIndexedContent_ClearsDatabase()
    {
        // Arrange
        await _vectorStore.IndexDocumentAsync("clear_test1.txt", "Content to clear");
        await _vectorStore.IndexDocumentAsync("clear_test2.txt", "More content to clear");
        await Assert.That(_vectorStore.GetChunkCount()).IsGreaterThan(0);

        // Act
        await _vectorStore.ClearIndexAsync();

        // Assert
        await Assert.That(_vectorStore.GetChunkCount()).IsEqualTo(0);
        await Assert.That(_vectorStore.GetIndexedFiles()).IsEmpty();
        
        // Verify database is actually empty
        using var connection = new SqliteConnection($"Data Source={_testDbPath}");
        await connection.OpenAsync();
        
        var command = new SqliteCommand("SELECT COUNT(*) FROM document_chunks", connection);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_ClosesConnectionsProperly()
    {
        // Arrange
        await _vectorStore.IndexDocumentAsync("dispose_test.txt", "Content");
        await Assert.That(_vectorStore.GetChunkCount()).IsGreaterThan(0);

        // Act
        _vectorStore.Dispose();

        // Assert - File should still exist but connections should be closed
        await Assert.That(File.Exists(_testDbPath)).IsTrue();
        
        // Should be able to create a new connection to the same file
        using var connection = new SqliteConnection($"Data Source={_testDbPath}");
        await connection.OpenAsync();
        
        var command = new SqliteCommand("SELECT COUNT(*) FROM document_chunks", connection);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        
        await Assert.That(count).IsGreaterThan(0); // Data should still be there
    }

    [Test]
    public async Task IndexDocumentAsync_WithFileHashTracking_DetectsChanges()
    {
        // Arrange
        var filePath = "hash_test.txt";
        var content1 = "Original content";
        var content2 = "Modified content";

        // Act - Index original content
        await _vectorStore.IndexDocumentAsync(filePath, content1);
        
        // Verify original hash is stored
        using var connection = new SqliteConnection($"Data Source={_testDbPath}");
        await connection.OpenAsync();
        
        var command = new SqliteCommand("SELECT file_hash FROM document_chunks WHERE source_file = @file LIMIT 1", connection);
        command.Parameters.AddWithValue("@file", filePath);
        var originalHash = await command.ExecuteScalarAsync() as string;
        
        // Act - Index modified content
        await _vectorStore.IndexDocumentAsync(filePath, content2);
        
        // Verify hash was updated
        var newHash = await command.ExecuteScalarAsync() as string;
        
        // Assert
        await Assert.That(originalHash).IsNotNull();
        await Assert.That(newHash).IsNotNull();
        await Assert.That(originalHash).IsNotEqualTo(newHash);
    }

    [Test]
    public async Task IndexDocumentAsync_WithSameContent_SkipsReindexing()
    {
        // Arrange
        var filePath = "skip_test.txt";
        var content = "Same content";

        // Act - Index content twice
        await _vectorStore.IndexDocumentAsync(filePath, content);
        var firstIndexTime = DateTime.UtcNow;
        
        await Task.Delay(100); // Small delay to ensure different timestamps
        
        await _vectorStore.IndexDocumentAsync(filePath, content);

        // Assert - Should still have only one set of chunks
        using var connection = new SqliteConnection($"Data Source={_testDbPath}");
        await connection.OpenAsync();
        
        var command = new SqliteCommand("SELECT COUNT(*), MIN(indexed_at) FROM document_chunks WHERE source_file = @file", connection);
        command.Parameters.AddWithValue("@file", filePath);
        
        using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        
        var count = reader.GetInt32(0);
        var indexedAtStr = reader.GetString(1);
        var indexedAt = DateTime.Parse(indexedAtStr);
        
        await Assert.That(count).IsGreaterThan(0);
        await Assert.That(indexedAt).IsLessThan(firstIndexTime.AddSeconds(1)); // Should be from first indexing
    }
}