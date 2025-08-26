using System.ComponentModel.DataAnnotations;
using Xunit;
using HlpAI.Attributes;

namespace HlpAI.Tests.Attributes;

public class SecurityAttributesTests
{
    [Theory]
    [InlineData("sk-1234567890abcdef1234567890abcdef", true)] // Valid OpenAI format
    [InlineData("sk-ant-1234567890abcdef1234567890abcdef", true)] // Valid Anthropic format
    [InlineData("sk-1234567890abcdef1234567890abcdef1234567890abcdef", true)] // Valid DeepSeek format
    [InlineData("", false)] // Empty
    [InlineData("invalid-key", false)] // Invalid format
    [InlineData("sk-123", false)] // Too short
    [InlineData(null, false)] // Null
    public void ApiKeyValidationAttribute_ShouldValidateCorrectly(string? apiKey, bool expected)
    {
        // Arrange
        var attribute = new ApiKeyValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(apiKey, context);
        
        // Assert
        if (expected)
        {
            Assert.Equal(ValidationResult.Success, result);
        }
        else
        {
            Assert.NotEqual(ValidationResult.Success, result);
        }
    }
    
    [Theory]
    [InlineData("https://api.openai.com/v1", true)]
    [InlineData("https://api.anthropic.com/v1", true)]
    [InlineData("http://localhost:8080", true)]
    [InlineData("ftp://example.com", false)]
    [InlineData("javascript:alert('xss')", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void UrlValidationAttribute_ShouldValidateCorrectly(string? url, bool expected)
    {
        // Arrange
        var attribute = new UrlValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(url, context);
        
        // Assert
        if (expected)
        {
            Assert.Equal(ValidationResult.Success, result);
        }
        else
        {
            Assert.NotEqual(ValidationResult.Success, result);
        }
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
    public void ModelNameValidationAttribute_ShouldValidateCorrectly(string? modelName, bool expected)
    {
        // Arrange
        var attribute = new ModelNameValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(modelName, context);
        
        // Assert
        if (expected)
        {
            Assert.Equal(ValidationResult.Success, result);
        }
        else
        {
            Assert.NotEqual(ValidationResult.Success, result);
        }
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
    public void ProviderNameValidationAttribute_ShouldValidateCorrectly(string? providerName, bool expected)
    {
        // Arrange
        var attribute = new ProviderNameValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(providerName, context);
        
        // Assert
        if (expected)
        {
            Assert.Equal(ValidationResult.Success, result);
        }
        else
        {
            Assert.NotEqual(ValidationResult.Success, result);
        }
    }
    
    [Theory]
    [InlineData("Normal text", true)]
    [InlineData("Text with numbers 123", true)]
    [InlineData("SELECT * FROM users", false)]
    [InlineData("'; DROP TABLE users; --", false)]
    [InlineData("UNION SELECT password FROM accounts", false)]
    [InlineData("exec('malicious code')", false)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void SqlInjectionSafeAttribute_ShouldValidateCorrectly(string? input, bool expected)
    {
        // Arrange
        var attribute = new SqlInjectionSafeAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(input, context);
        
        // Assert
        if (expected)
        {
            Assert.Equal(ValidationResult.Success, result);
        }
        else
        {
            Assert.NotEqual(ValidationResult.Success, result);
        }
    }
    
    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.5, true)]
    [InlineData(1.0, true)]
    [InlineData(2.0, true)]
    [InlineData(-0.1, false)]
    [InlineData(2.1, false)]
    public void TemperatureValidationAttribute_ShouldValidateCorrectly(double temperature, bool expected)
    {
        // Arrange
        var attribute = new TemperatureValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(temperature, context);
        
        // Assert
        if (expected)
        {
            Assert.Equal(ValidationResult.Success, result);
        }
        else
        {
            Assert.NotEqual(ValidationResult.Success, result);
        }
    }
    
    [Theory]
    [InlineData(1, true)]
    [InlineData(1000, true)]
    [InlineData(4096, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(100000, false)]
    public void TokenCountValidationAttribute_ShouldValidateCorrectly(int tokenCount, bool expected)
    {
        // Arrange
        var attribute = new TokenCountValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(tokenCount, context);
        
        // Assert
        if (expected)
        {
            Assert.Equal(ValidationResult.Success, result);
        }
        else
        {
            Assert.NotEqual(ValidationResult.Success, result);
        }
    }
    
    [Theory]
    [InlineData("Safe text", true)]
    [InlineData("Text with numbers 123", true)]
    [InlineData("<script>alert('xss')</script>", false)]
    [InlineData("javascript:alert('xss')", false)]
    [InlineData("<img src=x onerror=alert('xss')>", false)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void SafeTextAttribute_ShouldValidateCorrectly(string? text, bool expected)
    {
        // Arrange
        var attribute = new SafeTextAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(text, context);
        
        // Assert
        if (expected)
        {
            Assert.Equal(ValidationResult.Success, result);
        }
        else
        {
            Assert.NotEqual(ValidationResult.Success, result);
        }
    }
    
    [Fact]
    public void ApiKeyValidationAttribute_WithCustomErrorMessage_ShouldUseCustomMessage()
    {
        // Arrange
        var customMessage = "Custom API key validation error";
        var attribute = new ApiKeyValidationAttribute { ErrorMessage = customMessage };
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult("invalid-key", context);
        
        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Equal(customMessage, result?.ErrorMessage);
    }
    
    [Fact]
    public void UrlValidationAttribute_WithCustomErrorMessage_ShouldUseCustomMessage()
    {
        // Arrange
        var customMessage = "Custom URL validation error";
        var attribute = new UrlValidationAttribute { ErrorMessage = customMessage };
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult("invalid-url", context);
        
        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Equal(customMessage, result?.ErrorMessage);
    }
    
    [Fact]
    public void RequiresSecurityValidationAttribute_ShouldBeApplicableToClassesAndMethods()
    {
        // Arrange
        var attribute = new RequiresSecurityValidationAttribute();
        
        // Act & Assert
        var targets = attribute.GetType().GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault()?.ValidOn;
        
        Assert.True(targets?.HasFlag(AttributeTargets.Class) == true);
        Assert.True(targets?.HasFlag(AttributeTargets.Method) == true);
    }
    
    [Fact]
    public void SensitiveDataAttribute_ShouldBeApplicableToPropertiesAndFields()
    {
        // Arrange
        var attribute = new SensitiveDataAttribute();
        
        // Act & Assert
        var targets = attribute.GetType().GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault()?.ValidOn;
        
        Assert.True(targets?.HasFlag(AttributeTargets.Property) == true);
        Assert.True(targets?.HasFlag(AttributeTargets.Field) == true);
    }
    
    [Fact]
    public void SecurityLevel_ShouldHaveCorrectValues()
    {
        // Assert
        Assert.Equal(0, (int)SecurityLevel.Low);
        Assert.Equal(1, (int)SecurityLevel.Standard);
        Assert.Equal(2, (int)SecurityLevel.High);
        Assert.Equal(3, (int)SecurityLevel.Critical);
    }
    
    [Fact]
    public void TemperatureValidationAttribute_WithNullValue_ShouldReturnSuccess()
    {
        // Arrange
        var attribute = new TemperatureValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(null, context);
        
        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }
    
    [Fact]
    public void TokenCountValidationAttribute_WithNullValue_ShouldReturnSuccess()
    {
        // Arrange
        var attribute = new TokenCountValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(null, context);
        
        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }
    
    [Theory]
    [InlineData("sk-1234567890abcdef1234567890abcdef")]
    [InlineData("sk-ant-1234567890abcdef1234567890abcdef")]
    [InlineData("sk-1234567890abcdef1234567890abcdef1234567890abcdef")]
    public void ApiKeyValidationAttribute_WithValidKeys_ShouldReturnSuccess(string validKey)
    {
        // Arrange
        var attribute = new ApiKeyValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(validKey, context);
        
        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }
    
    [Theory]
    [InlineData("https://api.openai.com/v1")]
    [InlineData("https://api.anthropic.com/v1")]
    [InlineData("http://localhost:8080")]
    [InlineData("https://custom-api.example.com/v2")]
    public void UrlValidationAttribute_WithValidUrls_ShouldReturnSuccess(string validUrl)
    {
        // Arrange
        var attribute = new UrlValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(validUrl, context);
        
        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }
}

// Test classes to verify attribute application
[RequiresSecurityValidation]
public class TestSecureClass
{
    [SensitiveData]
    public string? ApiKey { get; set; }
    
    [SensitiveData]
    public string? Password;
    
    [RequiresSecurityValidation]
    public void SecureMethod()
    {
        // Test method
    }
}

public class TestValidationModel
{
    [ApiKeyValidation]
    public string? ApiKey { get; set; }
    
    [UrlValidation]
    public string? BaseUrl { get; set; }
    
    [ModelNameValidation]
    public string? ModelName { get; set; }
    
    [ProviderNameValidation]
    public string? ProviderName { get; set; }
    
    [SqlInjectionSafe]
    public string? UserInput { get; set; }
    
    [TemperatureValidation]
    public double Temperature { get; set; }
    
    [TokenCountValidation]
    public int MaxTokens { get; set; }
    
    [SafeText]
    public string? Description { get; set; }
}