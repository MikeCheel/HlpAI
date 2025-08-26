using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using HlpAI.Models;

namespace HlpAI.Services;

/// <summary>
/// LM Studio provider implementation
/// </summary>
public class LmStudioProvider : IAiProvider
{
    private const string DEFAULT_MODEL = "default";

    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly ILogger? _logger;
    private readonly AppConfiguration? _config;
    private bool _disposed = false;

    public AiProviderType ProviderType => AiProviderType.LmStudio;
    public string ProviderName => "LM Studio";
    public string DefaultModel => DEFAULT_MODEL;
    public string BaseUrl => _baseUrl;
    public string CurrentModel => _model;

    // Constructor for dependency injection (used in tests)
    public LmStudioProvider(HttpClient httpClient, string baseUrl = "http://localhost:1234", string model = DEFAULT_MODEL, ILogger? logger = null, AppConfiguration? config = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _disposeHttpClient = false; // Don't dispose injected HttpClient
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _logger = logger;
        _config = config;
    }

    // Main constructor
    public LmStudioProvider(string baseUrl = "http://localhost:1234", string model = DEFAULT_MODEL, ILogger? logger = null, AppConfiguration? config = null)
    {
        var timeoutMinutes = config?.LmStudioTimeoutMinutes ?? 10;
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
                max_tokens = _config?.LmStudioMaxTokens ?? 4096,
                stream = false
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger?.LogDebug("Sending request to LM Studio: {BaseUrl}/v1/chat/completions", _baseUrl);

            var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogError("LM Studio API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return $"Error: LM Studio API returned {response.StatusCode}";
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var lmStudioResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            if (lmStudioResponse.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var contentProperty))
                {
                    return contentProperty.GetString() ?? "No response from LM Studio";
                }
            }

            return "Invalid response format from LM Studio";
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Network error connecting to LM Studio at {BaseUrl}", _baseUrl);
            return $"Error: Could not connect to LM Studio at {_baseUrl}. Make sure LM Studio is running.";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error calling LM Studio");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/v1/models");
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
            var response = await _httpClient.GetAsync($"{_baseUrl}/v1/models");
            if (!response.IsSuccessStatusCode)
                return [];

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonSerializer.Deserialize<JsonElement>(content);
            var models = new List<string>();

            if (jsonDoc.TryGetProperty("data", out var dataArray))
            {
                foreach (var modelElement in dataArray.EnumerateArray())
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
            _logger?.LogError(ex, "Error fetching LM Studio models");
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
