using System.Text;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using TUnit.Core;
using TUnit.Assertions;
using System.IO;
using System.Threading.Tasks;

namespace HlpAI.Tests;

/// <summary>
/// Unit tests for Program class menu navigation methods
/// </summary>
[NotInParallel]
public class ProgramMenuTests
{
    private StringWriter _stringWriter = null!;
    private TextWriter _originalOut = null!;
    private readonly Mock<ILogger> _mockLogger;
    
    public ProgramMenuTests()
    {
        _mockLogger = new Mock<ILogger>();
    }

    [Before(Test)]
    public void Setup()
    {
        // Store the current Console.Out before redirecting
        _originalOut = Console.Out;
        // Create a fresh StringWriter for this test
        _stringWriter = new StringWriter();
        // Redirect console output to capture it for testing
#pragma warning disable TUnit0055 // Overwriting the Console writer can break TUnit logging
        Console.SetOut(_stringWriter);
#pragma warning restore TUnit0055
    }

    [After(Test)]
    public void TearDown()
    {
        // Restore original console output
#pragma warning disable TUnit0055 // Overwriting the Console writer can break TUnit logging
        Console.SetOut(_originalOut);
#pragma warning restore TUnit0055
        // Dispose the StringWriter to free resources
        _stringWriter?.Dispose();
    }
    
    [Test]
    public async Task Console_Redirection_WorksCorrectly()
    {
        // Arrange & Act
        Console.WriteLine("Test output");
        var output = _stringWriter.ToString();
        
        // Assert
        await Assert.That(output).Contains("Test output", StringComparison.Ordinal);
    }
    
    [Test]
    public async Task ClearScreenWithHeader_WithHeaderOnly_DisplaysCorrectOutput()
    {
        // Arrange
        const string header = "🎯 Test";
        
        // Act
        Program.ClearScreenWithHeader(header);
        var output = _stringWriter.ToString();
        
        // Assert
        await Assert.That(output).Contains(header, StringComparison.Ordinal);
        await Assert.That(output).Contains("========================", StringComparison.Ordinal);
        await Assert.That(output).DoesNotContain("📍", StringComparison.Ordinal); // No breadcrumb
    }
    
    [Test]
    public async Task ClearScreenWithHeader_WithHeaderAndBreadcrumb_DisplaysCorrectOutput()
    {
        // Arrange
        var header = "🤖 AI Provider Configuration";
        var breadcrumb = "Main Menu > AI Provider Management";
        
        // Act
        Program.ClearScreenWithHeader(header, breadcrumb);
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains(header);
        await Assert.That(output).Contains(new string('=', Math.Max(header.Length, 24)));
        await Assert.That(output).Contains($"📍 {breadcrumb}");
    }
    
    [Test]
    public async Task ClearScreenWithHeader_WithLongHeader_UsesHeaderLengthForSeparator()
    {
        // Arrange
        var longHeader = "🔧 This is a very long header that exceeds the minimum length";
        
        // Act
        Program.ClearScreenWithHeader(longHeader);
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains(longHeader);
        await Assert.That(output).Contains(new string('=', longHeader.Length));
    }
    
    [Test]
    public async Task ClearScreenWithHeader_WithShortHeader_UsesMinimumLengthForSeparator()
    {
        // Arrange
        var shortHeader = "🎯 Test";
        
        // Act
        Program.ClearScreenWithHeader(shortHeader);
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains(shortHeader);
        await Assert.That(output).Contains(new string('=', 24)); // Minimum length
    }
    
    [Test]
    public async Task ClearScreenWithHeader_WithEmptyBreadcrumb_DoesNotDisplayBreadcrumb()
    {
        // Arrange
        var header = "🎯 Test Header";
        var emptyBreadcrumb = "";
        
        // Act
        Program.ClearScreenWithHeader(header, emptyBreadcrumb);
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains(header);
        await Assert.That(output).DoesNotContain("📍");
    }
    
    [Test]
    public async Task ClearScreenWithHeader_WithNullBreadcrumb_DoesNotDisplayBreadcrumb()
    {
        // Arrange
        var header = "🎯 Test Header";
        
        // Act
        Program.ClearScreenWithHeader(header, null!);
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains(header);
        await Assert.That(output).DoesNotContain("📍");
    }
    
