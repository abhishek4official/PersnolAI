using LocalChatApi.Core;
using LocalChatApi.Models;
using LocalChatApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;

#pragma warning disable SKEXP0001 // Suppress experimental API warnings

namespace LocalChatApi.Agents;

/// <summary>
/// Agent that handles file uploads and generates unique file IDs
/// </summary>
public class FileUploadAgent : BaseAgent<FileProcessingResult>
{
    private readonly IFileStorageService _fileStorageService;

    public FileUploadAgent(ILogger<FileUploadAgent> logger, Kernel kernel, IFileStorageService fileStorageService)
        : base(logger, kernel)
    {
        _fileStorageService = fileStorageService;
    }

    public override AgentMetadata Metadata => new AgentMetadata
    {
        Id = "file-upload-agent",
        Name = "File Upload Agent",
        Description = "Handles file uploads and generates unique file IDs",
        Version = "1.0.0",
        InputType = typeof(FileUploadRequest),
        OutputType = typeof(FileProcessingResult)
    };

    protected override async Task<FileProcessingResult> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
    {
        if (input is not FileUploadRequest uploadRequest)
        {
            throw new ArgumentException("Input must be a FileUploadRequest", nameof(input));
        }

        try
        {
            // Generate unique file ID
            var fileId = GenerateFileId();

            // Save physical file to disk first
            var filePath = await _fileStorageService.SavePhysicalFileAsync(uploadRequest.File, fileId);

            // Create file document (metadata only - no content stored in MongoDB)
            var fileDoc = new FileDocument
            {
                FileId = fileId,
                SessionId = uploadRequest.SessionId,
                FileName = uploadRequest.File.FileName,
                ContentType = uploadRequest.File.ContentType,
                FilePath = filePath,
                FileSize = uploadRequest.File.Length,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["size"] = uploadRequest.File.Length,
                    ["uploadedAt"] = DateTime.UtcNow.ToString("O"),
                    ["originalFileName"] = uploadRequest.File.FileName
                }
            };

            // Save metadata to MongoDB
            await _fileStorageService.SaveFileAsync(fileDoc);

            _logger.LogInformation("File uploaded successfully: {FileId}, Name: {FileName}, Size: {Size} bytes, Path: {FilePath}",
                fileId, uploadRequest.File.FileName, uploadRequest.File.Length, filePath);

            return new FileProcessingResult
            {
                Success = true,
                FileId = fileId,
                Message = $"File '{uploadRequest.File.FileName}' uploaded successfully. File ID: {fileId}",
                ChunkCount = 0 // Will be updated after processing
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", uploadRequest.File.FileName);
            return new FileProcessingResult
            {
                Success = false,
                Message = $"Failed to upload file: {ex.Message}"
            };
        }
    }

    private static string GenerateFileId()
    {
        return DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
    }
}

/// <summary>
/// Agent that reads and parses file content from various formats
/// </summary>
public class FileReaderAgent : BaseAgent<string>
{
    public FileReaderAgent(ILogger<FileReaderAgent> logger, Kernel kernel)
        : base(logger, kernel)
    {
    }

    public override AgentMetadata Metadata => new AgentMetadata
    {
        Id = "file-reader-agent",
        Name = "File Reader Agent",
        Description = "Reads and parses content from various file formats using the file storage service",
        Version = "1.0.0",
        InputType = typeof(FileDocument),
        OutputType = typeof(string)
    };

    protected override async Task<string> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
    {
        FileDocument? fileDoc = null;

        // Handle different input types
        if (input is FileDocument directFileDoc)
        {
            fileDoc = directFileDoc;
        }
        else if (input is Dictionary<string, object> inputData && inputData.TryGetValue("fileId", out var fileIdObj))
        {
            var fileId = fileIdObj.ToString();
            if (!string.IsNullOrEmpty(fileId))
            {
                var fileStorageService = context.ServiceProvider?.GetService(typeof(IFileStorageService)) as IFileStorageService;
                if (fileStorageService != null)
                {
                    fileDoc = await fileStorageService.GetFileAsync(fileId);
                }
            }
        }

        if (fileDoc == null)
        {
            throw new ArgumentException("Input must be a FileDocument or contain a valid 'fileId'", nameof(input));
        }

        try
        {
            // Read file content from disk using the file storage service
            var fileStorageService = context.ServiceProvider?.GetService(typeof(IFileStorageService)) as IFileStorageService;
            if (fileStorageService == null)
            {
                throw new InvalidOperationException("IFileStorageService not available in context");
            }

            var content = await fileStorageService.ReadFileContentAsync(fileDoc.FileId);
            if (string.IsNullOrEmpty(content))
            {
                throw new InvalidOperationException($"Could not read content for file: {fileDoc.FileName}");
            }

            _logger.LogInformation("Extracted {ContentLength} characters from file: {FileName}",
                content.Length, fileDoc.FileName);

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file: {FileName}", fileDoc.FileName);
            throw;
        }
    }
}

