using Elsa.Extensions;
using Elsa.Workflows.Contracts;
using Elsa.Workflows.Models;
using Elsa.Workflows.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ElsaWorkflowAgent.Workflows;

namespace ElsaWorkflowAgent.Execution;

/// <summary>
/// Elsa-based workflow engine implementation
/// </summary>
public class ElsaWorkflowEngine : IWorkflowEngine
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ElsaWorkflowEngine> _logger;

    public ElsaWorkflowEngine(IServiceProvider serviceProvider, ILogger<ElsaWorkflowEngine> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<WorkflowExecutionResult> ExecuteAsync(SimpleWorkflow workflow, object inputData, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Executing workflow: {WorkflowName}", workflow.Name);

            // Get Elsa workflow runtime
            var workflowRuntime = _serviceProvider.GetRequiredService<IWorkflowRunner>();
            
            // Convert our SimpleWorkflow to Elsa Workflow if needed
            // For now, we'll assume the workflow is already an Elsa workflow
            // In practice, you'd convert SimpleWorkflow to Elsa Workflow format
            
            // Create workflow input
            var workflowInput = new Dictionary<string, object>(workflow.Variables);
            if (inputData != null)
            {
                workflowInput["Input"] = inputData;
            }

            // Execute the workflow
            var runWorkflowOptions = new RunWorkflowOptions
            {
                Input = workflowInput,
                CancellationToken = cancellationToken
            };

            // For this implementation, we need to create a proper Elsa workflow
            // This is a simplified approach - in practice you'd have a more sophisticated conversion
            var elsaWorkflow = CreateElsaWorkflowFromSimple(workflow);
            
            var result = await workflowRuntime.RunAsync(elsaWorkflow, runWorkflowOptions);

            return MapElsaResultToWorkflowResult(result, workflow.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing workflow: {WorkflowName}", workflow.Name);
            return WorkflowExecutionResult.CreateFailure($"Workflow execution failed: {ex.Message}");
        }
    }

    private Workflow CreateElsaWorkflowFromSimple(SimpleWorkflow simpleWorkflow)
    {
        // This is a simplified conversion
        // In practice, you'd need to properly map SimpleWorkflow steps to Elsa activities
        var builder = ElsaWorkflowBuilderFactory.CreateBuilder(simpleWorkflow.Name, simpleWorkflow.Description);
        
        foreach (var variable in simpleWorkflow.Variables)
        {
            builder.WithVariable(variable.Key, variable.Value);
        }

        // Convert steps to Elsa activities
        // This is where you'd map your custom steps to Elsa activities
        // For now, we'll create a basic sequence

        return builder.BuildElsaWorkflow();
    }

    private WorkflowExecutionResult MapElsaResultToWorkflowResult(WorkflowResult elsaResult, string workflowName)
    {
        var stepResults = new List<StepExecutionResult>();

        // Map Elsa execution log to our step results
        if (elsaResult.WorkflowState?.ActivityExecutionContexts != null)
        {
            foreach (var activityContext in elsaResult.WorkflowState.ActivityExecutionContexts)
            {
                stepResults.Add(new StepExecutionResult
                {
                    StepId = activityContext.Value.Id,
                    StepType = activityContext.Value.Activity.Type,
                    Success = activityContext.Value.Status == ActivityStatus.Completed,
                    Result = activityContext.Value.OutputRegisters.FirstOrDefault().Value,
                    ErrorMessage = activityContext.Value.Status == ActivityStatus.Faulted ? "Activity faulted" : null,
                    ExecutionTime = TimeSpan.Zero // Elsa doesn't provide execution time by default
                });
            }
        }

        var success = elsaResult.WorkflowState?.Status == WorkflowStatus.Finished;
        var finalResult = elsaResult.WorkflowState?.Output;

        return success 
            ? WorkflowExecutionResult.CreateSuccess(finalResult, stepResults)
            : WorkflowExecutionResult.CreateFailure("Workflow execution failed or was interrupted");
    }
}

