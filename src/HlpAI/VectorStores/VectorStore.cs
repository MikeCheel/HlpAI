using Microsoft.Extensions.Logging;
using HlpAI.Models;
using HlpAI.Services;
using SystemPath = System.IO.Path;

namespace HlpAI.VectorStores
{
    public class VectorStore : IVectorStore
    {
        private readonly List<DocumentChunk> _chunks = [];
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger? _logger;
        private readonly AppConfiguration? _config;
        private bool _disposed = false;

        public VectorStore(IEmbeddingService embeddingService, ILogger? logger = null)
        {
            _embeddingService = embeddingService;
            _logger = logger;
            _config = null;
        }

        public VectorStore(IEmbeddingService embeddingService, AppConfiguration config, ILogger? logger = null)
        {
            _embeddingService = embeddingService;
            _logger = logger;
            _config = config;
        }

        public async Task IndexDocumentAsync(string filePath, string content, Dictionary<string, object>? metadata = null)
        {
            try
            {
                var config = _config ?? ConfigurationService.LoadConfiguration(_logger);
                var chunks = SplitIntoChunks(content, config.ChunkSize, config.ChunkOverlap);

                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunk = new DocumentChunk
                    {
                        SourceFile = filePath,
                        Content = chunks[i],
                        ChunkIndex = i,
                        Metadata = metadata ?? [],
                        Embedding = []
                    };

                    chunk.Metadata["file_name"] = SystemPath.GetFileName(filePath);
                    chunk.Metadata["file_extension"] = SystemPath.GetExtension(filePath);
                    chunk.Metadata["chunk_count"] = chunks.Count;

                    chunk.Embedding = await _embeddingService.GetEmbeddingAsync(chunks[i]);

                    _chunks.Add(chunk);
                }

                _logger?.LogInformation("Indexed {ChunkCount} chunks from {FilePath}", chunks.Count, filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error indexing document {FilePath}", filePath);
            }
        }

        public async Task<List<SearchResult>> SearchAsync(RagQuery query)
        {
            if (_chunks.Count == 0)
                return [];

            try
            {
                var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query.Query);
                var results = new List<SearchResult>();

                foreach (var chunk in _chunks)
                {
                    if (query.FileFilters.Count > 0 && !query.FileFilters.Any(f =>
                        chunk.SourceFile.Contains(f, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var similarity = EmbeddingService.CosineSimilarity(queryEmbedding, chunk.Embedding);

                    if (similarity >= query.MinSimilarity)
                    {
                        results.Add(new SearchResult
                        {
                            Chunk = chunk,
                            Similarity = similarity
                        });
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

        public int GetChunkCount() => _chunks.Count;

        public List<string> GetIndexedFiles() => [.. _chunks.Select(c => c.SourceFile).Distinct()];

        public void ClearIndex() => _chunks.Clear();

        public Task<int> GetChunkCountAsync()
        {
            return Task.FromResult(_chunks.Count);
        }

        public Task<List<string>> GetIndexedFilesAsync()
        {
            var result = _chunks.Select(c => c.SourceFile).Distinct().ToList();
            return Task.FromResult(result);
        }

        public Task ClearIndexAsync()
        {
            _chunks.Clear();
            return Task.CompletedTask;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _chunks.Clear();
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