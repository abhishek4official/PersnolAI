using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace ElsaWorkflowAgent.Agents;

/// <summary>
/// Base implementation of IAgent with common functionality
/// </summary>
/// <typeparam name="TResponse">The type of response the agent produces</typeparam>
public abstract class BaseAgent<TResponse> : IAgent<TResponse>
{
    private readonly ILogger<BaseAgent<TResponse>> _logger;
    protected readonly Microsoft.SemanticKernel.Kernel _kernel;

    protected ILogger<BaseAgent<TResponse>> Logger => _logger;

    protected BaseAgent(ILogger<BaseAgent<TResponse>> logger, Microsoft.SemanticKernel.Kernel kernel)
    {
        _logger = logger;
        _kernel = kernel;
    }

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract AgentMetadata Metadata { get; }

    public virtual bool CanHandle(object input)
    {
        return true; // Default implementation - can be overridden
    }

    public async Task<TResponse> ExecuteAsync(object input, AgentContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing agent {AgentName} with input type {InputType}", Name, input?.GetType().Name ?? "null");
        
        try
        {
            var result = await ExecuteInternalAsync(input, context, cancellationToken);
            _logger.LogInformation("Agent {AgentName} completed successfully", Name);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent {AgentName} failed with error: {Error}", Name, ex.Message);
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
        return context.ServiceProvider.GetService(typeof(T)) as T ?? throw new InvalidOperationException($"Service {typeof(T).Name} not found");
    }

    /// <summary>
    /// Helper method to get a property from the context
    /// </summary>
    protected T? GetContextProperty<T>(AgentContext context, string key, T? defaultValue = default)
    {
        if (context.Properties.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Helper method to set a property in the context
    /// </summary>
    protected void SetContextProperty<T>(AgentContext context, string key, T value)
    {
        context.Properties[key] = value!;
    }
}
