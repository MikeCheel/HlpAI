namespace HlpAI.Services
{
    public interface IEmbeddingService : IDisposable
    {
        Task<float[]> GetEmbeddingAsync(string text);
    }
}