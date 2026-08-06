public class InMemoryVectorStore
{
    private readonly List<(string SessionId, string ChunkText, float[] Embedding)> _embeddings = new();
    private readonly object _lock = new();

    public void AddEmbedding(string sessionId, string chunkText, float[] embedding)
    {
        lock (_lock)
        {
            _embeddings.Add((sessionId, chunkText, embedding));
        }
    }

    public List<(string SessionId, string ChunkText, float[] Embedding)> GetEmbeddingsForSession(string sessionId)
    {
        lock (_lock)
        {
            return _embeddings.Where(e => e.SessionId == sessionId).ToList();
        }
    }
}