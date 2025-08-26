using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HlpAI.Services;

/// <summary>
/// Anthropic Claude API provider implementation
/// </summary>
public class AnthropicProvider : ICloudAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _currentModel;
    private bool _disposed;

    public AnthropicProvider(string apiKey, string model = "claude-3-haiku-20240307", string? baseUrl = null, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
        
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model cannot be null or empty", nameof(model));

        _apiKey = apiKey;
        _currentModel = model;
        _baseUrl = baseUrl ?? "https://api.anthropic.com";
        _logger = logger;
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };
        
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    /// <summary>
    /// Constructor for testing with custom HttpClient
    /// </summary>
    public AnthropicProvider(string apiKey, string model, HttpClient httpClient, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
        
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model cannot be null or empty", nameof(model));

        _apiKey = apiKey;
        _currentModel = model;
        _baseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "https://api.anthropic.com";
        _logger = logger;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "HlpAI/1.0");
    }

    public AiProviderType ProviderType => AiProviderType.Anthropic;
    public string ProviderName => "Anthropic";
    public string DefaultModel => "claude-3-5-haiku-20241022";
    public string BaseUrl => _baseUrl;
    public string CurrentModel => _currentModel;
    public string ApiKey => _apiKey;

    public async Task<string> GenerateAsync(string prompt, string? context = null, double temperature = 0.7)
    {
        try
        {
            var messages = new List<object>();
            
            // Add user message
            messages.Add(new { role = "user", content = prompt });

            // Build request body with optional system parameter
            object requestBody;
            if (!string.IsNullOrEmpty(context))
            {
                requestBody = new
                {
                    model = _currentModel,
                    max_tokens = 4000,
                    temperature = Math.Max(0.0, Math.Min(1.0, temperature)),
                    system = context,
                    messages = messages
                };
            }
            else
            {
                requestBody = new
                {
                    model = _currentModel,
                    max_tokens = 4000,
                    temperature = Math.Max(0.0, Math.Min(1.0, temperature)),
                    messages = messages
                };
            }

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);

            _logger?.LogDebug("Sending request to Anthropic API with model {Model}", _currentModel);
            
            var response = await _httpClient.PostAsync("/v1/messages", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError("Anthropic API request failed with status {StatusCode}: {Content}", 
                    response.StatusCode, responseContent);
                return $"Error: Anthropic API returned {response.StatusCode}";
            }

            var responseJson = JsonDocument.Parse(responseContent);
            var content_array = responseJson.RootElement.GetProperty("content");
            
            if (content_array.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("No content returned from Anthropic API");
            }

            var text = content_array[0].GetProperty("text").GetString();
            return text ?? string.Empty;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error generating response from Anthropic");
            return "Could not connect to Anthropic. Please check your internet connection and try again.";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating response from Anthropic");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            // Anthropic doesn't have a simple health check endpoint
            // We'll try to make a minimal request to test connectivity
            var testBody = new
            {
                model = _currentModel,
                max_tokens = 1,
                messages = new[] { new { role = "user", content = "Hi" } }
            };

            var json = JsonSerializer.Serialize(testBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/v1/messages", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Anthropic availability check failed");
            return false;
        }
    }

    public Task<List<string>> GetModelsAsync()
    {
        try
        {
            // Anthropic doesn't provide a models endpoint in their public API
            // Return the known available models
            _logger?.LogDebug("Returning default Anthropic models (no public models API available)");
            return Task.FromResult(GetDefaultModels());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error fetching models from Anthropic");
            return Task.FromResult(GetDefaultModels());
        }
    }

    public async Task<bool> ValidateApiKeyAsync()
    {
        try
        {
            // Test with a minimal request
            var testBody = new
            {
                model = _currentModel,
                max_tokens = 1,
                messages = new[] { new { role = "user", content = "Test" } }
            };

            var json = JsonSerializer.Serialize(testBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/v1/messages", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Anthropic API key validation failed");
            return false;
        }
    }

    public Task<ApiUsageInfo?> GetUsageInfoAsync()
    {
        try
        {
            // Anthropic doesn't provide usage info through their standard API
            _logger?.LogDebug("Usage info not available through standard Anthropic API");
            return Task.FromResult<ApiUsageInfo?>(null);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to get usage info from Anthropic");
            return Task.FromResult<ApiUsageInfo?>(null);
        }
    }

    public async Task<RateLimitInfo?> GetRateLimitInfoAsync()
    {
        try
        {
            // Rate limit info might be in response headers
            var testBody = new
            {
                model = _currentModel,
                max_tokens = 1,
                messages = new[] { new { role = "user", content = "Test" } }
            };

            var json = JsonSerializer.Serialize(testBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/v1/messages", content);
            
            // Check for rate limit headers (these may vary)
            if (response.Headers.TryGetValues("anthropic-ratelimit-requests-limit", out var limitValues) &&
                response.Headers.TryGetValues("anthropic-ratelimit-requests-remaining", out var remainingValues))
            {
                if (int.TryParse(limitValues.FirstOrDefault(), out var limit) &&
                    int.TryParse(remainingValues.FirstOrDefault(), out var remaining))
                {
                    // Try to get token limits as well
                    var tokensPerMinute = 0;
                    var tokensRemaining = 0;
                    
                    if (response.Headers.TryGetValues("anthropic-ratelimit-tokens-limit", out var tokenLimitValues) &&
                        int.TryParse(tokenLimitValues.FirstOrDefault(), out var tokenLimit))
                    {
                        tokensPerMinute = tokenLimit;
                    }
                    
                    if (response.Headers.TryGetValues("anthropic-ratelimit-tokens-remaining", out var tokenRemainingValues) &&
                        int.TryParse(tokenRemainingValues.FirstOrDefault(), out var tokenRemainingValue))
                    {
                        tokensRemaining = tokenRemainingValue;
                    }
                    
                    return new RateLimitInfo(
                        RequestsPerMinute: limit,
                        RequestsRemaining: remaining,
                        TokensPerMinute: tokensPerMinute,
                        TokensRemaining: tokensRemaining,
                        ResetTime: DateTime.UtcNow.AddMinutes(1) // Approximate
                    );
                }
            }
            
            return null!;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to get rate limit info from Anthropic");
            return null!;
        }
    }

    private static List<string> GetDefaultModels()
    {
        return new List<string>
        {
            "claude-3-5-sonnet-20241022",
            "claude-3-5-sonnet-20240620",
            "claude-3-5-haiku-20241022",
            "claude-3-opus-20240229",
            "claude-3-sonnet-20240229",
            "claude-3-haiku-20240307",
            "claude-2.1",
            "claude-2.0",
            "claude-instant-1.2"
        };
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