using System;
using System.Threading.Tasks;
using HlpAI.Services;
using Microsoft.Extensions.Logging;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Setting LastDirectory to ChmData for testing...");
        
        // Create a simple program to clear the LastDirectory
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Program>();
        var configService = new SqliteConfigurationService(logger);

        // Set LastDirectory to the ChmData directory for testing
        var testDirectory = @"C:\Users\mikec\Desktop\ChmData";
        var result = await configService.SetConfigurationAsync("LastDirectory", testDirectory, "general");

        if (result)
        {
            Console.WriteLine($"✓ LastDirectory has been set to: {testDirectory}");
        }
        else
        {
            Console.WriteLine("✗ Failed to set LastDirectory.");
        }

        // Show current configuration
        var currentConfig = await configService.LoadAppConfigurationAsync();
        Console.WriteLine($"Current LastDirectory: {currentConfig.LastDirectory ?? "(null)"}");
        Console.WriteLine($"RememberLastDirectory: {currentConfig.RememberLastDirectory}");
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
