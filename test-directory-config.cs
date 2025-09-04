using HlpAI.Services;
using Microsoft.Extensions.Logging;

class Program
{
    static async Task Main(string[] args)
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("TestDirectoryConfig");
        
        try
        {
            var configService = SqliteConfigurationService.GetInstance(logger);
            var testDirectory = @"C:\Users\mikec\Documents";
            
            Console.WriteLine($"Setting LastDirectory to: {testDirectory}");
            var result = await configService.UpdateLastDirectoryAsync(testDirectory);
            
            if (result)
            {
                Console.WriteLine("✅ LastDirectory updated successfully!");
                
                // Verify it was saved
                var config = await configService.LoadAppConfigurationAsync();
                Console.WriteLine($"Verified LastDirectory: {config.LastDirectory}");
                Console.WriteLine($"RememberLastDirectory: {config.RememberLastDirectory}");
            }
            else
            {
                Console.WriteLine("❌ Failed to update LastDirectory");
            }
            
            SqliteConfigurationService.ReleaseInstance();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}