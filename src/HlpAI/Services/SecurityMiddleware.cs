using Microsoft.Extensions.Logging;
using System.Text;
using HlpAI.Attributes;
using HlpAI.Models;

namespace HlpAI.Services;

/// <summary>
/// Provides security middleware functionality for request validation and protection
/// </summary>
public class SecurityMiddleware
{
    private readonly ILogger<SecurityMiddleware>? _logger;
    private readonly SecurityValidationService _validationService;
    private readonly SecurityAuditService _auditService;
    private readonly SecurityConfiguration _config;
    private readonly ICrossPlatformDataProtection _dataProtection;
    private readonly Dictionary<string, List<DateTime>> _rateLimitTracker = new();
    
    public SecurityMiddleware(ILogger<SecurityMiddleware>? logger = null, SecurityConfiguration? config = null)
    {
        _logger = logger;
        var appConfig = ConfigurationService.LoadConfiguration(logger);
        _validationService = new SecurityValidationService(appConfig, logger as ILogger<SecurityValidationService>);
        _auditService = new SecurityAuditService(logger as ILogger<SecurityAuditService>);
        _config = config ?? new SecurityConfiguration();
        _dataProtection = new CrossPlatformDataProtection(appConfig, logger);
    }
    
    public SecurityMiddleware(SecurityValidationService validationService, ILogger<SecurityMiddleware>? logger = null)
    {
        _logger = logger;
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _auditService = new SecurityAuditService(logger as ILogger<SecurityAuditService>);
        _config = new SecurityConfiguration();
        var appConfig = ConfigurationService.LoadConfiguration(logger);
        _dataProtection = new CrossPlatformDataProtection(appConfig, logger);
    }
    
    public SecurityMiddleware(SecurityValidationService validationService, SecurityAuditService auditService, ILogger<SecurityMiddleware>? logger = null, SecurityConfiguration? config = null)
    {
        _logger = logger;
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _config = config ?? new SecurityConfiguration();
        var appConfig = ConfigurationService.LoadConfiguration(logger);
        _dataProtection = new CrossPlatformDataProtection(appConfig, logger);
    }
    
