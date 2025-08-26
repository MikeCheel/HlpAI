using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using HlpAI.Services;

namespace HlpAI.Tests.Services;

public class FileChangeDetectionServiceTests : IDisposable
{
    private readonly Mock<ILogger<FileChangeDetectionService>> _mockLogger;
    private readonly FileChangeDetectionService _service;
    private readonly List<string> _tempFiles;

    public FileChangeDetectionServiceTests()
    {
        _mockLogger = new Mock<ILogger<FileChangeDetectionService>>();
        _service = new FileChangeDetectionService(_mockLogger.Object);
        _tempFiles = new List<string>();
    }

    private string CreateTempFile(string content)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, content);
        _tempFiles.Add(tempFile);
        return tempFile;
    }

    [Fact]
    public async Task ComputeFileHashAsync_ShouldReturnConsistentHash()
    {
        // Arrange
        var content = "This is test content for hash computation.";
        var tempFile = CreateTempFile(content);

        // Act
        var hash1 = await _service.ComputeFileHashAsync(tempFile);
        var hash2 = await _service.ComputeFileHashAsync(tempFile);

        // Assert
        Assert.NotNull(hash1);
        Assert.NotNull(hash2);
        Assert.Equal(hash1, hash2);
        Assert.Equal(32, hash1.Length); // MD5 hash should be 32 characters
    }

    [Fact]
    public async Task ComputeFileHashAsync_DifferentContent_ShouldReturnDifferentHashes()
    {
        // Arrange
        var content1 = "This is the first test content.";
        var content2 = "This is the second test content.";
        var tempFile1 = CreateTempFile(content1);
        var tempFile2 = CreateTempFile(content2);

        // Act
        var hash1 = await _service.ComputeFileHashAsync(tempFile1);
        var hash2 = await _service.ComputeFileHashAsync(tempFile2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public async Task ComputeFileHashAsync_NonExistentFile_ShouldThrowException()
    {
        // Arrange
        var nonExistentFile = "non-existent-file.txt";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => 
            _service.ComputeFileHashAsync(nonExistentFile));
    }

    [Fact]
    public async Task GetFileMetadataAsync_ShouldReturnCorrectMetadata()
    {
        // Arrange
        var content = "Test content for metadata.";
        var tempFile = CreateTempFile(content);
        var fileInfo = new FileInfo(tempFile);

        // Act
        var metadata = await _service.GetFileMetadataAsync(tempFile);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(tempFile, metadata.FilePath);
        Assert.Equal(fileInfo.Length, metadata.Size);
        Assert.Equal(fileInfo.LastWriteTime, metadata.LastModified);
        Assert.NotNull(metadata.Hash);
        Assert.Equal(32, metadata.Hash.Length); // MD5 hash length
    }

    [Fact]
    public async Task GetFileMetadataAsync_NonExistentFile_ShouldThrowException()
    {
        // Arrange
        var nonExistentFile = "non-existent-file.txt";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => 
            _service.GetFileMetadataAsync(nonExistentFile));
    }

    [Fact]
    public async Task HasFileChangedAsync_NewFile_ShouldReturnTrue()
    {
        // Arrange
        var tempFile = CreateTempFile("Test content");

        // Act
        var hasChanged = await _service.HasFileChangedAsync(tempFile, null, null);

        // Assert
        Assert.True(hasChanged);
    }

    [Fact]
    public async Task HasFileChangedAsync_UnchangedFile_ShouldReturnFalse()
    {
        // Arrange
        var content = "Test content for unchanged file.";
        var tempFile = CreateTempFile(content);
        var fileInfo = new FileInfo(tempFile);
        var hash = await _service.ComputeFileHashAsync(tempFile);

        // Act
        var hasChanged = await _service.HasFileChangedAsync(tempFile, hash, fileInfo.LastWriteTime);

        // Assert
        Assert.False(hasChanged);
    }

    [Fact]
    public async Task HasFileChangedAsync_ChangedContent_ShouldReturnTrue()
    {
        // Arrange
        var originalContent = "Original content";
        var tempFile = CreateTempFile(originalContent);
        var originalHash = await _service.ComputeFileHashAsync(tempFile);
        var originalModified = new FileInfo(tempFile).LastWriteTime;

        // Modify the file
        await Task.Delay(1100); // Ensure different timestamp
        await File.WriteAllTextAsync(tempFile, "Modified content");

        // Act
        var hasChanged = await _service.HasFileChangedAsync(tempFile, originalHash, originalModified);

        // Assert
        Assert.True(hasChanged);
    }

    [Fact]
    public async Task HasFileChangedAsync_SameContentDifferentTimestamp_ShouldReturnFalse()
    {
        // Arrange
        var content = "Test content";
        var tempFile = CreateTempFile(content);
        var originalHash = await _service.ComputeFileHashAsync(tempFile);
        var originalModified = new FileInfo(tempFile).LastWriteTime;

        // Touch the file to change timestamp but keep content same
        await Task.Delay(1100); // Ensure different timestamp
        File.SetLastWriteTime(tempFile, DateTime.Now);

        // Act
        var hasChanged = await _service.HasFileChangedAsync(tempFile, originalHash, originalModified);

        // Assert
        Assert.False(hasChanged); // Content hash is the same, so no change
    }

    [Fact]
    public async Task HasFileChangedAsync_DifferentHash_ShouldReturnTrue()
    {
        // Arrange
        var tempFile = CreateTempFile("Test content");
        var fileInfo = new FileInfo(tempFile);
        var differentHash = "different-hash-value-123456789012";

        // Act
        var hasChanged = await _service.HasFileChangedAsync(tempFile, differentHash, fileInfo.LastWriteTime);

        // Assert
        Assert.True(hasChanged);
    }

    [Fact]
    public async Task HasFileChangedAsync_NonExistentFile_ShouldReturnTrue()
    {
        // Arrange
        var nonExistentFile = "non-existent-file.txt";

        // Act
        var hasChanged = await _service.HasFileChangedAsync(nonExistentFile, "some-hash", DateTime.Now);

        // Assert
        Assert.True(hasChanged); // Non-existent files are considered changed
    }

    [Fact]
    public async Task BatchCheckFilesChangedAsync_ShouldReturnCorrectResults()
    {
        // Arrange
        var file1 = CreateTempFile("Content 1");
        var file2 = CreateTempFile("Content 2");
        var file3 = "non-existent-file.txt";
        
        var filePaths = new[] { file1, file2, file3 };
        
        var file1Hash = await _service.ComputeFileHashAsync(file1);
        var file1Modified = new FileInfo(file1).LastWriteTime;
        
        var storedMetadata = new Dictionary<string, FileMetadata>
        {
            [file1] = new FileMetadata
            {
                FilePath = file1,
                Hash = file1Hash,
                LastModified = file1Modified
            },
            [file2] = new FileMetadata
            {
                FilePath = file2,
                Hash = "different-hash-123456789012345",
                LastModified = DateTime.Now.AddHours(-1)
            }
        };

        // Act
        var results = await _service.BatchCheckFilesChangedAsync(filePaths, storedMetadata);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.False(results[file1]); // Unchanged
        Assert.True(results[file2]);  // Changed (different hash)
        Assert.True(results[file3]);  // New file (not in stored metadata)
    }

    [Fact]
    public async Task BatchCheckFilesChangedAsync_EmptyInput_ShouldReturnEmptyResult()
    {
        // Arrange
        var filePaths = Array.Empty<string>();
        var storedMetadata = new Dictionary<string, FileMetadata>();

        // Act
        var results = await _service.BatchCheckFilesChangedAsync(filePaths, storedMetadata);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task BatchCheckFilesChangedAsync_AllNewFiles_ShouldReturnAllTrue()
    {
        // Arrange
        var file1 = CreateTempFile("Content 1");
        var file2 = CreateTempFile("Content 2");
        var filePaths = new[] { file1, file2 };
        var storedMetadata = new Dictionary<string, FileMetadata>(); // Empty - all files are new

        // Act
        var results = await _service.BatchCheckFilesChangedAsync(filePaths, storedMetadata);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.True(results[file1]);
        Assert.True(results[file2]);
    }

    [Fact]
    public async Task BatchCheckFilesChangedAsync_AllUnchangedFiles_ShouldReturnAllFalse()
    {
        // Arrange
        var file1 = CreateTempFile("Content 1");
        var file2 = CreateTempFile("Content 2");
        var filePaths = new[] { file1, file2 };
        
        var file1Hash = await _service.ComputeFileHashAsync(file1);
        var file1Modified = new FileInfo(file1).LastWriteTime;
        var file2Hash = await _service.ComputeFileHashAsync(file2);
        var file2Modified = new FileInfo(file2).LastWriteTime;
        
        var storedMetadata = new Dictionary<string, FileMetadata>
        {
            [file1] = new FileMetadata
            {
                FilePath = file1,
                Hash = file1Hash,
                LastModified = file1Modified
            },
            [file2] = new FileMetadata
            {
                FilePath = file2,
                Hash = file2Hash,
                LastModified = file2Modified
            }
        };

        // Act
        var results = await _service.BatchCheckFilesChangedAsync(filePaths, storedMetadata);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.False(results[file1]);
        Assert.False(results[file2]);
    }

    [Fact]
    public async Task ComputeFileHashAsync_LargeFile_ShouldHandleEfficiently()
    {
        // Arrange
        var largeContent = new StringBuilder();
        for (int i = 0; i < 10000; i++)
        {
            largeContent.AppendLine($"This is line {i} of the large test file.");
        }
        var tempFile = CreateTempFile(largeContent.ToString());

        // Act
        var startTime = DateTime.UtcNow;
        var hash = await _service.ComputeFileHashAsync(tempFile);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.NotNull(hash);
        Assert.Equal(32, hash.Length);
        Assert.True(duration.TotalSeconds < 5, "Hash computation should be efficient even for large files");
    }

    [Fact]
    public async Task HasFileChangedAsync_QuickMetadataCheck_ShouldOptimizePerformance()
    {
        // Arrange
        var content = "Test content for performance check";
        var tempFile = CreateTempFile(content);
        var fileInfo = new FileInfo(tempFile);
        var correctHash = await _service.ComputeFileHashAsync(tempFile);

        // Act - Test with correct metadata (should be fast)
        var startTime = DateTime.UtcNow;
        var hasChanged = await _service.HasFileChangedAsync(tempFile, correctHash, fileInfo.LastWriteTime);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.False(hasChanged);
        Assert.True(duration.TotalMilliseconds < 100, "Metadata check should be very fast when file is unchanged");
    }

    [Theory]
    [InlineData("")]           // Empty file
    [InlineData("a")]          // Single character
    [InlineData("Hello World")] // Normal text
    [InlineData("Special chars: !@#$%^&*()_+-=[]{}|;':,.<>?")]  // Special characters
    public async Task ComputeFileHashAsync_VariousContent_ShouldProduceValidHashes(string content)
    {
        // Arrange
        var tempFile = CreateTempFile(content);

        // Act
        var hash = await _service.ComputeFileHashAsync(tempFile);

        // Assert
        Assert.NotNull(hash);
        Assert.Equal(32, hash.Length);
        Assert.Matches("^[a-f0-9]{32}$", hash); // Should be valid hex string
    }

    [Fact]
    public async Task GetFileMetadataAsync_ShouldIncludeLastCheckedTime()
    {
        // Arrange
        var tempFile = CreateTempFile("Test content");
        var beforeCall = DateTime.UtcNow;

        // Act
        var metadata = await _service.GetFileMetadataAsync(tempFile);
        var afterCall = DateTime.UtcNow;

        // Assert
        Assert.True(metadata.LastChecked >= beforeCall);
        Assert.True(metadata.LastChecked <= afterCall);
    }

    public void Dispose()
    {
        // Clean up temp files
        foreach (var tempFile in _tempFiles)
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        
        _service?.Dispose();
    }
}