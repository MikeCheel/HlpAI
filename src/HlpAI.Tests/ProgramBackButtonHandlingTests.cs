using Microsoft.Extensions.Logging;
using TUnit.Assertions;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using HlpAI.Models;
using HlpAI.Services;
using System.Reflection;
using System.Text.RegularExpressions;

namespace HlpAI.Tests
{
    /// <summary>
    /// Tests to ensure that all SafePromptForString calls with 'b' as default
    /// have proper handling for the 'b' input to prevent crashes.
    /// </summary>
    public class ProgramBackButtonHandlingTests
    {
        private readonly ILogger<ProgramBackButtonHandlingTests> _logger;
        private readonly AppConfiguration _config;

        public ProgramBackButtonHandlingTests()
        {
            _logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<ProgramBackButtonHandlingTests>();
            _config = new AppConfiguration();
        }

        [Test]
        public async Task AllSafePromptForStringCallsWithBDefaultShouldHaveProperHandling()
        {
            // Read the Program.cs file
            var programPath = Path.Combine(
                Directory.GetCurrentDirectory().Replace("\\bin\\Debug\\net8.0", "").Replace("/bin/Debug/net8.0", ""),
                "..", "HlpAI", "Program.cs");
            
            if (!File.Exists(programPath))
            {
                // Try alternative path
                programPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..", "..", "..", "..", "HlpAI", "Program.cs");
            }
            
            await Assert.That(File.Exists(programPath)).IsTrue();
            
            var programContent = File.ReadAllText(programPath);
            
            // Find all SafePromptForString calls with 'b' as default
            var pattern = @"SafePromptForString\s*\(\s*[^,]*,\s*""b""\s*\)";
            var matches = Regex.Matches(programContent, pattern);
            
            await Assert.That(matches.Count > 0).IsTrue();
            
            var lines = programContent.Split('\n');
            var problematicCalls = new List<string>();
            
            foreach (Match match in matches)
            {
                var lineNumber = GetLineNumber(programContent, match.Index);
                var methodContext = GetMethodContext(lines, lineNumber);
                
                // Check if the method properly handles 'b' input
                if (!HasProperBackHandling(methodContext, lineNumber))
                {
                    problematicCalls.Add($"Line {lineNumber}: {match.Value.Trim()}");
                }
            }
            
            if (problematicCalls.Count > 0)
            {
                var errorMessage = "Found SafePromptForString calls with 'b' default that don't properly handle 'b' input:\n" +
                    string.Join("\n", problematicCalls);
                throw new Exception(errorMessage);
            }
        }

        [Test]
        public async Task SelectAiProviderAsync_ShouldHandleBackInput()
        {
            // This test ensures SelectAiProviderAsync properly handles 'b' input
            // We can't easily test the actual method due to Console I/O,
            // but we can verify the code structure
            
            var programPath = await GetProgramPath();
            var programContent = File.ReadAllText(programPath);
            
            // Find SelectAiProviderAsync method
            var methodStart = programContent.IndexOf("static async Task SelectAiProviderAsync");
            await Assert.That(methodStart > -1).IsTrue();
            
            var methodEnd = FindMethodEnd(programContent, methodStart);
            var methodContent = programContent.Substring(methodStart, methodEnd - methodStart);
            
            // Verify it has proper 'b' handling
            await Assert.That(methodContent).Contains("string.Equals(input, \"b\", StringComparison.OrdinalIgnoreCase)");
            await Assert.That(methodContent).Contains("return; // Return to parent menu");
        }

        [Test]
        public async Task QuickSwitchToAvailableProviderAsync_ShouldHandleBackInput()
        {
            var programPath = await GetProgramPath();
            var programContent = File.ReadAllText(programPath);
            
            // Find QuickSwitchToAvailableProviderAsync method
            var methodStart = programContent.IndexOf("public static async Task QuickSwitchToAvailableProviderAsync");
            await Assert.That(methodStart > -1).IsTrue();
            
            var methodEnd = FindMethodEnd(programContent, methodStart);
            var methodContent = programContent.Substring(methodStart, methodEnd - methodStart);
            
            // Verify it has proper 'b' handling
            await Assert.That(methodContent).Contains("string.Equals(input, \"b\", StringComparison.OrdinalIgnoreCase)");
            await Assert.That(methodContent).Contains("return; // Return to parent menu");
        }

