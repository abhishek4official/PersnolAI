using LocalChatApi.Models;
using LocalChatApi.Core;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace LocalChatApi.Services;

/// <summary>
/// Simplified workflow definition for JSON serialization
/// </summary>
public class SimpleWorkflowDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<WorkflowStepDefinition> Steps { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public string Version { get; set; } = "1.0";
}

/// <summary>
/// Workflow step definition
/// </summary>
public class WorkflowStepDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // "agent" or "decision"
    public string AgentType { get; set; } = ""; // For agent steps
    public string DecisionType { get; set; } = ""; // For decision steps
    public object? InputData { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Workflow execution result
/// </summary>
public class SimpleWorkflowResult
{
    public bool Success { get; set; }
    public object? FinalResult { get; set; }
    public List<StepResult> StepResults { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Step execution result
/// </summary>
public class StepResult
{
    public string StepId { get; set; } = "";
    public string StepName { get; set; } = "";
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// Service for managing simple workflows with JSON serialization
/// </summary>
public interface ISimpleWorkflowService
{
    /// <summary>
    /// Execute intent detection workflow
    /// </summary>
    Task<SimpleWorkflowResult> ExecuteIntentDetectionWorkflowAsync(UserRequest request);

    /// <summary>
    /// Execute chat workflow
    /// </summary>
    Task<SimpleWorkflowResult> ExecuteChatWorkflowAsync(UserRequest request);

    /// <summary>
    /// Execute file upload workflow
    /// </summary>
    Task<SimpleWorkflowResult> ExecuteFileUploadWorkflowAsync(FileUploadRequest request);

    /// <summary>
    /// Execute file processing workflow for background processing
    /// </summary>
    Task<SimpleWorkflowResult> ExecuteFileProcessingWorkflowAsync(string fileId);

    /// <summary>
    /// Execute file chat workflow
    /// </summary>
    Task<SimpleWorkflowResult> ExecuteFileChatWorkflowAsync(FileChatRequest request);

    /// <summary>
    /// Execute custom workflow from definition
    /// </summary>
    Task<SimpleWorkflowResult> ExecuteWorkflowAsync(SimpleWorkflowDefinition workflowDefinition, object input);

    /// <summary>
    /// Save workflow as JSON
    /// </summary>
    Task<bool> SaveWorkflowAsync(string name, SimpleWorkflowDefinition workflow);

    /// <summary>
    /// Load workflow from JSON
    /// </summary>
    Task<SimpleWorkflowDefinition?> LoadWorkflowAsync(string name);

    /// <summary>
    /// Get all saved workflows
    /// </summary>
    Task<List<string>> GetSavedWorkflowsAsync();

    /// <summary>
    /// Export workflow to JSON string
    /// </summary>
    Task<string?> ExportWorkflowAsync(string name);

    /// <summary>
    /// Import workflow from JSON string
    /// </summary>
    Task<bool> ImportWorkflowAsync(string name, string json);
}

/// <summary>
/// Simple workflow service implementation
/// </summary>
public class SimpleWorkflowService : ISimpleWorkflowService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<SimpleWorkflowService> _logger;
    private readonly string _workflowsDirectory = "SimpleWorkflows";

    public SimpleWorkflowService(IServiceScopeFactory serviceScopeFactory, ILogger<SimpleWorkflowService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;

        // Ensure directory exists
        if (!Directory.Exists(_workflowsDirectory))
        {
            Directory.CreateDirectory(_workflowsDirectory);
        }
    }

    public async Task<SimpleWorkflowResult> ExecuteIntentDetectionWorkflowAsync(UserRequest request)
    {
        var workflow = CreateIntentDetectionWorkflow();
        return await ExecuteWorkflowAsync(workflow, request);
    }

    public async Task<SimpleWorkflowResult> ExecuteChatWorkflowAsync(UserRequest request)
    {
        var workflow = CreateChatWorkflow();
        return await ExecuteWorkflowAsync(workflow, request);
    }

    public async Task<SimpleWorkflowResult> ExecuteFileUploadWorkflowAsync(FileUploadRequest request)
    {
        var workflow = CreateFileUploadWorkflow();
        return await ExecuteWorkflowAsync(workflow, request);
    }

    public async Task<SimpleWorkflowResult> ExecuteFileProcessingWorkflowAsync(string fileId)
    {
        var workflow = CreateFileProcessingWorkflow();
        
        // Set fileId in workflow variables so it can be passed through the pipeline
        workflow.Variables["fileId"] = fileId;
        
        // Create input data with file ID
        var inputData = new Dictionary<string, object>
        {
            ["fileId"] = fileId,
            ["processType"] = "background"
        };
        
        return await ExecuteWorkflowAsync(workflow, inputData);
    }

    public async Task<SimpleWorkflowResult> ExecuteFileChatWorkflowAsync(FileChatRequest request)
    {
        var workflow = CreateFileChatWorkflow();
        
        // Store the original request in workflow variables so agents can access it
        workflow.Variables["originalInput"] = request;
        workflow.Variables["question"] = request.Question;
        workflow.Variables["sessionId"] = request.SessionId;
        workflow.Variables["fileId"] = request.FileId;
        
        return await ExecuteWorkflowAsync(workflow, request);
    }

    public async Task<SimpleWorkflowResult> ExecuteWorkflowAsync(SimpleWorkflowDefinition workflowDefinition, object input)
    {
        var result = new SimpleWorkflowResult { Success = true };
        var currentData = input;

        _logger.LogInformation("Executing workflow: {WorkflowName} with {StepCount} steps", 
            workflowDefinition.Name, workflowDefinition.Steps.Count);

        // Create a service scope for the entire workflow execution
        using var scope = _serviceScopeFactory.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        try
        {
            foreach (var stepDef in workflowDefinition.Steps)
            {
                var stepStartTime = DateTime.UtcNow;
                
                try
                {
                    _logger.LogInformation("Executing step: {StepName} ({StepType})", stepDef.Name, stepDef.Type);

                    object? stepResult = null;

                    if (stepDef.Type == "agent")
                    {
                        stepResult = await ExecuteAgentStepAsync(stepDef, currentData, workflowDefinition.Variables, serviceProvider);
                    }
                    else if (stepDef.Type == "decision")
                    {
                        stepResult = await ExecuteDecisionStepAsync(stepDef, currentData, workflowDefinition.Variables, serviceProvider);
                    }

                    var executionTime = DateTime.UtcNow - stepStartTime;

                    result.StepResults.Add(new StepResult
                    {
                        StepId = stepDef.Id,
                        StepName = stepDef.Name,
                        Success = true,
                        Result = stepResult,
                        ExecutionTime = executionTime
                    });

                    // Update current data for next step
                    if (stepResult != null)
                    {
                        currentData = stepResult;
                        result.FinalResult = stepResult;
                    }

                    _logger.LogInformation("Step completed: {StepName} in {ExecutionTime}ms", 
                        stepDef.Name, executionTime.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    var executionTime = DateTime.UtcNow - stepStartTime;
                    
                    _logger.LogError(ex, "Step failed: {StepName}", stepDef.Name);

                    result.StepResults.Add(new StepResult
                    {
                        StepId = stepDef.Id,
                        StepName = stepDef.Name,
                        Success = false,
                        ErrorMessage = ex.Message,
                        ExecutionTime = executionTime
                    });

                    result.Success = false;
                    result.ErrorMessage = $"Step '{stepDef.Name}' failed: {ex.Message}";
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow execution failed: {WorkflowName}", workflowDefinition.Name);
            result.Success = false;
            result.ErrorMessage = $"Workflow execution failed: {ex.Message}";
        }

        return result;
    }

    public async Task<bool> SaveWorkflowAsync(string name, SimpleWorkflowDefinition workflow)
    {
        try
        {
            var fileName = $"{SanitizeFileName(name)}.json";
            var filePath = Path.Combine(_workflowsDirectory, fileName);
            
            var json = JsonSerializer.Serialize(workflow, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("Saved workflow: {Name} to {FilePath}", name, filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving workflow: {Name}", name);
            return false;
        }
    }

    public async Task<SimpleWorkflowDefinition?> LoadWorkflowAsync(string name)
    {
        try
        {
            var fileName = $"{SanitizeFileName(name)}.json";
            var filePath = Path.Combine(_workflowsDirectory, fileName);
            
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var workflow = JsonSerializer.Deserialize<SimpleWorkflowDefinition>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            _logger.LogInformation("Loaded workflow: {Name} from {FilePath}", name, filePath);
            return workflow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading workflow: {Name}", name);
            return null;
        }
    }

    public async Task<List<string>> GetSavedWorkflowsAsync()
    {
        try
        {
            if (!Directory.Exists(_workflowsDirectory))
            {
                return new List<string>();
            }

            var files = Directory.GetFiles(_workflowsDirectory, "*.json");
            return files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting saved workflows");
            return new List<string>();
        }
    }

    public async Task<string?> ExportWorkflowAsync(string name)
    {
        try
        {
            var workflow = await LoadWorkflowAsync(name);
            if (workflow == null)
            {
                return null;
            }

            return JsonSerializer.Serialize(workflow, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting workflow: {Name}", name);
            return null;
        }
    }

    public async Task<bool> ImportWorkflowAsync(string name, string json)
    {
        try
        {
            var workflow = JsonSerializer.Deserialize<SimpleWorkflowDefinition>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            if (workflow == null)
            {
                return false;
            }

            // Update metadata
            workflow.Name = name;
            workflow.UpdatedAt = DateTime.UtcNow;

            return await SaveWorkflowAsync(name, workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing workflow: {Name}", name);
            return false;
        }
    }

    // Private helper methods
    private async Task<object?> ExecuteAgentStepAsync(WorkflowStepDefinition stepDef, object input, Dictionary<string, object> workflowVariables, IServiceProvider serviceProvider)
    {
        var agentType = Type.GetType(stepDef.AgentType) ?? 
                       AppDomain.CurrentDomain.GetAssemblies()
                           .SelectMany(a => a.GetTypes())
                           .FirstOrDefault(t => t.Name == stepDef.AgentType || t.FullName == stepDef.AgentType);

        if (agentType == null)
        {
            throw new InvalidOperationException($"Agent type not found: {stepDef.AgentType}");
        }

        var agent = serviceProvider.GetService(agentType);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent not registered: {stepDef.AgentType}");
        }

        // Create agent context with workflow variables
        var agentContext = new AgentContext
        {
            WorkflowInstanceId = Guid.NewGuid().ToString(),
            StepId = stepDef.Id,
            ServiceProvider = serviceProvider,
            Variables = new Dictionary<string, object>(workflowVariables) // Pass workflow variables
        };

        // Execute agent using reflection
        var executeMethod = agentType.GetMethod("ExecuteAsync");
        if (executeMethod == null)
        {
            throw new InvalidOperationException($"ExecuteAsync method not found on agent: {stepDef.AgentType}");
        }

        var task = (Task)executeMethod.Invoke(agent, new[] { stepDef.InputData ?? input, agentContext, CancellationToken.None })!;
        await task;

        // Get result from task
        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task);
    }

    private async Task<object?> ExecuteDecisionStepAsync(WorkflowStepDefinition stepDef, object input, Dictionary<string, object> workflowVariables, IServiceProvider serviceProvider)
    {
        var decisionType = Type.GetType(stepDef.DecisionType) ?? 
                          AppDomain.CurrentDomain.GetAssemblies()
                              .SelectMany(a => a.GetTypes())
                              .FirstOrDefault(t => t.Name == stepDef.DecisionType || t.FullName == stepDef.DecisionType);

        if (decisionType == null)
        {
            throw new InvalidOperationException($"Decision type not found: {stepDef.DecisionType}");
        }

        var decision = serviceProvider.GetService(decisionType);
        if (decision == null)
        {
            throw new InvalidOperationException($"Decision not registered: {stepDef.DecisionType}");
        }

        // Create decision context with workflow variables
        var decisionContext = new DecisionContext
        {
            WorkflowInstanceId = Guid.NewGuid().ToString(),
            StepId = stepDef.Id,
            ServiceProvider = serviceProvider,
            Variables = new Dictionary<string, object>(workflowVariables) // Pass workflow variables
        };

        // Execute decision using reflection
        var executeMethod = decisionType.GetMethod("ExecuteAsync");
        if (executeMethod == null)
        {
            throw new InvalidOperationException($"ExecuteAsync method not found on decision: {stepDef.DecisionType}");
        }

        var task = (Task)executeMethod.Invoke(decision, new[] { stepDef.InputData ?? input, decisionContext, CancellationToken.None })!;
        await task;

        // Get result from task
        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task);
    }

    // Predefined workflow creators
    private SimpleWorkflowDefinition CreateIntentDetectionWorkflow()
    {
        return new SimpleWorkflowDefinition
        {
            Name = "IntentDetectionWorkflow",
            Description = "Detects user intent from input",
            Steps = new List<WorkflowStepDefinition>
            {
                new WorkflowStepDefinition
                {
                    Name = "Detect Intent",
                    Type = "agent",
                    AgentType = "IntentDetectionAgent"
                },
                new WorkflowStepDefinition
                {
                    Name = "Route Intent",
                    Type = "decision",
                    DecisionType = "IntentRoutingDecision"
                }
            }
        };
    }

    private SimpleWorkflowDefinition CreateChatWorkflow()
    {
        return new SimpleWorkflowDefinition
        {
            Name = "ChatWorkflow",
            Description = "Handles normal chat conversation",
            Steps = new List<WorkflowStepDefinition>
            {
                new WorkflowStepDefinition
                {
                    Name = "Process Chat",
                    Type = "agent",
                    AgentType = "ChatAgent"
                }
            }
        };
    }

    private SimpleWorkflowDefinition CreateFileUploadWorkflow()
    {
        return new SimpleWorkflowDefinition
        {
            Name = "FileUploadWorkflow",
            Description = "Processes file uploads",
            Steps = new List<WorkflowStepDefinition>
            {
                new WorkflowStepDefinition
                {
                    Name = "Upload File",
                    Type = "agent",
                    AgentType = "FileUploadAgent"
                }
            }
        };
    }

    /// <summary>
    /// Create complete file processing workflow for background processing
    /// </summary>
    private SimpleWorkflowDefinition CreateFileProcessingWorkflow()
    {
        return new SimpleWorkflowDefinition
        {
            Name = "FileProcessingWorkflow",
            Description = "Complete file processing pipeline: Read ? Extract ? Chunk ? Embed",
            Steps = new List<WorkflowStepDefinition>
            {
                new WorkflowStepDefinition
                {
                    Name = "Read File Content",
                    Type = "agent",
                    AgentType = "FileReaderAgent"
                },
                new WorkflowStepDefinition
                {
                    Name = "Extract and Clean Data",
                    Type = "agent",
                    AgentType = "DataExtractionAgent"
                },
                new WorkflowStepDefinition
                {
                    Name = "Create Chunks and Embeddings",
                    Type = "agent",
                    AgentType = "ChunkingEmbeddingAgent"
                }
            }
        };
    }

    private SimpleWorkflowDefinition CreateFileChatWorkflow()
    {
        return new SimpleWorkflowDefinition
        {
            Name = "FileChatWorkflow",
            Description = "Handles file chat (RAG)",
            Steps = new List<WorkflowStepDefinition>
            {
                new WorkflowStepDefinition
                {
                    Name = "Check File Availability",
                    Type = "decision",
                    DecisionType = "FileAvailabilityDecision"
                },
                new WorkflowStepDefinition
                {
                    Name = "Process File Chat",
                    Type = "agent",
                    AgentType = "FileChatAgent"
                }
            }
        };
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }
}