# Test script to validate DeepSeek API key
# This script will run the HlpAI application and test DeepSeek provider availability

Write-Host "Testing DeepSeek API Key..." -ForegroundColor Yellow

# Change to the HlpAI directory
Set-Location "src\HlpAI"

# Run the application with DeepSeek provider to test the key
Write-Host "Running HlpAI with DeepSeek provider test..." -ForegroundColor Green
dotnet run -- --provider deepseek --test-connection

Write-Host "DeepSeek API key test completed." -ForegroundColor Blue