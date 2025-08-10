using ElsaWorkflowAgent.Decisions;
using Microsoft.Extensions.Logging;
using LocalChatApi.Models;

namespace LocalChatApi.Decisions;

/// <summary>
/// Intent routing decision that determines which workflow to execute based on detected intent
/// </summary>
public class IntentRoutingDecision : BaseDecision
{
    private readonly ILogger<IntentRoutingDecision> _logger;

    public IntentRoutingDecision(ILogger<IntentRoutingDecision> logger) : base(logger)
    {
        _logger = logger;
    }

    public override string Id => "intent-routing-decision";
    public override string Name => "Intent Routing Decision";
    public override string Description => "Routes requests to appropriate workflow based on detected intent";

    public override DecisionMetadata Metadata => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        InputType = typeof(IntentResult)
    };

    protected override Task<DecisionResult> ExecuteInternalAsync(object input, DecisionContext context, CancellationToken cancellationToken)
    {
        var intentResult = input as IntentResult ?? throw new ArgumentException("Input must be IntentResult");
        
        _logger.LogInformation("Routing based on intent: {Intent} with confidence: {Confidence}", 
            intentResult.Intent, intentResult.Confidence);

        var nextStep = intentResult.Intent.ToLowerInvariant() switch
        {
            "chat" => "chat_workflow",
            "file_upload" => "file_upload_workflow", 
            "file_chat" => "file_chat_workflow",
            _ => "chat_workflow" // Default fallback
        };

        var result = new DecisionResult
        {
            NextStep = nextStep,
            Success = true,
            Data = new Dictionary<string, object>
            {
                ["intent"] = intentResult.Intent,
                ["confidence"] = intentResult.Confidence,
                ["entities"] = intentResult.Entities,
                ["original_input"] = intentResult.OriginalInput
            }
        };

        _logger.LogInformation("Intent routed to: {NextStep}", nextStep);
        return Task.FromResult(result);
    }
}

/// <summary>
/// File availability decision that checks if a file ID is available in the session
/// </summary>
public class FileAvailabilityDecision : BaseDecision
{
    private readonly Services.IFileStorageService _fileStorageService;
    private readonly ILogger<FileAvailabilityDecision> _logger;

    public FileAvailabilityDecision(ILogger<FileAvailabilityDecision> logger, Services.IFileStorageService fileStorageService) 
        : base(logger)
    {
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public override string Id => "file-availability-decision";
    public override string Name => "File Availability Decision";
    public override string Description => "Checks if a file ID is available for chat";

    public override DecisionMetadata Metadata => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        InputType = typeof(FileChatRequest)
    };

    protected override async Task<DecisionResult> ExecuteInternalAsync(object input, DecisionContext context, CancellationToken cancellationToken)
    {
        var chatRequest = input as FileChatRequest ?? throw new ArgumentException("Input must be FileChatRequest");
        
        _logger.LogInformation("Checking file availability for ID: {FileId}", chatRequest.FileId);

        try
        {
            var fileDoc = await _fileStorageService.GetFileAsync(chatRequest.FileId);
            
            if (fileDoc == null)
            {
                return new DecisionResult
                {
                    NextStep = "request_file_id",
                    Success = false,
                    Data = new Dictionary<string, object> { ["file_id"] = chatRequest.FileId }
                };
            }

            if (!fileDoc.Chunks.Any())
            {
                return new DecisionResult
                {
                    NextStep = "file_processing_incomplete",
                    Success = false,
                    Data = new Dictionary<string, object> 
                    { 
                        ["file_id"] = chatRequest.FileId,
                        ["file_name"] = fileDoc.FileName
                    }
                };
            }

            return new DecisionResult
            {
                NextStep = "proceed_with_file_chat",
                Success = true,
                Data = new Dictionary<string, object> 
                { 
                    ["file_id"] = chatRequest.FileId,
                    ["file_name"] = fileDoc.FileName,
                    ["chunk_count"] = fileDoc.Chunks.Count
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file availability for ID: {FileId}", chatRequest.FileId);
            
            return new DecisionResult
            {
                NextStep = "error_handling",
                Success = false,
                Data = new Dictionary<string, object> { ["error"] = ex.Message }
            };
        }
    }
}
