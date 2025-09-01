# HlpAI - MCP Server Mode Documentation

> **Model Context Protocol server for external integration and automation**

The MCP (Model Context Protocol) Server Mode enables HlpAI to run as a service that can be integrated with external tools, applications, and AI assistants. This mode provides programmatic access to all HlpAI capabilities through a standardized protocol, making it ideal for automation, integration, and building document intelligence into other systems.

## 🎯 Overview

**MCP Server Mode** is designed for:
- ✅ **Integration with AI tools** like Claude Desktop, Cursor, and other MCP-compatible applications
- ✅ **Automation workflows** where document processing needs to be scripted or scheduled
- ✅ **Microservices architecture** where HlpAI capabilities are exposed as APIs
- ✅ **Custom applications** that need to leverage document intelligence programmatically
- ✅ **Headless operation** where no user interface is required

## 🚀 Getting Started

### Starting MCP Server Mode

#### From Interactive Mode
```bash
# Start interactive mode first
dotnet run

# Then use Command 12 to switch to MCP server mode
Command: 12
```

#### Direct Command Line Startup
```bash
# Start directly in MCP server mode with parameters
dotnet run "C:\YourDocuments" "llama3.2" "mcp"

# Or let the app prompt for model selection
dotnet run "C:\YourDocuments" "mcp"
```

#### Configuration Parameters
```bash
# Full parameter format
dotnet run <document-directory> <model-name> <operation-mode>

# Examples:
dotnet run "C:\Docs" "llama3.2" "mcp"
dotnet run "/home/user/documents" "codellama" "mcp"
dotnet run "~/Documents" "mcp"  # Prompts for model selection
```

#### AI Provider Configuration

**Timeout Settings:**
Each AI provider has configurable timeout settings (default: 5 minutes):
- **OpenAI**: `OpenAiTimeoutMinutes`
- **Anthropic**: `AnthropicTimeoutMinutes`
- **DeepSeek**: `DeepSeekTimeoutMinutes`
- **LM Studio**: `LmStudioTimeoutMinutes`
- **OpenWebUI**: `OpenWebUiTimeoutMinutes`

**Max Tokens Settings:**
Token limits can be configured per provider:
- **OpenAI**: `OpenAiMaxTokens` (default: 4000)
- **Anthropic**: `AnthropicMaxTokens` (default: 4000)
- **DeepSeek**: `DeepSeekMaxTokens` (default: 4000)
- **LM Studio**: `LmStudioMaxTokens` (default: 4096)
- **OpenWebUI**: `OpenWebUiMaxTokens` (default: 4096)

**Configuration Methods:**
1. **Interactive Mode**: Use the provider switching menu to configure settings
2. **Command Line**: Pass configuration arguments during startup
3. **Configuration File**: Settings are persisted in SQLite database
4. **Environment Variables**: Can be set via system environment

**Example Configuration:**
```bash
# Set custom timeout and token limits
dotnet run "C:\Docs" "gpt-4" "mcp" --openai-timeout 10 --openai-max-tokens 8000
```

**Configuration Validation:**
- Timeout values must be positive integers (minutes)
- Max tokens must be within provider limits
- Invalid configurations will use default values
- Settings are validated during provider switching

### Server Initialization

**Typical Startup Output:**
```
🎯 HlpAI - MCP Server Mode
==========================
📁 Document Directory: C:\YourDocuments
🤖 AI Provider: Ollama (http://localhost:11434)
🧠 Model: llama3.2
🎯 Operation Mode: MCP Server

Initializing document processing...
Found 156 files to process
✅ RAG initialization complete. Indexed 234 chunks from 45 files.

🖥️  MCP Server Ready
====================
Server is now running and ready for MCP requests.
Available on stdio for MCP protocol communication.

📋 Available MCP Methods:
  • resources/list     - List all available document resources
  • resources/read     - Read content of a specific document
  • tools/list         - List all available AI tools
  • tools/call         - Execute an AI tool

🛠️ Available Tools:
  • search_files       - Search files by text content
  • ask_ai             - Ask AI questions with optional RAG
  • analyze_file       - AI-powered file analysis
  • rag_search         - Semantic search using vectors (RAG/Hybrid modes)
  • rag_ask            - RAG-enhanced AI questioning (RAG/Hybrid modes)
  • reindex_documents  - Rebuild vector index (RAG/Hybrid modes)
  • indexing_report    - Get indexing status report (RAG/Hybrid modes)

✅ Server initialized successfully and ready for requests.
```

## 📋 MCP Protocol Implementation

### Supported MCP Methods

