public class InMemoryVectorStore
{
    private readonly List<(string SessionId, string ChunkText, float[] Embedding, DateTime CreatedAt)> _embeddings = new();
    private readonly object _lock = new();

    public void RemoveExpired(TimeSpan maxAge)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            _embeddings.RemoveAll(e => e.CreatedAt < cutoff);
        }
    }

    public void AddEmbedding(string sessionId, string chunkText, float[] embedding)
    {
        lock (_lock)
        {
            _embeddings.Add((sessionId, chunkText, embedding, DateTime.UtcNow));
        }
    }

    public List<(string SessionId, string ChunkText, float[] Embedding, DateTime CreatedAt)> GetEmbeddingsForSession(string sessionId)
    {
        lock (_lock)
        {
            return _embeddings.Where(e => e.SessionId == sessionId).ToList();
        }
    }
}