/// <summary>
/// Agent that extracts and validates data from file content
/// </summary>
public class DataExtractionAgent : BaseAgent<string>
{
    public DataExtractionAgent(ILogger<DataExtractionAgent> logger, Kernel kernel)
        : base(logger, kernel)
    {
    }

    public override AgentMetadata Metadata => new AgentMetadata
    {
        Id = "data-extraction-agent",
        Name = "Data Extraction Agent",
        Description = "Extracts and validates structured data from file content",
        Version = "1.0.0",
        InputType = typeof(string),
        OutputType = typeof(string)
    };

    protected override async Task<string> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
    {
        if (input is not string content)
        {
            throw new ArgumentException("Input must be a string", nameof(input));
        }

        try
        {
            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

         

            _logger.LogInformation("Data extraction completed. Original length: {OriginalLength}, Cleaned length: {CleanedLength}",
                content.Length, content.Length);

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data extraction");
            // Return original content if cleaning fails
            return content;
        }
    }
}

/// <summary>
/// Agent that creates text chunks and embeddings for RAG
/// </summary>
public class ChunkingEmbeddingAgent : BaseAgent<List<DocumentChunk>>
{
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IFileStorageService _fileStorageService;

    public ChunkingEmbeddingAgent(ILogger<ChunkingEmbeddingAgent> logger, Kernel kernel, 
        ITextEmbeddingGenerationService embeddingService, IFileStorageService fileStorageService)
        : base(logger, kernel)
    {
        _embeddingService = embeddingService;
        _fileStorageService = fileStorageService;
    }

    public override AgentMetadata Metadata => new AgentMetadata
    {
        Id = "chunking-embedding-agent",
        Name = "Chunking & Embedding Agent",
        Description = "Creates text chunks and generates embeddings for RAG",
        Version = "1.0.0",
        InputType = typeof(Dictionary<string, object>),
        OutputType = typeof(List<DocumentChunk>)
    };

    protected override async Task<List<DocumentChunk>> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
    {
        string? fileId = null;
        string? content = null;

        // Handle different input types
        if (input is Dictionary<string, object> inputData)
        {
            inputData.TryGetValue("fileId", out var fileIdObj);
            inputData.TryGetValue("content", out var contentObj);
            
            fileId = fileIdObj?.ToString();
            content = contentObj?.ToString();
        }
        else if (input is string directContent)
        {
            content = directContent;
            // Try to get fileId from context
            if (context.Variables.TryGetValue("fileId", out var contextFileId))
            {
                fileId = contextFileId?.ToString();
            }
        }

        if (string.IsNullOrEmpty(fileId) || string.IsNullOrEmpty(content))
        {
            throw new ArgumentException("Input must contain both 'fileId' and 'content'", nameof(input));
        }

        try
        {
            // Create chunks
            var chunks = CreateTextChunks(content, chunkSize: 1000, overlap: 200);
            var documentChunks = new List<DocumentChunk>();

            _logger.LogInformation("Created {ChunkCount} chunks for file: {FileId}", chunks.Count, fileId);

            // Generate embeddings for each chunk
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk, cancellationToken: cancellationToken);

                var documentChunk = new DocumentChunk
                {
                    Id = $"{fileId}-chunk-{i:D3}",
                    Content = chunk,
                    Embedding = embedding.ToArray(),
                    ChunkIndex = i,
                    Metadata = new Dictionary<string, object>
                    {
                        ["fileId"] = fileId,
                        ["chunkIndex"] = i,
                        ["length"] = chunk.Length
                    }
                };

                documentChunks.Add(documentChunk);
            }

            // Update file document with chunks
            await _fileStorageService.UpdateFileChunksAsync(fileId, documentChunks);

            _logger.LogInformation("Generated embeddings for {ChunkCount} chunks for file: {FileId}", 
                documentChunks.Count, fileId);

            return documentChunks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating chunks and embeddings for file: {FileId}", fileId);
            throw;
        }
    }

    private List<string> CreateTextChunks(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        
        var currentChunk = "";
        
        foreach (var sentence in sentences)
        {
            var trimmedSentence = sentence.Trim();
            if (string.IsNullOrEmpty(trimmedSentence))
                continue;

            var potentialChunk = currentChunk + trimmedSentence + ". ";
            
            if (potentialChunk.Length > chunkSize && !string.IsNullOrEmpty(currentChunk))
            {
                chunks.Add(currentChunk.Trim());
                
                // Start new chunk with overlap
                var words = currentChunk.Split(' ');
                var overlapWords = words.TakeLast(Math.Min(overlap / 10, words.Length / 2)).ToArray();
                currentChunk = string.Join(" ", overlapWords) + " " + trimmedSentence + ". ";
            }
            else
            {
                currentChunk = potentialChunk;
            }
        }
        
        if (!string.IsNullOrEmpty(currentChunk.Trim()))
        {
            chunks.Add(currentChunk.Trim());
        }
        
        return chunks;
    }
}

/// <summary>
/// Agent that handles RAG-based file chat using vector similarity
/// </summary>
public class FileChatAgent : BaseAgent<string>
{
    private readonly IFileStorageService _fileStorageService;
    private readonly ITextEmbeddingGenerationService _embeddingService;

