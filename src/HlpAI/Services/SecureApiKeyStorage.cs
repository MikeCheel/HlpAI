using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace HlpAI.Services;

/// <summary>
/// Secure storage for API keys using Windows Data Protection API (DPAPI)
/// </summary>
public class SecureApiKeyStorage
{
    private readonly ILogger? _logger;
    private readonly string _storageDirectory;
    private const string KeyFileExtension = ".key";

    public SecureApiKeyStorage(ILogger? logger = null)
    {
        _logger = logger;
        _storageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HlpAI",
            "SecureKeys"
        );
        
        // Ensure the directory exists
        Directory.CreateDirectory(_storageDirectory);
    }

    /// <summary>
    /// Store an API key securely using DPAPI encryption
    /// </summary>
    /// <param name="providerName">Name of the AI provider (e.g., "OpenAI", "Anthropic")</param>
    /// <param name="apiKey">The API key to store</param>
    /// <returns>True if stored successfully</returns>
    [SupportedOSPlatform("windows")]
    public bool StoreApiKey(string providerName, string apiKey)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(providerName))
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

            if (!OperatingSystem.IsWindows())
            {
                _logger?.LogError("Secure API key storage is only supported on Windows");
                return false;
            }

            var keyBytes = Encoding.UTF8.GetBytes(apiKey);
            var encryptedBytes = ProtectedData.Protect(
                keyBytes,
                GetEntropy(providerName),
                DataProtectionScope.CurrentUser
            );

            var filePath = GetKeyFilePath(providerName);
            File.WriteAllBytes(filePath, encryptedBytes);
            
            _logger?.LogInformation("API key stored securely for provider: {Provider}", providerName);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to store API key for provider: {Provider}", providerName);
            return false;
        }
    }

    /// <summary>
    /// Retrieve an API key securely using DPAPI decryption
    /// </summary>
    /// <param name="providerName">Name of the AI provider</param>
    /// <returns>The decrypted API key or null if not found or decryption failed</returns>
    [SupportedOSPlatform("windows")]
    public string? RetrieveApiKey(string providerName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(providerName))
                return null;

            if (!OperatingSystem.IsWindows())
            {
                _logger?.LogError("Secure API key storage is only supported on Windows");
                return null;
            }

            var filePath = GetKeyFilePath(providerName);
            if (!File.Exists(filePath))
            {
                _logger?.LogDebug("No API key file found for provider: {Provider}", providerName);
                return null;
            }

            var encryptedBytes = File.ReadAllBytes(filePath);
            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                GetEntropy(providerName),
                DataProtectionScope.CurrentUser
            );

            var apiKey = Encoding.UTF8.GetString(decryptedBytes);
            _logger?.LogDebug("API key retrieved successfully for provider: {Provider}", providerName);
            return apiKey;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve API key for provider: {Provider}", providerName);
            return null;
        }
    }

    /// <summary>
    /// Check if an API key exists for the specified provider
    /// </summary>
    /// <param name="providerName">Name of the AI provider</param>
    /// <returns>True if an API key exists</returns>
    public bool HasApiKey(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return false;

        var filePath = GetKeyFilePath(providerName);
        return File.Exists(filePath);
    }

    /// <summary>
    /// Delete the stored API key for the specified provider
    /// </summary>
    /// <param name="providerName">Name of the AI provider</param>
    /// <returns>True if deleted successfully or key didn't exist</returns>
    public bool DeleteApiKey(string providerName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(providerName))
                return false;

            var filePath = GetKeyFilePath(providerName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger?.LogInformation("API key deleted for provider: {Provider}", providerName);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete API key for provider: {Provider}", providerName);
            return false;
        }
    }

    /// <summary>
    /// Get all providers that have stored API keys
    /// </summary>
    /// <returns>List of provider names with stored keys</returns>
    public List<string> GetProvidersWithKeys()
    {
        try
        {
            if (!Directory.Exists(_storageDirectory))
                return new List<string>();

            return Directory.GetFiles(_storageDirectory, $"*{KeyFileExtension}")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList()!;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get providers with stored keys");
            return new List<string>();
        }
    }

    /// <summary>
    /// Clear all stored API keys
    /// </summary>
    /// <returns>True if cleared successfully</returns>
    public bool ClearAllKeys()
    {
        try
        {
            if (Directory.Exists(_storageDirectory))
            {
                var keyFiles = Directory.GetFiles(_storageDirectory, $"*{KeyFileExtension}");
                foreach (var file in keyFiles)
                {
                    File.Delete(file);
                }
                _logger?.LogInformation("All API keys cleared successfully");
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to clear all API keys");
            return false;
        }
    }

    private string GetKeyFilePath(string providerName)
    {
        // Sanitize provider name for file system
        var sanitizedName = string.Join("", providerName.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'));
        return Path.Combine(_storageDirectory, $"{sanitizedName}{KeyFileExtension}");
    }

    private static byte[] GetEntropy(string providerName)
    {
        // Use provider name as additional entropy for encryption
        // This ensures keys for different providers use different encryption
        var entropy = $"HlpAI-{providerName}-Entropy";
        return Encoding.UTF8.GetBytes(entropy);
    }
}