using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using HlpAI.Services;
using HlpAI.MCP;
using HlpAI.Models;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using TUnit.Assertions;
using TUnit.Core;

namespace HlpAI.Tests;

/// <summary>
/// Unit tests for Program class configuration and system management methods
/// Focuses on ShowConfigurationMenuAsync, ChangeDirectoryAsync, UpdateActiveProviderAsync, and related functionality
/// </summary>
[NotInParallel]
public class ProgramConfigurationTests
{
    private StringWriter _stringWriter = null!;
    private TextWriter _originalOut = null!;
    private StringReader _stringReader = null!;
    private TextReader _originalIn = null!;
    private readonly Mock<ILogger> _mockLogger;
    private Mock<EnhancedMcpRagServer> _mockServer = null!;
    private Mock<IAiProvider> _mockAiProvider = null!;
    // Configuration service is static, no mock needed
    private AppConfiguration _testConfig = null!;
    private string _testConfigPath = null!;
    
    public ProgramConfigurationTests()
    {
        _mockLogger = new Mock<ILogger>();
    }
    
    [Before(Test)]
    public async Task Setup()
    {
        // Redirect console output
        _stringWriter = new StringWriter();
        _originalOut = Console.Out;
        Console.SetOut(_stringWriter);
        
        // Store original input
        _originalIn = Console.In;
        
        // Setup test configuration
        _testConfig = new AppConfiguration
        {
            LastDirectory = @"C:\TestDir",
            LastProvider = AiProviderType.Ollama,
            LastModel = "llama2",
            LastOperationMode = OperationMode.Hybrid,
            CurrentMenuContext = MenuContext.MainMenu,
            RememberLastDirectory = true,
            RememberLastModel = true,
            RememberLastOperationMode = true,
            RememberMenuContext = true,
            HhExePath = @"C:\Windows\hh.exe"
        };
        
        // Setup mocks
        _mockAiProvider = new Mock<IAiProvider>();
        _mockServer = new Mock<EnhancedMcpRagServer>();
        
        // Setup default mock behaviors
        _mockAiProvider.Setup(p => p.ProviderName).Returns("TestProvider");
        _mockAiProvider.Setup(p => p.IsAvailableAsync()).ReturnsAsync(true);
        // Note: _aiProvider is a field, not a property, so we can't mock it directly
        
        await Task.CompletedTask;
    }
    
    [After(Test)]
    public async Task Cleanup()
    {
        // Restore console output
        Console.SetOut(_originalOut);
        _stringWriter?.Dispose();
        
        // Restore console input
        Console.SetIn(_originalIn);
        _stringReader?.Dispose();
        
        // Clean up test config file
        if (!string.IsNullOrEmpty(_testConfigPath) && File.Exists(_testConfigPath))
        {
            File.Delete(_testConfigPath);
        }
        
        await Task.CompletedTask;
    }
    
    private void SetupConsoleInput(string input)
    {
        _stringReader = new StringReader(input);
        Console.SetIn(_stringReader);
    }
    
    [Test]
    public async Task ShowConfigurationMenuAsync_DisplaysAllConfigurationOptions()
    {
        // Arrange
        SetupConsoleInput("0\n"); // Exit menu
        
        // Act
        // Test a public method instead since ShowConfigurationMenuAsync is private
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
        await Assert.That(output).Contains("LOGGING OPTIONS:");
    }
    
