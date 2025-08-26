namespace HlpAI.Services;

/// <summary>
/// Interface for AI providers (Ollama, LM Studio, Open Web UI)
/// </summary>
public interface IAiProvider : IDisposable
{
    /// <summary>
    /// Generate a response using the AI provider
    /// </summary>
    Task<string> GenerateAsync(string prompt, string? context = null, double temperature = 0.7);

    /// <summary>
    /// Check if the provider is available
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Get list of available models
    /// </summary>
    Task<List<string>> GetModelsAsync();

    /// <summary>
    /// Provider type identifier
    /// </summary>
    AiProviderType ProviderType { get; }

    /// <summary>
    /// Provider display name
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Default model for this provider
    /// </summary>
    string DefaultModel { get; }

    /// <summary>
    /// Base URL for the provider
    /// </summary>
    string BaseUrl { get; }

    /// <summary>
    /// Currently selected model
    /// </summary>
    string CurrentModel { get; }
}

/// <summary>
/// AI provider types
/// </summary>
public enum AiProviderType
{
    /// <summary>
    /// Ollama - Local model runner
    /// </summary>
    Ollama,

    /// <summary>
    /// LM Studio - Local API server with GUI
    /// </summary>
    LmStudio,

    /// <summary>
    /// Open Web UI - Web-based model management
    /// </summary>
    OpenWebUi,

    /// <summary>
    /// OpenAI - Cloud-based AI service (GPT models)
    /// </summary>
    OpenAI,

    /// <summary>
    /// Anthropic - Cloud-based AI service (Claude models)
    /// </summary>
    Anthropic,

    /// <summary>
    /// DeepSeek - Cloud-based AI service
    /// </summary>
    DeepSeek
}
