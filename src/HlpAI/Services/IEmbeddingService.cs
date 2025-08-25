using System.Threading.Tasks;

namespace HlpAI.Services
{
    public interface IEmbeddingService : IDisposable
    {
        Task<float[]> GetEmbeddingAsync(string text);
    }
}