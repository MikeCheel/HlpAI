using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HlpAI.Models;

namespace HlpAI.Services
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly bool _disposeHttpClient;
        private readonly string _baseUrl;
        private readonly string _embeddingModel;
        private readonly ILogger? _logger;
        private readonly AppConfiguration? _config;
        private bool _disposed = false;

        // Constructor for dependency injection (used in tests)
        public EmbeddingService(HttpClient httpClient, string baseUrl = "http://localhost:11434", string embeddingModel = "nomic-embed-text", ILogger? logger = null, AppConfiguration? config = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _disposeHttpClient = false; // Don't dispose injected HttpClient
            _baseUrl = baseUrl.TrimEnd('/');
            _embeddingModel = embeddingModel;
            _logger = logger;
            _config = config;
        }

        // Original constructor for backward compatibility
        public EmbeddingService(string baseUrl = "http://localhost:11434", string embeddingModel = "nomic-embed-text", ILogger? logger = null, AppConfiguration? config = null)
        {
            var timeoutMinutes = config?.EmbeddingTimeoutMinutes ?? 10;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(timeoutMinutes)
            };
            _disposeHttpClient = true; // Dispose our own HttpClient
            _baseUrl = baseUrl.TrimEnd('/');
            _embeddingModel = embeddingModel;
            _logger = logger;
            _config = config;
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EmbeddingService));
                
            try
            {
                // First check if the embedding model is available
                var modelsResponse = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
                if (modelsResponse.IsSuccessStatusCode)
                {
                    var modelsContent = await modelsResponse.Content.ReadAsStringAsync();
                    var modelsJson = JsonSerializer.Deserialize<JsonElement>(modelsContent);

                    bool modelAvailable = false;
                    if (modelsJson.TryGetProperty("models", out var modelsArray))
                    {
                        modelAvailable = modelsArray.EnumerateArray()
                            .Any(m => m.TryGetProperty("name", out var name) &&
                                     name.GetString()?.Contains(_embeddingModel) == true);
                    }

                    if (!modelAvailable)
                    {
                        _logger?.LogWarning("Embedding model '{Model}' not found. Available models: {Models}",
                            _embeddingModel, string.Join(", ", modelsArray.EnumerateArray()
                                .Where(m => m.TryGetProperty("name", out _))
                                .Select(m => m.GetProperty("name").GetString())));
                        return GenerateSimpleEmbedding(text);
                    }
                }

                var request = new
                {
                    model = _embeddingModel,
                    prompt = text
                };

                var jsonContent = JsonSerializer.Serialize(request);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/embeddings", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogWarning("Embedding API error: {StatusCode} - {Content}", response.StatusCode, errorContent);

                    // Try to pull the model if it's not found
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        _logger?.LogInformation("Attempting to pull embedding model: {Model}", _embeddingModel);
                        await TryPullModelAsync(_embeddingModel);
                    }

                    return GenerateSimpleEmbedding(text);
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var embeddingResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (embeddingResponse.TryGetProperty("embedding", out var embeddingArray))
                {
                    return [.. embeddingArray.EnumerateArray().Select(e => e.GetSingle())];
                }

                return GenerateSimpleEmbedding(text);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error getting embedding for text");
                return GenerateSimpleEmbedding(text);
            }
        }

        private async Task TryPullModelAsync(string modelName)
        {
            try
            {
                var pullRequest = new { name = modelName };
                var jsonContent = JsonSerializer.Serialize(pullRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Properly await the async operation
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/pull", content);
                _logger?.LogInformation("Started pulling model: {Model} - Status: {StatusCode}", modelName, response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to pull model: {Model}", modelName);
            }
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

        public static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length) return 0f;

            float dotProduct = 0f;
            float normA = 0f;
            float normB = 0f;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA is 0f || normB is 0f) return 0f;

            return dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (_disposeHttpClient)
                {
                    _httpClient?.Dispose();
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