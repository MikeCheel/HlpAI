# HlpAI Development Tasks

**IMPORTANT REQUIREMENTS:**
- Each todo must have unit tests written. TUnit is the test framework. I want 90% or better code coverage.
- No errors, warnings, or messages allowed (no suppressions)
- Configuration should be stored in an appwide SQLite database. this db should be located in the users home diretory and created if it doesnt exist.
- Work on one todo at a time, wait for approval before starting next

## Configuration & Setup
- [x] Look for hh.exe in default location ("C:\Windows\hh.exe") - with unit tests
- [x] Allow hh.exe path to be configured via menu system if not found in default location - store in SQLite config DB.
- [x] Make configuration prompts default to "yes" when Enter is pressed without y/n input - with unit tests

## Logging System
- [x] Create error logging system for interactive mode - store config in configuration db, with unit tests
- [x] Make logging configurable as a command-line parameter - with unit tests
- [x] Add logging configuration to menu system - store in SQLite configutation DB, with unit tests
- [x] Implement log viewer in menu system with paging functionality - with unit tests

## File Management & Export
- [x] Make "List all available files" feature exportable - with unit tests
- [x] Implement menu-driven cleanup routine - with unit tests
- [x] Add configurable file type filtering system with include/exclude patterns - store in SQLite config DB, with unit tests
  - Support for include patterns (e.g., "*.txt,*.md,*.pdf")
  - Support for exclude patterns (e.g., "*.tmp,*.log,*~")
  - Command-line parameters: --include-files, --exclude-files
  - Menu system integration for file type management
  - Apply filtering in all modes: interactive, server, and library usage
  - Default supported types: .txt, .md, .html, .htm, .pdf, .chm, .hhc, .log, .csv
  - Validation of file extension patterns
  - Real-time preview of matching files in menu
  - Integration with file indexing and RAG processing
- [x] Add file type extractor management system - store in SQLite config DB, with unit tests
  - Allow adding new file extensions to existing extractors (e.g., .docx to text extractor)
  - Allow removing file extensions from extractors if causing issues
  - Command-line parameters: --add-file-type, --remove-file-type, --list-extractors
  - Menu system for extractor configuration and testing
  - Validation of file type compatibility with extractors
  - Test extraction capability before enabling new file types
  - Support for custom extractor mapping (extension → extractor type)
  - Real-time testing of file extraction in menu system
  - Backup and restore of extractor configurations
  - Integration with existing text, PDF, HTML, and CHM extractors

## Model Management
- [ ] Allow changing the AI model from the menu system - store in SQLite config DB, with unit test

## Database Management
- [ ] Allow managing vector.db through command line interface - with unit tests

## User Experience
- [ ] Add "thinking" cursor/indicator in interactive mode when processing - with unit tests
- [ ] Verify file analysis functionality is working correctly - with unit tests
- [ ] Refactor interactive mode menu system into organized categories with sub-menus - store navigation state in SQLite config DB, with unit tests
  - Create main menu categories for interactive mode: Configuration, File Management, Logging, Database, System
  - Configuration sub-menu: hh.exe path, prompt defaults, model selection, file types, extractors
  - File Management sub-menu: list files, export features, cleanup routines, file type filtering
  - Logging sub-menu: error logging config, log viewer, log statistics, log retention
  - Database sub-menu: vector.db management, configuration DB stats, backup/restore
  - System sub-menu: reset settings, delete databases, help, diagnostics
  - Breadcrumb navigation showing current menu path in interactive mode
  - Back/up navigation with keyboard shortcuts
  - Remember last visited menu section in SQLite config for interactive sessions
  - Consistent menu styling and numbering across all interactive sub-menus
  - Search functionality to quickly find menu options by keyword in interactive mode
  - Menu help system with descriptions for each option in interactive mode

## Status
Created: 2025-08-22
Last Updated: 2025-08-23

---
*This file tracks ongoing development tasks for the HlpAI project*
