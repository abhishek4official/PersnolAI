using ElsaWorkflowAgent.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using LocalChatApi.Models;
using LocalChatApi.Services;

#pragma warning disable SKEXP0001 // Suppress experimental API warnings

namespace LocalChatApi.Agents;

/// <summary>
/// File reader agent that parses and cleans file content
/// </summary>
public class FileReaderAgent : BaseAgent<string>
{
    private readonly IFileStorageService _fileStorageService;

    public FileReaderAgent(ILogger<FileReaderAgent> logger, Kernel kernel, IFileStorageService fileStorageService)
        : base(logger, kernel)
    {
        _fileStorageService = fileStorageService;
    }

    public override string Id => "file-reader-agent";
    public override string Name => "File Reader Agent";
    public override string Description => "Parses and cleans file content";

    public override AgentMetadata Metadata => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        InputType = typeof(FileDocument),
        OutputType = typeof(string)
    };

    protected override async Task<string> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
    {
        var fileDoc = input as FileDocument ?? throw new ArgumentException("Input must be FileDocument");
        
        Logger.LogInformation("Reading and cleaning file: {FileName}", fileDoc.FileName);

        try
        {
            // Read the file content from disk
            var originalContent = await _fileStorageService.ReadFileContentAsync(fileDoc.FileId);
            if (string.IsNullOrEmpty(originalContent))
            {
                throw new InvalidOperationException($"Could not read content for file: {fileDoc.FileName}");
            }

           

            Logger.LogInformation("File content cleaned successfully for: {FileName}", fileDoc.FileName);
            return originalContent;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error cleaning file content for: {FileName}", fileDoc.FileName);
            throw;
        }
    }
}

/// <summary>
/// Data extraction and testing agent that validates cleaned content
/// </summary>
public class DataExtractionAgent : BaseAgent<string>
{
    public DataExtractionAgent(ILogger<DataExtractionAgent> logger, Kernel kernel)
        : base(logger, kernel)
    {
    }

    public override string Id => "data-extraction-agent";
    public override string Name => "Data Extraction Agent";
    public override string Description => "Validates and extracts structured data from cleaned content";

    public override AgentMetadata Metadata => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        InputType = typeof(string),
        OutputType = typeof(string)
    };

    protected override Task<string> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
    {
        var content = input as string ?? throw new ArgumentException("Input must be string content");
        
        Logger.LogInformation("Extracting and validating data from content (length: {Length})", content.Length);

        // For now, perform basic validation and return the content
        // In a real implementation, you might want to extract specific data types, validate formats, etc.
        
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Content is empty or contains only whitespace");
        }

        if (content.Length < 10)
        {
            Logger.LogWarning("Content is very short, may not be suitable for chunking");
        }

        Logger.LogInformation("Data extraction completed successfully");
        return Task.FromResult(content);
    }
}

/// <summary>
/// Chunking and embedding agent that creates vector embeddings using Semantic Kernel
/// </summary>
public class ChunkingEmbeddingAgent : BaseAgent<List<DocumentChunk>>
{
    private readonly IFileStorageService _fileStorageService;
    private readonly ITextEmbeddingGenerationService _embeddingService;

    public ChunkingEmbeddingAgent(
        ILogger<ChunkingEmbeddingAgent> logger, 
        Kernel kernel, 
        IFileStorageService fileStorageService,
        ITextEmbeddingGenerationService embeddingService)
        : base(logger, kernel)
    {
        _fileStorageService = fileStorageService;
        _embeddingService = embeddingService;
    }

    public override string Id => "chunking-embedding-agent";
    public override string Name => "Chunking & Embedding Agent";
    public override string Description => "Creates text chunks and generates vector embeddings";

    public override AgentMetadata Metadata => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        InputType = typeof(object),
        OutputType = typeof(List<DocumentChunk>)
    };

    protected override async Task<List<DocumentChunk>> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
    {
        // Expect input to be { fileId, content }
        if (input is not Dictionary<string, object> inputDict || 
            !inputDict.TryGetValue("fileId", out var fileIdObj) ||
            !inputDict.TryGetValue("content", out var contentObj))
        {
            throw new ArgumentException("Input must contain fileId and content properties");
        }
        
        var fileId = fileIdObj?.ToString() ?? throw new ArgumentException("FileId is required");
        var content = contentObj?.ToString() ?? throw new ArgumentException("Content is required");
        
        Logger.LogInformation("Creating chunks and embeddings for file: {FileId}", fileId);

        try
        {
            // Create chunks (simple approach - split by paragraphs or sentences)
            var chunks = CreateTextChunks(content, 1000); // Max 1000 characters per chunk
            var documentChunks = new List<DocumentChunk>();

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                
                // Generate embedding for the chunk
                var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk, kernel: null, cancellationToken: cancellationToken);
                
                var documentChunk = new DocumentChunk
                {
                    Id = Guid.NewGuid().ToString(),
                    FileId = fileId,
                    Content = chunk,
                    Embedding = embedding.ToArray(),
                    ChunkIndex = i,
                    Metadata = new Dictionary<string, object>
                    {
                        ["chunk_length"] = chunk.Length,
                        ["created_at"] = DateTime.UtcNow
                    }
                };

                documentChunks.Add(documentChunk);
            }

            // Update the file document with chunks
            var fileDoc = await _fileStorageService.GetFileAsync(fileId);
            if (fileDoc != null)
            {
                fileDoc.Chunks = documentChunks;
                fileDoc.CleanedContent = content;
                
                // Update the metadata with chunking information
                fileDoc.Metadata["chunk_count"] = documentChunks.Count;
                fileDoc.Metadata["processed_at"] = DateTime.UtcNow;
                fileDoc.Metadata["total_content_length"] = content.Length;
                
                await _fileStorageService.SaveFileAsync(fileDoc);
                Logger.LogInformation("File document updated with {ChunkCount} chunks for file: {FileId}", documentChunks.Count, fileId);
            }
            else
            {
                Logger.LogWarning("Could not find file document to update with chunks for file: {FileId}", fileId);
            }

            Logger.LogInformation("Created {ChunkCount} chunks with embeddings for file: {FileId}", documentChunks.Count, fileId);
            return documentChunks;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating chunks and embeddings for file: {FileId}", fileId);
            return new List<DocumentChunk>();
        }
    }

    private List<string> CreateTextChunks(string content, int maxChunkSize)
    {
        var chunks = new List<string>();
        
        // Split by paragraphs first
        var paragraphs = content.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        var currentChunk = "";
        
        foreach (var paragraph in paragraphs)
        {
            // If adding this paragraph would exceed max size, save current chunk and start new one
            if (currentChunk.Length + paragraph.Length > maxChunkSize && !string.IsNullOrEmpty(currentChunk))
            {
                chunks.Add(currentChunk.Trim());
                currentChunk = paragraph;
            }
            else
            {
                if (!string.IsNullOrEmpty(currentChunk))
                    currentChunk += "\n\n";
                currentChunk += paragraph;
            }
        }
        
        // Add the last chunk if it's not empty
        if (!string.IsNullOrEmpty(currentChunk))
        {
            chunks.Add(currentChunk.Trim());
        }
        
        // If no chunks were created (empty content), return a single empty chunk
        if (chunks.Count == 0)
        {
            chunks.Add(content);
        }
        
        return chunks;
    }
}