    public FileChatAgent(ILogger<FileChatAgent> logger, Kernel kernel, 
        IFileStorageService fileStorageService, ITextEmbeddingGenerationService embeddingService)
        : base(logger, kernel)
    {
        _fileStorageService = fileStorageService;
        _embeddingService = embeddingService;
    }

    public override AgentMetadata Metadata => new AgentMetadata
    {
        Id = "file-chat-agent",
        Name = "File Chat Agent",
        Description = "Handles RAG-based file chat using vector similarity search",
        Version = "1.0.0",
        InputType = typeof(object), // Can handle FileChatRequest or DecisionResult
        OutputType = typeof(string)
    };

    protected override async Task<string> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
    {
        FileChatRequest? chatRequest = null;

        // Handle different input types - the agent might receive the original FileChatRequest 
        // or a DecisionResult from the FileAvailabilityDecision
        if (input is FileChatRequest directRequest)
        {
            chatRequest = directRequest;
        }
        else if (input is DecisionResult decisionResult)
        {
            // If we got a decision result, we need to get the original request from the workflow variables
            // or reconstruct it from the decision data
            if (context.Variables.TryGetValue("originalInput", out var originalInputObj) && 
                originalInputObj is FileChatRequest originalRequest)
            {
                chatRequest = originalRequest;
            }
            else
            {
                // Try to extract from decision result data
                if (decisionResult.Data != null)
                {
                    var dataType = decisionResult.Data.GetType();
                    var fileIdProperty = dataType.GetProperty("fileId");
                    
                    if (fileIdProperty != null && fileIdProperty.GetValue(decisionResult.Data) is string fileId)
                    {
                        // We have fileId but need to find the question and sessionId from context or return an error message
                        if (context.Variables.TryGetValue("question", out var questionObj) && questionObj is string question &&
                            context.Variables.TryGetValue("sessionId", out var sessionIdObj) && sessionIdObj is string sessionId)
                        {
                            chatRequest = new FileChatRequest
                            {
                                FileId = fileId,
                                Question = question,
                                SessionId = sessionId
                            };
                        }
                    }
                }
            }

            // If the decision indicates the file is not available or still processing, return that message
            if (decisionResult.NextStep == "not_found")
            {
                return "Sorry, the file you're asking about was not found. Please check the File ID and try again.";
            }
            else if (decisionResult.NextStep == "processing")
            {
                return "The file is still being processed. Please try again in a moment.";
            }
        }

        if (chatRequest == null)
        {
            throw new ArgumentException("Input must be a FileChatRequest or a valid DecisionResult with file information", nameof(input));
        }

        try
        {
            // Get file document
            var fileDoc = await _fileStorageService.GetFileAsync(chatRequest.FileId);
            if (fileDoc?.Chunks == null || !fileDoc.Chunks.Any())
            {
                return "Sorry, the file is not available or hasn't been processed yet. Please try again later.";
            }

            // Generate embedding for the question
            var questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(chatRequest.Question, cancellationToken: cancellationToken);

            // Find most relevant chunks using cosine similarity
            var relevantChunks = FindRelevantChunks(fileDoc.Chunks, questionEmbedding.ToArray(), topK: 3);

            if (!relevantChunks.Any())
            {
                return "I couldn't find relevant information in the file to answer your question.";
            }

            // Create RAG prompt with context
            var ragContext = string.Join("\n\n", relevantChunks.Select(chunk => $"Context {chunk.ChunkIndex + 1}: {chunk.Content}"));
            
            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

            var prompt = $@"
Based on the following context from the file '{fileDoc.FileName}', please answer the user's question. If the context doesn't contain enough information to answer the question, say so clearly.

Context:
{ragContext}

Question: {chatRequest.Question}

Answer:";

            var response = await chatCompletionService.GetChatMessageContentAsync(prompt, cancellationToken: cancellationToken);
            var answer = response.Content ?? "I couldn't generate an answer to your question.";

            _logger.LogInformation("Generated RAG response for file: {FileId}, Question length: {QuestionLength}, Answer length: {AnswerLength}",
                chatRequest.FileId, chatRequest.Question.Length, answer.Length);

            return answer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file chat for file: {FileId}", chatRequest?.FileId ?? "unknown");
            return "I encountered an error while processing your question. Please try again.";
        }
    }

    private List<DocumentChunk> FindRelevantChunks(List<DocumentChunk> chunks, float[] queryEmbedding, int topK = 3)
    {
        var chunkSimilarities = new List<(DocumentChunk chunk, double similarity)>();

        foreach (var chunk in chunks)
        {
            if (chunk.Embedding != null && chunk.Embedding.Length > 0)
            {
                var similarity = CosineSimilarity(queryEmbedding, chunk.Embedding);
                chunkSimilarities.Add((chunk, similarity));
            }
        }

        return chunkSimilarities
            .OrderByDescending(x => x.similarity)
            .Take(topK)
            .Select(x => x.chunk)
            .ToList();
    }

    private static double CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            return 0;

        var dotProduct = 0.0;
        var magnitudeA = 0.0;
        var magnitudeB = 0.0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }
}
