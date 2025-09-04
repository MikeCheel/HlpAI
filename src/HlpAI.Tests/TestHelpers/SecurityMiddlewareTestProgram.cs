using HlpAI.Services;
using HlpAI.Models;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.TestHelpers;

/// <summary>
/// Test utility for SecurityMiddleware functionality
/// </summary>
public static class SecurityMiddlewareTestProgram
{
    /// <summary>
    /// Runs a comprehensive test of SecurityMiddleware functionality
    /// </summary>
    public static void RunSecurityMiddlewareTest()
    {
        // Create a simple console logger
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<SecurityMiddleware>();

        Console.WriteLine("Testing SecurityMiddleware...");

        try
        {
            // Create SecurityMiddleware instance
            var middleware = new SecurityMiddleware(logger);
            Console.WriteLine("SecurityMiddleware created successfully");
            
            // Create a test request
            var request = new SecurityRequest
            {
                Content = "Test content",
                ContentLength = 100,
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } },
                Parameters = new Dictionary<string, string> { { "test", "value" } },
                ClientId = "test-client",
                Endpoint = "/api/test"
            };
            
            Console.WriteLine("Created test request with content: " + request.Content);
            Console.WriteLine("Request content length: " + request.ContentLength);
            
            // Call ValidateRequest
            Console.WriteLine("Calling ValidateRequest...");
            var result = middleware.ValidateRequest(request);
            
            Console.WriteLine($"Validation result: IsValid={result.IsValid}");
            Console.WriteLine($"Violations count: {result.Violations.Count}");
            if (result.Violations.Count > 0)
            {
                Console.WriteLine("Violations:");
                foreach (var violation in result.Violations)
                {
                    Console.WriteLine($"  - {violation}");
                }
            }
            Console.WriteLine($"Security headers count: {result.SecurityHeaders.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
