using Microsoft.Extensions.Logging;
using TUnit.Assertions;
using TUnit.Core;
using HlpAI.Services;
using HlpAI.Attributes;
using HlpAI.Models;

namespace HlpAI.Tests.Services;

public class SecurityMiddlewareDebugTest : IDisposable
{
    private readonly SecurityMiddleware _middleware;
    private readonly ILogger<SecurityMiddleware> _logger;
    private readonly LoggerFactory _loggerFactory;
    
    public SecurityMiddlewareDebugTest()
    {
        _loggerFactory = new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<SecurityMiddleware>();
        
        // Create minimal configuration
        var config = new SecurityConfiguration
        {
            MaxRequestSize = 1000000,
            MaxContentLength = 500000,
            MaxParameterLength = 1000,
            AddSecurityHeaders = true,
            RequireSecurityHeaders = false,
            EnableRateLimiting = false
        };
        
        // Create minimal AppConfiguration
        var testAppConfig = new AppConfiguration
        {
            MaxRequestSizeBytes = 1000000,
            MaxContentLengthBytes = 500000,
            ApiKeyMinLength = 10,
            ApiKeyMaxLength = 100,
            ProviderNameMaxLength = 50
        };
        
        // Create services
        var validationService = new SecurityValidationService(testAppConfig, _loggerFactory.CreateLogger<SecurityValidationService>());
        var auditConfig = new SecurityAuditConfiguration { EnableBuffering = false, MinimumLogLevel = SecurityLevel.Low };
        var auditService = new SecurityAuditService(_loggerFactory.CreateLogger<SecurityAuditService>(), auditConfig);
        
        // Create middleware
        _middleware = new SecurityMiddleware(validationService, auditService, _logger, config);
        
        Console.WriteLine("SecurityMiddleware created successfully");
    }
    
    [Test]
    public async Task DebugTest_ValidateRequest_ShouldExecuteCode()
    {
        Console.WriteLine("Starting debug test");
        
        // Arrange
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 10,
            Content = "test",
            Headers = new Dictionary<string, string> { { "User-Agent", "Test" } },
            Parameters = new Dictionary<string, string> { { "test", "value" } }
        };
        
        Console.WriteLine("Request created");
        
        // Act
        Console.WriteLine("About to call ValidateRequest");
        var result = _middleware.ValidateRequest(request);
        Console.WriteLine($"ValidateRequest returned: IsValid={result.IsValid}, Violations={result.Violations.Count}");
        
        // Assert
        await Assert.That(result).IsNotNull();
        Console.WriteLine("Test completed successfully");
    }
    
    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }
}