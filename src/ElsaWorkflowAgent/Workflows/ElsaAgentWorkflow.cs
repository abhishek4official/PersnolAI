using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;
using Elsa.Workflows.Contracts;
using Elsa.Extensions;
using Microsoft.Extensions.DependencyInjection;
using ElsaWorkflowAgent.Agents;
using ElsaWorkflowAgent.Decisions;

namespace ElsaWorkflowAgent.Workflows;

/// <summary>
/// Custom Elsa activity that wraps our agent system
/// </summary>
public class AgentActivity<TResponse> : CodeActivity<TResponse>
{
    /// <summary>
    /// The agent to execute
    /// </summary>
    public Input<IAgent<TResponse>> Agent { get; set; } = default!;
    
    /// <summary>
    /// Input data for the agent
    /// </summary>
    public Input<object> InputData { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var agent = Agent.Get(context);
        var inputData = InputData.Get(context);
        
        var agentContext = new AgentContext
        {
            WorkflowInstanceId = context.WorkflowExecutionContext.Id,
            StepId = context.Id,
            ServiceProvider = context.GetRequiredService<IServiceProvider>(),
            Variables = context.ExpressionExecutionContext.Memory.ToDictionary(
                kvp => kvp.Key, 
                kvp => kvp.Value.Value ?? new object())
        };

        var result = await agent.ExecuteAsync(inputData, agentContext, context.CancellationToken);
        
        context.SetResult(result);
    }
}

/// <summary>
/// Custom Elsa activity that wraps our decision system
/// </summary>
public class DecisionActivity : Activity<string>
{
    /// <summary>
    /// The decision to execute
    /// </summary>
    public Input<IDecision> Decision { get; set; } = default!;
    
    /// <summary>
    /// Input data for the decision
    /// </summary>
    public Input<object> InputData { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var decision = Decision.Get(context);
        var inputData = InputData.Get(context);
        
        var decisionContext = new DecisionContext
        {
            WorkflowInstanceId = context.WorkflowExecutionContext.Id,
            StepId = context.Id,
            ServiceProvider = context.GetRequiredService<IServiceProvider>(),
            Variables = context.ExpressionExecutionContext.Memory.ToDictionary(
                kvp => kvp.Key, 
                kvp => kvp.Value.Value ?? new object())
        };

        var result = await decision.ExecuteAsync(inputData, decisionContext, context.CancellationToken);
        
        // Set the next step outcome for Elsa workflow routing
        context.SetResult(result.NextStep);
        
        // Store additional result data in workflow memory
        if (result.Data != null)
        {
            context.Set("DecisionData", result.Data);
        }
    }
}

/// <summary>
/// Builder for creating Elsa workflows with agents and decisions
/// </summary>
public class ElsaAgentWorkflowBuilder
{
    private readonly List<IActivity> _activities = new();
    private readonly Dictionary<string, object> _variables = new();
    private string _workflowName = "AgentWorkflow";
    private string _workflowDescription = "Agent-based workflow";

    public ElsaAgentWorkflowBuilder WithName(string name)
    {
        _workflowName = name;
        return this;
    }

    public ElsaAgentWorkflowBuilder WithDescription(string description)
    {
        _workflowDescription = description;
        return this;
    }

    public ElsaAgentWorkflowBuilder WithVariable(string name, object value)
    {
        _variables[name] = value;
        return this;
    }

    /// <summary>
    /// Add an agent activity to the workflow
    /// </summary>
    public ElsaAgentWorkflowBuilder AddAgent<TResponse>(IAgent<TResponse> agent, object input)
    {
        var activity = new AgentActivity<TResponse>
        {
            Agent = new Input<IAgent<TResponse>>(agent),
            InputData = new Input<object>(input),
            Id = ActivityIdGenerator.Generate()
        };
        
        _activities.Add(activity);
        return this;
    }

