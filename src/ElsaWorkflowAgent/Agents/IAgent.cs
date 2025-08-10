namespace ElsaWorkflowAgent.Agents;

/// <summary>
/// Represents an agent that processes input and produces output of type TResponse
/// </summary>
/// <typeparam name="TResponse">The type of response the agent produces</typeparam>
public interface IAgent<TResponse>
{
    /// <summary>
    /// The unique identifier for the agent
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// The name of the agent
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// The description of what the agent does
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Execute the agent with the given input and context
    /// </summary>
    /// <param name="input">The input data for the agent</param>
    /// <param name="context">The execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The agent's response</returns>
    Task<TResponse> ExecuteAsync(object input, AgentContext context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Determines if the agent can handle the given input
    /// </summary>
    /// <param name="input">The input to check</param>
    /// <returns>True if the agent can handle the input</returns>
    bool CanHandle(object input);
    
    /// <summary>
    /// Get the agent's metadata
    /// </summary>
    AgentMetadata Metadata { get; }
}

/// <summary>
/// Context information passed to agents during execution
/// </summary>
public class AgentContext
{
    public Dictionary<string, object> Properties { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public IServiceProvider ServiceProvider { get; set; } = default!;
}

/// <summary>
/// Metadata about an agent
/// </summary>
public class AgentMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public Type InputType { get; set; } = typeof(object);
    public Type OutputType { get; set; } = typeof(object);
    public Dictionary<string, object> Properties { get; set; } = new();
}
