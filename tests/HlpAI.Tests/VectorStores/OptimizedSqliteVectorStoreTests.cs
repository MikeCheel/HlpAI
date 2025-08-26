using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using Moq;
using Xunit;
using HlpAI.VectorStores;
using HlpAI.Services;

namespace HlpAI.Tests.VectorStores;

public class OptimizedSqliteVectorStoreTests : IDisposable
{
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IFileChangeDetectionService> _mockChangeDetectionService;
    private readonly Mock<ILogger<OptimizedSqliteVectorStore>> _mockLogger;
    private readonly string _connectionString;
    private readonly string _testFilePath;
    private readonly string _testContent;
    private readonly float[] _testEmbedding;
    private OptimizedSqliteVectorStore? _vectorStore;

    public OptimizedSqliteVectorStoreTests()
    {
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockChangeDetectionService = new Mock<IFileChangeDetectionService>();
        _mockLogger = new Mock<ILogger<OptimizedSqliteVectorStore>>();
        _connectionString = "Data Source=:memory:";
        _testFilePath = "test-file.txt";
        _testContent = "This is test content for the vector store.";
        _testEmbedding = [0.1f, 0.2f, 0.3f, 0.4f, 0.5f];

        // Setup default mock behaviors
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(_testEmbedding);
    }

    private OptimizedSqliteVectorStore CreateVectorStore()
    {
        return new OptimizedSqliteVectorStore(
            _connectionString,
            _mockEmbeddingService.Object,
            _mockChangeDetectionService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task IndexDocumentAsync_NewFile_ShouldIndexSuccessfully()
    {
        // Arrange
        _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(_testFilePath, null, null))
            .ReturnsAsync(true);
        _mockChangeDetectionService.Setup(x => x.ComputeFileHashAsync(_testFilePath))
            .ReturnsAsync("test-hash-123");

        // Create a temporary file for testing
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, _testContent);

        try
        {
            using var vectorStore = CreateVectorStore();

            // Act
            await vectorStore.IndexDocumentAsync(tempFile, _testContent);

            // Assert
            var chunkCount = await vectorStore.GetChunkCountAsync();
            Assert.True(chunkCount > 0);

            var indexedFiles = await vectorStore.GetIndexedFilesAsync();
            Assert.Contains(tempFile, indexedFiles);

            _mockChangeDetectionService.Verify(x => x.HasFileChangedAsync(tempFile, null, null), Times.Once);
            _mockChangeDetectionService.Verify(x => x.ComputeFileHashAsync(tempFile), Times.Once);
            _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(It.IsAny<string>()), Times.AtLeastOnce);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task IndexDocumentAsync_UnchangedFile_ShouldSkipIndexing()
    {
        // Arrange
        var storedMetadata = new FileMetadata
        {
            FilePath = _testFilePath,
            Hash = "existing-hash",
            Size = 1000,
            LastModified = DateTime.UtcNow.AddHours(-1),
            LastChecked = DateTime.UtcNow
        };

        _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(_testFilePath, "existing-hash", storedMetadata.LastModified))
            .ReturnsAsync(false);

        // Create a temporary file for testing
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, _testContent);

        try
        {
            using var vectorStore = CreateVectorStore();

            // First, index the file
            _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(tempFile, null, null))
                .ReturnsAsync(true);
            _mockChangeDetectionService.Setup(x => x.ComputeFileHashAsync(tempFile))
                .ReturnsAsync("existing-hash");

            await vectorStore.IndexDocumentAsync(tempFile, _testContent);

            // Reset mocks for the second call
            _mockEmbeddingService.Reset();
            _mockChangeDetectionService.Reset();

