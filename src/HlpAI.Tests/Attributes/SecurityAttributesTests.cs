using System.ComponentModel.DataAnnotations;
using TUnit.Assertions;
using TUnit.Core;
using HlpAI.Attributes;

namespace HlpAI.Tests.Attributes;

public class SecurityAttributesTests
{
    [Test]
    [Arguments("sk-1234567890abcdef1234567890abcdef", true)] // Valid OpenAI format
    [Arguments("sk-ant-1234567890abcdef1234567890abcdef", true)] // Valid Anthropic format
    [Arguments("sk-1234567890abcdef1234567890abcdef1234567890abcdef", true)] // Valid DeepSeek format
    [Arguments("", false)] // Empty
    [Arguments("invalid-key", false)] // Invalid format
    [Arguments("sk-123", false)] // Too short
    [Arguments(null, false)] // Null
    public async Task ApiKeyValidationAttribute_ShouldValidateCorrectly(string? apiKey, bool expected)
    {
        // Arrange
        var attribute = new ApiKeyValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(apiKey, context);
        
        // Assert
        if (expected)
         {
             await Assert.That(result).IsEqualTo(ValidationResult.Success);
         }
         else
         {
             await Assert.That(result).IsNotEqualTo(ValidationResult.Success);
         }
    }
    
