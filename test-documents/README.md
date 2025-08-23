# Sample Markdown Document

This is a **sample Markdown file** that demonstrates the markdown processing capabilities of HlpAI.

## Features Tested

- **Bold text formatting**
- *Italic text formatting*
- `Code snippets`
- Lists and bullet points
- Headers and sections

## Code Example

```python
def extract_text(file_path):
    """Extract text from a markdown file."""
    with open(file_path, 'r', encoding='utf-8') as file:
        return file.read()
```

## Lists

### Ordered List
1. First item
2. Second item
3. Third item

### Unordered List
- Item A
- Item B
- Item C

## Table Example

| Feature | Status | Notes |
|---------|--------|-------|
| Text extraction | ✅ | Working |
| Markdown parsing | ✅ | Working |
| Code highlighting | ✅ | Working |

## Technical Details

- **File Type**: Markdown (.md)
- **Extractor**: TextFileExtractor
- **MIME Type**: text/plain
- **Encoding**: UTF-8

This file demonstrates how HlpAI can process structured markdown content while preserving the semantic meaning of the text.