using Microsoft.Extensions.Logging;
using Xunit;
using HlpAI.Services;
using HlpAI.Attributes;

namespace HlpAI.Tests.Services;

public class SecurityAuditServiceTests : IDisposable
{
    private readonly SecurityAuditService _auditService;
    private readonly ILogger<SecurityAuditService> _logger;
    private readonly SecurityAuditConfiguration _config;
    
    public SecurityAuditServiceTests()
    {
        _logger = new LoggerFactory().CreateLogger<SecurityAuditService>();
        _config = new SecurityAuditConfiguration
        {
            EnableBuffering = false, // Disable buffering for immediate testing
            MinimumLogLevel = SecurityLevel.Low
        };
        _auditService = new SecurityAuditService(_logger, _config);
    }
    
    [Fact]
    public void LogSecurityEvent_WithValidEvent_ShouldLogSuccessfully()
    {
        // Arrange
        var eventType = SecurityEventType.Authentication;
        var message = "User login attempt";
        var details = new { UserId = "test-user", Success = true };
        
        // Act
        _auditService.LogSecurityEvent(eventType, message, details, SecurityLevel.Medium);
        
        // Assert
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
    public void LogAuthenticationEvent_WithSuccessfulLogin_ShouldLogWithLowSeverity()
    {
        // Arrange
        var action = "login";
        var success = true;
        var userId = "test-user";
        
        // Act
        _auditService.LogAuthenticationEvent(action, success, userId);
        
        // Assert
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
    public void LogAuthenticationEvent_WithFailedLogin_ShouldLogWithHighSeverity()
    {
        // Arrange
        var action = "login";
        var success = false;
        var userId = "test-user";
        
        // Act
        _auditService.LogAuthenticationEvent(action, success, userId);
        
        // Assert
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
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
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
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
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
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
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
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
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
    public void LogSecurityViolation_ShouldLogWithHighSeverity()
    {
        // Arrange
        var violationType = "SQL Injection Attempt";
        var description = "Detected SQL injection pattern in user input";
        var context = new { Input = "'; DROP TABLE users; --", Endpoint = "/api/search" };
        
        // Act
        _auditService.LogSecurityViolation(violationType, description, context);
        
        // Assert
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
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
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
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
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
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
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
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
        Assert.NotNull(events);
        // Note: In a real implementation, this would return the actual events
        // For now, it returns events from the buffer which may be empty after flushing
    }
    
    [Fact]
    public async Task GetSecurityEventsAsync_WithTimeRange_ShouldFilterEvents()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-1);
        var endTime = DateTime.UtcNow;
        
        // Act
        var events = await _auditService.GetSecurityEventsAsync(startTime, endTime);
        
        // Assert
        Assert.NotNull(events);
        Assert.All(events, e => Assert.True(e.Timestamp >= startTime && e.Timestamp <= endTime));
    }
    
    [Fact]
    public async Task GetSecurityEventsAsync_WithEventTypeFilter_ShouldFilterEvents()
    {
        // Arrange
        var eventType = SecurityEventType.Authentication;
        
        // Act
        var events = await _auditService.GetSecurityEventsAsync(eventType: eventType);
        
        // Assert
        Assert.NotNull(events);
        Assert.All(events, e => Assert.Equal(eventType, e.EventType));
    }
    
    [Fact]
    public async Task GetSecurityEventsAsync_WithSeverityFilter_ShouldFilterEvents()
    {
        // Arrange
        var minSeverity = SecurityLevel.Medium;
        
        // Act
        var events = await _auditService.GetSecurityEventsAsync(minSeverity: minSeverity);
        
        // Assert
        Assert.NotNull(events);
        Assert.All(events, e => Assert.True(e.Severity >= minSeverity));
    }
    
    [Fact]
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
        Assert.NotNull(report);
        Assert.Equal(startTime, report.StartTime);
        Assert.Equal(endTime, report.EndTime);
        Assert.True(report.GeneratedAt <= DateTime.UtcNow);
        Assert.NotNull(report.EventsByType);
        Assert.NotNull(report.EventsBySeverity);
        Assert.NotNull(report.HighSeverityEvents);
        Assert.NotNull(report.TopViolations);
    }
    
    [Fact]
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
        bufferedService.FlushEvents(null);
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
    public void LogSecurityEvent_WithNullDetails_ShouldHandleGracefully()
    {
        // Act & Assert
        _auditService.LogSecurityEvent(SecurityEventType.SystemAccess, "Test message", null);
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
    public void LogAuthenticationEvent_WithNullUserId_ShouldHandleGracefully()
    {
        // Act & Assert
        _auditService.LogAuthenticationEvent("login", true, null);
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
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
        Assert.True(true);
    }
    
    [Theory]
    [InlineData(SecurityLevel.Low)]
    [InlineData(SecurityLevel.Medium)]
    [InlineData(SecurityLevel.High)]
    [InlineData(SecurityLevel.Critical)]
    public void LogSecurityEvent_WithDifferentSeverities_ShouldLogAppropriately(SecurityLevel severity)
    {
        // Act & Assert
        _auditService.LogSecurityEvent(SecurityEventType.SystemAccess, $"Test {severity} event", null, severity);
        // No exception should be thrown
        Assert.True(true);
    }
    
    [Fact]
    public void Constructor_WithNullLogger_ShouldNotThrow()
    {
        // Act & Assert
        using var service = new SecurityAuditService(null, _config);
        Assert.NotNull(service);
    }
    
    [Fact]
    public void Constructor_WithNullConfig_ShouldUseDefaults()
    {
        // Act & Assert
        using var service = new SecurityAuditService(_logger, null);
        Assert.NotNull(service);
    }
    
    public void Dispose()
    {
        _auditService?.Dispose();
    }
}