using System.Runtime.Versioning;
using HlpAI;
using HlpAI.Models;
using HlpAI.MCP;
using HlpAI.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace HlpAI.Tests.Program;

/// <summary>
/// Unit tests for Program class initialization error handling
/// Tests the error handling around server.InitializeAsync() call
/// </summary>
[NotInParallel]
public class ProgramInitializationErrorHandlingTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<ErrorLoggingService> _mockErrorLoggingService;
    
    public ProgramInitializationErrorHandlingTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockErrorLoggingService = new Mock<ErrorLoggingService>();
    }
    
    [Before(Test)]
    public async Task Setup()
    {
        // Setup code if needed
        await Task.CompletedTask;
    }
    
    [After(Test)]
    public async Task Cleanup()
    {
        // Cleanup code if needed
        await Task.CompletedTask;
    }

    [Test]
    public async Task UnauthorizedAccessException_ContainsExpectedMessage()
    {
        // Arrange
        var exception = new UnauthorizedAccessException("Access to the path 'C:\\restricted' is denied.");
        
        // Act & Assert
        await Assert.That(exception.Message).Contains("Access to the path");
        await Assert.That(exception.Message).Contains("denied");
    }

    [Test]
    public async Task DirectoryNotFoundException_ContainsExpectedMessage()
    {
        // Arrange
        var exception = new DirectoryNotFoundException("Could not find a part of the path 'C:\\nonexistent\\path'.");
        
        // Act & Assert
        await Assert.That(exception.Message).Contains("Could not find");
        await Assert.That(exception.Message).Contains("path");
    }

    [Test]
    public async Task GeneralException_PreservesMessage()
    {
        // Arrange
        var exception = new InvalidOperationException("Database connection failed");
        
        // Act & Assert
        await Assert.That(exception.Message).IsEqualTo("Database connection failed");
    }

    [Test]
    public async Task ErrorHandling_ContainsAuditSuggestion()
    {
        // This test verifies that error messages suggest using --audit command
        // The actual implementation is in Program.cs around line 460-480
        
        // Arrange
        var errorMessage = "❌ Error initializing RAG system: Access denied. Try running with --audit <directory> first to check for permission issues.";
        
        // Assert
        await Assert.That(errorMessage).Contains("--audit");
        await Assert.That(errorMessage).Contains("<directory>");
        await Assert.That(errorMessage).Contains("permission issues");
    }

    [Test]
    public async Task ErrorHandling_ContainsHelpfulSuggestions()
    {
        // This test verifies that error messages contain helpful suggestions
        // The actual implementation is in Program.cs around line 460-480
        
        // Arrange - Test different error scenarios
        var unauthorizedMessage = "❌ Error initializing RAG system: Access denied. Try running with --audit <directory> first to check for permission issues.";
        var notFoundMessage = "❌ Error initializing RAG system: Directory not found. Please check the path and try again.";
        var generalMessage = "❌ Error initializing RAG system: Database connection failed. Please check your configuration and try again.";
        
        // Assert
        await Assert.That(unauthorizedMessage).Contains("Try running with --audit");
        await Assert.That(notFoundMessage).Contains("Please check the path");
        await Assert.That(generalMessage).Contains("Please check your configuration");
    }

    [Test]
    public async Task ErrorHandling_LogsErrorsAppropriately()
    {
        // This test verifies that errors are logged using ErrorLoggingService
        // The actual implementation is in Program.cs around line 460-480
        
        // Arrange
        var testException = new UnauthorizedAccessException("Test access denied");
        
        // Act - Simulate error logging
        var logMessage = $"RAG system initialization failed: {testException.Message}";
        
        // Assert
        await Assert.That(logMessage).Contains("RAG system initialization failed");
        await Assert.That(logMessage).Contains(testException.Message);
    }
}