#### **resources/list**
List all available document resources in the current directory.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": "1",
  "method": "resources/list"
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "1",
  "result": {
    "resources": [
      {
        "file_uri": "file:///user-manual.pdf",
        "name": "user-manual.pdf",
        "description": "PDF document - User Manual",
        "type": "document",
        "size": 2150000,
        "indexed": true
      },
      {
        "uri": "file:///api-docs.html", 
        "name": "api-docs.html",
        "description": "HTML document - API Documentation",
        "type": "document",
        "size": 450000,
        "indexed": true
      }
    ]
  }
}
```

#### **resources/read**
Read content of a specific document.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": "2",
  "method": "resources/read",
  "params": {
    "uri": "file:///user-manual.pdf"
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "2",
  "result": {
    "uri": "file:///user-manual.pdf",
    "name": "user-manual.pdf",
    "content": "User Manual\n===========\n\nThis document provides comprehensive guidance for setting up and using the application...",
    "mimeType": "text/plain",
    "size": 2150000
  }
}
```

#### **tools/list**
List all available AI tools.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": "3",
  "method": "tools/list"
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "3",
  "result": {
    "tools": [
      {
        "name": "search_files",
        "description": "Search for files containing specific text",
        "inputSchema": {
          "type": "object",
          "properties": {
            "query": {"type": "string", "description": "Search query"},
            "fileTypes": {"type": "array", "items": {"type": "string"}, "description": "File extensions to search"}
          },
          "required": ["query"]
        }
      },
      {
        "name": "ask_ai",
        "description": "Ask AI a question about file contents using the configured AI provider",
        "inputSchema": {
          "type": "object",
          "properties": {
            "question": {"type": "string", "description": "Question to ask the AI"},
            "context": {"type": "string", "description": "Optional context or file content to provide to the AI"},
            "temperature": {"type": "number", "description": "Temperature for AI response (0.0-1.0)", "default": 0.7},
            "use_rag": {"type": "boolean", "description": "Whether to use RAG for context retrieval", "default": true}
          },
          "required": ["question"]
        }
      },
      {
         "name": "analyze_file",
         "description": "Analyze a specific file using AI",
         "inputSchema": {
           "type": "object",
           "properties": {
             "file_uri": {"type": "string", "description": "URI of the file to analyze"},
             "analysis_type": {"type": "string", "description": "Type of analysis (summary, key_points, questions, etc.)"},
             "use_rag": {"type": "boolean", "description": "Whether to use RAG for enhanced context", "default": true}
           },
           "required": ["file_uri", "analysis_type"]
         }
       }
    ]
  }
}
```

**Note:** In RAG or Hybrid modes, additional tools are available:
- `rag_search` - Semantic search using RAG vector store
- `rag_ask` - Ask AI with RAG-enhanced context retrieval  
- `reindex_documents` - Rebuild the RAG vector store index
- `indexing_report` - Get detailed report of indexed and non-indexed files

#### **tools/call**
Execute an AI tool with specific parameters.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": "4",
  "method": "tools/call",
  "params": {
    "name": "ask_ai",
    "arguments": {
      "question": "How do I configure the database?",
      "temperature": 0.3,
      "use_rag": true
    }
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "4",
  "result": {
    "content": "Based on your documentation, configure the database by:\n1. Edit the config.json file...\n2. Set the connection string...\n3. Ensure the database service is running...",
    "tool": "ask_ai",
    "success": true
  }
}
```

**Error Handling:**
```json
{
  "jsonrpc": "2.0",
  "error": {
    "code": -32602,
    "message": "Invalid params",
    "data": "File URI is required"
  }
}
```

**Common Error Scenarios:**
- Missing required `file_uri` parameter
- Invalid file URI format (must start with "file:///")
- File not found or inaccessible
- Unsupported file type
- File extraction failure (corrupted file, permission issues)
- Invalid `analysis_type` (not in supported list)
- Invalid `temperature` value (outside 0.0-2.0 range)
- AI provider timeout or unavailability
- RAG enhancement unavailable when requested

## 🛠️ Available Tools Reference

### **search_files** - Text-based File Search
Search files by text content with configurable result limits.

**Parameters:**
- `query` (required): Search query text
- `maxResults` (optional): Maximum number of results to return (default: 10)

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "search_files",
    "arguments": {
      "query": "authentication setup",
      "maxResults": 5
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "results": [
      {
        "file": "security-guide.pdf",
        "matches": 3,
        "snippet": "User authentication is handled through JWT tokens...",
        "page": 12
      },
      {
        "file": "api-docs.html",
        "matches": 2, 
        "snippet": "Authentication endpoints: /auth/login, /auth/verify...",
        "line": 45
      }
    ],
    "totalMatches": 8,
    "filesSearched": 156
  }
}
```

### **search_files**
Search for files containing specific text.

**Parameters:**
- `query` (string, required): Search query
- `fileTypes` (array, optional): File extensions to search

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "id": "11",
  "method": "tools/call",
  "params": {
    "name": "search_files",
    "arguments": {
      "query": "authentication",
      "fileTypes": [".md", ".txt", ".cs"]
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "11",
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Found 3 files containing 'authentication':\n\n1. docs/security.md - Line 15: 'User authentication is required'\n2. src/Auth.cs - Line 42: 'public class AuthenticationService'\n3. README.md - Line 8: 'Authentication setup instructions'"
      }
    ]
  }
}
```

