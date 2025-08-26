using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using HlpAI.Models;

namespace HlpAI.Services;

/// <summary>
/// Open Web UI provider implementation
/// </summary>
public class OpenWebUiProvider : IAiProvider
{
    private const string DEFAULT_MODEL = "default";

    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly ILogger? _logger;
    private readonly AppConfiguration? _config;
    private bool _disposed = false;

    public AiProviderType ProviderType => AiProviderType.OpenWebUi;
    public string ProviderName => "Open Web UI";
    public string DefaultModel => DEFAULT_MODEL;
    public string BaseUrl => _baseUrl;
    public string CurrentModel => _model;

    // Constructor for dependency injection (used in tests)
    public OpenWebUiProvider(HttpClient httpClient, string baseUrl = "http://localhost:3000", string model = DEFAULT_MODEL, ILogger? logger = null, AppConfiguration? config = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _disposeHttpClient = false; // Don't dispose injected HttpClient
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _logger = logger;
        _config = config;
    }

    // Main constructor
    public OpenWebUiProvider(string baseUrl = "http://localhost:3000", string model = DEFAULT_MODEL, ILogger? logger = null, AppConfiguration? config = null)
    {
        var timeoutMinutes = config?.OpenWebUiTimeoutMinutes ?? 10;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(timeoutMinutes)
        };
        _disposeHttpClient = true; // Dispose our own HttpClient
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _logger = logger;
        _config = config;
    }

    public async Task<string> GenerateAsync(string prompt, string? context = null, double temperature = 0.7)
    {
        try
        {
            var messages = new List<object>();

            if (!string.IsNullOrEmpty(context))
            {
                messages.Add(new { role = "system", content = context });
            }

            messages.Add(new { role = "user", content = prompt });

            var request = new
            {
                model = _model,
                messages,
                temperature,
                max_tokens = _config?.OpenWebUiMaxTokens ?? 4096,
                stream = false
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger?.LogDebug("Sending request to Open Web UI: {BaseUrl}/api/chat", _baseUrl);

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/chat", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogError("Open Web UI API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return $"Error: Open Web UI API returned {response.StatusCode}";
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var openWebUiResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            // Open Web UI typically returns the response directly or in a message format
            if (openWebUiResponse.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var contentProperty))
            {
                return contentProperty.GetString() ?? "No response from Open Web UI";
            }

            // Fallback: try to get direct response
            if (openWebUiResponse.TryGetProperty("response", out var responseProperty))
            {
                return responseProperty.GetString() ?? "No response from Open Web UI";
            }

            return "Invalid response format from Open Web UI";
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Network error connecting to Open Web UI at {BaseUrl}", _baseUrl);
            return $"Error: Could not connect to Open Web UI at {_baseUrl}. Make sure Open Web UI is running.";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error calling Open Web UI");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/models");
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
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/models");
            if (!response.IsSuccessStatusCode)
                return [];

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonSerializer.Deserialize<JsonElement>(content);
            var models = new List<string>();

            if (jsonDoc.TryGetProperty("models", out var modelsArray))
            {
                foreach (var modelElement in modelsArray.EnumerateArray())
                {
                    if (modelElement.TryGetProperty("id", out var idProperty))
                    {
                        var modelId = idProperty.GetString();
                        if (modelId != null)
                        {
                            models.Add(modelId);
                        }
                    }
                }
            }

            return models;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error fetching Open Web UI models");
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
