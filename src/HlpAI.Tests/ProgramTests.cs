using HlpAI.MCP;
using HlpAI.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace HlpAI.Tests;

public class ProgramTests
{
    private readonly Mock<ILogger<EnhancedMcpRagServer>> _mockLogger;
    private readonly string _testRootPath;
    
    public ProgramTests()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), "ProgramTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRootPath);
        _mockLogger = new Mock<ILogger<EnhancedMcpRagServer>>();
    }
    
    [Test]
    public async Task UpdateAiProvider_DirectTest_UpdatesProviderSuccessfully()
    {
        // Arrange
        var server = new EnhancedMcpRagServer(_mockLogger.Object, _testRootPath, "initial-model");
        var initialProviderType = server._aiProvider.ProviderType;
        
        // Create a mock for the new provider
        var mockProvider = new Mock<IAiProvider>();
        mockProvider.Setup(p => p.ProviderType).Returns(AiProviderType.LmStudio);
        mockProvider.Setup(p => p.CurrentModel).Returns("test-model");
        mockProvider.Setup(p => p.ProviderName).Returns("LM Studio");
        
        try
        {
            // Act
            server.UpdateAiProvider(mockProvider.Object);
            
            // Assert
            await Assert.That(server._aiProvider).IsEqualTo(mockProvider.Object);
            await Assert.That(server._aiProvider.ProviderType).IsEqualTo(AiProviderType.LmStudio);
            await Assert.That(server._aiProvider.CurrentModel).IsEqualTo("test-model");
        }
        finally
        {
            // Clean up
            server.Dispose();
        }
    }
}