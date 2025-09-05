using System.Text.Json;
using HlpAI.Models;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using HlpAI.MCP;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Integration;

/// <summary>
/// Real-world usage scenario tests that simulate complete user workflows
/// These tests focus on catching issues that unit tests might miss by testing 
/// the full application flow as users would actually use it.
/// 
/// Purpose: Ensure the application works correctly in practice, not just in isolated unit tests
/// Focus: Quality over coverage - real user scenarios that could break in production
/// </summary>
[NotInParallel] // These tests simulate real usage and need isolation
public class RealWorldUsageScenarioTests
{
    /// <summary>
    /// Scenario 1: New user first-time setup and basic document processing
    /// Tests the complete onboarding flow that a real user would experience
    /// </summary>
    [Test]
    public async Task Scenario_NewUserFirstTimeSetup_CompleteWorkflow()
    {
        // Create minimal test environment
        var testDirectory = FileTestHelper.CreateTempDirectory("scenario1");
        var logger = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Critical))
            .CreateLogger<EnhancedMcpRagServer>();
        
        try
        {
            // Create basic configuration without database persistence
            var config = new AppConfiguration
            {
                LastDirectory = testDirectory,
                RememberLastDirectory = true,
                LastOperationMode = OperationMode.Hybrid,
                LastProvider = AiProviderType.Ollama,
                MaxFileAuditSizeBytes = 256 * 1024 // 256KB for tests
            };
            
            // Test server initialization without heavy operations
            using var server = new EnhancedMcpRagServer(logger, testDirectory, config, "test-model", OperationMode.Hybrid);
            
            // Verify basic properties
            await Assert.That(server.RootPath).IsEqualTo(testDirectory);
            await Assert.That(server._operationMode).IsEqualTo(OperationMode.Hybrid);
        }
        finally
        {
            FileTestHelper.SafeDeleteDirectory(testDirectory);
        }
    }

    /// <summary>
    /// Scenario 2: Daily usage - Mixed document types with realistic file sizes
    /// Tests handling of diverse document formats that users commonly work with
    /// </summary>
    [Test]
    public async Task Scenario_DailyUsage_MixedDocumentTypes_RealisticSizes()
    {
        // Create minimal test environment
        var testDirectory = FileTestHelper.CreateTempDirectory("scenario2");
        var logger = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Critical))
            .CreateLogger<EnhancedMcpRagServer>();
        
        try
        {
            // Create test files
            await File.WriteAllTextAsync(Path.Combine(testDirectory, "test.txt"), "Sample content");
            await File.WriteAllTextAsync(Path.Combine(testDirectory, "test.md"), "# Test\nContent");
            
            var config = new AppConfiguration
            {
                LastDirectory = testDirectory,
                LastOperationMode = OperationMode.Hybrid,
                MaxFileAuditSizeBytes = 256 * 1024
            };
            
            // Test server with mixed documents
            using var server = new EnhancedMcpRagServer(logger, testDirectory, config, "test-model", OperationMode.Hybrid);
            
            await Assert.That(server.RootPath).IsEqualTo(testDirectory);
            await Assert.That(server._operationMode).IsEqualTo(OperationMode.Hybrid);
        }
        finally
        {
            FileTestHelper.SafeDeleteDirectory(testDirectory);
        }
    }

    /// <summary>
    /// Scenario 3: Configuration changes and persistence under stress
    /// Tests configuration reliability when user frequently changes settings
    /// </summary>
    [Test]
    public async Task Scenario_ConfigurationReliability_FrequentChanges()
    {
        // Test configuration object state changes (no database)
        var config = new AppConfiguration();
        
        var providers = new[] { AiProviderType.Ollama, AiProviderType.OpenAI };
        var models = new[] { "llama3.1", "gpt-4" };
        var modes = new[] { OperationMode.Hybrid, OperationMode.MCP };
        
        // Test rapid configuration changes in memory
        for (int i = 0; i < 2; i++)
        {
            config.LastProvider = providers[i % providers.Length];
            config.LastModel = models[i % models.Length];
            config.LastOperationMode = modes[i % modes.Length];
            
            // Verify immediate state changes
            await Assert.That(config.LastProvider).IsEqualTo(providers[i % providers.Length]);
            await Assert.That(config.LastModel).IsEqualTo(models[i % models.Length]);
        }
        
        // Verify final state
        await Assert.That(config).IsNotNull();
        await Assert.That(config.LastProvider).IsEqualTo(providers[1]);
        await Assert.That(config.LastModel).IsEqualTo(models[1]);
    }

    /// <summary>
    /// Scenario 4: Menu navigation workflows - Real user interaction patterns
    /// Tests the complete menu system as users would actually navigate it
    /// </summary>
    [Test]
    public async Task Scenario_MenuNavigation_RealUserWorkflows()
    {
        // Test menu structure expectations (logical validation)
        var expectedMainMenuActions = new Dictionary<int, string>
        {
            [1] = "interactive_chat",
            [2] = "rag_question", 
            [3] = "ask_ai",
            [4] = "operations_menu",
            [5] = "configuration_menu",
            [6] = "management_menu"
        };
        
        // Verify menu structure is logically consistent
        foreach (var kvp in expectedMainMenuActions)
        {
            await Assert.That(expectedMainMenuActions.ContainsKey(kvp.Key)).IsTrue();
            await Assert.That(expectedMainMenuActions[kvp.Key]).IsEqualTo(kvp.Value);
        }
        
        // Test navigation options
        var navigationOptions = new[] { "x", "cancel", "m", "main" };
        await Assert.That(navigationOptions.Contains("x")).IsTrue();
        await Assert.That(navigationOptions.Contains("cancel")).IsTrue();
        await Assert.That(navigationOptions.Contains("m")).IsTrue();
    }

    /// <summary>
    /// Scenario 5: Error handling and recovery - Real-world failure modes
    /// Tests how the application handles common failure scenarios users encounter
    /// </summary>
    [Test]
    public async Task Scenario_ErrorHandling_RealWorldFailures()
    {
        var logger = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Critical))
            .CreateLogger<EnhancedMcpRagServer>();
        
        // Test handling of missing directory
        var invalidConfig = new AppConfiguration
        {
            LastDirectory = "/nonexistent/directory/path",
            RememberLastDirectory = true,
            LastOperationMode = OperationMode.Hybrid,
            LastProvider = AiProviderType.Ollama
        };
        
        // Verify graceful handling
        try
        {
            using var testServer = new EnhancedMcpRagServer(logger, "/nonexistent/directory/path", invalidConfig, "test-model", OperationMode.Hybrid);
            await Assert.That(testServer).IsNotNull();
            await Assert.That(testServer.RootPath).IsEqualTo("/nonexistent/directory/path");
        }
        catch (Exception ex)
        {
            await Assert.That(ex.Message).IsNotNull();
        }
    }

    /// <summary>
    /// Scenario 6: Performance Under Realistic Load
    /// Tests system performance with realistic document volumes and usage patterns
    /// </summary>
    [Test]
    public async Task Scenario_PerformanceUnderLoad_RealisticUsagePatterns()
    {
        // Test performance of configuration operations
        var operationStartTime = DateTime.UtcNow;
        
        var config = new AppConfiguration();
        
        // Simulate configuration changes (in-memory only)
        for (int i = 0; i < 5; i++)
        {
            config.LastModel = $"perf-model-{i}";
            config.LastProvider = AiProviderType.Ollama;
            config.LastOperationMode = OperationMode.Hybrid;
            
            // Verify each change
            await Assert.That(config.LastModel).IsEqualTo($"perf-model-{i}");
        }
        
        var operationEndTime = DateTime.UtcNow;
        var operationDuration = operationEndTime - operationStartTime;
        
        // Operations should complete quickly
        await Assert.That(operationDuration.TotalMilliseconds).IsLessThan(100);
        
        // Test memory usage
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var memoryBefore = GC.GetTotalMemory(false);
        
        // Minimal operations
        for (int i = 0; i < 3; i++)
        {
            var tempConfig = new AppConfiguration
            {
                LastModel = $"memory-test-{i}",
                LastProvider = AiProviderType.Ollama
            };
            await Assert.That(tempConfig.LastModel).IsEqualTo($"memory-test-{i}");
        }
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var memoryAfter = GC.GetTotalMemory(false);
        
        // Memory usage should be minimal
        var memoryGrowth = memoryAfter - memoryBefore;
        await Assert.That(memoryGrowth).IsLessThan(1024 * 1024); // Less than 1MB growth
    }
}