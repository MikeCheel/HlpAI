# HlpAI - Interactive Mode Documentation

> **Menu-driven interface for end users - Zero configuration required**

The Interactive Mode provides a guided, user-friendly interface perfect for individual users, researchers, and anyone who prefers visual configuration over command-line parameters. This mode offers step-by-step setup and comprehensive menu-based control over all HlpAI capabilities.

## 🎯 Overview

**Interactive Mode** is designed for:
- ✅ **First-time users** who want guided setup
- ✅ **Non-technical users** who prefer menus over command lines
- ✅ **Exploratory work** where you need to frequently change settings
- ✅ **Learning the system** before using advanced modes
- ✅ **Quick document analysis** without complex configuration

## 🚀 Getting Started

### Starting Interactive Mode

Simply run without any parameters:
```bash
dotnet run
```

### Interactive Setup Process

The setup guides you through four comprehensive steps:

#### **Step 1: Document Directory Selection**
```
🎯 HlpAI - Interactive Setup
=============================================

Welcome! Let's configure your document intelligence system.

📁 Step 1: Document Directory
------------------------------
Enter the path to your documents directory: C:\MyDocuments
✅ Using directory: C:\MyDocuments
```

**Features:**
- ✅ **Automatic validation**: Checks if directories exist
- ✅ **Directory creation**: Offers to create non-existent directories
- ✅ **Path flexibility**: Accepts any valid file path
- ✅ **Exit anytime**: Type 'quit' or 'exit' to cancel setup

#### **Step 2: AI Provider & Model Selection**
```
🤖 Step 2: AI Provider & Model Selection
----------------------------------------
✅ Available AI Providers:

  1. Ollama (http://localhost:11434) - ✅ Available
  2. LM Studio (http://localhost:1234) - ❌ Not available  
  3. Open WebUI (http://localhost:3000) - ✅ Available

Select a provider (1-3, or 'q' to quit): 1
✅ Selected provider: Ollama

🤖 Model Selection
------------------
✅ Ollama connected! Available models:

  1. llama3.2
  2. llama3.1
  3. codellama
  4. Enter custom model name

Select a model (1-4, or 'q' to quit): 1
✅ Selected model: llama3.2
```

**Smart Provider Detection:**
- 🔍 **Auto-discovery**: Automatically detects available AI providers
- 🎯 **Provider fallback**: Seamlessly switches to available providers
- 📊 **Provider status**: Shows availability status for each provider
- 🎯 **Custom models**: Option to specify models not yet installed
- 📥 **Model installation**: Provides installation commands for missing models

#### **Step 3: Operation Mode**
```
⚙️ Step 3: Operation Mode
-------------------------
Available modes:
  1. Hybrid (recommended) - Full MCP + RAG capabilities
  2. MCP - Model Context Protocol server only
  3. RAG - Retrieval-Augmented Generation only

Select operation mode (1-3, default: 1): 1
✅ Selected mode: Hybrid
```

**Mode Explanations:**
- **Hybrid** (Recommended): Full-featured mode with both MCP server and RAG capabilities
- **MCP**: Lightweight mode for Model Context Protocol integration only
- **RAG**: Focused mode for document search and AI enhancement only

#### **Step 4: Configuration Summary**
```
📋 Configuration Summary
========================
Directory: C:\MyDocuments
Provider: Ollama
Model: llama3.2
Mode: Hybrid

Continue with this configuration? (y/n): y
✅ Starting application with selected configuration...
```

## 📋 Comprehensive Command Reference

### 📁 File Operations

#### **Command 1: List All Available Files**
Lists all supported files in the current document directory with detailed information.

**Usage:**
```
Command: 1

📁 Files in C:\MyDocuments
==========================
1. 📄 user-manual.pdf (PDF, 2.1MB, indexed)
2. 📄 api-docs.html (HTML, 450KB, indexed) 
3. 📄 config.txt (TXT, 12KB, indexed)
4. 📄 security-guide.pdf (PDF, 3.4MB, indexed)
5. 📄 README.md (Markdown, 8KB, indexed)

Total: 156 files (89 indexable, 45 not indexable, 4 too large)
```

