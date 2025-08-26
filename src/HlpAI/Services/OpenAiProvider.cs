using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HlpAI.Services;

/// <summary>
/// OpenAI API provider implementation
/// </summary>
public class OpenAiProvider : ICloudAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _currentModel;
    private bool _disposed;

    public OpenAiProvider(string apiKey, string model = "gpt-3.5-turbo", string? baseUrl = null, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
        
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model cannot be null or empty", nameof(model));

        _apiKey = apiKey;
        _currentModel = model;
        _baseUrl = baseUrl ?? "https://api.openai.com";
        _logger = logger;
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };
        
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "HlpAI/1.0");
    }

    public AiProviderType ProviderType => AiProviderType.OpenAI;
    public string ProviderName => "OpenAI";
    public string DefaultModel => "gpt-3.5-turbo";
    public string BaseUrl => _baseUrl;
    public string CurrentModel => _currentModel;
    public string ApiKey => _apiKey;

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

            var requestBody = new
            {
                model = _currentModel,
                messages = messages,
                temperature = Math.Max(0.0, Math.Min(2.0, temperature)),
                max_tokens = 4000,
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger?.LogDebug("Sending request to OpenAI API with model {Model}", _currentModel);
            
            var response = await _httpClient.PostAsync("/v1/chat/completions", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError("OpenAI API request failed with status {StatusCode}: {Content}", 
                    response.StatusCode, responseContent);
                throw new HttpRequestException($"OpenAI API request failed: {response.StatusCode} - {responseContent}");
            }

            var responseJson = JsonDocument.Parse(responseContent);
            var choices = responseJson.RootElement.GetProperty("choices");
            
            if (choices.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("No response choices returned from OpenAI API");
            }

            var message = choices[0].GetProperty("message").GetProperty("content").GetString();
            return message ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating response from OpenAI");
            throw;
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/v1/models");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "OpenAI availability check failed");
            return false;
        }
    }

    public async Task<List<string>> GetModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/v1/models");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Failed to fetch models from OpenAI: {StatusCode}", response.StatusCode);
                return GetDefaultModels();
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);
            var data = json.RootElement.GetProperty("data");

            var models = new List<string>();
            foreach (var model in data.EnumerateArray())
            {
                var id = model.GetProperty("id").GetString();
                if (!string.IsNullOrEmpty(id) && IsValidChatModel(id))
                {
                    models.Add(id);
                }
            }

            return models.Any() ? models.OrderBy(m => m).ToList() : GetDefaultModels();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error fetching models from OpenAI");
            return GetDefaultModels();
        }
    }

    public async Task<bool> ValidateApiKeyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/v1/models");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "OpenAI API key validation failed");
            return false;
        }
    }

    public Task<ApiUsageInfo?> GetUsageInfoAsync()
    {
        try
        {
            // OpenAI doesn't provide usage info through a simple API endpoint
            // This would require the usage API which has different authentication
            _logger?.LogDebug("Usage info not available through standard OpenAI API");
            return Task.FromResult<ApiUsageInfo?>(null);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to get usage info from OpenAI");
            return Task.FromResult<ApiUsageInfo?>(null);
        }
    }

    public async Task<RateLimitInfo?> GetRateLimitInfoAsync()
    {
        try
        {
            // Rate limit info is typically returned in response headers
            // We'd need to make a request and check headers
            var response = await _httpClient.GetAsync("/v1/models");
            
            if (response.Headers.TryGetValues("x-ratelimit-limit-requests", out var limitValues) &&
                response.Headers.TryGetValues("x-ratelimit-remaining-requests", out var remainingValues))
            {
                if (int.TryParse(limitValues.FirstOrDefault(), out var limit) &&
                    int.TryParse(remainingValues.FirstOrDefault(), out var remaining))
                {
                    return new RateLimitInfo(
                        RequestsPerMinute: limit,
                        RequestsRemaining: remaining,
                        TokensPerMinute: 0, // Not provided in headers
                        TokensRemaining: 0, // Not provided in headers
                        ResetTime: DateTime.UtcNow.AddMinutes(1) // Approximate
                    );
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to get rate limit info from OpenAI");
            return null;
        }
    }

    private static List<string> GetDefaultModels()
    {
        return new List<string>
        {
            "gpt-4",
            "gpt-4-turbo",
            "gpt-4o",
            "gpt-4o-mini",
            "gpt-3.5-turbo",
            "gpt-3.5-turbo-16k"
        };
    }

    private static bool IsValidChatModel(string modelId)
    {
        // Filter to only include chat completion models
        var chatModels = new[] { "gpt-3.5", "gpt-4", "gpt-4o" };
        return chatModels.Any(prefix => modelId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}