### **ask_ai** - AI-Powered Question Answering
Ask AI questions about file contents using the configured AI provider with optional RAG enhancement.

**Parameters:**
- `question` (required): Question to ask the AI (string)
- `context` (optional): Additional context or file content to provide to the AI (string)
- `temperature` (optional): Controls response creativity and randomness (number, 0.0-2.0, default: 0.7)
  - `0.0-0.3`: Factual, deterministic responses
  - `0.4-0.7`: Balanced creativity and accuracy
  - `0.8-2.0`: More creative and varied responses
- `use_rag` (optional): Enable RAG for enhanced context retrieval from indexed documents (boolean, default: true)

**Validation Rules:**
- `question`: Must be non-empty string
- `temperature`: Must be between 0.0 and 2.0
- `context`: Optional, no length restrictions
- `use_rag`: Boolean value, automatically enabled in RAG/Hybrid modes

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "ask_ai",
    "arguments": {
      "question": "What are the security best practices?",
      "context": "Focus on authentication and authorization",
      "temperature": 0.2,
      "use_rag": true
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "answer": "Based on your security documentation, here are the recommended best practices:\n\n1. **Authentication**: Use JWT tokens with strong secret keys (minimum 256 bits)\n2. **Authorization**: Implement role-based access control...\n3. **Encryption**: Enable TLS 1.3 for all communications...\n\nSpecific recommendations from your documents:\n• Rotate JWT secrets every 90 days (security-guide.pdf)\n• Use environment variables for sensitive configuration...",
    "sources": [
      "security-guide.pdf",
      "config-reference.txt"
    ],
    "temperature": 0.2,
    "ragEnhanced": true
  }
}
```

**Error Handling:**
```json
{
  "jsonrpc": "2.0",
  "error": {
    "code": -32602,
    "message": "Invalid params",
    "data": "Question is required"
  }
}
```

**Common Error Scenarios:**
- Missing required `question` parameter
- Invalid `temperature` value (outside 0.0-2.0 range)
- AI provider timeout or unavailability
- RAG index not available when `use_rag` is true
- Network connectivity issues with AI provider

## 🧠 RAG-Enhanced Questioning Features

RAG (Retrieval-Augmented Generation) enhances AI responses by incorporating relevant context from your indexed documents. This section covers the comprehensive RAG capabilities available in HlpAI.

### **RAG Architecture Overview**

**Components:**
1. **Vector Store**: Stores document embeddings for semantic search
2. **Embedding Model**: Converts text to high-dimensional vectors
3. **Similarity Engine**: Performs cosine similarity calculations
4. **Context Retrieval**: Finds and ranks relevant document chunks
5. **AI Integration**: Combines context with user queries for enhanced responses

**RAG Workflow:**
```
User Query → Vector Embedding → Semantic Search → Context Retrieval → AI Enhancement → Response
```

### **RAG-Enhanced Tools Integration**

**1. Direct RAG Search (`rag_search`)**
- Pure semantic search without AI generation
- Returns ranked document chunks with similarity scores
- Ideal for finding specific information or exploring content
- Supports file filtering and similarity thresholds

**2. RAG-Enhanced AI Questioning (`rag_ask`)**
- Combines semantic search with AI generation
- Automatically retrieves relevant context for questions
- Provides AI-generated answers enhanced with document knowledge
- Best for getting comprehensive, contextual answers

**3. AI Tools with RAG Enhancement (`ask_ai`, `analyze_file`)**
- Optional RAG enhancement via `use_rag` parameter
- Maintains tool functionality while adding document context
- Seamless integration with existing workflows
- Flexible RAG activation based on needs

### **RAG Configuration & Optimization**

**Key Parameters:**
- `top_k`: Controls amount of context retrieved (1-50 for search, 1-20 for questioning)
- `min_similarity`: Filters context by relevance threshold (0.0-1.0)
- `temperature`: Balances creativity vs. factual accuracy in AI responses
- `file_filters`: Restricts search to specific file types or patterns

**Performance Tuning:**
- **High Precision**: `min_similarity=0.7+`, `top_k=3-5`
- **Balanced Retrieval**: `min_similarity=0.5-0.7`, `top_k=5-8`
- **Broad Context**: `min_similarity=0.3-0.5`, `top_k=8-15`
- **Exploratory Search**: `min_similarity=0.1-0.3`, `top_k=10-20`

### **RAG Best Practices**

**Query Optimization:**
- Use specific, well-formed questions for better context retrieval
- Include domain-specific terminology when available
- Combine multiple concepts for richer context (e.g., "SSL certificate installation process")
- Avoid overly broad queries that may retrieve irrelevant context

**Context Management:**
- Monitor similarity scores to assess context relevance
- Adjust `min_similarity` based on document quality and query specificity
- Use `file_filters` to focus on relevant document types
- Balance `top_k` to avoid context overload while ensuring completeness

**Response Quality:**
- Lower `temperature` (0.1-0.3) for factual, technical questions
- Higher `temperature` (0.7-1.0) for creative or analytical responses
- Use RAG enhancement for domain-specific questions
- Disable RAG for general knowledge or creative writing tasks

### **RAG Limitations & Considerations**

**Technical Limitations:**
- Context window limits may truncate large document chunks
- Embedding quality depends on document content and structure
- Semantic search may miss exact keyword matches in some cases
- Performance scales with document collection size

**Content Considerations:**
- RAG works best with well-structured, informative documents
- Poor quality or fragmented documents may reduce effectiveness
- Regular reindexing recommended for frequently updated documents
- File format limitations apply (see supported file types)

**Usage Guidelines:**
- Verify document indexing status before relying on RAG
- Monitor response quality and adjust parameters as needed
- Consider document freshness for time-sensitive information
- Use error handling for RAG unavailability scenarios

### **analyze_file** - AI-Powered File Analysis
Analyze specific files using AI with multiple analysis types and optional RAG enhancement.

**Parameters:**
- `file_uri` (required): URI of the file to analyze (string, format: "file:///path/to/file")
- `analysis_type` (optional): Type of analysis to perform (string, default: "summary")
- `temperature` (optional): Controls response creativity and randomness (number, 0.0-2.0, default: 0.7)
- `use_rag` (optional): Enable RAG for enhanced context from related documents (boolean, default: true)

**Supported Analysis Types:**
- `summary`: Comprehensive overview of file content
- `key_points`: Extract main points and important information
- `questions`: Generate relevant questions based on content
- `topics`: Identify and categorize main topics
- `technical`: Focus on technical details and specifications
- `explanation`: Provide detailed explanations of complex concepts

**Supported File Types:**
- Text files: `.txt`, `.md`, `.log`, `.csv`, `.docx`
- HTML files: `.html`, `.htm`
- PDF files: `.pdf`
- Help files: `.hhc`, `.chm` (Windows only)

**Validation Rules:**
- `file_uri`: Must be valid URI format starting with "file:///"
- `analysis_type`: Must be one of the supported analysis types
- `temperature`: Must be between 0.0 and 2.0
- File must exist and be readable
- File type must be supported by configured extractors

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "analyze_file",
    "arguments": {
      "file_uri": "file:///user-manual.pdf",
      "analysis_type": "key_points",
      "temperature": 0.4,
      "use_rag": true
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "analysis": {
      "type": "key_points",
      "file": "user-manual.pdf",
      "keyPoints": [
        "System requirements: 8GB RAM minimum, 16GB recommended",
        "Installation process requires administrative privileges",
        "Configuration should be done through config.json file",
        "Database connection string must be set in environment variables",
        "API endpoints require authentication tokens"
      ],
      "summary": "Comprehensive user manual covering installation, configuration, and usage with emphasis on security and performance best practices.",
      "recommendations": [
        "Review hardware requirements before installation",
        "Backup configuration before making changes",
        "Test in development environment before production deployment"
      ]
    },
    "success": true
  }
}
```

