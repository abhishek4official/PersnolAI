using ElsaWorkflowAgent.Agents;
using ElsaWorkflowAgent.Decisions;
using ElsaWorkflowAgent.Workflows;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ElsaWorkflowAgent.Execution;

/// <summary>
/// Simple workflow engine implementation
/// </summary>
public class SimpleWorkflowEngine : IWorkflowEngine
{
    private readonly ILogger<SimpleWorkflowEngine> _logger;

    public SimpleWorkflowEngine(ILogger<SimpleWorkflowEngine> logger)
    {
        _logger = logger;
    }

    public async Task<WorkflowExecutionResult> ExecuteAsync(SimpleWorkflow workflow, object inputData, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting execution of workflow: {WorkflowName}", workflow.Name);
        var stepResults = new List<StepExecutionResult>();
        var currentData = inputData;
        var routingDecision = "";

        try
        {
            for (int i = 0; i < workflow.Steps.Count; i++)
            {
                var step = workflow.Steps[i];
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    // Skip agents that don't match the routing decision
                    if (ShouldSkipStep(step, routingDecision))
                    {
                        _logger.LogInformation("Skipping step {StepIndex}: {StepType} (route mismatch)", i + 1, step.GetType().Name);
                        continue;
                    }

                    _logger.LogInformation("Executing step {StepIndex}: {StepType}", i + 1, step.GetType().Name);

                    var stepResult = await ExecuteStepAsync(step, currentData, workflow, cancellationToken);
                    stopwatch.Stop();

                    stepResults.Add(new StepExecutionResult
                    {
                        StepId = GetStepId(step),
                        StepType = step.GetType().Name,
                        Success = true,
                        Result = stepResult,
                        ExecutionTime = stopwatch.Elapsed
                    });

                    // Update routing decision if this was a decision step
                    if (step is DecisionStep && stepResult is DecisionResult decisionResult)
                    {
                        routingDecision = decisionResult.NextStep ?? "";
                        _logger.LogInformation("Decision outcome: {Route}", routingDecision);
                    }

                    // Update current data for next step (only if result is not empty)
                    if (stepResult != null && !string.IsNullOrEmpty(stepResult.ToString()))
                    {
                        currentData = stepResult;
                    }

                    _logger.LogInformation("Step {StepIndex} completed successfully in {ExecutionTime}ms", 
                        i + 1, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex, "Step {StepIndex} failed after {ExecutionTime}ms", 
                        i + 1, stopwatch.ElapsedMilliseconds);

                    stepResults.Add(new StepExecutionResult
                    {
                        StepId = GetStepId(step),
                        StepType = step.GetType().Name,
                        Success = false,
                        ErrorMessage = ex.Message,
                        ExecutionTime = stopwatch.Elapsed
                    });

                    return WorkflowExecutionResult.CreateFailure($"Step {i + 1} failed: {ex.Message}", ex);
                }
            }

            _logger.LogInformation("Workflow {WorkflowName} completed successfully", workflow.Name);
            return WorkflowExecutionResult.CreateSuccess(currentData, stepResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow {WorkflowName} execution failed", workflow.Name);
            return WorkflowExecutionResult.CreateFailure($"Workflow execution failed: {ex.Message}", ex);
        }
    }

    private bool ShouldSkipStep(object step, string routingDecision)
    {
        if (string.IsNullOrEmpty(routingDecision) || step is DecisionStep)
            return false;

        // Handle AgentStep generically using reflection
        var stepType = step.GetType();
        if (stepType.IsGenericType && stepType.GetGenericTypeDefinition() == typeof(AgentStep<>))
        {
            var agentProperty = stepType.GetProperty("Agent");
            if (agentProperty?.GetValue(step) is IAgent<object> agent)
            {
                return agent.Id switch
                {
                    "greeting_agent" => routingDecision != "greeting",
                    "name_agent" => routingDecision != "name",
                    "llm_agent" => routingDecision != "llm",
                    _ => false
                };
            }
        }

        return false;
    }

    private async Task<object> ExecuteStepAsync(object step, object inputData, SimpleWorkflow workflow, CancellationToken cancellationToken)
    {
        // Handle EnhancedAgentStep<T> first
        var stepType = step.GetType();
        if (stepType.IsGenericType && stepType.GetGenericTypeDefinition() == typeof(EnhancedAgentStep<>))
        {
            return await ExecuteEnhancedAgentStepAsync(step, inputData, workflow, cancellationToken);
        }
        
        // Handle regular AgentStep<T> generically using reflection
        if (stepType.IsGenericType && stepType.GetGenericTypeDefinition() == typeof(AgentStep<>))
        {
            return await ExecuteAgentStepGenericAsync(step, inputData, workflow, cancellationToken);
        }

        return step switch
        {
            DecisionStep decisionStep => await ExecuteDecisionStepAsync(decisionStep, inputData, workflow, cancellationToken),
            _ => throw new NotSupportedException($"Step type {step.GetType().Name} is not supported")
        };
    }

