using LocalChatApi.Models;
using LocalChatApi.Agents;
using LocalChatApi.Decisions;
using LocalChatApi.Services;
using ElsaWorkflowAgent.Workflows;
using ElsaWorkflowAgent.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Embeddings;

namespace LocalChatApi.Services;

/// <summary>
/// Interface for workflow orchestration service
/// </summary>
public interface IWorkflowOrchestrationService
{
    Task<ChatResponse> ProcessUserRequestAsync(UserRequest request);
    Task<FileProcessingResult> ProcessFileUploadAsync(FileUploadRequest request);
    Task<string> ProcessFileChatAsync(FileChatRequest request);
}

/// <summary>
/// Main workflow orchestration service that coordinates all agents and workflows
/// </summary>
public class WorkflowOrchestrationService : IWorkflowOrchestrationService
{
    private readonly ILogger<WorkflowOrchestrationService> _logger;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IChatHistoryService _chatHistoryService;
    private readonly IFileStorageService _fileStorageService;
    
    // Agents
    private readonly IntentDetectionAgent _intentDetectionAgent;
    private readonly ChatAgent _chatAgent;
    private readonly FileUploadAgent _fileUploadAgent;
    private readonly FileReaderAgent _fileReaderAgent;
    private readonly DataExtractionAgent _dataExtractionAgent;
    private readonly ChunkingEmbeddingAgent _chunkingEmbeddingAgent;
    private readonly FileChatAgent _fileChatAgent;
    
    // Decisions
    private readonly IntentRoutingDecision _intentRoutingDecision;
    private readonly FileAvailabilityDecision _fileAvailabilityDecision;

    public WorkflowOrchestrationService(
        ILogger<WorkflowOrchestrationService> logger,
        IWorkflowEngine workflowEngine,
        IChatHistoryService chatHistoryService,
        IFileStorageService fileStorageService,
        IntentDetectionAgent intentDetectionAgent,
        ChatAgent chatAgent,
        FileUploadAgent fileUploadAgent,
        FileReaderAgent fileReaderAgent,
        DataExtractionAgent dataExtractionAgent,
        ChunkingEmbeddingAgent chunkingEmbeddingAgent,
        FileChatAgent fileChatAgent,
        IntentRoutingDecision intentRoutingDecision,
        FileAvailabilityDecision fileAvailabilityDecision)
    {
        _logger = logger;
        _workflowEngine = workflowEngine;
        _chatHistoryService = chatHistoryService;
        _fileStorageService = fileStorageService;
        _intentDetectionAgent = intentDetectionAgent;
        _chatAgent = chatAgent;
        _fileUploadAgent = fileUploadAgent;
        _fileReaderAgent = fileReaderAgent;
        _dataExtractionAgent = dataExtractionAgent;
        _chunkingEmbeddingAgent = chunkingEmbeddingAgent;
        _fileChatAgent = fileChatAgent;
        _intentRoutingDecision = intentRoutingDecision;
        _fileAvailabilityDecision = fileAvailabilityDecision;
    }

