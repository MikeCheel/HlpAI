# Test script to manually set and verify LastDirectory
Write-Host "Testing LastDirectory save/load functionality..."

# First, show current config
Write-Host "\nCurrent configuration:"
dotnet run --project src/HlpAI/HlpAI.csproj --show-config

# Set a test directory using the command line argument
$testDir = "C:\Users\mikec\Documents"
Write-Host "\nSetting LastDirectory to: $testDir"
dotnet run --project src/HlpAI/HlpAI.csproj --set-remember-last-directory true

# Now try to run a simple test to set the directory
Write-Host "\nRunning test to set directory..."
# We'll need to create a simple test method to set the directory

Write-Host "\nChecking configuration after test:"
dotnet run --project src/HlpAI/HlpAI.csproj --show-config