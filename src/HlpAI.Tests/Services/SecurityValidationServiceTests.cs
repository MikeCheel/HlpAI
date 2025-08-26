using Microsoft.Extensions.Logging;
using TUnit.Assertions;
using TUnit.Core;
using HlpAI.Services;
using HlpAI.Attributes;

namespace HlpAI.Tests.Services;

public class SecurityValidationServiceTests : IDisposable
{
    private readonly SecurityValidationService _service;
    private readonly ILogger<SecurityValidationService> _logger;
    private readonly LoggerFactory _loggerFactory;
    
    public SecurityValidationServiceTests()
    {
        _loggerFactory = new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<SecurityValidationService>();
        _service = new SecurityValidationService(_logger);
    }
    
    [After(Test)]
    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }
    
    [Test]
    [Arguments("sk-1234567890abcdef1234567890abcdef", true)] // Valid OpenAI format
    [Arguments("sk-ant-1234567890abcdef1234567890abcdef", true)] // Valid Anthropic format
    [Arguments("sk-1234567890abcdef1234567890abcdef1234567890abcdef", true)] // Valid DeepSeek format
    [Arguments("", false)] // Empty
    [Arguments("invalid-key", false)] // Invalid format
    [Arguments("sk-123", false)] // Too short
    [Arguments(null, false)] // Null
    public async Task ValidateApiKey_ShouldReturnExpectedResult(string? apiKey, bool expected)
    {
        // Act
        var result = _service.ValidateApiKey(apiKey, "test-provider");
        
        // Assert
        await Assert.That(result.IsValid).IsEqualTo(expected);
    }
    
    [Test]
    [Arguments("https://api.openai.com/v1", true)]
    [Arguments("https://api.anthropic.com/v1", true)]
    [Arguments("http://localhost:8080", true)]
    [Arguments("ftp://example.com", false)]
    [Arguments("javascript:alert('xss')", false)]
    [Arguments("", false)]
    [Arguments(null, false)]
    public async Task ValidateUrl_ShouldReturnExpectedResult(string? url, bool expected)
    {
        // Act
        var result = _service.ValidateUrl(url);
        
        // Assert
        await Assert.That(result.IsValid).IsEqualTo(expected);
    }
    
    [Test]
    [Arguments("gpt-4", true)]
    [Arguments("claude-3-opus", true)]
    [Arguments("deepseek-chat", true)]
    [Arguments("model_with_underscores", true)]
    [Arguments("model-with-dashes", true)]
    [Arguments("model123", true)]
    [Arguments("", false)]
    [Arguments("model with spaces", false)]
    [Arguments("model@special", false)]
    [Arguments(null, false)]
    public async Task ValidateModelName_ShouldReturnExpectedResult(string? modelName, bool expected)
    {
        // Act
        var result = _service.ValidateModelName(modelName);
        
        // Assert
        await Assert.That(result.IsValid).IsEqualTo(expected);
    }
    
    [Test]
    [Arguments("openai", true)]
    [Arguments("anthropic", true)]
    [Arguments("deepseek", true)]
    [Arguments("provider_name", true)]
    [Arguments("provider-name", true)]
    [Arguments("", false)]
    [Arguments("provider with spaces", false)]
    [Arguments("provider@special", false)]
    [Arguments(null, false)]
    public async Task ValidateProviderName_ShouldReturnExpectedResult(string? providerName, bool expected)
    {
        // Act
        var result = _service.ValidateProviderName(providerName);
        
        // Assert
        await Assert.That(result.IsValid).IsEqualTo(expected);
    }
    
    [Test]
    [Arguments("/home/user/file.txt", true)]
    [Arguments("C:\\Users\\user\\file.txt", true)]
    [Arguments("./relative/path.txt", true)]
    [Arguments("../parent/file.txt", false)] // Path traversal
    [Arguments("/etc/passwd", false)] // Sensitive system file
    [Arguments("file<script>.txt", false)] // Dangerous characters
    [Arguments("", false)]
    [Arguments(null, false)]
    public async Task ValidateFilePath_ShouldReturnExpectedResult(string? filePath, bool expected)
    {
        // Act
        var result = _service.ValidateFilePath(filePath);
        
        // Assert
        await Assert.That(result.IsValid).IsEqualTo(expected);
    }
    
    [Test]
    [Arguments("Hello world", "Hello world")]
    [Arguments("Text with <script>alert('xss')</script>", "Text with alert('xss')")]
    [Arguments("SQL injection'; DROP TABLE users; --", "SQL injection'; DROP TABLE users; --")] // Should be detected by SQL injection check
    [Arguments("", "")]
    [Arguments(null, "")]
    public async Task SanitizeText_ShouldRemoveDangerousContent(string? input, string expected)
    {
        // Act
        var result = _service.SanitizeText(input);
        
        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }
    
    [Test]
    [Arguments("Normal text", false)]
    [Arguments("SELECT * FROM users", true)]
    [Arguments("'; DROP TABLE users; --", true)]
    [Arguments("UNION SELECT password FROM accounts", true)]
    [Arguments("exec('malicious code')", true)]
    [Arguments("", false)]
    [Arguments(null, false)]
    public async Task ContainsSqlInjection_ShouldDetectSqlPatterns(string? input, bool expected)
    {
        // Act
        var result = SecurityValidationService.ContainsSqlInjection(input);
        
        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }
    
    [Test]
    [Arguments(0.0, true)]
    [Arguments(0.5, true)]
    [Arguments(1.0, true)]
    [Arguments(2.0, true)]
    [Arguments(-0.1, false)]
    [Arguments(2.1, false)]
    public async Task ValidateTemperature_ShouldReturnExpectedResult(double temperature, bool expected)
    {
        // Act
        var result = _service.ValidateTemperature(temperature);
        
        // Assert
        await Assert.That(result.IsValid).IsEqualTo(expected);
    }
    
    [Test]
    [Arguments(1, true)]
    [Arguments(1000, true)]
    [Arguments(4096, true)]
    [Arguments(0, false)]
    [Arguments(-1, false)]
    [Arguments(100000, false)]
    public async Task ValidateMaxTokens_ShouldReturnExpectedResult(int maxTokens, bool expected)
    {
        // Act
        var result = _service.ValidateMaxTokens(maxTokens);
        
        // Assert
        await Assert.That(result.IsValid).IsEqualTo(expected);
    }
    
    [Test]
    public async Task SanitizeText_WithLongText_ShouldTruncate()
    {
        // Arrange
        var longText = new string('a', 200);
        var maxLength = 100;
        
        // Act
        var result = _service.SanitizeText(longText, maxLength);
        
        // Assert
        await Assert.That(result.Length <= maxLength).IsTrue();
    }
    
    [Test]
    public async Task ValidateApiKey_WithWhitespace_ShouldReturnFalse()
    {
        // Arrange
        var apiKeyWithSpaces = "sk-1234 5678 90ab cdef";
        
        // Act
        var result = _service.ValidateApiKey(apiKeyWithSpaces, "test-provider");
        
        // Assert
        await Assert.That(result.IsValid).IsFalse();
    }
    
    [Test]
    public async Task ValidateUrl_WithInvalidScheme_ShouldReturnFalse()
    {
        // Arrange
        var invalidUrl = "file:///etc/passwd";
        
        // Act
        var result = _service.ValidateUrl(invalidUrl);
        
        // Assert
        await Assert.That(result.IsValid).IsFalse();
    }
    
    [Test]
    [Arguments("<script>alert('xss')</script>", true)]
    [Arguments("javascript:alert('xss')", true)]
    [Arguments("<img src=x onerror=alert('xss')>", true)]
    [Arguments("Normal text", false)]
    [Arguments("", false)]
    [Arguments(null, false)]
    public async Task ContainsDangerousCharacters_ShouldDetectXssPatterns(string? input, bool expected)
    {
        // Act
        var result = _service.ContainsDangerousCharacters(input);
        
        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }
    
    [Test]
    public async Task ValidateFilePath_WithNullOrEmpty_ShouldReturnFalse()
    {
        // Act & Assert
        await Assert.That(_service.ValidateFilePath(null).IsValid).IsFalse();
        await Assert.That(_service.ValidateFilePath("").IsValid).IsFalse();
        await Assert.That(_service.ValidateFilePath("   ").IsValid).IsFalse();
    }
    
    [Test]
    public async Task SanitizeText_WithNullInput_ShouldReturnEmptyString()
    {
        // Act
        var result = _service.SanitizeText(null);
        
        // Assert
        await Assert.That(result).IsEqualTo(string.Empty);
    }
}