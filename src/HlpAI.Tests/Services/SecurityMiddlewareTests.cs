using Microsoft.Extensions.Logging;
using TUnit.Assertions;
using TUnit.Core;
using HlpAI.Services;

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
        _config = new SecurityConfiguration();
        _middleware = new SecurityMiddleware(_logger, _config);
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
        var result = _middleware.ValidateRequest(request);
        
        // Assert
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Violations).IsEmpty();
        await Assert.That(result.SecurityHeaders).IsNotEmpty();
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
            Content = "Content"
        };
        
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
        await Assert.That(() => _middleware.DecryptSensitiveData(encrypted, context2)).Throws<SecurityException>();
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
            .Throws<SecurityException>();
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