    /// <summary>
    /// Validates a request for security compliance
    /// </summary>
    public SecurityValidationResult ValidateRequest(SecurityRequest request)
    {
        var violations = new List<string>();
        
        try
        {
            // Validate request size
            if (request.ContentLength > _config.MaxRequestSize)
            {
                violations.Add($"Request size ({request.ContentLength}) exceeds maximum allowed ({_config.MaxRequestSize})");
                _logger?.LogWarning("Request size violation: {Size} > {MaxSize}", request.ContentLength, _config.MaxRequestSize);
                _auditService.LogSecurityViolation("RequestSizeExceeded", 
                    $"Request size {request.ContentLength} exceeds maximum {_config.MaxRequestSize}",
                    new { Endpoint = request.Endpoint, ClientId = request.ClientId, Size = request.ContentLength });
            }
            
            // Validate headers
            if (_config.RequireSecurityHeaders)
            {
                ValidateSecurityHeaders(request.Headers, violations);
            }
            
            // Validate content for suspicious patterns
            if (!string.IsNullOrEmpty(request.Content))
            {
                ValidateContent(request.Content, violations);
            }
            
            // Validate parameters
            if (request.Parameters?.Any() == true)
            {
                ValidateParameters(request.Parameters, violations);
            }
            
            // Rate limiting check
            if (_config.EnableRateLimiting)
            {
                var rateLimitResult = CheckRateLimit(request.ClientId, request.Endpoint);
                if (!rateLimitResult.IsAllowed)
                {
                    violations.Add($"Rate limit exceeded: {rateLimitResult.Message}");
                }
            }
            
            var isValid = violations.Count == 0;
            var result = new SecurityValidationResult(isValid, violations, GenerateSecurityHeaders());
            
            if (!isValid)
            {
                _logger?.LogWarning("Security validation failed for request to {Endpoint}: {Violations}", 
                    request.Endpoint, string.Join(", ", violations));
                _auditService.LogSecurityEvent(SecurityEventType.SecurityViolation, 
                    "Security validation failed", 
                    new { Endpoint = request.Endpoint, ClientId = request.ClientId, Violations = violations },
                    SecurityLevel.High);
            }
            else
            {
                _auditService.LogSecurityEvent(SecurityEventType.SystemAccess, 
                    "Security validation passed", 
                    new { Endpoint = request.Endpoint, ClientId = request.ClientId },
                    SecurityLevel.Low);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during security validation for request to {Endpoint}", request.Endpoint);
            violations.Add("Internal security validation error");
            _auditService.LogSecurityEvent(SecurityEventType.SecurityViolation, 
                "Security validation error", 
                new { Endpoint = request.Endpoint, ClientId = request.ClientId, Error = ex.Message },
                SecurityLevel.Critical);
            return new SecurityValidationResult(false, violations, GenerateSecurityHeaders());
        }
    }
    
    /// <summary>
    /// Generates security headers for responses
    /// </summary>
    public Dictionary<string, string> GenerateSecurityHeaders()
    {
        var headers = new Dictionary<string, string>();
        
        if (_config.AddSecurityHeaders)
        {
            // Content Security Policy
            headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:";
            
            // X-Frame-Options
            headers["X-Frame-Options"] = "DENY";
            
            // X-Content-Type-Options
            headers["X-Content-Type-Options"] = "nosniff";
            
            // X-XSS-Protection
            headers["X-XSS-Protection"] = "1; mode=block";
            
            // Referrer Policy
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            
            // Strict Transport Security (if HTTPS)
            if (_config.UseHttpsOnly)
            {
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            }
            
            // Permissions Policy
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        }
        
        return headers;
    }
    
    /// <summary>
    /// Sanitizes input data for safe processing
    /// </summary>
    public string SanitizeInput(string? input, SanitizationOptions? options = null)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        
        options ??= new SanitizationOptions();
        
        var sanitized = input;
        
        // Apply length limit first
        if (sanitized.Length > options.MaxLength)
        {
            sanitized = sanitized[..options.MaxLength];
        }
        
        // Additional sanitization based on options
        if (options.RemoveHtml)
        {
            sanitized = RemoveHtmlTags(sanitized);
        }
        
        if (options.EscapeSpecialChars)
        {
            sanitized = EscapeSpecialCharacters(sanitized);
        }
        else if (!options.RemoveHtml)
        {
            // Only use basic sanitization if we're not removing HTML or escaping special chars
            sanitized = _validationService.SanitizeText(sanitized, options.MaxLength);
        }
        
        return sanitized;
    }
    
