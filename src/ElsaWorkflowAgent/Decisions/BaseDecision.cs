using Microsoft.Extensions.Logging;

namespace ElsaWorkflowAgent.Decisions;

/// <summary>
/// Base implementation of IDecision with common functionality
/// </summary>
public abstract class BaseDecision : IDecision
{
    private readonly ILogger<BaseDecision> _logger;

    protected BaseDecision(ILogger<BaseDecision> logger)
    {
        _logger = logger;
    }

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract DecisionMetadata Metadata { get; }

    public virtual bool CanHandle(object input)
    {
        return true; // Default implementation - can be overridden
    }

    public async Task<DecisionResult> ExecuteAsync(object input, DecisionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing decision {DecisionName} with input type {InputType}", Name, input?.GetType().Name ?? "null");
        
        try
        {
            var result = await ExecuteInternalAsync(input, context, cancellationToken);
            _logger.LogInformation("Decision {DecisionName} completed with outcome: {Outcome}", Name, result.NextStep);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decision {DecisionName} failed with error: {Error}", Name, ex.Message);
            return DecisionResult.Failure(ex.Message);
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
        return context.ServiceProvider.GetService(typeof(T)) as T ?? throw new InvalidOperationException($"Service {typeof(T).Name} not found");
    }

    /// <summary>
    /// Helper method to get a property from the context
    /// </summary>
    protected T? GetContextProperty<T>(DecisionContext context, string key, T? defaultValue = default)
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
    protected void SetContextProperty<T>(DecisionContext context, string key, T value)
    {
        context.Properties[key] = value!;
    }
}
