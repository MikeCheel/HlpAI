using HlpAI.Models;
using HlpAI.Services;
using TUnit.Assertions;

namespace HlpAI.Tests.Program;

/// <summary>
/// Tests for edge cases in configuration prompting logic covering scenarios
/// for when to prompt "keep current configuration" vs starting fresh
/// </summary>
public class ConfigurationPromptingEdgeCaseTests
{
    [Test]
    public async Task KeepConfigurationPrompt_WithValidDirectoryOnly_ShouldPrompt()
    {
        // Arrange
        var config = new AppConfiguration
        {
            LastDirectory = @"C:\ValidPath",
            LastProvider = AiProviderType.None,
            LastModel = null
        };
        
        // Act - Simulate the logic from Program.cs lines 785-786
        var hasValidDirectory = !string.IsNullOrEmpty(config.LastDirectory); // Note: doesn't check Directory.Exists in test
        var hasModel = !string.IsNullOrEmpty(config.LastModel);
        var hasProvider = config.LastProvider != AiProviderType.None;
        var shouldPromptToKeep = hasValidDirectory || hasModel || hasProvider;
        
        // Assert
        await Assert.That(shouldPromptToKeep).IsTrue();
        await Assert.That(hasValidDirectory).IsTrue();
        await Assert.That(hasModel).IsFalse();
        await Assert.That(hasProvider).IsFalse();
    }
    
    [Test]
    public async Task KeepConfigurationPrompt_WithModelOnly_ShouldPrompt()
    {
        // Arrange
        var config = new AppConfiguration
        {
            LastDirectory = null,
            LastProvider = AiProviderType.None,
            LastModel = "llama3.2"
        };
        
        // Act - Simulate the logic from Program.cs lines 785-786
        var hasValidDirectory = !string.IsNullOrEmpty(config.LastDirectory);
        var hasModel = !string.IsNullOrEmpty(config.LastModel);
        var hasProvider = config.LastProvider != AiProviderType.None;
        var shouldPromptToKeep = hasValidDirectory || hasModel || hasProvider;
        
        // Assert
        await Assert.That(shouldPromptToKeep).IsTrue();
        await Assert.That(hasValidDirectory).IsFalse();
        await Assert.That(hasModel).IsTrue();
        await Assert.That(hasProvider).IsFalse();
    }
    
    [Test]
    public async Task KeepConfigurationPrompt_WithProviderOnly_ShouldPrompt()
    {
        // Arrange
        var config = new AppConfiguration
        {
            LastDirectory = null,
            LastProvider = AiProviderType.Ollama,
            LastModel = null
        };
        
        // Act - Simulate the logic from Program.cs lines 785-786
        var hasValidDirectory = !string.IsNullOrEmpty(config.LastDirectory);
        var hasModel = !string.IsNullOrEmpty(config.LastModel);
        var hasProvider = config.LastProvider != AiProviderType.None;
        var shouldPromptToKeep = hasValidDirectory || hasModel || hasProvider;
        
        // Assert
        await Assert.That(shouldPromptToKeep).IsTrue();
        await Assert.That(hasValidDirectory).IsFalse();
        await Assert.That(hasModel).IsFalse();
        await Assert.That(hasProvider).IsTrue();
    }
    
    [Test]
    public async Task KeepConfigurationPrompt_WithNothingConfigured_ShouldNotPrompt()
    {
        // Arrange
        var config = new AppConfiguration
        {
            LastDirectory = null,
            LastProvider = AiProviderType.None,
            LastModel = null
        };
        
        // Act - Simulate the logic from Program.cs lines 785-786
        var hasValidDirectory = !string.IsNullOrEmpty(config.LastDirectory);
        var hasModel = !string.IsNullOrEmpty(config.LastModel);
        var hasProvider = config.LastProvider != AiProviderType.None;
        var shouldPromptToKeep = hasValidDirectory || hasModel || hasProvider;
        
        // Assert
        await Assert.That(shouldPromptToKeep).IsFalse();
        await Assert.That(hasValidDirectory).IsFalse();
        await Assert.That(hasModel).IsFalse();
        await Assert.That(hasProvider).IsFalse();
    }
    
    [Test]
    public async Task KeepConfigurationPrompt_WithEmptyStrings_ShouldNotPrompt()
    {
        // Arrange
        var config = new AppConfiguration
        {
            LastDirectory = "",
            LastProvider = AiProviderType.None,
            LastModel = ""
        };
        
        // Act - Simulate the logic from Program.cs lines 785-786
        var hasValidDirectory = !string.IsNullOrEmpty(config.LastDirectory);
        var hasModel = !string.IsNullOrEmpty(config.LastModel);
        var hasProvider = config.LastProvider != AiProviderType.None;
        var shouldPromptToKeep = hasValidDirectory || hasModel || hasProvider;
        
        // Assert
        await Assert.That(shouldPromptToKeep).IsFalse();
        await Assert.That(hasValidDirectory).IsFalse();
        await Assert.That(hasModel).IsFalse();
        await Assert.That(hasProvider).IsFalse();
    }
    
