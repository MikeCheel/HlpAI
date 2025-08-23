using System;

public class DebugStackTrace
{
    public static void Main()
    {
        // Test if StackTrace is null when exception is created but not thrown
        var exception = new InvalidOperationException("Test exception message");
        Console.WriteLine($"StackTrace before throwing: {exception.StackTrace}");
        Console.WriteLine($"StackTrace is null: {exception.StackTrace == null}");
        
        try
        {
            throw exception;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StackTrace after throwing: {ex.StackTrace}");
            Console.WriteLine($"StackTrace is null after throwing: {ex.StackTrace == null}");
        }
    }
}
