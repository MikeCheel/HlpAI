using System.Net;
using System.Text;

namespace HlpAI.Tests.TestHelpers;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode statusCode, string content)> _responses = [];
    private readonly Dictionary<string, Func<HttpRequestMessage, string>> _dynamicResponses = [];
    private readonly List<(string method, string url, string? content)> _requests = [];

    public void SetupResponse(string url, HttpStatusCode statusCode, string content)
    {
        _responses[url] = (statusCode, content);
    }

    public void SetupResponse(string url, string content)
    {
        _responses[url] = (HttpStatusCode.OK, content);
    }
    
    public void SetupDynamicResponse(string url, Func<HttpRequestMessage, string> responseFunc)
    {
        _dynamicResponses[url] = responseFunc;
    }

    public List<(string method, string url, string? content)> GetRequests() => _requests;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? "";
        var method = request.Method.ToString();
        var content = request.Content != null ? await request.Content.ReadAsStringAsync() : null;
        
        _requests.Add((method, url, content));

        // Check for dynamic responses first
        var dynamicKey = _dynamicResponses.Keys.FirstOrDefault(key => url.Contains(key));
        if (dynamicKey != null)
        {
            var responseContent = _dynamicResponses[dynamicKey](request);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            };
        }
        
        // Find matching static response
        var matchingKey = _responses.Keys.FirstOrDefault(key => url.Contains(key));
        if (matchingKey != null)
        {
            var (statusCode, responseContent) = _responses[matchingKey];
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            };
        }

        // Default response for unmatched requests
        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Not found", Encoding.UTF8, "text/plain")
        };
    }
}