# Test script to set LastDirectory in the configuration database
# This will allow us to test the "Use last directory" prompt

Write-Host "Setting LastDirectory to test the prompt functionality..."

# Run the application with a directory to complete setup and save LastDirectory
$testDir = "C:\Users\mikec\Documents"
Write-Host "Running application with directory: $testDir"

# Create a simple input file to automate the setup
$inputFile = "test-input.txt"
@"
1
1
q
"@ | Out-File -FilePath $inputFile -Encoding UTF8

try {
    # Run the application with automated input
    Get-Content $inputFile | dotnet run $testDir
    Write-Host "Setup completed successfully!"
} catch {
    Write-Host "Error during setup: $_"
} finally {
    # Clean up
    if (Test-Path $inputFile) {
        Remove-Item $inputFile
    }
}

# Verify the configuration
Write-Host "\nChecking current configuration:"
dotnet run -- --show-config

Write-Host "\nNow testing the 'Use last directory' prompt:"
Write-Host "Run: dotnet run"
Write-Host "You should see the 'Use last directory' prompt with default 'yes'"