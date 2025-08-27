using Microsoft.Extensions.Logging;
using TUnit.Assertions;
using TUnit.Core;
using HlpAI.Services;
using HlpAI.Attributes;

namespace HlpAI.Tests.Services;

public class SecurityMiddlewareTests : IDisposable
{
    private readonly SecurityMiddleware _middleware;
    private readonly ILogger<SecurityMiddleware> _logger;
    private readonly SecurityConfiguration _config;
    private readonly LoggerFactory _loggerFactory;
    
    public SecurityMiddlewareTests()
    {
        _loggerFactory = new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<SecurityMiddleware>();
        _config = new SecurityConfiguration
        {
            MaxRequestSize = 1000000,
            MaxContentLength = 500000,
            MaxParameterLength = 1000,
            AddSecurityHeaders = true,
            RequireSecurityHeaders = false,
            EnableRateLimiting = false
        };
        
        // Create a test AppConfiguration to avoid database dependencies
        var testAppConfig = new HlpAI.Models.AppConfiguration
        {
            MaxRequestSizeBytes = 1000000,
            MaxContentLengthBytes = 500000
        };
        var validationService = new SecurityValidationService(testAppConfig, _loggerFactory.CreateLogger<SecurityValidationService>());
        var auditConfig = new SecurityAuditConfiguration { EnableBuffering = false, MinimumLogLevel = SecurityLevel.Low };
        var auditService = new SecurityAuditService(_loggerFactory.CreateLogger<SecurityAuditService>(), auditConfig);
        
        try
        {
            _middleware = new SecurityMiddleware(validationService, auditService, testAppConfig, _logger, _config);
            _logger.LogInformation("SecurityMiddleware instantiated successfully in test constructor");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to instantiate SecurityMiddleware in test constructor");
            throw;
        }
    }
    
