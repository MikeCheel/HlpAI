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
}