### **rag_search** - Semantic Vector Search (RAG/Hybrid modes)
Perform semantic search using vector embeddings to find contextually relevant content based on meaning rather than exact keyword matches.

**Parameters:**
- `query` (required): Search query or question
- `top_k` (optional): Maximum number of results to return (default: 5, range: 1-50)
- `min_similarity` (optional): Minimum cosine similarity threshold (0.0-1.0, default: 0.5)
- `file_filters` (optional): Array of file extensions or patterns to filter results

**Validation Rules:**
- `query`: Must be non-empty string, maximum 1000 characters
- `top_k`: Integer between 1 and 50
- `min_similarity`: Float between 0.0 and 1.0 (higher values = more strict matching)
- `file_filters`: Array of strings (e.g., [".pdf", ".txt", "*security*"])

**Semantic Search Features:**
- **Contextual Understanding**: Finds content based on meaning, not just keywords
- **Multi-language Support**: Works with documents in different languages
- **Fuzzy Matching**: Handles typos and variations in terminology
- **Concept Mapping**: Connects related concepts (e.g., "login" matches "authentication")
- **Ranking by Relevance**: Results sorted by semantic similarity scores

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "rag_search",
    "arguments": {
      "query": "authentication setup",
      "top_k": 3,
      "min_similarity": 0.6,
      "file_filters": [".pdf", ".md"]
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "content": [{
      "type": "text",
      "text": "Found 3 relevant chunks (similarity ≥ 0.6):\n\n**[From security-guide.pdf - Similarity: 0.89]**\nUser authentication is handled through JWT tokens. Configure the auth service by setting the secret key in your environment variables...\n\n**[From api-docs.md - Similarity: 0.76]**\nAuthentication endpoints are available at /auth/login and /auth/verify. These endpoints require valid API keys...\n\n**[From setup-guide.pdf - Similarity: 0.68]**\nInitial authentication setup requires creating an admin user account. Use the following command to create the first user..."
    }]
  }
}
```

**Search Tips:**
- **Broad Queries**: Use general terms for exploratory search (e.g., "security")
- **Specific Queries**: Use detailed questions for targeted results (e.g., "How to configure SSL certificates?")
- **Concept Search**: Search for concepts rather than exact phrases (e.g., "user management" vs "add user")
- **Multi-term Queries**: Combine related terms for better context (e.g., "database backup restore")

**Similarity Score Interpretation:**
- **0.9-1.0**: Highly relevant, direct matches
- **0.7-0.9**: Very relevant, strong semantic connection
- **0.5-0.7**: Moderately relevant, related concepts
- **0.3-0.5**: Loosely related, may contain useful context
- **0.0-0.3**: Weakly related, likely not useful

### **rag_ask** - RAG-Enhanced AI Questioning (RAG/Hybrid modes)
Ask questions enhanced with document context using semantic search and vector embeddings.

**Parameters:**
- `question` (required): Question to ask the AI
- `top_k` (optional): Number of context chunks to retrieve (default: 5, range: 1-20)
- `temperature` (optional): AI creativity level (0.0-2.0, default: 0.7)
- `min_similarity` (optional): Minimum similarity threshold for context chunks (0.0-1.0, default: 0.1)

**Validation Rules:**
- `question`: Must be non-empty string, maximum 2000 characters
- `top_k`: Integer between 1 and 20
- `temperature`: Float between 0.0 and 2.0 (0.0 = deterministic, 2.0 = highly creative)
- `min_similarity`: Float between 0.0 and 1.0 (higher values = more relevant context)

**How RAG Enhancement Works:**
1. **Query Processing**: The question is converted to vector embeddings
2. **Context Retrieval**: Semantic search finds the most relevant document chunks
3. **Context Ranking**: Results are ranked by cosine similarity scores
4. **Context Integration**: Top-K chunks are combined with the original question
5. **AI Generation**: Enhanced prompt is sent to the AI provider for response

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "rag_ask",
    "arguments": {
      "question": "How do I set up SSL certificates?",
      "top_k": 3,
      "temperature": 0.3,
      "min_similarity": 0.6
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "content": [{
      "type": "text",
      "text": "RAG-Enhanced Response (using 3 context chunks):\n\nBased on your security documentation, SSL certificate setup involves:\n\n1. **Certificate Generation**: Use OpenSSL to generate certificates as described in the security guide (page 8)\n2. **Configuration**: Set the certificate paths in config.json under the 'Security' section\n3. **Verification**: Use the provided test script to verify certificate validity\n\nSpecific steps from your documents:\n• Certificate files should be placed in /etc/ssl/certs/ (security-guide.pdf)\n• Private key must have 600 permissions (config-reference.txt)\n• Certificate chain must include intermediate certificates (api-docs.html)"
    }]
  }
}
```

