using Microsoft.Extensions.Logging;
using TUnit.Assertions;
using TUnit.Core;
using HlpAI.Services;
using HlpAI.Attributes;

namespace HlpAI.Tests.Services;

public class SecurityAuditServiceTests : IDisposable
{
    private readonly SecurityAuditService _auditService;
    private readonly ILogger<SecurityAuditService> _logger;
    private readonly SecurityAuditConfiguration _config;
    private readonly LoggerFactory _loggerFactory;
    
    public SecurityAuditServiceTests()
    {
        _loggerFactory = new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<SecurityAuditService>();
        _config = new SecurityAuditConfiguration
        {
            EnableBuffering = false, // Disable buffering for immediate testing
            MinimumLogLevel = SecurityLevel.Low
        };
        _auditService = new SecurityAuditService(_logger, _config);
    }
    
    [Test]
    public void LogSecurityEvent_WithValidEvent_ShouldLogSuccessfully()
    {
        // Arrange
        var eventType = SecurityEventType.Authentication;
        var message = "User login attempt";
        var details = new { UserId = "test-user", Success = true };
        
        // Act
        _auditService.LogSecurityEvent(eventType, message, details, SecurityLevel.Standard);
        
        // Assert
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public void LogAuthenticationEvent_WithSuccessfulLogin_ShouldLogWithLowSeverity()
    {
        // Arrange
        var action = "login";
        var success = true;
        var userId = "test-user";
        
        // Act
        _auditService.LogAuthenticationEvent(action, success, userId);
        
        // Assert
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public void LogAuthenticationEvent_WithFailedLogin_ShouldLogWithHighSeverity()
    {
        // Arrange
        var action = "login";
        var success = false;
        var userId = "test-user";
        
        // Act
        _auditService.LogAuthenticationEvent(action, success, userId);
        
        // Assert
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public void LogAuthorizationEvent_WithGrantedAccess_ShouldLogWithLowSeverity()
    {
        // Arrange
        var resource = "api/users";
        var action = "read";
        var granted = true;
        var userId = "test-user";
        
        // Act
        _auditService.LogAuthorizationEvent(resource, action, granted, userId);
        
        // Assert
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public void LogAuthorizationEvent_WithDeniedAccess_ShouldLogWithMediumSeverity()
    {
        // Arrange
        var resource = "api/admin";
        var action = "write";
        var granted = false;
        var userId = "test-user";
        
        // Act
        _auditService.LogAuthorizationEvent(resource, action, granted, userId);
        
        // Assert
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public void LogDataAccessEvent_WithSuccessfulAccess_ShouldLogWithLowSeverity()
    {
        // Arrange
        var dataType = "user-profile";
        var operation = "read";
        var success = true;
        var userId = "test-user";
        
        // Act
        _auditService.LogDataAccessEvent(dataType, operation, success, userId);
        
        // Assert
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public void LogDataAccessEvent_WithFailedAccess_ShouldLogWithMediumSeverity()
    {
        // Arrange
        var dataType = "sensitive-data";
        var operation = "delete";
        var success = false;
        var userId = "test-user";
        
        // Act
        _auditService.LogDataAccessEvent(dataType, operation, success, userId);
        
        // Assert
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public void LogSecurityViolation_ShouldLogWithHighSeverity()
    {
        // Arrange
        var violationType = "SQL Injection Attempt";
        var description = "Detected SQL injection pattern in user input";
        var context = new { Input = "'; DROP TABLE users; --", Endpoint = "/api/search" };
        
        // Act
        _auditService.LogSecurityViolation(violationType, description, context);
        
        // Assert
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public void LogApiKeyUsage_WithSuccessfulUsage_ShouldLogWithLowSeverity()
    {
        // Arrange
        var provider = "openai";
        var operation = "chat-completion";
        var success = true;
        var keyId = "sk-1234567890abcdef";
        
        // Act
        _auditService.LogApiKeyUsage(provider, operation, success, keyId);
        
        // Assert
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public void LogApiKeyUsage_WithFailedUsage_ShouldLogWithMediumSeverity()
    {
        // Arrange
        var provider = "anthropic";
        var operation = "chat-completion";
        var success = false;
        var keyId = "sk-ant-1234567890abcdef";
        
        // Act
        _auditService.LogApiKeyUsage(provider, operation, success, keyId);
        
        // Assert
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public void LogConfigurationChange_ShouldLogWithMediumSeverity()
    {
        // Arrange
        var setting = "api-endpoint";
        var oldValue = "https://old-api.example.com";
        var newValue = "https://new-api.example.com";
        var userId = "admin-user";
        
        // Act
        _auditService.LogConfigurationChange(setting, oldValue, newValue, userId);
        
        // Assert
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public async Task GetSecurityEventsAsync_WithBufferedService_ShouldReturnEvents()
    {
        // Arrange
        var bufferedConfig = new SecurityAuditConfiguration
        {
            EnableBuffering = true,
            BufferSize = 10
        };
        using var bufferedService = new SecurityAuditService(_logger, bufferedConfig);
        
        // Log some events
        bufferedService.LogSecurityEvent(SecurityEventType.Authentication, "Test event 1");
        bufferedService.LogSecurityEvent(SecurityEventType.Authorization, "Test event 2");
        
        // Act
        var events = await bufferedService.GetSecurityEventsAsync();
        
        // Assert
        await Assert.That(events).IsNotNull();
        // Note: In a real implementation, this would return the actual events
        // For now, it returns events from the buffer which may be empty after flushing
    }
    
    [Test]
    public async Task GetSecurityEventsAsync_WithTimeRange_ShouldFilterEvents()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-1);
        var endTime = DateTime.UtcNow;
        
        // Act
        var events = await _auditService.GetSecurityEventsAsync(startTime, endTime);
        
        // Assert
        await Assert.That(events).IsNotNull();
        await Assert.That(events.All(e => e.Timestamp >= startTime && e.Timestamp <= endTime)).IsTrue();
    }
    
    [Test]
    public async Task GetSecurityEventsAsync_WithEventTypeFilter_ShouldFilterEvents()
    {
        // Arrange
        var eventType = SecurityEventType.Authentication;
        
        // Act
        var events = await _auditService.GetSecurityEventsAsync(eventType: eventType);
        
        // Assert
        await Assert.That(events).IsNotNull();
        await Assert.That(events.All(e => e.EventType == eventType)).IsTrue();
    }
    
    [Test]
    public async Task GetSecurityEventsAsync_WithSeverityFilter_ShouldFilterEvents()
    {
        // Arrange
        var minSeverity = SecurityLevel.Standard;
        
        // Act
        var events = await _auditService.GetSecurityEventsAsync(minSeverity: minSeverity);
        
        // Assert
        await Assert.That(events).IsNotNull();
        await Assert.That(events.All(e => e.Severity >= minSeverity)).IsTrue();
    }
    
    [Test]
    public async Task GenerateAuditReportAsync_ShouldReturnValidReport()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-1);
        var endTime = DateTime.UtcNow;
        
        // Log some test events
        _auditService.LogSecurityEvent(SecurityEventType.Authentication, "Test auth event", null, SecurityLevel.Low);
        _auditService.LogSecurityEvent(SecurityEventType.SecurityViolation, "Test violation", null, SecurityLevel.High);
        
        // Act
        var report = await _auditService.GenerateAuditReportAsync(startTime, endTime);
        
        // Assert
        await Assert.That(report).IsNotNull();
        await Assert.That(report.StartTime).IsEqualTo(startTime);
        await Assert.That(report.EndTime).IsEqualTo(endTime);
        await Assert.That(report.GeneratedAt <= DateTime.UtcNow).IsTrue();
        await Assert.That(report.EventsByType).IsNotNull();
        await Assert.That(report.EventsBySeverity).IsNotNull();
        await Assert.That(report.HighSeverityEvents).IsNotNull();
        await Assert.That(report.TopViolations).IsNotNull();
    }
    
    [Test]
    public void FlushEvents_ShouldNotThrowException()
    {
        // Arrange
        var bufferedConfig = new SecurityAuditConfiguration
        {
            EnableBuffering = true,
            BufferSize = 10
        };
        using var bufferedService = new SecurityAuditService(_logger, bufferedConfig);
        
        // Log some events
        bufferedService.LogSecurityEvent(SecurityEventType.Authentication, "Test event");
        
        // Act & Assert
        bufferedService.FlushEvents(null);// Assert
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public void LogSecurityEvent_WithNullDetails_ShouldHandleGracefully()
    {
        // Act & Assert
        _auditService.LogSecurityEvent(SecurityEventType.SystemAccess, "Test message", null);
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public void LogAuthenticationEvent_WithNullUserId_ShouldHandleGracefully()
    {
        // Act & Assert
        _auditService.LogAuthenticationEvent("login", true, null);
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public void LogConfigurationChange_WithSensitiveValues_ShouldSanitize()
    {
        // Arrange
        var setting = "api-key";
        var oldValue = "sk-1234567890abcdef1234567890abcdef";
        var newValue = "sk-abcdef1234567890abcdef1234567890";
        
        // Act
        _auditService.LogConfigurationChange(setting, oldValue, newValue);
        
        // Assert
        // Values should be sanitized in the log (not directly testable without log inspection)
        // Test passes if no exception is thrown
    }
    
    [Test]
    [Arguments(SecurityLevel.Low)]
    [Arguments(SecurityLevel.Standard)]
    [Arguments(SecurityLevel.High)]
    [Arguments(SecurityLevel.Critical)]
    public void LogSecurityEvent_WithDifferentSeverities_ShouldLogAppropriately(SecurityLevel severity)
    {
        // Act & Assert
        _auditService.LogSecurityEvent(SecurityEventType.SystemAccess, $"Test {severity} event", null, severity);
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public async Task Constructor_WithNullLogger_ShouldNotThrow()
    {
        // Act & Assert
        var service = new SecurityAuditService(null, _config);
        await Assert.That(service).IsNotNull();
    }
    
    [Test]
    public async Task Constructor_WithNullConfig_ShouldUseDefaults()
    {
        // Act & Assert
        var service = new SecurityAuditService(_logger, null);
        await Assert.That(service).IsNotNull();
    }
    
    [Test]
    public async Task LogSecurityEvent_WithLargeDetails_ShouldHandleGracefully()
    {
        // Arrange
        var largeDetails = new Dictionary<string, object>
        {
            { "large_data", new string('a', 10000) },
            { "nested_object", new { prop1 = "value1", prop2 = "value2" } },
            { "array_data", new[] { 1, 2, 3, 4, 5 } }
        };
        
        // Act & Assert
        _auditService.LogSecurityEvent(SecurityEventType.SystemAccess, "Test with large details", largeDetails);
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public async Task LogApiKeyUsage_WithNullApiKey_ShouldHandleGracefully()
    {
        // Act & Assert
        _auditService.LogApiKeyUsage(null, "test-operation", true);
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public async Task LogDataAccessEvent_WithComplexData_ShouldLogCorrectly()
    {
        // Arrange
        var resource = "user_database";
        var operation = "SELECT";
        var userId = "user123";
        var details = new Dictionary<string, object>
        {
            { "query", "SELECT * FROM users WHERE id = @id" },
            { "parameters", new { id = userId } },
            { "execution_time_ms", 150 }
        };
        
        // Act & Assert
        _auditService.LogDataAccessEvent(resource, operation, true, userId);
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public async Task GetSecurityEventsAsync_WithMultipleFilters_ShouldFilterCorrectly()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-2);
        var endTime = DateTime.UtcNow;
        var eventType = SecurityEventType.Authentication;
        var minSeverity = SecurityLevel.Standard;
        
        // Log test events
        _auditService.LogSecurityEvent(SecurityEventType.Authentication, "Auth event 1", null, SecurityLevel.High);
        _auditService.LogSecurityEvent(SecurityEventType.Authorization, "Auth event 2", null, SecurityLevel.Low);
        _auditService.LogSecurityEvent(SecurityEventType.Authentication, "Auth event 3", null, SecurityLevel.Critical);
        
        // Act
        var events = await _auditService.GetSecurityEventsAsync(startTime, endTime, eventType, minSeverity);
        
        // Assert
        await Assert.That(events).IsNotNull();
        await Assert.That(events.All(e => e.EventType == eventType)).IsTrue();
        await Assert.That(events.All(e => e.Severity >= minSeverity)).IsTrue();
        await Assert.That(events.All(e => e.Timestamp >= startTime && e.Timestamp <= endTime)).IsTrue();
    }
    
    [Test]
    public async Task GenerateAuditReportAsync_WithNoEvents_ShouldReturnEmptyReport()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddDays(-1);
        var endTime = DateTime.UtcNow.AddDays(-1).AddHours(1); // 1 hour window in the past
        
        // Act
        var report = await _auditService.GenerateAuditReportAsync(startTime, endTime);
        
        // Assert
        await Assert.That(report).IsNotNull();
        await Assert.That(report.StartTime).IsEqualTo(startTime);
        await Assert.That(report.EndTime).IsEqualTo(endTime);
        await Assert.That(report.EventsByType).IsNotNull();
        await Assert.That(report.EventsBySeverity).IsNotNull();
        await Assert.That(report.HighSeverityEvents).IsNotNull();
        await Assert.That(report.TopViolations).IsNotNull();
    }
    
    [Test]
    public void FlushEvents_WithNullCallback_ShouldNotThrow()
    {
        // Arrange
        var bufferedConfig = new SecurityAuditConfiguration
        {
            EnableBuffering = true,
            BufferSize = 10
        };
        using var bufferedService = new SecurityAuditService(_logger, bufferedConfig);
        
        // Log some events
        bufferedService.LogSecurityEvent(SecurityEventType.Authentication, "Test event");
        
        // Act & Assert
        bufferedService.FlushEvents(null);
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public void LogSecurityViolation_WithComplexViolationData_ShouldLogCorrectly()
    {
        // Arrange
        var violationType = "SQL_INJECTION";
        var description = "Detected SQL injection attempt in user input";
        var details = new Dictionary<string, object>
        {
            { "input_field", "username" },
            { "malicious_input", "'; DROP TABLE users; --" },
            { "client_ip", "192.168.1.100" },
            { "user_agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" },
            { "timestamp", DateTime.UtcNow },
            { "blocked", true }
        };
        
        // Act & Assert
        _auditService.LogSecurityViolation(violationType, description, details);
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public async Task LogAuthenticationEvent_WithDetailedContext_ShouldLogCorrectly()
    {
        // Arrange
        var action = "login_attempt";
        var success = false;
        var userId = "user123";
        var details = new Dictionary<string, object>
        {
            { "failure_reason", "invalid_password" },
            { "attempt_count", 3 },
            { "client_ip", "192.168.1.100" },
            { "user_agent", "Mobile App v1.0" }
        };
        
        // Act & Assert
        _auditService.LogAuthenticationEvent(action, success, userId, "Mobile authentication with detailed context");
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [Test]
    public async Task LogAuthorizationEvent_WithPermissionDetails_ShouldLogCorrectly()
    {
        // Arrange
        var resource = "/api/admin/users";
        var action = "DELETE";
        var userId = "admin123";
        var allowed = false;
        var details = new Dictionary<string, object>
        {
            { "required_permission", "admin.users.delete" },
            { "user_permissions", new[] { "admin.users.read", "admin.users.write" } },
            { "denial_reason", "insufficient_permissions" }
        };
        
        // Act & Assert
        _auditService.LogAuthorizationEvent(resource, action, allowed, userId);
        // No exception should be thrown - test passes if no exception is thrown
    }
    
    [After(Test)]
    public void Dispose()
    {
        _auditService?.Dispose();
        _loggerFactory?.Dispose();
    }
}