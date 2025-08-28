using Microsoft.Extensions.Logging;
using HlpAI.Models;

namespace HlpAI.Services;

/// <summary>
/// Service for handling user prompts with configurable defaults and input validation
/// </summary>
public class PromptService : IDisposable
{
    private readonly ILogger? _logger;
    private readonly SqliteConfigurationService _configService;
    private readonly SecurityValidationService _validationService;
    private readonly SecurityMiddleware _securityMiddleware;
    private readonly bool _ownsConfigService;
    private bool _disposed = false;

    public PromptService(ILogger? logger = null)
    {
        _logger = logger;
        _configService = new SqliteConfigurationService(logger);
        var appConfig = ConfigurationService.LoadConfiguration(logger);
        _validationService = new SecurityValidationService(appConfig, logger as ILogger<SecurityValidationService>);
        var securityConfig = SecurityConfiguration.FromAppConfiguration(appConfig);
        _securityMiddleware = new SecurityMiddleware(_validationService, new SecurityAuditService(logger as ILogger<SecurityAuditService>), logger as ILogger<SecurityMiddleware>, securityConfig);
        _ownsConfigService = true;
    }

    public PromptService(SqliteConfigurationService configService, ILogger? logger = null)
    {
        _logger = logger;
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        var appConfig = ConfigurationService.LoadConfiguration(logger);
        _validationService = new SecurityValidationService(appConfig, logger as ILogger<SecurityValidationService>);
        var securityConfig = SecurityConfiguration.FromAppConfiguration(appConfig);
        _securityMiddleware = new SecurityMiddleware(_validationService, new SecurityAuditService(logger as ILogger<SecurityAuditService>), logger as ILogger<SecurityMiddleware>, securityConfig);
        _ownsConfigService = false;
    }

    public PromptService(SqliteConfigurationService configService, SecurityValidationService validationService, SecurityMiddleware securityMiddleware, ILogger? logger = null)
    {
        _logger = logger;
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _securityMiddleware = securityMiddleware ?? throw new ArgumentNullException(nameof(securityMiddleware));
        _ownsConfigService = false;
    }

    public PromptService(AppConfiguration appConfig, ILogger? logger = null)
    {
        _logger = logger;
        _configService = new SqliteConfigurationService(logger);
        _validationService = new SecurityValidationService(appConfig, logger as ILogger<SecurityValidationService>);
        var securityConfig = SecurityConfiguration.FromAppConfiguration(appConfig);
        _securityMiddleware = new SecurityMiddleware(_validationService, new SecurityAuditService(logger as ILogger<SecurityAuditService>), logger as ILogger<SecurityMiddleware>, securityConfig);
        _ownsConfigService = true;
    }

    /// <summary>
    /// Prompts the user for a yes/no response with configurable default
    /// </summary>
    /// <param name="prompt">The prompt message to display</param>
    /// <param name="defaultToYes">Whether to default to 'yes' when Enter is pressed</param>
    /// <returns>True if user responds with yes (or Enter when defaultToYes is true), false otherwise</returns>
    public async Task<bool> PromptYesNoAsync(string prompt, bool defaultToYes = true)
    {
        // In test environment, return the default to avoid hanging
        if (IsTestEnvironment())
        {
            return defaultToYes;
        }
        
        // Get user preference for default behavior from configuration
        var userPreference = await GetDefaultPromptBehaviorAsync();
        var effectiveDefault = userPreference ?? defaultToYes;
        
        var promptSuffix = effectiveDefault ? " (Y/n, default: y)" : " (y/N, default: n)";
        var fullPrompt = prompt.TrimEnd(':', ' ') + promptSuffix + ": ";
        
        Console.Write(fullPrompt);
        var response = Console.ReadLine()?.Trim().ToLower();
        
        // Handle empty response (Enter pressed)
        if (string.IsNullOrEmpty(response))
        {
            _logger?.LogDebug("User pressed Enter, using default: {Default}", effectiveDefault);
            return effectiveDefault;
        }
        
        // Handle explicit responses
        var isYes = response == "y" || response == "yes";
        var isNo = response == "n" || response == "no";
        
        if (isYes)
        {
            _logger?.LogDebug("User explicitly chose 'yes'");
            return true;
        }
        
        if (isNo)
        {
            _logger?.LogDebug("User explicitly chose 'no'");
            return false;
        }
        
        // Handle invalid responses
        Console.WriteLine($"Invalid response '{response}'. Please enter 'y' for yes or 'n' for no (or just press Enter for default).");
        return await PromptYesNoAsync(prompt, effectiveDefault);
    }

    /// <summary>
    /// Prompts the user for a yes/no response, always defaulting to 'yes' when Enter is pressed
    /// </summary>
    /// <param name="prompt">The prompt message to display</param>
    /// <returns>True if user responds with yes or presses Enter, false otherwise</returns>
    public async Task<bool> PromptYesNoDefaultYesAsync(string prompt)
    {
        return await PromptYesNoAsync(prompt, true);
    }

