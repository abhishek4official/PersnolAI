namespace LocalChatApi.Core;

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

/// <summary>
/// Base implementation of IAgent with common functionality
/// </summary>
/// <typeparam name="TResponse">The type of response the agent produces</typeparam>
public abstract class BaseAgent<TResponse> : IAgent<TResponse>
{
    protected readonly ILogger<BaseAgent<TResponse>> _logger;
    protected readonly Microsoft.SemanticKernel.Kernel _kernel;

    protected BaseAgent(ILogger<BaseAgent<TResponse>> logger, Microsoft.SemanticKernel.Kernel kernel)
    {
        _logger = logger;
        _kernel = kernel;
    }

    public virtual string Id => GetType().Name;
    public virtual string Name => GetType().Name;
    public virtual string Description => $"{GetType().Name} implementation";
    public abstract AgentMetadata Metadata { get; }

    public virtual bool CanHandle(object input) => true;

    public async Task<TResponse> ExecuteAsync(object input, AgentContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Executing agent: {AgentName} with input type: {InputType}", 
                GetType().Name, input?.GetType().Name ?? "null");

            var result = await ExecuteInternalAsync(input, context, cancellationToken);

            _logger.LogInformation("Agent execution completed: {AgentName}", GetType().Name);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent execution failed: {AgentName}", GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Override this method to implement the agent's specific logic
    /// </summary>
    protected abstract Task<TResponse> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Helper method to get a service from the context
    /// </summary>
    protected T GetService<T>(AgentContext context) where T : class
    {
        return context.ServiceProvider.GetRequiredService<T>();
    }
}

/// <summary>
/// Base implementation of IDecision with common functionality
/// </summary>
public abstract class BaseDecision : IDecision
{
    protected readonly ILogger<BaseDecision> _logger;

    protected BaseDecision(ILogger<BaseDecision> logger)
    {
        _logger = logger;
    }

    public virtual string Id => GetType().Name;
    public virtual string Name => GetType().Name;
    public virtual string Description => $"{GetType().Name} implementation";
    public abstract DecisionMetadata Metadata { get; }

    public virtual bool CanHandle(object input) => true;

    public async Task<DecisionResult> ExecuteAsync(object input, DecisionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Executing decision: {DecisionName} with input type: {InputType}", 
                GetType().Name, input?.GetType().Name ?? "null");

            var result = await ExecuteInternalAsync(input, context, cancellationToken);

            _logger.LogInformation("Decision execution completed: {DecisionName}, outcome: {Outcome}", 
                GetType().Name, result.NextStep);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decision execution failed: {DecisionName}", GetType().Name);
            return DecisionResult.Failure($"Decision execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Override this method to implement the decision's specific logic
    /// </summary>
    protected abstract Task<DecisionResult> ExecuteInternalAsync(object input, DecisionContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Helper method to get a service from the context
    /// </summary>
    protected T GetService<T>(DecisionContext context) where T : class
    {
        return context.ServiceProvider.GetRequiredService<T>();
    }
}