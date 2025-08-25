using HlpAI.MCP;

namespace HlpAI.Tests.MCP;

public class McpModelsTests
{
    [Test]
    public async Task McpRequest_DefaultConstructor_SetsCorrectDefaults()
    {
        // Act
        var request = new McpRequest
        {
            Method = "test/method",
            Params = new { test = "value" }
        };

        // Assert
        await Assert.That(request.Jsonrpc).IsEqualTo("2.0");
        await Assert.That(request.Id).IsNotNull();
        await Assert.That(request.Id).IsNotEmpty();
        await Assert.That(request.Method).IsEqualTo("test/method");
        await Assert.That(request.Params).IsNotNull();
    }

    [Test]
    public async Task McpRequest_WithCustomId_UsesProvidedId()
    {
        // Arrange
        var customId = "custom-123";
        
        // Act
        var request = new McpRequest
        {
            Id = customId,
            Method = "test/method",
            Params = new { }
        };

        // Assert
        await Assert.That(request.Id).IsEqualTo(customId);
    }

    [Test]
    public async Task McpResponse_Constructor_SetsRequiredProperties()
    {
        // Arrange
        var id = "test-id";
        var result = new { message = "success" };
        
        // Act
        var response = new McpResponse
        {
            Id = id,
            Result = result
        };

        // Assert
        await Assert.That(response.Jsonrpc).IsEqualTo("2.0");
        await Assert.That(response.Id).IsEqualTo(id);
        await Assert.That(response.Result).IsEqualTo(result);
        await Assert.That(response.Error).IsNull();
    }

    [Test]
    public async Task McpResponse_WithError_SetsErrorProperty()
    {
        // Arrange
        var id = "test-id";
        var error = new ErrorResponse { Code = 404, Message = "Not found" };
        
        // Act
        var response = new McpResponse
        {
            Id = id,
            Error = error
        };

        // Assert
        await Assert.That(response.Id).IsEqualTo(id);
        await Assert.That(response.Error).IsEqualTo(error);
        await Assert.That(response.Result).IsNull();
    }

    [Test]
    public async Task ResourceInfo_Constructor_SetsAllProperties()
    {
        // Arrange
        var uri = "file:///test.txt";
        var name = "test.txt";
        var description = "Test file";
        var mimeType = "text/plain";
        
        // Act
        var resourceInfo = new ResourceInfo
        {
            Uri = uri,
            Name = name,
            Description = description,
            MimeType = mimeType
        };

        // Assert
        await Assert.That(resourceInfo.Uri).IsEqualTo(uri);
        await Assert.That(resourceInfo.Name).IsEqualTo(name);
        await Assert.That(resourceInfo.Description).IsEqualTo(description);
        await Assert.That(resourceInfo.MimeType).IsEqualTo(mimeType);
    }

    [Test]
    public async Task ReadResourceRequest_Constructor_SetsUri()
    {
        // Arrange
        var uri = "file:///document.pdf";
        
        // Act
        var request = new ReadResourceRequest
        {
            Uri = uri
        };

        // Assert
        await Assert.That(request.Uri).IsEqualTo(uri);
    }

    [Test]
    public async Task ResourceContent_Constructor_SetsAllProperties()
    {
        // Arrange
        var uri = "file:///content.txt";
        var mimeType = "text/plain";
        var text = "Sample content";
        
        // Act
        var content = new ResourceContent
        {
            Uri = uri,
            MimeType = mimeType,
            Text = text
        };

        // Assert
        await Assert.That(content.Uri).IsEqualTo(uri);
        await Assert.That(content.MimeType).IsEqualTo(mimeType);
        await Assert.That(content.Text).IsEqualTo(text);
    }

    [Test]
    public async Task TextContent_Constructor_SetsDefaultType()
    {
        // Arrange
        var text = "Sample text content";
        
        // Act
        var textContent = new TextContent
        {
            Type = "text",
            Text = text
        };

        // Assert
        await Assert.That(textContent.Type).IsEqualTo("text");
        await Assert.That(textContent.Text).IsEqualTo(text);
    }

    [Test]
    public async Task TextContent_WithCustomType_UsesProvidedType()
    {
        // Arrange
        var customType = "markdown";
        var text = "# Markdown content";
        
        // Act
        var textContent = new TextContent
        {
            Type = customType,
            Text = text
        };

        // Assert
        await Assert.That(textContent.Type).IsEqualTo(customType);
        await Assert.That(textContent.Text).IsEqualTo(text);
    }

    [Test]
    public async Task ErrorResponse_Constructor_SetsCodeAndMessage()
    {
        // Arrange
        var code = 500;
        var message = "Internal server error";
        
        // Act
        var error = new ErrorResponse
        {
            Code = code,
            Message = message
        };

        // Assert
        await Assert.That(error.Code).IsEqualTo(code);
        await Assert.That(error.Message).IsEqualTo(message);
    }

    [Test]
    public async Task ResourcesListResponse_Constructor_SetsResourcesList()
    {
        // Arrange
        var resources = new List<ResourceInfo>
        {
            new ResourceInfo
            {
                Uri = "file:///test1.txt",
                Name = "test1.txt",
                Description = "First test file",
                MimeType = "text/plain"
            },
            new ResourceInfo
            {
                Uri = "file:///test2.pdf",
                Name = "test2.pdf",
                Description = "Second test file",
                MimeType = "application/pdf"
            }
        };
        
        // Act
        var response = new ResourcesListResponse
        {
            Resources = resources
        };

        // Assert
        await Assert.That(response.Resources).IsEqualTo(resources);
        await Assert.That(response.Resources.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ToolsListResponse_Constructor_SetsToolsList()
    {
        // Arrange
        var tools = new List<object>
        {
            new { name = "search_files", description = "Search for files" },
            new { name = "ask_ai", description = "Ask AI questions" }
        };
        
        // Act
        var response = new ToolsListResponse
        {
            Tools = tools
        };

        // Assert
        await Assert.That(response.Tools).IsEqualTo(tools);
        await Assert.That(response.Tools.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ReadResourceResponse_Constructor_SetsContentsList()
    {
        // Arrange
        var contents = new List<ResourceContent>
        {
            new ResourceContent
            {
                Uri = "file:///content1.txt",
                MimeType = "text/plain",
                Text = "Content 1"
            },
            new ResourceContent
            {
                Uri = "file:///content2.txt",
                MimeType = "text/plain",
                Text = "Content 2"
            }
        };
        
        // Act
        var response = new ReadResourceResponse
        {
            Contents = contents
        };

        // Assert
        await Assert.That(response.Contents).IsEqualTo(contents);
        await Assert.That(response.Contents.Count).IsEqualTo(2);
    }

    [Test]
    public async Task TextContentResponse_Constructor_SetsContentList()
    {
        // Arrange
        var content = new List<TextContent>
        {
            new TextContent { Type = "text", Text = "First text" },
            new TextContent { Type = "text", Text = "Second text" }
        };
        
        // Act
        var response = new TextContentResponse
        {
            Content = content
        };

        // Assert
        await Assert.That(response.Content).IsEqualTo(content);
        await Assert.That(response.Content.Count).IsEqualTo(2);
    }
}