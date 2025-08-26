using System.Text.Json.Serialization;
using HlpAI.Services;

namespace HlpAI.Models;

/// <summary>
/// Application configuration settings that persist between sessions
/// </summary>
public class AppConfiguration
{
    /// <summary>
    /// The last used directory path
    /// </summary>
    public string? LastDirectory { get; set; }

    /// <summary>
    /// Whether to remember and suggest the last directory on startup
    /// </summary>
    public bool RememberLastDirectory { get; set; } = true;

    /// <summary>
    /// The last used AI provider
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AiProviderType LastProvider { get; set; } = AiProviderType.Ollama;

    /// <summary>
    /// The last used AI model
    /// </summary>
    public string? LastModel { get; set; }

    /// <summary>
    /// Whether to remember and suggest the last model on startup
    /// </summary>
    public bool RememberLastModel { get; set; } = true;

    /// <summary>
    /// Whether to remember and suggest the last provider on startup
    /// </summary>
    public bool RememberLastProvider { get; set; } = true;

    /// <summary>
    /// Base URL for Ollama provider
    /// </summary>
    public string? OllamaUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Base URL for LM Studio provider
    /// </summary>
    public string? LmStudioUrl { get; set; } = "http://localhost:1234";

    /// <summary>
    /// Base URL for Open Web UI provider
    /// </summary>
    public string? OpenWebUiUrl { get; set; } = "http://localhost:3000";

    /// <summary>
    /// Default model for Ollama provider
    /// </summary>
    public string OllamaDefaultModel { get; set; } = "llama3.2";

    /// <summary>
    /// Default model for LM Studio provider
    /// </summary>
    public string LmStudioDefaultModel { get; set; } = "default";

    /// <summary>
    /// Default model for Open Web UI provider
    /// </summary>
    public string OpenWebUiDefaultModel { get; set; } = "default";

    /// <summary>
    /// Base URL for OpenAI provider
    /// </summary>
    public string? OpenAiUrl { get; set; } = "https://api.openai.com";

    /// <summary>
    /// Base URL for Anthropic provider
    /// </summary>
    public string? AnthropicUrl { get; set; } = "https://api.anthropic.com";

    /// <summary>
    /// Base URL for DeepSeek provider
    /// </summary>
    public string? DeepSeekUrl { get; set; } = "https://api.deepseek.com";

    /// <summary>
    /// Default model for OpenAI provider
    /// </summary>
    public string OpenAiDefaultModel { get; set; } = "gpt-3.5-turbo";

    /// <summary>
    /// Default model for Anthropic provider
    /// </summary>
    public string AnthropicDefaultModel { get; set; } = "claude-3-haiku-20240307";

    /// <summary>
    /// Default model for DeepSeek provider
    /// </summary>
    public string DeepSeekDefaultModel { get; set; } = "deepseek-chat";

    /// <summary>
    /// Whether to store API keys securely using Windows DPAPI
    /// </summary>
    public bool UseSecureApiKeyStorage { get; set; } = true;

    /// <summary>
    /// Whether to validate API keys on startup
    /// </summary>
    public bool ValidateApiKeysOnStartup { get; set; } = true;

    /// <summary>
    /// The last used operation mode
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OperationMode LastOperationMode { get; set; } = OperationMode.Hybrid;

    /// <summary>
    /// Whether to remember and suggest the last operation mode on startup
    /// </summary>
    public bool RememberLastOperationMode { get; set; } = true;

    /// <summary>
    /// The path to the hh.exe executable for CHM file processing
    /// </summary>
    public string? HhExePath { get; set; }

    /// <summary>
    /// Whether to automatically detect hh.exe location if not configured
    /// </summary>
    public bool AutoDetectHhExe { get; set; } = true;

    /// <summary>
    /// When this configuration was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Version of the configuration format (for future migrations)
    /// </summary>
    public int ConfigVersion { get; set; } = 1;

    /// <summary>
    /// The current menu context for state management
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MenuContext CurrentMenuContext { get; set; } = MenuContext.MainMenu;

    /// <summary>
    /// Whether to remember and restore the last menu context on startup
    /// </summary>
    public bool RememberMenuContext { get; set; } = false;

    /// <summary>
    /// Stack of menu contexts for proper navigation history
    /// </summary>
    public List<MenuContext> MenuHistory { get; set; } = new();
}

/// <summary>
/// Enumeration of available menu contexts for state management
/// </summary>
public enum MenuContext
{
    MainMenu,
    Configuration,
    LogViewer,
    ExtractorManagement,
    AiProviderManagement,
    VectorDatabaseManagement,
    FileFilteringManagement
}