**Context Sources Information:**
The response includes context from retrieved document chunks, formatted as:
- `[From filename - Similarity: 0.XXX]` headers indicate source and relevance
- Higher similarity scores (closer to 1.0) indicate more relevant context
- Context chunks are automatically ranked and filtered by similarity threshold

**Best Practices:**
- Use `top_k=3-5` for focused, specific questions
- Use `top_k=8-10` for broad, exploratory questions
- Set `min_similarity=0.6+` for highly relevant context only
- Set `min_similarity=0.3-0.5` for broader context inclusion
- Use `temperature=0.1-0.3` for factual, technical questions
- Use `temperature=0.7-1.0` for creative or analytical questions

**Error Handling for RAG Tools:**
```json
{
  "jsonrpc": "2.0",
  "error": {
    "code": -32603,
    "message": "Internal error",
    "data": "Vector store not initialized. Please run reindex_documents first."
  }
}
```

**Common RAG Error Scenarios:**
- **Vector Store Not Available**: RAG index hasn't been created or is corrupted
- **Empty Search Results**: No documents match the similarity threshold
- **Invalid Query Parameters**: `top_k` out of range or invalid `min_similarity`
- **Embedding Generation Failed**: Unable to convert query to vector embeddings
- **AI Provider Unavailable**: RAG search succeeded but AI generation failed (rag_ask only)
- **File Filter Errors**: Invalid file patterns or no matching files
- **Context Window Exceeded**: Retrieved context too large for AI provider
- **Indexing In Progress**: Vector store is being rebuilt and temporarily unavailable

