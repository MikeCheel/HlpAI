# AI Todos - HlpAI Project

## ⚠️ CRITICAL CODE QUALITY REQUIREMENTS ⚠️
**BEFORE ANY TASK IS MARKED COMPLETE:**
- ❌ ZERO compilation errors allowed
- ❌ ZERO warnings allowed  
- ❌ ZERO messages allowed
- ✅ ALL tests MUST pass (100% success rate)
- ✅ Build MUST succeed completely
- 🚫 NO EXCEPTIONS - These requirements are NON-NEGOTIABLE

**If ANY of the above requirements are not met, the task is NOT complete and must be fixed immediately.**

## Current Session Progress

### Completed Tasks ✅

1. **Fix UnauthorizedAccessException crash** - COMPLETED
   - Fixed EnhancedMcpRagServer.IndexAllDocumentsAsync method
   - Wrapped Directory.GetFiles call in try-catch block
   - Added graceful error handling and logging instead of crashing

2. **Add initialization error handling** - COMPLETED
   - Added proper error handling around server.InitializeAsync() call in Program.cs
   - Prevents application crashes during initialization
   - Logs errors appropriately

3. **Require audit directory** - COMPLETED
   - Modified audit functionality to require users to specify a directory
   - Removed default to MyDocuments
   - Added proper validation and updated help text

4. **TUnit Framework Note** - COMPLETED
   - INTERNAL NOTE: This application exclusively uses TUnit testing framework
   - Never use NUnit or other testing frameworks
   - All test files should use TUnit attributes: [Test], [TestClass], [Before(Test)], [After(Test)] etc.

5. **Write unit tests** - COMPLETED ✅
   - Written comprehensive unit tests for new error handling functionality
   - Covers both EnhancedMcpRagServer and Program.cs initialization
   - Uses TUnit framework (NOT NUnit)
   - All 1296 tests passing (100% success rate)
   - Fixed all TUnit syntax issues and Assert format
   - Added comprehensive error handling tests for:
     - Invalid paths and unauthorized access
     - Initialization errors and graceful fallbacks
     - Null parameter handling
     - JSON deserialization errors

6. **Fix CS8625 Warning** - COMPLETED ✅
   - Resolved "Cannot convert null literal to non-nullable reference type" warning
   - Updated McpRequest.Params property from `required object Params` to `required object? Params`
   - Build now succeeds with zero warnings and zero errors
   - All tests continue to pass after the fix

### Current Status: All High Priority Tasks Complete ✅
- Zero compilation errors
- Zero warnings
- All 1296 unit tests passing
- Comprehensive error handling implemented
- Robust fallback mechanisms in place

### Pending Tasks 📋

7. **Improve directory enumeration** - PENDING
   - Implement safer directory enumeration
   - Handle restricted directories using Directory.EnumerateFiles
   - Add proper exception handling for each directory traversal
   - Priority: Medium

8. **Update documentation** - PENDING
   - Update project documentation
   - Reflect changes made to audit functionality
   - Document error handling improvements
   - Priority: Medium

## Next Steps When Resuming

### 🔥 MANDATORY VERIFICATION BEFORE ANY WORK 🔥
1. **Run `dotnet build src/HlpAI.sln`** - Must succeed with ZERO errors and ZERO warnings
2. **Run `dotnet test src/HlpAI.Tests/HlpAI.Tests.csproj`** - ALL tests must pass (100%)
3. **If either fails, FIX IMMEDIATELY before proceeding with any other work**

### Code Quality Enforcement
- Every single task completion requires verification of the above
- No shortcuts, no exceptions, no "I'll fix it later"
- Broken code = incomplete task, period

**Current Status: All critical error handling tasks completed successfully!**

- All high-priority error handling tasks are complete
- Application is stable with comprehensive error handling
- All 1296 unit tests passing with 100% success rate
- Zero compilation errors or warnings
- Ready for production use

