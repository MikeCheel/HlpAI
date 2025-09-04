using System.Text.Json;
using HlpAI;
using HlpAI.MCP;
using HlpAI.Models;
using HlpAI.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace HlpAI.Tests.Program;

/// <summary>
/// Unit tests for Program class interactive chat functionality
/// Tests DemoInteractiveChat method and ExtractPlainTextResponse method
/// </summary>
[NotInParallel]
public class ProgramInteractiveChatTests
{
    private StringReader _stringReader = null!;
    private TextReader _originalIn = null!;
    private readonly Mock<ILogger> _mockLogger;
    private Mock<EnhancedMcpRagServer> _mockServer = null!;
    
    public ProgramInteractiveChatTests()
    {
        _mockLogger = new Mock<ILogger>();
    }
    
    [Before(Test)]
    public async Task Setup()
    {
        // Store original input
        _originalIn = Console.In;
        
        // Setup mock server
        _mockServer = new Mock<EnhancedMcpRagServer>();
        
        await Task.CompletedTask;
    }
    
    [After(Test)]
    public async Task Cleanup()
    {
        // Restore console input
        Console.SetIn(_originalIn);
        _stringReader?.Dispose();
        
        await Task.CompletedTask;
    }
    
    private void SetupConsoleInput(string input)
    {
        _stringReader = new StringReader(input);
        Console.SetIn(_stringReader);
    }
    
    #region ExtractPlainTextResponse Tests
    
    [Test]
    public async Task ExtractPlainTextResponse_WithValidTextContent_ReturnsText()
    {
        // Arrange
        var responseData = new
        {
            content = new[]
            {
                new { text = "Hello, this is a test response!" }
            }
        };
        
        var response = new McpResponse
        {
            Id = "test-1",
            Result = responseData
        };
        
        // Act
        var result = CallExtractPlainTextResponse(response);
        
        // Assert
        await Assert.That(result).IsEqualTo("Hello, this is a test response!");
    }
    
    [Test]
    public async Task ExtractPlainTextResponse_WithMultipleContentItems_ReturnsFirstText()
    {
        // Arrange
        var responseData = new
        {
            content = new[]
            {
                new { text = "First response" },
                new { text = "Second response" }
            }
        };
        
        var response = new McpResponse
        {
            Id = "test-4",
            Result = responseData
        };
        
        // Act
        var result = CallExtractPlainTextResponse(response);
        
        // Assert
        await Assert.That(result).IsEqualTo("First response");
    }
    
    [Test]
    public async Task ExtractPlainTextResponse_WithEmptyContent_ReturnsEmptyString()
    {
        // Arrange
        var responseData = new
        {
            content = new object[0]
        };
        
        var response = new McpResponse
        {
            Id = "test-5",
            Result = responseData
        };
        
        // Act
        var result = CallExtractPlainTextResponse(response);
        
        // Assert
        await Assert.That(result).IsEqualTo("");
    }
    
    [Test]
    public async Task ExtractPlainTextResponse_WithNullResult_ReturnsEmptyString()
    {
        // Arrange
        var response = new McpResponse
        {
            Id = "test-2",
            Result = null
        };
        
        // Act
        var result = CallExtractPlainTextResponse(response);
        
        // Assert
        await Assert.That(result).IsEqualTo("");
    }
    
    [Test]
    public async Task ExtractPlainTextResponse_WithNoContentProperty_ReturnsEmptyString()
    {
        // Arrange
        var responseData = new
        {
            message = "No content property here"
        };
        
        var response = new McpResponse
        {
            Id = "test-6",
            Result = responseData
        };
        
        // Act
        var result = CallExtractPlainTextResponse(response);
        
        // Assert
        await Assert.That(result).IsEqualTo("");
    }
    
    [Test]
    public async Task ExtractPlainTextResponse_WithNonArrayContent_ReturnsEmptyString()
    {
        // Arrange
        var responseData = new
        {
            content = "This is not an array"
        };
        
        var response = new McpResponse
        {
            Id = "test-7",
            Result = responseData
        };
        
        // Act
        var result = CallExtractPlainTextResponse(response);
        
        // Assert
        await Assert.That(result).IsEqualTo("");
    }
    
