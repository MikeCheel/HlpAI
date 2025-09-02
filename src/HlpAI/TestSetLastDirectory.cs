using HlpAI.Services;
using Microsoft.Extensions.Logging;

namespace HlpAI;

public static class TestSetLastDirectory
{
    public static async Task SetTestDirectoryAsync()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("TestSetLastDirectory");
        
        try
        {
            var configService = SqliteConfigurationService.GetInstance(logger);
            var testDirectory = @"C:\Users\mikec\Documents";
            
            Console.WriteLine($"Setting LastDirectory to: {testDirectory}");
            var result = await configService.UpdateLastDirectoryAsync(testDirectory);
            
            if (result)
            {
                Console.WriteLine("✅ LastDirectory updated successfully!");
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