using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace HlpAI.Services;

/// <summary>
/// Service for detecting and managing hh.exe executable location
/// </summary>
public class HhExeDetectionService : IDisposable
{
    private readonly ILogger? _logger;
    private readonly SqliteConfigurationService _configService;
    private readonly SqliteConnection _connection;
    private bool _disposed = false;

    public HhExeDetectionService(ILogger? logger = null)
        : this(SqliteConfigurationService.GetInstance(logger), logger)
    {
    }

    public HhExeDetectionService(SqliteConfigurationService configService, ILogger? logger = null)
    {
        _logger = logger;
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        
        // Get connection from the configuration service for detection history
        var connectionString = $"Data Source={_configService.DatabasePath}";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        
        InitializeDatabase();
        
        _logger?.LogInformation("HhExeDetectionService initialized with unified configuration database");
    }

    /// <summary>
    /// Checks if hh.exe exists at the default Windows location
    /// </summary>
    /// <returns>True if hh.exe is found at the default location, false otherwise</returns>
    public async Task<bool> CheckDefaultLocationAsync()
    {
        const string defaultPath = @"C:\Windows\hh.exe";
        
        _logger?.LogInformation("Checking for hh.exe at default location: {DefaultPath}", defaultPath);
        
        var exists = File.Exists(defaultPath);
        
        if (exists)
        {
            _logger?.LogInformation("hh.exe found at default location: {DefaultPath}", defaultPath);
            
            // Always update the configuration when hh.exe is found at the default location
            await _configService.SetConfigurationAsync("hh_exe_path", defaultPath, "system");
            await _configService.SetConfigurationBoolAsync("hh_exe_auto_detected", true, "system");
            
            // Store the successful detection in the database
            await StoreDetectionResultAsync(defaultPath, true, "Found at default Windows location");
        }
        else
        {
            _logger?.LogWarning("hh.exe not found at default location: {DefaultPath}", defaultPath);
            
            // Store the failed detection in the database
            await StoreDetectionResultAsync(defaultPath, false, "Not found at default Windows location");
        }
        
        return exists;
    }

    /// <summary>
    /// Gets the configured hh.exe path from the configuration database
    /// </summary>
    /// <returns>The configured path or null if not set</returns>
    public async Task<string?> GetConfiguredHhExePathAsync()
    {
        return await _configService.GetConfigurationAsync("hh_exe_path", "system");
    }

    /// <summary>
    /// Sets the hh.exe path in the configuration database
    /// </summary>
    /// <param name="path">Path to hh.exe</param>
    /// <param name="autoDetected">Whether this was auto-detected or manually set</param>
    /// <returns>True if successful</returns>
    public async Task<bool> SetHhExePathAsync(string? path, bool autoDetected = false)
    {
        var result = await _configService.SetConfigurationAsync("hh_exe_path", path, "system");
        if (result)
        {
            await _configService.SetConfigurationBoolAsync("hh_exe_auto_detected", autoDetected, "system");
        }
        return result;
    }

    /// <summary>
    /// Checks if the configured hh.exe path was auto-detected
    /// </summary>
    /// <returns>True if auto-detected, false if manually configured</returns>
    public async Task<bool> IsHhExePathAutoDetectedAsync()
    {
        return await _configService.GetConfigurationBoolAsync("hh_exe_auto_detected", "system", false);
    }

    /// <summary>
    /// Gets the path to hh.exe if it exists at the default location
    /// </summary>
    /// <returns>The path to hh.exe if found, null otherwise</returns>
    public async Task<string?> GetDefaultHhExePathAsync()
    {
        const string defaultPath = @"C:\Windows\hh.exe";
        
        if (await CheckDefaultLocationAsync())
        {
            return defaultPath;
        }
        
        return null;
    }

    /// <summary>
    /// Gets the detection history from the database
    /// </summary>
    /// <returns>List of detection attempts with their results</returns>
    public async Task<List<HhExeDetectionResult>> GetDetectionHistoryAsync()
    {
        const string sql = """
            SELECT path, found, notes, detected_at 
            FROM hh_exe_detections 
            ORDER BY detected_at DESC 
            LIMIT 100
            """;

        var results = new List<HhExeDetectionResult>();
        
        using var command = new SqliteCommand(sql, _connection);
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            results.Add(new HhExeDetectionResult
            {
                Path = reader.GetString(0),
                Found = reader.GetBoolean(1),
                Notes = reader.IsDBNull(2) ? null : reader.GetString(2),
                DetectedAt = DateTime.Parse(reader.GetString(3))
            });
        }
        
        return results;
    }

    /// <summary>
    /// Gets the most recent successful detection result
    /// </summary>
    /// <returns>The most recent successful detection, or null if none found</returns>
    public async Task<HhExeDetectionResult?> GetLastSuccessfulDetectionAsync()
    {
        const string sql = """
            SELECT path, found, notes, detected_at 
            FROM hh_exe_detections 
            WHERE found = 1 
            ORDER BY detected_at DESC 
            LIMIT 1
            """;

        using var command = new SqliteCommand(sql, _connection);
        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return new HhExeDetectionResult
            {
                Path = reader.GetString(0),
                Found = reader.GetBoolean(1),
                Notes = reader.IsDBNull(2) ? null : reader.GetString(2),
                DetectedAt = DateTime.Parse(reader.GetString(3))
            };
        }
        
        return null;
    }

    /// <summary>
    /// Clears all detection history from the database
    /// </summary>
    /// <returns>Number of records deleted</returns>
    public async Task<int> ClearDetectionHistoryAsync()
    {
        const string sql = "DELETE FROM hh_exe_detections";
        
        using var command = new SqliteCommand(sql, _connection);
        var deletedCount = await command.ExecuteNonQueryAsync();
        
        _logger?.LogInformation("Cleared {DeletedCount} detection history records", deletedCount);
        
        return deletedCount;
    }

    private void InitializeDatabase()
    {
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS hh_exe_detections (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                path TEXT NOT NULL,
                found BOOLEAN NOT NULL,
                notes TEXT,
                detected_at TEXT NOT NULL
            )
            """;

        using var command = new SqliteCommand(createTableSql, _connection);
        command.ExecuteNonQuery();
        
        _logger?.LogDebug("Database table 'hh_exe_detections' initialized");
    }

    private async Task StoreDetectionResultAsync(string path, bool found, string? notes = null)
    {
        const string sql = """
            INSERT INTO hh_exe_detections (path, found, notes, detected_at) 
            VALUES (@path, @found, @notes, @detected_at)
            """;

        using var command = new SqliteCommand(sql, _connection);
        command.Parameters.AddWithValue("@path", path);
        command.Parameters.AddWithValue("@found", found);
        command.Parameters.AddWithValue("@notes", notes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@detected_at", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        
        await command.ExecuteNonQueryAsync();
        
        _logger?.LogDebug("Stored detection result: Path={Path}, Found={Found}, Notes={Notes}", 
            path, found, notes);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                _connection?.Dispose();
                _configService?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing HhExeDetectionService");
            }
            
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents a result of hh.exe detection attempt
/// </summary>
public class HhExeDetectionResult
{
    public required string Path { get; set; }
    public required bool Found { get; set; }
    public string? Notes { get; set; }
    public required DateTime DetectedAt { get; set; }
}
