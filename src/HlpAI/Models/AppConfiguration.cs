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
    public AiProviderType LastProvider { get; set; } = AiProviderType.None;

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
    public bool RememberLastProvider { get; set; }

    /// <summary>
    /// Base URL for Ollama provider - defaults will be seeded from database
    /// </summary>
    public string? OllamaUrl { get; set; }

    /// <summary>
    /// Base URL for LM Studio provider - defaults will be seeded from database
    /// </summary>
    public string? LmStudioUrl { get; set; }

    /// <summary>
    /// Base URL for Open Web UI provider - defaults will be seeded from database
    /// </summary>
    public string? OpenWebUiUrl { get; set; }

    /// <summary>
    /// Default model for Ollama provider - defaults will be seeded from database
    /// </summary>
    public string? OllamaDefaultModel { get; set; }

    /// <summary>
    /// Default model for LM Studio provider - defaults will be seeded from database
    /// </summary>
    public string? LmStudioDefaultModel { get; set; }

    /// <summary>
    /// Default model for Open Web UI provider - defaults will be seeded from database
    /// </summary>
    public string? OpenWebUiDefaultModel { get; set; }

    /// <summary>
    /// Base URL for OpenAI provider - defaults will be seeded from database
    /// </summary>
    public string? OpenAiUrl { get; set; }

    /// <summary>
    /// Base URL for Anthropic provider - defaults will be seeded from database
    /// </summary>
    public string? AnthropicUrl { get; set; }

    /// <summary>
    /// Base URL for DeepSeek provider - defaults will be seeded from database
    /// </summary>
    public string? DeepSeekUrl { get; set; }

    /// <summary>
    /// Default model for OpenAI provider - defaults will be seeded from database
    /// </summary>
    public string? OpenAiDefaultModel { get; set; }

    /// <summary>
    /// Default model for Anthropic provider - defaults will be seeded from database
    /// </summary>
    public string? AnthropicDefaultModel { get; set; }

    /// <summary>
    /// Default model for DeepSeek provider - defaults will be seeded from database
    /// </summary>
    public string? DeepSeekDefaultModel { get; set; }

    /// <summary>
    /// Timeout in minutes for AI provider requests - defaults will be seeded from database
    /// </summary>
    public int AiProviderTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Timeout in minutes for Ollama provider requests - defaults will be seeded from database
    /// </summary>
    public int OllamaTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Timeout in minutes for LM Studio provider requests - defaults will be seeded from database
    /// </summary>
    public int LmStudioTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Timeout in minutes for Open Web UI provider requests - defaults will be seeded from database
    /// </summary>
    public int OpenWebUiTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// The last used embedding model
    /// </summary>
    public string? LastEmbeddingModel { get; set; }

    /// <summary>
    /// Whether to remember and suggest the last embedding model on startup
    /// </summary>
    public bool RememberLastEmbeddingModel { get; set; }

    /// <summary>
    /// Base URL for embedding service - defaults will be seeded from database
    /// </summary>
    public string? EmbeddingServiceUrl { get; set; }

    /// <summary>
    /// Default embedding model for Ollama provider - defaults will be seeded from database
    /// </summary>
    public string? DefaultEmbeddingModel { get; set; }

    /// <summary>
    /// Timeout in minutes for embedding service requests - defaults will be seeded from database
    /// </summary>
    public int EmbeddingTimeoutMinutes { get; set; } = 1;

    /// <summary>
    /// Timeout in minutes for OpenAI provider requests - defaults will be seeded from database
    /// </summary>
    public int OpenAiTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Timeout in minutes for Anthropic provider requests - defaults will be seeded from database
    /// </summary>
    public int AnthropicTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Timeout in minutes for DeepSeek provider requests - defaults will be seeded from database
    /// </summary>
    public int DeepSeekTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum tokens for OpenAI provider requests - defaults will be seeded from database
    /// </summary>
    public int OpenAiMaxTokens { get; set; } = 4000;

    /// <summary>
    /// Maximum tokens for Anthropic provider requests - defaults will be seeded from database
    /// </summary>
    public int AnthropicMaxTokens { get; set; } = 4000;

    /// <summary>
    /// Maximum tokens for DeepSeek provider requests - defaults will be seeded from database
    /// </summary>
    public int DeepSeekMaxTokens { get; set; } = 4000;

    /// <summary>
    /// Maximum tokens for LM Studio provider requests - defaults will be seeded from database
    /// </summary>
    public int LmStudioMaxTokens { get; set; } = 4096;

    /// <summary>
    /// Maximum tokens for Open Web UI provider requests - defaults will be seeded from database
    /// </summary>
    public int OpenWebUiMaxTokens { get; set; } = 4096;

    /// <summary>
    /// Whether to store API keys securely using Windows DPAPI - defaults will be seeded from database
    /// </summary>
    public bool UseSecureApiKeyStorage { get; set; } = true;

    /// <summary>
    /// Whether to validate API keys on startup - defaults will be seeded from database
    /// </summary>
    public bool ValidateApiKeysOnStartup { get; set; } = true;

    /// <summary>
    /// The last used operation mode - defaults will be seeded from database
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OperationMode LastOperationMode { get; set; } = OperationMode.Hybrid;

    /// <summary>
    /// Whether to remember and suggest the last operation mode on startup - defaults will be seeded from database
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
    /// Maximum request size in bytes for security middleware - defaults will be seeded from database
    /// </summary>
    public long MaxRequestSizeBytes { get; set; }

    /// <summary>
    /// Maximum content length in bytes for security middleware - defaults will be seeded from database
    /// </summary>
    public int MaxContentLengthBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Maximum file size in bytes for file audit operations - defaults will be seeded from database
    /// </summary>
    public long MaxFileAuditSizeBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Size of text chunks for vector store operations - defaults will be seeded from database
    /// </summary>
    public int ChunkSize { get; set; } = 1000;

    /// <summary>
    /// Overlap between text chunks for vector store operations - defaults will be seeded from database
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
    /// Maximum number of files to display in CHM extractor logs - defaults will be seeded from database
    /// </summary>
    public int MaxChmExtractorFilesDisplayed { get; set; }

    /// <summary>
    /// Maximum number of large files to display in audit reports - defaults will be seeded from database
    /// </summary>
    public int MaxLargeFilesDisplayed { get; set; }

    /// <summary>
    /// Maximum number of unsupported extension groups to display in audit reports - defaults will be seeded from database
    /// </summary>
    public int MaxUnsupportedExtensionGroupsDisplayed { get; set; }

    /// <summary>
    /// Maximum number of files to display per category in MCP server reports - defaults will be seeded from database
    /// </summary>
    public int MaxFilesPerCategoryDisplayed { get; set; }

    /// <summary>
    /// Maximum number of recent history items to display - defaults will be seeded from database
    /// </summary>
    public int MaxRecentHistoryDisplayed { get; set; }

    /// <summary>
    /// Maximum number of models to display in lists - defaults will be seeded from database
    /// </summary>
    public int MaxModelsDisplayed { get; set; }

    /// <summary>
    /// Maximum number of skipped files to display in reports - defaults will be seeded from database
    /// </summary>
    public int MaxSkippedFilesDisplayed { get; set; }

    /// <summary>
    /// Maximum number of failed files to display in operation reports - defaults will be seeded from database
    /// </summary>
    public int MaxOperationFailedFilesDisplayed { get; set; }

    // Encryption Configuration - defaults will be seeded from database
    /// <summary>
    /// AES key size in bits for encryption - defaults will be seeded from database
    /// </summary>
    public int EncryptionKeySize { get; set; }

    /// <summary>
    /// AES initialization vector size in bits - defaults will be seeded from database
    /// </summary>
    public int EncryptionIvSize { get; set; }

    /// <summary>
    /// Salt size in bytes for key derivation - defaults will be seeded from database
    /// </summary>
    public int EncryptionSaltSize { get; set; }

    /// <summary>
    /// Number of PBKDF2 iterations for key derivation - defaults will be seeded from database
    /// </summary>
    public int EncryptionPbkdf2Iterations { get; set; }

    /// <summary>
    /// When this configuration was last updated - defaults will be seeded from database
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Version of the configuration format (for future migrations) - defaults will be seeded from database
    /// </summary>
    public int ConfigVersion { get; set; } = 1;

    /// <summary>
    /// The current menu context for state management - defaults will be seeded from database
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MenuContext CurrentMenuContext { get; set; }

    /// <summary>
    /// Whether to remember and restore the last menu context on startup - defaults will be seeded from database
    /// </summary>
    public bool RememberMenuContext { get; set; }

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
