# Test script to verify interactive chat mode and ExtractPlainTextResponse fix
# This script will:
# 1. Start the application with directory and model parameters
# 2. Select interactive chat mode (option 1)
# 3. Send a simple test question
# 4. Exit the chat

$testInput = @"
1
What is 2 + 2?
quit
"@

Write-Host "Testing Interactive Chat Mode with ExtractPlainTextResponse fix..."
Write-Host "Input sequence: Select option 1 -> Ask 'What is 2 + 2?' -> Quit"
Write-Host ""

# Change to the correct directory and run the application with test input
Set-Location "C:\Users\mikec\source\repos\HlpAI\src\HlpAI"
$testInput | dotnet run "C:\Users\mikec\source\repos\HlpAI\src\HlpAI" "llama3.2:latest"

Write-Host ""
Write-Host "Test completed. Check above output for AI response to verify ExtractPlainTextResponse is working."