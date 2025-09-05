using System;
using System.IO;
using System.Threading.Tasks;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Services;

public class TestPromptDefaults
{
    private string _testDirectory = null!;
    private ILogger<PromptService> _logger = null!;
    private string _originalUserProfile = null!;

    [Before(Test)]
    public void Setup()
    {
        _testDirectory = FileTestHelper.CreateTempDirectory("prompt_defaults_tests");
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PromptService>();
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
    }

    [After(Test)]
    public void Cleanup()
    {
        // Restore original user profile
        Environment.SetEnvironmentVariable("USERPROFILE", _originalUserProfile);
        
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Test]
    public async Task TestPromptDefaultsWithoutInput()
    {
        // Create a StringReader with empty input to simulate no user input
        var input = new StringReader("\n\n\n\n\n"); // Just pressing Enter multiple times
        
        var originalIn = Console.In;
        
        Console.SetIn(input);

        var promptService = new PromptService(_logger);

        try
        {
            // Test PromptYesNoDefaultYesAsync - should return true when just pressing Enter
            var result1 = await promptService.PromptYesNoDefaultYesAsync("Test prompt defaulting to yes?");
            await Assert.That(result1).IsTrue();

            // Test PromptYesNoDefaultNoAsync - should return false when just pressing Enter
            var result2 = await promptService.PromptYesNoDefaultNoAsync("Test prompt defaulting to no?");
            await Assert.That(result2).IsFalse();
        }
        finally
        {
            // Restore original console streams
            Console.SetIn(originalIn);
        }
    }
}