    [Test]
    [Arguments("https://api.openai.com/v1", true)]
    [Arguments("https://api.anthropic.com/v1", true)]
    [Arguments("http://localhost:8080", true)]
    [Arguments("ftp://example.com", false)]
    [Arguments("javascript:alert('xss')", false)]
    [Arguments("", false)]
    [Arguments(null, false)]
    public async Task UrlValidationAttribute_ShouldValidateCorrectly(string? url, bool expected)
    {
        // Arrange
        var attribute = new UrlValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(url, context);
        
        // Assert
        if (expected)
        {
            await Assert.That(result).IsEqualTo(ValidationResult.Success);
        }
        else
        {
            await Assert.That(result).IsNotEqualTo(ValidationResult.Success);
        }
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
    public async Task ModelNameValidationAttribute_ShouldValidateCorrectly(string? modelName, bool expected)
    {
        // Arrange
        var attribute = new ModelNameValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(modelName, context);
        
        // Assert
        if (expected)
        {
            await Assert.That(result).IsEqualTo(ValidationResult.Success);
        }
        else
        {
            await Assert.That(result).IsNotEqualTo(ValidationResult.Success);
        }
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
    public async Task ProviderNameValidationAttribute_ShouldValidateCorrectly(string? providerName, bool expected)
    {
        // Arrange
        var attribute = new ProviderNameValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(providerName, context);
        
        // Assert
        if (expected)
        {
            await Assert.That(result).IsEqualTo(ValidationResult.Success);
        }
        else
        {
            await Assert.That(result).IsNotEqualTo(ValidationResult.Success);
        }
    }
    
    [Test]
    [Arguments("Normal text", true)]
    [Arguments("Text with numbers 123", true)]
    [Arguments("SELECT * FROM users", false)]
    [Arguments("'; DROP TABLE users; --", false)]
    [Arguments("UNION SELECT password FROM accounts", false)]
    [Arguments("exec('malicious code')", false)]
    [Arguments("", true)]
    [Arguments(null, true)]
    public async Task SqlInjectionSafeAttribute_ShouldValidateCorrectly(string? input, bool expected)
    {
        // Arrange
        var attribute = new SqlInjectionSafeAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(input, context);
        
        // Assert
        if (expected)
        {
            await Assert.That(result).IsEqualTo(ValidationResult.Success);
        }
        else
        {
            await Assert.That(result).IsNotEqualTo(ValidationResult.Success);
        }
    }
    
    [Test]
    [Arguments(0.0, true)]
    [Arguments(0.5, true)]
    [Arguments(1.0, true)]
    [Arguments(2.0, true)]
    [Arguments(-0.1, false)]
    [Arguments(2.1, false)]
    public async Task TemperatureValidationAttribute_ShouldValidateCorrectly(double temperature, bool expected)
    {
        // Arrange
        var attribute = new TemperatureValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(temperature, context);
        
        // Assert
        if (expected)
        {
            await Assert.That(result).IsEqualTo(ValidationResult.Success);
        }
        else
        {
            await Assert.That(result).IsNotEqualTo(ValidationResult.Success);
        }
    }
    
    [Test]
    [Arguments(1, true)]
    [Arguments(1000, true)]
    [Arguments(4096, true)]
    [Arguments(0, false)]
    [Arguments(-1, false)]
    [Arguments(100000, false)]
    public async Task TokenCountValidationAttribute_ShouldValidateCorrectly(int tokenCount, bool expected)
    {
        // Arrange
        var attribute = new TokenCountValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(tokenCount, context);
        
        // Assert
        if (expected)
        {
            await Assert.That(result).IsEqualTo(ValidationResult.Success);
        }
        else
        {
            await Assert.That(result).IsNotEqualTo(ValidationResult.Success);
        }
    }
    
    [Test]
    [Arguments("Safe text", true)]
    [Arguments("Text with numbers 123", true)]
    [Arguments("<script>alert('xss')</script>", false)]
    [Arguments("javascript:alert('xss')", false)]
    [Arguments("<img src=x onerror=alert('xss')>", false)]
    [Arguments("", true)]
    [Arguments(null, true)]
    public async Task SafeTextAttribute_ShouldValidateCorrectly(string? text, bool expected)
    {
        // Arrange
        var attribute = new SafeTextAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(text, context);
        
        // Assert
        if (expected)
        {
            await Assert.That(result).IsEqualTo(ValidationResult.Success);
        }
        else
        {
            await Assert.That(result).IsNotEqualTo(ValidationResult.Success);
        }
    }
    
    [Test]
    public async Task ApiKeyValidationAttribute_WithCustomErrorMessage_ShouldUseCustomMessage()
    {
        // Arrange
        var customMessage = "Custom API key validation error";
        var attribute = new ApiKeyValidationAttribute { ErrorMessage = customMessage };
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult("invalid-key", context);
        
        // Assert
         await Assert.That(result).IsNotEqualTo(ValidationResult.Success);
         await Assert.That(result?.ErrorMessage).IsEqualTo(customMessage);
    }
    
    [Test]
    public async Task UrlValidationAttribute_WithCustomErrorMessage_ShouldUseCustomMessage()
    {
        // Arrange
        var customMessage = "Custom URL validation error";
        var attribute = new UrlValidationAttribute { ErrorMessage = customMessage };
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult("invalid-url", context);
        
        // Assert
        await Assert.That(result).IsNotEqualTo(ValidationResult.Success);
        await Assert.That(result?.ErrorMessage).IsEqualTo(customMessage);
    }
    
    [Test]
    public async Task RequiresSecurityValidationAttribute_ShouldBeApplicableToClassesAndMethods()
    {
        // Arrange
        var attribute = new RequiresSecurityValidationAttribute();
        
        // Act & Assert
        var targets = attribute.GetType().GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault()?.ValidOn;
        
        await Assert.That(targets?.HasFlag(AttributeTargets.Class) == true).IsTrue();
        await Assert.That(targets?.HasFlag(AttributeTargets.Method) == true).IsTrue();
    }
    
    [Test]
    public async Task SensitiveDataAttribute_ShouldBeApplicableToPropertiesAndFields()
    {
        // Arrange
        var attribute = new SensitiveDataAttribute();
        
        // Act & Assert
        var targets = attribute.GetType().GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault()?.ValidOn;
        
        await Assert.That(targets?.HasFlag(AttributeTargets.Property) == true).IsTrue();
        await Assert.That(targets?.HasFlag(AttributeTargets.Field) == true).IsTrue();
    }
    
    [Test]
    public async Task SecurityLevel_ShouldHaveCorrectValues()
    {
        // Arrange
        var lowValue = (int)SecurityLevel.Low;
        var standardValue = (int)SecurityLevel.Standard;
        var highValue = (int)SecurityLevel.High;
        var criticalValue = (int)SecurityLevel.Critical;
        
        // Assert
        await Assert.That(lowValue).IsEqualTo(0);
        await Assert.That(standardValue).IsEqualTo(1);
        await Assert.That(highValue).IsEqualTo(2);
        await Assert.That(criticalValue).IsEqualTo(3);
    }
    
    [Test]
    public async Task TemperatureValidationAttribute_WithNullValue_ShouldReturnSuccess()
    {
        // Arrange
        var attribute = new TemperatureValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(null, context);
        
        // Assert
         await Assert.That(result).IsEqualTo(ValidationResult.Success);
    }
    
    [Test]
    public async Task TokenCountValidationAttribute_WithNullValue_ShouldReturnSuccess()
    {
        // Arrange
        var attribute = new TokenCountValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(null, context);
        
        // Assert
        await Assert.That(result).IsEqualTo(ValidationResult.Success);
    }
    
    [Test]
    [Arguments("sk-1234567890abcdef1234567890abcdef")]
    [Arguments("sk-ant-1234567890abcdef1234567890abcdef")]
    [Arguments("sk-1234567890abcdef1234567890abcdef1234567890abcdef")]
    public async Task ApiKeyValidationAttribute_WithValidKeys_ShouldReturnSuccess(string validKey)
    {
        // Arrange
        var attribute = new ApiKeyValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(validKey, context);
        
        // Assert
        await Assert.That(result).IsEqualTo(ValidationResult.Success);
    }
    
    [Test]
    [Arguments("https://api.openai.com/v1")]
    [Arguments("https://api.anthropic.com/v1")]
    [Arguments("http://localhost:8080")]
    [Arguments("https://custom-api.example.com/v2")]
    public async Task UrlValidationAttribute_WithValidUrls_ShouldReturnSuccess(string validUrl)
    {
        // Arrange
        var attribute = new UrlValidationAttribute();
        var context = new ValidationContext(new object());
        
        // Act
        var result = attribute.GetValidationResult(validUrl, context);
        
        // Assert
        await Assert.That(result).IsEqualTo(ValidationResult.Success);
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