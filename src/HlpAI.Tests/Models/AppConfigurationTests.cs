using System.Text.Json;
using HlpAI.Models;
using TUnit.Assertions;
using TUnit.Core;

namespace HlpAI.Tests.Models;

public class AppConfigurationTests
{
    [Test]
    public async Task AppConfiguration_DefaultConstructor_SetsCorrectDefaults()
    {
        // Act
        var config = new AppConfiguration();

        // Assert
        await Assert.That(config.LastDirectory).IsNull();
        await Assert.That(config.RememberLastDirectory).IsTrue();
        await Assert.That(config.LastModel).IsNull();
        await Assert.That(config.RememberLastModel).IsTrue();
        await Assert.That(config.LastOperationMode).IsEqualTo(OperationMode.Hybrid);
        await Assert.That(config.RememberLastOperationMode).IsTrue();
        await Assert.That(config.ConfigVersion).IsEqualTo(1);
        var timeDiff = Math.Abs((config.LastUpdated - DateTime.UtcNow).TotalSeconds);
        await Assert.That(timeDiff).IsLessThan(5.0);
    }

    [Test]
    public async Task AppConfiguration_PropertyAssignment_WorksCorrectly()
    {
        // Arrange
        var config = new AppConfiguration();
        var testDirectory = @"C:\TestDirectory";
        var testModel = "llama3.2";
        var testMode = OperationMode.RAG;
        var testTime = DateTime.UtcNow.AddHours(-1);

        // Act
        config.LastDirectory = testDirectory;
        config.RememberLastDirectory = false;
        config.LastModel = testModel;
        config.RememberLastModel = false;
        config.LastOperationMode = testMode;
        config.RememberLastOperationMode = false;
        config.LastUpdated = testTime;
        config.ConfigVersion = 2;

        // Assert
        await Assert.That(config.LastDirectory).IsEqualTo(testDirectory);
        await Assert.That(config.RememberLastDirectory).IsFalse();
        await Assert.That(config.LastModel).IsEqualTo(testModel);
        await Assert.That(config.RememberLastModel).IsFalse();
        await Assert.That(config.LastOperationMode).IsEqualTo(testMode);
        await Assert.That(config.RememberLastOperationMode).IsFalse();
        await Assert.That(config.LastUpdated).IsEqualTo(testTime);
        await Assert.That(config.ConfigVersion).IsEqualTo(2);
    }

