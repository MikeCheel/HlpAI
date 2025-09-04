# Automation script to get through HlpAI interactive setup quickly
# This script provides the necessary inputs to reach the main menu

$inputs = @(
    'C:\Users\mikec\Desktop\ChmData',  # Step 1: Document Directory
    '1',                                    # Step 2: Select Ollama (first option)
    '1',                                    # Step 3: Select first available model
    '1'                                     # Step 4: Select Hybrid mode (first option)
)

# Join inputs with newlines and pipe to the application
($inputs -join "`n") | dotnet run --project src/HlpAI/HlpAI.csproj