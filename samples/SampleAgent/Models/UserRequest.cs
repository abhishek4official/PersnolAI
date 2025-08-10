using System.ComponentModel.DataAnnotations;

namespace SampleAgent.Models;

/// <summary>
/// Represents a user request input
/// </summary>
public class UserRequest
{
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
    public string FileName { get; set; } = string.Empty;
    public byte[] FileContent { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
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
    public string Question { get; set; } = string.Empty;
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
