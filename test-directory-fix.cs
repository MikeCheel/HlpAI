using HlpAI.Services;
using Microsoft.Extensions.Logging;

// Test the directory remembering functionality
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Test");

Console.WriteLine("=== Testing Directory Remembering Functionality ===");

// Test setting a directory
var testDirectory = @"C:\Users\mikec\Desktop\ChmData";
Console.WriteLine($"Setting test directory: {testDirectory}");

var result = ConfigurationService.UpdateLastDirectory(testDirectory, logger);
Console.WriteLine($"UpdateLastDirectory result: {result}");

// Clear cache and reload
ConfigurationService.ClearCache();
var config = ConfigurationService.LoadConfiguration(logger);

Console.WriteLine($"RememberLastDirectory: {config.RememberLastDirectory}");
Console.WriteLine($"LastDirectory: {config.LastDirectory ?? "Not set"}");

Console.WriteLine("\nShould the directory be available for startup? " + 
    (config.RememberLastDirectory && !string.IsNullOrEmpty(config.LastDirectory) && Directory.Exists(config.LastDirectory)));