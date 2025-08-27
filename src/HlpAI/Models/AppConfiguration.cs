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
    /// Timeout in minutes for AI provider requests
    /// </summary>
    public int AiProviderTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Timeout in minutes for Ollama provider requests
    /// </summary>
    public int OllamaTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Timeout in minutes for LM Studio provider requests
    /// </summary>
    public int LmStudioTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Timeout in minutes for Open Web UI provider requests
    /// </summary>
    public int OpenWebUiTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Timeout in minutes for embedding service requests
    /// </summary>
    public int EmbeddingTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Timeout in minutes for OpenAI provider requests
    /// </summary>
    public int OpenAiTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Timeout in minutes for Anthropic provider requests
    /// </summary>
    public int AnthropicTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Timeout in minutes for DeepSeek provider requests
    /// </summary>
    public int DeepSeekTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum tokens for OpenAI provider requests
    /// </summary>
    public int OpenAiMaxTokens { get; set; } = 4000;

    /// <summary>
    /// Maximum tokens for Anthropic provider requests
    /// </summary>
    public int AnthropicMaxTokens { get; set; } = 4000;

    /// <summary>
    /// Maximum tokens for DeepSeek provider requests
    /// </summary>
    public int DeepSeekMaxTokens { get; set; } = 4000;

    /// <summary>
    /// Maximum tokens for LM Studio provider requests
    /// </summary>
    public int LmStudioMaxTokens { get; set; } = 4096;

    /// <summary>
    /// Maximum tokens for Open Web UI provider requests
    /// </summary>
    public int OpenWebUiMaxTokens { get; set; } = 4096;

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
    /// Maximum request size in bytes for security middleware (default: 10MB)
    /// </summary>
    public long MaxRequestSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Maximum content length in bytes for security middleware (default: 1MB)
    /// </summary>
    public int MaxContentLengthBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Maximum file size in bytes for file audit operations (default: 100MB)
    /// </summary>
    public long MaxFileAuditSizeBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Size of text chunks for vector store operations (default: 1000 characters)
    /// </summary>
    public int ChunkSize { get; set; } = 1000;

    /// <summary>
    /// Overlap between text chunks for vector store operations (default: 200 characters)
    /// </summary>
    public int ChunkOverlap { get; set; } = 200;

    /// <summary>
    /// Minimum API key length for validation (default: 20 characters)
    /// </summary>
    public int ApiKeyMinLength { get; set; } = 20;

    /// <summary>
    /// Maximum API key length for validation (default: 200 characters)
    /// </summary>
    public int ApiKeyMaxLength { get; set; } = 200;

    /// <summary>
    /// Maximum model name length for validation (default: 100 characters)
    /// </summary>
    public int ModelNameMaxLength { get; set; } = 100;

    /// <summary>
    /// Maximum provider name length for validation (default: 50 characters)
    /// </summary>
    public int ProviderNameMaxLength { get; set; } = 50;

    /// <summary>
    /// Maximum file path length for validation (default: 260 characters)
    /// </summary>
    public int FilePathMaxLength { get; set; } = 260;

    /// <summary>
    /// Maximum number of unsupported files to display in audit reports
    /// </summary>
    public int MaxUnsupportedFilesDisplayed { get; set; } = 10;

    /// <summary>
    /// Maximum number of recent logs to retrieve by default
    /// </summary>
    public int MaxRecentLogsDisplayed { get; set; } = 50;

    /// <summary>
    /// Maximum number of cleanup history records to display (default: 20)
    /// </summary>
    public int MaxCleanupHistoryRecords { get; set; } = 20;

    /// <summary>
    /// Maximum number of top violations to display in security audit reports (default: 10)
    /// </summary>
    public int MaxTopViolationsDisplayed { get; set; } = 10;

    /// <summary>
    /// Maximum number of failed files to display in indexing reports (default: 10)
    /// </summary>
    public int MaxFailedFilesDisplayed { get; set; } = 10;

    /// <summary>
    /// Maximum number of not-indexed files to display in reports (default: 20)
    /// </summary>
    public int MaxNotIndexedFilesDisplayed { get; set; } = 20;

    /// <summary>
    /// Maximum number of files to display in CHM extractor logs (default: 10)
    /// </summary>
    public int MaxChmExtractorFilesDisplayed { get; set; } = 10;

    /// <summary>
    /// Maximum number of large files to display in audit reports (default: 5)
    /// </summary>
    public int MaxLargeFilesDisplayed { get; set; } = 5;

    /// <summary>
    /// Maximum number of unsupported extension groups to display in audit reports (default: 3)
    /// </summary>
    public int MaxUnsupportedExtensionGroupsDisplayed { get; set; } = 3;

    /// <summary>
    /// Maximum number of files to display per category in MCP server reports (default: 5)
    /// </summary>
    public int MaxFilesPerCategoryDisplayed { get; set; } = 5;

    /// <summary>
    /// Maximum number of recent history items to display (default: 10)
    /// </summary>
    public int MaxRecentHistoryDisplayed { get; set; } = 10;

    /// <summary>
    /// Maximum number of models to display in lists (default: 5)
    /// </summary>
    public int MaxModelsDisplayed { get; set; } = 5;

    /// <summary>
    /// Maximum number of skipped files to display in reports (default: 10)
    /// </summary>
    public int MaxSkippedFilesDisplayed { get; set; } = 10;

    /// <summary>
    /// Maximum number of failed files to display in operation reports (default: 10)
    /// </summary>
    public int MaxOperationFailedFilesDisplayed { get; set; } = 10;

    // Encryption Configuration
    /// <summary>
    /// AES key size in bits for encryption (default: 256 for AES-256)
    /// </summary>
    public int EncryptionKeySize { get; set; } = 256;

    /// <summary>
    /// AES initialization vector size in bits (default: 128)
    /// </summary>
    public int EncryptionIvSize { get; set; } = 128;

    /// <summary>
    /// Salt size in bytes for key derivation (default: 32 for 256-bit salt)
    /// </summary>
    public int EncryptionSaltSize { get; set; } = 32;

    /// <summary>
    /// Number of PBKDF2 iterations for key derivation (default: 100000)
    /// </summary>
    public int EncryptionPbkdf2Iterations { get; set; } = 100000;

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
