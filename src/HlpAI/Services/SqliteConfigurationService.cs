using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using HlpAI.Models;
using System.Globalization;
using System.Linq;

namespace HlpAI.Services;

/// <summary>
/// SQLite-based configuration service for storing all application settings
/// </summary>
public class SqliteConfigurationService : IDisposable
{
    private readonly ILogger? _logger;
    private readonly SqliteConnection _connection = null!;
    private readonly string _dbPath;
    private bool _disposed = false;
    
    // Singleton pattern to prevent duplicate initialization logs
    private static SqliteConfigurationService? _instance;
    private static readonly object _lock = new object();
    private static int _referenceCount = 0;
    
    // Semaphore to ensure thread-safe database operations
    private static readonly SemaphoreSlim _dbSemaphore = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Gets a shared instance of SqliteConfigurationService to prevent duplicate initialization logs
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>A shared SqliteConfigurationService instance</returns>
    public static SqliteConfigurationService GetInstance(ILogger? logger = null)
    {
        lock (_lock)
        {
            if (_instance == null || _instance._disposed)
            {
                _instance = new SqliteConfigurationService(null, logger, true);
            }
            else
            {
                // Check if the connection is still valid
                try
                {
                    // First check if connection exists and is not disposed
                    if (_instance._connection?.State == System.Data.ConnectionState.Open)
                    {
                        SqliteCommand? testCommand = null;
                        try
                        {
                            testCommand = new SqliteCommand("SELECT 1", _instance._connection);
                            testCommand.ExecuteScalar();
                        }
                        finally
                        {
                            try
                            {
                                testCommand?.Dispose();
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                // Ignore disposal errors from SQLite command cleanup
                                // This can happen in concurrent scenarios with the singleton pattern
                            }
                        }
                    }
                    else
                    {
                        // Connection is closed or null, recreate the instance
                        throw new InvalidOperationException("Connection is not open");
                    }
                }
                catch (Exception ex) when (ex is SqliteException || ex is InvalidOperationException || ex is ObjectDisposedException)
                {
                    // Connection is invalid, recreate the instance
                    logger?.LogWarning("Connection is invalid, recreating SqliteConfigurationService instance: {Message}", ex.Message);
                    try
                    {
                        _instance.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        logger?.LogWarning("Error disposing invalid instance: {Message}", disposeEx.Message);
                    }
                    _instance = new SqliteConfigurationService(null, logger, true);
                }
            }
            _referenceCount++;
            return _instance;
        }
    }
    
    /// <summary>
    /// Sets a test instance with a custom database path for testing purposes
    /// </summary>
    /// <param name="dbPath">Custom database path for testing</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>A test SqliteConfigurationService instance</returns>
    public static SqliteConfigurationService SetTestInstance(string dbPath, ILogger? logger = null)
    {
        lock (_lock)
        {
            // Dispose existing instance if any
            if (_instance != null)
            {
                _instance.Dispose();
            }
            
            _instance = new SqliteConfigurationService(dbPath, logger, true);
            _referenceCount = 1;
            return _instance;
        }
    }
    
    /// <summary>
    /// Releases a reference to the shared instance
    /// </summary>
    public static void ReleaseInstance()
    {
        lock (_lock)
        {
            _referenceCount--;
            if (_referenceCount <= 0 && _instance != null)
            {
                _instance.Dispose();
                _instance = null;
                _referenceCount = 0;
            }
        }
    }

    public SqliteConfigurationService(ILogger? logger = null) : this(null, logger, false)
    {
    }
    
    public SqliteConfigurationService(string dbPath, ILogger? logger = null) : this(dbPath, logger, false)
    {
    }
    
    private SqliteConfigurationService(string? customDbPath, ILogger? logger, bool isSingleton)
    {
        _logger = logger;
        
        if (!string.IsNullOrEmpty(customDbPath))
        {
            // Use custom database path (for testing)
            _dbPath = customDbPath;
            var customDirectory = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(customDirectory) && !Directory.Exists(customDirectory))
            {
                Directory.CreateDirectory(customDirectory);
            }
        }
        else
        {
            // Create database in user's home directory
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var hlpAiDirectory = Path.Combine(homeDirectory, ".hlpai");
            
            if (!Directory.Exists(hlpAiDirectory))
            {
                Directory.CreateDirectory(hlpAiDirectory);
                _logger?.LogInformation("Created HlpAI configuration directory at {Directory}", hlpAiDirectory);
            }
            
            _dbPath = Path.Combine(hlpAiDirectory, "config.db");
        }
        