**If continuing development:**
1. Consider implementing safer directory enumeration (Task #7)
2. Update project documentation (Task #8)
3. Any new features or improvements as requested

**Quality Metrics Achieved:**
- ✅ Zero errors, warnings, or messages
- ✅ 100% test pass rate (1296/1296)
- ✅ Comprehensive error handling coverage
- ✅ Graceful fallback mechanisms
- ✅ Proper logging and user feedback

1. Fix TUnit syntax issues in ProgramInitializationErrorHandlingTests.cs:
   - Remove `await` from Assert statements (TUnit doesn't use async assertions like that)
   - Use proper TUnit Assert syntax
   - Run tests to ensure they compile and pass

2. Complete unit test coverage for error handling

3. Move on to directory enumeration improvements

4. Update documentation

## Important Notes

- **Testing Framework**: Always use TUnit, never NUnit
- **Error Handling**: Focus on graceful degradation, not crashes
- **Code Quality**: Maintain 70%+ test coverage
- **All tests must pass 100%**
- **Zero errors, warnings, or messages before marking tasks complete**
- **Approval Required**: All tasks require explicit approval before starting
- **Session Continuity**: All project rules and guidelines are understood and will be followed upon return
- **Current State**: Ready to continue with in-progress unit testing task when approved

### CRITICAL REMINDER - READ EVERY TIME

**MANDATORY**: Before starting ANY work, ALWAYS read and follow the project rules:
- No coding or task shall be started unless explicitly approved by the user
- No task that causes changes should be performed without approval, including git changes
- All design, coding, and security best practices must be used
- Keep track of progress in ai-todos.md file
- Use TUnit framework exclusively for testing (NOT NUnit)
- Achieve at least 70% code coverage
- Resolve all errors, warnings, and messages before marking tasks complete
- All tests must pass 100%
- Write unit tests for any bugs that are fixed

### PERSONAL NOTES

**User Communication Style**: User can be direct and expects me to follow rules precisely. When they say "not right now" or similar, I should wait for explicit approval before proceeding with any work.

**Task Verification**: User emphasized the importance of double-checking completed tasks before marking them as complete. Never assume something is done without verification.

**Session Context**: User gets frustrated when I don't follow established patterns or forget previous context. Always reference the CRITICAL REMINDER section above before starting any work.

## Files Modified This Session

- `src/HlpAI/MCP/EnhancedMcpRagServer.cs` - Added error handling
- `src/HlpAI/Program.cs` - Added initialization error handling
- `src/HlpAI.Tests/MCP/EnhancedMcpRagServerTests.cs` - Added unit tests
- `src/HlpAI.Tests/Program/ProgramInitializationErrorHandlingTests.cs` - Created (needs TUnit syntax fixes)

## Current Issues to Address

### High Priority - All Resolved ✅

1. **TUnit Syntax Issues in ProgramInitializationErrorHandlingTests.cs**
   - Location: `src/HlpAI.Tests/ProgramInitializationErrorHandlingTests.cs`
   - Issue: Incorrect TUnit syntax causing compilation errors
   - Status: COMPLETED
   - Details: Fixed async method declarations - marked ErrorHandling_ContainsAuditSuggestion, ErrorHandling_ContainsHelpfulSuggestions, and ErrorHandling_LogsErrorsAppropriately as async Task
   - Priority: HIGH - RESOLVED

2. **Vector Store Initialization with Non-Existent Directory**
   - Location: `src/HlpAI/MCP/EnhancedMcpRagServer.cs`
   - Issue: InitializeVectorStore method failed when root directory didn't exist
   - Status: COMPLETED
   - Details: Added directory creation logic in InitializeVectorStore method to ensure root directory exists before creating SQLite database
   - Priority: HIGH - RESOLVED

3. **CS1998 Warnings in ProgramInitializationErrorHandlingTests.cs**
   - Location: `src/HlpAI.Tests/ProgramInitializationErrorHandlingTests.cs`
   - Issue: Async methods without await causing CS1998 warnings
   - Status: COMPLETED
   - Details: Added await Task.CompletedTask to Setup() and Cleanup() methods to resolve async method warnings
   - Priority: HIGH - RESOLVED

### Final Status Update
- ✅ All 1272 tests now pass
- ✅ Zero compilation errors
- ✅ Zero warnings (CS1998 warnings resolved)
- ✅ Zero messages
- ✅ Build successful
- ✅ Project meets all mandatory requirements

---

## Previous Session TODOs (Archived)

### TODOs

1. Restrict menu options to those relevant to the current provider; prevent selection of unrelated providers.
   - Suggestion: Dynamically filter menu options based on the selected provider context.
   - Status: PENDING APPROVAL
   - Notes: Implementation completed - adaptive menu display in ShowAiProviderMenuAsync method shows only relevant options for current provider (URL/model configuration for active provider, API key management only for cloud providers). Added IsCloudProvider helper method and fixed database concurrency issue in HhExeDetectionService with semaphore synchronization. All 1247 tests pass with no errors or warnings. AWAITING USER APPROVAL TO MARK AS COMPLETED.

2. Organize test files to be exclusively under the tests directory.
   - Suggestion: Move any test-related files from the main project to the appropriate test project directory structure.
   - Status: COMPLETED
   - Notes: Successfully organized test files in multiple phases:
     **Phase 1**: Moved TestSetLastDirectory.cs from src\HlpAI\ to src\HlpAI.Tests\TestHelpers\ with proper namespace update (HlpAI.Tests.TestHelpers). Removed the --test-set-last-directory command line option from Program.cs since the utility is now part of the test project. Cleaned up build artifacts (obj\TestSetLastDirectory directory).
     **Phase 2**: Moved PowerShell test files from root directory to test project: test-deepseek-key.ps1, test-directory-save.ps1, and test-last-directory.ps1 → src\HlpAI.Tests\TestHelpers\.
     **Phase 3**: Moved SecurityMiddlewareTest project to test utilities - converted src\SecurityMiddlewareTest\Program.cs to src\HlpAI.Tests\TestHelpers\SecurityMiddlewareTestProgram.cs as a static test utility class and deleted the entire SecurityMiddlewareTest project directory.
     **Phase 4**: Cleaned up duplicate PowerShell test files from root directory by removing test-deepseek-key.ps1 and test-last-directory.ps1 that were already moved to src\HlpAI.Tests\TestHelpers. Left run-tests-with-coverage.ps1 and run-tests.bat in root as these are test runner scripts, not test files.
     **Phase 5**: Removed obsolete PowerShell test scripts from src\HlpAI.Tests\TestHelpers as they were outdated manual testing utilities: test-deepseek-key.ps1 (referenced non-existent --test-connection flag), test-directory-save.ps1 (used deprecated command line arguments), and test-last-directory.ps1 (used manual input automation superseded by proper unit tests).
     Verified all 1264 tests pass and main application builds successfully. Test organization is now properly structured with all test utilities in the designated test project.

3. Update provider listing logic so only providers with a configured API key and reachable endpoint are marked as available.
   - Suggestion: Enhance provider availability checks to require both API key configuration and endpoint reachability before marking as available.
   - Status: COMPLETED
   - Notes: Implementation completed - modified SelectAiProviderAsync method in Program.cs to use AiProviderFactory.DetectAvailableProvidersAsync() for filtering providers. Added SupportedOSPlatform attribute for Windows compatibility. FIXED ISSUE: Enhanced DetectAvailableProvidersAsync() in AiProviderFactory.cs to validate that retrieved API keys are not null/empty before marking cloud providers as available. Now properly returns "API key is empty or invalid" error message for empty/corrupted keys. All 1247 tests pass 100% with no errors or warnings.

4. Remove inappropriate 'cancel' and 'back' options from setup prompts and top-level contexts.
   - Suggestion: Review PromptService.cs and all prompt methods to remove 'cancel'/'back'/'b' options from contexts where they don't make logical sense (e.g., initial setup, document directory selection, top-level configuration prompts where there's no previous context to return to).
   - Status: COMPLETED
   - Notes: Implementation completed - Added new setup-specific methods (PromptYesNoSetupAsync, PromptYesNoDefaultYesSetupAsync, PromptYesNoDefaultNoSetupAsync, PromptForValidatedStringSetup, PromptForStringSetup) to PromptService.cs that do not offer cancel/back options. Updated Program.cs line 696 to use PromptYesNoDefaultYesSetupAsync for model selection and line 763 to use PromptYesNoDefaultYesSetupAsync for operation mode selection. Updated Program.cs line 6960 to use PromptYesNoDefaultYesSetupAsync instead of PromptYesNoDefaultYesCancellableAsync for initial provider configuration prompts. Updated Program.cs line 640 to use PromptForValidatedStringSetup for documents directory prompt. Updated Program.cs line 823 to use PromptYesNoDefaultYesSetupAsync for configuration confirmation prompt. Fixed AI provider selection during initial setup by adding SafePromptForStringSetup wrapper and updating SelectProviderForSetupAsync to use setup-specific methods when hasParentMenu is false. Removed associated null checks and "Setup cancelled" logic from all locations. All setup prompts now properly exclude cancel/back options. All 1247 tests pass.

---

### Archived TODOs

1. Ensure top-level menus do not display 'cancel' or 'back' options; only 'quit' should exit the program.
   - Suggestion: Review all menu definitions in Program.cs and related files. Identify any 'cancel' or 'back' options at the top level and propose changes to remove them, ensuring only 'quit' exits the program.
   - Status: COMPLETED
   - Notes: Removed 'q. Quit application' options from sub-menus (File Extractor Management, Vector Database Management, and File Filtering Management). These menus now only show 'b. Back to main menu' option. Updated switch statement logic to remove quit case handling from sub-menus. Build succeeded with no compilation errors.
