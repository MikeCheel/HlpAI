# AI Provider Availability Handling and Fallback Mechanisms

## Current Tasks

### 🔄 NEW: Provider Configuration Prompt Enhancement
**Status**: IN PROGRESS 🔄  
**Description**: When a user selects an unconfigured provider, prompt them to configure it immediately

**Requirements**:
- When selecting a provider that isn't configured, ask user: "This provider is not configured. Would you like to configure it now? (Y/N)"
- If Yes: Navigate directly to the configuration menu for that specific provider
- If No: Return to the provider listing menu
- Apply this enhancement to the interactive screen workflow

**Implementation Areas**:
- Provider selection logic in interactive mode
- Configuration menu navigation
- User prompt handling
- Menu flow control

**Expected Benefits**:
- Improved user experience with immediate configuration option
- Reduced friction in provider setup workflow
- Better guidance for users with unconfigured providers

### ✅ COMPLETED: Fix InvalidOperationException in Provider Selection
**Status**: COMPLETED ✅  
**Description**: Resolved InvalidOperationException that occurred when enumerating AI providers during interactive setup

**Root Cause**: Cloud providers (OpenAI, Anthropic, DeepSeek) require API keys for availability checking, but the system was attempting to check availability without first verifying if an API key was required.

**Solution Implemented**:
1. **Enhanced AiProviderFactory**: Added `RequiresApiKey(AiProvider provider)` method to identify cloud providers that need API keys
2. **Updated Provider Selection Logic**: Modified `SelectProviderForSetupAsync` in Program.cs to handle cloud and local providers differently:
   - Cloud providers: Skip availability check if no API key is configured
   - Local providers: Perform normal availability check
3. **Comprehensive Testing**: Created `ProgramProviderSelectionTests.cs` with tests to verify:
   - Correct identification of cloud vs local providers
   - No exceptions thrown during provider enumeration
   - Proper handling of both provider types

**Results**:
- ✅ Application starts successfully without InvalidOperationException
- ✅ Interactive mode launches correctly
- ✅ All tests pass (1109/1109)
- ✅ Provider enumeration works for both cloud and local providers

### ✅ COMPLETED: Resolve All Code Quality Warnings
**Status**: COMPLETED ✅  
**Description**: Successfully resolved all code quality warnings (S1075, CA1416, S6667) to achieve zero warnings in the build

**Warnings Addressed**:
1. **S1075 - Hard-coded URIs**: 13 instances in AiProviderFactory.cs
2. **CA1416 - Platform-dependent API**: 1 instance in Program.cs line 6227
3. **S6667 - Logging without exceptions**: 2 instances in Program.cs lines 6244 and 6266

**Solution Implemented**:
1. **Created AiProviderConstants.cs**: Centralized all hard-coded URIs and default model names into constants
   - `DefaultUrls` class with constants for all AI provider base URLs
   - `DefaultModels` class with constants for default model names
2. **Updated AiProviderFactory.cs**: Replaced all hard-coded values with references to constants
   - Updated constructor parameter defaults
   - Updated `GetProviderInfo` method
   - Updated `GetDefaultModelForProvider` method
3. **Fixed Platform-dependent API**: Added `[SupportedOSPlatform("windows")]` attribute to `SelectProviderForSetupAsync` method
4. **Enhanced Exception Logging**: Updated catch blocks to include exception parameter in logging calls

**Results**:
- ✅ Zero build warnings achieved
- ✅ All tests continue to pass (1109/1109)
- ✅ Code maintainability improved with centralized constants

### ✅ COMPLETED: Fix SqliteTransaction Rollback Error in OptimizedSqliteVectorStore
**Status**: COMPLETED ✅  
**Description**: Resolved InvalidOperationException "This SqliteTransaction has completed; it is no longer usable" in OptimizedSqliteVectorStore.IndexDocumentAsync method

**Root Cause**: Multiple issues causing transaction state problems:
1. `RemoveFileChunksAsync` was creating and managing its own transaction when called within an existing transaction
2. Transaction rollback was attempted even when the transaction was already completed
3. Test was using in-memory databases with separate connections, preventing data sharing between instances
4. String manipulation error with short hash values in logging

**Solution Implemented**:
1. **Fixed RemoveFileChunksAsync Transaction Handling**: Modified to properly handle externally provided transactions
   - Added `localTransaction` variable to distinguish between internal and external transactions
   - Only commit/rollback transactions that were created internally
   - Always re-throw exceptions to allow caller to handle them
2. **Enhanced Transaction Rollback Safety**: Added checks to ensure transaction is still active before rollback
   - Check `transaction.Connection != null` before attempting rollback
   - Catch `InvalidOperationException` during rollback attempts
3. **Fixed String Manipulation Error**: Added safe substring operation for file hash logging
   - Changed `fileHash[..8]` to `fileHash.Length >= 8 ? fileHash[..8] : fileHash`
