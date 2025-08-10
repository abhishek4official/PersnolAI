using ElsaWorkflowAgent.Workflows;

namespace ElsaWorkflowAgent.Execution;

/// <summary>
/// Interface for executing workflows
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Executes a workflow with the provided input data
    /// </summary>
    /// <param name="workflow">The workflow to execute</param>
    /// <param name="inputData">Initial input data for the workflow</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The final result of the workflow execution</returns>
    Task<WorkflowExecutionResult> ExecuteAsync(SimpleWorkflow workflow, object inputData, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of workflow execution
/// </summary>
public class WorkflowExecutionResult
{
    public bool Success { get; set; }
    public object? FinalResult { get; set; }
    public List<StepExecutionResult> StepResults { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }

    public static WorkflowExecutionResult CreateSuccess(object? finalResult, List<StepExecutionResult> stepResults)
    {
        return new WorkflowExecutionResult
        {
            Success = true,
            FinalResult = finalResult,
            StepResults = stepResults
        };
    }

    public static WorkflowExecutionResult CreateFailure(string errorMessage, Exception? exception = null)
    {
        return new WorkflowExecutionResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Exception = exception
        };
    }
}

/// <summary>
/// Result of a single step execution
/// </summary>
public class StepExecutionResult
{
    public string StepId { get; set; } = string.Empty;
    public string StepType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}
