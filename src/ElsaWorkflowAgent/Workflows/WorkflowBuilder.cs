using ElsaWorkflowAgent.Agents;
using ElsaWorkflowAgent.Decisions;

namespace ElsaWorkflowAgent.Workflows;

/// <summary>
/// Simple workflow definition
/// </summary>
public class SimpleWorkflow
{
    public string Name { get; set; } = "SimpleWorkflow";
    public string Description { get; set; } = "";
    public List<object> Steps { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
}

/// <summary>
/// Special marker to indicate using the previous step result
/// </summary>
public class PreviousStepResult
{
    public static readonly PreviousStepResult Instance = new();
    private PreviousStepResult() { }
}

/// <summary>
/// Input transformation delegate
/// </summary>
public delegate object InputTransform(object previousResult);

/// <summary>
/// Enhanced agent step with input transformation support
/// </summary>
public class EnhancedAgentStep<TResponse> : AgentStep<TResponse>
{
    public InputTransform? InputTransform { get; set; }
    public bool UsePreviousResult { get; set; }
}

/// <summary>
/// Builder for creating dynamic workflows with agents and decisions
/// </summary>
public class AgentWorkflowBuilder
{
    private readonly List<object> _steps = new();
    private readonly Dictionary<string, object> _variables = new();
    private string _workflowName = "DynamicAgentWorkflow";
    private string _workflowDescription = "Dynamically created workflow with agents and decisions";

    public AgentWorkflowBuilder WithName(string name)
    {
        _workflowName = name;
        return this;
    }

    public AgentWorkflowBuilder WithDescription(string description)
    {
        _workflowDescription = description;
        return this;
    }

    public AgentWorkflowBuilder WithVariable(string name, object value)
    {
        _variables[name] = value;
        return this;
    }

    /// <summary>
    /// Original AddAgent method - unchanged for backward compatibility
    /// </summary>
    public AgentWorkflowBuilder AddAgent<TResponse>(IAgent<TResponse> agent, object input, string? stepId = null)
    {
        var step = new AgentStep<TResponse>
        {
            Id = stepId ?? Guid.NewGuid().ToString(),
            Agent = agent,
            InputData = input
        };
        
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// NEW: Add agent that uses the previous step result as input (x => x)
    /// </summary>
    public AgentWorkflowBuilder AddAgentWithPreviousResult<TResponse>(
        IAgent<TResponse> agent, 
        string? stepId = null)
    {
        var step = new EnhancedAgentStep<TResponse>
        {
            Id = stepId ?? Guid.NewGuid().ToString(),
            Agent = agent,
            InputData = PreviousStepResult.Instance,
            InputTransform = null,
            UsePreviousResult = true
        };
        
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// NEW: Add agent with transformation of previous step result (x => transform(x))
    /// </summary>
    public AgentWorkflowBuilder AddAgentWithTransform<TResponse>(
        IAgent<TResponse> agent, 
        Func<object, object> transform, 
        string? stepId = null)
    {
        var step = new EnhancedAgentStep<TResponse>
        {
            Id = stepId ?? Guid.NewGuid().ToString(),
            Agent = agent,
            InputData = PreviousStepResult.Instance,
            InputTransform = new InputTransform(transform),
            UsePreviousResult = true
        };
        
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// NEW: Overload for more complex transformations with fileId access
    /// </summary>
    public AgentWorkflowBuilder AddAgentWithTransform<TResponse>(
        IAgent<TResponse> agent, 
        Func<object, Dictionary<string, object>, object> transform, 
        string? stepId = null)
    {
        var step = new EnhancedAgentStep<TResponse>
        {
            Id = stepId ?? Guid.NewGuid().ToString(),
            Agent = agent,
            InputData = PreviousStepResult.Instance,
            InputTransform = new InputTransform(previousResult => transform(previousResult, _variables)),
            UsePreviousResult = true
        };
        
        _steps.Add(step);
        return this;
    }

    public AgentWorkflowBuilder AddDecision(IDecision decision, object input, string? stepId = null)
    {
        var step = new DecisionStep
        {
            Id = stepId ?? Guid.NewGuid().ToString(),
            Decision = decision,
            InputData = input
        };
        
        _steps.Add(step);
        return this;
    }

    public SimpleWorkflow Build()
    {
        return new SimpleWorkflow
        {
            Name = _workflowName,
            Description = _workflowDescription,
            Steps = _steps,
            Variables = _variables
        };
    }
}

/// <summary>
/// Factory for creating workflow builders
/// </summary>
public static class WorkflowBuilderFactory
{
    public static AgentWorkflowBuilder CreateBuilder()
    {
        return new AgentWorkflowBuilder();
    }

    public static AgentWorkflowBuilder CreateBuilder(string name, string description)
    {
        return new AgentWorkflowBuilder()
            .WithName(name)
            .WithDescription(description);
    }
}
