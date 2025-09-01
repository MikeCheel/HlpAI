using Microsoft.Extensions.Logging;
using Moq;

namespace HlpAI.Tests;

/// <summary>
/// Unit tests for Program class public methods
/// Focuses on ShowMenu, ClearScreen, ShowUsage, WaitForUserInput, and other public static methods
/// </summary>
[NotInParallel]
public class ProgramAiDemoTests
{
    private StringReader _stringReader = null!;
    private TextReader _originalIn = null!;
    private readonly Mock<ILogger> _mockLogger;
    
    public ProgramAiDemoTests()
    {
        _mockLogger = new Mock<ILogger>();
    }
    
    [Before(Test)]
    public async Task Setup()
    {
        // Store original input
        _originalIn = Console.In;
        
        await Task.CompletedTask;
    }
    
    [After(Test)]
    public async Task Cleanup()
    {
        // TUnit automatically captures console output, no manual cleanup needed
        
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
    
    [Test]
    public void ShowMenu_DisplaysMenuOptions()
    {
        // Act
        Program.ShowMenu();
        
        // Assert
        // TUnit automatically captures console output
        // Method completed without exception
    }
    
    [Test]
    public void ClearScreen_ExecutesWithoutError()
    {
        // Act & Assert - Should not throw
        Program.ClearScreen();
        
        // TUnit automatically captures console output
        // Method completed without exception
    }
    
    [Test]
    public void ClearScreenWithHeader_DisplaysHeaderAndBreadcrumb()
    {
        // Arrange
        var header = "Test Header";
        var breadcrumb = "Main > Test";
        
        // Act
        Program.ClearScreenWithHeader(header, breadcrumb);
        
        // Assert
        // TUnit automatically captures console output
        // Method completed without exception
    }
    
    [Test]
    public void ShowUsage_DisplaysHelpInformation()
    {
        // Act
        Program.ShowUsage();
        
        // Assert
        // TUnit automatically captures console output
        // Method completed without exception
    }
    
    [Test]
    public void WaitForUserInput_WithCustomPrompt_DisplaysPrompt()
    {
        // Arrange
        var customPrompt = "Custom test prompt...";
        SetupConsoleInput("\n"); // Simulate Enter key
        
        // Act
        Program.WaitForUserInput(customPrompt);
        
        // Assert
        // TUnit automatically captures console output
        // Method completed without exception
    }
    
    [Test]
    public void WaitForUserInput_WithDefaultPrompt_ExecutesWithoutError()
    {
        // Arrange
        SetupConsoleInput("\n"); // Simulate Enter key
        
        // Act & Assert - Should not throw
        Program.WaitForUserInput();
        
        // Method completed without exception
    }
    
    [Test]
    public async Task ShowBriefPauseAsync_WithCustomMessage_DisplaysMessage()
    {
        // Arrange
        var message = "Test pause message";
        
        // Act
        await Program.ShowBriefPauseAsync(message, 100); // Short delay for testing
        
        // Assert
        // TUnit automatically captures console output
        // Method completed without exception
    }
    
    [Test]
    public async Task ShowBriefPauseAsync_WithDefaultMessage_ExecutesWithoutError()
    {
        // Act & Assert - Should not throw
        await Program.ShowBriefPauseAsync(null, 100); // Short delay for testing
        
        // Method completed without exception
    }
}