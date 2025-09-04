namespace HlpAI.MCP;

public class McpRequest
{
    public string Jsonrpc { get; set; } = "2.0";
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Method { get; set; }
    public required object? Params { get; set; }
}

public class McpResponse
{
    public string Jsonrpc { get; set; } = "2.0";
    public required string Id { get; set; }
    public object? Result { get; set; }
    public object? Error { get; set; }
}

public class ResourceInfo
{
    public required string Uri { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string MimeType { get; set; }
}

public class ReadResourceRequest
{
    public required string Uri { get; set; }
}

public class ResourceContent
{
    public required string Uri { get; set; }
    public required string MimeType { get; set; }
    public required string Text { get; set; }
}

public class ResourcesListResponse
{
    public required List<ResourceInfo> Resources { get; set; }
}

public class ToolsListResponse
{
    public required List<object> Tools { get; set; }
}

public class ReadResourceResponse
{
    public required List<ResourceContent> Contents { get; set; }
}

public class TextContentResponse
{
    public required List<TextContent> Content { get; set; }
}

public class TextContent
{
    public required string Type { get; set; } = "text";
    public required string Text { get; set; }
}

public class ErrorResponse
{
    public required int Code { get; set; }
    public required string Message { get; set; }
}