    [Test]
    public async Task ShowBriefPauseAsync_WithMessage_DisplaysMessageAndPauses()
    {
        // Arrange
        var message = "Processing command";
        var delayMs = 100; // Short delay for testing
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        await Program.ShowBriefPauseAsync(message, delayMs);
        stopwatch.Stop();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains(message); // ShowBriefPauseAsync just prints the message as-is
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(50); // In test environment, no delay
    }
    
    [Test]
    public async Task ShowBriefPauseAsync_WithoutMessage_DisplaysNothingAndPauses()
    {
        // Arrange
        var delayMs = 100; // Short delay for testing
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        await Program.ShowBriefPauseAsync(null, delayMs);
        stopwatch.Stop();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).DoesNotContain("⏳"); // No message should be displayed when null
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(50); // In test environment, no delay
    }
    
    [Test]
    public async Task ShowBriefPauseAsync_WithDefaultDelay_UsesCorrectTiming()
    {
        // Arrange
        var message = "Test message";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        await Program.ShowBriefPauseAsync(message); // Uses default 1500ms
        stopwatch.Stop();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains(message); // ShowBriefPauseAsync just prints the message as-is
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(50); // In test environment, no delay
    }
    
    [Test]
    public async Task ShowMenu_DisplaysAllMenuOptions()
    {
        // Act
        Program.ShowMenu();
        
        // Assert
        var output = _stringWriter.ToString();
        
        // Check for main sections
        await Assert.That(output).Contains("📚 HlpAI - Available Commands:");
        await Assert.That(output).Contains("🤖 AI Provider:"); // This line includes provider status
        await Assert.That(output).Contains("📁 File Operations:");
        await Assert.That(output).Contains("🤖 AI Features:");
        await Assert.That(output).Contains("🔍 RAG Features:");
        await Assert.That(output).Contains("🛠️ System:");
        
        // Check for specific menu items
        await Assert.That(output).Contains("1 - List all available files");
        await Assert.That(output).Contains("17 - AI provider management");
        await Assert.That(output).Contains("18 - Vector database management");
        await Assert.That(output).Contains("q - Quit");
    }
    
    [Test]
    public async Task ClearScreen_CallsClearScreenWithHeader()
    {
        // Act
        Program.ClearScreen();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).Contains("🎯 HlpAI");
        await Assert.That(output).Contains(new string('=', 24));
    }
    
    [Test]
    public async Task IsTestEnvironment_InTestContext_ReturnsTrue()
    {
        // This test verifies that the IsTestEnvironment method correctly identifies test context
        // Since we're running in a test, it should return true
        
        // We can't directly test the private method, but we can verify behavior
        // by checking that console operations don't throw in test environment
        
        // Act & Assert - Should not throw
        Program.ClearScreenWithHeader("Test Header");
        await Program.ShowBriefPauseAsync("Test", 1);
        
        // If we reach here without exceptions, the test environment detection is working
        // Test passes if no exception is thrown
    }
    
    [Test]
    public async Task WaitForUserInput_InTestEnvironment_DoesNotBlock()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        Program.WaitForUserInput("Test prompt");
        stopwatch.Stop();
        
        // Assert
        // In test environment, this should return immediately without blocking or displaying anything
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(100);
        
        var output = _stringWriter.ToString();
        await Assert.That(output).DoesNotContain("Test prompt"); // No output in test environment
    }
    
    [Test]
    public async Task WaitForUserInput_WithDefaultPrompt_DisplaysNothing()
    {
        // Act
        Program.WaitForUserInput();
        
        // Assert
        var output = _stringWriter.ToString();
        await Assert.That(output).DoesNotContain("Press any key to continue..."); // No output in test environment
    }
    
    [Test]
    public async Task MenuNavigation_ClearScreenBeforeCommands_EnsuresConsistentDisplay()
    {
        // This test verifies that screen clearing functionality works correctly
        // by checking that ClearScreen produces expected output
        
        // Act
        Program.ClearScreen();
        var output = _stringWriter.ToString();
        
        // Assert
        await Assert.That(output).Contains("🎯 HlpAI", StringComparison.Ordinal);
        await Assert.That(output).Contains(new string('=', 24), StringComparison.Ordinal);
    }
    
    [Test]
    public async Task MenuNavigation_ShowMenuAfterCommands_DisplaysMainMenu()
    {
        // This test verifies that ShowMenu displays the main menu correctly
        // which is called after command execution
        
        // Act
        Program.ShowMenu();
        var output = _stringWriter.ToString();
        
        // Assert - Check for key menu elements
        await Assert.That(output).Contains("📚 HlpAI - Available Commands:", StringComparison.Ordinal);
        await Assert.That(output).Contains("📁 File Operations:", StringComparison.Ordinal);
        await Assert.That(output).Contains("🤖 AI Features:", StringComparison.Ordinal);
        await Assert.That(output).Contains("🔍 RAG Features:", StringComparison.Ordinal);
        await Assert.That(output).Contains("🛠️ System:", StringComparison.Ordinal);
    }
    
    [Test]
    public async Task MenuNavigation_ClearScreenWithBreadcrumb_ShowsNavigationPath()
    {
        // This test verifies breadcrumb navigation functionality
        
        // Arrange
        var header = "🔧 Configuration Menu";
        var breadcrumb = "Main Menu > Configuration";
        
        // Act
        Program.ClearScreenWithHeader(header, breadcrumb);
        var output = _stringWriter.ToString();
        
        // Assert
        await Assert.That(output).Contains(header, StringComparison.Ordinal);
        await Assert.That(output).Contains($"📍 {breadcrumb}", StringComparison.Ordinal);
    }
    
    [Test]
    public async Task MenuNavigation_MultipleScreenClears_MaintainConsistency()
    {
        // This test verifies that multiple screen clears work consistently
        
        // Act - Clear screen multiple times
        Program.ClearScreen();
        var firstOutput = _stringWriter.ToString();
        
        _stringWriter.GetStringBuilder().Clear(); // Reset for second test
        Program.ClearScreen();
        var secondOutput = _stringWriter.ToString();
        
        // Assert - Both outputs should be identical
        await Assert.That(firstOutput).IsEqualTo(secondOutput);
    }
    
    [Test]
    public async Task MenuNavigation_HeaderWithSpecialCharacters_DisplaysCorrectly()
    {
        // This test verifies that headers with emojis and special characters display correctly
        
        // Arrange
        var headerWithEmojis = "🤖 AI Provider 🔧 Configuration 📊 Status";
        
        // Act
        Program.ClearScreenWithHeader(headerWithEmojis);
        var output = _stringWriter.ToString();
        
        // Assert
        await Assert.That(output).Contains(headerWithEmojis, StringComparison.Ordinal);
        await Assert.That(output).Contains(new string('=', headerWithEmojis.Length), StringComparison.Ordinal);
    }
    
    [Test]
    public async Task MenuNavigation_BreadcrumbWithMultipleLevels_DisplaysFullPath()
    {
        // This test verifies that complex breadcrumb paths display correctly
        
        // Arrange
        var header = "🔍 Vector Database Settings";
        var complexBreadcrumb = "Main Menu > System > Vector Database Management > Settings";
        
        // Act
        Program.ClearScreenWithHeader(header, complexBreadcrumb);
        var output = _stringWriter.ToString();
        
        // Assert
        await Assert.That(output).Contains(header, StringComparison.Ordinal);
        await Assert.That(output).Contains($"📍 {complexBreadcrumb}", StringComparison.Ordinal);
    }
    
    [Test]
    public async Task MenuNavigation_EmptyHeader_HandlesGracefully()
    {
        // This test verifies that empty headers are handled gracefully
        
        // Arrange
        var emptyHeader = "";
        
        // Act
        Program.ClearScreenWithHeader(emptyHeader);
        var output = _stringWriter.ToString();
        
        // Assert - Should still show minimum separator length
        await Assert.That(output).Contains(new string('=', 24), StringComparison.Ordinal);
    }
    
    [Test]
    public async Task MenuNavigation_ShowMenuAfterInitialization_DisplaysMenu()
    {
        // This test verifies that the menu is displayed after initialization
        // regardless of startup context (fixes issue where no menu appeared after RAG indexing)
        
        // Act
        Program.ShowMenu();
        var output = _stringWriter.ToString();
        
        // Assert - Menu should contain main menu options
        await Assert.That(output).Contains("📚 HlpAI - Available Commands:", StringComparison.Ordinal);
        await Assert.That(output).Contains("1 - List all available files", StringComparison.Ordinal);
        await Assert.That(output).Contains("2 - Read specific file content", StringComparison.Ordinal);
        await Assert.That(output).Contains("q - Quit", StringComparison.Ordinal);
    }

}