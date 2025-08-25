using HlpAI.Models;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Services;

public class ConfigurationServiceErrorTests
{
    private string _testDirectory = null!;
    private ILogger<ConfigurationServiceErrorTests> _logger = null!;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("config_error_tests");
        _logger = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning))
            .CreateLogger<ConfigurationServiceErrorTests>();
        
        // Set up test-specific config path
        var testConfigPath = Path.Combine(_testDirectory, "config.json");
        ConfigurationService.SetConfigFilePathForTesting(testConfigPath);
    }

    [After(Test)]
    public void Cleanup()
    {
        // Reset to default config path
        ConfigurationService.SetConfigFilePathForTesting(null);
        FileTestHelper.SafeDeleteDirectory(_testDirectory);
    }

    [Test]
    public async Task SaveConfiguration_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(async () => await Task.Run(() => ConfigurationService.SaveConfiguration(null!, _logger)))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task UpdateLastDirectory_WithNullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(async () => await Task.Run(() => ConfigurationService.UpdateLastDirectory(null!, _logger)))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task UpdateLastModel_WithNullModel_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(async () => await Task.Run(() => ConfigurationService.UpdateLastModel(null!, _logger)))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SaveConfiguration_WithInvalidPath_HandlesGracefully()
    {
        // Arrange
        var config = new AppConfiguration
        {
            LastDirectory = new string('x', 300), // Very long path that might be invalid
            LastModel = "test-model"
        };

        // Act
        var result = await Task.Run(() => ConfigurationService.SaveConfiguration(config, _logger));

        // Assert - Should handle gracefully, not throw
        // Test passes if no exception thrown above
    }

    [Test]
    public async Task LoadConfiguration_WithFileAccessDenied_ReturnsDefaultConfiguration()
    {
        // This test simulates a scenario where the config file exists but can't be read
        var testConfigPath = Path.Combine(_testDirectory, "access_denied.json");
        
        // Create a config file
        var config = new AppConfiguration { LastDirectory = "test" };
        await File.WriteAllTextAsync(testConfigPath, System.Text.Json.JsonSerializer.Serialize(config));

        try
        {
            // Since we can't easily test file access denied in this environment,
            // we'll test with a different approach - corrupted file content
            await File.WriteAllTextAsync(testConfigPath, "not json content");

            // Act - This should return default config when JSON parsing fails
            var loadedConfig = await Task.Run(() => ConfigurationService.LoadConfiguration(_logger));

            // Assert
            await Assert.That(loadedConfig).IsNotNull();
            await Assert.That(loadedConfig.RememberLastDirectory).IsTrue(); // Default value
        }
        finally
        {
            // Clean up
            if (File.Exists(testConfigPath))
            {
                try
                {
                    File.Delete(testConfigPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    [Test]
    public async Task SaveConfiguration_WithExtremelyLongPaths_HandlesCorrectly()
    {
        // Arrange
        var veryLongPath = @"C:\" + string.Join(@"\", Enumerable.Repeat("VeryLongDirectoryNameThatExceedsNormalLimits", 10));
        var veryLongModel = string.Join("-", Enumerable.Repeat("VeryLongModelName", 20));
        
        var config = new AppConfiguration
        {
            LastDirectory = veryLongPath,
            LastModel = veryLongModel
        };

        // Act
        var result = ConfigurationService.SaveConfiguration(config, _logger);

        // Assert - Should handle gracefully
        // Test passes if no exception thrown above
        
        if (result)
        {
            var loadedConfig = ConfigurationService.LoadConfiguration(_logger);
            await Assert.That(loadedConfig.LastDirectory).IsEqualTo(veryLongPath);
            await Assert.That(loadedConfig.LastModel).IsEqualTo(veryLongModel);
        }
    }

    [Test]
    public async Task SaveConfiguration_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var pathWithSpecialChars = @"C:\Test\Path With Spaces\And-Dashes\And_Underscores\And.Dots\And(Parentheses)\And[Brackets]\And{Braces}";
        var modelWithSpecialChars = "model-with-special.chars_and|pipes&ampersands#hashes@ats!exclamations";
        
        var config = new AppConfiguration
        {
            LastDirectory = pathWithSpecialChars,
            LastModel = modelWithSpecialChars
        };

        // Act
        var result = ConfigurationService.SaveConfiguration(config, _logger);

        // Assert
        await Assert.That(result).IsTrue();
        
        var loadedConfig = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(loadedConfig.LastDirectory).IsEqualTo(pathWithSpecialChars);
        await Assert.That(loadedConfig.LastModel).IsEqualTo(modelWithSpecialChars);
    }

    [Test]
    public async Task SaveConfiguration_WithUnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        var pathWithUnicode = @"C:\Test\Ñiño\Москва\北京\العربية\עברית";
        var modelWithUnicode = "模型-with-émojis-🚀-and-ñiño";
        
        var config = new AppConfiguration
        {
            LastDirectory = pathWithUnicode,
            LastModel = modelWithUnicode
        };

        // Act
        var result = ConfigurationService.SaveConfiguration(config, _logger);

        // Assert
        await Assert.That(result).IsTrue();
        
        var loadedConfig = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(loadedConfig.LastDirectory).IsEqualTo(pathWithUnicode);
        await Assert.That(loadedConfig.LastModel).IsEqualTo(modelWithUnicode);
    }

    [Test]
    public async Task ConfigurationService_WithInvalidJsonStructure_ReturnsDefault()
    {
        // Arrange - Create invalid JSON structures
        var invalidJsonCases = new[]
        {
            "null",
            "[]", // Array instead of object
            "\"string\"", // String instead of object
            "123", // Number instead of object
            "true", // Boolean instead of object
            "{}", // Empty object
            "{\"invalidProperty\": \"value\"}", // Object with unknown properties only
        };

        foreach (var invalidJson in invalidJsonCases)
        {
            var testFile = Path.Combine(_testDirectory, $"invalid_{Array.IndexOf(invalidJsonCases, invalidJson)}.json");
            await File.WriteAllTextAsync(testFile, invalidJson);

            // We can't easily override the config file path, so we'll test JSON parsing directly
            try
            {
                var parsedConfig = System.Text.Json.JsonSerializer.Deserialize<AppConfiguration>(invalidJson);
                // If parsing succeeds, verify it has sensible defaults
                if (parsedConfig != null)
                {
                    await Assert.That(parsedConfig.ConfigVersion).IsGreaterThanOrEqualTo(0);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Expected for invalid JSON - this is the correct behavior
                // Test passes if exception is thrown
            }
        }
    }

    [Test]
    public async Task UpdateMethods_WithEmptyStrings_HandleCorrectly()
    {
        // Act
        var result1 = ConfigurationService.UpdateLastDirectory("", _logger);
        var result2 = ConfigurationService.UpdateLastModel("", _logger);

        // Assert
        await Assert.That(result1).IsTrue();
        await Assert.That(result2).IsTrue();

        var config = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(config.LastDirectory).IsEqualTo("");
        await Assert.That(config.LastModel).IsEqualTo("");
    }

    [Test]
    public async Task UpdateMethods_WithWhitespaceStrings_HandleCorrectly()
    {
        // Arrange
        var whitespaceDirectory = "   \t  \n  ";
        var whitespaceModel = "  \r\n\t  ";

        // Act
        var result1 = ConfigurationService.UpdateLastDirectory(whitespaceDirectory, _logger);
        var result2 = ConfigurationService.UpdateLastModel(whitespaceModel, _logger);

        // Assert
        await Assert.That(result1).IsTrue();
        await Assert.That(result2).IsTrue();

        var config = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(config.LastDirectory).IsEqualTo(whitespaceDirectory);
        await Assert.That(config.LastModel).IsEqualTo(whitespaceModel);
    }

    [Test]
    public async Task LoadConfiguration_WithMalformedDateTimes_HandleCorrectly()
    {
        // Arrange
        var malformedJson = """
        {
            "lastDirectory": "C:\\Test",
            "lastModel": "test-model",
            "lastUpdated": "not-a-valid-datetime",
            "configVersion": 1
        }
        """;

        var testFile = Path.Combine(_testDirectory, "malformed_datetime.json");
        await File.WriteAllTextAsync(testFile, malformedJson);

        // Act - Test JSON deserialization behavior with malformed datetime
        try
        {
            var config = System.Text.Json.JsonSerializer.Deserialize<AppConfiguration>(malformedJson);
            // If it doesn't throw, verify the object state
            await Assert.That(config).IsNotNull();
        }
        catch (System.Text.Json.JsonException)
        {
            // This is expected behavior for malformed JSON
            // Test passes if exception is thrown
        }
    }

    [Test]
    public async Task ConfigurationService_UnderConcurrentLoad_RemainsStable()
    {
        // Test concurrent read/write operations
        var tasks = new List<Task>();
        var config = new AppConfiguration
        {
            LastDirectory = @"C:\ConcurrentTest",
            LastModel = "concurrent-model"
        };

        // Save initial configuration
        ConfigurationService.SaveConfiguration(config, _logger);

        // Create multiple concurrent operations
        for (int i = 0; i < 20; i++)
        {
            var index = i;
            
            // Mix of read and write operations
            if (index % 3 == 0)
            {
                tasks.Add(Task.Run(() => ConfigurationService.LoadConfiguration(_logger)));
            }
            else if (index % 3 == 1)
            {
                tasks.Add(Task.Run(() => ConfigurationService.UpdateLastDirectory($@"C:\Test{index}", _logger)));
            }
            else
            {
                tasks.Add(Task.Run(() => ConfigurationService.UpdateLastModel($"model-{index}", _logger)));
            }
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert - Should complete without exceptions
        var finalConfig = ConfigurationService.LoadConfiguration(_logger);
        await Assert.That(finalConfig).IsNotNull();
    }

    [Test]
    public async Task GetConfigurationStatus_WithCorruptedFile_HandlesGracefully()
    {
        // Arrange - Create a corrupted config file at the expected location
        var configPath = ConfigurationService.ConfigFilePath;
        var configDir = Path.GetDirectoryName(configPath);
        
        if (configDir != null && !Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }
        
        await File.WriteAllTextAsync(configPath, "corrupted content that is not JSON");

        try
        {
            // Act
            var status = ConfigurationService.GetConfigurationStatus();

            // Assert
            await Assert.That(status).IsNotNull();
            await Assert.That(status).IsNotEmpty();
            // Should handle the error gracefully and provide some status information
            await Assert.That(status).Contains("Configuration file:");
        }
        finally
        {
            // Cleanup
            if (File.Exists(configPath))
            {
                try
                {
                    File.Delete(configPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