4. **Fixed Test Database Sharing**: Modified `IndexDocumentAsync_ChangedFile_ShouldReindex` test
   - Changed from in-memory database to file-based database for data sharing
   - Added proper cleanup with delays to handle SQLite connection disposal

**Results**:
- ✅ All OptimizedSqliteVectorStore tests now pass (including previously failing IndexDocumentAsync_ChangedFile_ShouldReindex)
- ✅ Transaction handling is now robust and safe
- ✅ No more SqliteTransaction rollback errors
- ✅ Proper test isolation while allowing data sharing when needed

### ✅ COMPLETED: Fix Configuration Acceptance Issue
**Status**: COMPLETED ✅  
**Description**: Fixed issue where program would end when user pressed Enter to accept default configuration during interactive setup

**Root Cause**: The directory selection prompt was using an empty string ("") as the default value in `PromptForValidatedString`, which caused the method to treat Enter presses as empty input and display "Input cannot be empty. Please try again."

**Solution Implemented**:
1. **Updated Directory Selection Logic**: Modified `InteractiveSetupAsync` in Program.cs to pass `null` instead of empty string as default value
2. **Proper Behavior**: Now when no saved directory exists, the user must provide a directory path (no default is offered)
3. **Preserved Last Directory Logic**: If a last directory is saved and RememberLastDirectory is enabled, user is still prompted to use it

**Results**:
- ✅ Application no longer ends when user presses Enter without saved directory
- ✅ User is properly required to enter a directory path when none is saved
- ✅ Last directory functionality remains intact
- ✅ Interactive setup flow works as intended
- ✅ Platform compatibility properly documented
- ✅ Exception logging enhanced for better debugging

### ✅ COMPLETED: Resolve TUnit0018 Warnings
**Status**: COMPLETED ✅  
**Description**: Successfully resolved TUnit0018 warnings related to test methods assigning instance data

**Warnings Addressed**:
1. **TUnit0018 - Test methods assigning instance data**: 2 instances in MenuStateManagerTests.cs lines 148 and 204

**Solution Implemented**:
1. **Removed Instance Field Assignments**: Eliminated assignments to instance fields (`_menuStateManager = null!`) from test cleanup methods
2. **Maintained Test Isolation**: Kept proper cleanup logic while adhering to TUnit rules
3. **Verified Test Integrity**: Ensured all tests continue to pass after modifications

**Results**:
- ✅ Zero TUnit0018 warnings achieved
- ✅ All tests continue to pass (1109/1109)
- ✅ Test cleanup methods comply with TUnit framework rules
- ✅ Test isolation maintained without rule violations

### ✅ COMPLETED: Remove Directory Creation Functionality
**Status**: COMPLETED ✅  
**Description**: Updated directory validation logic to ensure paths must always exist and are never created by the application

**Changes Made**:
1. **Removed Directory Creation Logic**: Eliminated the prompt to create non-existent directories in `InteractiveSetupAsync`
2. **Simplified Validation**: Directory paths that don't exist now simply display an error message and prompt again
3. **Enhanced User Guidance**: Clear error message instructs users to provide existing directory paths

**Solution Implemented**:
- Modified `Program.cs` lines 516-542 to remove directory creation functionality
- Replaced complex creation logic with simple validation and error messaging
- Maintained quit/exit functionality for user convenience

**Results**:
- ✅ Application no longer creates directories automatically
- ✅ Users must provide existing directory paths
- ✅ Clear error messaging guides users to valid input
- ✅ Simplified and more predictable behavior

## Current Implementation

### Current Todo

1. [Completed] Update application title from 'HlpAI - Enhanced MCP RAG Server' to 'HlpAI - Intelligent Document Assistant'
2. [Completed] Document AI provider switching process in README-INTERACTIVE.md
3. [Completed] Add configuration details for timeout and max tokens settings to README-MCP.md
4. [Completed] Implement standardized error handling middleware for AI operations
5. [Completed] Extend AiProviderFactory to support additional provider types - Deferred for later implementation
6. [Completed] Ensure menu items are appropriate for current provider (hide model-related options unless supported)
7. [Completed] Document AI tool definitions (tools/list and tools/call) in README-MCP.md
8. [Completed] Add ask_ai and analyze_file functionality documentation
9. [Completed] Document RAG-enhanced questioning features
10. [Completed] Add semantic search API documentation
11. [Completed] Document command-line arguments for AI provider timeouts (OpenAI, Anthropic, DeepSeek)
12. [Completed] Document max token limit configurations for all supported providers
13. [Completed] Add documentation for cosine similarity calculation in SemanticSearchService
14. [Completed] Document IAiProvider interface usage
15. [Completed] Add configuration details for secure API key storage
16. [Completed] Document provider auto-detection and fallback mechanisms
17. [Completed] Add documentation for model compatibility handling between providers
18. [Completed] Document configuration persistence features
19. ✅ Fix race condition in Constructor_WithLogger_InitializesCorrectly test - Added verification steps to ensure configuration is properly saved and loaded before MenuStateManager creation
20. [Completed] Modify interactive mode Step 2 to ask if user wants to use current configured provider or choose a different one before model selection - Enhanced Step 2 now calls `SelectProviderForSetupAsync` to ask about current provider usage, then `SelectModelForProviderAsync` for model selection
    - Implementation successfully compiled and interactive mode launches correctly
    - New methods handle provider selection before model selection in the startup flow