### **reindex_documents** - Rebuild Vector Index (RAG/Hybrid modes)
Rebuild the vector store index.

**Parameters:**
- `force` (optional): Force reindex of all files, not just changed ones (default: false)

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "reindex_documents",
    "arguments": {
      "force": true
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "status": "completed",
    "filesProcessed": 156,
    "chunksCreated": 234,
    "newFiles": 12,
    "modifiedFiles": 8,
    "timeTaken": "00:01:23.45",
    "forceReindex": true
  }
}
```

### **indexing_report** - Get Indexing Status Report (RAG/Hybrid modes)
Get comprehensive report of indexed vs. non-indexed files.

**Parameters:**
- `show_details` (optional): Include detailed file information (default: true)

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "indexing_report",
    "arguments": {
      "show_details": true
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "summary": {
      "totalFiles": 156,
      "indexable": 89,
      "notIndexable": 45,
      "tooLarge": 4,
      "passwordProtected": 3,
      "indexedChunks": 234
    },
    "byFileType": {
      ".pdf": {"total": 34, "indexable": 34, "indexed": 34},
      ".txt": {"total": 28, "indexable": 28, "indexed": 28},
      ".html": {"total": 18, "indexable": 18, "indexed": 18},
      ".jpg": {"total": 23, "indexable": 0, "indexed": 0},
      ".exe": {"total": 8, "indexable": 0, "indexed": 0}
    },
    "details": [
      {
        "file": "security-guide.pdf",
        "size": 3400000,
        "indexable": true,
        "indexed": true,
        "chunks": 34,
        "lastIndexed": "2024-01-15T14:30:22Z"
      },
      {
        "file": "presentation.pptx", 
        "size": 215000000,
        "indexable": false,
        "indexed": false,
        "reason": "File too large (>100MB)",
        "lastChecked": "2024-01-15T14:30:25Z"
      }
    ]
  }
}
```

## 🔌 Integration Examples

### Integration with Claude Desktop

**Claude Desktop Configuration:**
```json
{
  "mcpServers": {
    "hlpai": {
      "command": "dotnet",
      "args": [
        "run",
        "C:\\YourDocuments",
        "llama3.2", 
        "mcp"
      ],
      "env": {
        "DOTNET_ROOT": "/usr/local/share/dotnet"
      }
    }
  }
}
```

**Usage in Claude:**
```
@hlpai search_files query="authentication setup" maxResults=3
@hlpai ask_ai question="How do I configure SSL?" temperature=0.3 useRag=true
@hlpai analyze_file uri="file:///user-manual.pdf" analysisType="summary"
```

### Programmatic Integration in .NET

**Using EnhancedMcpRagServer Directly:**
```csharp
using HlpAI.MCP;

// Create and initialize server
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<EnhancedMcpRagServer>();
var server = new EnhancedMcpRagServer(logger, "C:\\YourDocuments", "llama3.2", OperationMode.MCP);

await server.InitializeAsync();

// Make MCP requests
var request = new McpRequest 
{
    JsonRpc = "2.0",
    Id = "1",
    Method = "tools/call",
    Params = new 
    {
        name = "ask_ai",
        arguments = new 
        {
            question = "What are the security best practices?",
            temperature = 0.7,
            useRag = true
        }
    }
};

var response = await server.HandleRequestAsync(request);
Console.WriteLine(response.Result.Content);
```

### HTTP API Wrapper Example

**Simple HTTP Wrapper:**
```csharp
// Program.cs
using HlpAI.MCP;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<EnhancedMcpRagServer>();

var app = builder.Build();

// Initialize server at startup
var server = app.Services.GetRequiredService<EnhancedMcpRagServer>();
await server.InitializeAsync();

// HTTP endpoints
app.MapPost("/api/ask", async ([FromBody] AskRequest request) =>
{
    var mcpRequest = new McpRequest
    {
        JsonRpc = "2.0",
        Id = Guid.NewGuid().ToString(),
        Method = "tools/call",
        Params = new
        {
            name = "ask_ai",
            arguments = request
        }
    };
    
    var response = await server.HandleRequestAsync(mcpRequest);
    return Results.Ok(response.Result);
});

app.MapPost("/api/search", async ([FromBody] SearchRequest request) =>
{
    var mcpRequest = new McpRequest
    {
        JsonRpc = "2.0",
        Id = Guid.NewGuid().ToString(),
        Method = "tools/call", 
        Params = new
        {
            name = "search_files",
            arguments = request
        }
    };
    
    var response = await server.HandleRequestAsync(mcpRequest);
    return Results.Ok(response.Result);
});

app.Run();

public record AskRequest(string Question, double Temperature = 0.7, bool UseRag = false);
public record SearchRequest(string Query, int MaxResults = 10);
```

