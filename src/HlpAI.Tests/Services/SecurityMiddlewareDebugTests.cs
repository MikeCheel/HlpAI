using Microsoft.Extensions.Logging;
using TUnit.Assertions;
using TUnit.Core;
using HlpAI.Services;
using HlpAI.Attributes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HlpAI.Tests.Services;

public class SecurityMiddlewareDebugTests : IDisposable
{
    private readonly ILogger<SecurityMiddleware> _logger;
    private readonly SecurityConfiguration _config;
    private readonly SecurityMiddleware _middleware;
    private readonly LoggerFactory _loggerFactory;

    public SecurityMiddlewareDebugTests()
    {
        try
        {
            // Create logger
            _loggerFactory = new LoggerFactory();
            _logger = _loggerFactory.CreateLogger<SecurityMiddleware>();
            
            Console.WriteLine("Logger created successfully");
            
            // Create configuration
            _config = new SecurityConfiguration
            {
                MaxRequestSize = 1000000,
                MaxContentLength = 500000,
                MaxParameterLength = 1000,
                AddSecurityHeaders = true,
                RequireSecurityHeaders = false,
                UseHttpsOnly = true,
                EnableRateLimiting = false
            };
            Console.WriteLine($"Configuration created - MaxRequestSize: {_config.MaxRequestSize}");
            
            // Create SecurityMiddleware
            Console.WriteLine("Creating SecurityMiddleware...");
            _middleware = new SecurityMiddleware(_logger, _config);
            Console.WriteLine("SecurityMiddleware created successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in SecurityMiddlewareDebugTests constructor: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    [Test]
    public async Task ValidateRequest_WithValidRequest_ShouldReturnSuccess_Debug()
    {
        // Arrange
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 100,
            Content = "Valid content",
            Headers = new Dictionary<string, string>
            {
                { "User-Agent", "Test-Agent/1.0" },
                { "Content-Type", "application/json" }
            },
            Parameters = new Dictionary<string, string>
            {
                { "param1", "value1" },
                { "param2", "value2" }
            }
        };
        
        Console.WriteLine("Test request created");
        
        // Act
        Console.WriteLine("Calling ValidateRequest...");
        var result = _middleware.ValidateRequest(request);
        Console.WriteLine($"ValidateRequest completed - IsValid: {result.IsValid}, Violations: {result.Violations.Count}");
        
        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Violations).IsNotNull();
        
        foreach (var violation in result.Violations)
        {
            Console.WriteLine($"Violation: {violation}");
        }
        
        Console.WriteLine($"Security headers count: {result.SecurityHeaders.Count}");
        Console.WriteLine("Test completed successfully!");
    }

    [Test]
    public async Task ValidateRequest_WithSqlInjection_ShouldDetectThreat()
    {
        // Arrange
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 50,
            Content = "'; DROP TABLE users; --",
            Headers = new Dictionary<string, string>
            {
                { "User-Agent", "Test-Agent/1.0" },
                { "Content-Type", "application/json" }
            },
            Parameters = new Dictionary<string, string>
            {
                { "query", "'; DROP TABLE users; --" }
            }
        };
        
        Console.WriteLine("SQL injection test request created");
        
        // Act
        Console.WriteLine("Calling ValidateRequest with SQL injection...");
        var result = _middleware.ValidateRequest(request);
        Console.WriteLine($"ValidateRequest completed - IsValid: {result.IsValid}, Violations: {result.Violations.Count}");
        
        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Violations.Count).IsGreaterThan(0);
        
        foreach (var violation in result.Violations)
        {
            Console.WriteLine($"Violation: {violation}");
        }
        
        Console.WriteLine("SQL injection detection test completed!");
    }

    [Test]
    public async Task ValidateRequest_WithXssAttempt_ShouldDetectThreat()
    {
        // Arrange
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 50,
            Content = "<script>alert('xss')</script>",
            Headers = new Dictionary<string, string>
            {
                { "User-Agent", "Test-Agent/1.0" },
                { "Content-Type", "application/json" }
            },
            Parameters = new Dictionary<string, string>
            {
                { "input", "<script>alert('xss')</script>" }
            }
        };
        
        Console.WriteLine("XSS test request created");
        
        // Act
        Console.WriteLine("Calling ValidateRequest with XSS attempt...");
        var result = _middleware.ValidateRequest(request);
        Console.WriteLine($"ValidateRequest completed - IsValid: {result.IsValid}, Violations: {result.Violations.Count}");
        
        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Violations.Count).IsGreaterThan(0);
        
        foreach (var violation in result.Violations)
        {
            Console.WriteLine($"Violation: {violation}");
        }
        
        Console.WriteLine("XSS detection test completed!");
    }

    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }
}