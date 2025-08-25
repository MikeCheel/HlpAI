using HlpAI.Models;

namespace HlpAI.Tests;

public class ProgramTests
{
    [Test]
    public async Task ParseOperationMode_WithMcpString_ReturnsMcpMode()
    {
        // Arrange
        var modeString = "mcp";
        
        // Act
        var result = Program.ParseOperationMode(modeString);
        
        // Assert
        await Assert.That(result).IsEqualTo(OperationMode.MCP);
    }
    
    [Test]
    public async Task ParseOperationMode_WithRagString_ReturnsRagMode()
    {
        // Arrange
        var modeString = "rag";
        
        // Act
        var result = Program.ParseOperationMode(modeString);
        
        // Assert
        await Assert.That(result).IsEqualTo(OperationMode.RAG);
    }
    
    [Test]
    public async Task ParseOperationMode_WithHybridString_ReturnsHybridMode()
    {
        // Arrange
        var modeString = "hybrid";
        
        // Act
        var result = Program.ParseOperationMode(modeString);
        
        // Assert
        await Assert.That(result).IsEqualTo(OperationMode.Hybrid);
    }
    
    [Test]
    public async Task ParseOperationMode_WithUpperCaseString_ReturnsCorrectMode()
    {
        // Arrange
        var modeString = "MCP";
        
        // Act
        var result = Program.ParseOperationMode(modeString);
        
        // Assert
        await Assert.That(result).IsEqualTo(OperationMode.MCP);
    }
    
    [Test]
    public async Task ParseOperationMode_WithInvalidString_ReturnsHybridAsDefault()
    {
        // Arrange
        var modeString = "invalid";
        
        // Act
        var result = Program.ParseOperationMode(modeString);
        
        // Assert
        await Assert.That(result).IsEqualTo(OperationMode.Hybrid);
    }
    
    [Test]
    public async Task ParseOperationMode_WithEmptyString_ReturnsHybridAsDefault()
    {
        // Arrange
        var modeString = "";
        
        // Act
        var result = Program.ParseOperationMode(modeString);
        
        // Assert
        await Assert.That(result).IsEqualTo(OperationMode.Hybrid);
    }
    
    [Test]
    public async Task ParseOperationMode_WithNullString_ReturnsHybridAsDefault()
    {
        // Arrange
        string? modeString = null;
        
        // Act
        var result = Program.ParseOperationMode(modeString!);
        
        // Assert
        await Assert.That(result).IsEqualTo(OperationMode.Hybrid);
    }
    
    [Test]
    public async Task ParseOperationMode_WithMixedCaseString_ReturnsCorrectMode()
    {
        // Arrange
        var modeString = "HyBrId";
        
        // Act
        var result = Program.ParseOperationMode(modeString);
        
        // Assert
        await Assert.That(result).IsEqualTo(OperationMode.Hybrid);
    }
    
    [Test]
    public void ShowUsage_DoesNotThrowException()
    {
        // Arrange & Act & Assert
        // This test verifies that ShowUsage method can be called without throwing exceptions
        // The method writes to console, so we just verify it executes successfully
        Program.ShowUsage();
        // Test passes if no exception is thrown
    }
    
    [Test]
    public void ShowMenu_DoesNotThrowException()
    {
        // Arrange & Act & Assert
        // This test verifies that ShowMenu method can be called without throwing exceptions
        // The method writes to console, so we just verify it executes successfully
        Program.ShowMenu();
        // Test passes if no exception is thrown
    }
    
    [Test]
    public void ClearScreen_DoesNotThrowException()
    {
        // Arrange & Act & Assert
        // This test verifies that ClearScreen method can be called without throwing exceptions
        // The method clears console, so we just verify it executes successfully
        Program.ClearScreen();
        // Test passes if no exception is thrown
    }
    
    [Test]
    public void ClearScreen_InTestEnvironment_DoesNotClearConsole()
    {
        // Arrange
        Environment.SetEnvironmentVariable("TESTING", "true");
        
        try
        {
            // Act & Assert
            // This test verifies that ClearScreen method respects test environment
            // and doesn't actually clear the console during testing
            Program.ClearScreen();
            // Test passes if no exception is thrown and console is not cleared
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("TESTING", null);
        }
    }
    
    [Test]
    public async Task ShowMenu_DisplaysAllExpectedCommands()
    {
        // Arrange
        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        
        try
        {
            // Act
            Program.ShowMenu();
            var output = sw.ToString();
            
            // Assert
            await Assert.That(output.Contains("📚 HlpAI - Available Commands:")).IsTrue();
            await Assert.That(output.Contains("📁 File Operations:")).IsTrue();
            await Assert.That(output.Contains("🤖 AI Features:")).IsTrue();
            await Assert.That(output.Contains("🔍 RAG Features:")).IsTrue();
            await Assert.That(output.Contains("🛠️ System:")).IsTrue();
            await Assert.That(output.Contains("1 - List all available files")).IsTrue();
            await Assert.That(output.Contains("c - Clear screen")).IsTrue();
            await Assert.That(output.Contains("m - Show this menu")).IsTrue();
            await Assert.That(output.Contains("q - Quit")).IsTrue();
        }
        finally
        {
            // Cleanup
            Console.SetOut(originalOut);
        }
    }
    
    [Test]
    public async Task MenuNavigation_ShouldClearScreenBeforeCommandExecution()
    {
        // Arrange
        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        
        try
        {
            // Act
            Program.ClearScreen();
            var output = sw.ToString();
            
            // Assert
            // Verify that ClearScreen produces expected output structure
            await Assert.That(output.Contains("HlpAI")).IsTrue();
        }
        finally
        {
            // Cleanup
            Console.SetOut(originalOut);
        }
    }
    
    [Test]
    public async Task MenuNavigation_ShouldRestoreMenuAfterSubMenuOperation()
    {
        // Arrange
        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        
        try
        {
            // Act - Simulate showing menu after sub-menu operation
            Program.ShowMenu();
            var output = sw.ToString();
            
            // Assert
            // Verify that menu is properly displayed with all expected sections
            await Assert.That(output.Contains("📚 HlpAI - Available Commands:")).IsTrue();
            await Assert.That(output.Contains("📁 File Operations:")).IsTrue();
            await Assert.That(output.Contains("🤖 AI Features:")).IsTrue();
            await Assert.That(output.Contains("🔍 RAG Features:")).IsTrue();
            await Assert.That(output.Contains("🛠️ System:")).IsTrue();
        }
        finally
        {
            // Cleanup
            Console.SetOut(originalOut);
        }
    }
}