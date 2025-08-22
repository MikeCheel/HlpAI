namespace HlpAI.Models;

public class IndexingResult
{
    public List<string> IndexedFiles { get; set; } = [];
    public List<SkippedFile> SkippedFiles { get; set; } = [];
    public List<FailedFile> FailedFiles { get; set; } = [];
    public DateTime IndexingStarted { get; set; }
    public DateTime IndexingCompleted { get; set; }
    public TimeSpan Duration => IndexingCompleted - IndexingStarted;
}

public class SkippedFile
{
    public required string FilePath { get; set; }
    public required string Reason { get; set; }
    public string? FileExtension { get; set; }
    public long FileSize { get; set; }
}

public class FailedFile
{
    public required string FilePath { get; set; }
    public required string Error { get; set; }
    public string? ExtractorType { get; set; }
}