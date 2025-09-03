### TODOs

1. Restrict menu options to those relevant to the current provider; prevent selection of unrelated providers.
   - Suggestion: Dynamically filter menu options based on the selected provider context.
   - Status: PENDING APPROVAL
   - Notes: Implementation completed - adaptive menu display in ShowAiProviderMenuAsync method shows only relevant options for current provider (URL/model configuration for active provider, API key management only for cloud providers). Added IsCloudProvider helper method and fixed database concurrency issue in HhExeDetectionService with semaphore synchronization. All 1247 tests pass with no errors or warnings. AWAITING USER APPROVAL TO MARK AS COMPLETED.

2. Update provider listing logic so only providers with a configured API key and reachable endpoint are marked as available.
   - Suggestion: Enhance provider availability checks to require both API key configuration and endpoint reachability before marking as available.
   - Status: COMPLETED
   - Notes: Implementation completed - modified SelectAiProviderAsync method in Program.cs to use AiProviderFactory.DetectAvailableProvidersAsync() for filtering providers. Added SupportedOSPlatform attribute for Windows compatibility. FIXED ISSUE: Enhanced DetectAvailableProvidersAsync() in AiProviderFactory.cs to validate that retrieved API keys are not null/empty before marking cloud providers as available. Now properly returns "API key is empty or invalid" error message for empty/corrupted keys. All 1247 tests pass 100% with no errors or warnings.

3. Remove inappropriate 'cancel' and 'back' options from setup prompts and top-level contexts.
   - Suggestion: Review PromptService.cs and all prompt methods to remove 'cancel'/'back'/'b' options from contexts where they don't make logical sense (e.g., initial setup, document directory selection, top-level configuration prompts where there's no previous context to return to).
   - Status: COMPLETED
   - Notes: Implementation completed - Added new setup-specific methods (PromptYesNoSetupAsync, PromptYesNoDefaultYesSetupAsync, PromptYesNoDefaultNoSetupAsync, PromptForValidatedStringSetup, PromptForStringSetup) to PromptService.cs that do not offer cancel/back options. Updated Program.cs line 696 to use PromptYesNoDefaultYesSetupAsync for model selection and line 763 to use PromptYesNoDefaultYesSetupAsync for operation mode selection. Updated Program.cs line 6960 to use PromptYesNoDefaultYesSetupAsync instead of PromptYesNoDefaultYesCancellableAsync for initial provider configuration prompts. Updated Program.cs line 640 to use PromptForValidatedStringSetup for documents directory prompt. Updated Program.cs line 823 to use PromptYesNoDefaultYesSetupAsync for configuration confirmation prompt. Fixed AI provider selection during initial setup by adding SafePromptForStringSetup wrapper and updating SelectProviderForSetupAsync to use setup-specific methods when hasParentMenu is false. Removed associated null checks and "Setup cancelled" logic from all locations. All setup prompts now properly exclude cancel/back options. All 1247 tests pass.

---

### Archived TODOs

1. Ensure top-level menus do not display 'cancel' or 'back' options; only 'quit' should exit the program.
   - Suggestion: Review all menu definitions in Program.cs and related files. Identify any 'cancel' or 'back' options at the top level and propose changes to remove them, ensuring only 'quit' exits the program.
   - Status: COMPLETED
   - Notes: Removed 'q. Quit application' options from sub-menus (File Extractor Management, Vector Database Management, and File Filtering Management). These menus now only show 'b. Back to main menu' option. Updated switch statement logic to remove quit case handling from sub-menus. Build succeeded with no compilation errors.
