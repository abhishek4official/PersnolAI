using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LocalChatApi.Models;

/// <summary>
/// Represents a user request input
/// </summary>
public class UserRequest
{
    [Required]
    public string Input { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Represents intent detection result
/// </summary>
public class IntentResult
{
    public string Intent { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public Dictionary<string, object> Entities { get; set; } = new();
    public string OriginalInput { get; set; } = string.Empty;
}

/// <summary>
/// Represents file upload request
/// </summary>
public class FileUploadRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;
    public string SessionId { get; set; } = string.Empty;
}

/// <summary>
/// Represents file processing result
/// </summary>
public class FileProcessingResult
{
    public string FileId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents file chat request (RAG)
/// </summary>
public class FileChatRequest
{
    [Required]
    public string Question { get; set; } = string.Empty;
    [Required]
    public string FileId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}

/// <summary>
/// Represents processed file data
/// </summary>
public class ProcessedFile
{
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string OriginalContent { get; set; } = string.Empty;
    public string CleanedContent { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public List<DocumentChunk> Chunks { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a document chunk with embeddings
/// </summary>
public class DocumentChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public int ChunkIndex { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents RAG search result
/// </summary>
public class RagSearchResult
{
    public List<DocumentChunk> RelevantChunks { get; set; } = new();
    public string Context { get; set; } = string.Empty;
    public double AverageScore { get; set; }
}

/// <summary>
/// API Response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Chat response
/// </summary>
public class ChatResponse
{
    public string Response { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Chat message for MongoDB storage
/// </summary>
public class ChatMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [BsonElement("role")]
    public string Role { get; set; } = string.Empty; // "user" or "assistant"

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("intent")]
    public string Intent { get; set; } = string.Empty;

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Chat session for MongoDB storage
/// </summary>
public class ChatSession
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("lastMessageAt")]
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// File storage document for MongoDB
/// </summary>
public class FileDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("fileId")]
    public string FileId { get; set; } = string.Empty;

    [BsonElement("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [BsonElement("fileName")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [BsonElement("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [BsonElement("fileSize")]
    public long FileSize { get; set; }

    [BsonElement("chunks")]
    public List<DocumentChunk> Chunks { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    // Computed properties (not stored in MongoDB)
    [BsonIgnore]
    public string? OriginalContent { get; set; }

    [BsonIgnore]
    public string? CleanedContent { get; set; }
}