    /// <summary>
    /// Add a decision activity to the workflow
    /// </summary>
    public ElsaAgentWorkflowBuilder AddDecision(IDecision decision, object input)
    {
        var activity = new DecisionActivity
        {
            Decision = new Input<IDecision>(decision),
            InputData = new Input<object>(input),
            Id = ActivityIdGenerator.Generate()
        };
        
        _activities.Add(activity);
        return this;
    }

    /// <summary>
    /// Add a conditional flow based on decision outcome
    /// </summary>
    public ElsaAgentWorkflowBuilder AddConditionalFlow(string condition, Action<ElsaAgentWorkflowBuilder> trueBranch, Action<ElsaAgentWorkflowBuilder>? falseBranch = null)
    {
        var ifActivity = new If
        {
            Condition = new Input<bool>(condition),
            Id = ActivityIdGenerator.Generate()
        };

        // Build true branch
        var trueBranchBuilder = new ElsaAgentWorkflowBuilder();
        trueBranch(trueBranchBuilder);
        var trueBranchWorkflow = trueBranchBuilder.BuildSequence();
        ifActivity.Then = trueBranchWorkflow;

        // Build false branch if provided
        if (falseBranch != null)
        {
            var falseBranchBuilder = new ElsaAgentWorkflowBuilder();
            falseBranch(falseBranchBuilder);
            var falseBranchWorkflow = falseBranchBuilder.BuildSequence();
            ifActivity.Else = falseBranchWorkflow;
        }

        _activities.Add(ifActivity);
        return this;
    }

    /// <summary>
    /// Add a parallel execution block
    /// </summary>
    public ElsaAgentWorkflowBuilder AddParallel(params Action<ElsaAgentWorkflowBuilder>[] branches)
    {
        var parallelActivity = new Parallel
        {
            Id = ActivityIdGenerator.Generate()
        };

        var branchActivities = new List<IActivity>();
        foreach (var branch in branches)
        {
            var branchBuilder = new ElsaAgentWorkflowBuilder();
            branch(branchBuilder);
            var branchWorkflow = branchBuilder.BuildSequence();
            branchActivities.Add(branchWorkflow);
        }

        parallelActivity.Branches = branchActivities;
        _activities.Add(parallelActivity);
        return this;
    }

    /// <summary>
    /// Build a sequence of activities
    /// </summary>
    private IActivity BuildSequence()
    {
        if (_activities.Count == 0)
            return new Inline();

        if (_activities.Count == 1)
            return _activities[0];

        var sequence = new Sequence
        {
            Activities = _activities,
            Id = ActivityIdGenerator.Generate()
        };

        return sequence;
    }

    /// <summary>
    /// Build the final Elsa workflow
    /// </summary>
    public Workflow BuildElsaWorkflow()
    {
        var workflow = new Workflow
        {
            Identity = new WorkflowIdentity
            {
                Id = Guid.NewGuid().ToString(),
                Name = _workflowName,
                Description = _workflowDescription,
                Version = 1
            },
            Root = BuildSequence(),
            Variables = _variables.Select(kvp => new Variable
            {
                Id = kvp.Key,
                Name = kvp.Key,
                Value = kvp.Value
            }).ToList()
        };

        return workflow;
    }
}

/// <summary>
/// Factory for creating Elsa agent workflows
/// </summary>
public static class ElsaWorkflowBuilderFactory
{
    public static ElsaAgentWorkflowBuilder CreateBuilder()
    {
        return new ElsaAgentWorkflowBuilder();
    }

    public static ElsaAgentWorkflowBuilder CreateBuilder(string name, string description)
    {
        return new ElsaAgentWorkflowBuilder()
            .WithName(name)
            .WithDescription(description);
    }
}

/// <summary>
/// Helper class for generating unique activity IDs
/// </summary>
public static class ActivityIdGenerator
{
    public static string Generate() => Guid.NewGuid().ToString("N")[..8];
}