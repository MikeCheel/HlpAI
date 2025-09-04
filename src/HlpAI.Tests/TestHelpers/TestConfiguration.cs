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
        // Global test cleanup - handle potential console redirection issues
        try
        {
            Console.WriteLine("✅ Test execution completed.");
        }
        catch (ObjectDisposedException)
        {
            // Console output may have been redirected and disposed by tests
            // This is expected behavior and can be safely ignored
        }
    }
}