/// <summary>
/// Enhanced workflow engine interface that supports Elsa workflows directly
/// </summary>
public interface IElsaWorkflowEngine : IWorkflowEngine
{
    /// <summary>
    /// Execute an Elsa workflow directly
    /// </summary>
    Task<WorkflowResult> ExecuteElsaWorkflowAsync(Workflow workflow, Dictionary<string, object>? input = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a workflow from JSON definition
    /// </summary>
    Task<WorkflowResult> ExecuteFromJsonAsync(string workflowJson, Dictionary<string, object>? input = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save a workflow to JSON
    /// </summary>
    Task<string> SaveWorkflowToJsonAsync(Workflow workflow);

    /// <summary>
    /// Load a workflow from JSON
    /// </summary>
    Task<Workflow> LoadWorkflowFromJsonAsync(string workflowJson);
}

/// <summary>
/// Enhanced Elsa workflow engine with JSON serialization support
/// </summary>
public class EnhancedElsaWorkflowEngine : ElsaWorkflowEngine, IElsaWorkflowEngine
{
    private readonly IWorkflowRunner _workflowRunner;
    private readonly IWorkflowDefinitionStore _workflowDefinitionStore;
    private readonly IActivitySerializer _activitySerializer;

    public EnhancedElsaWorkflowEngine(
        IServiceProvider serviceProvider, 
        ILogger<ElsaWorkflowEngine> logger,
        IWorkflowRunner workflowRunner,
        IWorkflowDefinitionStore workflowDefinitionStore,
        IActivitySerializer activitySerializer) 
        : base(serviceProvider, logger)
    {
        _workflowRunner = workflowRunner;
        _workflowDefinitionStore = workflowDefinitionStore;
        _activitySerializer = activitySerializer;
    }

    public async Task<WorkflowResult> ExecuteElsaWorkflowAsync(Workflow workflow, Dictionary<string, object>? input = null, CancellationToken cancellationToken = default)
    {
        var runOptions = new RunWorkflowOptions
        {
            Input = input ?? new Dictionary<string, object>(),
            CancellationToken = cancellationToken
        };

        return await _workflowRunner.RunAsync(workflow, runOptions);
    }

    public async Task<WorkflowResult> ExecuteFromJsonAsync(string workflowJson, Dictionary<string, object>? input = null, CancellationToken cancellationToken = default)
    {
        var workflow = await LoadWorkflowFromJsonAsync(workflowJson);
        return await ExecuteElsaWorkflowAsync(workflow, input, cancellationToken);
    }

    public async Task<string> SaveWorkflowToJsonAsync(Workflow workflow)
    {
        // Convert workflow to WorkflowDefinition for serialization
        var workflowDefinition = new WorkflowDefinition
        {
            Id = workflow.Identity.Id,
            Name = workflow.Identity.Name,
            Description = workflow.Identity.Description,
            Version = workflow.Identity.Version,
            Variables = workflow.Variables.Select(v => new WorkflowVariable
            {
                Id = v.Id,
                Name = v.Name,
                Value = v.Value,
                TypeName = v.Value?.GetType().AssemblyQualifiedName ?? typeof(object).AssemblyQualifiedName
            }).ToList(),
            Root = _activitySerializer.Serialize(workflow.Root),
            CreatedAt = DateTimeOffset.UtcNow,
            IsLatest = true,
            IsPublished = true
        };

        // Serialize to JSON
        return System.Text.Json.JsonSerializer.Serialize(workflowDefinition, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
    }

    public async Task<Workflow> LoadWorkflowFromJsonAsync(string workflowJson)
    {
        // Deserialize from JSON
        var workflowDefinition = System.Text.Json.JsonSerializer.Deserialize<WorkflowDefinition>(workflowJson, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        if (workflowDefinition == null)
        {
            throw new InvalidOperationException("Failed to deserialize workflow definition");
        }

        // Convert WorkflowDefinition back to Workflow
        var workflow = new Workflow
        {
            Identity = new WorkflowIdentity
            {
                Id = workflowDefinition.Id,
                Name = workflowDefinition.Name,
                Description = workflowDefinition.Description,
                Version = workflowDefinition.Version
            },
            Variables = workflowDefinition.Variables.Select(v => new Variable
            {
                Id = v.Id,
                Name = v.Name,
                Value = v.Value
            }).ToList(),
            Root = _activitySerializer.Deserialize(workflowDefinition.Root)
        };

        return workflow;
    }
}