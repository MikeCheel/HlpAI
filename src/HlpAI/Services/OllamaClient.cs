using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace HlpAI.Services
{
    public class OllamaClient : IDisposable
    {
        private const int OLLAMA_TIMEOUT = 10;  //  Minutes

        private readonly HttpClient _httpClient;
        private readonly bool _disposeHttpClient;
        private readonly string _baseUrl;
        private readonly string _model;
        private readonly ILogger? _logger;
        private bool _disposed = false;

        // Constructor for dependency injection (used in tests)
        public OllamaClient(HttpClient httpClient, string baseUrl = "http://localhost:11434", string model = "llama3.2", ILogger? logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _disposeHttpClient = false; // Don't dispose injected HttpClient
            _baseUrl = baseUrl.TrimEnd('/');
            _model = model;
            _logger = logger;
        }

        // Original constructor for backward compatibility
        public OllamaClient(string baseUrl = "http://localhost:11434", string model = "llama3.2", ILogger? logger = null)
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(OLLAMA_TIMEOUT)
            };
            _disposeHttpClient = true; // Dispose our own HttpClient
            _baseUrl = baseUrl.TrimEnd('/');
            _model = model;
            _logger = logger;
        }

        public async Task<string> GenerateAsync(string prompt, string? context = null, double temperature = 0.7)
        {
            try
            {
                var fullPrompt = context != null
                    ? $"Context: {context}\n\nQuestion: {prompt}"
                    : prompt;

                var request = new
                {
                    model = _model,
                    prompt = fullPrompt,
                    stream = false,
                    options = new
                    {
                        temperature,
                        top_p = 0.9,
                        top_k = 40
                    }
                };

                var jsonContent = JsonSerializer.Serialize(request);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger?.LogDebug("Sending request to Ollama: {BaseUrl}/api/generate", _baseUrl);

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogError("Ollama API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                    return $"Error: Ollama API returned {response.StatusCode}";
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var ollamaResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (ollamaResponse.TryGetProperty("response", out var responseText))
                {
                    return responseText.GetString() ?? "No response from Ollama";
                }

                return "Invalid response format from Ollama";
            }
            catch (HttpRequestException ex)
            {
                _logger?.LogError(ex, "Network error connecting to Ollama at {BaseUrl}", _baseUrl);
                return $"Error: Could not connect to Ollama at {_baseUrl}. Make sure Ollama is running.";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error calling Ollama");
                return $"Error: {ex.Message}";
            }
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<string>> GetModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
                if (!response.IsSuccessStatusCode)
                    return [];

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonSerializer.Deserialize<JsonElement>(content);
                var models = new List<string>();

                if (jsonDoc.TryGetProperty("models", out var modelsArray))
                {
                    foreach (var modelElement in modelsArray.EnumerateArray())
                    {
                        if (modelElement.TryGetProperty("name", out var nameProperty))
                        {
                            var modelName = nameProperty.GetString();
                            if (modelName != null)
                            {
                                models.Add(modelName);
                            }
                        }
                    }
                }

                return models;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching Ollama models");
                return [];
            }
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