using Microsoft.Extensions.Logging;
using Xunit;
using HlpAI.Services;
using HlpAI.Attributes;

namespace HlpAI.Tests.Services;

public class SecurityValidationServiceTests
{
    private readonly SecurityValidationService _service;
    private readonly ILogger<SecurityValidationService> _logger;
    
    public SecurityValidationServiceTests()
    {
        _logger = new LoggerFactory().CreateLogger<SecurityValidationService>();
        _service = new SecurityValidationService(_logger);
    }
    
    [Theory]
    [InlineData("sk-1234567890abcdef1234567890abcdef", true)] // Valid OpenAI format
    [InlineData("sk-ant-1234567890abcdef1234567890abcdef", true)] // Valid Anthropic format
    [InlineData("sk-1234567890abcdef1234567890abcdef1234567890abcdef", true)] // Valid DeepSeek format
    [InlineData("", false)] // Empty
    [InlineData("invalid-key", false)] // Invalid format
    [InlineData("sk-123", false)] // Too short
    [InlineData(null, false)] // Null
    public void ValidateApiKey_ShouldReturnExpectedResult(string? apiKey, bool expected)
    {
        // Act
        var result = _service.ValidateApiKey(apiKey);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("https://api.openai.com/v1", true)]
    [InlineData("https://api.anthropic.com/v1", true)]
    [InlineData("http://localhost:8080", true)]
    [InlineData("ftp://example.com", false)]
    [InlineData("javascript:alert('xss')", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidateUrl_ShouldReturnExpectedResult(string? url, bool expected)
    {
        // Act
        var result = _service.ValidateUrl(url);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("gpt-4", true)]
    [InlineData("claude-3-opus", true)]
    [InlineData("deepseek-chat", true)]
    [InlineData("model_with_underscores", true)]
    [InlineData("model-with-dashes", true)]
    [InlineData("model123", true)]
    [InlineData("", false)]
    [InlineData("model with spaces", false)]
    [InlineData("model@special", false)]
    [InlineData(null, false)]
    public void ValidateModelName_ShouldReturnExpectedResult(string? modelName, bool expected)
    {
        // Act
        var result = _service.ValidateModelName(modelName);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("openai", true)]
    [InlineData("anthropic", true)]
    [InlineData("deepseek", true)]
    [InlineData("provider_name", true)]
    [InlineData("provider-name", true)]
    [InlineData("", false)]
    [InlineData("provider with spaces", false)]
    [InlineData("provider@special", false)]
    [InlineData(null, false)]
    public void ValidateProviderName_ShouldReturnExpectedResult(string? providerName, bool expected)
    {
        // Act
        var result = _service.ValidateProviderName(providerName);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("/home/user/file.txt", true)]
    [InlineData("C:\\Users\\user\\file.txt", true)]
    [InlineData("./relative/path.txt", true)]
    [InlineData("../parent/file.txt", false)] // Path traversal
    [InlineData("/etc/passwd", false)] // Sensitive system file
    [InlineData("file<script>.txt", false)] // Dangerous characters
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidateFilePath_ShouldReturnExpectedResult(string? filePath, bool expected)
    {
        // Act
        var result = _service.ValidateFilePath(filePath);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("Hello world", "Hello world")]
    [InlineData("Text with <script>alert('xss')</script>", "Text with alert('xss')")]
    [InlineData("SQL injection'; DROP TABLE users; --", "SQL injection'; DROP TABLE users; --")] // Should be detected by SQL injection check
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SanitizeText_ShouldRemoveDangerousContent(string? input, string expected)
    {
        // Act
        var result = _service.SanitizeText(input);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("Normal text", false)]
    [InlineData("SELECT * FROM users", true)]
    [InlineData("'; DROP TABLE users; --", true)]
    [InlineData("UNION SELECT password FROM accounts", true)]
    [InlineData("exec('malicious code')", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ContainsSqlInjection_ShouldDetectSqlPatterns(string? input, bool expected)
    {
        // Act
        var result = _service.ContainsSqlInjection(input);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.5, true)]
    [InlineData(1.0, true)]
    [InlineData(2.0, true)]
    [InlineData(-0.1, false)]
    [InlineData(2.1, false)]
    public void ValidateTemperature_ShouldReturnExpectedResult(double temperature, bool expected)
    {
        // Act
        var result = _service.ValidateTemperature(temperature);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData(1, true)]
    [InlineData(1000, true)]
    [InlineData(4096, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(100000, false)]
    public void ValidateMaxTokens_ShouldReturnExpectedResult(int maxTokens, bool expected)
    {
        // Act
        var result = _service.ValidateMaxTokens(maxTokens);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void SanitizeText_WithMaxLength_ShouldTruncateText()
    {
        // Arrange
        var longText = new string('a', 1000);
        var maxLength = 100;
        
        // Act
        var result = _service.SanitizeText(longText, maxLength);
        
        // Assert
        Assert.True(result.Length <= maxLength);
    }
    
    [Fact]
    public void ValidateApiKey_WithWhitespace_ShouldReturnFalse()
    {
        // Arrange
        var apiKeyWithSpaces = "sk-1234 5678 90ab cdef";
        
        // Act
        var result = _service.ValidateApiKey(apiKeyWithSpaces);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public void ValidateUrl_WithInvalidScheme_ShouldReturnFalse()
    {
        // Arrange
        var invalidUrl = "file:///etc/passwd";
        
        // Act
        var result = _service.ValidateUrl(invalidUrl);
        
        // Assert
        Assert.False(result);
    }
    
    [Theory]
    [InlineData("<script>alert('xss')</script>", true)]
    [InlineData("javascript:alert('xss')", true)]
    [InlineData("<img src=x onerror=alert('xss')>", true)]
    [InlineData("Normal text", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ContainsDangerousCharacters_ShouldDetectXssPatterns(string? input, bool expected)
    {
        // Act
        var result = _service.ContainsDangerousCharacters(input);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void ValidateFilePath_WithNullOrEmpty_ShouldReturnFalse()
    {
        // Act & Assert
        Assert.False(_service.ValidateFilePath(null));
        Assert.False(_service.ValidateFilePath(""));
        Assert.False(_service.ValidateFilePath("   "));
    }
    
    [Fact]
    public void SanitizeText_WithNullInput_ShouldReturnEmptyString()
    {
        // Act
        var result = _service.SanitizeText(null);
        
        // Assert
        Assert.Equal(string.Empty, result);
    }
}