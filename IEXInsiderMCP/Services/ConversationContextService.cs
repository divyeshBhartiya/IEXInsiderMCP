using System.Collections.Concurrent;

namespace IEXInsiderMCP.Services;

/// <summary>
/// Manages conversation context for maintaining state across multiple queries
/// Similar to Claude's conversation memory
/// </summary>
public class ConversationContextService
{
    private readonly ConcurrentDictionary<string, ConversationSession> _sessions = new();
    private readonly ILogger<ConversationContextService> _logger;
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromHours(2);

    public ConversationContextService(ILogger<ConversationContextService> logger)
    {
        _logger = logger;

        // Cleanup expired sessions every 30 minutes
        Task.Run(async () => await CleanupExpiredSessionsAsync());
    }

    /// <summary>
    /// Get or create a conversation session
    /// </summary>
    public ConversationSession GetOrCreateSession(string sessionId)
    {
        return _sessions.GetOrAdd(sessionId, _ => new ConversationSession
        {
            SessionId = sessionId,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Add a message to the conversation history
    /// </summary>
    public void AddMessage(string sessionId, string role, string content, object? data = null)
    {
        var session = GetOrCreateSession(sessionId);
        session.LastAccessedAt = DateTime.UtcNow;

        session.Messages.Add(new ConversationMessage
        {
            Role = role,
            Content = content,
            Data = data,
            Timestamp = DateTime.UtcNow
        });

        // Keep only last 50 messages to prevent memory issues
        if (session.Messages.Count > 50)
        {
            session.Messages.RemoveAt(0);
        }

        _logger.LogInformation("Added {Role} message to session {SessionId}. Total messages: {Count}",
            role, sessionId, session.Messages.Count);
    }

    /// <summary>
    /// Get conversation history for context-aware responses
    /// </summary>
    public List<ConversationMessage> GetHistory(string sessionId, int maxMessages = 10)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.LastAccessedAt = DateTime.UtcNow;
            return session.Messages.TakeLast(maxMessages).ToList();
        }

        return new List<ConversationMessage>();
    }

    /// <summary>
    /// Get recent context for building context-aware queries
    /// </summary>
    public string GetRecentContext(string sessionId, int messagesToInclude = 3)
    {
        var history = GetHistory(sessionId, messagesToInclude);

        if (!history.Any())
            return "";

        var contextLines = new List<string>();
        foreach (var message in history)
        {
            contextLines.Add($"{message.Role}: {message.Content}");
        }

        return string.Join("\n", contextLines);
    }

    /// <summary>
    /// Extract entities and filters from conversation history
    /// For context-aware querying
    /// </summary>
    public Dictionary<string, object> GetContextualFilters(string sessionId)
    {
        var session = GetOrCreateSession(sessionId);
        var filters = new Dictionary<string, object>();

        // Look at last 5 messages for context
        var recentMessages = session.Messages.TakeLast(5);

        foreach (var message in recentMessages)
        {
            if (message.Data != null && message.Data is Dictionary<string, object> data)
            {
                // Extract commonly used filters from previous queries
                if (data.ContainsKey("market_type"))
                    filters["market_type"] = data["market_type"];

                if (data.ContainsKey("year"))
                    filters["year"] = data["year"];

                if (data.ContainsKey("month"))
                    filters["month"] = data["month"];
            }
        }

        return filters;
    }

    /// <summary>
    /// Clear a specific session
    /// </summary>
    public void ClearSession(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        _logger.LogInformation("Cleared session: {SessionId}", sessionId);
    }

    /// <summary>
    /// Get session statistics
    /// </summary>
    public SessionStats GetSessionStats(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return new SessionStats
            {
                SessionId = sessionId,
                MessageCount = session.Messages.Count,
                CreatedAt = session.CreatedAt,
                LastAccessedAt = session.LastAccessedAt,
                Duration = DateTime.UtcNow - session.CreatedAt
            };
        }

        return new SessionStats
        {
            SessionId = sessionId,
            MessageCount = 0
        };
    }

    /// <summary>
    /// Cleanup expired sessions periodically
    /// </summary>
    private async Task CleanupExpiredSessionsAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(30));

                var expiredSessions = _sessions
                    .Where(kvp => DateTime.UtcNow - kvp.Value.LastAccessedAt > _sessionTimeout)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var sessionId in expiredSessions)
                {
                    _sessions.TryRemove(sessionId, out _);
                    _logger.LogInformation("Removed expired session: {SessionId}", sessionId);
                }

                if (expiredSessions.Any())
                {
                    _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
            }
        }
    }
}

public class ConversationSession
{
    public string SessionId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public List<ConversationMessage> Messages { get; set; } = new();
}

public class ConversationMessage
{
    public string Role { get; set; } = ""; // "user" or "assistant"
    public string Content { get; set; } = "";
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; }
}

public class SessionStats
{
    public string SessionId { get; set; } = "";
    public int MessageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public TimeSpan Duration { get; set; }
}
