using HlpAI.Models;
using TUnit.Assertions;

namespace HlpAI.Tests.Models;

public class RagModelsTests
{
    [Test]
    public async Task DocumentChunk_Constructor_SetsDefaults()
    {
        // Act
        var chunk = new DocumentChunk
        {
            SourceFile = "test.txt",
            Content = "test content",
            Embedding = [1.0f, 2.0f, 3.0f]
        };

        // Assert
        await Assert.That(chunk.Id).IsNotNull();
        await Assert.That(chunk.Id).IsNotEmpty();
        await Assert.That(chunk.SourceFile).IsEqualTo("test.txt");
        await Assert.That(chunk.Content).IsEqualTo("test content");
        await Assert.That(chunk.Embedding.Length).IsEqualTo(3);
        await Assert.That(chunk.Embedding[0]).IsEqualTo(1.0f);
        await Assert.That(chunk.Embedding[1]).IsEqualTo(2.0f);
        await Assert.That(chunk.Embedding[2]).IsEqualTo(3.0f);
        await Assert.That(chunk.Metadata).IsNotNull();
        await Assert.That(chunk.ChunkIndex).IsEqualTo(0);
        await Assert.That(chunk.IndexedAt <= DateTime.UtcNow).IsTrue();
        await Assert.That(chunk.IndexedAt > DateTime.UtcNow.AddMinutes(-1)).IsTrue();
    }

    [Test]
    public async Task DocumentChunk_Id_IsUnique()
    {
        // Act
        var chunk1 = new DocumentChunk
        {
            SourceFile = "test.txt",
            Content = "content",
            Embedding = [1.0f]
        };
        
        var chunk2 = new DocumentChunk
        {
            SourceFile = "test.txt",
            Content = "content",
            Embedding = [1.0f]
        };

        // Assert
        await Assert.That(chunk1.Id).IsNotEqualTo(chunk2.Id);
    }

    [Test]
    public async Task SearchResult_Properties_WorkCorrectly()
    {
        // Arrange
        var chunk = new DocumentChunk
        {
            SourceFile = "test.txt",
            Content = "test content",
            Embedding = [1.0f, 2.0f]
        };

        // Act
        var result = new SearchResult
        {
            Chunk = chunk,
            Similarity = 0.85f
        };

        // Assert
        await Assert.That(result.Chunk).IsEqualTo(chunk);
        await Assert.That(result.Similarity).IsEqualTo(0.85f);
    }

    [Test]
    public async Task RagQuery_Constructor_SetsDefaults()
    {
        // Act
        var query = new RagQuery { Query = "test query" };

        // Assert
        await Assert.That(query.Query).IsEqualTo("test query");
        await Assert.That(query.TopK).IsEqualTo(5);
        await Assert.That(query.MinSimilarity).IsEqualTo(0.1f);
        await Assert.That(query.FileFilters).IsNotNull();
        await Assert.That(query.FileFilters).IsEmpty();
    }

    [Test]
    public async Task RagQuery_Properties_CanBeModified()
    {
        // Act
        var query = new RagQuery
        {
            Query = "test query",
            TopK = 10,
            MinSimilarity = 0.5f,
            FileFilters = ["filter1", "filter2"]
        };

        // Assert
        await Assert.That(query.Query).IsEqualTo("test query");
        await Assert.That(query.TopK).IsEqualTo(10);
        await Assert.That(query.MinSimilarity).IsEqualTo(0.5f);
        await Assert.That(query.FileFilters.Count).IsEqualTo(2);
        await Assert.That(query.FileFilters[0]).IsEqualTo("filter1");
        await Assert.That(query.FileFilters[1]).IsEqualTo("filter2");
    }

    [Test]
    public async Task DocumentChunk_Metadata_CanBeModified()
    {
        // Arrange
        var chunk = new DocumentChunk
        {
            SourceFile = "test.txt",
            Content = "content",
            Embedding = [1.0f]
        };

        // Act
        chunk.Metadata["key1"] = "value1";
        chunk.Metadata["key2"] = 42;

        // Assert
        await Assert.That(chunk.Metadata.Count).IsEqualTo(2);
        await Assert.That(chunk.Metadata["key1"]).IsEqualTo("value1");
        await Assert.That(chunk.Metadata["key2"]).IsEqualTo(42);
    }

    [Test]
    public async Task DocumentChunk_ChunkIndex_CanBeSet()
    {
        // Arrange
        var chunk = new DocumentChunk
        {
            SourceFile = "test.txt",
            Content = "content",
            Embedding = [1.0f]
        };

        // Act
        chunk.ChunkIndex = 5;

        // Assert
        await Assert.That(chunk.ChunkIndex).IsEqualTo(5);
    }
}