# ElsaWorkflowAgent

A .NET Class Library for dynamic planning using Elsa Workflow with agent-based architecture, similar to LangGraph but for .NET ecosystem.

## Features

- **IAgent<TResponse>**: Generic interface for creating reusable agents that can be composed into workflows
- **IDecision**: Interface for injecting decision logic into workflows for dynamic routing
- **Semantic Kernel Integration**: Automatic Semantic Kernel object provisioning and function registration
- **Elsa Workflow Integration**: Custom activities for executing agents and decisions within workflows
- **Dynamic Workflow Builder**: Fluent API for creating complex workflows programmatically
- **Dependency Injection**: Full support for .NET DI container with automatic service registration

## Architecture

### Core Interfaces

#### IAgent<TResponse>
```csharp
public interface IAgent<TResponse>
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    Task<TResponse> ExecuteAsync(object input, AgentContext context, CancellationToken cancellationToken = default);
    bool CanHandle(object input);
    AgentMetadata Metadata { get; }
}
```

#### IDecision
```csharp
public interface IDecision
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    Task<DecisionResult> ExecuteAsync(object input, DecisionContext context, CancellationToken cancellationToken = default);
    bool CanHandle(object input);
    DecisionMetadata Metadata { get; }
}
```

## Getting Started

### 1. Add Package Reference

```xml
<PackageReference Include="ElsaWorkflowAgent" Version="1.0.0" />
```

### 2. Configure Services

```csharp
using ElsaWorkflowAgent.Extensions;

var builder = Host.CreateDefaultBuilder();
builder.ConfigureServices(services =>
{
    services.AddElsaWorkflowAgent(options =>
    {
        options.OpenAIApiKey = "your-api-key";
        options.UseEntityFramework = false; // Use in-memory for development
    });
    
    // Register your agents and decisions
    services.AddAgent<MyAnalysisAgent, string>();
    services.AddDecision<MyRouteDecision>();
});
```

### 3. Create an Agent

```csharp
public class MyAnalysisAgent : BaseAgent<string>
{
    public MyAnalysisAgent(ILogger<MyAnalysisAgent> logger, Kernel kernel) 
        : base(logger, kernel) { }

    public override string Id => "analysis-agent";
    public override string Name => "Analysis Agent";
    public override string Description => "Analyzes input data";
    
    public override AgentMetadata Metadata => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        InputType = typeof(object),
        OutputType = typeof(string)
    };

    protected override async Task<string> ExecuteInternalAsync(
        object input, 
        AgentContext context, 
        CancellationToken cancellationToken)
    {
        // Your agent logic here
        return "Analysis complete";
    }
}
```

### 4. Create a Decision

```csharp
public class MyRouteDecision : BaseDecision
{
    public MyRouteDecision(ILogger<MyRouteDecision> logger) : base(logger) { }

    public override string Id => "route-decision";
    public override string Name => "Route Decision";
    public override string Description => "Routes based on analysis";
    
    public override DecisionMetadata Metadata => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        PossibleOutcomes = new List<string> { "path-a", "path-b" }
    };

    protected override async Task<DecisionResult> ExecuteInternalAsync(
        object input, 
        DecisionContext context, 
        CancellationToken cancellationToken)
    {
        // Your decision logic here
        return DecisionResult.Success("path-a", new { confidence = 0.9 });
    }
}
```

### 5. Build Dynamic Workflows

```csharp
var workflow = WorkflowBuilderFactory
    .CreateBuilder("MyWorkflow", "A sample workflow")
    .WithVariable("data", inputData)
    .AddAgent(analysisAgent, inputData)
    .AddDecision(routeDecision, analysisResult)
    .AddAgent(generatorAgent, processedData)
    .Build();
```

### 6. Register Agents as Semantic Kernel Functions

```csharp
var kernelProvider = serviceProvider.GetRequiredService<IKernelProvider>();
kernelProvider.RegisterAgentAsFunction(myAgent);

// Now your agent can be called as a Semantic Kernel function
var kernel = kernelProvider.GetKernel();
var result = await kernel.InvokeAsync("analysis-agent", new { input = "data" });
```

## Advanced Features

### Parallel Execution
```csharp
.AddParallel(
    new AgentActivity<string> { Agent = agent1, InputData = data1 },
    new AgentActivity<string> { Agent = agent2, InputData = data2 }
)
```

### Conditional Logic
```csharp
.AddIf(
    () => someCondition,
    thenActivity: successAgent,
    elseActivity: fallbackAgent
)
```

### Loops
```csharp
.AddWhile(
    () => hasMoreData,
    body: processingAgent
)
```

### ForEach
```csharp
.AddForEach(
    items: dataList,
    activityFactory: item => new AgentActivity<string> { InputData = item }
)
```

## Context and State Management

Agents and decisions receive context objects that provide:
- Workflow instance information
- Service provider access
- Property bag for data sharing
- Step identification

```csharp
protected override async Task<string> ExecuteInternalAsync(
    object input, 
    AgentContext context, 
    CancellationToken cancellationToken)
{
    // Access services
    var someService = GetService<ISomeService>(context);
    
    // Get/set context properties
    var previousResult = GetContextProperty<string>(context, "previous-result");
    SetContextProperty(context, "my-result", "some value");
    
    return "result";
}
```

## Configuration Options

```csharp
services.AddElsaWorkflowAgent(options =>
{
    // Database options
    options.UseEntityFramework = true;
    options.ConnectionString = "Data Source=workflows.db";
    
    // OpenAI configuration
    options.OpenAIApiKey = "sk-...";
    options.OpenAIModel = "gpt-4";
    
    // Azure OpenAI configuration
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com/";
    options.AzureOpenAIApiKey = "your-key";
    options.AzureOpenAIModel = "gpt-4";
});
```

## Best Practices

1. **Agent Design**: Keep agents focused on single responsibilities
2. **Error Handling**: Use the built-in logging and error handling in BaseAgent/BaseDecision
3. **State Management**: Use context properties for sharing data between agents
4. **Testing**: Agents and decisions are easily unit testable
5. **Performance**: Use cancellation tokens for long-running operations

## Examples

See the `samples/SampleAgent` project for a complete working example demonstrating:
- Agent creation and registration
- Decision logic implementation
- Dynamic workflow building
- Semantic Kernel integration
- Context sharing between components

## License

MIT License - see LICENSE file for details.
