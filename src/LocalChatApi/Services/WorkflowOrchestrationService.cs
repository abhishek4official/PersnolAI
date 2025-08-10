using LocalChatApi.Models;
using LocalChatApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace LocalChatApi.Services;

/// <summary>
/// Interface for workflow orchestration service using simplified workflows
/// </summary>
public interface IWorkflowOrchestrationService
{
    Task<ChatResponse> ProcessUserRequestAsync(UserRequest request);
    Task<FileProcessingResult> ProcessFileUploadAsync(FileUploadRequest request);
    Task<string> ProcessFileChatAsync(FileChatRequest request);
    
    // Workflow management methods
    Task<bool> SaveWorkflowAsync(string workflowName, string workflowJson);
    Task<string?> LoadWorkflowAsync(string workflowName);
    Task<List<string>> GetSavedWorkflowsAsync();
    Task<bool> DeleteWorkflowAsync(string workflowName);
    Task<string?> ExportWorkflowAsync(string workflowName);
    Task<bool> ImportWorkflowAsync(string workflowName, string json);
}

/// <summary>
/// Main workflow orchestration service using simplified workflows
/// </summary>
public class WorkflowOrchestrationService : IWorkflowOrchestrationService
{
    private readonly ILogger<WorkflowOrchestrationService> _logger;
    private readonly ISimpleWorkflowService _workflowService;
    private readonly IChatHistoryService _chatHistoryService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public WorkflowOrchestrationService(
        ILogger<WorkflowOrchestrationService> logger,
        ISimpleWorkflowService workflowService,
        IChatHistoryService chatHistoryService,
        IFileStorageService fileStorageService,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _workflowService = workflowService;
        _chatHistoryService = chatHistoryService;
        _fileStorageService = fileStorageService;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<ChatResponse> ProcessUserRequestAsync(UserRequest request)
    {
        _logger.LogInformation("Processing user request for session: {SessionId}", request.SessionId);

        try
        {
            // Ensure session exists
            await EnsureSessionExists(request.SessionId);

            // Execute intent detection workflow
            var intentWorkflowResult = await _workflowService.ExecuteIntentDetectionWorkflowAsync(request);

            if (!intentWorkflowResult.Success)
            {
                return new ChatResponse
                {
                    Response = "I apologize, but I encountered an error processing your request.",
                    Intent = "error",
                    SessionId = request.SessionId,
                    Metadata = new Dictionary<string, object> { ["error"] = intentWorkflowResult.ErrorMessage ?? "Unknown error" }
                };
            }

            // Extract intent from workflow result
            var intentResult = ExtractIntentFromWorkflowResult(intentWorkflowResult);
            
            // Route based on intent
            var response = await RouteToWorkflow(intentResult, request);
            
            // Save chat history
            await SaveChatHistory(request, response, intentResult.Intent);
            
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

            // Execute file upload workflow
            var workflowResult = await _workflowService.ExecuteFileUploadWorkflowAsync(request);

            if (!workflowResult.Success)
            {
                return new FileProcessingResult
                {
                    Success = false,
                    Message = workflowResult.ErrorMessage ?? "File upload workflow failed"
                };
            }

            var uploadResult = ExtractFileProcessingResultFromWorkflowResult(workflowResult);
            if (uploadResult == null || !uploadResult.Success)
            {
                return new FileProcessingResult
                {
                    Success = false,
                    Message = "File upload completed but result was invalid"
                };
            }

            // Start background processing with proper scoping
            _ = Task.Run(async () =>
            {
                // Add a small delay to ensure the HTTP response is sent first
                await Task.Delay(100);
                await ProcessFileInBackground(uploadResult.FileId);
            });

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

            // Execute file chat workflow
            var workflowResult = await _workflowService.ExecuteFileChatWorkflowAsync(request);

            if (!workflowResult.Success)
            {
                return workflowResult.ErrorMessage ?? "I encountered an error processing your file chat request.";
            }

            var response = ExtractStringResultFromWorkflowResult(workflowResult) ?? "I couldn't generate a response to your question.";
            
            // Save file chat history
            await SaveFileChatHistory(request, response);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file chat for session: {SessionId}", request.SessionId);
            return "I apologize, but I encountered an error while processing your question about the file.";
        }
    }

    // Workflow management methods
    public async Task<bool> SaveWorkflowAsync(string workflowName, string workflowJson)
    {
        try
        {
            return await _workflowService.ImportWorkflowAsync(workflowName, workflowJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving workflow: {WorkflowName}", workflowName);
            return false;
        }
    }

    public async Task<string?> LoadWorkflowAsync(string workflowName)
    {
        try
        {
            return await _workflowService.ExportWorkflowAsync(workflowName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading workflow: {WorkflowName}", workflowName);
            return null;
        }
    }

    public async Task<List<string>> GetSavedWorkflowsAsync()
    {
        return await _workflowService.GetSavedWorkflowsAsync();
    }

    public async Task<bool> DeleteWorkflowAsync(string workflowName)
    {
        try
        {
            var workflows = await _workflowService.GetSavedWorkflowsAsync();
            if (workflows.Contains(workflowName))
            {
                // Delete the file
                var workflowsDirectory = "SimpleWorkflows";
                var fileName = $"{SanitizeFileName(workflowName)}.json";
                var filePath = Path.Combine(workflowsDirectory, fileName);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted workflow: {WorkflowName}", workflowName);
                    return true;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting workflow: {WorkflowName}", workflowName);
            return false;
        }
    }

    public async Task<string?> ExportWorkflowAsync(string workflowName)
    {
        return await _workflowService.ExportWorkflowAsync(workflowName);
    }

    public async Task<bool> ImportWorkflowAsync(string workflowName, string json)
    {
        return await _workflowService.ImportWorkflowAsync(workflowName, json);
    }

    // Private helper methods
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
        var workflowResult = await _workflowService.ExecuteChatWorkflowAsync(request);
        return ExtractStringResultFromWorkflowResult(workflowResult) ?? "I couldn't generate a response.";
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

            // Create a new service scope for background processing
            using var scope = _serviceScopeFactory.CreateScope();
            var scopedFileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
            var scopedWorkflowService = scope.ServiceProvider.GetRequiredService<ISimpleWorkflowService>();

            var fileDoc = await scopedFileStorageService.GetFileAsync(fileId);
            if (fileDoc == null)
            {
                _logger.LogError("File not found for background processing: {FileId}", fileId);
                return;
            }

            // Execute the complete file processing workflow with scoped services
            _logger.LogInformation("Starting file processing workflow for: {FileId}", fileId);
            var workflowResult = await scopedWorkflowService.ExecuteFileProcessingWorkflowAsync(fileId);

            if (workflowResult.Success)
            {
                _logger.LogInformation("File processing workflow completed successfully for: {FileId} in {StepCount} steps", 
                    fileId, workflowResult.StepResults.Count);

                // Log step details
                foreach (var step in workflowResult.StepResults)
                {
                    _logger.LogInformation("Step '{StepName}' completed in {ExecutionTime}ms - Success: {Success}", 
                        step.StepName, step.ExecutionTime.TotalMilliseconds, step.Success);
                }

                // Extract chunk count from final result if available
                if (workflowResult.FinalResult is List<DocumentChunk> chunks)
                {
                    _logger.LogInformation("File processing completed with {ChunkCount} chunks created for file: {FileId}", 
                        chunks.Count, fileId);
                }
            }
            else
            {
                _logger.LogError("File processing workflow failed for: {FileId}. Error: {ErrorMessage}", 
                    fileId, workflowResult.ErrorMessage);
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

    private async Task SaveChatHistory(UserRequest request, string response, string intent)
    {
        try
        {
            // Save user message
            await _chatHistoryService.SaveMessageAsync(new ChatMessage
            {
                SessionId = request.SessionId,
                Role = "user",
                Content = request.Input,
                Timestamp = DateTime.UtcNow,
                Intent = intent
            });

            // Save assistant response
            await _chatHistoryService.SaveMessageAsync(new ChatMessage
            {
                SessionId = request.SessionId,
                Role = "assistant",
                Content = response,
                Timestamp = DateTime.UtcNow,
                Intent = intent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving chat history for session: {SessionId}", request.SessionId);
        }
    }

    private async Task SaveFileChatHistory(FileChatRequest request, string response)
    {
        try
        {
            // Save user question
            await _chatHistoryService.SaveMessageAsync(new ChatMessage
            {
                SessionId = request.SessionId,
                Role = "user",
                Content = $"[File: {request.FileId}] {request.Question}",
                Timestamp = DateTime.UtcNow,
                Intent = "file_chat"
            });

            // Save assistant response
            await _chatHistoryService.SaveMessageAsync(new ChatMessage
            {
                SessionId = request.SessionId,
                Role = "assistant",
                Content = response,
                Timestamp = DateTime.UtcNow,
                Intent = "file_chat"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file chat history for session: {SessionId}", request.SessionId);
        }
    }

    // Helper methods to extract results from workflow results
    private IntentResult ExtractIntentFromWorkflowResult(SimpleWorkflowResult workflowResult)
    {
        // Extract intent from the first step result (intent detection)
        var intentStep = workflowResult.StepResults.FirstOrDefault(s => s.StepName.Contains("Intent"));
        
        if (intentStep?.Result is IntentResult intentResult)
        {
            return intentResult;
        }

        // Default intent if extraction fails
        return new IntentResult
        {
            Intent = "chat",
            Confidence = 0.8,
            Entities = new Dictionary<string, object>(),
            OriginalInput = ""
        };
    }

    private string? ExtractStringResultFromWorkflowResult(SimpleWorkflowResult workflowResult)
    {
        // Get the final result or the last successful step result
        if (workflowResult.FinalResult is string stringResult)
        {
            return stringResult;
        }

        var lastStep = workflowResult.StepResults.LastOrDefault(s => s.Success);
        return lastStep?.Result as string;
    }

    private FileProcessingResult? ExtractFileProcessingResultFromWorkflowResult(SimpleWorkflowResult workflowResult)
    {
        // Extract file processing result
        if (workflowResult.FinalResult is FileProcessingResult fileResult)
        {
            return fileResult;
        }

        var uploadStep = workflowResult.StepResults.FirstOrDefault(s => s.StepName.Contains("Upload"));
        return uploadStep?.Result as FileProcessingResult;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }
}