    [Test]
    public async Task KeepConfigurationPrompt_WithWhitespaceDirectory_ShouldPrompt()
    {
        // Arrange
        var config = new AppConfiguration
        {
            LastDirectory = "   ", // Whitespace only
            LastProvider = AiProviderType.None,
            LastModel = null
        };
        
        // Act - Simulate the logic from Program.cs lines 785-786
        var hasValidDirectory = !string.IsNullOrEmpty(config.LastDirectory);
        var hasModel = !string.IsNullOrEmpty(config.LastModel);
        var hasProvider = config.LastProvider != AiProviderType.None;
        var shouldPromptToKeep = hasValidDirectory || hasModel || hasProvider;
        
        // Assert - string.IsNullOrEmpty doesn't trim, so whitespace is considered valid
        await Assert.That(shouldPromptToKeep).IsTrue();
        await Assert.That(hasValidDirectory).IsTrue();
        await Assert.That(hasModel).IsFalse();
        await Assert.That(hasProvider).IsFalse();
    }
    
    [Test]
    public async Task KeepConfigurationPrompt_WithAllConfigured_ShouldPrompt()
    {
        // Arrange
        var config = new AppConfiguration
        {
            LastDirectory = @"C:\Projects\MyApp",
            LastProvider = AiProviderType.OpenAI,
            LastModel = "gpt-4o-mini"
        };
        
        // Act - Simulate the logic from Program.cs lines 785-786
        var hasValidDirectory = !string.IsNullOrEmpty(config.LastDirectory);
        var hasModel = !string.IsNullOrEmpty(config.LastModel);
        var hasProvider = config.LastProvider != AiProviderType.None;
        var shouldPromptToKeep = hasValidDirectory || hasModel || hasProvider;
        
        // Assert
        await Assert.That(shouldPromptToKeep).IsTrue();
        await Assert.That(hasValidDirectory).IsTrue();
        await Assert.That(hasModel).IsTrue();
        await Assert.That(hasProvider).IsTrue();
    }
    
    [Test]
    public async Task SelectProviderPrompt_LogicConsistency_BetweenKeepAndSelect()
    {
        // This test ensures the logic for "keep configuration" and "select provider" prompting is consistent
        
        var testCases = new[]
        {
            // Case 1: None provider with model - should prompt to keep (has model) but not prompt to select provider (None provider)
            new { Provider = AiProviderType.None, Model = (string?)"llama3.2", Directory = (string?)null, 
                  ShouldPromptKeep = true, ShouldPromptSelectProvider = false },
            
            // Case 2: Valid provider with model - should prompt both to keep and to select provider
            new { Provider = AiProviderType.Ollama, Model = (string?)"llama3.2", Directory = (string?)null, 
                  ShouldPromptKeep = true, ShouldPromptSelectProvider = true },
            
            // Case 3: Valid provider without model - should prompt to keep (has provider) but not to select provider (no model)
            new { Provider = AiProviderType.LmStudio, Model = (string?)null, Directory = (string?)null, 
                  ShouldPromptKeep = true, ShouldPromptSelectProvider = false },
            
            // Case 4: Nothing configured - should not prompt either
            new { Provider = AiProviderType.None, Model = (string?)null, Directory = (string?)null, 
                  ShouldPromptKeep = false, ShouldPromptSelectProvider = false },
            
            // Case 5: Only directory configured - should prompt to keep but not to select provider
            new { Provider = AiProviderType.None, Model = (string?)null, Directory = (string?)@"C:\Projects", 
                  ShouldPromptKeep = true, ShouldPromptSelectProvider = false }
        };
        
        foreach (var testCase in testCases)
        {
            // Arrange
            var config = new AppConfiguration
            {
                LastProvider = testCase.Provider,
                LastModel = testCase.Model,
                LastDirectory = testCase.Directory
            };
            
            // Act - Simulate both logic paths
            
            // Keep configuration logic (OR condition - any one triggers prompt)
            var hasValidDirectory = !string.IsNullOrEmpty(config.LastDirectory);
            var hasModel = !string.IsNullOrEmpty(config.LastModel);
            var hasProvider = config.LastProvider != AiProviderType.None;
            var shouldPromptToKeep = hasValidDirectory || hasModel || hasProvider;
            
            // Select provider logic (AND condition - both required for prompt)
            var shouldPromptSelectProvider = hasProvider && hasModel;
            
            // Assert
            await Assert.That(shouldPromptToKeep).IsEqualTo(testCase.ShouldPromptKeep);
            await Assert.That(shouldPromptSelectProvider).IsEqualTo(testCase.ShouldPromptSelectProvider);
        }
    }
    
    [Test]
    public async Task ConfigurationPrompting_AllProviderTypes_HandledCorrectly()
    {
        // Arrange & Act & Assert
        var providers = Enum.GetValues<AiProviderType>();
        
        foreach (var provider in providers)
        {
            var config = new AppConfiguration
            {
                LastProvider = provider,
                LastModel = "test-model"
            };
            
            // Act - Test both logic paths
            var hasProvider = config.LastProvider != AiProviderType.None;
            var hasModel = !string.IsNullOrEmpty(config.LastModel);
            
            var shouldPromptToKeep = hasProvider || hasModel; // Simplified for this test
            var shouldPromptSelectProvider = hasProvider && hasModel;
            
            // Assert
            if (provider == AiProviderType.None)
            {
                await Assert.That(hasProvider).IsFalse();
                await Assert.That(shouldPromptToKeep).IsTrue(); // Because hasModel is true
                await Assert.That(shouldPromptSelectProvider).IsFalse(); // Because hasProvider is false
            }
            else
            {
                await Assert.That(hasProvider).IsTrue();
                await Assert.That(shouldPromptToKeep).IsTrue();
                await Assert.That(shouldPromptSelectProvider).IsTrue();
            }
        }
    }
}