### Python Integration Example

**Using subprocess for MCP communication:**
```python
import json
import subprocess
import threading

class HlpAIClient:
    def __init__(self, document_dir, model_name="llama3.2"):
        self.process = subprocess.Popen(
            ["dotnet", "run", document_dir, model_name, "mcp"],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True
        )
        self.request_id = 1
        
    def send_request(self, method, params):
        request = {
            "jsonrpc": "2.0",
            "id": self.request_id,
            "method": method,
            "params": params
        }
        self.request_id += 1
        
        # Send request
        self.process.stdin.write(json.dumps(request) + "\n")
        self.process.stdin.flush()
        
        # Read response
        response_line = self.process.stdout.readline()
        return json.loads(response_line)
    
    def ask_ai(self, question, temperature=0.7, use_rag=False):
        return self.send_request("tools/call", {
            "name": "ask_ai",
            "arguments": {
                "question": question,
                "temperature": temperature,
                "useRag": use_rag
            }
        })
    
    def search_files(self, query, max_results=10):
        return self.send_request("tools/call", {
            "name": "search_files", 
            "arguments": {
                "query": query,
                "maxResults": max_results
            }
        })

# Usage
client = HlpAIClient("/path/to/documents")
response = client.ask_ai("How do I set up the database?")
print(response["result"]["content"])
```

## 🚀 Deployment & Operations

### Running as a Service

**Windows Service (using NSSM):**
```bash
# Install as service
nssm install HlpAI-MCP "C:\Program Files\dotnet\dotnet.exe" "run C:\Documents llama3.2 mcp"

# Start service
nssm start HlpAI-MCP

# Monitor logs
nssm get HlpAI-MCP AppStderr
```

**Linux Systemd Service:**
```ini
# /etc/systemd/system/hlpai-mcp.service
[Unit]
Description=HlpAI MCP Server
After=network.target

[Service]
Type=simple
User=hlpai
WorkingDirectory=/opt/hlpai
ExecStart=/usr/bin/dotnet run /home/hlpai/documents llama3.2 mcp
Restart=always
RestartSec=5
Environment=DOTNET_ROOT=/usr/share/dotnet

[Install]
WantedBy=multi-user.target
```

**Docker Deployment:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore && dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app .
VOLUME /documents
ENTRYPOINT ["dotnet", "HlpAI.dll", "/documents", "llama3.2", "mcp"]
```

```bash
# Build and run
docker build -t hlpai-mcp .
docker run -d -v /path/to/documents:/documents -p 11434:11434 hlpai-mcp
```

### Performance Optimization

**Memory Management:**
```bash
# Increase memory limits
export DOTNET_GCHeapCount=4
export DOTNET_GCHeapHardLimit=0x100000000  # 4GB

# Or use runtimeconfig.json
{
  "runtimeOptions": {
    "configProperties": {
      "System.GC.HeapHardLimit": 4294967296,
      "System.GC.HeapCount": 4
    }
  }
}
```

**Concurrency Settings:**
```csharp
// In your startup configuration
services.Configure<ConcurrencyOptions>(options =>
{
    options.MaxConcurrentRequests = 10;
    options.RequestTimeout = TimeSpan.FromMinutes(2);
    options.MaxMemoryUsage = 1024 * 1024 * 1024; // 1GB
});
```

### Monitoring & Logging

**Structured Logging Configuration:**
```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/hlpai-mcp-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

**Health Check Endpoint:**
```csharp
app.MapGet("/health", async (EnhancedMcpRagServer server) =>
{
    try
    {
        var status = await server.GetStatusAsync();
        return Results.Ok(new {
            status = "healthy",
            documents = status.TotalFiles,
            indexed = status.IndexedFiles,
            memory = GC.GetTotalMemory(false) / 1024 / 1024
        });
    }
    catch
    {
        return Results.StatusCode(503);
    }
});
```

## 🔧 Advanced Configuration

### Custom MCP Server Settings

