using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HlpAI.Attributes;

namespace HlpAI.Services;

/// <summary>
/// Provides security auditing and monitoring functionality
/// </summary>
[RequiresSecurityValidation]
public class SecurityAuditService : IDisposable
{
    private readonly ILogger<SecurityAuditService>? _logger;
    private readonly SecurityAuditConfiguration _config;
    private readonly List<SecurityEvent> _eventBuffer;
    private readonly object _bufferLock = new();
    private readonly Timer? _flushTimer;
    
    public SecurityAuditService(ILogger<SecurityAuditService>? logger = null, SecurityAuditConfiguration? config = null)
    {
        _logger = logger;
        _config = config ?? new SecurityAuditConfiguration();
        _eventBuffer = new List<SecurityEvent>();
        
        if (_config.EnableBuffering)
        {
            _flushTimer = new Timer(FlushEvents, null, TimeSpan.FromSeconds(_config.FlushIntervalSeconds), 
                TimeSpan.FromSeconds(_config.FlushIntervalSeconds));
        }
    }
    
    /// <summary>
    /// Logs a security event
    /// </summary>
    public void LogSecurityEvent(SecurityEventType eventType, string message, object? details = null, 
        SecurityLevel severity = SecurityLevel.Standard)
    {
        try
        {
            var securityEvent = new SecurityEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                Message = message,
                Details = details != null ? JsonSerializer.Serialize(details) : null,
                Severity = severity,
                Source = Environment.MachineName,
                ProcessId = Environment.ProcessId,
                ThreadId = Environment.CurrentManagedThreadId,
                UserContext = Environment.UserName
            };
            
            // Add to buffer or log immediately
            if (_config.EnableBuffering)
            {
                lock (_bufferLock)
                {
                    _eventBuffer.Add(securityEvent);
                    
                    // Flush if buffer is full
                    if (_eventBuffer.Count >= _config.BufferSize)
                    {
                        FlushEventsInternal();
                    }
                }
            }
            else
            {
                LogEventInternal(securityEvent);
            }
            
            // Log to system logger based on severity
            LogToSystemLogger(securityEvent);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to log security event: {EventType} - {Message}", eventType, message);
        }
    }
    
    /// <summary>
    /// Logs an authentication event
    /// </summary>
    public void LogAuthenticationEvent(string action, bool success, string? userId = null, string? details = null)
    {
        var eventDetails = new
        {
            Action = action,
            Success = success,
            UserId = userId,
            Details = details,
            IpAddress = GetClientIpAddress(),
            UserAgent = GetUserAgent()
        };
        
        var severity = success ? SecurityLevel.Low : SecurityLevel.High;
        var message = $"Authentication {action}: {(success ? "Success" : "Failed")}";
        
        LogSecurityEvent(SecurityEventType.Authentication, message, eventDetails, severity);
    }
    
    /// <summary>
    /// Logs an authorization event
    /// </summary>
    public void LogAuthorizationEvent(string resource, string action, bool granted, string? userId = null)
    {
        var eventDetails = new
        {
            Resource = resource,
            Action = action,
            Granted = granted,
            UserId = userId
        };
        
        var severity = granted ? SecurityLevel.Low : SecurityLevel.Standard;
        var message = $"Authorization for {action} on {resource}: {(granted ? "Granted" : "Denied")}";
        
        LogSecurityEvent(SecurityEventType.Authorization, message, eventDetails, severity);
    }
    
    /// <summary>
    /// Logs a data access event
    /// </summary>
    public void LogDataAccessEvent(string dataType, string operation, bool success, string? userId = null)
    {
        var eventDetails = new
        {
            DataType = dataType,
            Operation = operation,
            Success = success,
            UserId = userId,
            Timestamp = DateTime.UtcNow
        };
        
        var severity = success ? SecurityLevel.Low : SecurityLevel.Standard;
        var message = $"Data access {operation} on {dataType}: {(success ? "Success" : "Failed")}";
        
        LogSecurityEvent(SecurityEventType.DataAccess, message, eventDetails, severity);
    }
    
    /// <summary>
    /// Logs a security violation
    /// </summary>
    public void LogSecurityViolation(string violationType, string description, object? context = null)
    {
        var eventDetails = new
        {
            ViolationType = violationType,
            Description = description,
            Context = context,
            StackTrace = Environment.StackTrace
        };
        
        LogSecurityEvent(SecurityEventType.SecurityViolation, $"Security violation: {violationType}", 
            eventDetails, SecurityLevel.High);
    }
    
    /// <summary>
    /// Logs an API key usage event
    /// </summary>
    public void LogApiKeyUsage(string provider, string operation, bool success, string? keyId = null)
    {
        var eventDetails = new
        {
            Provider = provider,
            Operation = operation,
            Success = success,
            KeyId = keyId != null ? HashString(keyId) : null, // Hash for privacy
            Timestamp = DateTime.UtcNow
        };
        
        var severity = success ? SecurityLevel.Low : SecurityLevel.Standard;
        var message = $"API key {operation} for {provider}: {(success ? "Success" : "Failed")}";
        
        LogSecurityEvent(SecurityEventType.ApiKeyUsage, message, eventDetails, severity);
    }
    
    /// <summary>
    /// Logs a configuration change event
    /// </summary>
    public void LogConfigurationChange(string setting, string? oldValue, string? newValue, string? userId = null)
    {
        var eventDetails = new
        {
            Setting = setting,
            OldValue = SanitizeValue(oldValue),
            NewValue = SanitizeValue(newValue),
            UserId = userId
        };
        
        LogSecurityEvent(SecurityEventType.ConfigurationChange, $"Configuration changed: {setting}", 
            eventDetails, SecurityLevel.Standard);
    }
    
    /// <summary>
    /// Gets security events within a time range
    /// </summary>
    public Task<List<SecurityEvent>> GetSecurityEventsAsync(DateTime? startTime = null, DateTime? endTime = null, 
        SecurityEventType? eventType = null, SecurityLevel? minSeverity = null)
    {
        try
        {
            // Flush any pending events
            if (_config.EnableBuffering)
            {
                FlushEvents(null);
            }
            
            // In a real implementation, this would query from a database or log store
            // For now, return events from buffer (limited functionality)
            lock (_bufferLock)
            {
                var events = _eventBuffer.AsEnumerable();
                
                if (startTime.HasValue)
                {
                    events = events.Where(e => e.Timestamp >= startTime.Value);
                }
                
                if (endTime.HasValue)
                {
                    events = events.Where(e => e.Timestamp <= endTime.Value);
                }
                
                if (eventType.HasValue)
                {
                    events = events.Where(e => e.EventType == eventType.Value);
                }
                
                if (minSeverity.HasValue)
                {
                    events = events.Where(e => e.Severity >= minSeverity.Value);
                }
                
                return Task.FromResult(events.OrderByDescending(e => e.Timestamp).ToList());
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve security events");
            return Task.FromResult(new List<SecurityEvent>());
        }
    }
    
    /// <summary>
    /// Generates a security audit report
    /// </summary>
    public async Task<SecurityAuditReport> GenerateAuditReportAsync(DateTime startTime, DateTime endTime)
    {
        try
        {
            var events = await GetSecurityEventsAsync(startTime, endTime);
            
            var report = new SecurityAuditReport
            {
                StartTime = startTime,
                EndTime = endTime,
                TotalEvents = events.Count,
                EventsByType = events.GroupBy(e => e.EventType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                EventsBySeverity = events.GroupBy(e => e.Severity)
                    .ToDictionary(g => g.Key, g => g.Count()),
                HighSeverityEvents = events.Where(e => e.Severity == SecurityLevel.High).ToList(),
                TopViolations = events.Where(e => e.EventType == SecurityEventType.SecurityViolation)
                    .GroupBy(e => ExtractViolationType(e.Details))
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count()),
                GeneratedAt = DateTime.UtcNow
            };
            
            return report;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate security audit report");
            throw;
        }
    }
    
    /// <summary>
    /// Flushes buffered events to storage
    /// </summary>
    public void FlushEvents(object? state)
    {
        if (_config.EnableBuffering)
        {
            lock (_bufferLock)
            {
                FlushEventsInternal();
            }
        }
    }
    
    /// <summary>
    /// Disposes resources
    /// </summary>
    public void Dispose()
    {
        _flushTimer?.Dispose();
        FlushEvents(null);
    }
    
    /// <summary>
    /// Internal method to flush events
    /// </summary>
    private void FlushEventsInternal()
    {
        if (_eventBuffer.Count == 0)
        {
            return;
        }
        
        try
        {
            foreach (var securityEvent in _eventBuffer)
            {
                LogEventInternal(securityEvent);
            }
            
            _eventBuffer.Clear();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to flush security events");
        }
    }
    
    /// <summary>
    /// Internal method to log a single event
    /// </summary>
    private void LogEventInternal(SecurityEvent securityEvent)
    {
        try
        {
            // In a real implementation, this would write to a secure audit log
            // For now, we'll use the logger
            var eventJson = JsonSerializer.Serialize(securityEvent, new JsonSerializerOptions
            {
                WriteIndented = false
            });
            
            _logger?.LogInformation("SECURITY_AUDIT: {EventJson}", eventJson);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to log security event internally");
        }
    }
    
    /// <summary>
    /// Logs to system logger based on severity
    /// </summary>
    private void LogToSystemLogger(SecurityEvent securityEvent)
    {
        var message = $"[{securityEvent.EventType}] {securityEvent.Message}";
        
        switch (securityEvent.Severity)
        {
            case SecurityLevel.Low:
                _logger?.LogInformation(message);
                break;
            case SecurityLevel.Standard:
                _logger?.LogWarning(message);
                break;
            case SecurityLevel.High:
                _logger?.LogError(message);
                break;
            case SecurityLevel.Critical:
                _logger?.LogCritical(message);
                break;
        }
    }
    
    /// <summary>
    /// Gets client IP address (placeholder implementation)
    /// </summary>
    private static string? GetClientIpAddress()
    {
        // In a web application, this would extract from HttpContext
        return "127.0.0.1";
    }
    
    /// <summary>
    /// Gets user agent (placeholder implementation)
    /// </summary>
    private static string? GetUserAgent()
    {
        // In a web application, this would extract from HttpContext
        return "HlpAI-Client";
    }
    
    /// <summary>
    /// Hashes a string for privacy
    /// </summary>
    private static string HashString(string input)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes)[..8]; // First 8 characters for brevity
    }
    
    /// <summary>
    /// Sanitizes sensitive values for logging
    /// </summary>
    private static string? SanitizeValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }
        
        // Mask sensitive data
        if (value.Length > 10)
        {
            return $"{value[..3]}***{value[^3..]}";
        }
        
        return "***";
    }
    
    /// <summary>
    /// Extracts violation type from event details
    /// </summary>
    private static string ExtractViolationType(string? details)
    {
        if (string.IsNullOrEmpty(details))
        {
            return "Unknown";
        }
        
        try
        {
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(details);
            return json?.TryGetValue("ViolationType", out var type) == true ? type.ToString() ?? "Unknown" : "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}

/// <summary>
/// Security audit configuration
/// </summary>
public class SecurityAuditConfiguration
{
    public bool EnableBuffering { get; set; } = true;
    public int BufferSize { get; set; } = 100;
    public int FlushIntervalSeconds { get; set; } = 30;
    public bool LogToFile { get; set; } = true;
    public string? LogFilePath { get; set; }
    public SecurityLevel MinimumLogLevel { get; set; } = SecurityLevel.Low;
}

/// <summary>
/// Security event types
/// </summary>
public enum SecurityEventType
{
    Authentication,
    Authorization,
    DataAccess,
    ConfigurationChange,
    SecurityViolation,
    ApiKeyUsage,
    SystemAccess,
    DataModification,
    PrivilegeEscalation,
    SuspiciousActivity
}

/// <summary>
/// Security event record
/// </summary>
public class SecurityEvent
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public SecurityEventType EventType { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public SecurityLevel Severity { get; set; }
    public string? Source { get; set; }
    public int ProcessId { get; set; }
    public int ThreadId { get; set; }
    public string? UserContext { get; set; }
}

/// <summary>
/// Security audit report
/// </summary>
public class SecurityAuditReport
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalEvents { get; set; }
    public Dictionary<SecurityEventType, int> EventsByType { get; set; } = new();
    public Dictionary<SecurityLevel, int> EventsBySeverity { get; set; } = new();
    public List<SecurityEvent> HighSeverityEvents { get; set; } = new();
    public Dictionary<string, int> TopViolations { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}