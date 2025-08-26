using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace HlpAI.Services;

/// <summary>
/// Provides comprehensive input validation and sanitization services
/// </summary>
public class SecurityValidationService
{
    private readonly ILogger<SecurityValidationService>? _logger;
    
    // Security patterns for validation
    private static readonly Regex ApiKeyPattern = new(@"^[a-zA-Z0-9_-]{20,}$", RegexOptions.Compiled);
    private static readonly Regex UrlPattern = new(@"^https?:\/\/[a-zA-Z0-9.-]+(?::[0-9]+)?(?:\/[^\s]*)?$", RegexOptions.Compiled);
    private static readonly Regex ModelNamePattern = new(@"^[a-zA-Z0-9._-]{1,100}$", RegexOptions.Compiled);
    private static readonly Regex ProviderNamePattern = new(@"^[a-zA-Z0-9_-]{1,50}$", RegexOptions.Compiled);
    private static readonly Regex FilePathPattern = new(@"^[a-zA-Z0-9\\\/._ -]{1,260}$", RegexOptions.Compiled);
    
    // Dangerous characters and patterns
    private static readonly char[] DangerousChars = { '<', '>', '"', '\'', '&', '\0', '\r', '\n' };
    private static readonly string[] SqlInjectionPatterns = 
    {
        "'", "--", "/*", "*/", "xp_", "sp_", "exec", "execute", "select", "insert", "update", "delete", "drop", "create", "alter", "union"
    };
    
