using Microsoft.Extensions.Logging;
using HlpAI.Services;
using HlpAI.Models;

namespace HlpAI.Tests.Services;

public class SecurityValidationServiceTests : IDisposable
{
    private readonly SecurityValidationService _service;
    private readonly ILogger<SecurityValidationService> _logger;
    private readonly LoggerFactory _loggerFactory;
    private readonly AppConfiguration _config;
    
    public SecurityValidationServiceTests()
    {
        _loggerFactory = new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<SecurityValidationService>();
        _config = new AppConfiguration(); // Use default configuration values
        _service = new SecurityValidationService(_config, _logger);
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
    [Arguments(100000, true)]
    [Arguments(100001, false)]
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
    
    [Test]
    public async Task ContainsDangerousCharacters_WithVariousInputs_ShouldDetectCorrectly()
    {
        // Test cases with dangerous characters
        await Assert.That(_service.ContainsDangerousCharacters("<script>")).IsTrue();
        await Assert.That(_service.ContainsDangerousCharacters("javascript:")).IsTrue();
        await Assert.That(_service.ContainsDangerousCharacters("vbscript:")).IsTrue();
        await Assert.That(_service.ContainsDangerousCharacters("data:")).IsTrue();
        await Assert.That(_service.ContainsDangerousCharacters("../")).IsTrue();
        await Assert.That(_service.ContainsDangerousCharacters("..\\")).IsTrue();
        
        // Test safe inputs
        await Assert.That(_service.ContainsDangerousCharacters("normal text")).IsFalse();
        await Assert.That(_service.ContainsDangerousCharacters("user@example.com")).IsFalse();
    }
    
    [Test]
    public async Task ValidateUrl_WithAdvancedCases_ShouldValidateCorrectly()
    {
        // Test valid URLs with ports and paths
        await Assert.That(_service.ValidateUrl("https://example.com:8080").IsValid).IsTrue();
        await Assert.That(_service.ValidateUrl("https://api.example.com/v1/endpoint").IsValid).IsTrue();
        
        // Test invalid URLs
        await Assert.That(_service.ValidateUrl("ftp://example.com").IsValid).IsFalse();
        await Assert.That(_service.ValidateUrl("javascript:alert('xss')").IsValid).IsFalse();
        await Assert.That(_service.ValidateUrl("data:text/html,<script>alert('xss')</script>").IsValid).IsFalse();
        await Assert.That(_service.ValidateUrl("file:///etc/passwd").IsValid).IsFalse();
    }
    
    [Test]
    public async Task ValidateModelName_WithEdgeCases_ShouldValidateCorrectly()
    {
        // Test valid model names
        await Assert.That(_service.ValidateModelName("gpt-4").IsValid).IsTrue();
        await Assert.That(_service.ValidateModelName("claude-3-opus").IsValid).IsTrue();
        await Assert.That(_service.ValidateModelName("model_name_123").IsValid).IsTrue();
        
        // Test invalid model names
        await Assert.That(_service.ValidateModelName("model<script>").IsValid).IsFalse();
        await Assert.That(_service.ValidateModelName("model with spaces").IsValid).IsFalse();
        await Assert.That(_service.ValidateModelName("model/with/slashes").IsValid).IsFalse();
        await Assert.That(_service.ValidateModelName("model\\with\\backslashes").IsValid).IsFalse();
    }
    
    [Test]
    public async Task ValidateProviderName_WithEdgeCases_ShouldValidateCorrectly()
    {
        // Test valid provider names
        await Assert.That(_service.ValidateProviderName("openai").IsValid).IsTrue();
        await Assert.That(_service.ValidateProviderName("anthropic").IsValid).IsTrue();
        await Assert.That(_service.ValidateProviderName("provider_123").IsValid).IsTrue();
        
        // Test invalid provider names
        await Assert.That(_service.ValidateProviderName("provider<script>").IsValid).IsFalse();
        await Assert.That(_service.ValidateProviderName("provider with spaces").IsValid).IsFalse();
        await Assert.That(_service.ValidateProviderName("provider/with/slashes").IsValid).IsFalse();
    }
    
    [Test]
    public async Task ValidateFilePath_WithEdgeCases_ShouldValidateCorrectly()
    {
        // Test valid file paths
        await Assert.That(_service.ValidateFilePath("C:\\Users\\test\\file.txt").IsValid).IsTrue();
        await Assert.That(_service.ValidateFilePath("/home/user/file.txt").IsValid).IsTrue();
        await Assert.That(_service.ValidateFilePath("./relative/path.txt").IsValid).IsTrue();
        
        // Test invalid file paths
        await Assert.That(_service.ValidateFilePath("../../../etc/passwd").IsValid).IsFalse();
        await Assert.That(_service.ValidateFilePath("..\\..\\..\\windows\\system32").IsValid).IsFalse();
        await Assert.That(_service.ValidateFilePath("file<script>.txt").IsValid).IsFalse();
        await Assert.That(_service.ValidateFilePath("file|pipe.txt").IsValid).IsFalse();
    }
    
    [Test]
    public async Task ValidateTemperature_WithBoundaryValues_ShouldValidateCorrectly()
    {
        // Test boundary values
        await Assert.That(_service.ValidateTemperature(0.0).IsValid).IsTrue();
        await Assert.That(_service.ValidateTemperature(2.0).IsValid).IsTrue();
        await Assert.That(_service.ValidateTemperature(1.0).IsValid).IsTrue();
        
        // Test invalid values
        await Assert.That(_service.ValidateTemperature(-0.1).IsValid).IsFalse();
        await Assert.That(_service.ValidateTemperature(2.1).IsValid).IsFalse();
        await Assert.That(_service.ValidateTemperature(double.NaN).IsValid).IsFalse();
        await Assert.That(_service.ValidateTemperature(double.PositiveInfinity).IsValid).IsFalse();
        await Assert.That(_service.ValidateTemperature(double.NegativeInfinity).IsValid).IsFalse();
    }
    
    [Test]
    public async Task ValidateMaxTokens_WithBoundaryValues_ShouldValidateCorrectly()
    {
        // Test boundary values
        await Assert.That(_service.ValidateMaxTokens(1).IsValid).IsTrue();
        await Assert.That(_service.ValidateMaxTokens(100000).IsValid).IsTrue();
        await Assert.That(_service.ValidateMaxTokens(50000).IsValid).IsTrue();
        
        // Test invalid values
        await Assert.That(_service.ValidateMaxTokens(0).IsValid).IsFalse();
        await Assert.That(_service.ValidateMaxTokens(-1).IsValid).IsFalse();
        await Assert.That(_service.ValidateMaxTokens(100001).IsValid).IsFalse();
    }
    
    [Test]
    public async Task SanitizeText_WithComplexInput_ShouldSanitizeCorrectly()
    {
        // Test complex input with multiple issues
        var complexInput = "<script>alert('xss')</script>Hello & <b>World</b> with 'quotes' and \"double quotes\"";
        var result = _service.SanitizeText(complexInput);
        
        await Assert.That(result).DoesNotContain("<script>");
        await Assert.That(result).DoesNotContain("</script>");
        await Assert.That(result).Contains("Hello");
        await Assert.That(result).Contains("World");
    }
    
    [Test]
    public async Task ContainsSqlInjection_WithAdvancedPatterns_ShouldDetectCorrectly()
    {
        // Test advanced SQL injection patterns
        await Assert.That(SecurityValidationService.ContainsSqlInjection("1' OR '1'='1")).IsTrue();
        await Assert.That(SecurityValidationService.ContainsSqlInjection("'; DROP TABLE users; --")).IsTrue();
        await Assert.That(SecurityValidationService.ContainsSqlInjection("1 UNION SELECT * FROM users")).IsTrue();
        await Assert.That(SecurityValidationService.ContainsSqlInjection("admin'--")).IsTrue();
        await Assert.That(SecurityValidationService.ContainsSqlInjection("1; DELETE FROM users")).IsTrue();
        
        // Test safe inputs
        await Assert.That(SecurityValidationService.ContainsSqlInjection("normal user input")).IsFalse();
        await Assert.That(SecurityValidationService.ContainsSqlInjection("user@example.com")).IsFalse();
        await Assert.That(SecurityValidationService.ContainsSqlInjection("Product Name v1.0")).IsFalse();
    }
    
    [Test]
    [Arguments("../../../etc/passwd", true)]
    [Arguments("..\\..\\..\\windows\\system32", true)]
    [Arguments("/etc/passwd", true)]
    [Arguments("/etc/shadow", true)]
    [Arguments("/root/secret.txt", true)]
    [Arguments("C:\\Windows\\System32\\config", true)]
    [Arguments("C:\\Program Files\\app.exe", true)]
    [Arguments("~/secret.txt", true)]
    [Arguments("normal/file/path.txt", false)]
    [Arguments("./relative/path.txt", false)]
    [Arguments("file.txt", false)]
    [Arguments("", false)]
    [Arguments(null, false)]
    public async Task ContainsPathTraversal_ShouldDetectPathTraversalAttempts(string? input, bool expected)
    {
        // Act
        var result = _service.ContainsPathTraversal(input);
        
        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }
    
    [Test]
    public async Task ContainsPathTraversal_WithSensitiveSystemPaths_ShouldDetectCorrectly()
    {
        // Test Windows sensitive paths
        await Assert.That(_service.ContainsPathTraversal("C:\\Windows\\System32\\drivers")).IsTrue();
        await Assert.That(_service.ContainsPathTraversal("C:\\Windows\\SysWOW64\\kernel32.dll")).IsTrue();
        await Assert.That(_service.ContainsPathTraversal("C:\\Users\\Administrator\\Desktop")).IsTrue();
        await Assert.That(_service.ContainsPathTraversal("C:\\ProgramData\\config.xml")).IsTrue();
        
        // Test Unix/Linux sensitive paths
        await Assert.That(_service.ContainsPathTraversal("/etc/ssh/ssh_config")).IsTrue();
        await Assert.That(_service.ContainsPathTraversal("/var/log/auth.log")).IsTrue();
        await Assert.That(_service.ContainsPathTraversal("/proc/version")).IsTrue();
        await Assert.That(_service.ContainsPathTraversal("/sys/kernel/debug")).IsTrue();
        await Assert.That(_service.ContainsPathTraversal("/dev/random")).IsTrue();
        
        // Test safe paths
        await Assert.That(_service.ContainsPathTraversal("/home/user/documents/file.txt")).IsFalse();
        await Assert.That(_service.ContainsPathTraversal("C:\\Users\\user\\Documents\\file.txt")).IsFalse();
        await Assert.That(_service.ContainsPathTraversal("./app/data/config.json")).IsFalse();
    }
    
    [Test]
    public async Task ContainsPathTraversal_WithCaseInsensitiveMatching_ShouldDetectCorrectly()
    {
        // Test case insensitive matching for Windows paths
        await Assert.That(_service.ContainsPathTraversal("c:\\windows\\system32\\config")).IsTrue();
        await Assert.That(_service.ContainsPathTraversal("C:\\WINDOWS\\SYSTEM32\\CONFIG")).IsTrue();
        await Assert.That(_service.ContainsPathTraversal("c:\\Windows\\System32\\Config")).IsTrue();
        
        // Test case insensitive matching for Unix paths
        await Assert.That(_service.ContainsPathTraversal("/ETC/PASSWD")).IsTrue();
        await Assert.That(_service.ContainsPathTraversal("/Etc/Shadow")).IsTrue();
        await Assert.That(_service.ContainsPathTraversal("/ROOT/secret")).IsTrue();
    }
}