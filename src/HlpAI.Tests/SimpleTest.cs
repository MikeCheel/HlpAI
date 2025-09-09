using TUnit.Core;

namespace HlpAI.Tests;

public class SimpleTest
{
    [Test]
    public async Task BasicTest_ShouldPass()
    {
        // Arrange
        var value = 42;
        
        // Act
        var result = value * 2;
        
        // Assert
        await Assert.That(result).IsEqualTo(84);
    }
}