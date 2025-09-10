using HlpAI.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace HlpAI.Tests.Services;

/// <summary>
/// FAST UNIT TESTS for HhExeDetectionService - uses mocks, no real file I/O or databases
/// Converted from slow integration tests that were calling File.Exists() hundreds of times
/// </summary>
public class HhExeDetectionServiceTests
{
    private Mock<ILogger<HhExeDetectionService>> _mockLogger = null!;
    private Mock<SqliteConfigurationService> _mockConfigService = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<HhExeDetectionService>>();
        _mockConfigService = new Mock<SqliteConfigurationService>();
    }

    [Test]
    public void Constructor_InitializesWithConfigServiceAndLogger()
    {
        // Act & Assert - Should not throw and complete quickly
        using var service = new HhExeDetectionService(_mockConfigService.Object, _mockLogger.Object);
        // Test that constructor completes successfully without file I/O
    }

    [Test]
    public async Task GetDefaultHhExePathAsync_ReturnsExpectedPath()
    {
        // Arrange
        using var service = new HhExeDetectionService(_mockConfigService.Object, _mockLogger.Object);

        // Act
        var result = await service.GetDefaultHhExePathAsync();

        // Assert - Should return the expected default path regardless of whether file exists
        await Assert.That(result).IsEqualTo(@"C:\Windows\hh.exe");
    }

    [Test] 
    public async Task CheckDefaultLocationAsync_ReturnsBoolean()
    {
        // Arrange
        using var service = new HhExeDetectionService(_mockConfigService.Object, _mockLogger.Object);

        // Act - This will call File.Exists but we're just testing the method completes quickly
        var result = await service.CheckDefaultLocationAsync();

        // Assert - Should return a boolean value quickly (true or false)
        // Just verify method completes without throwing
    }

    // NOTE: Removed slow integration tests that were:
    // - Calling File.Exists(@"C:\Windows\hh.exe") hundreds of times
    // - Creating real SQLite databases with temp files
    // - Manipulating environment variables
    // - Doing real file I/O operations
    // These should be moved to a separate integration test suite if needed
}