namespace LocalChatApi.Configuration;

/// <summary>
/// MongoDB configuration settings
/// </summary>
public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "LocalChatDb";
    public string ChatMessagesCollection { get; set; } = "ChatMessages";
    public string ChatSessionsCollection { get; set; } = "ChatSessions";
    public string FileDocumentsCollection { get; set; } = "FileDocuments";
}

/// <summary>
/// Ollama configuration settings
/// </summary>
public class OllamaSettings
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string ChatModel { get; set; } = "llama3.1:latest";
    public string EmbeddingModel { get; set; } = "mxbai-embed-large:latest";
    
    /// <summary>
    /// Request timeout in minutes (default: 20 minutes)
    /// </summary>
    public double TimeoutMinutes { get; set; } = 20.0;
}
