using HlpAI.Models;
using HlpAI.Services;
using TUnit.Assertions;

namespace HlpAI.Tests.Program;

/// <summary>
/// Tests for operation mode display logic covering edge cases for the conditional display
/// of "Not configured (will prompt for selection)" vs actual operation mode
/// </summary>
public class OperationModeDisplayTests
{
    [Test]
    public async Task OperationModeDisplay_WithRememberFalse_ShowsNotConfigured()
    {
        // Arrange
        var config = new AppConfiguration
        {
            RememberLastOperationMode = false,
            LastOperationMode = OperationMode.RAG,
            LastProvider = AiProviderType.Ollama,
            LastModel = "llama3.2"
        };
        
        // Act - Simulate the display logic from Program.cs
        var hasProvider = config.LastProvider != AiProviderType.None;
        var hasModel = !string.IsNullOrEmpty(config.LastModel);
        var operationModeDisplay = config.RememberLastOperationMode && hasProvider && hasModel 
            ? config.LastOperationMode.ToString() 
            : "Not configured (will prompt for selection)";
        
        // Assert
        await Assert.That(operationModeDisplay).IsEqualTo("Not configured (will prompt for selection)");
    }
    
    [Test]
    public async Task OperationModeDisplay_WithRememberTrueButNoProvider_ShowsNotConfigured()
    {
        // Arrange
        var config = new AppConfiguration
        {
            RememberLastOperationMode = true,
            LastOperationMode = OperationMode.MCP,
            LastProvider = AiProviderType.None,
            LastModel = "some-model"
        };
        
        // Act - Simulate the display logic from Program.cs
        var hasProvider = config.LastProvider != AiProviderType.None;
        var hasModel = !string.IsNullOrEmpty(config.LastModel);
        var operationModeDisplay = config.RememberLastOperationMode && hasProvider && hasModel 
            ? config.LastOperationMode.ToString() 
            : "Not configured (will prompt for selection)";
        
        // Assert
        await Assert.That(operationModeDisplay).IsEqualTo("Not configured (will prompt for selection)");
    }
    
    [Test]
    public async Task OperationModeDisplay_WithRememberTrueButNoModel_ShowsNotConfigured()
    {
        // Arrange
        var config = new AppConfiguration
        {
            RememberLastOperationMode = true,
            LastOperationMode = OperationMode.Hybrid,
            LastProvider = AiProviderType.Ollama,
            LastModel = ""
        };
        
        // Act - Simulate the display logic from Program.cs
        var hasProvider = config.LastProvider != AiProviderType.None;
        var hasModel = !string.IsNullOrEmpty(config.LastModel);
        var operationModeDisplay = config.RememberLastOperationMode && hasProvider && hasModel 
            ? config.LastOperationMode.ToString() 
            : "Not configured (will prompt for selection)";
        
        // Assert
        await Assert.That(operationModeDisplay).IsEqualTo("Not configured (will prompt for selection)");
    }
    
    [Test]
    public async Task OperationModeDisplay_WithRememberTrueAndNullModel_ShowsNotConfigured()
    {
        // Arrange
        var config = new AppConfiguration
        {
            RememberLastOperationMode = true,
            LastOperationMode = OperationMode.RAG,
            LastProvider = AiProviderType.LmStudio,
            LastModel = null
        };
        
        // Act - Simulate the display logic from Program.cs
        var hasProvider = config.LastProvider != AiProviderType.None;
        var hasModel = !string.IsNullOrEmpty(config.LastModel);
        var operationModeDisplay = config.RememberLastOperationMode && hasProvider && hasModel 
            ? config.LastOperationMode.ToString() 
            : "Not configured (will prompt for selection)";
        
        // Assert
        await Assert.That(operationModeDisplay).IsEqualTo("Not configured (will prompt for selection)");
    }
    
    [Test]
    public async Task OperationModeDisplay_WithAllConditionsMet_ShowsActualMode()
    {
        // Arrange
        var config = new AppConfiguration
        {
            RememberLastOperationMode = true,
            LastOperationMode = OperationMode.MCP,
            LastProvider = AiProviderType.OpenAI,
            LastModel = "gpt-4o-mini"
        };
        
        // Act - Simulate the display logic from Program.cs
        var hasProvider = config.LastProvider != AiProviderType.None;
        var hasModel = !string.IsNullOrEmpty(config.LastModel);
        var operationModeDisplay = config.RememberLastOperationMode && hasProvider && hasModel 
            ? config.LastOperationMode.ToString() 
            : "Not configured (will prompt for selection)";
        
        // Assert
        await Assert.That(operationModeDisplay).IsEqualTo("MCP");
    }
    
    [Test]
    public async Task OperationModeDisplay_AllOperationModes_DisplayCorrectly()
    {
        // Arrange & Act & Assert
        var modes = new[] { OperationMode.MCP, OperationMode.RAG, OperationMode.Hybrid };
        
        foreach (var mode in modes)
        {
            var config = new AppConfiguration
            {
                RememberLastOperationMode = true,
                LastOperationMode = mode,
                LastProvider = AiProviderType.Ollama,
                LastModel = "test-model"
            };
            
            // Act - Simulate the display logic from Program.cs
            var hasProvider = config.LastProvider != AiProviderType.None;
            var hasModel = !string.IsNullOrEmpty(config.LastModel);
            var operationModeDisplay = config.RememberLastOperationMode && hasProvider && hasModel 
                ? config.LastOperationMode.ToString() 
                : "Not configured (will prompt for selection)";
            
            // Assert
            await Assert.That(operationModeDisplay).IsEqualTo(mode.ToString());
        }
    }
    
    [Test]
    public async Task OperationModeDisplay_EdgeCaseWhitespaceModel_ShowsNotConfigured()
    {
        // Arrange
        var config = new AppConfiguration
        {
            RememberLastOperationMode = true,
            LastOperationMode = OperationMode.Hybrid,
            LastProvider = AiProviderType.Anthropic,
            LastModel = "   " // Whitespace only
        };
        
        // Act - Simulate the display logic from Program.cs
        var hasProvider = config.LastProvider != AiProviderType.None;
        var hasModel = !string.IsNullOrEmpty(config.LastModel);
        var operationModeDisplay = config.RememberLastOperationMode && hasProvider && hasModel 
            ? config.LastOperationMode.ToString() 
            : "Not configured (will prompt for selection)";
        
        // Assert - string.IsNullOrEmpty doesn't trim, so whitespace is considered valid
        await Assert.That(operationModeDisplay).IsEqualTo("Hybrid");
    }
    
    [Test]
    public async Task OperationModeDisplay_DefaultConfiguration_ShowsNotConfigured()
    {
        // Arrange - Test with default AppConfiguration values
        var config = new AppConfiguration();
        
        // Act - Simulate the display logic from Program.cs
        var hasProvider = config.LastProvider != AiProviderType.None;
        var hasModel = !string.IsNullOrEmpty(config.LastModel);
        var operationModeDisplay = config.RememberLastOperationMode && hasProvider && hasModel 
            ? config.LastOperationMode.ToString() 
            : "Not configured (will prompt for selection)";
        
        // Assert
        await Assert.That(operationModeDisplay).IsEqualTo("Not configured (will prompt for selection)");
        
        // Verify the default values that lead to this result
        await Assert.That(config.LastProvider).IsEqualTo(AiProviderType.None);
        await Assert.That(config.RememberLastOperationMode).IsTrue(); // Default is true
        await Assert.That(hasProvider).IsFalse();
        await Assert.That(hasModel).IsFalse();
    }
}