    [Test]
    public async Task AppConfiguration_JsonSerialization_WorksCorrectly()
    {
        // Arrange
        var originalConfig = new AppConfiguration
        {
            LastDirectory = @"C:\TestDirectory",
            RememberLastDirectory = false,
            LastModel = "llama3.2",
            RememberLastModel = false,
            LastOperationMode = OperationMode.MCP,
            RememberLastOperationMode = false,
            LastUpdated = DateTime.UtcNow.AddHours(-1),
            ConfigVersion = 2
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(originalConfig, jsonOptions);
        var deserializedConfig = JsonSerializer.Deserialize<AppConfiguration>(json, jsonOptions);

        // Assert
        await Assert.That(deserializedConfig).IsNotNull();
        await Assert.That(deserializedConfig!.LastDirectory).IsEqualTo(originalConfig.LastDirectory);
        await Assert.That(deserializedConfig.RememberLastDirectory).IsEqualTo(originalConfig.RememberLastDirectory);
        await Assert.That(deserializedConfig.LastModel).IsEqualTo(originalConfig.LastModel);
        await Assert.That(deserializedConfig.RememberLastModel).IsEqualTo(originalConfig.RememberLastModel);
        await Assert.That(deserializedConfig.LastOperationMode).IsEqualTo(originalConfig.LastOperationMode);
        await Assert.That(deserializedConfig.RememberLastOperationMode).IsEqualTo(originalConfig.RememberLastOperationMode);
        var timeDiff = Math.Abs((deserializedConfig.LastUpdated - originalConfig.LastUpdated).TotalMilliseconds);
        await Assert.That(timeDiff).IsLessThan(100.0);
        await Assert.That(deserializedConfig.ConfigVersion).IsEqualTo(originalConfig.ConfigVersion);
    }

    [Test]
    public async Task AppConfiguration_JsonSerialization_HandlesNullValues()
    {
        // Arrange
        var config = new AppConfiguration
        {
            LastDirectory = null,
            LastModel = null
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(config, jsonOptions);
        var deserializedConfig = JsonSerializer.Deserialize<AppConfiguration>(json, jsonOptions);

        // Assert
        await Assert.That(deserializedConfig).IsNotNull();
        await Assert.That(deserializedConfig!.LastDirectory).IsNull();
        await Assert.That(deserializedConfig.LastModel).IsNull();
    }

    [Test]
    public async Task AppConfiguration_OperationModeEnum_SerializesAsString()
    {
        // Arrange
        var config = new AppConfiguration
        {
            LastOperationMode = OperationMode.RAG
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(config, jsonOptions);

        // Assert
        await Assert.That(json).Contains("\"lastOperationMode\": \"RAG\"");
    }

    [Test]
    public async Task AppConfiguration_AllOperationModes_SerializeCorrectly()
    {
        // Arrange & Act & Assert
        var modes = new[] { OperationMode.MCP, OperationMode.RAG, OperationMode.Hybrid };
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        foreach (var mode in modes)
        {
            var config = new AppConfiguration { LastOperationMode = mode };
            var json = JsonSerializer.Serialize(config, jsonOptions);
            var deserializedConfig = JsonSerializer.Deserialize<AppConfiguration>(json, jsonOptions);

            await Assert.That(deserializedConfig).IsNotNull();
            await Assert.That(deserializedConfig!.LastOperationMode).IsEqualTo(mode);
        }
    }

    [Test]
    public async Task AppConfiguration_EmptyStringValues_HandledCorrectly()
    {
        // Arrange
        var config = new AppConfiguration
        {
            LastDirectory = "",
            LastModel = ""
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(config, jsonOptions);
        var deserializedConfig = JsonSerializer.Deserialize<AppConfiguration>(json, jsonOptions);

        // Assert
        await Assert.That(deserializedConfig).IsNotNull();
        await Assert.That(deserializedConfig!.LastDirectory).IsEqualTo("");
        await Assert.That(deserializedConfig.LastModel).IsEqualTo("");
    }

    [Test]
    public async Task AppConfiguration_LongPaths_HandledCorrectly()
    {
        // Arrange
        var longPath = @"C:\This\Is\A\Very\Long\Path\With\Many\Subdirectories\That\Could\Be\Used\For\Testing\Very\Long\Directory\Names\And\Paths\That\Might\Cause\Issues";
        var config = new AppConfiguration
        {
            LastDirectory = longPath
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(config, jsonOptions);
        var deserializedConfig = JsonSerializer.Deserialize<AppConfiguration>(json, jsonOptions);

        // Assert
        await Assert.That(deserializedConfig).IsNotNull();
        await Assert.That(deserializedConfig!.LastDirectory).IsEqualTo(longPath);
    }

    [Test]
    public async Task AppConfiguration_SpecialCharactersInPaths_HandledCorrectly()
    {
        // Arrange
        var specialPath = @"C:\Test Directory\With Spaces\And-Dashes\And_Underscores\And.Dots\And(Parentheses)\And[Brackets]";
        var specialModel = "model-with-dashes_and_underscores.and.dots";
        
        var config = new AppConfiguration
        {
            LastDirectory = specialPath,
            LastModel = specialModel
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(config, jsonOptions);
        var deserializedConfig = JsonSerializer.Deserialize<AppConfiguration>(json, jsonOptions);

        // Assert
        await Assert.That(deserializedConfig).IsNotNull();
        await Assert.That(deserializedConfig!.LastDirectory).IsEqualTo(specialPath);
        await Assert.That(deserializedConfig.LastModel).IsEqualTo(specialModel);
    }

    [Test]
    public async Task AppConfiguration_BoundaryDateValues_HandledCorrectly()
    {
        // Arrange
        var minDate = DateTime.MinValue;
        var maxDate = DateTime.MaxValue;
        
        var configMin = new AppConfiguration { LastUpdated = minDate };
        var configMax = new AppConfiguration { LastUpdated = maxDate };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act & Assert
        var jsonMin = JsonSerializer.Serialize(configMin, jsonOptions);
        var deserializedMin = JsonSerializer.Deserialize<AppConfiguration>(jsonMin, jsonOptions);
        await Assert.That(deserializedMin).IsNotNull();
        await Assert.That(deserializedMin!.LastUpdated).IsEqualTo(minDate);

        var jsonMax = JsonSerializer.Serialize(configMax, jsonOptions);
        var deserializedMax = JsonSerializer.Deserialize<AppConfiguration>(jsonMax, jsonOptions);
        await Assert.That(deserializedMax).IsNotNull();
        await Assert.That(deserializedMax!.LastUpdated).IsEqualTo(maxDate);
    }
}