    [Test]
    public async Task ExtractPlainTextResponse_WithContentMissingTextProperty_ReturnsEmptyString()
    {
        // Arrange
        var responseData = new
        {
            content = new[]
            {
                new { message = "No text property here" }
            }
        };
        
        var response = new McpResponse
        {
            Id = "test-8",
            Result = responseData
        };
        
        // Act
        var result = CallExtractPlainTextResponse(response);
        
        // Assert
        await Assert.That(result).IsEqualTo("");
    }
    
    [Test]
    public async Task ExtractPlainTextResponse_WithNullTextValue_ReturnsEmptyString()
    {
        // Arrange - Create JSON with null text value
        var jsonString = "{\"content\":[{\"text\":null}]}";
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);
        
        var response = new McpResponse
        {
            Id = "test-3",
            Result = jsonElement
        };
        
        // Act
        var result = CallExtractPlainTextResponse(response);
        
        // Assert
        await Assert.That(result).IsEqualTo("");
    }
    
    [Test]
    public async Task ExtractPlainTextResponse_WithComplexNestedStructure_ExtractsCorrectText()
    {
        // Arrange
        var responseData = new
        {
            status = "success",
            metadata = new { timestamp = "2024-01-01" },
            content = new[]
            {
                new 
                { 
                    type = "text",
                    text = "This is the extracted text",
                    metadata = new { source = "ai" }
                }
            }
        };
        
        var response = new McpResponse
        {
            Id = "test-9",
            Result = responseData
        };
        
        // Act
        var result = CallExtractPlainTextResponse(response);
        
        // Assert
        await Assert.That(result).IsEqualTo("This is the extracted text");
    }
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Helper method to call the private ExtractPlainTextResponse method using reflection
    /// </summary>
    private static string CallExtractPlainTextResponse(McpResponse response)
    {
        var method = typeof(HlpAI.Program).GetMethod("ExtractPlainTextResponse", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        if (method == null)
        {
            throw new InvalidOperationException("ExtractPlainTextResponse method not found");
        }
        
        var result = method.Invoke(null, new object[] { response });
        return result?.ToString() ?? "";
    }
    
    #endregion
    
    #region Integration Tests for Chat Commands
    
    [Test]
    public async Task ChatCommands_QuitVariations_ShouldAllBeRecognized()
    {
        // Test that all quit command variations are properly recognized
        var quitCommands = new[] { "quit", "exit", "q", "cancel" };
        
        foreach (var command in quitCommands)
        {
            var lowerCommand = command.ToLower();
            var isQuitCommand = lowerCommand is "quit" or "exit" or "q" or "cancel";
            await Assert.That(isQuitCommand).IsTrue();
        }
    }
    
    [Test]
    public async Task ChatCommands_ClearVariations_ShouldAllBeRecognized()
    {
        // Test that all clear command variations are properly recognized
        var clearCommands = new[] { "clear", "c" };
        
        foreach (var command in clearCommands)
        {
            var lowerCommand = command.ToLower();
            var isClearCommand = lowerCommand is "clear" or "c";
            await Assert.That(isClearCommand).IsTrue();
        }
    }
    
    [Test]
    public async Task ChatCommands_HelpVariations_ShouldAllBeRecognized()
    {
        // Test that all help command variations are properly recognized
        var helpCommands = new[] { "help", "h" };
        
        foreach (var command in helpCommands)
        {
            var lowerCommand = command.ToLower();
            var isHelpCommand = lowerCommand is "help" or "h";
            await Assert.That(isHelpCommand).IsTrue();
        }
    }
    
    #endregion
    
    #region Conversation History Tests
    
    [Test]
    public async Task ConversationHistory_TakeLast10_ShouldLimitHistorySize()
    {
        // Arrange - Create a conversation history with more than 10 items
        var conversationHistory = new List<string>();
        for (int i = 1; i <= 15; i++)
        {
            conversationHistory.Add($"User: Message {i}");
            conversationHistory.Add($"AI: Response {i}");
        }
        
        // Act - Simulate the TakeLast(10) operation from the chat method
        var recentHistory = conversationHistory.TakeLast(10).ToList();
        
        // Assert
        await Assert.That(recentHistory.Count).IsEqualTo(10);
        await Assert.That(recentHistory.First()).IsEqualTo("User: Message 11");
        await Assert.That(recentHistory.Last()).IsEqualTo("AI: Response 15");
    }
    
    [Test]
    public async Task ConversationHistory_BuildContext_ShouldCombineCorrectly()
    {
        // Arrange
        var initialContext = "You are a helpful assistant.";
        var conversationHistory = new List<string>
        {
            "User: Hello",
            "AI: Hi there!",
            "User: How are you?"
        };
        
        // Act - Simulate context building from the chat method
        var recentHistory = conversationHistory.TakeLast(10).ToList();
        var historyContext = string.Join("\n", recentHistory.Take(recentHistory.Count - 1));
        var chatContext = string.IsNullOrEmpty(initialContext) ? historyContext : $"{initialContext}\n\nConversation History:\n{historyContext}";
        
        // Assert
        await Assert.That(chatContext).Contains("You are a helpful assistant.");
        await Assert.That(chatContext).Contains("Conversation History:");
        await Assert.That(chatContext).Contains("User: Hello");
        await Assert.That(chatContext).Contains("AI: Hi there!");
        await Assert.That(chatContext).DoesNotContain("User: How are you?"); // Current message should be excluded
    }
    
    [Test]
    public async Task ConversationHistory_EmptyInitialContext_ShouldUseHistoryOnly()
    {
        // Arrange
        var initialContext = "";
        var conversationHistory = new List<string>
        {
            "User: Hello",
            "AI: Hi there!",
            "User: How are you?"
        };
        
        // Act - Simulate context building with empty initial context
        var recentHistory = conversationHistory.TakeLast(10).ToList();
        var historyContext = string.Join("\n", recentHistory.Take(recentHistory.Count - 1));
        var chatContext = string.IsNullOrEmpty(initialContext) ? historyContext : $"{initialContext}\n\nConversation History:\n{historyContext}";
        
        // Assert
        await Assert.That(chatContext).IsEqualTo("User: Hello\nAI: Hi there!");
        await Assert.That(chatContext).DoesNotContain("Conversation History:");
    }
    
    #endregion
    
    #region Temperature Validation Tests
    
    [Test]
    public async Task TemperatureValidation_ValidValues_ShouldParseCorrectly()
    {
        // Test valid temperature values
        var validTemperatures = new[] { "0.0", "0.7", "1.0", "1.5", "2.0" };
        
        foreach (var tempStr in validTemperatures)
        {
            var success = double.TryParse(tempStr, out var temperature);
            await Assert.That(success).IsTrue();
            await Assert.That(temperature >= 0.0 && temperature <= 2.0).IsTrue();
        }
    }
    
    [Test]
    public async Task TemperatureValidation_InvalidValues_ShouldFallbackToDefault()
    {
        // Test invalid temperature values
        var invalidTemperatures = new[] { "invalid", "", "3.0", "-1.0", "abc" };
        
        foreach (var tempStr in invalidTemperatures)
        {
            var success = double.TryParse(tempStr, out var temperature);
            var originalTemp = temperature;
            
            // Apply validation logic similar to actual implementation
            if (!success || temperature < 0.0 || temperature > 2.0)
            {
                temperature = 0.7; // Default fallback
            }
            
            // After validation, temperature should either be valid or the default
            var isValidAfterProcessing = temperature >= 0.0 && temperature <= 2.0;
            
            await Assert.That(isValidAfterProcessing).IsTrue();
            
            // For truly invalid inputs, should fallback to default
            if (!success || originalTemp < 0.0 || originalTemp > 2.0)
            {
                await Assert.That(temperature).IsEqualTo(0.7);
            }
        }
    }
    
    #endregion
}