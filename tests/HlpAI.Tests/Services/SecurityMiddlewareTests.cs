using Microsoft.Extensions.Logging;
using Xunit;
using HlpAI.Services;

namespace HlpAI.Tests.Services;

public class SecurityMiddlewareTests
{
    private readonly SecurityMiddleware _middleware;
    private readonly ILogger<SecurityMiddleware> _logger;
    private readonly SecurityConfiguration _config;
    
    public SecurityMiddlewareTests()
    {
        _logger = new LoggerFactory().CreateLogger<SecurityMiddleware>();
        _config = new SecurityConfiguration();
        _middleware = new SecurityMiddleware(_logger, _config);
    }
    
    [Fact]
    public void ValidateRequest_WithValidRequest_ShouldReturnSuccess()
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
        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
        Assert.NotEmpty(result.SecurityHeaders);
    }
    
    [Fact]
    public void ValidateRequest_WithOversizedRequest_ShouldReturnViolation()
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
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Contains("Request size"));
    }
    
    [Fact]
    public void ValidateRequest_WithSuspiciousContent_ShouldReturnViolation()
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
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Contains("script injection"));
    }
    
    [Fact]
    public void ValidateRequest_WithSqlInjectionContent_ShouldReturnViolation()
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
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Contains("SQL injection"));
    }
    
    [Fact]
    public void ValidateRequest_WithInvalidParameters_ShouldReturnViolation()
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
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Contains("value too long"));
    }
    
    [Fact]
    public void GenerateSecurityHeaders_ShouldReturnStandardHeaders()
    {
        // Act
        var headers = _middleware.GenerateSecurityHeaders();
        
        // Assert
        Assert.Contains("Content-Security-Policy", headers.Keys);
        Assert.Contains("X-Frame-Options", headers.Keys);
        Assert.Contains("X-Content-Type-Options", headers.Keys);
        Assert.Contains("X-XSS-Protection", headers.Keys);
        Assert.Contains("Referrer-Policy", headers.Keys);
        Assert.Contains("Permissions-Policy", headers.Keys);
    }
    
    [Fact]
    public void GenerateSecurityHeaders_WithHttpsEnabled_ShouldIncludeHSTS()
    {
        // Arrange
        var config = new SecurityConfiguration { UseHttpsOnly = true };
        var middleware = new SecurityMiddleware(_logger, config);
        
        // Act
        var headers = middleware.GenerateSecurityHeaders();
        
        // Assert
        Assert.Contains("Strict-Transport-Security", headers.Keys);
    }
    
    [Fact]
    public void SanitizeInput_WithValidInput_ShouldReturnSanitized()
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
        Assert.DoesNotContain("<script>", result);
        Assert.DoesNotContain("<p>", result);
    }
    
    [Fact]
    public void SanitizeInput_WithNullInput_ShouldReturnEmpty()
    {
        // Act
        var result = _middleware.SanitizeInput(null);
        
        // Assert
        Assert.Equal(string.Empty, result);
    }
    
    [Fact]
    public void EncryptSensitiveData_WithValidData_ShouldReturnEncrypted()
    {
        // Arrange
        var sensitiveData = "secret-api-key";
        var context = "test-context";
        
        // Act
        var encrypted = _middleware.EncryptSensitiveData(sensitiveData, context);
        
        // Assert
        Assert.NotEqual(sensitiveData, encrypted);
        Assert.NotEmpty(encrypted);
    }
    
    [Fact]
    public void DecryptSensitiveData_WithValidEncryptedData_ShouldReturnOriginal()
    {
        // Arrange
        var originalData = "secret-api-key";
        var context = "test-context";
        var encrypted = _middleware.EncryptSensitiveData(originalData, context);
        
        // Act
        var decrypted = _middleware.DecryptSensitiveData(encrypted, context);
        
        // Assert
        Assert.Equal(originalData, decrypted);
    }
    
    [Fact]
    public void EncryptDecrypt_WithDifferentContexts_ShouldFail()
    {
        // Arrange
        var originalData = "secret-api-key";
        var context1 = "context1";
        var context2 = "context2";
        var encrypted = _middleware.EncryptSensitiveData(originalData, context1);
        
        // Act & Assert
        Assert.Throws<SecurityException>(() => _middleware.DecryptSensitiveData(encrypted, context2));
    }
    
    [Fact]
    public void ValidateRequest_WithEmptyRequest_ShouldHandleGracefully()
    {
        // Arrange
        var request = new SecurityRequest();
        
        // Act
        var result = _middleware.ValidateRequest(request);
        
        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.SecurityHeaders);
    }
    
    [Fact]
    public void SanitizeInput_WithLongInput_ShouldTruncate()
    {
        // Arrange
        var longInput = new string('a', 2000);
        var options = new SanitizationOptions { MaxLength = 100 };
        
        // Act
        var result = _middleware.SanitizeInput(longInput, options);
        
        // Assert
        Assert.True(result.Length <= options.MaxLength);
    }
    
    [Theory]
    [InlineData("<script>alert('xss')</script>", "alert('xss')")]
    [InlineData("<p>Hello World</p>", "Hello World")]
    [InlineData("No HTML here", "No HTML here")]
    [InlineData("", "")]
    public void SanitizeInput_WithHtmlRemoval_ShouldRemoveHtmlTags(string input, string expected)
    {
        // Arrange
        var options = new SanitizationOptions { RemoveHtml = true, EscapeSpecialChars = false };
        
        // Act
        var result = _middleware.SanitizeInput(input, options);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("Hello & World", "Hello &amp; World")]
    [InlineData("<test>", "&lt;test&gt;")]
    [InlineData("Quote: \"test\"", "Quote: &quot;test&quot;")]
    [InlineData("Apostrophe: 'test'", "Apostrophe: &#x27;test&#x27;")]
    public void SanitizeInput_WithEscaping_ShouldEscapeSpecialChars(string input, string expected)
    {
        // Arrange
        var options = new SanitizationOptions { RemoveHtml = false, EscapeSpecialChars = true };
        
        // Act
        var result = _middleware.SanitizeInput(input, options);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void ValidateRequest_WithRequiredHeaders_ShouldCheckHeaders()
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
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Contains("Missing required header: X-API-Key"));
    }
}