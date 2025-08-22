namespace HlpAI.MCP;

public class McpRequest
{
    public string Jsonrpc { get; set; } = "2.0";
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Method { get; set; }
    public required object Params { get; set; }
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