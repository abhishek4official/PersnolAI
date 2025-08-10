using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ElsaWorkflowAgent.Kernel;
using ElsaWorkflowAgent.Agents;
using ElsaWorkflowAgent.Decisions;
using ElsaWorkflowAgent.Execution;
using ElsaWorkflowAgent.Workflows;
using Elsa.Extensions;

namespace ElsaWorkflowAgent.Extensions;

/// <summary>
/// Extension methods for configuring ElsaWorkflowAgent services with actual Elsa Workflow engine
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add ElsaWorkflowAgent services with actual Elsa Workflow engine integration
    /// </summary>
    public static IServiceCollection AddElsaWorkflowAgent(this IServiceCollection services, Action<ElsaWorkflowAgentOptions>? configure = null)
    {
        var options = new ElsaWorkflowAgentOptions();
        configure?.Invoke(options);

        // Register Elsa Workflow services
        services.AddElsa(elsa =>
        {
            elsa
                .UseIdentity(identity =>
                {
                    identity.TokenLifeSpan = TimeSpan.FromDays(1);
                    identity.SigningKey = "sufficiently-large-secret-signing-key-for-elsa-workflow-tokens";
                })
                .UseDefaultAuthentication()
                .UseWorkflowManagement(management =>
                {
                    management.UseFileSystemWorkflowDefinitionStore("Workflows");
                })
                .UseWorkflowRuntime(runtime =>
                {
                    runtime.UseDefaultRuntime();
                })
                .UseScheduling()
                .UseCSharp()
                .UseJavaScript()
                .UseLiquid()
                .UseHttp()
                .UseWorkflowsApi();
        });

        // Register our custom activities
        services.AddActivity<AgentActivity<object>>();
        services.AddActivity<DecisionActivity>();

        // Register kernel provider
        services.AddSingleton<IKernelProvider, SemanticKernelProvider>();
        
        // Register workflow engines
        services.AddScoped<IWorkflowEngine, ElsaWorkflowEngine>();
        services.AddScoped<IElsaWorkflowEngine, EnhancedElsaWorkflowEngine>();

        return services;
    }

    /// <summary>
    /// Add Semantic Kernel configuration to the ElsaWorkflowAgent
    /// </summary>
    public static IServiceCollection AddSemanticKernelConfiguration(this IServiceCollection services, Action<KernelProviderOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure<KernelProviderOptions>(configure);
        }
        else
        {
            services.Configure<KernelProviderOptions>(options => { });
        }

        return services;
    }

    /// <summary>
    /// Register a custom Semantic Kernel instance
    /// </summary>
    public static IServiceCollection AddCustomSemanticKernel(this IServiceCollection services, Microsoft.SemanticKernel.Kernel kernel)
    {
        services.AddSingleton(kernel);
        return services;
    }

    /// <summary>
    /// Register a custom Semantic Kernel factory
    /// </summary>
    public static IServiceCollection AddCustomSemanticKernel(this IServiceCollection services, Func<IServiceProvider, Microsoft.SemanticKernel.Kernel> kernelFactory)
    {
        services.AddSingleton(kernelFactory);
        return services;
    }

    /// <summary>
    /// Register an agent with the DI container
    /// </summary>
    public static IServiceCollection AddAgent<TAgent, TResponse>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TAgent : class, IAgent<TResponse>
    {
        services.Add(new ServiceDescriptor(typeof(IAgent<TResponse>), typeof(TAgent), lifetime));
        services.Add(new ServiceDescriptor(typeof(TAgent), typeof(TAgent), lifetime));
        return services;
    }

    /// <summary>
    /// Register a decision with the DI container
    /// </summary>
    public static IServiceCollection AddDecision<TDecision>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TDecision : class, IDecision
    {
        services.Add(new ServiceDescriptor(typeof(IDecision), typeof(TDecision), lifetime));
        services.Add(new ServiceDescriptor(typeof(TDecision), typeof(TDecision), lifetime));
        return services;
    }

    /// <summary>
    /// Register multiple agents from an assembly
    /// </summary>
    public static IServiceCollection AddAgentsFromAssembly(this IServiceCollection services, System.Reflection.Assembly assembly, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var agentTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAgent<>)))
            .ToList();

        foreach (var agentType in agentTypes)
        {
            var interfaceType = agentType.GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAgent<>));
            
            services.Add(new ServiceDescriptor(interfaceType, agentType, lifetime));
            services.Add(new ServiceDescriptor(agentType, agentType, lifetime));
        }

        return services;
    }

    /// <summary>
    /// Register multiple decisions from an assembly
    /// </summary>
    public static IServiceCollection AddDecisionsFromAssembly(this IServiceCollection services, System.Reflection.Assembly assembly, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var decisionTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IDecision).IsAssignableFrom(t))
            .ToList();

        foreach (var decisionType in decisionTypes)
        {
            services.Add(new ServiceDescriptor(typeof(IDecision), decisionType, lifetime));
            services.Add(new ServiceDescriptor(decisionType, decisionType, lifetime));
        }

        return services;
    }
}

/// <summary>
/// Configuration options for ElsaWorkflowAgent
/// </summary>
public class ElsaWorkflowAgentOptions
{
    /// <summary>
    /// Directory for storing workflow definitions
    /// </summary>
    public string WorkflowsDirectory { get; set; } = "Workflows";

    /// <summary>
    /// Directory for storing workflow templates
    /// </summary>
    public string WorkflowTemplatesDirectory { get; set; } = "WorkflowTemplates";

    /// <summary>
    /// Enable workflow persistence
    /// </summary>
    public bool EnablePersistence { get; set; } = true;

    /// <summary>
    /// Enable workflow scheduling
    /// </summary>
    public bool EnableScheduling { get; set; } = true;

    /// <summary>
    /// Enable HTTP activities
    /// </summary>
    public bool EnableHttp { get; set; } = true;

    /// <summary>
    /// Enable Workflows API
    /// </summary>
    public bool EnableApi { get; set; } = false;
}
