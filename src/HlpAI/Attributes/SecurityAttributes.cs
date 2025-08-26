using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace HlpAI.Attributes;

/// <summary>
/// Validates that a string is a valid API key format
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class ApiKeyValidationAttribute : ValidationAttribute
{
    private static readonly Regex ApiKeyPattern = new(@"^[a-zA-Z0-9_-]{20,200}$", RegexOptions.Compiled);
    
    public override bool IsValid(object? value)
    {
        if (value is null or string { Length: 0 })
        {
            return !IsRequired;
        }
        
        if (value is string apiKey)
        {
            return ApiKeyPattern.IsMatch(apiKey.Trim());
        }
        
        return false;
    }
    
    public override string FormatErrorMessage(string name)
    {
        return !string.IsNullOrEmpty(ErrorMessage) 
            ? ErrorMessage 
            : $"The {name} field must be a valid API key (20-200 alphanumeric characters, underscores, or hyphens)";
    }
    
    public bool IsRequired { get; set; } = true;
}

/// <summary>
/// Validates that a string is a valid URL format
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class UrlValidationAttribute : ValidationAttribute
{
    private static readonly Regex UrlPattern = new(@"^https?:\/\/[a-zA-Z0-9.-]+(?::[0-9]+)?(?:\/[^\s]*)?$", RegexOptions.Compiled);
    
    public override bool IsValid(object? value)
    {
        if (value is null or string { Length: 0 })
        {
            return !IsRequired;
        }
        
        if (value is string url)
        {
            return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
                   (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                    uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) &&
                   UrlPattern.IsMatch(url.Trim());
        }
        
        return false;
    }
    
    public override string FormatErrorMessage(string name)
    {
        return !string.IsNullOrEmpty(ErrorMessage) 
            ? ErrorMessage 
            : $"The {name} field must be a valid HTTP or HTTPS URL";
    }
    
    public bool IsRequired { get; set; } = true;
}

/// <summary>
/// Validates that a string is a valid model name
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class ModelNameValidationAttribute : ValidationAttribute
{
    private static readonly Regex ModelNamePattern = new(@"^[a-zA-Z0-9._-]{1,100}$", RegexOptions.Compiled);
    
    public override bool IsValid(object? value)
    {
        if (value is null or string { Length: 0 })
        {
            return !IsRequired;
        }
        
        if (value is string modelName)
        {
            return ModelNamePattern.IsMatch(modelName.Trim());
        }
        
        return false;
    }
    
    public override string FormatErrorMessage(string name)
    {
        return !string.IsNullOrEmpty(ErrorMessage) 
            ? ErrorMessage 
            : $"The {name} field must be a valid model name (1-100 characters: letters, numbers, dots, underscores, hyphens)";
    }
    
    public bool IsRequired { get; set; } = true;
}

/// <summary>
/// Validates that a string is a valid provider name
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class ProviderNameValidationAttribute : ValidationAttribute
{
    private static readonly Regex ProviderNamePattern = new(@"^[a-zA-Z0-9_-]{1,50}$", RegexOptions.Compiled);
    
    public override bool IsValid(object? value)
    {
        if (value is null or string { Length: 0 })
        {
            return !IsRequired;
        }
        
        if (value is string providerName)
        {
            return ProviderNamePattern.IsMatch(providerName.Trim());
        }
        
        return false;
    }
    
    public override string FormatErrorMessage(string name)
    {
        return !string.IsNullOrEmpty(ErrorMessage) 
            ? ErrorMessage 
            : $"The {name} field must be a valid provider name (1-50 characters: letters, numbers, underscores, hyphens)";
    }
    
    public bool IsRequired { get; set; } = true;
}

/// <summary>
/// Validates that a string is safe from SQL injection patterns
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class SqlInjectionSafeAttribute : ValidationAttribute
{
    private static readonly string[] SqlInjectionPatterns = 
    {
        "'", "--", "/*", "*/", "xp_", "sp_", "exec", "execute", "select", "insert", 
        "update", "delete", "drop", "create", "alter", "union", "script", "javascript"
    };
    
    public override bool IsValid(object? value)
    {
        if (value is null or string { Length: 0 })
        {
            return true; // Null/empty is considered safe
        }
        
        if (value is string input)
        {
            var lowerInput = input.ToLowerInvariant();
            return !SqlInjectionPatterns.Any(pattern => lowerInput.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }
        
        return true;
    }
    
    public override string FormatErrorMessage(string name)
    {
        return !string.IsNullOrEmpty(ErrorMessage) 
            ? ErrorMessage 
            : $"The {name} field contains potentially dangerous content";
    }
}

/// <summary>
/// Validates that a double value is within the valid temperature range for AI models
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class TemperatureValidationAttribute : ValidationAttribute
{
    public double MinValue { get; set; } = 0.0;
    public double MaxValue { get; set; } = 2.0;
    
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true; // Null is valid, will use default
        }
        
        if (value is double temperature)
        {
            return temperature >= MinValue && temperature <= MaxValue;
        }
        
        if (value is float floatTemp)
        {
            return floatTemp >= MinValue && floatTemp <= MaxValue;
        }
        
        return false;
    }
    
    public override string FormatErrorMessage(string name)
    {
        return !string.IsNullOrEmpty(ErrorMessage) 
            ? ErrorMessage 
            : $"The {name} field must be between {MinValue} and {MaxValue}";
    }
}

/// <summary>
/// Validates that an integer value is within the valid token range for AI models
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class TokenCountValidationAttribute : ValidationAttribute
{
    public int MinValue { get; set; } = 1;
    public int MaxValue { get; set; } = 99999;
    
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true; // Null is valid, will use default
        }
        
        if (value is int tokens)
        {
            return tokens >= MinValue && tokens <= MaxValue;
        }
        
        return false;
    }
    
    public override string FormatErrorMessage(string name)
    {
        return !string.IsNullOrEmpty(ErrorMessage) 
            ? ErrorMessage 
            : $"The {name} field must be between {MinValue} and {MaxValue}";
    }
}

/// <summary>
/// Validates that a string does not contain dangerous characters
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class SafeTextAttribute : ValidationAttribute
{
    private static readonly char[] DangerousChars = { '<', '>', '"', '\'', '&', '\0', '\r', '\n' };
    
    public int MaxLength { get; set; } = 1000;
    
    public override bool IsValid(object? value)
    {
        if (value is null or string { Length: 0 })
        {
            return true;
        }
        
        if (value is string text)
        {
            return text.Length <= MaxLength && text.IndexOfAny(DangerousChars) < 0;
        }
        
        return false;
    }
    
    public override string FormatErrorMessage(string name)
    {
        return !string.IsNullOrEmpty(ErrorMessage) 
            ? ErrorMessage 
            : $"The {name} field must not contain dangerous characters and be {MaxLength} characters or less";
    }
}

/// <summary>
/// Marks a method or class as requiring security validation
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequiresSecurityValidationAttribute : Attribute
{
    public string? Description { get; set; }
    public SecurityLevel Level { get; set; } = SecurityLevel.Standard;
}

/// <summary>
/// Marks a method or class as handling sensitive data
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
public class SensitiveDataAttribute : Attribute
{
    public string? DataType { get; set; }
    public bool LogAccess { get; set; } = true;
    public bool RequireEncryption { get; set; } = false;
}

/// <summary>
/// Security levels for validation
/// </summary>
public enum SecurityLevel
{
    Low,
    Standard,
    High,
    Critical
}