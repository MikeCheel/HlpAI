# HlpAI Test Documents

This folder contains sample files representing all officially supported file types in HlpAI. Use these files to test the application's extraction capabilities.

## File Types Included

### 📄 Text Files (TextFileExtractor)
- **sample.txt** - Basic plain text file with various content types
- **README.md** - Markdown document with formatting, code blocks, and tables
- **application.log** - Sample application log file with timestamped entries
- **sample-data.csv** - CSV file with tabular data and headers

### 🌐 HTML Files (HtmlFileExtractor)  
- **index.html** - Modern HTML5 document with CSS and JavaScript (filtered out)
- **page.htm** - Legacy HTML document using .htm extension

### 📕 PDF Files (PdfFileExtractor)
- **Note**: PDF files cannot be created as text files. Create a real PDF document and place it here for testing.
- Expected: `document.pdf` - Sample PDF with text content

### 📚 Help Files
- **contents.hhc** - HTML Help Contents file (HhcFileExtractor) - Cross-platform support
- **Note**: CHM files are binary and cannot be created as text files. Create a real .chm file for testing.
- Expected: `help.chm` - Compiled HTML Help file (ChmFileExtractor) - Windows only

### 📎 Custom Extensions
- **report.docx.txt** - Placeholder for DOCX file (requires real .docx file for testing)
- **Note**: .docx was added as a custom extension to the TextFileExtractor

## Testing Instructions

### Quick Test Commands

```bash
# List all available extractors
dotnet run -- --list-extractors

# Show extractor statistics  
dotnet run -- --extractor-stats

# Test extraction on a specific file
dotnet run -- --test-extraction "C:\path\to\test-documents\sample.txt"
```

### Interactive Testing

```bash
# Run the application interactively
dotnet run

# Navigate to option 16 - File extractor management
# Use this folder as your test directory
```

### Audit Mode Testing

```bash
# Analyze all test files
dotnet run -- --audit "C:\path\to\test-documents"
```

## Expected Behavior

When processing these files, HlpAI should:

1. **Text Files**: Extract all content as-is
2. **HTML Files**: Remove `<script>` and `<style>` tags, extract visible text
3. **Log Files**: Process as plain text with timestamp information
4. **CSV Files**: Extract tabular data as structured text
5. **HHC Files**: Extract table of contents structure and navigation info

## Adding Your Own Test Files

Feel free to add additional files of supported types:
- `.txt`, `.md`, `.log`, `.csv` (text-based)
- `.html`, `.htm` (web documents)  
- `.pdf` (documents)
- `.chm` (Windows help files)
- `.hhc` (help contents)
- Custom extensions you've added via ExtractorManagementService

## File Creation Notes

- Binary files (.pdf, .chm, .docx) require appropriate software to create
- Text-based files can be created with any text editor
- HTML files should include various elements to test extraction thoroughly
- Log files should contain realistic timestamp and message formats

## Troubleshooting

If extraction fails on any test file:
1. Check file permissions
2. Verify file is not corrupted  
3. Ensure appropriate extractor is configured
4. Use `--test-extraction` command to get detailed error info