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
        "uri": "file:///user-manual.pdf",
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
        "description": "Search files by text content",
        "inputSchema": {
          "type": "object",
          "properties": {
            "query": {"type": "string", "description": "Search query"},
            "maxResults": {"type": "number", "description": "Maximum results to return", "default": 10}
          },
          "required": ["query"]
        }
      },
      {
        "name": "ask_ai",
        "description": "Ask AI questions with optional RAG enhancement",
        "inputSchema": {
          "type": "object",
          "properties": {
            "question": {"type": "string", "description": "Question to ask"},
            "context": {"type": "string", "description": "Additional context for the AI"},
            "temperature": {"type": "number", "description": "Creativity level (0.0-1.0)", "default": 0.7},
            "useRag": {"type": "boolean", "description": "Use RAG enhancement", "default": false}
          },
          "required": ["question"]
        }
      }
    ]
  }
}
```

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
      "useRag": true
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

### **ask_ai** - AI Question Answering
Ask questions with full customization options.

**Parameters:**
- `question` (required): Question to ask the AI
- `context` (optional): Additional context to guide the AI response
- `temperature` (optional): Creativity level (0.0 = factual, 1.0 = creative, default: 0.7)
- `useRag` (optional): Use RAG enhancement (default: false)

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
      "useRag": true
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

### **analyze_file** - AI-Powered File Analysis
Analyze specific files with multiple analysis types.

**Parameters:**
- `uri` (required): File URI to analyze (e.g., "file:///user-manual.pdf")
- `analysisType` (optional): Type of analysis (summary, key_points, questions, topics, technical, explanation, default: summary)
- `temperature` (optional): Creativity level (0.0-1.0, default: 0.7)
- `useRag` (optional): Use RAG enhancement (default: false)

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "analyze_file",
    "arguments": {
      "uri": "file:///user-manual.pdf",
      "analysisType": "key_points",
      "temperature": 0.4,
      "useRag": true
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
Search using vector embeddings for meaning-based results.

**Parameters:**
- `query` (required): Search query
- `topK` (optional): Number of top results to return (default: 5)
- `minSimilarity` (optional): Minimum similarity score (0.0-1.0, default: 0.5)

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "rag_search",
    "arguments": {
      "query": "authentication setup",
      "topK": 3,
      "minSimilarity": 0.6
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
        "similarity": 0.89,
        "content": "User authentication is handled through JWT tokens. Configure the auth service by setting the secret key in your environment variables...",
        "page": 12,
        "chunk": 34
      },
      {
        "file": "api-docs.html",
        "similarity": 0.76,
        "content": "Authentication endpoints are available at /auth/login and /auth/verify. These endpoints require valid API keys...",
        "line": 45,
        "chunk": 22
      }
    ],
    "query": "authentication setup",
    "totalResults": 12,
    "minSimilarity": 0.6
  }
}
```

### **rag_ask** - RAG-Enhanced AI Questioning (RAG/Hybrid modes)
Ask questions enhanced with document context.

**Parameters:**
- `question` (required): Question to ask
- `topK` (optional): Number of context chunks to use (default: 5)
- `temperature` (optional): Creativity level (0.0-1.0, default: 0.7)

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "rag_ask",
    "arguments": {
      "question": "How do I set up SSL certificates?",
      "topK": 3,
      "temperature": 0.3
    }
  }
}
```

**Example Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "answer": "Based on your security documentation, SSL certificate setup involves:\n\n1. **Certificate Generation**: Use OpenSSL to generate certificates as described in the security guide (page 8)\n2. **Configuration**: Set the certificate paths in config.json under the 'Security' section\n3. **Verification**: Use the provided test script to verify certificate validity\n\nSpecific steps from your documents:\n• Certificate files should be placed in /etc/ssl/certs/ (security-guide.pdf)\n• Private key must have 600 permissions (config-reference.txt)\n• Certificate chain must include intermediate certificates (api-docs.html)",
    "contextSources": [
      {"file": "security-guide.pdf", "similarity": 0.92, "page": 8},
      {"file": "config-reference.txt", "similarity": 0.85},
      {"file": "api-docs.html", "similarity": 0.78, "line": 128}
    ],
    "topK": 3,
    "temperature": 0.3
  }
}
```

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
- `showDetails` (optional): Include detailed file information (default: false)

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "indexing_report",
    "arguments": {
      "showDetails": true
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