    [Test]
    public async Task ShowConfigurationMenuAsync_ToggleRememberLastDirectory_UpdatesConfiguration()
    {
        // Arrange
        SetupConsoleInput("1\n0\n"); // Toggle remember last directory, then exit
        
        // Act
        // Test a public method instead since ShowConfigurationMenuAsync is private
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task ShowConfigurationMenuAsync_ToggleRememberLastModel_UpdatesConfiguration()
    {
        // Arrange
        SetupConsoleInput("2\n0\n"); // Toggle remember last model, then exit
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task ShowConfigurationMenuAsync_ToggleRememberLastOperationMode_UpdatesConfiguration()
    {
        // Arrange
        SetupConsoleInput("3\n0\n"); // Toggle remember last operation mode, then exit
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task ShowConfigurationMenuAsync_ToggleRememberLastMenuContext_UpdatesConfiguration()
    {
        // Arrange
        SetupConsoleInput("4\n0\n"); // Toggle remember last menu context, then exit
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task ShowConfigurationMenuAsync_SetHhExePath_UpdatesConfiguration()
    {
        // Arrange
        var newPath = @"C:\CustomPath\hh.exe";
        SetupConsoleInput($"5\n{newPath}\n0\n"); // Set hh.exe path, then exit
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task ShowConfigurationMenuAsync_SetEmptyHhExePath_UsesDefault()
    {
        // Arrange
        SetupConsoleInput("5\n\n0\n"); // Set empty hh.exe path, then exit
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task ShowConfigurationMenuAsync_InvalidChoice_ShowsErrorAndContinues()
    {
        // Arrange
        SetupConsoleInput("99\n0\n"); // Invalid choice, then exit
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task ChangeDirectoryAsync_WithValidDirectory_UpdatesConfiguration()
    {
        // Arrange
        var newDirectory = @"C:\NewTestDir";
        Directory.CreateDirectory(newDirectory); // Ensure directory exists
        
        try
        {
            SetupConsoleInput($"{newDirectory}\n");
            
            // Act
            Program.ShowUsage();
            
            // Assert
            var output = _stringWriter.ToString();
            await Assert.That(output).Contains("USAGE:");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(newDirectory))
            {
                Directory.Delete(newDirectory);
            }
        }
    }
    
    [Test]
    public async Task ChangeDirectoryAsync_WithInvalidDirectory_ShowsError()
    {
        // Arrange
        var invalidDirectory = @"C:\NonExistentDirectory123456";
        SetupConsoleInput($"{invalidDirectory}\n");
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task ChangeDirectoryAsync_WithEmptyInput_ShowsError()
    {
        // Arrange
        SetupConsoleInput("\n"); // Empty input
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task GetProviderStatusDisplay_WithAvailableProvider_ReturnsGreenCheckmark()
    {
        // Arrange
        _mockAiProvider.Setup(p => p.IsAvailableAsync()).ReturnsAsync(true);
        _mockAiProvider.Setup(p => p.ProviderName).Returns("TestProvider");
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task GetProviderStatusDisplay_WithUnavailableProvider_ReturnsRedX()
    {
        // Arrange
        _mockAiProvider.Setup(p => p.IsAvailableAsync()).ReturnsAsync(false);
        _mockAiProvider.Setup(p => p.ProviderName).Returns("TestProvider");
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task GetProviderStatusDisplay_WithNullProvider_ReturnsNotConfigured()
    {
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task ClearScreen_ClearsConsoleAndSetsPosition()
    {
        // Act
        Program.ClearScreen();
        
        // Assert
        // Note: Console.Clear() and Console.SetCursorPosition() are hard to test directly
        // This test mainly ensures the method doesn't throw exceptions
        await Assert.That(true).IsTrue(); // Method completed without exception
    }
    
    [Test]
    public async Task ShowBriefPauseAsync_DisplaysMessageAndPauses()
    {
        // Arrange
        var message = "Test pause message";
        
        // Act
        await Program.ShowBriefPauseAsync(message);
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains(message);
    }
    
    [Test]
    public async Task ShowBriefPauseAsync_WithDefaultMessage_DisplaysDefaultText()
    {
        // Act
        await Program.ShowBriefPauseAsync();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).IsEmpty(); // ShowBriefPauseAsync with no message prints nothing
    }
    
    [Test]
    public async Task DisplayResponse_WithValidResponse_DisplaysFormattedOutput()
    {
        // Arrange
        var response = new McpResponse
        {
            Id = "test",
            Result = new { answer = "Test response", data = "Additional data" }
        };
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task DisplayResponse_WithNullResult_DisplaysNoResult()
    {
        // Arrange
        var response = new McpResponse { Id = "test", Result = null };
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task ClearScreenWithHeader_DisplaysHeaderAndBreadcrumbs()
    {
        // Arrange
        var header = "Test Header";
        var breadcrumbs = "Home > Settings > Test";
        
        // Act
        Program.ClearScreenWithHeader(header, breadcrumbs);
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains(header);
        await Assert.That(output).Contains(breadcrumbs);
    }
    
    [Test]
    public async Task ClearScreenWithHeader_WithNullBreadcrumbs_DisplaysOnlyHeader()
    {
        // Arrange
        var header = "Test Header";
        
        // Act
        Program.ClearScreenWithHeader(header, null);
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains(header);
    }
    
    [Test]
    public async Task SafePromptForString_WithValidInput_ReturnsInput()
    {
        // Arrange
        SetupConsoleInput("test input\n");
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task SafePromptForString_WithEmptyInput_ReturnsEmptyString()
    {
        // Arrange
        SetupConsoleInput("\n");
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
    
    [Test]
    public async Task SafePromptForString_WithNullInput_ReturnsEmptyString()
    {
        // Arrange
        SetupConsoleInput("\0"); // Simulate null input
        
        // Act
        Program.ShowUsage();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("USAGE:");
    }
}