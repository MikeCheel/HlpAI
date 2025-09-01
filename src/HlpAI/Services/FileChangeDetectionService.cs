using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace HlpAI.Services;

/// <summary>
/// Service for detecting file changes using MD5 checksums and file metadata
/// to optimize startup performance by avoiding unnecessary file processing
/// </summary>
public interface IFileChangeDetectionService
{
    /// <summary>
    /// Check if a file has changed since last processing using MD5 checksum and metadata
    /// </summary>
    Task<bool> HasFileChangedAsync(string filePath, string? lastKnownHash = null, DateTime? lastKnownModified = null);
    
    /// <summary>
    /// Compute MD5 hash of a file without loading entire content into memory
    /// </summary>
    Task<string> ComputeFileHashAsync(string filePath);
    
    /// <summary>
    /// Get file metadata for change detection
    /// </summary>
    FileMetadata GetFileMetadata(string filePath);
    
    /// <summary>
    /// Batch check multiple files for changes
    /// </summary>
    Task<Dictionary<string, bool>> BatchCheckFilesChangedAsync(IEnumerable<string> filePaths, Dictionary<string, FileMetadata>? knownMetadata = null);
    
    /// <summary>
    /// Clear the metadata cache
    /// </summary>
    void ClearCache();
    
    /// <summary>
    /// Get cache statistics
    /// </summary>
    (int CachedFiles, long TotalCacheSize) GetCacheStats();
}

/// <summary>
/// File metadata for change detection
/// </summary>
public record FileMetadata
{
    public string FilePath { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime LastModified { get; init; }
    public string Hash { get; init; } = string.Empty;
    public DateTime LastChecked { get; init; }
}

/// <summary>
/// Implementation of file change detection service using MD5 checksums
/// </summary>
public class FileChangeDetectionService : IFileChangeDetectionService, IDisposable
{
    private readonly ILogger<FileChangeDetectionService>? _logger;
    private readonly Dictionary<string, FileMetadata> _metadataCache = new();
    private readonly object _cacheLock = new();

    public FileChangeDetectionService(ILogger<FileChangeDetectionService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if a file has changed since last processing
    /// Uses a multi-stage approach: size check -> modification time -> MD5 hash
    /// </summary>
    public async Task<bool> HasFileChangedAsync(string filePath, string? lastKnownHash = null, DateTime? lastKnownModified = null)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger?.LogWarning("File not found for change detection: {FilePath}", filePath);
                return true; // File doesn't exist, consider it changed
            }

            var fileInfo = new FileInfo(filePath);
            
            // Stage 1: Quick size and modification time check
            if (lastKnownModified.HasValue)
            {
                // If modification time is different, file has changed
                if (fileInfo.LastWriteTime != lastKnownModified.Value)
                {
                    _logger?.LogDebug("File modification time changed: {FilePath}", filePath);
                    return true;
                }
            }

            // Stage 2: Check cache first
            lock (_cacheLock)
            {
                if (_metadataCache.TryGetValue(filePath, out var cachedMetadata))
                {
                    // If size or modification time changed, file has changed
                    if (cachedMetadata.Size != fileInfo.Length || 
                        cachedMetadata.LastModified != fileInfo.LastWriteTime)
                    {
                        _logger?.LogDebug("File metadata changed (cached): {FilePath}", filePath);
                        return true;
                    }
                    
                    // If we have a known hash and it matches cached hash, file hasn't changed
                    if (!string.IsNullOrEmpty(lastKnownHash) && 
                        cachedMetadata.Hash.Equals(lastKnownHash, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogDebug("File unchanged (cached hash match): {FilePath}", filePath);
                        return false;
                    }
                }
            }

            // Stage 3: Compute MD5 hash if needed
            if (!string.IsNullOrEmpty(lastKnownHash))
            {
                var currentHash = await ComputeFileHashAsync(filePath);
                
                // Update cache
                var metadata = new FileMetadata
                {
                    FilePath = filePath,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    Hash = currentHash,
                    LastChecked = DateTime.UtcNow
                };
                
                lock (_cacheLock)
                {
                    _metadataCache[filePath] = metadata;
                }

                var hasChanged = !currentHash.Equals(lastKnownHash, StringComparison.OrdinalIgnoreCase);
                _logger?.LogDebug("File hash comparison for {FilePath}: {HasChanged}", filePath, hasChanged);
                return hasChanged;
            }

            // If no known hash provided, assume file has changed
            _logger?.LogDebug("No known hash provided for {FilePath}, assuming changed", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking if file has changed: {FilePath}", filePath);
            return true; // Default to assuming file has changed on error
        }
    }

    /// <summary>
    /// Compute MD5 hash of a file using streaming to avoid loading entire file into memory
    /// </summary>
    public async Task<string> ComputeFileHashAsync(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            using var md5 = MD5.Create();
            
            var hashBytes = await md5.ComputeHashAsync(stream);
            return Convert.ToHexString(hashBytes);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error computing file hash: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Get file metadata for change detection
    /// </summary>
    public FileMetadata GetFileMetadata(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            return new FileMetadata
            {
                FilePath = filePath,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                Hash = string.Empty, // Hash computed separately when needed
                LastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting file metadata: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Batch check multiple files for changes efficiently
    /// </summary>
    public async Task<Dictionary<string, bool>> BatchCheckFilesChangedAsync(
        IEnumerable<string> filePaths, 
        Dictionary<string, FileMetadata>? knownMetadata = null)
    {
        var results = new Dictionary<string, bool>();
        var tasks = new List<Task<(string filePath, bool hasChanged)>>();

        foreach (var filePath in filePaths)
        {
            var knownMeta = knownMetadata?.GetValueOrDefault(filePath);
            
            tasks.Add(Task.Run(async () =>
            {
                var hasChanged = await HasFileChangedAsync(
                    filePath, 
                    knownMeta?.Hash, 
                    knownMeta?.LastModified);
                return (filePath, hasChanged);
            }));
        }

        var completedTasks = await Task.WhenAll(tasks);
        
        foreach (var (filePath, hasChanged) in completedTasks)
        {
            results[filePath] = hasChanged;
        }

        _logger?.LogInformation("Batch checked {TotalFiles} files: {ChangedFiles} changed, {UnchangedFiles} unchanged",
            results.Count, results.Values.Count(x => x), results.Values.Count(x => !x));

        return results;
    }

    /// <summary>
    /// Clear the metadata cache
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _metadataCache.Clear();
        }
        _logger?.LogDebug("File metadata cache cleared");
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public (int CachedFiles, long TotalCacheSize) GetCacheStats()
    {
        lock (_cacheLock)
        {
            var totalSize = _metadataCache.Values.Sum(m => m.Size);
            return (_metadataCache.Count, totalSize);
        }
    }

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public void Dispose()
    {
        // FileChangeDetectionService doesn't hold any unmanaged resources
        // This implementation is provided to satisfy IDisposable interface
        // for use in using statements
        GC.SuppressFinalize(this);
    }
}