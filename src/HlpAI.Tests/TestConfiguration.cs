namespace HlpAI.Tests;

public static class TestConfiguration
{
    [Before(Assembly)]
    public static void SetupAssembly()
    {
        // Global test setup
        Console.WriteLine("🧪 Starting test execution...");
    }

    [After(Assembly)]
    public static void TearDownAssembly()
    {
        // Global test cleanup
        Console.WriteLine("✅ Test execution completed.");
    }
}