    private async Task<object> ExecuteEnhancedAgentStepAsync(object enhancedStep, object inputData, SimpleWorkflow workflow, CancellationToken cancellationToken)
    {
        var stepType = enhancedStep.GetType();
        var agentProperty = stepType.GetProperty("Agent") ?? throw new InvalidOperationException("Agent property not found");
        var usePreviousResultProperty = stepType.GetProperty("UsePreviousResult");
        var inputTransformProperty = stepType.GetProperty("InputTransform");
        var idProperty = stepType.GetProperty("Id") ?? throw new InvalidOperationException("Id property not found");

        var agent = agentProperty.GetValue(enhancedStep) ?? throw new InvalidOperationException("Agent is null");
        var usePreviousResult = (bool)(usePreviousResultProperty?.GetValue(enhancedStep) ?? false);
        var inputTransform = inputTransformProperty?.GetValue(enhancedStep) as InputTransform;
        var stepId = idProperty.GetValue(enhancedStep)?.ToString() ?? Guid.NewGuid().ToString();

        var context = new AgentContext
        {
            WorkflowInstanceId = Guid.NewGuid().ToString(),
            StepId = stepId,
            Variables = workflow.Variables
        };

        // Use previous result if specified
        object stepInput;
        if (usePreviousResult)
        {
            stepInput = inputData; // Start with previous result
            
            // Apply transformation if specified
            if (inputTransform != null)
            {
                try
                {
                    // Invoke the delegate directly
                    stepInput = inputTransform(inputData);
                    _logger.LogInformation("Applied transformation for step {StepId}. Input type: {InputType}, Output type: {OutputType}", 
                        stepId, inputData?.GetType().Name ?? "null", stepInput?.GetType().Name ?? "null");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying input transformation for step: {StepId}", stepId);
                    stepInput = inputData; // Fall back to original input
                }
            }
        }
        else
        {
            var inputDataProperty = stepType.GetProperty("InputData");
            stepInput = inputDataProperty?.GetValue(enhancedStep) ?? inputData;
        }

        _logger.LogInformation("Executing enhanced agent {StepId} with transformed input: {InputType}", 
            stepId, stepInput?.GetType().Name ?? "null");

        // Execute the agent using reflection
        var executeMethod = agent.GetType().GetMethod("ExecuteAsync") ?? throw new InvalidOperationException("ExecuteAsync method not found");
        var task = (Task)executeMethod.Invoke(agent, new object[] { stepInput ?? new object(), context, cancellationToken })!;
        await task;

        // Get the result from the completed task
        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task) ?? string.Empty;
    }

    private async Task<object> ExecuteAgentStepGenericAsync(object agentStep, object inputData, SimpleWorkflow workflow, CancellationToken cancellationToken)
    {
        var stepType = agentStep.GetType();
        var agentProperty = stepType.GetProperty("Agent") ?? throw new InvalidOperationException("Agent property not found");
        var inputDataProperty = stepType.GetProperty("InputData");
        var idProperty = stepType.GetProperty("Id") ?? throw new InvalidOperationException("Id property not found");

        var agent = agentProperty.GetValue(agentStep) ?? throw new InvalidOperationException("Agent is null");
        var stepInputData = inputDataProperty?.GetValue(agentStep);
        var stepId = idProperty.GetValue(agentStep)?.ToString() ?? Guid.NewGuid().ToString();

        var context = new AgentContext
        {
            WorkflowInstanceId = Guid.NewGuid().ToString(),
            StepId = stepId,
            Variables = workflow.Variables
        };

        // Use the step's input data if specified, otherwise use the current workflow data
        var stepInput = stepInputData ?? inputData;
        
        _logger.LogInformation("Executing agent with input: {Input}", 
            stepInput?.GetType().Name ?? "null");

        // Use reflection to call ExecuteAsync method
        var executeMethod = agent.GetType().GetMethod("ExecuteAsync") ?? throw new InvalidOperationException("ExecuteAsync method not found");
        var task = (Task)executeMethod.Invoke(agent, new object[] { stepInput ?? new object(), context, cancellationToken })!;
        await task;

        // Get the result from the completed task
        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task) ?? string.Empty;
    }

    private async Task<object> ExecuteDecisionStepAsync(DecisionStep decisionStep, object inputData, SimpleWorkflow workflow, CancellationToken cancellationToken)
    {
        var context = new DecisionContext
        {
            WorkflowInstanceId = Guid.NewGuid().ToString(),
            StepId = decisionStep.Id,
            Variables = workflow.Variables
        };

        // Use the step's input data if specified, otherwise use the current workflow data
        var stepInput = decisionStep.InputData ?? inputData;

        _logger.LogInformation("Executing decision {DecisionId} with input: {Input}", 
            decisionStep.Decision.Id, stepInput?.GetType().Name ?? "null");

        var result = await decisionStep.Decision.ExecuteAsync(stepInput, context, cancellationToken);
        
        _logger.LogInformation("Decision {DecisionId} chose path: {NextStep}", 
            decisionStep.Decision.Id, result.NextStep);

        return result;
    }

    private static string GetStepId(object step)
    {
        return step switch
        {
            DecisionStep decisionStep => decisionStep.Id,
            _ => GetStepIdGeneric(step)
        };
    }

    private static string GetStepIdGeneric(object step)
    {
        var stepType = step.GetType();
        var idProperty = stepType.GetProperty("Id");
        return idProperty?.GetValue(step)?.ToString() ?? Guid.NewGuid().ToString();
    }
}
