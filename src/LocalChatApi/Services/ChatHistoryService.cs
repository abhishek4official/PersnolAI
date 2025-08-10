using LocalChatApi.Models;
using MongoDB.Driver;

namespace LocalChatApi.Services;

/// <summary>
/// Interface for chat history service
/// </summary>
public interface IChatHistoryService
{
    Task<ChatSession> CreateSessionAsync(string userId, string title = "");
    Task<ChatSession?> GetSessionAsync(string sessionId);
    Task<List<ChatSession>> GetUserSessionsAsync(string userId);
    Task<bool> UpdateSessionAsync(ChatSession session);
    Task<bool> DeactivateSessionAsync(string sessionId);

    Task<ChatMessage> SaveMessageAsync(ChatMessage message);
    Task<List<ChatMessage>> GetSessionMessagesAsync(string sessionId, int limit = 50);
    Task<List<ChatMessage>> GetRecentMessagesAsync(string sessionId, int count = 10);
}

/// <summary>
/// MongoDB implementation of chat history service
/// </summary>
public class MongoDbChatHistoryService : IChatHistoryService
{
    private readonly IMongoCollection<ChatMessage> _messagesCollection;
    private readonly IMongoCollection<ChatSession> _sessionsCollection;

    public MongoDbChatHistoryService(IMongoDatabase database)
    {
        _messagesCollection = database.GetCollection<ChatMessage>("ChatMessages");
        _sessionsCollection = database.GetCollection<ChatSession>("ChatSessions");

        // Create indexes for better performance
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        // Index on sessionId for messages
        _messagesCollection.Indexes.CreateOne(
            new CreateIndexModel<ChatMessage>(
                Builders<ChatMessage>.IndexKeys.Ascending(x => x.SessionId)));

        // Index on timestamp for messages
        _messagesCollection.Indexes.CreateOne(
            new CreateIndexModel<ChatMessage>(
                Builders<ChatMessage>.IndexKeys.Descending(x => x.Timestamp)));

        // Index on sessionId for sessions
        _sessionsCollection.Indexes.CreateOne(
            new CreateIndexModel<ChatSession>(
                Builders<ChatSession>.IndexKeys.Ascending(x => x.SessionId)));

        // Index on userId for sessions
        _sessionsCollection.Indexes.CreateOne(
            new CreateIndexModel<ChatSession>(
                Builders<ChatSession>.IndexKeys.Ascending(x => x.UserId)));
    }

    public async Task<ChatSession> CreateSessionAsync(string userId, string title = "")
    {
        var session = new ChatSession
        {
            SessionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Title = string.IsNullOrEmpty(title) ? $"Chat {DateTime.Now:yyyy-MM-dd HH:mm}" : title,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
            IsActive = true
        };

        await _sessionsCollection.InsertOneAsync(session);
        return session;
    }

    public async Task<ChatSession?> GetSessionAsync(string sessionId)
    {
        return await _sessionsCollection
            .Find(x => x.SessionId == sessionId && x.IsActive)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ChatSession>> GetUserSessionsAsync(string userId)
    {
        return await _sessionsCollection
            .Find(x => x.UserId == userId && x.IsActive)
            .SortByDescending(x => x.LastMessageAt)
            .ToListAsync();
    }

    public async Task<bool> UpdateSessionAsync(ChatSession session)
    {
        var result = await _sessionsCollection
            .ReplaceOneAsync(x => x.SessionId == session.SessionId, session);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeactivateSessionAsync(string sessionId)
    {
        var update = Builders<ChatSession>.Update.Set(x => x.IsActive, false);
        var result = await _sessionsCollection
            .UpdateOneAsync(x => x.SessionId == sessionId, update);
        return result.ModifiedCount > 0;
    }

    public async Task<ChatMessage> SaveMessageAsync(ChatMessage message)
    {
        await _messagesCollection.InsertOneAsync(message);

        // Update session's last message time
        var update = Builders<ChatSession>.Update.Set(x => x.LastMessageAt, DateTime.UtcNow);
        await _sessionsCollection.UpdateOneAsync(x => x.SessionId == message.SessionId, update);

        return message;
    }

    public async Task<List<ChatMessage>> GetSessionMessagesAsync(string sessionId, int limit = 50)
    {
        return await _messagesCollection
            .Find(x => x.SessionId == sessionId)
            .SortByDescending(x => x.Timestamp)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<ChatMessage>> GetRecentMessagesAsync(string sessionId, int count = 10)
    {
        return await _messagesCollection
            .Find(x => x.SessionId == sessionId)
            .SortByDescending(x => x.Timestamp)
            .Limit(count)
            .ToListAsync();
    }
}
