namespace ElsaWorkflowAgent.Decisions;

/// <summary>
/// Represents a decision that can be injected into workflows for dynamic logic
/// </summary>
public interface IDecision
{
    /// <summary>
    /// The unique identifier for the decision
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// The name of the decision
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// The description of what the decision does
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Execute the decision logic
    /// </summary>
    /// <param name="input">The input data for the decision</param>
    /// <param name="context">The decision context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The decision result</returns>
    Task<DecisionResult> ExecuteAsync(object input, DecisionContext context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Determines if the decision can handle the given input
    /// </summary>
    /// <param name="input">The input to check</param>
    /// <returns>True if the decision can handle the input</returns>
    bool CanHandle(object input);
    
    /// <summary>
    /// Get the decision's metadata
    /// </summary>
    DecisionMetadata Metadata { get; }
}

/// <summary>
/// Context information passed to decisions during execution
/// </summary>
public class DecisionContext
{
    public Dictionary<string, object> Properties { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public IServiceProvider ServiceProvider { get; set; } = default!;
    public object? PreviousResult { get; set; }
}

/// <summary>
/// Result of a decision execution
/// </summary>
public class DecisionResult
{
    public bool Success { get; set; }
    public string NextStep { get; set; } = string.Empty;
    public object? Data { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string? ErrorMessage { get; set; }
    
    public static DecisionResult CreateSuccess(string nextStep, object? data = null)
    {
        return new DecisionResult
        {
            Success = true,
            NextStep = nextStep,
            Data = data
        };
    }
    
    public static DecisionResult Failure(string errorMessage)
    {
        return new DecisionResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Metadata about a decision
/// </summary>
public class DecisionMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public Type InputType { get; set; } = typeof(object);
    public List<string> PossibleOutcomes { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}