## Archived Content

### Previous Todos

1. [Question] How to add new tool integrations to the MCP system?
   - Suggestion: Document the tool registration process in EnhancedMcpRagServer.cs
   - Answer: Use this suggestion for now.

2. [Question] Documentation for RAG configuration options like similarity thresholds, chunk sizes, or indexing parameters?
   - Suggestion: Add configuration section to README-MCP.md
   - Answer: Use this suggestion.

3. [Question] Best practices for error recovery and logging configuration?
   - Suggestion: Implement standardized error handling middleware
   - Answer: Use this suggestion.

4. [Question] Documentation of configurable parameters for indexing thresholds (file size limits, skip rules)?
   - Suggestion: Add to ConfigurationService documentation
   - Answer: Use this suggestion.

5. [Question] How to add new AI provider types or modify detection behavior?
   - Suggestion: Extend AiProviderFactory class
   - Answer: 

6. [Question] How to configure timeout settings for different AI providers?
   - Suggestion: Document in CommandLineArgumentsService.cs
   - Answer: Use this suggestion.

7. [Question] How to set maximum token limits per provider?
   - Suggestion: Add to AppConfiguration documentation
   - Answer: Use this suggestion.

8. [Question] How to implement semantic search functionality?
   - Suggestion: Document SemanticSearchService usage
   - Answer: Use this suggestion.

9. [Question] How to add support for additional file types?
   - Suggestion: Extend file extractor management system
   - Answer: Use this suggestion.

10. [Question] How to customize AI interaction APIs?
    - Suggestion: Document IAiProvider interface
    - Answer: Use this suggestion.

### From vibe-docs/ai-provider-availability.md

## System Startup Behavior

### When No AI Providers Are Available

The HlpAI system is designed to handle scenarios where no AI providers are available at startup gracefully:

1. **Default Provider Initialization**: The system attempts to initialize the default AI provider (Ollama) based on the configuration
2. **Fallback to Configuration Defaults**: If no configuration exists, the system uses built-in defaults
3. **Graceful Startup**: The application starts successfully even if no providers are immediately available
4. **Runtime Detection**: The system continuously checks for provider availability during operation

### Startup Flow

```
Application Start
       ↓
Load Configuration (ConfigurationService.LoadConfiguration)
       ↓
Create AI Provider (AiProviderFactory.CreateProvider)
       ↓
Initialize EnhancedMcpRagServer
       ↓
Check Provider Availability (IsAvailableAsync)
       ↓
[Available] → Normal Operation
[Unavailable] → Graceful Degradation Mode
```

## Default Configuration

### Built-in Defaults

When no configuration file exists or configuration loading fails, the system uses these defaults:

- **Default Provider**: `AiProviderType.Ollama`
- **Default Ollama URL**: `http://localhost:11434`
- **Default Ollama Model**: `llama3.2:3b`
- **Default LM Studio URL**: `http://localhost:1234`
- **Default Open Web UI URL**: `http://localhost:3000`
- **Remember Last Provider**: `true`

### Configuration Sources

1. **Primary**: `appsettings.json` file
2. **Fallback**: Built-in default values in `AppConfiguration` class
3. **Override**: Command-line arguments (highest priority)

## Provider Detection and Management

### Automatic Provider Detection

The `AiProviderFactory.DetectAvailableProvidersAsync()` method:

1. **Scans All Configured Providers**: Attempts to connect to each provider type
2. **Tests Connectivity**: Uses `IsAvailableAsync()` to verify each provider
3. **Returns Availability Status**: Provides a list of available providers
4. **Logs Results**: Records detection results for debugging

### Provider Creation Process

```csharp
// AiProviderFactory.CreateProvider implementation
switch (config.LastProvider)
{
    case AiProviderType.Ollama:
        return new OllamaClient(config.OllamaUrl, config.OllamaModel, logger);
    case AiProviderType.LmStudio:
        return new LmStudioProvider(config.LmStudioUrl, config.LmStudioModel, logger);
    case AiProviderType.OpenWebUi:
        return new OpenWebUiProvider(config.OpenWebUiUrl, config.OpenWebUiModel, logger);
    default:
        throw new ArgumentException($"Unknown AI provider type: {config.LastProvider}