    public SecurityValidationService(ILogger<SecurityValidationService>? logger = null)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Validates and sanitizes an API key
    /// </summary>
    public ValidationResult ValidateApiKey(string? apiKey, string providerName)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ValidationResult(false, "API key cannot be empty");
        }
        
        // Remove whitespace
        apiKey = apiKey.Trim();
        
        // Check length constraints
        if (apiKey.Length < 20 || apiKey.Length > 200)
        {
            _logger?.LogWarning("Invalid API key length for provider {Provider}: {Length}", providerName, apiKey.Length);
            return new ValidationResult(false, "API key must be between 20 and 200 characters");
        }
        
        // Validate pattern
        if (!ApiKeyPattern.IsMatch(apiKey))
        {
            _logger?.LogWarning("Invalid API key format for provider {Provider}", providerName);
            return new ValidationResult(false, "API key contains invalid characters");
        }
        
        // Check for suspicious patterns
        if (ContainsSqlInjectionPatterns(apiKey))
        {
            _logger?.LogWarning("Suspicious API key pattern detected for provider {Provider}", providerName);
            return new ValidationResult(false, "API key contains suspicious patterns");
        }
        
        return new ValidationResult(true, "Valid API key", apiKey);
    }
    
    /// <summary>
    /// Validates and sanitizes a URL
    /// </summary>
    public ValidationResult ValidateUrl(string? url, string context = "URL")
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new ValidationResult(false, $"{context} cannot be empty");
        }
        
        // Remove whitespace
        url = url.Trim();
        
        // Check basic format
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _logger?.LogWarning("Invalid URL format: {Url}", url);
            return new ValidationResult(false, $"Invalid {context} format");
        }
        
        // Validate scheme
        if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && 
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogWarning("Unsupported URL scheme: {Scheme}", uri.Scheme);
            return new ValidationResult(false, $"{context} must use HTTP or HTTPS");
        }
        
        // Validate pattern
        if (!UrlPattern.IsMatch(url))
        {
            _logger?.LogWarning("URL failed pattern validation: {Url}", url);
            return new ValidationResult(false, $"{context} contains invalid characters");
        }
        
        // Check for suspicious patterns
        if (ContainsSqlInjectionPatterns(url))
        {
            _logger?.LogWarning("Suspicious URL pattern detected: {Url}", url);
            return new ValidationResult(false, $"{context} contains suspicious patterns");
        }
        
        return new ValidationResult(true, $"Valid {context}", url);
    }
    
    /// <summary>
    /// Validates and sanitizes a model name
    /// </summary>
    public ValidationResult ValidateModelName(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return new ValidationResult(false, "Model name cannot be empty");
        }
        
        // Remove whitespace
        modelName = modelName.Trim();
        
        // Check length
        if (modelName.Length > 100)
        {
            _logger?.LogWarning("Model name too long: {Length}", modelName.Length);
            return new ValidationResult(false, "Model name must be 100 characters or less");
        }
        
        // Validate pattern
        if (!ModelNamePattern.IsMatch(modelName))
        {
            _logger?.LogWarning("Invalid model name format: {ModelName}", modelName);
            return new ValidationResult(false, "Model name contains invalid characters");
        }
        
        return new ValidationResult(true, "Valid model name", modelName);
    }
    
    /// <summary>
    /// Validates and sanitizes a provider name
    /// </summary>
    public ValidationResult ValidateProviderName(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return new ValidationResult(false, "Provider name cannot be empty");
        }
        
        // Remove whitespace
        providerName = providerName.Trim();
        
        // Check length
        if (providerName.Length > 50)
        {
            _logger?.LogWarning("Provider name too long: {Length}", providerName.Length);
            return new ValidationResult(false, "Provider name must be 50 characters or less");
        }
        
        // Validate pattern
        if (!ProviderNamePattern.IsMatch(providerName))
        {
            _logger?.LogWarning("Invalid provider name format: {ProviderName}", providerName);
            return new ValidationResult(false, "Provider name contains invalid characters");
        }
        
        return new ValidationResult(true, "Valid provider name", providerName);
    }
    
    /// <summary>
    /// Validates and sanitizes a file path
    /// </summary>
    public ValidationResult ValidateFilePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new ValidationResult(false, "File path cannot be empty");
        }
        
        // Remove whitespace
        filePath = filePath.Trim();
        
        // Check length
        if (filePath.Length > 260)
        {
            _logger?.LogWarning("File path too long: {Length}", filePath.Length);
            return new ValidationResult(false, "File path must be 260 characters or less");
        }
        
        // Check for dangerous characters
        if (filePath.IndexOfAny(DangerousChars) >= 0)
        {
            _logger?.LogWarning("File path contains dangerous characters: {FilePath}", filePath);
            return new ValidationResult(false, "File path contains invalid characters");
        }
        
        // Validate pattern
        if (!FilePathPattern.IsMatch(filePath))
        {
            _logger?.LogWarning("Invalid file path format: {FilePath}", filePath);
            return new ValidationResult(false, "File path format is invalid");
        }
        
        // Check for path traversal attempts
        if (filePath.Contains("..") || filePath.Contains("~"))
        {
            _logger?.LogWarning("Path traversal attempt detected: {FilePath}", filePath);
            return new ValidationResult(false, "File path contains path traversal patterns");
        }
        
        return new ValidationResult(true, "Valid file path", filePath);
    }
    
    /// <summary>
    /// Sanitizes general text input
    /// </summary>
    public string SanitizeText(string? input, int maxLength = 1000)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }
        
        // Trim and limit length
        input = input.Trim();
        if (input.Length > maxLength)
        {
            input = input[..maxLength];
        }
        
        // Remove dangerous characters
        var sanitized = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (!DangerousChars.Contains(c) && !char.IsControl(c))
            {
                sanitized.Append(c);
            }
        }
        
        return sanitized.ToString();
    }
    
    /// <summary>
    /// Validates temperature parameter for AI models
    /// </summary>
    public ValidationResult ValidateTemperature(double temperature)
    {
        if (temperature < 0.0 || temperature > 2.0)
        {
            _logger?.LogWarning("Invalid temperature value: {Temperature}", temperature);
            return new ValidationResult(false, "Temperature must be between 0.0 and 2.0");
        }
        
        return new ValidationResult(true, "Valid temperature");
    }
    
    /// <summary>
    /// Validates max tokens parameter for AI models
    /// </summary>
    public ValidationResult ValidateMaxTokens(int maxTokens)
    {
        if (maxTokens < 1 || maxTokens > 100000)
        {
            _logger?.LogWarning("Invalid max tokens value: {MaxTokens}", maxTokens);
            return new ValidationResult(false, "Max tokens must be between 1 and 100,000");
        }
        
        return new ValidationResult(true, "Valid max tokens");
    }
    
    /// <summary>
    /// Checks if input contains SQL injection patterns
    /// </summary>
    private static bool ContainsSqlInjectionPatterns(string input)
    {
        var lowerInput = input.ToLowerInvariant();
        return SqlInjectionPatterns.Any(pattern => lowerInput.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Result of validation operation
/// </summary>
public record ValidationResult(bool IsValid, string Message, string? SanitizedValue = null);