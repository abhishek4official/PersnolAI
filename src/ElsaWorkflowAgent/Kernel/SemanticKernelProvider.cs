using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using System.Collections.Concurrent;
using System.ComponentModel;
using ElsaWorkflowAgent.Agents;

namespace ElsaWorkflowAgent.Kernel;

/// <summary>
/// Default implementation of IKernelProvider
/// </summary>
public class SemanticKernelProvider : IKernelProvider
{
    private readonly ILogger<SemanticKernelProvider> _logger;
    private readonly KernelProviderOptions _options;
    private readonly ConcurrentDictionary<string, Microsoft.SemanticKernel.Kernel> _kernels = new();
    private readonly Microsoft.SemanticKernel.Kernel _defaultKernel;
    private readonly Dictionary<string, KernelFunction> _customFunctions = new();
    private KernelPlugin? _customPlugin;

    public SemanticKernelProvider(ILogger<SemanticKernelProvider> logger, IOptions<KernelProviderOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _defaultKernel = CreateDefaultKernel();
    }

    public Microsoft.SemanticKernel.Kernel GetKernel()
    {
        return _defaultKernel;
    }

    public Microsoft.SemanticKernel.Kernel GetKernel(string name)
    {
        return _kernels.GetOrAdd(name, _ => CreateNamedKernel(name));
    }

    public Microsoft.SemanticKernel.Kernel CreateKernel(Action<IKernelBuilder> configure)
    {
        var builder = Microsoft.SemanticKernel.Kernel.CreateBuilder();
        configure(builder);
        return builder.Build();
    }

    public void RegisterFunction(KernelFunction function)
    {
        _customFunctions[function.Name] = function;
        UpdateCustomPlugin(_defaultKernel);
        _logger.LogInformation("Registered function {FunctionName} with default kernel", function.Name);
    }

    public void RegisterFunction(string kernelName, KernelFunction function)
    {
        var kernel = GetKernel(kernelName);
        _customFunctions[function.Name] = function;
        UpdateCustomPlugin(kernel);
        _logger.LogInformation("Registered function {FunctionName} with kernel {KernelName}", function.Name, kernelName);
    }

    private void UpdateCustomPlugin(Microsoft.SemanticKernel.Kernel kernel)
    {
        // Remove existing custom plugin if it exists
        var existingPlugin = kernel.Plugins.FirstOrDefault(p => p.Name == "CustomFunctions");
        if (existingPlugin != null)
        {
            kernel.Plugins.Remove(existingPlugin);
        }

        // Create new plugin with all functions
        if (_customFunctions.Any())
        {
            var newPlugin = KernelPluginFactory.CreateFromFunctions("CustomFunctions", _customFunctions.Values);
            kernel.Plugins.Add(newPlugin);
        }
    }

    public void RegisterAgentAsFunction<TResponse>(IAgent<TResponse> agent)
    {
        var function = CreateAgentFunction(agent);
        RegisterFunction(function);
    }

    public void RegisterAgentAsFunction<TResponse>(string kernelName, IAgent<TResponse> agent)
    {
        var function = CreateAgentFunction(agent);
        RegisterFunction(kernelName, function);
    }

    private KernelFunction CreateAgentFunction<TResponse>(IAgent<TResponse> agent)
    {
        return KernelFunctionFactory.CreateFromMethod(
            async (object input, AgentContext context, CancellationToken cancellationToken) =>
            {
                return await agent.ExecuteAsync(input, context, cancellationToken);
            },
            functionName: agent.Id,
            description: agent.Description,
            parameters: new[]
            {
                new KernelParameterMetadata("input") { Description = "Input data for the agent", ParameterType = typeof(object) },
                new KernelParameterMetadata("context") { Description = "Agent execution context", ParameterType = typeof(AgentContext) }
            },
            returnParameter: new KernelReturnParameterMetadata { Description = "Agent response", ParameterType = typeof(TResponse) }
        );
    }

    private Microsoft.SemanticKernel.Kernel CreateDefaultKernel()
    {
        var builder = Microsoft.SemanticKernel.Kernel.CreateBuilder();
        
        // Configure default services based on options
        if (!string.IsNullOrEmpty(_options.OpenAIApiKey))
        {
            builder.AddOpenAIChatCompletion(_options.OpenAIModel, _options.OpenAIApiKey);
        }
        else if (!string.IsNullOrEmpty(_options.AzureOpenAIEndpoint))
        {
            builder.AddAzureOpenAIChatCompletion(_options.AzureOpenAIModel, _options.AzureOpenAIEndpoint, _options.AzureOpenAIApiKey);
        }

        return builder.Build();
    }

    private Microsoft.SemanticKernel.Kernel CreateNamedKernel(string name)
    {
        // For named kernels, use the same configuration as default for now
        // This can be extended to support different configurations per named kernel
        return CreateDefaultKernel();
    }
}

/// <summary>
/// Configuration options for the Kernel Provider
/// </summary>
public class KernelProviderOptions
{
    public string OpenAIApiKey { get; set; } = string.Empty;
    public string OpenAIModel { get; set; } = "gpt-3.5-turbo";
    public string AzureOpenAIEndpoint { get; set; } = string.Empty;
    public string AzureOpenAIApiKey { get; set; } = string.Empty;
    public string AzureOpenAIModel { get; set; } = "gpt-35-turbo";
}