            // Setup for unchanged file
            _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(tempFile, "existing-hash", It.IsAny<DateTime?>()))
                .ReturnsAsync(false);

            // Act - try to index the same file again
            await vectorStore.IndexDocumentAsync(tempFile, _testContent);

            // Assert
            _mockChangeDetectionService.Verify(x => x.HasFileChangedAsync(tempFile, "existing-hash", It.IsAny<DateTime?>()), Times.Once);
            _mockChangeDetectionService.Verify(x => x.ComputeFileHashAsync(It.IsAny<string>()), Times.Never);
            _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task IndexDocumentAsync_ChangedFile_ShouldReindex()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, _testContent);

        try
        {
            using var vectorStore = CreateVectorStore();

            // First indexing
            _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(tempFile, null, null))
                .ReturnsAsync(true);
            _mockChangeDetectionService.Setup(x => x.ComputeFileHashAsync(tempFile))
                .ReturnsAsync("hash-v1");

            await vectorStore.IndexDocumentAsync(tempFile, _testContent);
            var initialChunkCount = await vectorStore.GetChunkCountAsync();

            // Reset mocks
            _mockEmbeddingService.Reset();
            _mockChangeDetectionService.Reset();

            // Setup for changed file
            _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(tempFile, "hash-v1", It.IsAny<DateTime?>()))
                .ReturnsAsync(true);
            _mockChangeDetectionService.Setup(x => x.ComputeFileHashAsync(tempFile))
                .ReturnsAsync("hash-v2");
            _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
                .ReturnsAsync(_testEmbedding);

            // Act - index the changed file
            var newContent = _testContent + " Additional content.";
            await vectorStore.IndexDocumentAsync(tempFile, newContent);

            // Assert
            _mockChangeDetectionService.Verify(x => x.HasFileChangedAsync(tempFile, "hash-v1", It.IsAny<DateTime?>()), Times.Once);
            _mockChangeDetectionService.Verify(x => x.ComputeFileHashAsync(tempFile), Times.Once);
            _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(It.IsAny<string>()), Times.AtLeastOnce);

            var finalChunkCount = await vectorStore.GetChunkCountAsync();
            Assert.True(finalChunkCount >= initialChunkCount); // Should have at least the same number of chunks
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task BatchCheckFilesForChangesAsync_ShouldReturnCorrectResults()
    {
        // Arrange
        var filePaths = new[] { "file1.txt", "file2.txt", "file3.txt" };
        var storedMetadata = new Dictionary<string, FileMetadata>
        {
            ["file1.txt"] = new FileMetadata { FilePath = "file1.txt", Hash = "hash1" },
            ["file2.txt"] = new FileMetadata { FilePath = "file2.txt", Hash = "hash2" }
        };

        var expectedResults = new Dictionary<string, bool>
        {
            ["file1.txt"] = false, // unchanged
            ["file2.txt"] = true,  // changed
            ["file3.txt"] = true   // new file
        };

        _mockChangeDetectionService.Setup(x => x.BatchCheckFilesChangedAsync(filePaths, It.IsAny<Dictionary<string, FileMetadata>>()))
            .ReturnsAsync(expectedResults);

        using var vectorStore = CreateVectorStore();

        // Act
        var results = await vectorStore.BatchCheckFilesForChangesAsync(filePaths);

        // Assert
        Assert.Equal(expectedResults, results);
        _mockChangeDetectionService.Verify(x => x.BatchCheckFilesChangedAsync(filePaths, It.IsAny<Dictionary<string, FileMetadata>>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnResults()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, _testContent);

        try
        {
            _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(tempFile, null, null))
                .ReturnsAsync(true);
            _mockChangeDetectionService.Setup(x => x.ComputeFileHashAsync(tempFile))
                .ReturnsAsync("test-hash");

            using var vectorStore = CreateVectorStore();

            // Index a document first
            await vectorStore.IndexDocumentAsync(tempFile, _testContent);

            // Act
            var results = await vectorStore.SearchAsync("test query", 5);

            // Assert
            Assert.NotNull(results);
            var resultsList = results.ToList();
            Assert.NotEmpty(resultsList);
            Assert.All(resultsList, r => Assert.NotNull(r.Content));
            Assert.All(resultsList, r => Assert.NotNull(r.Source));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetChunkCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        using var vectorStore = CreateVectorStore();

        // Act
        var initialCount = await vectorStore.GetChunkCountAsync();

        // Assert
        Assert.Equal(0, initialCount);
    }

    [Fact]
    public async Task GetIndexedFilesAsync_ShouldReturnIndexedFiles()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, _testContent);

        try
        {
            _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(tempFile, null, null))
                .ReturnsAsync(true);
            _mockChangeDetectionService.Setup(x => x.ComputeFileHashAsync(tempFile))
                .ReturnsAsync("test-hash");

            using var vectorStore = CreateVectorStore();

            // Index a document
            await vectorStore.IndexDocumentAsync(tempFile, _testContent);

            // Act
            var indexedFiles = await vectorStore.GetIndexedFilesAsync();

            // Assert
            Assert.Contains(tempFile, indexedFiles);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllData()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, _testContent);

        try
        {
            _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(tempFile, null, null))
                .ReturnsAsync(true);
            _mockChangeDetectionService.Setup(x => x.ComputeFileHashAsync(tempFile))
                .ReturnsAsync("test-hash");

            using var vectorStore = CreateVectorStore();

            // Index a document
            await vectorStore.IndexDocumentAsync(tempFile, _testContent);
            var countBeforeClear = await vectorStore.GetChunkCountAsync();
            Assert.True(countBeforeClear > 0);

            // Act
            await vectorStore.ClearAsync();

            // Assert
            var countAfterClear = await vectorStore.GetChunkCountAsync();
            Assert.Equal(0, countAfterClear);

            var filesAfterClear = await vectorStore.GetIndexedFilesAsync();
            Assert.Empty(filesAfterClear);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task IndexDocumentAsync_WithMetadata_ShouldStoreMetadata()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, _testContent);
        var metadata = new Dictionary<string, object>
        {
            ["author"] = "Test Author",
            ["category"] = "Test Category"
        };

        try
        {
            _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(tempFile, null, null))
                .ReturnsAsync(true);
            _mockChangeDetectionService.Setup(x => x.ComputeFileHashAsync(tempFile))
                .ReturnsAsync("test-hash");

            using var vectorStore = CreateVectorStore();

            // Act
            await vectorStore.IndexDocumentAsync(tempFile, _testContent, metadata);

            // Assert
            var results = await vectorStore.SearchAsync("test", 1);
            var result = results.First();
            
            Assert.Contains("author", result.Metadata.Keys);
            Assert.Contains("category", result.Metadata.Keys);
            Assert.Contains("file_name", result.Metadata.Keys);
            Assert.Contains("file_extension", result.Metadata.Keys);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task IndexDocumentAsync_WithError_ShouldThrowException()
    {
        // Arrange
        _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        using var vectorStore = CreateVectorStore();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            vectorStore.IndexDocumentAsync(_testFilePath, _testContent));
    }

    [Fact]
    public void Constructor_ShouldInitializeDatabase()
    {
        // Act & Assert - Should not throw
        using var vectorStore = CreateVectorStore();
        Assert.NotNull(vectorStore);
    }

    [Fact]
    public async Task SearchAsync_WithError_ShouldThrowException()
    {
        // Arrange
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Embedding error"));

        using var vectorStore = CreateVectorStore();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            vectorStore.SearchAsync("test query"));
    }

    [Fact]
    public async Task GetChunkCountAsync_WithError_ShouldReturnZero()
    {
        // Arrange - Create a vector store with a closed connection to simulate error
        var vectorStore = CreateVectorStore();
        vectorStore.Dispose(); // Close the connection

        // Act
        var count = await vectorStore.GetChunkCountAsync();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetIndexedFilesAsync_WithError_ShouldReturnEmptyList()
    {
        // Arrange - Create a vector store with a closed connection to simulate error
        var vectorStore = CreateVectorStore();
        vectorStore.Dispose(); // Close the connection

        // Act
        var files = await vectorStore.GetIndexedFilesAsync();

        // Assert
        Assert.Empty(files);
    }

    public void Dispose()
    {
        _vectorStore?.Dispose();
    }
}