using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using HlpAI.Models;
using HlpAI.Services;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HlpAI.VectorStores
{
    // SQLite Vector Store
    public class SqliteVectorStore : IVectorStore
    {
        private readonly SqliteConnection _connection;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger? _logger;
        private bool _disposed = false;

        public SqliteVectorStore(IEmbeddingService embeddingService, string dbPath = "vectors.db", ILogger? logger = null)
        {
            _embeddingService = embeddingService;
            _logger = logger;

            var connectionString = $"Data Source={dbPath}";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            InitializeDatabase();
            
            // Mark the database file as hidden to prevent accidental visibility
            try
            {
                if (File.Exists(dbPath))
                {
                    var attributes = File.GetAttributes(dbPath);
                    if (!attributes.HasFlag(FileAttributes.Hidden))
                    {
                        File.SetAttributes(dbPath, attributes | FileAttributes.Hidden);
                        _logger?.LogDebug("Marked vector database file as hidden: {DbPath}", dbPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to mark vector database file as hidden: {DbPath}", dbPath);
            }
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
                    file_modified TEXT NOT NULL
                );
                
                CREATE INDEX IF NOT EXISTS idx_source_file ON document_chunks(source_file);
                CREATE INDEX IF NOT EXISTS idx_file_hash ON document_chunks(file_hash);
                CREATE INDEX IF NOT EXISTS idx_indexed_at ON document_chunks(indexed_at);
            ";

            using var command = new SqliteCommand(createTableSql, _connection);
            command.ExecuteNonQuery();
        }

        public async Task IndexDocumentAsync(string filePath, string content, Dictionary<string, object>? metadata = null)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileHash = ComputeFileHash(content);
                var lastModified = fileInfo.LastWriteTime;

                // Check if file is already indexed and up to date
                if (await IsFileUpToDateAsync(filePath, fileHash, lastModified))
                {
                    _logger?.LogInformation("File {FilePath} is already up to date, skipping", filePath);
                    return;
                }

                // Remove existing chunks for this file
                await RemoveFileChunksAsync(filePath);

                var chunks = SplitIntoChunks(content, 1000, 200);

                var insertSql = @"
                    INSERT INTO document_chunks 
                    (id, source_file, content, chunk_index, embedding, metadata, indexed_at, file_hash, file_modified)
                    VALUES (@id, @source_file, @content, @chunk_index, @embedding, @metadata, @indexed_at, @file_hash, @file_modified)
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

                        await command.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                    _logger?.LogInformation("Indexed {ChunkCount} chunks from {FilePath}", chunks.Count, filePath);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error indexing document {FilePath}", filePath);
            }
        }

        public async Task<List<SearchResult>> SearchAsync(RagQuery query)
        {
            try
            {
                var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query.Query);
                var results = new List<SearchResult>();

                var sql = @"
                    SELECT id, source_file, content, chunk_index, embedding, metadata, indexed_at 
                    FROM document_chunks
                ";

                var parameters = new List<SqliteParameter>();

                if (query.FileFilters.Count > 0)
                {
                    var filterConditions = query.FileFilters.Select((_, i) => $"source_file LIKE @filter{i}").ToArray();
                    sql += " WHERE " + string.Join(" OR ", filterConditions);

                    for (int i = 0; i < query.FileFilters.Count; i++)
                    {
                        parameters.Add(new SqliteParameter($"@filter{i}", $"%{query.FileFilters[i]}%"));
                    }
                }

                using var command = new SqliteCommand(sql, _connection);
                foreach (var param in parameters)
                {
                    command.Parameters.Add(param);
                }

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var embeddingBytes = (byte[])reader["embedding"];
                    var embedding = BytesToEmbedding(embeddingBytes);

                    var similarity = EmbeddingService.CosineSimilarity(queryEmbedding, embedding);

                    if (similarity >= query.MinSimilarity)
                    {
                        var metadataJson = reader["metadata"].ToString();
                        var metadata = string.IsNullOrEmpty(metadataJson)
                            ? []
                            : JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson) ?? [];

                        var chunk = new DocumentChunk
                        {
                            Id = reader["id"].ToString() ?? Guid.NewGuid().ToString(),
                            SourceFile = reader["source_file"].ToString() ?? "",
                            Content = reader["content"].ToString() ?? "",
                            ChunkIndex = Convert.ToInt32(reader["chunk_index"]),
                            Embedding = embedding,
                            Metadata = metadata,
                            IndexedAt = DateTime.Parse(reader["indexed_at"].ToString() ?? DateTime.UtcNow.ToString("O"), CultureInfo.InvariantCulture)
                        };

                        results.Add(new SearchResult { Chunk = chunk, Similarity = similarity });
                    }
                }

                return [.. results
                    .OrderByDescending(r => r.Similarity)
                    .Take(query.TopK)];
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error searching vector store");
                return [];
            }
        }

        public async Task<int> GetChunkCountAsync()
        {
            try
            {
                var sql = "SELECT COUNT(*) FROM document_chunks";
                using var command = new SqliteCommand(sql, _connection);
                var count = await command.ExecuteScalarAsync();
                return Convert.ToInt32(count ?? 0);
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
                using var reader = await command.ExecuteReaderAsync();

                var files = new List<string>();
                while (await reader.ReadAsync())
                {
                    files.Add(reader["source_file"].ToString() ?? "");
                }

                return files;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting indexed files");
                return [];
            }
        }

        public async Task ClearIndexAsync()
        {
            try
            {
                var sql = "DELETE FROM document_chunks";
                using var command = new SqliteCommand(sql, _connection);
                await command.ExecuteNonQueryAsync();
                _logger?.LogInformation("Cleared all indexed documents");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error clearing index");
            }
        }

        // Synchronous wrapper methods for interface compatibility
        public int GetChunkCount()
        {
            return GetChunkCountAsync().GetAwaiter().GetResult();
        }

        public List<string> GetIndexedFiles()
        {
            return GetIndexedFilesAsync().GetAwaiter().GetResult();
        }

        public void ClearIndex()
        {
            ClearIndexAsync().GetAwaiter().GetResult();
        }

        // Private helper methods
        private async Task<bool> IsFileUpToDateAsync(string filePath, string fileHash, DateTime lastModified)
        {
            try
            {
                var sql = "SELECT COUNT(*) FROM document_chunks WHERE source_file = @file AND file_hash = @hash AND file_modified = @modified";
                using var command = new SqliteCommand(sql, _connection);
                command.Parameters.AddWithValue("@file", filePath);
                command.Parameters.AddWithValue("@hash", fileHash);
                command.Parameters.AddWithValue("@modified", lastModified.ToString("O"));

                var count = (long)(await command.ExecuteScalarAsync() ?? 0);
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error checking if file is up to date: {FilePath}", filePath);
                return false;
            }
        }

        private async Task RemoveFileChunksAsync(string filePath)
        {
            try
            {
                var sql = "DELETE FROM document_chunks WHERE source_file = @file";
                using var command = new SqliteCommand(sql, _connection);
                command.Parameters.AddWithValue("@file", filePath);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error removing file chunks: {FilePath}", filePath);
            }
        }

        private static string ComputeFileHash(string content)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(hash);
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
                    _logger?.LogError(ex, "Error disposing SqliteVectorStore");
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
}