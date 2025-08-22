namespace HlpAI.Models;

public class DocumentChunk
{
    public string Id { get; set; }
    public required string SourceFile { get; set; }
    public required string Content { get; set; }
    public required float[] Embedding { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    public int ChunkIndex { get; set; }
    public DateTime IndexedAt { get; set; }

    public DocumentChunk()
    {
        Id = Guid.NewGuid().ToString();
        Metadata = [];
        IndexedAt = DateTime.UtcNow;
    }
}

public class SearchResult
{
    public required DocumentChunk Chunk { get; set; }
    public float Similarity { get; set; }
}

public class RagQuery
{
    public required string Query { get; set; }
    public int TopK { get; set; } = 5;
    public float MinSimilarity { get; set; } = 0.1f;
    public List<string> FileFilters { get; set; } = [];
}