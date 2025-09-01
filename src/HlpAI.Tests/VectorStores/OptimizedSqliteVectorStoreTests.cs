using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using Moq;
using TUnit.Core;
using HlpAI.Models;
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

    [Test]
    public async Task IndexDocumentAsync_NewFile_ShouldIndexSuccessfully()
    {
        // Create a temporary file for testing
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, _testContent);

        try
        {
            // Arrange - Set up mocks for the actual temp file path
            _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(tempFile, null, null))
                .ReturnsAsync(true);
            _mockChangeDetectionService.Setup(x => x.ComputeFileHashAsync(tempFile))
                .ReturnsAsync("test-hash-123");

            using var vectorStore = CreateVectorStore();

            // Act
            await vectorStore.IndexDocumentAsync(tempFile, _testContent);

            // Assert
            var chunkCount = await vectorStore.GetChunkCountAsync();
            await Assert.That(chunkCount > 0).IsTrue();

            var indexedFiles = await vectorStore.GetIndexedFilesAsync();
            await Assert.That(indexedFiles).Contains(tempFile);

            _mockChangeDetectionService.Verify(x => x.HasFileChangedAsync(tempFile, null, null), Times.Once);
            _mockChangeDetectionService.Verify(x => x.ComputeFileHashAsync(tempFile), Times.Once);
            _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(It.IsAny<string>()), Times.AtLeastOnce);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
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

    [Test]
    public async Task IndexDocumentAsync_ChangedFile_ShouldReindex()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var tempDbFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, _testContent);
        int initialChunkCount;

        try
        {
            // Use a file-based database so both instances can share data
            var sharedConnectionString = $"Data Source={tempDbFile}";
            
            // First indexing with first vector store instance
            using (var vectorStore1 = new OptimizedSqliteVectorStore(
                sharedConnectionString,
                _mockEmbeddingService.Object,
                _mockChangeDetectionService.Object,
                _mockLogger.Object))
            {
                _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(tempFile, null, null))
                    .ReturnsAsync(true);
                _mockChangeDetectionService.Setup(x => x.ComputeFileHashAsync(tempFile))
                    .ReturnsAsync("hash-v1");
                _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
                    .ReturnsAsync(_testEmbedding);

                await vectorStore1.IndexDocumentAsync(tempFile, _testContent);
                initialChunkCount = await vectorStore1.GetChunkCountAsync();
            }

            // Reset mocks
            _mockEmbeddingService.Reset();
            _mockChangeDetectionService.Reset();

            // Second indexing with new vector store instance using same database
            using (var vectorStore2 = new OptimizedSqliteVectorStore(
                sharedConnectionString,
                _mockEmbeddingService.Object,
                _mockChangeDetectionService.Object,
                _mockLogger.Object))
            {
                // Setup for changed file
                _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(tempFile, "hash-v1", It.IsAny<DateTime?>()))
                    .ReturnsAsync(true);
                _mockChangeDetectionService.Setup(x => x.ComputeFileHashAsync(tempFile))
                    .ReturnsAsync("hash-v2");
                _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
                    .ReturnsAsync(_testEmbedding);

                // Act - index the changed file
                var newContent = _testContent + " Additional content.";
                await vectorStore2.IndexDocumentAsync(tempFile, newContent);

                // Assert
                _mockChangeDetectionService.Verify(x => x.HasFileChangedAsync(tempFile, "hash-v1", It.IsAny<DateTime?>()), Times.Once);
                _mockChangeDetectionService.Verify(x => x.ComputeFileHashAsync(tempFile), Times.Once);
                _mockEmbeddingService.Verify(x => x.GetEmbeddingAsync(It.IsAny<string>()), Times.AtLeastOnce);

                var finalChunkCount = await vectorStore2.GetChunkCountAsync();
                 await Assert.That(finalChunkCount >= initialChunkCount).IsTrue(); // Should have at least the same number of chunks
             }
         }
        finally
        {
            // Clean up temp file
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch (Exception ex)
            {
                _mockLogger.Object?.LogWarning(ex, "Failed to delete temp file: {TempFile}", tempFile);
            }

            // Clean up database file with proper connection pool clearing
            if (File.Exists(tempDbFile))
            {
                // Force garbage collection to ensure connections are disposed
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                // Clear SQLite connection pools
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                
                // Wait for file handles to be released
                await Task.Delay(200);
                
                // Retry deletion with exponential backoff
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        File.Delete(tempDbFile);
                        break;
                    }
                    catch (IOException) when (i < 4)
                    {
                        await Task.Delay(100 * (i + 1));
                        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    }
                }
            }
        }
    }

    [Test]
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
        await Assert.That(results).IsEqualTo(expectedResults);
        _mockChangeDetectionService.Verify(x => x.BatchCheckFilesChangedAsync(filePaths, It.IsAny<Dictionary<string, FileMetadata>>()), Times.Once);
    }

    [Test]
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
            var results = await vectorStore.SearchAsync(new RagQuery { Query = "test query", TopK = 5 });

            // Assert
            await Assert.That(results).IsNotNull();
            var resultsList = results.ToList();
            await Assert.That(resultsList).IsNotEmpty();
            await Assert.That(resultsList.All(r => r.Chunk.Content != null)).IsTrue();
            await Assert.That(resultsList.All(r => r.Chunk.SourceFile != null)).IsTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task GetChunkCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        using var vectorStore = CreateVectorStore();

        // Act
        var initialCount = await vectorStore.GetChunkCountAsync();

        // Assert
        await Assert.That(initialCount).IsEqualTo(0);
    }

    [Test]
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
            await Assert.That(indexedFiles).Contains(tempFile);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
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
            await Assert.That(countBeforeClear > 0).IsTrue();

            // Act
            await vectorStore.ClearAsync();

            // Assert
            var countAfterClear = await vectorStore.GetChunkCountAsync();
            await Assert.That(countAfterClear).IsEqualTo(0);

            var filesAfterClear = await vectorStore.GetIndexedFilesAsync();
            await Assert.That(filesAfterClear).IsEmpty();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
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
            var results = await vectorStore.SearchAsync(new RagQuery { Query = "test", TopK = 1 });
            var result = results.First();
            
            await Assert.That(result.Chunk.Metadata.Keys).Contains("author");
            await Assert.That(result.Chunk.Metadata.Keys).Contains("category");
            await Assert.That(result.Chunk.Metadata.Keys).Contains("file_name");
            await Assert.That(result.Chunk.Metadata.Keys).Contains("file_extension");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task IndexDocumentAsync_WithError_ShouldThrowException()
    {
        // Arrange
        _mockChangeDetectionService.Setup(x => x.HasFileChangedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        using var vectorStore = CreateVectorStore();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => vectorStore.IndexDocumentAsync(_testFilePath, _testContent));
    }

    [Test]
    public async Task Constructor_ShouldInitializeDatabase()
    {
        // Act & Assert - Should not throw
        using var vectorStore = CreateVectorStore();
        await Assert.That(vectorStore).IsNotNull();
    }

    [Test]
    public async Task SearchAsync_WithError_ShouldThrowException()
    {
        // Arrange
        _mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Embedding error"));

        using var vectorStore = CreateVectorStore();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => vectorStore.SearchAsync(new RagQuery { Query = "test query" }));
    }

    [Test]
    public async Task GetChunkCountAsync_WithError_ShouldReturnZero()
    {
        // Arrange - Create a vector store with a closed connection to simulate error
        var vectorStore = CreateVectorStore();
        vectorStore.Dispose(); // Close the connection

        // Act
        var count = await vectorStore.GetChunkCountAsync();

        // Assert
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task GetIndexedFilesAsync_WithError_ShouldReturnEmptyList()
    {
        // Arrange - Create a vector store with a closed connection to simulate error
        var vectorStore = CreateVectorStore();
        vectorStore.Dispose(); // Close the connection

        // Act
        var files = await vectorStore.GetIndexedFilesAsync();

        // Assert
        await Assert.That(files).IsEmpty();
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}