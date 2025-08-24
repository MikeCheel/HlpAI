using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using HlpAI.Models;

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

    public SqliteConfigurationService(ILogger? logger = null)
    {
        _logger = logger;
        
        // Create database in user's home directory
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var hlpAiDirectory = Path.Combine(homeDirectory, ".hlpai");
        
        if (!Directory.Exists(hlpAiDirectory))
        {
            Directory.CreateDirectory(hlpAiDirectory);
            _logger?.LogInformation("Created HlpAI configuration directory at {Directory}", hlpAiDirectory);
        }
        
        _dbPath = Path.Combine(hlpAiDirectory, "config.db");
        
        // Initialize connection and database with better connection settings
        var connectionString = $"Data Source={_dbPath};Cache=Shared;";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        InitializeDatabase();
        
        _logger?.LogInformation("SqliteConfigurationService initialized with database at {DbPath}", _dbPath);
    }

    public SqliteConfigurationService(string customDbPath, ILogger? logger = null)
    {
        _logger = logger;
        _dbPath = customDbPath ?? throw new ArgumentNullException(nameof(customDbPath));
        
        // Ensure the directory for the custom database path exists
        var directory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Initialize connection and database with better connection settings
        var connectionString = $"Data Source={_dbPath};Cache=Shared;";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        InitializeDatabase();
        
        _logger?.LogInformation("SqliteConfigurationService initialized with database at {DbPath}", _dbPath);
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

        try
        {
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
            
            _logger?.LogDebug("Set configuration: {Category}.{Key} = {Value}, rows affected: {Rows}", 
                category, key, value, rowsAffected);
            
            // Immediately verify the change was persisted
            if (rowsAffected > 0)
            {
                const string verifySql = "SELECT value FROM configuration WHERE key = @key AND category = @category";
                using var verifyCommand = new SqliteCommand(verifySql, _connection);
                verifyCommand.Parameters.AddWithValue("@key", key);
                verifyCommand.Parameters.AddWithValue("@category", category);
                
                var verifyResult = await verifyCommand.ExecuteScalarAsync();
                _logger?.LogDebug("Verification result for {Category}.{Key}: {Result}", category, key, verifyResult);
            }
            
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error setting configuration {Category}.{Key}", category, key);
            return false;
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

        try
        {
            const string sql = "SELECT value FROM configuration WHERE key = @key AND category = @category";

            using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@category", category);

            var result = await command.ExecuteScalarAsync();
            
            if (result == null || result == DBNull.Value)
            {
                _logger?.LogDebug("Configuration not found: {Category}.{Key}, returning default: {Default}", 
                    category, key, defaultValue);
                return defaultValue;
            }

            var value = result.ToString();
            _logger?.LogDebug("Retrieved configuration: {Category}.{Key} = {Value}", category, key, value);
            return value;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting configuration {Category}.{Key}", category, key);
            return defaultValue;
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

    private void InitializeDatabase()
    {
        // Set PRAGMA statements for better transaction handling
        using var pragmaCommand = new SqliteCommand("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;", _connection);
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
            """;

        using var command = new SqliteCommand(createTableSql, _connection);
        command.ExecuteNonQuery();
        
        _logger?.LogDebug("Database table 'configuration' initialized");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                _connection?.Dispose();
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
