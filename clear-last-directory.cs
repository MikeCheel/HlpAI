using System;
using System.Threading.Tasks;
using HlpAI.Services;
using Microsoft.Extensions.Logging;

class Program
{
    static async Task Main(string[] args)
    {
        // Create a simple program to clear the LastDirectory
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Program>();
        var configService = new SqliteConfigurationService(logger);

        // Set LastDirectory to null to clear it
        var result = await configService.SetConfigurationAsync("LastDirectory", null, "general");

        if (result)
        {
            Console.WriteLine("LastDirectory has been cleared (set to null).");
        }
        else
        {
            Console.WriteLine("Failed to clear LastDirectory.");
        }

        // Show current configuration
        var currentConfig = await configService.LoadAppConfigurationAsync();
        Console.WriteLine($"Current LastDirectory: {currentConfig.LastDirectory ?? "(null)"}");
        Console.WriteLine($"RememberLastDirectory: {currentConfig.RememberLastDirectory}");
    }
}