    public async Task<ChatResponse> ProcessUserRequestAsync(UserRequest request)
    {
        _logger.LogInformation("Processing user request for session: {SessionId}", request.SessionId);

        try
        {
            // Ensure session exists
            await EnsureSessionExists(request.SessionId);

            // Create intent detection workflow
            var workflow = WorkflowBuilderFactory
                .CreateBuilder("IntentDetectionWorkflow", "Main intent detection and routing workflow")
                .WithVariable("userRequest", request)
                .AddAgent(_intentDetectionAgent, request)
                .AddDecision(_intentRoutingDecision, "routing_input") // Will use result from previous step
                .Build();

            var workflowResult = await _workflowEngine.ExecuteAsync(workflow, request);

            if (!workflowResult.Success)
            {
                return new ChatResponse
                {
                    Response = "I apologize, but I encountered an error processing your request.",
                    Intent = "error",
                    SessionId = request.SessionId,
                    Metadata = new Dictionary<string, object> { ["error"] = workflowResult.ErrorMessage ?? "Unknown error" }
                };
            }

            // Extract intent result from workflow
            var intentResult = workflowResult.StepResults.FirstOrDefault()?.Result as IntentResult;
            if (intentResult == null)
            {
                return new ChatResponse
                {
                    Response = "I couldn't determine your intent. Could you please rephrase your request?",
                    Intent = "unknown",
                    SessionId = request.SessionId
                };
            }

            // Route to appropriate sub-workflow based on intent
            var response = await RouteToWorkflow(intentResult, request);
            
            return new ChatResponse
            {
                Response = response,
                Intent = intentResult.Intent,
                SessionId = request.SessionId,
                Metadata = new Dictionary<string, object>
                {
                    ["confidence"] = intentResult.Confidence,
                    ["entities"] = intentResult.Entities
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user request for session: {SessionId}", request.SessionId);
            return new ChatResponse
            {
                Response = "I apologize, but I encountered an unexpected error. Please try again.",
                Intent = "error",
                SessionId = request.SessionId
            };
        }
    }

    public async Task<FileProcessingResult> ProcessFileUploadAsync(FileUploadRequest request)
    {
        _logger.LogInformation("Processing file upload for session: {SessionId}", request.SessionId);

        try
        {
            // Ensure session exists
            await EnsureSessionExists(request.SessionId);

            // Create file upload workflow
            var workflow = WorkflowBuilderFactory
                .CreateBuilder("FileUploadWorkflow", "File upload and processing workflow")
                .WithVariable("fileRequest", request)
                .AddAgent(_fileUploadAgent, request)
                .Build();

            var workflowResult = await _workflowEngine.ExecuteAsync(workflow, request);

            if (!workflowResult.Success)
            {
                return new FileProcessingResult
                {
                    Success = false,
                    Message = workflowResult.ErrorMessage ?? "File upload failed"
                };
            }

            var uploadResult = workflowResult.FinalResult as FileProcessingResult;
            if (uploadResult == null || !uploadResult.Success)
            {
                return new FileProcessingResult
                {
                    Success = false,
                    Message = "File upload completed but result was invalid"
                };
            }

            // Start background processing workflow
            _ = Task.Run(async () => await ProcessFileInBackground(uploadResult.FileId));

            return uploadResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file upload for session: {SessionId}", request.SessionId);
            return new FileProcessingResult
            {
                Success = false,
                Message = $"File upload failed: {ex.Message}"
            };
        }
    }

    public async Task<string> ProcessFileChatAsync(FileChatRequest request)
    {
        _logger.LogInformation("Processing file chat for session: {SessionId}, file: {FileId}", 
            request.SessionId, request.FileId);

        try
        {
            // Ensure session exists
            await EnsureSessionExists(request.SessionId);

            // Create file chat workflow with availability check
            var workflow = WorkflowBuilderFactory
                .CreateBuilder("FileChatWorkflow", "File chat workflow with availability check")
                .WithVariable("chatRequest", request)
                .AddDecision(_fileAvailabilityDecision, request)
                .AddAgent(_fileChatAgent, request)
                .Build();

            var workflowResult = await _workflowEngine.ExecuteAsync(workflow, request);

            if (!workflowResult.Success)
            {
                return workflowResult.ErrorMessage ?? "I encountered an error processing your file chat request.";
            }

            return workflowResult.FinalResult as string ?? "I couldn't generate a response to your question.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file chat for session: {SessionId}", request.SessionId);
            return "I apologize, but I encountered an error while processing your question about the file.";
        }
    }

    private async Task<string> RouteToWorkflow(IntentResult intentResult, UserRequest request)
    {
        return intentResult.Intent.ToLowerInvariant() switch
        {
            "chat" => await ExecuteChatWorkflow(request),
            "file_upload" => "I see you want to upload a file. Please use the file upload endpoint to upload your file.",
            "file_chat" => await HandleFileChatIntent(intentResult, request),
            _ => await ExecuteChatWorkflow(request)
        };
    }

    private async Task<string> ExecuteChatWorkflow(UserRequest request)
    {
        var workflow = WorkflowBuilderFactory
            .CreateBuilder("ChatWorkflow", "Normal chat conversation workflow")
            .WithVariable("userRequest", request)
            .AddAgent(_chatAgent, request)
            .Build();

        var result = await _workflowEngine.ExecuteAsync(workflow, request);
        return result.FinalResult as string ?? "I couldn't generate a response.";
    }

    private async Task<string> HandleFileChatIntent(IntentResult intentResult, UserRequest request)
    {
        // Extract file ID from entities or ask user to provide it
        if (intentResult.Entities.TryGetValue("file_id", out var fileIdObj) && fileIdObj is string fileId)
        {
            var fileChatRequest = new FileChatRequest
            {
                Question = request.Input,
                FileId = fileId,
                SessionId = request.SessionId
            };
            
            return await ProcessFileChatAsync(fileChatRequest);
        }
        else
        {
            return "I see you want to ask about a file. Please provide the File ID (e.g., 'What does file ABC123 say about...?').";
        }
    }

    private async Task ProcessFileInBackground(string fileId)
    {
        try
        {
            _logger.LogInformation("Starting background processing for file: {FileId}", fileId);

            var fileDoc = await _fileStorageService.GetFileAsync(fileId);
            if (fileDoc == null)
            {
                _logger.LogError("File not found for background processing: {FileId}", fileId);
                return;
            }

            // 🔥 NEW: Enhanced file processing workflow with proper data flow
            var workflow = WorkflowBuilderFactory
                .CreateBuilder("FileProcessingWorkflow", "Background file processing workflow")
                .WithVariable("fileDoc", fileDoc)
                .AddAgent(_fileReaderAgent, fileDoc)                                                    // Step 1: Read file content
                .AddAgentWithPreviousResult(_dataExtractionAgent)                                       // Step 2: x => x (previous result)
                .AddAgentWithTransform(_chunkingEmbeddingAgent, content => new Dictionary<string, object>
                { 
                    ["fileId"] = fileDoc.FileId, 
                    ["content"] = (string)content 
                })                         // Step 3: x => transform(x)
                .Build();

            var result = await _workflowEngine.ExecuteAsync(workflow, fileDoc);

            if (result.Success)
            {
                _logger.LogInformation("Background file processing completed for: {FileId}", fileId);
            }
            else
            {
                _logger.LogError("Background file processing failed for {FileId}: {Error}", fileId, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in background file processing for: {FileId}", fileId);
        }
    }

    private async Task EnsureSessionExists(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return;

        var session = await _chatHistoryService.GetSessionAsync(sessionId);
        if (session == null)
        {
            await _chatHistoryService.CreateSessionAsync("default_user", $"Session {sessionId}");
        }
    }
}
