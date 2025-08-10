using Microsoft.AspNetCore.Mvc;
using LocalChatApi.Models;
using LocalChatApi.Services;

namespace LocalChatApi.Controllers;

/// <summary>
/// Main chat controller that handles all user interactions through the intent-driven workflow
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILogger<ChatController> _logger;
    private readonly IWorkflowOrchestrationService _orchestrationService;
    private readonly IChatHistoryService _chatHistoryService;

    public ChatController(
        ILogger<ChatController> logger,
        IWorkflowOrchestrationService orchestrationService,
        IChatHistoryService chatHistoryService)
    {
        _logger = logger;
        _orchestrationService = orchestrationService;
        _chatHistoryService = chatHistoryService;
    }

    /// <summary>
    /// Process user message through intent-driven workflow
    /// </summary>
    [HttpPost("message")]
    public async Task<ActionResult<ApiResponse<ChatResponse>>> SendMessage([FromBody] UserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Input))
            {
                return BadRequest(new ApiResponse<ChatResponse>
                {
                    Success = false,
                    Message = "Input cannot be empty",
                    Errors = new List<string> { "Input is required" }
                });
            }

            // Generate session ID if not provided
            if (string.IsNullOrEmpty(request.SessionId))
            {
                request.SessionId = Guid.NewGuid().ToString();
            }

            var response = await _orchestrationService.ProcessUserRequestAsync(request);

            return Ok(new ApiResponse<ChatResponse>
            {
                Success = true,
                Message = "Message processed successfully",
                Data = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new ApiResponse<ChatResponse>
            {
                Success = false,
                Message = "Internal server error",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Upload a file for processing and chat
    /// </summary>
    [HttpPost("upload")]
    public async Task<ActionResult<ApiResponse<FileProcessingResult>>> UploadFile([FromForm] FileUploadRequest request)
    {
        try
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new ApiResponse<FileProcessingResult>
                {
                    Success = false,
                    Message = "No file provided",
                    Errors = new List<string> { "File is required" }
                });
            }

            // Generate session ID if not provided
            if (string.IsNullOrEmpty(request.SessionId))
            {
                request.SessionId = Guid.NewGuid().ToString();
            }

            var result = await _orchestrationService.ProcessFileUploadAsync(request);

            return Ok(new ApiResponse<FileProcessingResult>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file upload");
            return StatusCode(500, new ApiResponse<FileProcessingResult>
            {
                Success = false,
                Message = "Internal server error",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Ask questions about an uploaded file (RAG)
    /// </summary>
    [HttpPost("file-chat")]
    public async Task<ActionResult<ApiResponse<string>>> ChatWithFile([FromBody] FileChatRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest(new ApiResponse<string>
                {
                    Success = false,
                    Message = "Question cannot be empty",
                    Errors = new List<string> { "Question is required" }
                });
            }

            if (string.IsNullOrWhiteSpace(request.FileId))
            {
                return BadRequest(new ApiResponse<string>
                {
                    Success = false,
                    Message = "File ID cannot be empty",
                    Errors = new List<string> { "File ID is required" }
                });
            }

            // Generate session ID if not provided
            if (string.IsNullOrEmpty(request.SessionId))
            {
                request.SessionId = Guid.NewGuid().ToString();
            }

            var response = await _orchestrationService.ProcessFileChatAsync(request);

            return Ok(new ApiResponse<string>
            {
                Success = true,
                Message = "File chat processed successfully",
                Data = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file chat");
            return StatusCode(500, new ApiResponse<string>
            {
                Success = false,
                Message = "Internal server error",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get chat history for a session
    /// </summary>
    [HttpGet("history/{sessionId}")]
    public async Task<ActionResult<ApiResponse<List<ChatMessage>>>> GetChatHistory(string sessionId, [FromQuery] int limit = 50)
    {
        try
        {
            var messages = await _chatHistoryService.GetSessionMessagesAsync(sessionId, limit);

            return Ok(new ApiResponse<List<ChatMessage>>
            {
                Success = true,
                Message = "Chat history retrieved successfully",
                Data = messages.OrderBy(m => m.Timestamp).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat history for session: {SessionId}", sessionId);
            return StatusCode(500, new ApiResponse<List<ChatMessage>>
            {
                Success = false,
                Message = "Internal server error",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get all sessions for a user
    /// </summary>
    [HttpGet("sessions/{userId}")]
    public async Task<ActionResult<ApiResponse<List<ChatSession>>>> GetUserSessions(string userId)
    {
        try
        {
            var sessions = await _chatHistoryService.GetUserSessionsAsync(userId);

            return Ok(new ApiResponse<List<ChatSession>>
            {
                Success = true,
                Message = "User sessions retrieved successfully",
                Data = sessions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sessions for user: {UserId}", userId);
            return StatusCode(500, new ApiResponse<List<ChatSession>>
            {
                Success = false,
                Message = "Internal server error",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Create a new chat session
    /// </summary>
    [HttpPost("sessions")]
    public async Task<ActionResult<ApiResponse<ChatSession>>> CreateSession([FromBody] CreateSessionRequest request)
    {
        try
        {
            var session = await _chatHistoryService.CreateSessionAsync(
                request.UserId ?? "default_user", 
                request.Title ?? $"Chat {DateTime.Now:yyyy-MM-dd HH:mm}");

            return Ok(new ApiResponse<ChatSession>
            {
                Success = true,
                Message = "Session created successfully",
                Data = session
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session");
            return StatusCode(500, new ApiResponse<ChatSession>
            {
                Success = false,
                Message = "Internal server error",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    #region Workflow Management Endpoints

    /// <summary>
    /// Get all saved workflows
    /// </summary>
    [HttpGet("workflows")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetSavedWorkflows()
    {
        try
        {
            var workflows = await _orchestrationService.GetSavedWorkflowsAsync();
            return Ok(new ApiResponse<List<string>>
            {
                Success = true,
                Message = "Workflows retrieved successfully",
                Data = workflows
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflows");
            return StatusCode(500, new ApiResponse<List<string>>
            {
                Success = false,
                Message = "Internal server error",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Save a workflow as JSON
    /// </summary>
    [HttpPost("workflows/{workflowName}")]
    public async Task<ActionResult<ApiResponse<bool>>> SaveWorkflow(string workflowName, [FromBody] SaveWorkflowRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.WorkflowJson))
            {
                return BadRequest(new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Workflow JSON cannot be empty",
                    Errors = new List<string> { "WorkflowJson is required" }
                });
            }

            var success = await _orchestrationService.SaveWorkflowAsync(workflowName, request.WorkflowJson);

            return Ok(new ApiResponse<bool>
            {
                Success = success,
                Message = success ? "Workflow saved successfully" : "Failed to save workflow",
                Data = success
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving workflow: {WorkflowName}", workflowName);
            return StatusCode(500, new ApiResponse<bool>
            {
                Success = false,
                Message = "Internal server error",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Load a workflow as JSON
    /// </summary>
    [HttpGet("workflows/{workflowName}")]
    public async Task<ActionResult<ApiResponse<string>>> LoadWorkflow(string workflowName)
    {
        try
        {
            var workflowJson = await _orchestrationService.LoadWorkflowAsync(workflowName);

            if (workflowJson == null)
            {
                return NotFound(new ApiResponse<string>
                {
                    Success = false,
                    Message = "Workflow not found",
                    Errors = new List<string> { $"Workflow '{workflowName}' does not exist" }
                });
            }

            return Ok(new ApiResponse<string>
            {
                Success = true,
                Message = "Workflow loaded successfully",
                Data = workflowJson
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading workflow: {WorkflowName}", workflowName);
            return StatusCode(500, new ApiResponse<string>
            {
                Success = false,
                Message = "Internal server error",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Delete a saved workflow
    /// </summary>
    [HttpDelete("workflows/{workflowName}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteWorkflow(string workflowName)
    {
        try
        {
            var success = await _orchestrationService.DeleteWorkflowAsync(workflowName);

            if (!success)
            {
                return NotFound(new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Workflow not found",
                    Errors = new List<string> { $"Workflow '{workflowName}' does not exist" }
                });
            }

            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Message = "Workflow deleted successfully",
                Data = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting workflow: {WorkflowName}", workflowName);
            return StatusCode(500, new ApiResponse<bool>
            {
                Success = false,
                Message = "Internal server error",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Export a workflow as JSON
    /// </summary>
    [HttpGet("workflows/{workflowName}/export")]
    public async Task<ActionResult<ApiResponse<string>>> ExportWorkflow(string workflowName)
    {
        try
        {
            var workflowJson = await _orchestrationService.ExportWorkflowAsync(workflowName);

            if (workflowJson == null)
            {
                return NotFound(new ApiResponse<string>
                {
                    Success = false,
                    Message = "Workflow not found",
                    Errors = new List<string> { $"Workflow '{workflowName}' does not exist" }
                });
            }

            return Ok(new ApiResponse<string>
            {
                Success = true,
                Message = "Workflow exported successfully",
                Data = workflowJson
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting workflow: {WorkflowName}", workflowName);
            return StatusCode(500, new ApiResponse<string>
            {
                Success = false,
                Message = "Internal server error",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Import a workflow from JSON
    /// </summary>
    [HttpPost("workflows/{workflowName}/import")]
    public async Task<ActionResult<ApiResponse<bool>>> ImportWorkflow(string workflowName, [FromBody] ImportWorkflowRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.WorkflowJson))
            {
                return BadRequest(new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Workflow JSON cannot be empty",
                    Errors = new List<string> { "WorkflowJson is required" }
                });
            }

            var success = await _orchestrationService.ImportWorkflowAsync(workflowName, request.WorkflowJson);

            return Ok(new ApiResponse<bool>
            {
                Success = success,
                Message = success ? "Workflow imported successfully" : "Failed to import workflow",
                Data = success
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing workflow: {WorkflowName}", workflowName);
            return StatusCode(500, new ApiResponse<bool>
            {
                Success = false,
                Message = "Internal server error",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    #endregion
}

/// <summary>
/// Request model for creating a new session
/// </summary>
public class CreateSessionRequest
{
    public string? UserId { get; set; }
    public string? Title { get; set; }
}

/// <summary>
/// Request model for saving a workflow
/// </summary>
public class SaveWorkflowRequest
{
    public string WorkflowJson { get; set; } = "";
}

/// <summary>
/// Request model for importing a workflow
/// </summary>
public class ImportWorkflowRequest
{
    public string WorkflowJson { get; set; } = "";
}
