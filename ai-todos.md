### TODOs

1. Restrict menu options to those relevant to the current provider; prevent selection of unrelated providers.
   - Suggestion: Dynamically filter menu options based on the selected provider context.
   - Status: Pending
   - Notes: Awaiting user approval before implementation.

2. Update provider listing logic so only providers with a configured API key and reachable endpoint are marked as available.
   - Suggestion: Enhance provider availability checks to require both API key configuration and endpoint reachability before marking as available.
   - Status: COMPLETED
   - Notes: Modified SelectAiProviderAsync method in Program.cs to use AiProviderFactory.DetectAvailableProvidersAsync() for filtering providers. Added SupportedOSPlatform attribute for Windows compatibility. All tests pass 100% with no errors or warnings.

---

### Archived TODOs

1. Ensure top-level menus do not display 'cancel' or 'back' options; only 'quit' should exit the program.
   - Suggestion: Review all menu definitions in Program.cs and related files. Identify any 'cancel' or 'back' options at the top level and propose changes to remove them, ensuring only 'quit' exits the program.
   - Status: COMPLETED
   - Notes: Removed 'q. Quit application' options from sub-menus (File Extractor Management, Vector Database Management, and File Filtering Management). These menus now only show 'b. Back to main menu' option. Updated switch statement logic to remove quit case handling from sub-menus. Build succeeded with no compilation errors.
