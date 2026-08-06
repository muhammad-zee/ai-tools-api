using Microsoft.SemanticKernel.ChatCompletion;

public class ChatSessionStore
{
    private readonly Dictionary<string, (ChatHistory History, DateTime LastUpdated)> _sessions = new();
    private readonly object _lock = new();

    public ChatHistory GetOrCreate(string sessionId)
    {
       lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry))
            {
                entry = (new ChatHistory(), DateTime.UtcNow);
                _sessions[sessionId] = entry;
            }
            return entry.History;
        }
    }
      public void Touch(string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var entry))
            {
                _sessions[sessionId] = (entry.History, DateTime.UtcNow);
            }
        }
    }
    public void RemoveExpired(TimeSpan maxAge)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            var expiredKeys = _sessions
                .Where(kvp => kvp.Value.LastUpdated < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _sessions.Remove(key);
            }
        }
    }
}