        // Ensure the directory exists for the database file
        var dbDirectory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }
        
        // Initialize connection and database with better connection settings
        var connectionString = $"Data Source={_dbPath};Cache=Shared;";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        InitializeDatabase();
        
        // Only log initialization for singleton instances to reduce duplicate messages
        if (isSingleton)
        {
            _logger?.LogInformation("SqliteConfigurationService initialized with database at {DbPath}", _dbPath);
        }
    }



    /// <summary>
    /// Gets the database file path
    /// </summary>
    public string DatabasePath => _dbPath;

    /// <summary>
    /// Sets a configuration value
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Configuration value</param>
    /// <param name="category">Optional category for grouping settings</param>
    /// <returns>True if successful</returns>
    public async Task<bool> SetConfigurationAsync(string key, string? value, string category = "general")
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(category);

        await _dbSemaphore.WaitAsync();
        try
        {
            // Ensure connection is open before executing commands
            if (_connection.State != System.Data.ConnectionState.Open)
            {
                try
                {
                    _connection.Open();
                    InitializeDatabase(); // Reinitialize if connection was closed
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to reopen database connection for SetConfigurationAsync");
                    return false;
                }
            }

            const string sql = """
                INSERT OR REPLACE INTO configuration (key, value, category, updated_at) 
                VALUES (@key, @value, @category, @updated_at)
                """;

            using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", value ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@category", category);
            command.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

            var rowsAffected = await command.ExecuteNonQueryAsync();
            
            // Only log debug info in non-test environments to improve test performance
            if (!IsTestEnvironment())
            {
                _logger?.LogDebug("Set configuration: {Category}.{Key} = {Value}, rows affected: {Rows}",
                    category, key, value, rowsAffected);
                
                // Immediately verify the change was persisted (skip verification in tests for performance)
                if (rowsAffected > 0)
                {
                    const string verifySql = "SELECT value FROM configuration WHERE key = @key AND category = @category";
                    using var verifyCommand = new SqliteCommand(verifySql, _connection);
                    verifyCommand.Parameters.AddWithValue("@key", key);
                    verifyCommand.Parameters.AddWithValue("@category", category);
                    
                    var verifyResult = await verifyCommand.ExecuteScalarAsync();
                    _logger?.LogDebug("Verification result for {Category}.{Key}: {Result}", category, key, verifyResult);
                }
            }
            
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error setting configuration {Category}.{Key}", category, key);
            return false;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets a configuration value
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="category">Optional category for grouping settings</param>
    /// <param name="defaultValue">Default value if not found</param>
    /// <returns>Configuration value or default</returns>
    public async Task<string?> GetConfigurationAsync(string key, string category = "general", string? defaultValue = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(category);

        await _dbSemaphore.WaitAsync();
        try
        {
            // Ensure connection is open before executing commands
            if (_connection.State != System.Data.ConnectionState.Open)
            {
                try
                {
                    _connection.Open();
                    InitializeDatabase(); // Reinitialize if connection was closed
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to reopen database connection for GetConfigurationAsync");
                    return defaultValue;
                }
            }

            const string sql = "SELECT value FROM configuration WHERE key = @key AND category = @category";

            using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@category", category);

            var result = await command.ExecuteScalarAsync();
            
            if (result == null || result == DBNull.Value)
            {
                // Only log debug info in non-test environments to improve test performance
                if (!IsTestEnvironment())
                {
                    _logger?.LogDebug("Configuration not found: {Category}.{Key}, returning default: {Default}",
                        category, key, defaultValue);
                }
                return defaultValue;
            }

            var value = result.ToString();
            // Only log debug info in non-test environments to improve test performance
            if (!IsTestEnvironment())
            {
                _logger?.LogDebug("Retrieved configuration: {Category}.{Key} = {Value}", category, key, value);
            }
            return value;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting configuration {Category}.{Key}", category, key);
            return defaultValue;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets a configuration value as a boolean
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="category">Optional category for grouping settings</param>
    /// <param name="defaultValue">Default value if not found</param>
    /// <returns>Configuration value as boolean or default</returns>
    public async Task<bool> GetConfigurationBoolAsync(string key, string category = "general", bool defaultValue = false)
    {
        var value = await GetConfigurationAsync(key, category);
        
        if (string.IsNullOrEmpty(value))
            return defaultValue;

        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Sets a configuration value as a boolean
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Boolean value</param>
    /// <param name="category">Optional category for grouping settings</param>
    /// <returns>True if successful</returns>
    public async Task<bool> SetConfigurationBoolAsync(string key, bool value, string category = "general")
    {
        return await SetConfigurationAsync(key, value.ToString().ToLowerInvariant(), category);
    }

    /// <summary>
    /// Removes a configuration value
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="category">Optional category for grouping settings</param>
    /// <returns>True if successful</returns>
    public async Task<bool> RemoveConfigurationAsync(string key, string category = "general")
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(category);

        try
        {
            const string sql = "DELETE FROM configuration WHERE key = @key AND category = @category";

            using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@category", category);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            
            _logger?.LogDebug("Removed configuration: {Category}.{Key}, rows affected: {Rows}", 
                category, key, rowsAffected);
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing configuration {Category}.{Key}", category, key);
            return false;
        }
    }

    /// <summary>
    /// Gets all configuration values for a category
    /// </summary>
    /// <param name="category">Category to retrieve</param>
    /// <returns>Dictionary of key-value pairs</returns>
    public async Task<Dictionary<string, string?>> GetCategoryConfigurationAsync(string category = "general")
    {
        ArgumentNullException.ThrowIfNull(category);

        var result = new Dictionary<string, string?>();

        try
        {
            const string sql = "SELECT key, value FROM configuration WHERE category = @category ORDER BY key";

            using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@category", category);

            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var key = reader.GetString(0);
                var value = reader.IsDBNull(1) ? null : reader.GetString(1);
                result[key] = value;
            }

            _logger?.LogDebug("Retrieved {Count} configuration items for category: {Category}", 
                result.Count, category);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting category configuration: {Category}", category);
        }

        return result;
    }

    /// <summary>
    /// Clears all configuration for a category
    /// </summary>
    /// <param name="category">Category to clear</param>
    /// <returns>Number of items removed</returns>
    public async Task<int> ClearCategoryAsync(string category)
    {
        ArgumentNullException.ThrowIfNull(category);

        try
        {
            const string sql = "DELETE FROM configuration WHERE category = @category";

            using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@category", category);

            var deletedCount = await command.ExecuteNonQueryAsync();
            
            _logger?.LogInformation("Cleared {DeletedCount} configuration items from category: {Category}", 
                deletedCount, category);
            
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing category configuration: {Category}", category);
            return 0;
        }
    }

    /// <summary>
    /// Gets configuration statistics
    /// </summary>
    /// <returns>Configuration statistics</returns>
    public async Task<ConfigurationStats> GetStatsAsync()
    {
        try
        {
            const string sql = """
                SELECT 
                    COUNT(*) as total_items,
                    COUNT(DISTINCT category) as total_categories,
                    MAX(updated_at) as last_update
                FROM configuration
                """;

            using var command = new SqliteCommand(sql, _connection);
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var totalItems = reader.GetInt32(0);
                var totalCategories = reader.GetInt32(1);
                var lastUpdateStr = reader.IsDBNull(2) ? null : reader.GetString(2);
                
                DateTime? lastUpdate = null;
                if (!string.IsNullOrEmpty(lastUpdateStr) && DateTime.TryParse(lastUpdateStr, out var parsed))
                {
                    lastUpdate = parsed;
                }

                return new ConfigurationStats
                {
                    TotalItems = totalItems,
                    TotalCategories = totalCategories,
                    LastUpdate = lastUpdate,
                    DatabasePath = _dbPath
                };
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting configuration statistics");
        }

        return new ConfigurationStats
        {
            TotalItems = 0,
            TotalCategories = 0,
            LastUpdate = null,
            DatabasePath = _dbPath
        };
    }

    /// <summary>
    /// Optimize the database by running VACUUM and ANALYZE
    /// </summary>
    public async Task OptimizeDatabaseAsync()
    {
        try
        {
            using var command = new SqliteCommand("VACUUM; ANALYZE;", _connection);
            await command.ExecuteNonQueryAsync();
            _logger?.LogInformation("Database optimization completed");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error optimizing database");
            throw;
        }
    }

    /// <summary>
    /// Sets the AI provider configuration
    /// </summary>
    /// <param name="providerType">The AI provider type</param>
    /// <param name="model">The model name</param>
    /// <returns>True if successful</returns>
    public async Task<bool> SetAiProviderConfigurationAsync(AiProviderType providerType, string model)
    {
        try
        {
            // Store provider type
            await SetConfigurationAsync("LastProvider", providerType.ToString(), "ai_provider");
            
            // Store model name
            await SetConfigurationAsync("LastModel", model, "ai_provider");
            
            // Store timestamp
            await SetConfigurationAsync("LastUpdated", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), "ai_provider");
            
            _logger?.LogInformation("AI provider configuration saved: {Provider} with model {Model}", providerType, model);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving AI provider configuration");
            return false;
        }
    }

    /// <summary>
    /// Gets the AI provider configuration
    /// </summary>
    /// <returns>Tuple containing provider type and model, or null if not configured</returns>
    public async Task<(AiProviderType ProviderType, string Model)?> GetAiProviderConfigurationAsync()
    {
        try
        {
            var providerStr = await GetConfigurationAsync("LastProvider", "ai_provider");
            var model = await GetConfigurationAsync("LastModel", "ai_provider");
            
            if (string.IsNullOrEmpty(providerStr) || string.IsNullOrEmpty(model))
            {
                _logger?.LogDebug("AI provider configuration not found in SQLite database");
                return null;
            }

            if (Enum.TryParse<AiProviderType>(providerStr, out var providerType))
            {
                _logger?.LogDebug("Retrieved AI provider configuration: {Provider} with model {Model}", providerType, model);
                return (providerType, model);
            }
            
            _logger?.LogWarning("Invalid provider type in configuration: {ProviderStr}", providerStr);
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving AI provider configuration");
            return null;
        }
    }

    /// <summary>
    /// Clears the AI provider configuration
    /// </summary>
    /// <returns>True if successful</returns>
    public async Task<bool> ClearAiProviderConfigurationAsync()
    {
        try
        {
            await RemoveConfigurationAsync("LastProvider", "ai_provider");
            await RemoveConfigurationAsync("LastModel", "ai_provider");
            await RemoveConfigurationAsync("LastUpdated", "ai_provider");
            
            _logger?.LogInformation("AI provider configuration cleared");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing AI provider configuration");
            return false;
        }
    }

    /// <summary>
    /// Gets the timestamp when the AI provider configuration was last updated
    /// </summary>
    /// <returns>Last update timestamp or null if not configured</returns>
    public async Task<DateTime?> GetAiProviderConfigurationTimestampAsync()
    {
        try
        {
            var timestampStr = await GetConfigurationAsync("LastUpdated", "ai_provider");
            
            if (string.IsNullOrEmpty(timestampStr))
            {
                return null;
            }

            if (DateTime.TryParse(timestampStr, out var timestamp))
            {
                return timestamp;
            }
            
            _logger?.LogWarning("Invalid timestamp format in AI provider configuration: {TimestampStr}", timestampStr);
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving AI provider configuration timestamp");
            return null;
        }
    }

    /// <summary>
    /// Saves a complete AppConfiguration object to SQLite
    /// </summary>
    /// <param name="config">The configuration to save</param>
    /// <returns>True if successful</returns>
    public async Task<bool> SaveAppConfigurationAsync(AppConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        
        try
        {
            // Update timestamp
            config.LastUpdated = DateTime.UtcNow;
            
            // Save all configuration properties to appropriate categories
            await SetConfigurationAsync("LastDirectory", config.LastDirectory, "general");
            await SetConfigurationBoolAsync("RememberLastDirectory", config.RememberLastDirectory, "ui");
            await SetConfigurationAsync("LastProvider", config.LastProvider.ToString(), "ai_provider");
            await SetConfigurationAsync("LastModel", config.LastModel, "ai_provider");
            await SetConfigurationBoolAsync("RememberLastModel", config.RememberLastModel, "ai_provider");
            await SetConfigurationBoolAsync("RememberLastProvider", config.RememberLastProvider, "ai_provider");
            
            // Provider URLs
            await SetConfigurationAsync("OllamaUrl", config.OllamaUrl, "provider_urls");
            await SetConfigurationAsync("LmStudioUrl", config.LmStudioUrl, "provider_urls");
            await SetConfigurationAsync("OpenWebUiUrl", config.OpenWebUiUrl, "provider_urls");
            await SetConfigurationAsync("OpenAiUrl", config.OpenAiUrl, "provider_urls");
            await SetConfigurationAsync("AnthropicUrl", config.AnthropicUrl, "provider_urls");
            await SetConfigurationAsync("DeepSeekUrl", config.DeepSeekUrl, "provider_urls");
            
            // Default models
            await SetConfigurationAsync("OllamaDefaultModel", config.OllamaDefaultModel, "default_models");
            await SetConfigurationAsync("LmStudioDefaultModel", config.LmStudioDefaultModel, "default_models");
            await SetConfigurationAsync("OpenWebUiDefaultModel", config.OpenWebUiDefaultModel, "default_models");
            await SetConfigurationAsync("OpenAiDefaultModel", config.OpenAiDefaultModel, "default_models");
            await SetConfigurationAsync("AnthropicDefaultModel", config.AnthropicDefaultModel, "default_models");
            await SetConfigurationAsync("DeepSeekDefaultModel", config.DeepSeekDefaultModel, "default_models");
            
            // Timeouts
            await SetConfigurationAsync("AiProviderTimeoutMinutes", config.AiProviderTimeoutMinutes.ToString(), "timeouts");
            await SetConfigurationAsync("OllamaTimeoutMinutes", config.OllamaTimeoutMinutes.ToString(), "timeouts");
            await SetConfigurationAsync("LmStudioTimeoutMinutes", config.LmStudioTimeoutMinutes.ToString(), "timeouts");
            await SetConfigurationAsync("OpenWebUiTimeoutMinutes", config.OpenWebUiTimeoutMinutes.ToString(), "timeouts");
            await SetConfigurationAsync("EmbeddingTimeoutMinutes", config.EmbeddingTimeoutMinutes.ToString(), "timeouts");
            await SetConfigurationAsync("OpenAiTimeoutMinutes", config.OpenAiTimeoutMinutes.ToString(), "timeouts");
            await SetConfigurationAsync("AnthropicTimeoutMinutes", config.AnthropicTimeoutMinutes.ToString(), "timeouts");
            await SetConfigurationAsync("DeepSeekTimeoutMinutes", config.DeepSeekTimeoutMinutes.ToString(), "timeouts");
            
            // Max tokens
            await SetConfigurationAsync("OpenAiMaxTokens", config.OpenAiMaxTokens.ToString(), "max_tokens");
            await SetConfigurationAsync("AnthropicMaxTokens", config.AnthropicMaxTokens.ToString(), "max_tokens");
            await SetConfigurationAsync("DeepSeekMaxTokens", config.DeepSeekMaxTokens.ToString(), "max_tokens");
            await SetConfigurationAsync("LmStudioMaxTokens", config.LmStudioMaxTokens.ToString(), "max_tokens");
            await SetConfigurationAsync("OpenWebUiMaxTokens", config.OpenWebUiMaxTokens.ToString(), "max_tokens");
            
            // Security settings
            await SetConfigurationBoolAsync("UseSecureApiKeyStorage", config.UseSecureApiKeyStorage, "security");
            await SetConfigurationBoolAsync("ValidateApiKeysOnStartup", config.ValidateApiKeysOnStartup, "security");
            await SetConfigurationAsync("MaxRequestSizeBytes", config.MaxRequestSizeBytes.ToString(), "security");
            await SetConfigurationAsync("MaxContentLengthBytes", config.MaxContentLengthBytes.ToString(), "security");
            await SetConfigurationAsync("MaxFileAuditSizeBytes", config.MaxFileAuditSizeBytes.ToString(), "security");
            
            // Operation mode
            await SetConfigurationAsync("LastOperationMode", config.LastOperationMode.ToString(), "operation");
            await SetConfigurationBoolAsync("RememberLastOperationMode", config.RememberLastOperationMode, "general");
            
            // CHM settings
            await SetConfigurationAsync("HhExePath", config.HhExePath, "chm");
            await SetConfigurationBoolAsync("AutoDetectHhExe", config.AutoDetectHhExe, "chm");
            
            // Processing settings
            await SetConfigurationAsync("ChunkSize", config.ChunkSize.ToString(), "processing");
            await SetConfigurationAsync("ChunkOverlap", config.ChunkOverlap.ToString(), "processing");
            
            // Validation limits
            await SetConfigurationAsync("ApiKeyMaxLength", config.ApiKeyMaxLength.ToString(), "validation");
            await SetConfigurationAsync("ModelNameMaxLength", config.ModelNameMaxLength.ToString(), "validation");
            await SetConfigurationAsync("ProviderNameMaxLength", config.ProviderNameMaxLength.ToString(), "validation");
            await SetConfigurationAsync("FilePathMaxLength", config.FilePathMaxLength.ToString(), "validation");
            
            // Display limits
            // Save additional display limits - using actual properties from AppConfiguration
            await SetConfigurationAsync("MaxUnsupportedExtensionGroupsDisplayed", config.MaxUnsupportedExtensionGroupsDisplayed.ToString(), "display");
            await SetConfigurationAsync("MaxFilesPerCategoryDisplayed", config.MaxFilesPerCategoryDisplayed.ToString(), "display");
            // Save additional display limits - using actual properties from AppConfiguration
            await SetConfigurationAsync("MaxNotIndexedFilesDisplayed", config.MaxNotIndexedFilesDisplayed.ToString(), "display");
            await SetConfigurationAsync("MaxChmExtractorFilesDisplayed", config.MaxChmExtractorFilesDisplayed.ToString(), "display");
            // Save display limits - using actual properties from AppConfiguration
            await SetConfigurationAsync("MaxUnsupportedFilesDisplayed", config.MaxUnsupportedFilesDisplayed.ToString(), "display");
            await SetConfigurationAsync("MaxRecentLogsDisplayed", config.MaxRecentLogsDisplayed.ToString(), "display");
            await SetConfigurationAsync("MaxRecentHistoryDisplayed", config.MaxRecentHistoryDisplayed.ToString(), "display");
            await SetConfigurationAsync("MaxCleanupHistoryRecords", config.MaxCleanupHistoryRecords.ToString(), "display");
            await SetConfigurationAsync("MaxTopViolationsDisplayed", config.MaxTopViolationsDisplayed.ToString(), "display");
            await SetConfigurationAsync("MaxFailedFilesDisplayed", config.MaxFailedFilesDisplayed.ToString(), "display");
            
            // Encryption settings
            await SetConfigurationAsync("EncryptionKeySize", config.EncryptionKeySize.ToString(), "encryption");
            await SetConfigurationAsync("EncryptionIvSize", config.EncryptionIvSize.ToString(), "encryption");
            await SetConfigurationAsync("EncryptionSaltSize", config.EncryptionSaltSize.ToString(), "encryption");
            await SetConfigurationAsync("EncryptionPbkdf2Iterations", config.EncryptionPbkdf2Iterations.ToString(), "encryption");
            
            // Metadata
            await SetConfigurationAsync("LastUpdated", config.LastUpdated.ToString("O"), "metadata");
            await SetConfigurationAsync("ConfigVersion", config.ConfigVersion.ToString(), "metadata");
            
            // Menu context
            await SetConfigurationAsync("CurrentMenuContext", config.CurrentMenuContext.ToString(), "menu");
            await SetConfigurationBoolAsync("RememberMenuContext", config.RememberMenuContext, "menu");
            if (config.MenuHistory != null && config.MenuHistory.Any())
            {
                await SetConfigurationAsync("MenuHistory", string.Join("|", config.MenuHistory), "menu");
            }
            
            _logger?.LogInformation("Complete application configuration saved to SQLite");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving complete application configuration to SQLite");
            return false;
        }
    }
    
    /// <summary>
    /// Loads a complete AppConfiguration object from SQLite
    /// </summary>
    /// <returns>The loaded configuration or a default configuration if not found</returns>
    public async Task<AppConfiguration> LoadAppConfigurationAsync()
    {
        try
        {
            var config = new AppConfiguration();
            
            // Load general settings
            config.LastDirectory = await GetConfigurationAsync("LastDirectory", "general") ?? config.LastDirectory;
            config.RememberLastDirectory = await GetConfigurationBoolAsync("RememberLastDirectory", "ui", true); // Default: true
            
            // Load AI provider settings
            var lastProviderStr = await GetConfigurationAsync("LastProvider", "ai_provider");
            if (!string.IsNullOrEmpty(lastProviderStr) && Enum.TryParse<AiProviderType>(lastProviderStr, out var lastProvider))
            {
                config.LastProvider = lastProvider;
            }
            config.LastModel = await GetConfigurationAsync("LastModel", "ai_provider") ?? config.LastModel;
            config.RememberLastModel = await GetConfigurationBoolAsync("RememberLastModel", "ai_provider", true); // Default: true
            config.RememberLastProvider = await GetConfigurationBoolAsync("RememberLastProvider", "ai_provider", true); // Default: true
            
            // Load provider URLs
            config.OllamaUrl = await GetConfigurationAsync("OllamaUrl", "provider_urls") ?? config.OllamaUrl;
            config.LmStudioUrl = await GetConfigurationAsync("LmStudioUrl", "provider_urls") ?? config.LmStudioUrl;
            config.OpenWebUiUrl = await GetConfigurationAsync("OpenWebUiUrl", "provider_urls") ?? config.OpenWebUiUrl;
            config.OpenAiUrl = await GetConfigurationAsync("OpenAiUrl", "provider_urls") ?? config.OpenAiUrl;
            config.AnthropicUrl = await GetConfigurationAsync("AnthropicUrl", "provider_urls") ?? config.AnthropicUrl;
            config.DeepSeekUrl = await GetConfigurationAsync("DeepSeekUrl", "provider_urls") ?? config.DeepSeekUrl;
            
            // Load default models
            config.OllamaDefaultModel = await GetConfigurationAsync("OllamaDefaultModel", "default_models") ?? config.OllamaDefaultModel;
            config.LmStudioDefaultModel = await GetConfigurationAsync("LmStudioDefaultModel", "default_models") ?? config.LmStudioDefaultModel;
            config.OpenWebUiDefaultModel = await GetConfigurationAsync("OpenWebUiDefaultModel", "default_models") ?? config.OpenWebUiDefaultModel;
            config.OpenAiDefaultModel = await GetConfigurationAsync("OpenAiDefaultModel", "default_models") ?? config.OpenAiDefaultModel;
            config.AnthropicDefaultModel = await GetConfigurationAsync("AnthropicDefaultModel", "default_models") ?? config.AnthropicDefaultModel;
            config.DeepSeekDefaultModel = await GetConfigurationAsync("DeepSeekDefaultModel", "default_models") ?? config.DeepSeekDefaultModel;
            
            // Load timeouts
            if (int.TryParse(await GetConfigurationAsync("AiProviderTimeoutMinutes", "timeouts"), out var aiProviderTimeout))
                config.AiProviderTimeoutMinutes = aiProviderTimeout;
            if (int.TryParse(await GetConfigurationAsync("OllamaTimeoutMinutes", "timeouts"), out var ollamaTimeout))
                config.OllamaTimeoutMinutes = ollamaTimeout;
            if (int.TryParse(await GetConfigurationAsync("LmStudioTimeoutMinutes", "timeouts"), out var lmStudioTimeout))
                config.LmStudioTimeoutMinutes = lmStudioTimeout;
            if (int.TryParse(await GetConfigurationAsync("OpenWebUiTimeoutMinutes", "timeouts"), out var openWebUiTimeout))
                config.OpenWebUiTimeoutMinutes = openWebUiTimeout;
            if (int.TryParse(await GetConfigurationAsync("EmbeddingTimeoutMinutes", "timeouts"), out var embeddingTimeout))
                config.EmbeddingTimeoutMinutes = embeddingTimeout;
            if (int.TryParse(await GetConfigurationAsync("OpenAiTimeoutMinutes", "timeouts"), out var openAiTimeout))
                config.OpenAiTimeoutMinutes = openAiTimeout;
            if (int.TryParse(await GetConfigurationAsync("AnthropicTimeoutMinutes", "timeouts"), out var anthropicTimeout))
                config.AnthropicTimeoutMinutes = anthropicTimeout;
            if (int.TryParse(await GetConfigurationAsync("DeepSeekTimeoutMinutes", "timeouts"), out var deepSeekTimeout))
                config.DeepSeekTimeoutMinutes = deepSeekTimeout;
            
            // Load max tokens
            if (int.TryParse(await GetConfigurationAsync("OpenAiMaxTokens", "max_tokens"), out var openAiMaxTokens))
                config.OpenAiMaxTokens = openAiMaxTokens;
            if (int.TryParse(await GetConfigurationAsync("AnthropicMaxTokens", "max_tokens"), out var anthropicMaxTokens))
                config.AnthropicMaxTokens = anthropicMaxTokens;
            if (int.TryParse(await GetConfigurationAsync("DeepSeekMaxTokens", "max_tokens"), out var deepSeekMaxTokens))
                config.DeepSeekMaxTokens = deepSeekMaxTokens;
            if (int.TryParse(await GetConfigurationAsync("LmStudioMaxTokens", "max_tokens"), out var lmStudioMaxTokens))
                config.LmStudioMaxTokens = lmStudioMaxTokens;
            if (int.TryParse(await GetConfigurationAsync("OpenWebUiMaxTokens", "max_tokens"), out var openWebUiMaxTokens))
                config.OpenWebUiMaxTokens = openWebUiMaxTokens;
            
            // Load security settings
            config.UseSecureApiKeyStorage = await GetConfigurationBoolAsync("UseSecureApiKeyStorage", "security", true); // Default: true
            config.ValidateApiKeysOnStartup = await GetConfigurationBoolAsync("ValidateApiKeysOnStartup", "security", true); // Default: true
            if (long.TryParse(await GetConfigurationAsync("MaxRequestSizeBytes", "security"), out var maxRequestSize))
                config.MaxRequestSizeBytes = maxRequestSize;
            if (long.TryParse(await GetConfigurationAsync("MaxContentLengthBytes", "security"), out var maxContentLength))
                config.MaxContentLengthBytes = (int)maxContentLength;
            if (long.TryParse(await GetConfigurationAsync("MaxFileAuditSizeBytes", "security"), out var maxFileAuditSize))
                config.MaxFileAuditSizeBytes = (int)maxFileAuditSize;
            
            // Load operation mode
            var lastOperationModeStr = await GetConfigurationAsync("LastOperationMode", "operation");
            if (!string.IsNullOrEmpty(lastOperationModeStr) && Enum.TryParse<OperationMode>(lastOperationModeStr, out var lastOperationMode))
            {
                config.LastOperationMode = lastOperationMode;
            }
            config.RememberLastOperationMode = await GetConfigurationBoolAsync("RememberLastOperationMode", "general", true); // Default: true
            
            // Load CHM settings
            config.HhExePath = await GetConfigurationAsync("HhExePath", "chm") ?? config.HhExePath;
            config.AutoDetectHhExe = await GetConfigurationBoolAsync("AutoDetectHhExe", "chm", true); // Default: true
            
            // Load processing settings
            if (int.TryParse(await GetConfigurationAsync("ChunkSize", "processing"), out var chunkSize))
                config.ChunkSize = chunkSize;
            if (int.TryParse(await GetConfigurationAsync("ChunkOverlap", "processing"), out var chunkOverlap))
                config.ChunkOverlap = chunkOverlap;
            
            // Load validation limits
            if (int.TryParse(await GetConfigurationAsync("ApiKeyMaxLength", "validation"), out var apiKeyMaxLength))
                config.ApiKeyMaxLength = apiKeyMaxLength;
            if (int.TryParse(await GetConfigurationAsync("ModelNameMaxLength", "validation"), out var modelNameMaxLength))
                config.ModelNameMaxLength = modelNameMaxLength;
            if (int.TryParse(await GetConfigurationAsync("ProviderNameMaxLength", "validation"), out var providerNameMaxLength))
                config.ProviderNameMaxLength = providerNameMaxLength;
            if (int.TryParse(await GetConfigurationAsync("FilePathMaxLength", "validation"), out var filePathMaxLength))
                config.FilePathMaxLength = filePathMaxLength;
            
            // Load display limits
            // Load additional display limits - using actual properties from AppConfiguration
            if (int.TryParse(await GetConfigurationAsync("MaxUnsupportedExtensionGroupsDisplayed", "display"), out var maxUnsupportedExtensionGroups))
                config.MaxUnsupportedExtensionGroupsDisplayed = maxUnsupportedExtensionGroups;
            if (int.TryParse(await GetConfigurationAsync("MaxFilesPerCategoryDisplayed", "display"), out var maxFilesPerCategory))
                config.MaxFilesPerCategoryDisplayed = maxFilesPerCategory;
            // Load additional display limits - using actual properties from AppConfiguration
            if (int.TryParse(await GetConfigurationAsync("MaxNotIndexedFilesDisplayed", "display"), out var maxNotIndexedFiles))
                config.MaxNotIndexedFilesDisplayed = maxNotIndexedFiles;
            if (int.TryParse(await GetConfigurationAsync("MaxChmExtractorFilesDisplayed", "display"), out var maxChmExtractorFiles))
                config.MaxChmExtractorFilesDisplayed = maxChmExtractorFiles;
            if (int.TryParse(await GetConfigurationAsync("MaxLargeFilesDisplayed", "display"), out var maxLargeFiles))
                config.MaxLargeFilesDisplayed = maxLargeFiles;
            if (int.TryParse(await GetConfigurationAsync("MaxModelsDisplayed", "display"), out var maxModels))
                config.MaxModelsDisplayed = maxModels;
            // Load additional display limits
            if (int.TryParse(await GetConfigurationAsync("MaxCleanupHistoryRecords", "display"), out var maxCleanupHistory))
                config.MaxCleanupHistoryRecords = maxCleanupHistory;
            if (int.TryParse(await GetConfigurationAsync("MaxTopViolationsDisplayed", "display"), out var maxTopViolations))
                config.MaxTopViolationsDisplayed = maxTopViolations;
            if (int.TryParse(await GetConfigurationAsync("MaxFailedFilesDisplayed", "display"), out var maxFailedFiles))
                config.MaxFailedFilesDisplayed = maxFailedFiles;
            // Load display limits - using actual properties from AppConfiguration
            if (int.TryParse(await GetConfigurationAsync("MaxUnsupportedFilesDisplayed", "display"), out var maxUnsupportedFiles))
                config.MaxUnsupportedFilesDisplayed = maxUnsupportedFiles;
            if (int.TryParse(await GetConfigurationAsync("MaxRecentLogsDisplayed", "display"), out var maxRecentLogs))
                config.MaxRecentLogsDisplayed = maxRecentLogs;
            if (int.TryParse(await GetConfigurationAsync("MaxRecentHistoryDisplayed", "display"), out var maxRecentHistory))
                config.MaxRecentHistoryDisplayed = maxRecentHistory;
            
            // Load encryption settings
            if (int.TryParse(await GetConfigurationAsync("EncryptionKeySize", "encryption"), out var encryptionKeySize))
                config.EncryptionKeySize = encryptionKeySize;
            if (int.TryParse(await GetConfigurationAsync("EncryptionIvSize", "encryption"), out var encryptionIvSize))
                config.EncryptionIvSize = encryptionIvSize;
            if (int.TryParse(await GetConfigurationAsync("EncryptionSaltSize", "encryption"), out var encryptionSaltSize))
                config.EncryptionSaltSize = encryptionSaltSize;
            if (int.TryParse(await GetConfigurationAsync("EncryptionPbkdf2Iterations", "encryption"), out var encryptionPbkdf2Iterations))
                config.EncryptionPbkdf2Iterations = encryptionPbkdf2Iterations;
            
            // Load metadata
            var lastUpdatedStr = await GetConfigurationAsync("LastUpdated", "metadata");
            if (!string.IsNullOrEmpty(lastUpdatedStr) && DateTime.TryParseExact(lastUpdatedStr, "O", null, DateTimeStyles.RoundtripKind, out var lastUpdated))
            {
                config.LastUpdated = lastUpdated;
            }
            var configVersionStr = await GetConfigurationAsync("ConfigVersion", "metadata");
            if (!string.IsNullOrEmpty(configVersionStr) && int.TryParse(configVersionStr, out var configVersion))
            {
                config.ConfigVersion = configVersion;
            }
            
            // Load menu context
            var currentMenuContextStr = await GetConfigurationAsync("CurrentMenuContext", "menu");
            if (!string.IsNullOrEmpty(currentMenuContextStr) && Enum.TryParse<MenuContext>(currentMenuContextStr, out var currentMenuContext))
            {
                config.CurrentMenuContext = currentMenuContext;
            }
            
            // GetConfigurationBoolAsync returns bool, not bool?, so we can assign directly
            config.RememberMenuContext = await GetConfigurationBoolAsync("RememberMenuContext", "menu", true); // Default: true
            
            var menuHistoryStr = await GetConfigurationAsync("MenuHistory", "menu");
            if (!string.IsNullOrEmpty(menuHistoryStr))
            {
                var menuHistoryItems = menuHistoryStr.Split('|', StringSplitOptions.RemoveEmptyEntries);
                config.MenuHistory = new List<MenuContext>();
                foreach (var item in menuHistoryItems)
                {
                    if (Enum.TryParse<MenuContext>(item, out var menuContext))
                    {
                        config.MenuHistory.Add(menuContext);
                    }
                }
            }
            
            _logger?.LogInformation("Complete application configuration loaded from SQLite");
            return config;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading complete application configuration from SQLite, returning defaults");
            return new AppConfiguration();
        }
    }

    private void InitializeDatabase()
    {
        try
        {
            // Set PRAGMA statements for better transaction handling
            // Use DELETE mode for test databases to avoid WAL file conflicts
            var journalMode = _dbPath.Contains("test_") ? "DELETE" : "WAL";
            using var pragmaCommand = new SqliteCommand($"PRAGMA journal_mode = {journalMode}; PRAGMA synchronous = NORMAL;", _connection);
            pragmaCommand.ExecuteNonQuery();

            const string createTableSql = """
                CREATE TABLE IF NOT EXISTS configuration (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    category TEXT NOT NULL DEFAULT 'general',
                    key TEXT NOT NULL,
                    value TEXT,
                    updated_at TEXT NOT NULL,
                    UNIQUE(category, key)
                );
                
                CREATE INDEX IF NOT EXISTS idx_configuration_category_key
                ON configuration(category, key);
                
                CREATE TABLE IF NOT EXISTS directory_configurations (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    directory_path TEXT NOT NULL UNIQUE,
                    ai_provider TEXT NOT NULL,
                    ai_model TEXT NOT NULL,
                    operation_mode TEXT NOT NULL DEFAULT 'Hybrid',
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                
                CREATE INDEX IF NOT EXISTS idx_directory_configurations_path
                ON directory_configurations(directory_path);
                """;

            using var command = new SqliteCommand(createTableSql, _connection);
            command.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.Message.Contains("database disk image is malformed") || ex.Message.Contains("file is not a database"))
        {
            _logger?.LogWarning("Database file is corrupted or invalid. Recreating database at {DbPath}", _dbPath);
            
            // Close and dispose current connection
            _connection?.Close();
            _connection?.Dispose();
            
            // Delete corrupted database file with retry logic
            if (File.Exists(_dbPath))
            {
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        // Try to remove readonly attribute if present
                        var fileInfo = new FileInfo(_dbPath);
                        if (fileInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
                        {
                            fileInfo.Attributes &= ~FileAttributes.ReadOnly;
                        }
                        
                        File.Delete(_dbPath);
                        break;
                    }
                    catch (IOException) when (i < 4)
                    {
                        Thread.Sleep(100 * (i + 1));
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // File is read-only, cannot delete
                        _logger?.LogWarning("Cannot delete readonly database file at {DbPath}. Service will operate in read-only mode.", _dbPath);
                        return; // Exit gracefully without recreating database
                    }
                }
            }
            
            _logger?.LogWarning("Database was corrupted and recreated at {DbPath}.", _dbPath);
            
            // Since _connection is readonly, we cannot reassign it. Instead, we'll throw an exception
            // that will cause the singleton instance to be recreated with a fresh connection
            throw new InvalidOperationException("Database was corrupted and needs to be recreated. Please recreate the SqliteConfigurationService instance.");
        }
        
        _logger?.LogDebug("Database table 'configuration' initialized");
        
        // Seed default configuration if database is empty
        SeedDefaultConfigurationIfEmpty();
    }
    
    /// <summary>
    /// Seeds the database with default configuration values if it's empty
    /// </summary>
    private void SeedDefaultConfigurationIfEmpty()
    {
        try
        {
            // Check if configuration table has any data
            const string countSql = "SELECT COUNT(*) FROM configuration";
            using var countCommand = new SqliteCommand(countSql, _connection);
            var count = Convert.ToInt32(countCommand.ExecuteScalar());
            
            if (count == 0)
            {
                if (IsTestEnvironment())
                {
                    // Skip seeding in test environment for faster tests
                    _logger?.LogDebug("Skipping default configuration seeding in test environment for performance");
                    return;
                }
                else
                {
                    _logger?.LogInformation("Seeding database with default configuration values");
                }
                SeedDefaultConfiguration();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking if database seeding is needed");
        }
    }
    
    private bool IsTestEnvironment()
    {
        // Check if we're in a test environment by looking for test-specific indicators
        return _dbPath.Contains("test_") || 
               _dbPath.Contains("sqlite_config_tests") ||
               AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName?.Contains("testhost") == true);
    }
    
    /// <summary>
    /// Seeds the database with all default configuration values
    /// </summary>
    private void SeedDefaultConfiguration()
    {
        try
        {
            // Use a transaction for better performance and consistency
            using var transaction = _connection.BeginTransaction();
            
            try
            {
                // Provider URLs (all null by default - configured by user)
                SetConfigurationSync("OllamaUrl", "http://localhost:11434", "provider_urls", transaction);
                SetConfigurationSync("LmStudioUrl", "http://localhost:1234", "provider_urls", transaction);
                SetConfigurationSync("OpenWebUiUrl", "http://localhost:3000", "provider_urls", transaction);
                SetConfigurationSync("OpenAiUrl", "https://api.openai.com", "provider_urls", transaction);
                SetConfigurationSync("AnthropicUrl", "https://api.anthropic.com", "provider_urls", transaction);
                SetConfigurationSync("DeepSeekUrl", "https://api.deepseek.com", "provider_urls", transaction);
                SetConfigurationSync("EmbeddingServiceUrl", "http://localhost:11434", "provider_urls", transaction);
                
                // Default models (null by default - configured by user)
                SetConfigurationSync("OllamaDefaultModel", "llama3.2", "default_models", transaction);
                SetConfigurationSync("LmStudioDefaultModel", "llama-3.2-3b-instruct", "default_models", transaction);
                SetConfigurationSync("OpenWebUiDefaultModel", "llama3.2", "default_models", transaction);
                SetConfigurationSync("OpenAiDefaultModel", "gpt-4o-mini", "default_models", transaction);
                SetConfigurationSync("AnthropicDefaultModel", "claude-3-5-sonnet-20241022", "default_models", transaction);
                SetConfigurationSync("DeepSeekDefaultModel", "deepseek-chat", "default_models", transaction);
                SetConfigurationSync("DefaultEmbeddingModel", "nomic-embed-text", "default_models", transaction);
                
                // Timeouts
                SetConfigurationSync("AiProviderTimeoutMinutes", "10", "timeouts", transaction);
                SetConfigurationSync("OllamaTimeoutMinutes", "10", "timeouts", transaction);
                SetConfigurationSync("LmStudioTimeoutMinutes", "10", "timeouts", transaction);
                SetConfigurationSync("OpenWebUiTimeoutMinutes", "10", "timeouts", transaction);
                SetConfigurationSync("EmbeddingTimeoutMinutes", "10", "timeouts", transaction);
                SetConfigurationSync("OpenAiTimeoutMinutes", "5", "timeouts", transaction);
                SetConfigurationSync("AnthropicTimeoutMinutes", "5", "timeouts", transaction);
                SetConfigurationSync("DeepSeekTimeoutMinutes", "5", "timeouts", transaction);
                
                // Max tokens
                SetConfigurationSync("OpenAiMaxTokens", "4000", "max_tokens", transaction);
                SetConfigurationSync("AnthropicMaxTokens", "4000", "max_tokens", transaction);
                SetConfigurationSync("DeepSeekMaxTokens", "4000", "max_tokens", transaction);
                SetConfigurationSync("LmStudioMaxTokens", "4096", "max_tokens", transaction);
                SetConfigurationSync("OpenWebUiMaxTokens", "4096", "max_tokens", transaction);
                
                // Security settings
                SetConfigurationSync("UseSecureApiKeyStorage", "true", "security", transaction);
                SetConfigurationSync("ValidateApiKeysOnStartup", "true", "security", transaction);
                SetConfigurationSync("MaxRequestSizeBytes", "10485760", "security", transaction); // 10MB
                SetConfigurationSync("MaxContentLengthBytes", "1048576", "security", transaction); // 1MB
                SetConfigurationSync("MaxFileAuditSizeBytes", "104857600", "security", transaction); // 100MB
                SetConfigurationSync("ApiKeyMinLength", "20", "security", transaction);
                SetConfigurationSync("ApiKeyMaxLength", "200", "security", transaction);
                SetConfigurationSync("ModelNameMaxLength", "100", "security", transaction);
                SetConfigurationSync("ProviderNameMaxLength", "50", "security", transaction);
                SetConfigurationSync("FilePathMaxLength", "260", "security", transaction);
                
                // UI preferences
                SetConfigurationSync("RememberLastDirectory", "true", "ui", transaction);
                SetConfigurationSync("RememberLastModel", "true", "ui", transaction);
                SetConfigurationSync("RememberLastProvider", "true", "ui", transaction);
                SetConfigurationSync("RememberLastEmbeddingModel", "true", "ui", transaction);
                SetConfigurationSync("RememberLastOperationMode", "true", "general", transaction);
                SetConfigurationSync("RememberMenuContext", "false", "ui", transaction);
                SetConfigurationSync("MaxChmExtractorFilesDisplayed", "10", "ui", transaction);
                SetConfigurationSync("MaxLargeFilesDisplayed", "5", "ui", transaction);
                SetConfigurationSync("MaxUnsupportedExtensionGroupsDisplayed", "3", "ui", transaction);
                SetConfigurationSync("MaxFilesPerCategoryDisplayed", "5", "ui", transaction);
                SetConfigurationSync("MaxRecentHistoryDisplayed", "10", "ui", transaction);
                SetConfigurationSync("MaxModelsDisplayed", "5", "ui", transaction);
                SetConfigurationSync("MaxSkippedFilesDisplayed", "10", "ui", transaction);
                SetConfigurationSync("MaxOperationFailedFilesDisplayed", "10", "ui", transaction);
                
                // File processing
                SetConfigurationSync("ChunkSize", "1000", "file_processing", transaction);
                SetConfigurationSync("ChunkOverlap", "200", "file_processing", transaction);
                
                // Encryption
                SetConfigurationSync("EncryptionKeySize", "256", "encryption", transaction);
                SetConfigurationSync("EncryptionIvSize", "128", "encryption", transaction);
                SetConfigurationSync("EncryptionSaltSize", "32", "encryption", transaction);
                SetConfigurationSync("EncryptionPbkdf2Iterations", "100000", "encryption", transaction);
                
                // Operation modes
                SetConfigurationSync("LastOperationMode", "Hybrid", "operation", transaction);
                SetConfigurationSync("LastProvider", "Ollama", "operation", transaction);
                SetConfigurationSync("CurrentMenuContext", "MainMenu", "operation", transaction);
                
                // Version and metadata
                SetConfigurationSync("ConfigVersion", "1", "metadata", transaction);
                SetConfigurationSync("LastUpdated", DateTime.UtcNow.ToString("O"), "metadata", transaction);
                
                transaction.Commit();
                _logger?.LogInformation("Successfully seeded database with default configuration values");
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error seeding default configuration");
        }
    }
    
    /// <summary>
    /// Synchronous version of SetConfigurationAsync for use during database initialization
    /// </summary>
    private void SetConfigurationSync(string key, string? value, string category, SqliteTransaction transaction)
    {
        const string sql = """
            INSERT OR REPLACE INTO configuration (key, value, category, updated_at) 
            VALUES (@key, @value, @category, @updated_at)
            """;

        using var command = new SqliteCommand(sql, _connection, transaction);
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value ?? string.Empty);
        command.Parameters.AddWithValue("@category", category);
        command.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("O"));
        
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Updates the last directory in the configuration
    /// </summary>
    /// <param name="directory">The directory path to save</param>
    /// <returns>True if updated successfully</returns>
    public async Task<bool> UpdateLastDirectoryAsync(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        return await SetConfigurationAsync("LastDirectory", directory, "general");
    }

    /// <summary>
    /// Updates the last model in the configuration
    /// </summary>
    /// <param name="model">The model name to save</param>
    /// <returns>True if updated successfully</returns>
    public async Task<bool> UpdateLastModelAsync(string model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return await SetConfigurationAsync("LastModel", model, "ai_provider");
    }

    /// <summary>
    /// Updates the last operation mode in the configuration
    /// </summary>
    /// <param name="mode">The operation mode to save</param>
    /// <returns>True if updated successfully</returns>
    public async Task<bool> UpdateLastOperationModeAsync(OperationMode mode)
    {
        return await SetConfigurationAsync("LastOperationMode", mode.ToString(), "operation");
    }

    /// <summary>
    /// Saves directory-specific configuration
    /// </summary>
    /// <param name="directoryPath">The directory path</param>
    /// <param name="aiProvider">The AI provider type</param>
    /// <param name="aiModel">The AI model name</param>
    /// <param name="operationMode">The operation mode</param>
    /// <returns>True if saved successfully</returns>
    public async Task<bool> SaveDirectoryConfigurationAsync(string directoryPath, AiProviderType aiProvider, string aiModel, OperationMode operationMode)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        ArgumentNullException.ThrowIfNull(aiModel);

        await _dbSemaphore.WaitAsync();
        try
        {
            if (_connection?.State != System.Data.ConnectionState.Open)
            {
                _connection?.Close();
                _connection?.Open();
            }

            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            const string sql = """
                INSERT OR REPLACE INTO directory_configurations 
                (directory_path, ai_provider, ai_model, operation_mode, created_at, updated_at)
                VALUES (@directoryPath, @aiProvider, @aiModel, @operationMode, 
                        COALESCE((SELECT created_at FROM directory_configurations WHERE directory_path = @directoryPath), @now), @now)
                """;

            using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@directoryPath", directoryPath);
            command.Parameters.AddWithValue("@aiProvider", aiProvider.ToString());
            command.Parameters.AddWithValue("@aiModel", aiModel);
            command.Parameters.AddWithValue("@operationMode", operationMode.ToString());
            command.Parameters.AddWithValue("@now", now);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            var success = rowsAffected > 0;

            if (success)
            {
                _logger?.LogDebug("Directory configuration saved: {DirectoryPath} -> {Provider}/{Model}/{Mode}", 
                    directoryPath, aiProvider, aiModel, operationMode);
            }
            else
            {
                _logger?.LogWarning("Failed to save directory configuration for: {DirectoryPath}", directoryPath);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving directory configuration for: {DirectoryPath}", directoryPath);
            return false;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets directory-specific configuration
    /// </summary>
    /// <param name="directoryPath">The directory path</param>
    /// <returns>Directory configuration or null if not found</returns>
    public async Task<DirectoryConfiguration?> GetDirectoryConfigurationAsync(string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);

        await _dbSemaphore.WaitAsync();
        try
        {
            if (_connection?.State != System.Data.ConnectionState.Open)
            {
                _connection?.Close();
                _connection?.Open();
            }

            const string sql = """
                SELECT ai_provider, ai_model, operation_mode, created_at, updated_at
                FROM directory_configurations 
                WHERE directory_path = @directoryPath
                """;

            using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@directoryPath", directoryPath);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var providerStr = reader.GetString(0);
                var model = reader.GetString(1);
                var modeStr = reader.GetString(2);
                var createdAt = DateTime.Parse(reader.GetString(3));
                var updatedAt = DateTime.Parse(reader.GetString(4));

                if (Enum.TryParse<AiProviderType>(providerStr, out var provider) &&
                    Enum.TryParse<OperationMode>(modeStr, out var mode))
                {
                    return new DirectoryConfiguration
                    {
                        DirectoryPath = directoryPath,
                        AiProvider = provider,
                        AiModel = model,
                        OperationMode = mode,
                        CreatedAt = createdAt,
                        UpdatedAt = updatedAt
                    };
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting directory configuration for: {DirectoryPath}", directoryPath);
            return null;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets the most recently used directory configuration
    /// </summary>
    /// <returns>Most recent directory configuration or null if none found</returns>
    public async Task<DirectoryConfiguration?> GetMostRecentDirectoryConfigurationAsync()
    {
        await _dbSemaphore.WaitAsync();
        try
        {
            if (_connection?.State != System.Data.ConnectionState.Open)
            {
                _connection?.Close();
                _connection?.Open();
            }

            const string sql = """
                SELECT directory_path, ai_provider, ai_model, operation_mode, created_at, updated_at
                FROM directory_configurations 
                ORDER BY updated_at DESC
                LIMIT 1
                """;

            using var command = new SqliteCommand(sql, _connection);
            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                var directoryPath = reader.GetString(0);
                var providerStr = reader.GetString(1);
                var model = reader.GetString(2);
                var modeStr = reader.GetString(3);
                var createdAt = DateTime.Parse(reader.GetString(4));
                var updatedAt = DateTime.Parse(reader.GetString(5));

                if (Enum.TryParse<AiProviderType>(providerStr, out var provider) &&
                    Enum.TryParse<OperationMode>(modeStr, out var mode))
                {
                    return new DirectoryConfiguration
                    {
                        DirectoryPath = directoryPath,
                        AiProvider = provider,
                        AiModel = model,
                        OperationMode = mode,
                        CreatedAt = createdAt,
                        UpdatedAt = updatedAt
                    };
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting most recent directory configuration");
            return null;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                // Close the connection explicitly before disposing
                if (_connection?.State == System.Data.ConnectionState.Open)
                {
                    _connection.Close();
                }
                _connection?.Dispose();
                
                // Clear the connection pool to release file handles
                if (_connection != null)
                {
                    SqliteConnection.ClearPool(_connection);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing SqliteConfigurationService");
            }
            
            _disposed = true;
        }
    }
}

/// <summary>
/// Configuration database statistics
/// </summary>
public class ConfigurationStats
{
    public required int TotalItems { get; set; }
    public required int TotalCategories { get; set; }
    public DateTime? LastUpdate { get; set; }
    public required string DatabasePath { get; set; }
}

/// <summary>
/// Directory-specific configuration
/// </summary>
public class DirectoryConfiguration
{
    public required string DirectoryPath { get; set; }
    public required AiProviderType AiProvider { get; set; }
    public required string AiModel { get; set; }
    public required OperationMode OperationMode { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
}