**appsettings.json Configuration:**
```json
{
  "McpServer": {
    "Name": "HlpAI",
    "Version": "1.0.0",
    "Capabilities": {
      "Resources": true,
      "Tools": true,
      "Logging": true
    },
    "Limits": {
      "MaxConcurrentRequests": 10,
      "MaxMemoryUsage": 1073741824,
      "RequestTimeout": "00:02:00"
    },
    "DocumentProcessing": {
      "ChunkSize": 1000,
      "ChunkOverlap": 200,
      "MaxFileSize": 104857600,
      "SupportedExtensions": [".txt", ".md", ".pdf", ".html", ".htm"]
    }
  }
}
```

### Authentication & Security

**API Key Authentication:**
```csharp
// Add authentication middleware
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.TryGetValue("X-API-Key", out var apiKey) ||
        apiKey != Configuration["ApiKey"])
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Invalid API key");
        return;
    }
    await next();
});
```

**Rate Limiting:**
```csharp
// Add rate limiting
services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString(),
            partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

## 🚨 Troubleshooting

### Common MCP Server Issues

**Connection Problems:**
```bash
# Test server connectivity
curl -X POST http://localhost:11434/api/version
# Check if ports are open
netstat -an | find "11434"
# Verify firewall settings
netsh advfirewall firewall show rule name="HlpAI MCP"
```

**Performance Issues:**
```bash
# Monitor memory usage
dotnet-counters monitor --process-id <PID> --counters System.Runtime
# Check CPU usage
dotnet-trace collect --process-id <PID> --profile cpu-sampling
# Analyze memory dump
dotnet-dump collect --process-id <PID>
```

**Log Analysis:**
```bash
# View recent errors
grep -i error logs/hlpai-mcp-*.log
# Monitor live logs
tail -f logs/hlpai-mcp-$(date +%Y-%m-%d).log
# Search for specific patterns
grep -n "authentication" logs/hlpai-mcp-*.log
```

### Debugging MCP Requests

**Enable Debug Logging:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "HlpAI.MCP": "Debug",
      "System": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

**Request/Response Logging Middleware:**
```csharp
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    
    // Log request
    context.Request.EnableBuffering();
    var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
    context.Request.Body.Position = 0;
    
    logger.LogDebug("Request: {Method} {Path} {Body}", 
        context.Request.Method, context.Request.Path, requestBody);
    
    // Capture response
    var originalBody = context.Response.Body;
    using var responseBody = new MemoryStream();
    context.Response.Body = responseBody;
    
    await next();
    
    // Log response
    responseBody.Position = 0;
    var response = await new StreamReader(responseBody).ReadToEndAsync();
    responseBody.Position = 0;
    await responseBody.CopyToAsync(originalBody);
    
    logger.LogDebug("Response: {StatusCode} {Body}", 
        context.Response.StatusCode, response);
});
```

## 📊 Performance Benchmarks

### Typical Performance Metrics

**Indexing Performance:**
- **Small documents** (<100KB): 50-100 files/second
- **Medium documents** (100KB-1MB): 10-20 files/second  
- **Large documents** (1MB-10MB): 2-5 files/second
- **Very large documents** (>10MB): Processed in chunks, ~1 file/second

**Search Performance:**
- **Text search**: <100ms for most queries
- **Semantic search**: 200-500ms depending on index size
- **AI responses**: 1-5 seconds depending on model complexity

**Memory Usage:**
- **Base memory**: 50-100MB for runtime
- **Per document**: ~1MB per 1000 documents indexed
- **Vector store**: Additional 50-100MB for embeddings

### Scaling Recommendations

**For small deployments** (<10,000 documents):
- Single server instance
- 2-4GB RAM recommended
- 2 CPU cores minimum

**For medium deployments** (10,000-100,000 documents):
- Consider multiple instances with load balancing
- 8-16GB RAM recommended  
- 4-8 CPU cores recommended
- SSD storage strongly recommended

**For large deployments** (>100,000 documents):
- Distributed architecture with multiple specialized instances
- 32GB+ RAM per instance
- 8+ CPU cores per instance
- High-performance SSD storage required
- Consider database sharding for vector store

## 🎯 Next Steps

After setting up MCP Server Mode, consider:

1. **Integration with existing tools** like Claude Desktop, Cursor, or custom applications
2. **Automation workflows** for document processing and analysis
3. **Monitoring and alerting** for production deployments
4. **Performance optimization** based on your specific workload
5. **Security hardening** for exposed APIs

For more advanced usage, explore:
- **[Interactive Mode](README-INTERACTIVE.md)** - For manual exploration and testing
- **[Library Mode](README-LIBRARY.md)** - For .NET application integration
- **Custom extractors** - For specialized document types
- **Advanced RAG configurations** - For optimized search performance

---

**MCP Server Mode provides powerful programmatic access to HlpAI's document intelligence capabilities, enabling seamless integration with external tools, automation workflows, and custom applications through the standardized Model Context Protocol.**