    /// <summary>
    /// Prompts the user for a yes/no response, always defaulting to 'no' when Enter is pressed
    /// </summary>
    /// <param name="prompt">The prompt message to display</param>
    /// <returns>True if user explicitly responds with yes, false otherwise</returns>
    public async Task<bool> PromptYesNoDefaultNoAsync(string prompt)
    {
        return await PromptYesNoAsync(prompt, false);
    }

    /// <summary>
    /// Gets the user's configured default prompt behavior
    /// </summary>
    /// <returns>True if configured to default to 'yes', false for 'no', null for use method default</returns>
    public async Task<bool?> GetDefaultPromptBehaviorAsync()
    {
        var setting = await _configService.GetConfigurationAsync("default_prompt_behavior", "ui");
        
        return setting?.ToLowerInvariant() switch
        {
            "yes" => true,
            "no" => false,
            _ => null // Use method default
        };
    }

    /// <summary>
    /// Sets the user's default prompt behavior configuration
    /// </summary>
    /// <param name="defaultToYes">True to default to 'yes', false for 'no', null to use method defaults</param>
    /// <returns>True if successful</returns>
    public async Task<bool> SetDefaultPromptBehaviorAsync(bool? defaultToYes)
    {
        string? value = defaultToYes switch
        {
            true => "yes",
            false => "no",
            null => null
        };
        
        if (value == null)
        {
            return await _configService.RemoveConfigurationAsync("default_prompt_behavior", "ui");
        }
        
        var result = await _configService.SetConfigurationAsync("default_prompt_behavior", value, "ui");
        
        if (result)
        {
            _logger?.LogInformation("Default prompt behavior set to: {Behavior}", value);
        }
        
        return result;
    }

    /// <summary>
    /// Prompts for string input with optional default value and input validation
    /// </summary>
    /// <param name="prompt">The prompt message to display</param>
    /// <param name="defaultValue">Default value if Enter is pressed</param>
    /// <param name="sanitizationOptions">Options for input sanitization</param>
    /// <param name="maxLength">Maximum allowed input length</param>
    /// <returns>User input or default value, sanitized and validated</returns>
    public string PromptForString(string prompt, string? defaultValue = null, SanitizationOptions? sanitizationOptions = null, int maxLength = 1000)
    {
        // In test environment, return the default to avoid hanging
        if (IsTestEnvironment())
        {
            return SanitizeAndValidateInput(defaultValue ?? string.Empty, sanitizationOptions, maxLength);
        }
        
        var promptSuffix = !string.IsNullOrEmpty(defaultValue) ? $" (default: {defaultValue})" : "";
        var fullPrompt = prompt.TrimEnd(':', ' ') + promptSuffix + ": ";
        
        Console.Write(fullPrompt);
        var response = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(response))
        {
            _logger?.LogDebug("User pressed Enter, using default: {Default}", defaultValue ?? "null");
            return SanitizeAndValidateInput(defaultValue ?? string.Empty, sanitizationOptions, maxLength);
        }
        