    [After(Test)]
    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }
    
    [Test]
    public async Task ValidateRequest_WithValidRequest_ShouldReturnSuccess()
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

        // Act
        _logger.LogInformation("About to call ValidateRequest");
        
        // Ensure the middleware is not null
        await Assert.That(_middleware).IsNotNull();
        
        var result = _middleware.ValidateRequest(request);
        _logger.LogInformation("ValidateRequest completed with result: {IsValid}", result.IsValid);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Violations).IsEmpty();
        await Assert.That(result.SecurityHeaders).IsNotEmpty();
        
        // Additional assertions to ensure methods are called
        var headers = _middleware.GenerateSecurityHeaders();
        await Assert.That(headers).IsNotEmpty();
        
        var sanitized = _middleware.SanitizeInput("<script>alert('test')</script>");
        await Assert.That(sanitized).IsNotNull();
        
        var encrypted = _middleware.EncryptSensitiveData("sensitive data", "test-context");
        await Assert.That(encrypted).IsNotNull();
        
        var decrypted = _middleware.DecryptSensitiveData(encrypted, "test-context");
        await Assert.That(decrypted).IsEqualTo("sensitive data");
    }
    
    [Test]
    public async Task SecurityMiddleware_AllMethods_ShouldExecuteSuccessfully()
    {
        // This test ensures all SecurityMiddleware methods are called for coverage
        
        // Test ValidateRequest with various scenarios
        var validRequest = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 100,
            Content = "Valid content",
            Headers = new Dictionary<string, string> { { "User-Agent", "Test-Agent/1.0" } },
            Parameters = new Dictionary<string, string> { { "param1", "value1" } }
        };
        
        var result = _middleware.ValidateRequest(validRequest);
        await Assert.That(result).IsNotNull();
        
        // Test oversized request
        var oversizedRequest = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 2000000, // Exceeds MaxRequestSize
            Content = "Large content",
            Headers = new Dictionary<string, string> { { "User-Agent", "Test-Agent/1.0" } },
            Parameters = new Dictionary<string, string>()
        };
        
        var oversizedResult = _middleware.ValidateRequest(oversizedRequest);
        await Assert.That(oversizedResult).IsNotNull();
        await Assert.That(oversizedResult.IsValid).IsFalse();
        
        // Test suspicious content
        var suspiciousRequest = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 100,
            Content = "<script>alert('xss')</script>",
            Headers = new Dictionary<string, string> { { "User-Agent", "Test-Agent/1.0" } },
            Parameters = new Dictionary<string, string>()
        };
        
        var suspiciousResult = _middleware.ValidateRequest(suspiciousRequest);
        await Assert.That(suspiciousResult).IsNotNull();
        
        // Test SQL injection content
        var sqlRequest = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 100,
            Content = "'; DROP TABLE users; --",
            Headers = new Dictionary<string, string> { { "User-Agent", "Test-Agent/1.0" } },
            Parameters = new Dictionary<string, string>()
        };
        
        var sqlResult = _middleware.ValidateRequest(sqlRequest);
        await Assert.That(sqlResult).IsNotNull();
        
        // Test GenerateSecurityHeaders
        var headers = _middleware.GenerateSecurityHeaders();
        await Assert.That(headers).IsNotNull();
        
        // Test SanitizeInput with various inputs
        var sanitized1 = _middleware.SanitizeInput("<script>alert('test')</script>");
        await Assert.That(sanitized1).IsNotNull();
        
        var sanitized2 = _middleware.SanitizeInput(null);
        await Assert.That(sanitized2).IsNotNull();
        
        var sanitized3 = _middleware.SanitizeInput("Normal text");
        await Assert.That(sanitized3).IsNotNull();
        
        // Test long input
        var longInput = new string('a', 2000);
        var sanitizedLong = _middleware.SanitizeInput(longInput);
        await Assert.That(sanitizedLong).IsNotNull();
        
        // Test EncryptSensitiveData and DecryptSensitiveData
        var testData = "sensitive information";
        var context = "test-context";
        
        var encrypted = _middleware.EncryptSensitiveData(testData, context);
        await Assert.That(encrypted).IsNotNull();
        await Assert.That(encrypted).IsNotEqualTo(testData);
        
        var decrypted = _middleware.DecryptSensitiveData(encrypted, context);
        await Assert.That(decrypted).IsEqualTo(testData);
        
        // Test with different context (should fail)
        try
        {
            var wrongDecrypted = _middleware.DecryptSensitiveData(encrypted, "wrong-context");
            await Assert.That(wrongDecrypted).IsNotEqualTo(testData);
        }
        catch (HlpAI.Services.SecurityException)
        {
            // Expected behavior - decryption should fail with wrong context
        }
        
        // Test with null/empty inputs
        var encryptedNull = _middleware.EncryptSensitiveData(null!, context);
        await Assert.That(encryptedNull).IsNotNull();
        
        var encryptedEmpty = _middleware.EncryptSensitiveData("", context);
        await Assert.That(encryptedEmpty).IsNotNull();
    }
    
    [Test]
    public async Task ValidateRequest_WithOversizedRequest_ShouldReturnViolation()
    {
        // Arrange
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = _config.MaxRequestSize + 1,
            Content = "Content",
            Headers = new Dictionary<string, string>(),
            Parameters = new Dictionary<string, string>()
        };
        
        _logger.LogInformation("Testing oversized request with size: {Size}, max: {Max}", request.ContentLength, _config.MaxRequestSize);
        
        // Act
        var result = _middleware.ValidateRequest(request);
        
        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Violations).Contains(v => v.Contains("Request size"));
    }
    
    [Test]
    public async Task ValidateRequest_WithSuspiciousContent_ShouldReturnViolation()
    {
        // Arrange
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 100,
            Content = "<script>alert('xss')</script>"
        };
        
        // Act
        var result = _middleware.ValidateRequest(request);
        
        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Violations).Contains(v => v.Contains("script injection"));
    }
    
    [Test]
    public async Task ValidateRequest_WithSqlInjectionContent_ShouldReturnViolation()
    {
        // Arrange
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 100,
            Content = "'; DROP TABLE users; --"
        };
        
        // Act
        var result = _middleware.ValidateRequest(request);
        
        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Violations).Contains(v => v.Contains("SQL injection"));
    }
    
    [Test]
    public async Task ValidateRequest_WithInvalidParameters_ShouldReturnViolation()
    {
        // Arrange
        var longValue = new string('a', _config.MaxParameterLength + 1);
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 100,
            Content = "Valid content",
            Parameters = new Dictionary<string, string>
            {
                { "param1", longValue }
            }
        };
        
        // Act
        var result = _middleware.ValidateRequest(request);
        
        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Violations).Contains(v => v.Contains("value too long"));
    }
    
    [Test]
    public async Task GenerateSecurityHeaders_ShouldReturnStandardHeaders()
    {
        // Act
        var headers = _middleware.GenerateSecurityHeaders();
        
        // Assert
        await Assert.That(headers.Keys).Contains("Content-Security-Policy");
        await Assert.That(headers.Keys).Contains("X-Frame-Options");
        await Assert.That(headers.Keys).Contains("X-Content-Type-Options");
        await Assert.That(headers.Keys).Contains("X-XSS-Protection");
        await Assert.That(headers.Keys).Contains("Referrer-Policy");
        await Assert.That(headers.Keys).Contains("Permissions-Policy");
    }
    
    [Test]
    public async Task GenerateSecurityHeaders_WithHttpsEnabled_ShouldIncludeHSTS()
    {
        // Arrange
        var config = new SecurityConfiguration { UseHttpsOnly = true };
        var middleware = new SecurityMiddleware(_logger, config);
        
        // Act
        var headers = middleware.GenerateSecurityHeaders();
        
        // Assert
        await Assert.That(headers.Keys).Contains("Strict-Transport-Security");
    }
    
    [Test]
    public async Task SanitizeInput_WithValidInput_ShouldReturnSanitized()
    {
        // Arrange
        var input = "<p>Hello <script>alert('xss')</script> World</p>";
        var options = new SanitizationOptions
        {
            RemoveHtml = true,
            EscapeSpecialChars = true
        };
        
        // Act
        var result = _middleware.SanitizeInput(input, options);
        
        // Assert
        await Assert.That(result).DoesNotContain("<script>");
        await Assert.That(result).DoesNotContain("<p>");
    }
    
    [Test]
    public async Task SanitizeInput_WithNullInput_ShouldReturnEmpty()
    {
        // Act
        var result = _middleware.SanitizeInput(null);
        
        // Assert
        await Assert.That(result).IsEqualTo(string.Empty);
    }
    
    [Test]
    public async Task EncryptSensitiveData_WithValidData_ShouldReturnEncrypted()
    {
        // Arrange
        var sensitiveData = "secret-api-key";
        var context = "test-context";
        
        // Act
        var encrypted = _middleware.EncryptSensitiveData(sensitiveData, context);
        
        // Assert
        await Assert.That(encrypted).IsNotEqualTo(sensitiveData);
        await Assert.That(encrypted).IsNotEmpty();
    }
    
    [Test]
    public async Task DecryptSensitiveData_WithValidEncryptedData_ShouldReturnOriginal()
    {
        // Arrange
        var originalData = "secret-api-key";
        var context = "test-context";
        var encrypted = _middleware.EncryptSensitiveData(originalData, context);
        
        // Act
        var decrypted = _middleware.DecryptSensitiveData(encrypted, context);
        
        // Assert
        await Assert.That(decrypted).IsEqualTo(originalData);
    }
    
    [Test]
    public async Task EncryptDecrypt_WithDifferentContexts_ShouldFail()
    {
        // Arrange
        var originalData = "secret-api-key";
        var context1 = "context1";
        var context2 = "context2";
        var encrypted = _middleware.EncryptSensitiveData(originalData, context1);
        
        // Act & Assert
        await Assert.That(() => _middleware.DecryptSensitiveData(encrypted, context2)).Throws<HlpAI.Services.SecurityException>();
    }
    
    [Test]
    public async Task ValidateRequest_WithEmptyRequest_ShouldHandleGracefully()
    {
        // Arrange
        var request = new SecurityRequest();
        
        // Act
        var result = _middleware.ValidateRequest(request);
        
        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.SecurityHeaders).IsNotNull();
    }
    
    [Test]
    public async Task SanitizeInput_WithLongInput_ShouldTruncate()
    {
        // Arrange
        var longInput = new string('a', 2000);
        var options = new SanitizationOptions { MaxLength = 100 };
        
        // Act
        var result = _middleware.SanitizeInput(longInput, options);
        
        // Assert
        await Assert.That(result.Length <= options.MaxLength).IsTrue();
    }
    
    [Test]
    [Arguments("<script>alert('xss')</script>", "alert('xss')")]
    [Arguments("<p>Hello World</p>", "Hello World")]
    [Arguments("No HTML here", "No HTML here")]
    [Arguments("", "")]
    public async Task SanitizeInput_WithHtmlRemoval_ShouldRemoveHtmlTags(string input, string expected)
    {
        // Arrange
        var options = new SanitizationOptions { RemoveHtml = true, EscapeSpecialChars = false };
        
        // Act
        var result = _middleware.SanitizeInput(input, options);
        
        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }
    
    [Test]
    [Arguments("Hello & World", "Hello &amp; World")]
    [Arguments("<test>", "&lt;test&gt;")]
    [Arguments("Quote: \"test\"", "Quote: &quot;test&quot;")]
    [Arguments("Apostrophe: 'test'", "Apostrophe: &#x27;test&#x27;")]
    public async Task SanitizeInput_WithEscaping_ShouldEscapeSpecialChars(string input, string expected)
    {
        // Arrange
        var options = new SanitizationOptions { RemoveHtml = false, EscapeSpecialChars = true };
        
        // Act
        var result = _middleware.SanitizeInput(input, options);
        
        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }
    
    [Test]
    public async Task ValidateRequest_WithRequiredHeaders_ShouldCheckHeaders()
    {
        // Arrange
        var config = new SecurityConfiguration
        {
            RequireSecurityHeaders = true,
            RequiredHeaders = new[] { "Authorization", "X-API-Key" }
        };
        var middleware = new SecurityMiddleware(_logger, config);
        
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            Headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer token" }
                // Missing X-API-Key
            }
        };
        
        // Act
        var result = middleware.ValidateRequest(request);
        
        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Violations).Contains(v => v.Contains("Missing required header: X-API-Key"));
    }
    
    [Test]
    public async Task ValidateRequest_WithRateLimitExceeded_ShouldReturnViolation()
    {
        // Arrange
        var config = new SecurityConfiguration
        {
            EnableRateLimiting = true
        };
        var middleware = new SecurityMiddleware(_logger, config);
        
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 100,
            Content = "Valid content"
        };
        
        // Act - First request should pass
        var result1 = middleware.ValidateRequest(request);
        // Second request should fail due to rate limit
        var result2 = middleware.ValidateRequest(request);
        
        // Assert
        await Assert.That(result1.IsValid).IsTrue();
        await Assert.That(result2.IsValid).IsFalse();
        await Assert.That(result2.Violations).Contains(v => v.Contains("Rate limit exceeded"));
    }
    
    [Test]
    public async Task ValidateRequest_WithSuspiciousParameters_ShouldReturnViolation()
    {
        // Arrange
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 100,
            Content = "Valid content",
            Parameters = new Dictionary<string, string>
            {
                { "param1", "../../../etc/passwd" }, // Path traversal
                { "param2", "normal_value" }
            }
        };
        
        // Act
        var result = _middleware.ValidateRequest(request);
        
        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Violations).Contains(v => v.Contains("suspicious pattern"));
    }
    
    [Test]
    public async Task ValidateRequest_WithInvalidParameterNames_ShouldReturnViolation()
    {
        // Arrange
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 100,
            Content = "Valid content",
            Parameters = new Dictionary<string, string>
            {
                { "param<script>", "value1" }, // Invalid parameter name
                { "normal_param", "value2" }
            }
        };
        
        // Act
        var result = _middleware.ValidateRequest(request);
        
        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Violations).Contains(v => v.Contains("Invalid parameter name"));
    }
    
    [Test]
    public async Task EncryptSensitiveData_WithNullData_ShouldReturnEmpty()
    {
        // Act
        var result = _middleware.EncryptSensitiveData(null!, "context");
        
        // Assert
        await Assert.That(result).IsEqualTo(string.Empty);
    }
    
    [Test]
    public async Task EncryptSensitiveData_WithEmptyData_ShouldReturnEmpty()
    {
        // Act
        var result = _middleware.EncryptSensitiveData("", "context");
        
        // Assert
        await Assert.That(result).IsEqualTo(string.Empty);
    }
    
    [Test]
    public async Task DecryptSensitiveData_WithNullData_ShouldReturnEmpty()
    {
        // Act
        var result = _middleware.DecryptSensitiveData(null!, "context");
        
        // Assert
        await Assert.That(result).IsEqualTo(string.Empty);
    }
    
    [Test]
    public async Task DecryptSensitiveData_WithInvalidData_ShouldThrowSecurityException()
    {
        // Act & Assert
        await Assert.That(() => _middleware.DecryptSensitiveData("invalid-encrypted-data", "context"))
            .Throws<HlpAI.Services.SecurityException>();
    }
    
    [Test]
    public async Task ValidateRequest_WithExcessiveContentLength_ShouldReturnViolation()
    {
        // Arrange
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 100,
            Content = new string('a', _config.MaxContentLength + 1)
        };
        
        // Act
        var result = _middleware.ValidateRequest(request);
        
        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Violations).Contains(v => v.Contains("Content too long"));
    }
    
    [Test]
    public async Task SanitizeInput_WithDefaultOptions_ShouldUseDefaults()
    {
        // Arrange
        var input = "<script>alert('test')</script>Hello & World";
        
        // Act
        var result = _middleware.SanitizeInput(input);
        
        // Assert
        await Assert.That(result).DoesNotContain("<script>");
        await Assert.That(result).Contains("Hello");
    }
    
    [Test]
    public async Task Constructor_WithNullLogger_ShouldNotThrow()
    {
        // Act & Assert
        var middleware = new SecurityMiddleware(null, _config);
        await Assert.That(middleware).IsNotNull();
    }
    
    [Test]
    public async Task Constructor_WithNullConfig_ShouldUseDefaults()
    {
        // Act & Assert
        var middleware = new SecurityMiddleware(_logger, null);
        await Assert.That(middleware).IsNotNull();
    }
    
    [Test]
    public async Task ValidateRequest_WithNullHeaders_ShouldHandleGracefully()
    {
        // Arrange
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 100,
            Content = "Valid content",
            Headers = null
        };
        
        // Act
        var result = _middleware.ValidateRequest(request);
        
        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.SecurityHeaders).IsNotNull();
    }
    
    [Test]
    public async Task ValidateRequest_WithNullParameters_ShouldHandleGracefully()
    {
        // Arrange
        var request = new SecurityRequest
        {
            Endpoint = "/api/test",
            ClientId = "test-client",
            ContentLength = 100,
            Content = "Valid content",
            Parameters = null
        };
        
        // Act
        var result = _middleware.ValidateRequest(request);
        
        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.SecurityHeaders).IsNotNull();
    }
}