#### **Command 2: Read Specific File Content**
Reads and displays the content of a specific file.

**Usage:**
```
Command: 2
Enter file name or number: 2

📄 api-docs.html
================
Content:
API Documentation
=================

Endpoints:
- GET /api/users - List all users
- POST /api/users - Create new user
- GET /api/users/{id} - Get user by ID

[Content continues...]
```

#### **Command 3: Search Files by Text Content**
Performs text-based search across all files.

**Usage:**
```
Command: 3
Enter search query: authentication

🔍 Search Results for "authentication"
======================================
📄 security-guide.pdf (Page 12)
  User authentication is handled through JWT tokens...

📄 api-docs.html 
  Authentication endpoints: /auth/login, /auth/verify...

Found 8 matches across 3 files.
```

### 🤖 AI Features

#### **Command 4: Ask AI Questions**
Ask questions with full customization options.

**Usage:**
```
Command: 4
Enter your question: How do I configure the database connection?
Enter additional context (optional, press Enter to skip): 
Enter temperature (0.0-1.0, default 0.7): 0.3
Use RAG enhancement? (y/n): y

🤖 AI Response:
Based on your documentation, configure database connections by:
1. Edit the config.json file in your application root
2. Set the connection string in the "Database" section...
3. Ensure the database service is running on port 5432...

[Response continues with context from your actual documents]
```

**Customization Options:**
- **Additional Context**: Provide extra information to guide the AI
- **Temperature**: Control creativity (0.0 = factual, 1.0 = creative)
- **RAG Enhancement**: Use document context for more accurate responses

#### **Command 5: Analyze Specific Files**
AI-powered analysis of individual files with multiple analysis types.

**Usage:**
```
Command: 5
Enter file URI (e.g., file:///example.txt): file:///user-manual.pdf
Available analysis types: summary, key_points, questions, topics, technical, explanation
Enter analysis type (default: summary): summary
Enter temperature (0.0-1.0, default 0.7): 0.4
Use RAG enhancement? (y/n): y

📊 File Analysis: user-manual.pdf
==================================
This user manual provides comprehensive guidance for setting up and using 
the application. Key sections include installation requirements, configuration 
options, troubleshooting guide, and API reference...

Main Topics Covered:
• System requirements and installation process
• Configuration options and best practices
• User interface overview and navigation
• Troubleshooting common issues
• API reference and integration examples

Recommended Next Steps:
1. Review the installation section for hardware requirements
2. Configure application settings according to your environment
3. Test basic functionality before production deployment
```

**Available Analysis Types:**
- **summary**: Comprehensive overview of the document
- **key_points**: Main ideas and important information
- **questions**: Questions the document answers or raises
- **topics**: Main subjects and themes covered
- **technical**: Technical details and specifications
- **explanation**: Detailed explanations of concepts

### 🔍 RAG Features (Hybrid/RAG modes only)

#### **Command 6: Semantic Search**
Search using vector embeddings for meaning-based results.

**Usage:**
```
Command: 6
Query: authentication setup

🔍 Semantic Search Results
==========================
📄 security-guide.pdf (Similarity: 0.89)
   User authentication is handled through JWT tokens. Configure the auth 
   service by setting the secret key in your environment variables...

📄 api-docs.html (Similarity: 0.76)
   Authentication endpoints are available at /auth/login and /auth/verify...
   
📄 config-reference.txt (Similarity: 0.68)
   auth_secret_key: Required for JWT token generation. Set to a secure random value...

Found 12 relevant results (similarity > 0.5).
```

#### **Command 7: RAG-Enhanced AI Questioning**
Ask questions enhanced with document context.

