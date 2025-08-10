using ElsaWorkflowAgent.Agents;
using ElsaWorkflowAgent.Decisions;

namespace ElsaWorkflowAgent.Workflows;

/// <summary>
/// Represents a workflow step that executes an agent
/// </summary>
public class AgentStep<TResponse>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public IAgent<TResponse> Agent { get; set; } = default!;
    public object InputData { get; set; } = default!;
    
    public async Task<TResponse> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        return await Agent.ExecuteAsync(InputData, context, cancellationToken);
    }
}

/// <summary>
/// Represents a workflow step that executes a decision
/// </summary>
public class DecisionStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public IDecision Decision { get; set; } = default!;
    public object InputData { get; set; } = default!;
    
    public async Task<DecisionResult> ExecuteAsync(DecisionContext context, CancellationToken cancellationToken = default)
    {
        return await Decision.ExecuteAsync(InputData, context, cancellationToken);
    }
}
