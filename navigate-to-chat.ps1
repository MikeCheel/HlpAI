# Script to navigate to interactive chat mode
# This script provides automated inputs to reach the chat functionality

# Create input sequence:
# y - use last directory
# y - use current AI provider
# y - use current model
# y - use current operation mode
# 4 - select interactive chat mode
$inputs = @('y', 'y', 'y', 'y', '4')

# Convert inputs to a single string with newlines
$inputString = $inputs -join "`n"

# Run the application with piped input
$inputString | dotnet run --project src/HlpAI/HlpAI.csproj