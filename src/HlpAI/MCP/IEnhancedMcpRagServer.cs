using HlpAI.Services;

namespace HlpAI.MCP
{
    /// <summary>
    /// Interface for EnhancedMcpRagServer to enable proper mocking in tests
    /// </summary>
    public interface IEnhancedMcpRagServer : IDisposable
    {
        /// <summary>
        /// Gets the root path for the server
        /// </summary>
        string RootPath { get; }
        
        /// <summary>
        /// Updates the AI provider used by the server
        /// </summary>
        /// <param name="newProvider">The new AI provider to use</param>
        void UpdateAiProvider(IAiProvider newProvider);
        
        /// <summary>
        /// Handles MCP requests
        /// </summary>
        /// <param name="request">The MCP request to handle</param>
        /// <returns>The MCP response</returns>
        Task<McpResponse> HandleRequestAsync(McpRequest request);
        
        /// <summary>
        /// Initializes the server asynchronously
        /// </summary>
        /// <returns>A task representing the initialization operation</returns>
        Task InitializeAsync();
    }
}