        [Test]
        public async Task SelectModelAsync_ShouldHandleBackInput()
        {
            var programPath = await GetProgramPath();
            var programContent = File.ReadAllText(programPath);
            
            // Find SelectModelAsync method
            var methodStart = programContent.IndexOf("private static async Task<string> SelectModelAsync");
            await Assert.That(methodStart > -1).IsTrue();
            
            var methodEnd = FindMethodEnd(programContent, methodStart);
            var methodContent = programContent.Substring(methodStart, methodEnd - methodStart);
            
            // Verify it handles 'b' input by returning empty string
            await Assert.That(methodContent).Contains("input?.ToLower() == \"b\"");
        }

        private async Task<string> GetProgramPath()
        {
            var programPath = Path.Combine(
                Directory.GetCurrentDirectory().Replace("\\bin\\Debug\\net8.0", "").Replace("/bin/Debug/net8.0", ""),
                "..", "HlpAI", "Program.cs");
            
            if (!File.Exists(programPath))
            {
                programPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..", "..", "..", "..", "HlpAI", "Program.cs");
            }
            
            await Assert.That(File.Exists(programPath)).IsTrue();
            return programPath;
        }

        private int GetLineNumber(string content, int index)
        {
            return content.Substring(0, index).Count(c => c == '\n') + 1;
        }

        private string GetMethodContext(string[] lines, int lineNumber)
        {
            // Get context around the line (200 lines before and after to capture entire method)
            var start = Math.Max(0, lineNumber - 200);
            var end = Math.Min(lines.Length, lineNumber + 200);
            
            var contextLines = new string[end - start];
            Array.Copy(lines, start, contextLines, 0, end - start);
            
            return string.Join("\n", contextLines);
        }

        private bool HasProperBackHandling(string methodContext, int lineNumber)
        {
            // Check if the method context contains proper 'b' handling
            var lowerContext = methodContext.ToLowerInvariant();
            
            // Look for various patterns of 'b' handling
            var patterns = new[]
            {
                "case \"b\":",
                "case \"back\":",
                "input.tolowerinvariant() == \"b\"",
                "input?.tolower() == \"b\"",
                "input == \"b\"",
                "input.tolower() == \"b\"",
                "choice.tolower() == \"b\"",
                "input.equals(\"b\", stringcomparison.ordinalignorecase)",
                "choice.equals(\"b\", stringcomparison.ordinalignorecase)",
                "string.equals(input, \"b\", stringcomparison.ordinalignorecase)",
                "string.equals(choice, \"b\", stringcomparison.ordinalignorecase)",
                "input.equals(\"q\", stringcomparison.ordinalignorecase) || input.equals(\"b\", stringcomparison.ordinalignorecase)",
                "\"b\" => \"\"",
                "\"back\" => \"\""
            };
            
            var hasHandling = patterns.Any(pattern => lowerContext.Contains(pattern));
            
            // Debug: Print context for failing cases
            if (!hasHandling)
            {
                Console.WriteLine($"\n=== DEBUG: Line {lineNumber} context ===");
                Console.WriteLine(methodContext);
                Console.WriteLine("=== END DEBUG ===");
            }
            
            return hasHandling;
        }

        private int FindMethodEnd(string content, int methodStart)
        {
            var braceCount = 0;
            var inMethod = false;
            
            for (int i = methodStart; i < content.Length; i++)
            {
                if (content[i] == '{')
                {
                    braceCount++;
                    inMethod = true;
                }
                else if (content[i] == '}' && inMethod)
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        return i + 1;
                    }
                }
            }
            
            return content.Length;
        }
    }
}