    /// <summary>
    /// Encrypts sensitive data for storage
    /// </summary>
    public string EncryptSensitiveData(string data, string context = "default")
    {
        if (string.IsNullOrEmpty(data))
        {
            return string.Empty;
        }
        
        try
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var entropy = Encoding.UTF8.GetBytes($"HlpAI-Security-{context}");
            
            var result = _dataProtection.Protect(dataBytes, entropy);
            
            _auditService.LogSecurityEvent(SecurityEventType.DataAccess, 
                "Sensitive data encrypted", 
                new { Context = context, DataLength = data.Length },
                SecurityLevel.Standard);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to encrypt sensitive data for context {Context}", context);
            _auditService.LogSecurityEvent(SecurityEventType.SecurityViolation, 
                "Failed to encrypt sensitive data", 
                new { Context = context, Error = ex.Message },
                SecurityLevel.Critical);
            throw new SecurityException($"Failed to encrypt sensitive data: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Decrypts sensitive data from storage
    /// </summary>
    public string DecryptSensitiveData(string encryptedData, string context = "default")
    {
        if (string.IsNullOrEmpty(encryptedData))
        {
            return string.Empty;
        }
        
        try
        {
            var entropy = Encoding.UTF8.GetBytes($"HlpAI-Security-{context}");
            
            var decryptedBytes = _dataProtection.Unprotect(encryptedData, entropy);
            var result = Encoding.UTF8.GetString(decryptedBytes);
            
            _auditService.LogSecurityEvent(SecurityEventType.DataAccess, 
                "Sensitive data decrypted", 
                new { Context = context, EncryptedDataLength = encryptedData.Length },
                SecurityLevel.Standard);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to decrypt sensitive data for context {Context}", context);
            _auditService.LogSecurityEvent(SecurityEventType.SecurityViolation, 
                "Failed to decrypt sensitive data", 
                new { Context = context, Error = ex.Message },
                SecurityLevel.Critical);
            throw new SecurityException($"Failed to decrypt sensitive data: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Validates security headers in the request
    /// </summary>
    private void ValidateSecurityHeaders(Dictionary<string, string>? headers, List<string> violations)
    {
        if (headers == null)
        {
            return;
        }
        
        // Check for required headers
        if (_config.RequiredHeaders?.Any() == true)
        {
            foreach (var requiredHeader in _config.RequiredHeaders)
            {
                if (!headers.ContainsKey(requiredHeader))
                {
                    violations.Add($"Missing required header: {requiredHeader}");
                }
            }
        }
        
        // Validate User-Agent if present
        if (headers.TryGetValue("User-Agent", out var userAgent))
        {
            if (string.IsNullOrWhiteSpace(userAgent) || userAgent.Length > 500)
            {
                violations.Add("Invalid User-Agent header");
            }
        }
    }
    
    /// <summary>
    /// Validates request content for suspicious patterns
    /// </summary>
    private void ValidateContent(string content, List<string> violations)
    {
        // Check for script injection
        if (content.Contains("<script", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("javascript:", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add("Potential script injection detected");
            _auditService.LogSecurityViolation("ScriptInjectionAttempt", 
                "Potential script injection detected in request content",
                new { ContentLength = content.Length, SuspiciousContent = content.Substring(0, Math.Min(100, content.Length)) });
        }
        
        // Check for SQL injection patterns
        var sqlPatterns = new[] { "union select", "drop table", "exec(", "execute(", "xp_cmdshell" };
        var lowerContent = content.ToLowerInvariant();
        
        foreach (var pattern in sqlPatterns)
        {
            if (lowerContent.Contains(pattern))
            {
                violations.Add($"Potential SQL injection pattern detected: {pattern}");
                _auditService.LogSecurityViolation("SqlInjectionAttempt", 
                    $"Potential SQL injection pattern detected: {pattern}",
                    new { Pattern = pattern, ContentLength = content.Length, SuspiciousContent = content.Substring(0, Math.Min(100, content.Length)) });
                break;
            }
        }
        
        // Check for excessive length
        if (content.Length > _config.MaxContentLength)
        {
            violations.Add("Content too long");
            _auditService.LogSecurityViolation("ExcessiveContentLength", 
                $"Content length {content.Length} exceeds maximum {_config.MaxContentLength}",
                new { ContentLength = content.Length, MaxAllowed = _config.MaxContentLength });
        }
    }
    
    /// <summary>
    /// Validates request parameters
    /// </summary>
    private void ValidateParameters(Dictionary<string, string> parameters, List<string> violations)
    {
        foreach (var param in parameters)
        {
            // Validate parameter name
            if (string.IsNullOrWhiteSpace(param.Key) || param.Key.Length > 100)
            {
                violations.Add($"Invalid parameter name: {param.Key}");
                continue;
            }
            
            // Check for dangerous characters in parameter name
            if (_validationService.ContainsDangerousCharacters(param.Key))
            {
                violations.Add($"Invalid parameter name: {param.Key}");
                continue;
            }
            
            // Validate parameter value
            if (param.Value?.Length > _config.MaxParameterLength)
            {
                violations.Add($"Parameter '{param.Key}' value too long");
            }
            
            // Check for suspicious patterns in values
            if (!string.IsNullOrEmpty(param.Value))
            {
                var sanitizedValue = _validationService.SanitizeText(param.Value);
                var hasDangerousChars = sanitizedValue != param.Value;
                var hasPathTraversal = _validationService.ContainsPathTraversal(param.Value);
                
                if (hasDangerousChars || hasPathTraversal)
                {
                    violations.Add($"Parameter '{param.Key}' contains suspicious pattern");
                    _auditService.LogSecurityViolation("SuspiciousParameterValue", 
                        $"Parameter '{param.Key}' contains suspicious pattern",
                        new { ParameterName = param.Key, OriginalValue = param.Value.Substring(0, Math.Min(50, param.Value.Length)), SanitizedValue = sanitizedValue.Substring(0, Math.Min(50, sanitizedValue.Length)), HasPathTraversal = hasPathTraversal });
                }
            }
        }
    }
    
    /// <summary>
    /// Checks rate limiting for a client and endpoint
    /// </summary>
    private RateLimitResult CheckRateLimit(string? clientId, string? endpoint)
    {
        // Simple in-memory rate limiting (in production, use Redis or similar)
        // This is a basic implementation for demonstration
        
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(endpoint))
        {
            _auditService.LogSecurityEvent(SecurityEventType.SystemAccess, 
                "Rate limit check skipped - missing client ID or endpoint", 
                new { ClientId = clientId, Endpoint = endpoint },
                SecurityLevel.Low);
            return new RateLimitResult(true, "No rate limiting applied");
        }
        
        var key = $"{clientId}:{endpoint}";
        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-1); // 1-minute window
        var maxRequests = 1; // Allow only 1 request per minute for testing
        
        // Clean up old entries and get current requests in window
        if (!_rateLimitTracker.ContainsKey(key))
        {
            _rateLimitTracker[key] = new List<DateTime>();
        }
        
        var requests = _rateLimitTracker[key];
        requests.RemoveAll(r => r < windowStart);
        
        // Check if limit exceeded
        if (requests.Count >= maxRequests)
        {
            _auditService.LogSecurityEvent(SecurityEventType.SecurityViolation, 
                "Rate limit exceeded", 
                new { ClientId = clientId, Endpoint = endpoint, RequestCount = requests.Count, MaxRequests = maxRequests },
                SecurityLevel.High);
            return new RateLimitResult(false, $"Rate limit exceeded: {requests.Count}/{maxRequests} requests in the last minute");
        }
        
        // Add current request
        requests.Add(now);
        
        _auditService.LogSecurityEvent(SecurityEventType.SystemAccess, 
            "Rate limit check passed", 
            new { ClientId = clientId, Endpoint = endpoint, RequestCount = requests.Count, MaxRequests = maxRequests },
            SecurityLevel.Low);
        return new RateLimitResult(true, "Rate limit check passed");
    }
    
    /// <summary>
    /// Removes HTML tags from input
    /// </summary>
    private static string RemoveHtmlTags(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        
        // Simple HTML tag removal (for production, use a proper HTML sanitizer)
        return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);
    }
    
    /// <summary>
    /// Escapes special characters
    /// </summary>
    private static string EscapeSpecialCharacters(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        
        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#x27;");
    }
}

/// <summary>
/// Security configuration options
/// </summary>
public class SecurityConfiguration
{
    public bool AddSecurityHeaders { get; set; } = true;
    public bool RequireSecurityHeaders { get; set; } = false;
    public bool UseHttpsOnly { get; set; } = true;
    public bool EnableRateLimiting { get; set; } = true;
    public long MaxRequestSize { get; set; } = 10 * 1024 * 1024; // 10MB
    public int MaxContentLength { get; set; } = 1024 * 1024; // 1MB
    public int MaxParameterLength { get; set; } = 1000;
    public string[]? RequiredHeaders { get; set; }

    /// <summary>
    /// Creates a SecurityConfiguration from AppConfiguration
    /// </summary>
    public static SecurityConfiguration FromAppConfiguration(HlpAI.Models.AppConfiguration appConfig)
    {
        return new SecurityConfiguration
        {
            MaxRequestSize = appConfig.MaxRequestSizeBytes,
            MaxContentLength = appConfig.MaxContentLengthBytes
        };
    }
}

/// <summary>
/// Security request information
/// </summary>
public class SecurityRequest
{
    public string? Endpoint { get; set; }
    public string? ClientId { get; set; }
    public long ContentLength { get; set; }
    public string? Content { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
}

/// <summary>
/// Security validation result
/// </summary>
public record SecurityValidationResult(bool IsValid, List<string> Violations, Dictionary<string, string> SecurityHeaders);

/// <summary>
/// Rate limiting result
/// </summary>
public record RateLimitResult(bool IsAllowed, string Message);

/// <summary>
/// Sanitization options
/// </summary>
public class SanitizationOptions
{
    public int MaxLength { get; set; } = 1000;
    public bool RemoveHtml { get; set; } = true;
    public bool EscapeSpecialChars { get; set; } = true;
}

/// <summary>
/// Security exception for security-related errors
/// </summary>
public class SecurityException : Exception
{
    public SecurityException(string message) : base(message) { }
    public SecurityException(string message, Exception innerException) : base(message, innerException) { }
}