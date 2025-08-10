using Microsoft.SemanticKernel;

namespace ElsaWorkflowAgent.Kernel;

/// <summary>
/// Provides access to Semantic Kernel instances
/// </summary>
public interface IKernelProvider
{
    /// <summary>
    /// Get the default kernel instance
    /// </summary>
    Microsoft.SemanticKernel.Kernel GetKernel();
    
    /// <summary>
    /// Get a named kernel instance
    /// </summary>
    /// <param name="name">The name of the kernel configuration</param>
    Microsoft.SemanticKernel.Kernel GetKernel(string name);
    
    /// <summary>
    /// Create a new kernel instance with custom configuration
    /// </summary>
    /// <param name="configure">Configuration action</param>
    Microsoft.SemanticKernel.Kernel CreateKernel(Action<IKernelBuilder> configure);
    
    /// <summary>
    /// Register a function with the default kernel
    /// </summary>
    /// <param name="function">The function to register</param>
    void RegisterFunction(KernelFunction function);
    
    /// <summary>
    /// Register a function with a named kernel
    /// </summary>
    /// <param name="kernelName">The name of the kernel</param>
    /// <param name="function">The function to register</param>
    void RegisterFunction(string kernelName, KernelFunction function);
    
    /// <summary>
    /// Register an agent as a function in the kernel
    /// </summary>
    /// <typeparam name="TResponse">The response type of the agent</typeparam>
    /// <param name="agent">The agent to register</param>
    void RegisterAgentAsFunction<TResponse>(Agents.IAgent<TResponse> agent);
    
    /// <summary>
    /// Register an agent as a function in a named kernel
    /// </summary>
    /// <typeparam name="TResponse">The response type of the agent</typeparam>
    /// <param name="kernelName">The name of the kernel</param>
    /// <param name="agent">The agent to register</param>
    void RegisterAgentAsFunction<TResponse>(string kernelName, Agents.IAgent<TResponse> agent);
}