**Usage:**
```
Command: 7
Enter your question: What are the security best practices?
Enter temperature (0.0-1.0, default 0.7): 0.2
Enter top K context chunks (default: 5): 3

🤖 RAG-Enhanced Response:
Based on your security documentation, here are the recommended best practices:

1. **Authentication**: Use JWT tokens with strong secret keys (minimum 256 bits)
2. **Authorization**: Implement role-based access control as described in section 4.2
3. **Encryption**: Enable TLS 1.3 for all communications, as specified in the network guide
4. **Audit Logging**: Configure comprehensive logging as outlined in the monitoring section

Specific recommendations from your documents:
• Rotate JWT secrets every 90 days (security-guide.pdf, page 8)
• Use environment variables for sensitive configuration (config-reference.txt)
• Implement rate limiting on authentication endpoints (api-docs.html)

[Response continues with specific document references]
```

#### **Command 8: Reindex Documents**
Rebuild the vector store index.

**Usage:**
```
Command: 8
Force reindex (process all files, not just changed ones)? (y/n): n

🔄 Reindexing Documents
=======================
Current index: 234 chunks from 45 files
Scanning for changes...
Found 12 modified files and 3 new files.

Processing files...
✅ security-guide.pdf - 34 chunks
✅ api-updates.html - 12 chunks  
✅ new-config.txt - 8 chunks

✅ Reindexing complete!
New index: 256 chunks from 48 files (added 22 chunks from 15 files)
Time: 00:00:18.45
```

### 🛠️ System Management

#### **Command 9: Show Available AI Models**
Display models available from the current provider.

**Usage:**
```
Command: 9

🤖 Available Models (Ollama)
============================
1. llama3.2 - 4.1B parameters, general purpose
2. llama3.1 - 8B parameters, enhanced capabilities  
3. codellama - 7B parameters, code generation
4. nomic-embed-text - 137M parameters, embeddings

Current model: llama3.2
Embedding model: nomic-embed-text
```

#### **Command 10: Display System Status**
Show current configuration and system status.

**Usage:**
```
Command: 10

📊 System Status
================
📁 Document Directory: C:\MyDocuments
🤖 AI Provider: Ollama (http://localhost:11434)
🧠 Model: llama3.2
🎯 Operation Mode: Hybrid
🗄️ Vector Store: SQLite (C:\MyDocuments\.hlpai\vectors.db)

📈 Performance Metrics:
• Files Processed: 156 total, 89 indexable
• Vector Chunks: 234 chunks, 1.2MB total
• Index Size: 3.4MB on disk
• Last Index: 2024-01-15 14:30:22

🔧 System Health:
• AI Connection: ✅ Healthy
• Vector Store: ✅ Healthy  
• Memory Usage: 128MB / 2GB available
• Uptime: 00:45:22
```

#### **Command 11: Show Comprehensive Indexing Report**
Detailed analysis of indexed vs. non-indexed files.

**Usage:**
```
Command: 11

📋 Indexing Report - C:\MyDocuments
===================================
📊 FILE STATISTICS
Total Files: 156
✅ Indexable: 89 (57.1%)
❌ Not Indexable: 45 (28.8%)
📦 Too Large: 4 (2.6%)
🔒 Password Protected: 3 (1.9%)

📈 BY FILE TYPE
✅ .pdf: 34 files (34 indexable, 0 skipped)
✅ .txt: 28 files (28 indexable, 0 skipped)
✅ .html: 18 files (18 indexable, 0 skipped)
❌ .jpg: 23 files (0 indexable, 23 skipped - unsupported)
❌ .exe: 8 files (0 indexable, 8 skipped - executable)

💡 RECOMMENDATIONS
• Consider adding support for .docx files (8 files found)
• Consider excluding 12 temporary/log files to improve speed
• Consider splitting or excluding 4 large files (>100MB)
```

#### **Command 12: Run as MCP Server**
Switch to MCP server mode for external integration.