        return SanitizeAndValidateInput(response, sanitizationOptions, maxLength);
    }
    
    /// <summary>
    /// Prompts for string input with specific validation type
    /// </summary>
    /// <param name="prompt">The prompt message to display</param>
    /// <param name="validationType">Type of validation to apply</param>
    /// <param name="defaultValue">Default value if Enter is pressed</param>
    /// <param name="context">Context for validation (e.g., provider name for API keys)</param>
    /// <returns>Validated user input or default value</returns>
    public string PromptForValidatedString(string prompt, InputValidationType validationType, string? defaultValue = null, string? context = null)
    {
        // In test environment, return the default to avoid hanging
        if (IsTestEnvironment())
        {
            return ValidateSpecificInput(defaultValue ?? string.Empty, validationType, context);
        }
        
        var promptSuffix = !string.IsNullOrEmpty(defaultValue) ? $" (default: {defaultValue})" : "";
        var fullPrompt = prompt.TrimEnd(':', ' ') + promptSuffix + ": ";
        
        while (true)
        {
            Console.Write(fullPrompt);
            var response = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(response))
            {
                if (!string.IsNullOrEmpty(defaultValue))
                {
                    _logger?.LogDebug("User pressed Enter, using default: {Default}", defaultValue);
                    return ValidateSpecificInput(defaultValue, validationType, context);
                }
                Console.WriteLine("Input cannot be empty. Please try again.");
                continue;
            }
            
            var validatedInput = ValidateSpecificInput(response, validationType, context);
            if (!string.IsNullOrEmpty(validatedInput))
            {
                return validatedInput;
            }
            
            Console.WriteLine("Invalid input. Please try again.");
        }
    }

    /// <summary>
    /// Sanitizes and validates general input
    /// </summary>
    private string SanitizeAndValidateInput(string input, SanitizationOptions? options = null, int maxLength = 1000)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        
        // Apply length limit first
        if (input.Length > maxLength)
        {
            _logger?.LogWarning("Input truncated from {OriginalLength} to {MaxLength} characters", input.Length, maxLength);
            input = input[..maxLength];
        }
        
        // Use security middleware for sanitization
        var sanitized = _securityMiddleware.SanitizeInput(input, options);
        
        // Log if input was modified during sanitization
        if (!string.Equals(input, sanitized, StringComparison.Ordinal))
        {
            _logger?.LogDebug("Input sanitized: original length {OriginalLength}, sanitized length {SanitizedLength}", 
                input.Length, sanitized.Length);
        }
        
        return sanitized;
    }
    
    /// <summary>
    /// Validates input based on specific validation type
    /// </summary>
    private string ValidateSpecificInput(string input, InputValidationType validationType, string? context = null)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        
        ValidationResult result = validationType switch
        {
            InputValidationType.ApiKey => _validationService.ValidateApiKey(input, context ?? "unknown"),
            InputValidationType.Url => _validationService.ValidateUrl(input, context ?? "URL"),
            InputValidationType.ModelName => _validationService.ValidateModelName(input),
            InputValidationType.ProviderName => _validationService.ValidateProviderName(input),
            InputValidationType.FilePath => _validationService.ValidateFilePath(input),
            InputValidationType.Temperature => ValidateTemperatureString(input),
            InputValidationType.MaxTokens => ValidateMaxTokensString(input),
            InputValidationType.General => new ValidationResult(true, "Valid", _validationService.SanitizeText(input)),
            _ => new ValidationResult(false, "Unknown validation type")
        };
        
        if (!result.IsValid)
        {
            _logger?.LogWarning("Input validation failed for type {ValidationType}: {Message}", validationType, result.Message);
            Console.WriteLine($"❌ {result.Message}");
            return string.Empty;
        }
        
        return result.SanitizedValue ?? input;
    }
    
    /// <summary>
    /// Validates temperature input as string
    /// </summary>
    private ValidationResult ValidateTemperatureString(string input)
    {
        if (!double.TryParse(input, out var temperature))
        {
            return new ValidationResult(false, "Temperature must be a valid number");
        }
        
        var result = _validationService.ValidateTemperature(temperature);
        return result.IsValid ? new ValidationResult(true, result.Message, temperature.ToString()) : result;
    }
    
    /// <summary>
    /// Validates max tokens input as string
    /// </summary>
    private ValidationResult ValidateMaxTokensString(string input)
    {
        if (!int.TryParse(input, out var maxTokens))
        {
            return new ValidationResult(false, "Max tokens must be a valid integer");
        }
        
        var result = _validationService.ValidateMaxTokens(maxTokens);
        return result.IsValid ? new ValidationResult(true, result.Message, maxTokens.ToString()) : result;
    }
    
    /// <summary>
    /// Determines if running in a test environment to avoid console blocking
    /// </summary>
    private static bool IsTestEnvironment()
    {
        return System.Diagnostics.Process.GetCurrentProcess().ProcessName.Contains("testhost") ||
               System.Diagnostics.Process.GetCurrentProcess().ProcessName.Contains("dotnet") ||
               Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
               AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName?.Contains("TUnit") == true);
    }

    /// <summary>
    /// Shows the current prompt configuration to the user
    /// </summary>
    public async Task ShowPromptConfigurationAsync()
    {
        var behavior = await GetDefaultPromptBehaviorAsync();
        
        Console.WriteLine("\n🎯 Prompt Configuration");
        Console.WriteLine("========================");
        
        string behaviorText = behavior switch
        {
            true => "✅ Default to 'Yes' when Enter is pressed",
            false => "❌ Default to 'No' when Enter is pressed", 
            null => "⚙️  Use individual prompt defaults"
        };
        
        Console.WriteLine($"Current setting: {behaviorText}");
        Console.WriteLine();
        Console.WriteLine("This affects prompts like:");
        Console.WriteLine("  • 'Continue with this configuration?'");
        Console.WriteLine("  • 'Use last directory?'");
        Console.WriteLine("  • 'Create directory?'");
        Console.WriteLine("  • And other yes/no prompts");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                // Only dispose the config service if we own it
                if (_ownsConfigService)
                {
                    _configService?.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing PromptService");
            }
            
            _disposed = true;
        }
    }
}

/// <summary>
/// Types of input validation that can be applied
/// </summary>
public enum InputValidationType
{
    /// <summary>
    /// General text input with basic sanitization
    /// </summary>
    General,
    
    /// <summary>
    /// API key validation with specific format requirements
    /// </summary>
    ApiKey,
    
    /// <summary>
    /// URL validation with protocol and format checks
    /// </summary>
    Url,
    
    /// <summary>
    /// AI model name validation
    /// </summary>
    ModelName,
    
    /// <summary>
    /// AI provider name validation
    /// </summary>
    ProviderName,
    
    /// <summary>
    /// File path validation with security checks
    /// </summary>
    FilePath,
    
    /// <summary>
    /// Temperature parameter validation (0.0 - 2.0)
    /// </summary>
    Temperature,
    
    /// <summary>
    /// Max tokens parameter validation (1 - 100,000)
    /// </summary>
    MaxTokens
}
