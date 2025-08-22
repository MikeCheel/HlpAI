namespace HlpAI.Models;

public interface IVectorStore : IDisposable
{
    Task IndexDocumentAsync(string filePath, string content, Dictionary<string, object>? metadata = null);
    Task<List<SearchResult>> SearchAsync(RagQuery query);
    int GetChunkCount();
    List<string> GetIndexedFiles();
    void ClearIndex();
    Task<int> GetChunkCountAsync();
    Task<List<string>> GetIndexedFilesAsync();
    Task ClearIndexAsync();
}