**Usage:**
```
Command: 12

🖥️  MCP Server Mode
===================
The application is now running as an MCP (Model Context Protocol) server.
You can interact with it programmatically using MCP requests.

📋 Available MCP Methods:
  • resources/list     - List all available document resources
  • resources/read     - Read content of a specific document
  • tools/list         - List all available AI tools
  • tools/call         - Execute an AI tool

🛠️ Available Tools:
  • search_files       - Search files by text content
  • ask_ai             - Ask AI questions with optional RAG
  • analyze_file       - AI-powered file analysis
  • rag_search         - Semantic search using vectors
  • rag_ask            - RAG-enhanced AI questioning
  • reindex_documents  - Rebuild vector index
  • indexing_report    - Get indexing status report

🎯 Server is ready! Press any key to return to interactive mode...
```

#### **Command 13: Change Document Directory**
Switch to a different document directory.

**Usage:**
```
Command: 13

📁 Change Document Directory
=============================
Current directory: C:\YourDocuments

Enter new document directory path (or 'cancel' to abort): C:\NewDocuments
📊 Found 89 supported files in the new directory.

🔄 Switching to new directory...
⚠️  This will dispose the current server and create a new one.
✅ Ollama connected! Available models: llama3.2, nomic-embed-text
Using model: llama3.2

Operation Mode: Hybrid
Initializing RAG system...
✅ RAG initialization complete. Indexed 156 chunks from 89 files.

✅ Successfully switched to directory: C:\NewDocuments
📱 Returning to main menu...
```

1. Change AI Provider (Current: Ollama)
2. Change AI Model (Current: llama3.2)
3. Change Operation Mode (Current: Hybrid)
4. View Current Configuration
5. Reset to Default Settings
b. Back to main menu

Enter your choice (1-5, b): 1

