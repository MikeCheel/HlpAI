using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using HlpAI.Models;
using HlpAI.Services;

namespace HlpAI.VectorStores;

/// <summary>
/// Optimized SQLite vector store with MD5 checksum-based file change detection
/// for improved startup performance
/// </summary>
public class OptimizedSqliteVectorStore : IVectorStore, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IEmbeddingService _embeddingService;
    private readonly IFileChangeDetectionService _changeDetectionService;
    private readonly ILogger<OptimizedSqliteVectorStore>? _logger;
    private readonly AppConfiguration? _config;
    private bool _disposed;

    public OptimizedSqliteVectorStore(
        string connectionString, 
        IEmbeddingService embeddingService,
        IFileChangeDetectionService changeDetectionService,
        ILogger<OptimizedSqliteVectorStore>? logger = null)
    {
        _connection = new SqliteConnection(connectionString);
        _embeddingService = embeddingService;
        _changeDetectionService = changeDetectionService;
        _logger = logger;
        _config = null;
        
        _connection.Open();
        InitializeDatabase();
    }

    public OptimizedSqliteVectorStore(
        string connectionString, 
        IEmbeddingService embeddingService,
        IFileChangeDetectionService changeDetectionService,
        AppConfiguration config,
        ILogger<OptimizedSqliteVectorStore>? logger = null)
    {
        _connection = new SqliteConnection(connectionString);
        _embeddingService = embeddingService;
        _changeDetectionService = changeDetectionService;
        _logger = logger;
        _config = config;
        
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS document_chunks (
                id TEXT PRIMARY KEY,
                source_file TEXT NOT NULL,
                content TEXT NOT NULL,
                chunk_index INTEGER NOT NULL,
                embedding BLOB NOT NULL,
                metadata TEXT,
                indexed_at TEXT NOT NULL,
                file_hash TEXT NOT NULL,
                file_modified TEXT NOT NULL,
                file_size INTEGER NOT NULL
            );
            
            CREATE INDEX IF NOT EXISTS idx_source_file ON document_chunks(source_file);
            CREATE INDEX IF NOT EXISTS idx_file_hash ON document_chunks(file_hash);
            CREATE INDEX IF NOT EXISTS idx_file_modified ON document_chunks(file_modified);
            CREATE INDEX IF NOT EXISTS idx_indexed_at ON document_chunks(indexed_at);
            
            -- Table for storing file metadata for quick change detection
            CREATE TABLE IF NOT EXISTS file_metadata (
                file_path TEXT PRIMARY KEY,
                file_hash TEXT NOT NULL,
                file_size INTEGER NOT NULL,
                file_modified TEXT NOT NULL,
                last_indexed TEXT NOT NULL,
                chunk_count INTEGER NOT NULL
            );
            
            CREATE INDEX IF NOT EXISTS idx_file_metadata_hash ON file_metadata(file_hash);
            CREATE INDEX IF NOT EXISTS idx_file_metadata_modified ON file_metadata(file_modified);
        ";

        using var command = new SqliteCommand(createTableSql, _connection);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Optimized document indexing with MD5 checksum-based change detection
    /// </summary>
    public async Task IndexDocumentAsync(string filePath, string content, Dictionary<string, object>? metadata = null)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            
            // Use the file change detection service for optimized checking
            var storedMetadata = await GetStoredFileMetadataAsync(filePath);
            var hasChanged = await _changeDetectionService.HasFileChangedAsync(
                filePath, 
                storedMetadata?.Hash, 
                storedMetadata?.LastModified);

            if (!hasChanged && storedMetadata != null)
            {
                _logger?.LogInformation("File {FilePath} is already up to date (MD5 optimization), skipping", filePath);
                return;
            }

            _logger?.LogDebug("Processing file {FilePath} - detected changes or new file", filePath);

            // Compute file hash using the optimized service
            var fileHash = await _changeDetectionService.ComputeFileHashAsync(filePath);
            var lastModified = fileInfo.LastWriteTime;

            // Remove existing chunks for this file
            await RemoveFileChunksAsync(filePath);

            var config = _config ?? ConfigurationService.LoadConfiguration(_logger);
            var chunks = SplitIntoChunks(content, config.ChunkSize, config.ChunkOverlap);

            var insertSql = @"
                INSERT INTO document_chunks 
                (id, source_file, content, chunk_index, embedding, metadata, indexed_at, file_hash, file_modified, file_size)
                VALUES (@id, @source_file, @content, @chunk_index, @embedding, @metadata, @indexed_at, @file_hash, @file_modified, @file_size)
            ";

            using var transaction = await _connection.BeginTransactionAsync();

            try
            {
                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunkMetadata = metadata ?? [];
                    chunkMetadata["file_name"] = Path.GetFileName(filePath);
                    chunkMetadata["file_extension"] = Path.GetExtension(filePath);
                    chunkMetadata["chunk_count"] = chunks.Count;

                    var embedding = await _embeddingService.GetEmbeddingAsync(chunks[i]);
                    var embeddingBytes = EmbeddingToBytes(embedding);

                    using var command = new SqliteCommand(insertSql, _connection, (SqliteTransaction)transaction);
                    command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                    command.Parameters.AddWithValue("@source_file", filePath);
                    command.Parameters.AddWithValue("@content", chunks[i]);
                    command.Parameters.AddWithValue("@chunk_index", i);
                    command.Parameters.AddWithValue("@embedding", embeddingBytes);
                    command.Parameters.AddWithValue("@metadata", JsonSerializer.Serialize(chunkMetadata));
                    command.Parameters.AddWithValue("@indexed_at", DateTime.UtcNow.ToString("O"));
                    command.Parameters.AddWithValue("@file_hash", fileHash);
                    command.Parameters.AddWithValue("@file_modified", lastModified.ToString("O"));
                    command.Parameters.AddWithValue("@file_size", fileInfo.Length);

                    await command.ExecuteNonQueryAsync();
                }

                // Update file metadata table for quick future lookups
                await UpdateFileMetadataAsync(filePath, fileHash, fileInfo.Length, lastModified, chunks.Count, (SqliteTransaction)transaction);

                await transaction.CommitAsync();
                _logger?.LogInformation("Successfully indexed {FilePath} with {ChunkCount} chunks (hash: {Hash})", 
                    filePath, chunks.Count, fileHash[..8]);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error indexing document: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Get stored file metadata for quick change detection
    /// </summary>
    private async Task<FileMetadata?> GetStoredFileMetadataAsync(string filePath)
    {
        try
        {
            var sql = "SELECT file_hash, file_size, file_modified, last_indexed, chunk_count FROM file_metadata WHERE file_path = @file";
            using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@file", filePath);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var fileHash = reader.GetString(0); // file_hash
                var fileSize = reader.GetInt64(1); // file_size
                var fileModifiedStr = reader.GetString(2); // file_modified
                var lastIndexedStr = reader.GetString(3); // last_indexed
                var chunkCount = reader.GetInt32(4); // chunk_count
                
                return new FileMetadata
                {
                    FilePath = filePath,
                    Hash = fileHash,
                    Size = fileSize,
                    LastModified = DateTime.Parse(fileModifiedStr),
                    LastChecked = DateTime.Parse(lastIndexedStr)
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting stored file metadata: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Update file metadata table for quick future lookups
    /// </summary>
    private async Task UpdateFileMetadataAsync(string filePath, string fileHash, long fileSize, 
        DateTime lastModified, int chunkCount, SqliteTransaction transaction)
    {
        try
        {
            var sql = @"
                INSERT OR REPLACE INTO file_metadata 
                (file_path, file_hash, file_size, file_modified, last_indexed, chunk_count)
                VALUES (@file_path, @file_hash, @file_size, @file_modified, @last_indexed, @chunk_count)
            ";

            using var command = new SqliteCommand(sql, _connection, transaction);
            command.Parameters.AddWithValue("@file_path", filePath);
            command.Parameters.AddWithValue("@file_hash", fileHash);
            command.Parameters.AddWithValue("@file_size", fileSize);
            command.Parameters.AddWithValue("@file_modified", lastModified.ToString("O"));
            command.Parameters.AddWithValue("@last_indexed", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("@chunk_count", chunkCount);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating file metadata: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Batch check multiple files for changes using optimized detection
    /// </summary>
    public async Task<Dictionary<string, bool>> BatchCheckFilesForChangesAsync(IEnumerable<string> filePaths)
    {
        var storedMetadata = new Dictionary<string, FileMetadata>();
        
        // Get all stored metadata in one query
        var filePathsList = filePaths.ToList();
        if (filePathsList.Count > 0)
        {
            var placeholders = string.Join(",", filePathsList.Select((_, i) => $"@file{i}"));
            var sql = $"SELECT file_path, file_hash, file_size, file_modified, last_indexed FROM file_metadata WHERE file_path IN ({placeholders})";
            
            using var command = new SqliteCommand(sql, _connection);
            for (int i = 0; i < filePathsList.Count; i++)
            {
                command.Parameters.AddWithValue($"@file{i}", filePathsList[i]);
            }

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var filePath = reader.GetString(0); // file_path
                var fileHash = reader.GetString(1); // file_hash
                var fileSize = reader.GetInt64(2); // file_size
                var fileModifiedStr = reader.GetString(3); // file_modified
                var lastIndexedStr = reader.GetString(4); // last_indexed
                
                storedMetadata[filePath] = new FileMetadata
                {
                    FilePath = filePath,
                    Hash = fileHash,
                    Size = fileSize,
                    LastModified = DateTime.Parse(fileModifiedStr),
                    LastChecked = DateTime.Parse(lastIndexedStr)
                };
            }
        }

        // Use the change detection service for batch checking
        return await _changeDetectionService.BatchCheckFilesChangedAsync(filePaths, storedMetadata);
    }

    public async Task<List<SearchResult>> SearchAsync(RagQuery query)
    {
        try
        {
            var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query.Query);
            var queryBytes = EmbeddingToBytes(queryEmbedding);

            var sql = @"
                SELECT source_file, content, metadata, chunk_index, embedding,
                       (SELECT AVG((e1.value - e2.value) * (e1.value - e2.value)) 
                        FROM json_each(@query_embedding) e1 
                        JOIN json_each(embedding) e2 ON e1.key = e2.key) as distance
                FROM document_chunks
                ORDER BY distance ASC
                LIMIT @topK
            ";

            using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@query_embedding", JsonSerializer.Serialize(queryEmbedding));
            command.Parameters.AddWithValue("@topK", query.TopK);

            var results = new List<SearchResult>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var metadataString = reader.GetString(2); // metadata
                var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataString) ?? [];

                results.Add(new SearchResult
                {
                    Chunk = new DocumentChunk
                    {
                        SourceFile = reader.GetString(0), // source_file
                        Content = reader.GetString(1), // content
                        Embedding = BytesToEmbedding(reader.GetFieldValue<byte[]>(4)), // embedding
                        ChunkIndex = reader.GetInt32(3), // chunk_index
                        Metadata = metadata
                    },
                    Similarity = 1.0f - (float)reader.GetDouble(5) // distance
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error searching vector store with query: {Query}", query.Query);
            return new List<SearchResult>();
        }
    }

    public async Task<int> GetChunkCountAsync()
    {
        try
        {
            var sql = "SELECT COUNT(*) FROM document_chunks";
            using var command = new SqliteCommand(sql, _connection);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result ?? 0);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting chunk count");
            return 0;
        }
    }

    public async Task<List<string>> GetIndexedFilesAsync()
    {
        try
        {
            var sql = "SELECT DISTINCT source_file FROM document_chunks ORDER BY source_file";
            using var command = new SqliteCommand(sql, _connection);
            
            var files = new List<string>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                files.Add(reader.GetString(0));
            }
            
            return files;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting indexed files");
            return [];
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            using var transaction = await _connection.BeginTransactionAsync();
            
            var deleteChunksSql = "DELETE FROM document_chunks";
            using var deleteChunksCommand = new SqliteCommand(deleteChunksSql, _connection, (SqliteTransaction)transaction);
            await deleteChunksCommand.ExecuteNonQueryAsync();
            
            var deleteMetadataSql = "DELETE FROM file_metadata";
            using var deleteMetadataCommand = new SqliteCommand(deleteMetadataSql, _connection, (SqliteTransaction)transaction);
            await deleteMetadataCommand.ExecuteNonQueryAsync();
            
            await transaction.CommitAsync();
            _logger?.LogInformation("Vector store cleared successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing vector store");
            throw;
        }
    }

    private async Task RemoveFileChunksAsync(string filePath)
    {
        try
        {
            using var transaction = await _connection.BeginTransactionAsync();
            
            var deleteChunksSql = "DELETE FROM document_chunks WHERE source_file = @file";
            using var deleteChunksCommand = new SqliteCommand(deleteChunksSql, _connection, (SqliteTransaction)transaction);
            deleteChunksCommand.Parameters.AddWithValue("@file", filePath);
            await deleteChunksCommand.ExecuteNonQueryAsync();
            
            var deleteMetadataSql = "DELETE FROM file_metadata WHERE file_path = @file";
            using var deleteMetadataCommand = new SqliteCommand(deleteMetadataSql, _connection, (SqliteTransaction)transaction);
            deleteMetadataCommand.Parameters.AddWithValue("@file", filePath);
            await deleteMetadataCommand.ExecuteNonQueryAsync();
            
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing file chunks: {FilePath}", filePath);
        }
    }

    private static byte[] EmbeddingToBytes(float[] embedding)
    {
        var bytes = new byte[embedding.Length * 4];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BytesToEmbedding(byte[] bytes)
    {
        var embedding = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, embedding, 0, bytes.Length);
        return embedding;
    }

    private static List<string> SplitIntoChunks(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < words.Length; i += chunkSize - overlap)
        {
            var chunkWords = words.Skip(i).Take(chunkSize).ToArray();
            if (chunkWords.Length > 0)
            {
                chunks.Add(string.Join(' ', chunkWords));
            }

            if (i + chunkSize >= words.Length)
                break;
        }

        return chunks;
    }

    public int GetChunkCount()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM chunks";
        var result = command.ExecuteScalar();
        return Convert.ToInt32(result);
    }

    public List<string> GetIndexedFiles()
    {
        var files = new List<string>();
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT file_path FROM chunks";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            files.Add(reader.GetString(0));
        }
        return files;
    }

    public void ClearIndex()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM document_chunks";
        command.ExecuteNonQuery();
        
        // Also clear file metadata table
        using var metadataCommand = _connection.CreateCommand();
        metadataCommand.CommandText = "DELETE FROM file_metadata";
        metadataCommand.ExecuteNonQuery();
        
        // Clear file metadata as well
        _changeDetectionService.ClearCache();
        
        _logger?.LogInformation("Vector store index cleared");
    }

    public async Task ClearIndexAsync()
    {
        await Task.Run(() => ClearIndex());
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                _connection?.Close();
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing OptimizedSqliteVectorStore");
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}