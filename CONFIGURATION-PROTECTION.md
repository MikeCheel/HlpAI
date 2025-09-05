# Configuration Protection System

## Overview

This system prevents your critical configuration settings (like LastDirectory and prompt defaults) from being accidentally reset, ensuring they persist even after configuration resets or database deletions.

## Problem Solved

Previously, settings like `LastDirectory` and prompt behavior would revert to defaults due to:

1. **"Reset all settings to defaults"** menu option
2. **"Delete configuration database"** menu option  
3. **Error recovery fallbacks** that return default configurations
4. **Test environments** creating/destroying databases

## Protection Features

### 1. Automatic Backup & Restore
- **Pre-Reset Backup**: Automatically backs up preferences before any reset
- **Startup Restoration**: Checks and restores preferences on application startup
- **Protected Settings**:
  - `LastDirectory` - Your last used directory path
  - `RememberLastDirectory` - Whether to remember the last directory
  - `DefaultPromptBehavior` - Your prompt default preferences

### 2. Configuration Validation
- **Startup Enforcement**: Validates and corrects accidental resets on startup
- **User Intent Recognition**: Distinguishes between accidental resets and explicit user choices
- **Smart Recovery**: Only auto-corrects when user didn't explicitly set the values

### 3. Command Line Management
```bash
# Backup current preferences
dotnet run -- --backup-preferences

# Restore from backup
dotnet run -- --restore-preferences

# Check protection status
dotnet run -- --check-protection

# Set prompt defaults (with explicit user intent tracking)
dotnet run -- --set-prompt-defaults individual  # Use individual defaults
dotnet run -- --set-prompt-defaults yes         # Always default to Yes
dotnet run -- --set-prompt-defaults no          # Always default to No
```

## How It Works

### 1. Protected Reset Process
When you select "Reset all settings to defaults":

1. **Backup**: System backs up your directory and prompt preferences
2. **Reset**: Configuration is reset to defaults
3. **Restore**: Critical user preferences are immediately restored
4. **Result**: You get fresh defaults but keep your directory and prompt preferences

### 2. Smart Validation on Startup
Every time the app starts:

1. **Check for Pending Reset**: If a reset was interrupted, restore preferences
2. **Validate Settings**: Check if critical settings were accidentally reset
3. **Auto-Correct**: Only corrects settings that weren't explicitly set by user
4. **Preserve Intent**: Never overrides explicit user choices

### 3. Explicit User Intent Tracking
The system tracks when you explicitly set preferences:

- **Via Command Line**: `--set-prompt-defaults no` → System remembers you want "always no"
- **Via Configuration Menu**: Selecting option 2 → System remembers your choice
- **Auto-Correction**: Only happens for accidental resets, never for explicit choices

## Protection Status

Run `dotnet run -- --check-protection` to see:

```
🛡️ Configuration Protection Status
==================================
Backup exists: ✅ Yes
Pending reset: ✅ No  
Auto-restore enabled: ✅ Yes
```

## Files Added

- `ConfigurationProtectionService.cs` - Core backup/restore functionality
- `ConfigurationValidationService.cs` - Startup validation and enforcement
- `ConfigurationProtectionServiceTests.cs` - Unit tests for protection system

## Implementation Details

### Backup Storage
- Stored in SQLite database under `system` category
- Key: `protected_user_preferences`
- Includes timestamp for tracking

### Reset Detection
- Uses `pending_reset` flag in `system` category
- Set before reset, cleared after restoration
- Enables recovery from interrupted resets

### User Intent Tracking
- `user_disabled_remember_directory` - User explicitly disabled directory memory
- `user_wants_always_no` - User explicitly wants "always no" prompts
- Prevents auto-correction of explicit user choices

## Result

Your configuration settings will now persist through:
- ✅ Manual "Reset all settings"  
- ✅ Database deletion and recreation
- ✅ Error recovery scenarios
- ✅ Test environment interference
- ✅ Application crashes during reset

**Your last directory and prompt preferences are now protected!**