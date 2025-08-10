using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ElsaWorkflowAgent.Kernel;
using ElsaWorkflowAgent.Agents;
using ElsaWorkflowAgent.Decisions;
using ElsaWorkflowAgent.Execution;

namespace ElsaWorkflowAgent.Extensions;

/// <summary>
/// Extension methods for configuring ElsaWorkflowAgent services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add ElsaWorkflowAgent services to the DI container
    /// </summary>
    public static IServiceCollection AddElsaWorkflowAgent(this IServiceCollection services, Action<ElsaWorkflowAgentOptions>? configure = null)
    {
        var options = new ElsaWorkflowAgentOptions();
        configure?.Invoke(options);

        // Only register the kernel provider interface, without automatic configuration
        services.AddSingleton<IKernelProvider, SemanticKernelProvider>();
        
        // Register workflow engine
        services.AddScoped<IWorkflowEngine, SimpleWorkflowEngine>();

        return services;
    }

    /// <summary>
    /// Add Semantic Kernel configuration to the ElsaWorkflowAgent
    /// Call this separately if you want the default Semantic Kernel setup
    /// </summary>
    public static IServiceCollection AddSemanticKernelConfiguration(this IServiceCollection services, Action<KernelProviderOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure<KernelProviderOptions>(configure);
        }
        else
        {
            // Default empty configuration - user should configure manually
            services.Configure<KernelProviderOptions>(options => { });
        }

        return services;
    }

    /// <summary>
    /// Register a custom Semantic Kernel instance
    /// Use this if you want full control over Semantic Kernel configuration
    /// </summary>
    public static IServiceCollection AddCustomSemanticKernel(this IServiceCollection services, Microsoft.SemanticKernel.Kernel kernel)
    {
        services.AddSingleton(kernel);
        return services;
    }

    /// <summary>
    /// Register a custom Semantic Kernel factory
    /// Use this if you want to create the kernel with access to IServiceProvider
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
    // This class is kept for future extensibility
    // Currently no specific options, but can be extended as needed
}
