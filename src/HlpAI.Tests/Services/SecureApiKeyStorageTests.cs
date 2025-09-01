using Microsoft.Extensions.Logging;
using HlpAI.Services;
using System.Runtime.Versioning;

namespace HlpAI.Tests.Services;

public class SecureApiKeyStorageTests : IDisposable
{
    private readonly SecureApiKeyStorage _storage;
    private readonly ILogger<SecureApiKeyStorage> _logger;
    private readonly LoggerFactory _loggerFactory;
    private readonly string _testStorageDirectory;
    private readonly List<string> _testProviders;

    public SecureApiKeyStorageTests()
    {
        _loggerFactory = new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<SecureApiKeyStorage>();
        
        // Create a unique test directory for this test instance to avoid interference
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        _testStorageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HlpAI",
            "SecureKeys_Test_" + uniqueId
        );
        
        // Create storage with the unique test directory
        _storage = new SecureApiKeyStorage(_logger, _testStorageDirectory);
        _testProviders = new List<string>();
    }

    [After(Test)]
    public void Dispose()
    {
        // Clean up test keys
        foreach (var provider in _testProviders)
        {
            try
            {
                _storage.DeleteApiKey(provider);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        
        // Clean up the unique test directory
        try
        {
            if (Directory.Exists(_testStorageDirectory))
            {
                Directory.Delete(_testStorageDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
        
        _loggerFactory?.Dispose();
    }

    // Removed Setup and TearDown methods that were interfering with test execution
    // Individual tests handle their own cleanup as needed

    private void AddTestProvider(string providerName)
    {
        _testProviders.Add(providerName);
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task StoreApiKey_WithValidInputs_ShouldReturnTrue()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange
        var providerName = "TestProvider_Valid";
        var apiKey = "sk-1234567890abcdef1234567890abcdef";
        AddTestProvider(providerName);

        // Act
        var result = _storage.StoreApiKey(providerName, apiKey);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(_storage.HasApiKey(providerName)).IsTrue();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task StoreApiKey_WithNullProviderName_ShouldReturnFalse()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange
        var apiKey = "sk-1234567890abcdef1234567890abcdef";
        
        // Act
        var result = _storage.StoreApiKey(null!, apiKey);
        
        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task StoreApiKey_WithEmptyProviderName_ShouldReturnFalse()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange
        var apiKey = "sk-1234567890abcdef1234567890abcdef";
        
        // Act
        var result = _storage.StoreApiKey("", apiKey);
        
        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task StoreApiKey_WithNullApiKey_ShouldReturnFalse()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange
        var providerName = "TestProvider_NullKey";
        
        // Act
        var result = _storage.StoreApiKey(providerName, null!);
        
        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task StoreApiKey_WithEmptyApiKey_ShouldReturnFalse()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange
        var providerName = "TestProvider_EmptyKey";
        
        // Act
        var result = _storage.StoreApiKey(providerName, "");
        
        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task RetrieveApiKey_WithValidProvider_ShouldReturnStoredKey()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange
        var providerName = "TestProvider_Retrieve";
        var apiKey = "sk-ant-1234567890abcdef1234567890abcdef";
        AddTestProvider(providerName);
        _storage.StoreApiKey(providerName, apiKey);

        // Act
        var retrievedKey = _storage.RetrieveApiKey(providerName);

        // Assert
        await Assert.That(retrievedKey).IsEqualTo(apiKey);
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task RetrieveApiKey_WithNonExistentProvider_ShouldReturnNull()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange
        var providerName = "NonExistentProvider";

        // Act
        var retrievedKey = _storage.RetrieveApiKey(providerName);

        // Assert
        await Assert.That(retrievedKey).IsNull();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task RetrieveApiKey_WithNullProviderName_ShouldReturnNull()
    {
        // Act
        var retrievedKey = _storage.RetrieveApiKey(null!);

        // Assert
        await Assert.That(retrievedKey).IsNull();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task RetrieveApiKey_WithEmptyProviderName_ShouldReturnNull()
    {
        // Act
        var retrievedKey = _storage.RetrieveApiKey("");

        // Assert
        await Assert.That(retrievedKey).IsNull();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task HasApiKey_WithExistingKey_ShouldReturnTrue()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange
        var providerName = "TestProvider_HasKey";
        var apiKey = "sk-1234567890abcdef1234567890abcdef";
        _storage.StoreApiKey(providerName, apiKey);

        // Act
        var hasKey = _storage.HasApiKey(providerName);

        // Assert
        await Assert.That(hasKey).IsTrue();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task HasApiKey_WithNonExistentKey_ShouldReturnFalse()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange
        var providerName = "NonExistentProvider_HasKey";

        // Act
        var hasKey = _storage.HasApiKey(providerName);

        // Assert
        await Assert.That(hasKey).IsFalse();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task HasApiKey_WithNullProviderName_ShouldReturnFalse()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Act
        var hasKey = _storage.HasApiKey(null!);

        // Assert
        await Assert.That(hasKey).IsFalse();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task HasApiKey_WithEmptyProviderName_ShouldReturnFalse()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Act
        var hasKey = _storage.HasApiKey("");

        // Assert
        await Assert.That(hasKey).IsFalse();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task DeleteApiKey_WithExistingKey_ShouldReturnTrue()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange
        var providerName = "TestProvider_Delete";
        var apiKey = "sk-1234567890abcdef1234567890abcdef";
        _storage.StoreApiKey(providerName, apiKey);

        // Act
        var result = _storage.DeleteApiKey(providerName);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(_storage.HasApiKey(providerName)).IsFalse();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task DeleteApiKey_WithNonExistentKey_ShouldReturnTrue()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange
        var providerName = "NonExistentProvider_Delete";

        // Act
        var result = _storage.DeleteApiKey(providerName);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task DeleteApiKey_WithNullProviderName_ShouldReturnFalse()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Act
        var result = _storage.DeleteApiKey(null!);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task DeleteApiKey_WithEmptyProviderName_ShouldReturnFalse()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Act
        var result = _storage.DeleteApiKey("");

        // Assert
        await Assert.That(result).IsFalse();
    }
[Test]
    [SupportedOSPlatform("windows")]
    public async Task GetProvidersWithKeys_WithMultipleKeys_ShouldReturnAllProviders()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange - Clear all existing keys first and ensure clean state
        _storage.ClearAllKeys();
        
        // Force cleanup any leftover files manually, including problematic ones from other tests
        if (Directory.Exists(_testStorageDirectory))
        {
            var leftoverFiles = Directory.GetFiles(_testStorageDirectory, "*.key");
            foreach (var file in leftoverFiles)
            {
                try
                {
                    File.Delete(file);
                    Console.WriteLine($"Manually deleted leftover file: {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }
        

        
        var providers = new[] { "TestProvider1", "TestProvider2", "TestProvider3" };
        var apiKey = "sk-1234567890abcdef1234567890abcdef";
        
        foreach (var provider in providers)
        {
            AddTestProvider(provider);
            
            var storeResult = _storage.StoreApiKey(provider, apiKey);
            await Assert.That(storeResult).IsTrue();
            
            // Verify the key was stored immediately
            var hasKey = _storage.HasApiKey(provider);
            await Assert.That(hasKey).IsTrue();
        }
        
        // Act
        var result = _storage.GetProvidersWithKeys();
        
        // Assert
        await Assert.That(result.Count).IsEqualTo(providers.Length);
        foreach (var provider in providers)
        {
            await Assert.That(result.Contains(provider)).IsTrue();
        }
        

    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task GetProvidersWithKeys_WithNoKeys_ShouldReturnEmptyList()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange - ensure directory is empty
        _storage.ClearAllKeys();
        
        // Act
        var result = _storage.GetProvidersWithKeys();

        // Assert
        await Assert.That(result).IsEmpty();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task ClearAllKeys_WithExistingKeys_ShouldReturnTrue()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange
        var providers = new[] { "TestProvider_Clear1", "TestProvider_Clear2" };
        var apiKey = "sk-1234567890abcdef1234567890abcdef";
        
        foreach (var provider in providers)
        {
            AddTestProvider(provider); // Add to cleanup list
            _storage.StoreApiKey(provider, apiKey);
        }

        // Act
        var result = _storage.ClearAllKeys();

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify all keys are cleared
        foreach (var provider in providers)
        {
            await Assert.That(_storage.HasApiKey(provider)).IsFalse();
        }
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task ClearAllKeys_WithNoKeys_ShouldReturnTrue()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Act
        var result = _storage.ClearAllKeys();

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task StoreApiKey_WithSpecialCharactersInProviderName_ShouldSanitizeAndStore()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange
        var providerName = "Test@Provider#With$Special%Characters";
        var sanitizedName = "TestProviderWithSpecialCharacters";
        var apiKey = "sk-1234567890abcdef1234567890abcdef";
        AddTestProvider(sanitizedName); // Use sanitized name for cleanup

        // Act
        var result = _storage.StoreApiKey(providerName, apiKey);

        // Assert
        await Assert.That(result).IsTrue();
        // The key should be stored under the sanitized name
        await Assert.That(_storage.HasApiKey(sanitizedName)).IsTrue();
        

    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task StoreAndRetrieve_WithLongApiKey_ShouldWorkCorrectly()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Arrange
        var providerName = "TestProvider_LongKey";
        var longApiKey = "sk-" + new string('a', 100) + new string('1', 50) + new string('b', 50);
        AddTestProvider(providerName);

        // Act
        var storeResult = _storage.StoreApiKey(providerName, longApiKey);
        var retrievedKey = _storage.RetrieveApiKey(providerName);

        // Assert
        await Assert.That(storeResult).IsTrue();
        await Assert.That(retrievedKey).IsEqualTo(longApiKey);
        

    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task Constructor_ShouldCreateStorageDirectory()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Act - constructor already called in setup
        
        // Assert
        await Assert.That(Directory.Exists(_testStorageDirectory)).IsTrue();
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task Constructor_WithNullLogger_ShouldNotThrow()
    {
        // Skip test if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("This test requires Windows platform");
            return;
        }
        
        // Act
        SecureApiKeyStorage? storage = null;
        Exception? exception = null;
        try
        {
            storage = new SecureApiKeyStorage(null);
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        
        // Assert
        await Assert.That(exception).IsNull();
        await Assert.That(storage).IsNotNull();
    }
}