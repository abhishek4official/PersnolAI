using LocalChatApi.Models;
using LocalChatApi.Core;
using LocalChatApi.Services;

namespace LocalChatApi.Decisions;

/// <summary>
/// Decision that routes user requests based on detected intent
/// </summary>
public class IntentRoutingDecision : BaseDecision
{
    public IntentRoutingDecision(ILogger<IntentRoutingDecision> logger) : base(logger)
    {
    }

    public override DecisionMetadata Metadata => new DecisionMetadata
    {
        Id = "intent-routing-decision",
        Name = "Intent Routing Decision",
        Description = "Routes user requests to appropriate workflows based on detected intent",
        Version = "1.0.0",
        InputType = typeof(IntentResult),
        PossibleOutcomes = new List<string> { "chat", "file_upload", "file_chat", "unknown" }
    };

    protected override async Task<DecisionResult> ExecuteInternalAsync(object input, DecisionContext context, CancellationToken cancellationToken)
    {
        if (input is not IntentResult intentResult)
        {
            _logger.LogWarning("Invalid input type for IntentRoutingDecision: {InputType}", input?.GetType().Name ?? "null");
            return DecisionResult.CreateSuccess("chat", new { route = "chat", handler = "chat_workflow" });
        }

        _logger.LogInformation("Routing intent: {Intent} with confidence: {Confidence}", 
            intentResult.Intent, intentResult.Confidence);

        var route = intentResult.Intent.ToLowerInvariant() switch
        {
            "chat" => "chat",
            "file_upload" => "file_upload", 
            "file_chat" => "file_chat",
            _ => "chat" // Default to chat for unknown intents
        };

        var routingData = new
        {
            route = route,
            intent = intentResult.Intent,
            confidence = intentResult.Confidence,
            entities = intentResult.Entities,
            handler = $"{route}_workflow"
        };

        return DecisionResult.CreateSuccess(route, routingData);
    }
}

/// <summary>
/// Decision that checks if a file is available for chat operations
/// </summary>
public class FileAvailabilityDecision : BaseDecision
{
    private readonly IFileStorageService _fileStorageService;

    public FileAvailabilityDecision(ILogger<FileAvailabilityDecision> logger, IFileStorageService fileStorageService) 
        : base(logger)
    {
        _fileStorageService = fileStorageService;
    }

    public override DecisionMetadata Metadata => new DecisionMetadata
    {
        Id = "file-availability-decision",
        Name = "File Availability Decision",
        Description = "Checks if a file is available and processed for chat operations",
        Version = "1.0.0",
        InputType = typeof(FileChatRequest),
        PossibleOutcomes = new List<string> { "available", "not_found", "processing" }
    };

    protected override async Task<DecisionResult> ExecuteInternalAsync(object input, DecisionContext context, CancellationToken cancellationToken)
    {
        if (input is not FileChatRequest fileChatRequest)
        {
            _logger.LogWarning("Invalid input type for FileAvailabilityDecision: {InputType}", input?.GetType().Name ?? "null");
            return DecisionResult.Failure("Invalid input: Expected FileChatRequest");
        }

        _logger.LogInformation("Checking availability for file: {FileId}", fileChatRequest.FileId);

        try
        {
            var fileDoc = await _fileStorageService.GetFileAsync(fileChatRequest.FileId);
            
            if (fileDoc == null)
            {
                _logger.LogWarning("File not found: {FileId}", fileChatRequest.FileId);
                return DecisionResult.CreateSuccess("not_found", new 
                { 
                    status = "not_found", 
                    fileId = fileChatRequest.FileId,
                    message = "File not found"
                });
            }

            // Check if file has been processed (has chunks)
            if (fileDoc.Chunks == null || !fileDoc.Chunks.Any())
            {
                _logger.LogInformation("File is still processing: {FileId}", fileChatRequest.FileId);
                return DecisionResult.CreateSuccess("processing", new 
                { 
                    status = "processing", 
                    fileId = fileChatRequest.FileId,
                    message = "File is still being processed. Please try again in a moment."
                });
            }

            _logger.LogInformation("File is available for chat: {FileId} with {ChunkCount} chunks", 
                fileChatRequest.FileId, fileDoc.Chunks.Count);

            return DecisionResult.CreateSuccess("available", new 
            { 
                status = "available", 
                fileId = fileChatRequest.FileId,
                chunkCount = fileDoc.Chunks.Count,
                fileName = fileDoc.FileName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file availability: {FileId}", fileChatRequest.FileId);
            return DecisionResult.Failure($"Error checking file availability: {ex.Message}");
        }
    }
}

