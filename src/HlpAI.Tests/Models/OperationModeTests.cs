using HlpAI.Models;
using TUnit.Assertions;

namespace HlpAI.Tests.Models;

public class OperationModeTests
{
    [Test]
    public async Task OperationMode_HasExpectedValues()
    {
        // Assert
        await Assert.That(Enum.IsDefined(typeof(OperationMode), OperationMode.MCP)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(OperationMode), OperationMode.RAG)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(OperationMode), OperationMode.Hybrid)).IsTrue();
    }

    [Test]
    public async Task OperationMode_ToString_ReturnsCorrectNames()
    {
        // Act & Assert
        await Assert.That(OperationMode.MCP.ToString()).IsEqualTo("MCP");
        await Assert.That(OperationMode.RAG.ToString()).IsEqualTo("RAG");
        await Assert.That(OperationMode.Hybrid.ToString()).IsEqualTo("Hybrid");
    }

    [Test]
    public async Task OperationMode_Values_HaveCorrectIntegerValues()
    {
        // Act
        var mcpValue = (int)OperationMode.MCP;
        var ragValue = (int)OperationMode.RAG;
        var hybridValue = (int)OperationMode.Hybrid;
        
        // Assert
        await Assert.That(mcpValue).IsEqualTo(0);
        await Assert.That(ragValue).IsEqualTo(1);
        await Assert.That(hybridValue).IsEqualTo(2);
    }

    [Test]
    public async Task OperationMode_GetValues_ReturnsAllModes()
    {
        // Act
        var values = Enum.GetValues<OperationMode>();

        // Assert
        await Assert.That(values.Length).IsEqualTo(3);
        await Assert.That(values).Contains(OperationMode.MCP);
        await Assert.That(values).Contains(OperationMode.RAG);
        await Assert.That(values).Contains(OperationMode.Hybrid);
    }

    [Test]
    public async Task OperationMode_Parse_WorksCorrectly()
    {
        // Act & Assert
        await Assert.That(Enum.Parse<OperationMode>("MCP")).IsEqualTo(OperationMode.MCP);
        await Assert.That(Enum.Parse<OperationMode>("RAG")).IsEqualTo(OperationMode.RAG);
        await Assert.That(Enum.Parse<OperationMode>("Hybrid")).IsEqualTo(OperationMode.Hybrid);
    }

    [Test]
    public async Task OperationMode_TryParse_HandlesInvalidValues()
    {
        // Act
        var success = Enum.TryParse<OperationMode>("InvalidMode", out var result);

        // Assert
        await Assert.That(success).IsFalse();
        await Assert.That(result).IsEqualTo((OperationMode)0); // Default value
    }

    [Test]
    public async Task OperationMode_TryParse_CaseInsensitive_WorksCorrectly()
    {
        // Act & Assert
        await Assert.That(Enum.TryParse<OperationMode>("mcp", true, out var mcp)).IsTrue();
        await Assert.That(mcp).IsEqualTo(OperationMode.MCP);
        
        await Assert.That(Enum.TryParse<OperationMode>("rag", true, out var rag)).IsTrue();
        await Assert.That(rag).IsEqualTo(OperationMode.RAG);
        
        await Assert.That(Enum.TryParse<OperationMode>("hybrid", true, out var hybrid)).IsTrue();
        await Assert.That(hybrid).IsEqualTo(OperationMode.Hybrid);
    }
}