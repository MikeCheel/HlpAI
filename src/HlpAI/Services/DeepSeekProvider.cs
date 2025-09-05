using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using HlpAI.Models;

namespace HlpAI.Services;

/// <summary>
/// DeepSeek API provider implementation
/// </summary>
public class DeepSeekProvider : ICloudAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _currentModel;
    private readonly AppConfiguration? _config;
    private bool _disposed;

    public DeepSeekProvider(string apiKey, string model = "deepseek-chat", string? baseUrl = null, ILogger? logger = null, AppConfiguration? config = null)
    {
        // Allow empty API key for local/testing scenarios - validation will occur during actual API calls
        // if (string.IsNullOrWhiteSpace(apiKey))
        //     throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
        
        if (string.IsNullOrWhiteSpace(model))
            model = AiProviderConstants.DefaultModels.DeepSeek;

        _apiKey = apiKey;
        _currentModel = model;
        _baseUrl = baseUrl ?? "https://api.deepseek.com";
        _logger = logger;
        _config = config;
        
        var timeoutMinutes = config?.DeepSeekTimeoutMinutes ?? 2; // Reduced from 5 to 2 minutes
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15), // Connection pooling
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 10 // Allow multiple concurrent connections
        })
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromMinutes(timeoutMinutes)
        };
        
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "HlpAI/1.0");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive"); // Keep connections alive
    }

    /// <summary>
    /// Constructor for testing with custom HttpClient
    /// </summary>
    public DeepSeekProvider(string apiKey, string model, HttpClient httpClient, ILogger? logger = null, AppConfiguration? config = null)
    {
        // Allow empty API key for local/testing scenarios - validation will occur during actual API calls
        // if (string.IsNullOrWhiteSpace(apiKey))
        //     throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
        
        if (string.IsNullOrWhiteSpace(model))
            model = AiProviderConstants.DefaultModels.DeepSeek;

        _apiKey = apiKey;
        _currentModel = model;
        _baseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "https://api.deepseek.com";
        _logger = logger;
        _config = config;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var timeoutMinutes = config?.DeepSeekTimeoutMinutes ?? 2; // Reduced from 5 to 2 minutes
        _httpClient.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "HlpAI/1.0");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive"); // Keep connections alive
    }

    public AiProviderType ProviderType => AiProviderType.DeepSeek;
    public string ProviderName => "DeepSeek";
    public string DefaultModel => "deepseek-chat";
    public string BaseUrl => _baseUrl;
    public string CurrentModel => _currentModel;
    public string ApiKey => _apiKey;
    public bool SupportsDynamicModelSelection => true;
    public bool SupportsEmbedding => false;

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

            // Optimize token limits for better performance
            var maxTokens = _config?.DeepSeekMaxTokens ?? 1000; // Reduced from 4000 to 1000 for faster responses
            
            var requestBody = new
            {
                model = _currentModel,
                messages = messages,
                temperature = Math.Max(0.0, Math.Min(2.0, temperature)),
                max_tokens = maxTokens,
                stream = false,
                top_p = 0.9, // Add top_p for more focused responses
                frequency_penalty = 0.1 // Slight penalty to reduce repetition
            };

            // Use optimized JSON serialization options
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            
            var json = JsonSerializer.Serialize(requestBody, jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger?.LogDebug("Sending request to DeepSeek API with model {Model}, max_tokens: {MaxTokens}", _currentModel, maxTokens);
            
            // Use ConfigureAwait(false) for better async performance
            var response = await _httpClient.PostAsync("/v1/chat/completions", content).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError("DeepSeek API request failed with status {StatusCode}: {Content}", 
                    response.StatusCode, responseContent);
                return $"Error: DeepSeek API returned {response.StatusCode} - {responseContent}";
            }

            var responseJson = JsonDocument.Parse(responseContent);
            var choices = responseJson.RootElement.GetProperty("choices");
            
            if (choices.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("No response choices returned from DeepSeek API");
            }

            var message = choices[0].GetProperty("message").GetProperty("content").GetString();
            return message ?? string.Empty;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error generating response from DeepSeek");
            return $"Error: Could not connect to DeepSeek - {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating response from DeepSeek");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        // If no API key is provided, DeepSeek is not available since it requires authentication
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger?.LogDebug("DeepSeek is not available: No API key provided");
            return false;
        }

        try
        {
            var response = await _httpClient.GetAsync("/v1/models");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "DeepSeek availability check failed");
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
                _logger?.LogWarning("Failed to fetch models from DeepSeek: {StatusCode}", response.StatusCode);
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
            _logger?.LogError(ex, "Error fetching models from DeepSeek");
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
            _logger?.LogDebug(ex, "DeepSeek API key validation failed");
            return false;
        }
    }

    public Task<ApiUsageInfo?> GetUsageInfoAsync()
    {
        try
        {
            // DeepSeek may not provide usage info through a simple API endpoint
            _logger?.LogDebug("Usage info not available through standard DeepSeek API");
            return Task.FromResult<ApiUsageInfo?>(null);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to get usage info from DeepSeek");
            return Task.FromResult<ApiUsageInfo?>(null);
        }
    }

    public async Task<RateLimitInfo?> GetRateLimitInfoAsync()
    {
        try
        {
            // Rate limit info is typically returned in response headers
            var response = await _httpClient.GetAsync("/v1/models");
            
            if (response.Headers.TryGetValues("x-ratelimit-limit-requests", out var limitValues) &&
                response.Headers.TryGetValues("x-ratelimit-remaining-requests", out var remainingValues))
            {
                if (int.TryParse(limitValues.FirstOrDefault(), out var limit) &&
                    int.TryParse(remainingValues.FirstOrDefault(), out var remaining))
                {
                    // Parse token limits if available
                    var tokensPerMinute = 0;
                    var tokensRemaining = 0;
                    
                    if (response.Headers.TryGetValues("x-ratelimit-limit-tokens", out var tokenLimitValues) &&
                        int.TryParse(tokenLimitValues.FirstOrDefault(), out var tokenLimit))
                    {
                        tokensPerMinute = tokenLimit;
                    }
                    
                    if (response.Headers.TryGetValues("x-ratelimit-remaining-tokens", out var tokenRemainingValues) &&
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
            
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to get rate limit info from DeepSeek");
            return null;
        }
    }

    private static List<string> GetDefaultModels()
    {
        return new List<string>
        {
            "deepseek-chat",
            "deepseek-coder",
            "deepseek-math",
            "deepseek-reasoner"
        };
    }

    private static bool IsValidChatModel(string modelId)
    {
        // Filter to only include chat/reasoning models
        var chatModels = new[] { "deepseek-chat", "deepseek-coder", "deepseek-math", "deepseek-reasoner" };
        return chatModels.Any(model => modelId.StartsWith(model, StringComparison.OrdinalIgnoreCase));
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