🤖 Change AI Provider
✅ Available AI Providers:
  1. Ollama (http://localhost:11434) - ✅ Available
  2. LM Studio (http://localhost:1234) - ❌ Not available
  3. Open WebUI (http://localhost:3000) - ✅ Available

Select provider (1-3): 3
✅ Selected provider: Open WebUI
✅ Configuration updated successfully!
```
#### **Command 14: Configuration Settings**
Access and modify application settings including AI provider switching.

**Usage:**
```
Command: 14

⚙️ Configuration Settings
1. Change AI Provider (Current: Ollama)
2. Change AI Model (Current: llama3.2)
3. Change Operation Mode (Current: Hybrid)
4. View Current Configuration
5. Reset to Default Settings
b. Back to main menu

Enter your choice (1-5, b): 1

🤖 Change AI Provider
✅ Available AI Providers:
  1. Ollama (http://localhost:11434) - ✅ Available
  2. LM Studio (http://localhost:1234) - ❌ Not available
  3. Open WebUI (http://localhost:3000) - ✅ Available

Select provider (1-3): 3
✅ Selected provider: Open WebUI
✅ Configuration updated successfully!
```

**Provider Switching Features:**
- 🔄 **Hot-swappable**: Change providers without restarting the application
- 🔍 **Auto-detection**: Automatically detects available providers and their status
- ⚡ **Instant switching**: Changes take effect immediately
- 📊 **Provider status**: Shows availability and connection status for each provider
- 🎯 **Model compatibility**: Automatically handles model compatibility between providers
- 💾 **Configuration persistence**: Provider changes are saved and restored on restart

**Supported AI Providers:**
- **Ollama** (default): Local AI models running via Ollama (http://localhost:11434)
- **LM Studio**: Local models via LM Studio API (http://localhost:1234)
- **Open WebUI**: Web-based AI interface (http://localhost:3000)

**Switching Process:**
1. Select "Change AI Provider" from configuration menu
2. View available providers with their connection status
3. Select the desired provider
4. System automatically validates the new provider connection
5. Configuration is updated and saved immediately
6. All subsequent AI operations use the new provider

**Provider Requirements:**
- **Ollama**: Must have Ollama installed and running with desired models
- **LM Studio**: Must have LM Studio running with API enabled
- **Open WebUI**: Must have Open WebUI instance accessible

**Note**: Some providers may have different model capabilities. The system will automatically handle model compatibility and may suggest appropriate models when switching providers.
=========================
1. Change AI Provider (Current: Ollama)
2. Change AI Model (Current: llama3.2)
3. Change Operation Mode (Current: Hybrid)
4. View Current Configuration
5. Reset to Default Settings
b. Back to main menu

Enter your choice (1-5, b): 1

🤖 Change AI Provider
=====================
✅ Available AI Providers:
  1. Ollama (http://localhost:11434) - ✅ Available
  2. LM Studio (http://localhost:1234) - ❌ Not available
  3. Open WebUI (http://localhost:3000) - ✅ Available

Select provider (1-3): 3
✅ Selected provider: Open WebUI
✅ Configuration updated successfully!
```

#### **Command 15: View Error Logs**
Display application error logs.

**Usage:**
```
Command: 15

📋 Error Logs
=============
🕒 2024-01-15 14:22:18 - WARNING: File too large: presentation.pptx (215MB)
🕒 2024-01-15 14:18:45 - INFO: Reindex completed successfully
🕒 2024-01-15 14:05:12 - ERROR: PDF extraction failed: encrypted-document.pdf
🕒 2024-01-15 13:58:30 - INFO: AI provider switched to Ollama

Total errors: 2, Warnings: 1, Info: 15
Press any key to clear logs...
```

#### **Command 16: File Extractor Management**
Manage file extractors and supported extensions.

**Usage:**
```
Command: 16

🔧 File Extractor Management
============================

Extractor Management Options:
1. List all extractors and their supported file types
2. View extractor statistics  
3. Add file extension to an extractor
4. Remove file extension from an extractor
5. Test file extraction
6. Reset extractor to default configuration
7. View configuration audit
b. Back to main menu
q. Quit application

Enter your choice (1-7, b, q): 1

📦 Available File Extractors:

🔧 Text File Extractor (text)
   Type: TextFileExtractor
   MIME Type: text/plain
   Description: Extracts plain text content from text-based files
   Extensions: .txt, .md, .log, .csv, .docx
   ⚡ Custom extensions added: 1
   📎 Custom: .docx

🔧 HTML File Extractor (html)
   Type: HtmlFileExtractor
   MIME Type: text/html
   Description: Extracts text content from HTML files
   Extensions: .html, .htm

🔧 PDF File Extractor (pdf)
   Type: PdfFileExtractor
   MIME Type: application/pdf
   Description: Extracts text content from PDF documents
   Extensions: .pdf

🔧 HHC File Extractor (hhc)
   Type: HhcFileExtractor
   MIME Type: text/html
   Description: Extracts content from HTML Help Contents files
   Extensions: .hhc

🔧 CHM File Extractor (chm)
   Type: ChmFileExtractor
   MIME Type: application/octet-stream
   Description: Extracts content from Windows Help files (CHM)
   Extensions: .chm
   ⚠️ Platform: Windows only (requires hh.exe)

**Key Features:**
- ✅ **Dynamic extension management**: Add `.docx`, `.rtf`, `.xml`, or any text-based extension
- ✅ **Real-time testing**: Test extraction on specific files before committing changes
- ✅ **Configuration persistence**: All changes automatically saved to database
- ✅ **Statistics and auditing**: Comprehensive view of all extractor configurations
- ✅ **Reset functionality**: Restore any extractor to its original default state
- ✅ **Conflict detection**: Prevents duplicate extension assignments

### 🔧 Utility Commands

#### **Command: c - Clear Screen**
Clear the console screen for better readability.

**Usage:**
```
Command: c
✅ Screen cleared.
```

#### **Command: m - Show Menu**
Display the main menu with all available commands.

**Usage:**
```
Command: m

🎯 HlpAI - Main Menu
📁 File Operations:
  1. List all available files
  2. Read specific file content
  3. Search files by text content

🤖 AI Features:
  4. Ask AI questions with full customization
  5. Analyze specific files with AI

🔍 RAG Features (Hybrid/RAG modes):
  6. Semantic search using vector embeddings
  7. RAG-enhanced AI questioning
  8. Reindex documents

🛠️ System Management:
  9. Show available AI models
  10. Display system status
  11. Show comprehensive indexing report
  12. Run as MCP server
  13. Change document directory
  14. Configuration settings
  15. View error logs
  16. File extractor management

🔧 Utility Commands:
  c. Clear screen
  m. Show menu
  q. Quit application

Enter command (1-16, c, m, q):
```

#### **Command: q - Quit Application**
Exit the application gracefully.

**Usage:**
```
Command: q
👋 Thank you for using HlpAI. Goodbye!
```

## 💡 Best Practices & Tips

### 🎯 Interactive Mode Best Practices

**For New Users:**
- Start with Interactive Mode to learn available options
- Use the default Hybrid mode for full functionality
- Let the system detect your installed providers and models
- Test with the included `test-documents/` folder first

**For Experienced Users:**
- Interactive Mode is still valuable for discovering new providers
- Perfect when switching between different document collections
- Useful for validating directory contents before indexing

**Directory Best Practices:**
- Choose directories with supported file types (.txt, .md, .pdf, .html, etc.)
- Avoid system directories or overly large document collections initially
- Test with smaller directories first to understand performance

### 🔄 Workflow Examples

**Quick Document Analysis:**
```
1. dotnet run
2. Select document directory
3. Choose AI provider and model
4. Use Command 1 to list files
5. Use Command 5 to analyze specific files
6. Use Command 4 to ask questions about content
```

**Research Workflow:**
```
1. Start with Command 11 to see what's indexed
2. Use Command 6 for semantic search on topics
3. Use Command 7 for RAG-enhanced questions
4. Use Command 2 to read specific documents
5. Use Command 5 for AI analysis of key documents
```

**Configuration Testing:**
```
1. Use Command 14 to switch between providers
2. Test different models with Command 9
3. Compare RAG vs non-RAG responses with Command 4
4. Use Command 16 to add custom file extensions
```

## 🚨 Troubleshooting

### Common Interactive Mode Issues

**Setup Cancelled or Failed:**
```
❌ Setup cancelled. Exiting.
```

**Solutions:**
1. Ensure you answer prompts completely
2. Use 'quit' or 'exit' to intentionally cancel setup
3. Check directory permissions if directory creation fails
4. Verify at least one AI provider is available

**Directory Validation Issues:**
```bash
# Use absolute paths
✅ C:\MyDocuments          (Windows)
✅ /home/user/Documents     (Linux)
✅ /Users/user/Documents    (macOS)

# Avoid relative paths in interactive mode
❌ ./docs
❌ ../MyDocuments
```

**Performance Tips:**
- Use Command 11 to identify large or unsupported files
- Consider excluding temporary files to improve speed
- Use incremental indexing (Command 8 without force) for better performance

## 📊 Performance Metrics

Interactive Mode provides comprehensive performance monitoring:

**Typical Performance:**
- **Indexing**: 50-100 files per minute (depends on file size and type)
- **Search**: <1 second for most queries
- **AI Responses**: 2-10 seconds depending on model and complexity
- **Memory Usage**: 100-500MB typical, scales with document collection size

**Optimization Tips:**
- Use SSD storage for better indexing performance
- Keep document collections under 10,000 files for optimal performance
- Use Command 11 to identify and exclude unnecessary files

## 🎯 Next Steps

After mastering Interactive Mode, consider exploring:
- **[MCP Server Mode](README-MCP.md)** - For integration with external tools
- **[Library Mode](README-LIBRARY.md)** - For .NET application integration
- **Advanced workflows** combining multiple commands for complex tasks

---

**Interactive Mode provides the perfect starting point for exploring HlpAI's capabilities with zero configuration required and comprehensive menu-driven control over all features.**
