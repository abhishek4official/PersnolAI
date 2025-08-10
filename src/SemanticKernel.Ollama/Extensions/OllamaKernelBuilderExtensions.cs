using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using SemanticKernel.Ollama.Configuration;
using SemanticKernel.Ollama.Services;
using System.Diagnostics.CodeAnalysis;

namespace SemanticKernel.Ollama.Extensions;

/// <summary>
/// Extension methods for adding Ollama services to Semantic Kernel
/// </summary>
public static class OllamaKernelBuilderExtensions
{
    /// <summary>
    /// Adds Ollama chat completion service to the kernel builder
    /// </summary>
    /// <param name="builder">The kernel builder</param>
    /// <param name="modelName">The model name to use (optional, defaults to llama3.2)</param>
    /// <param name="endpoint">The Ollama endpoint (optional, defaults to http://localhost:11434)</param>
    /// <param name="serviceId">The service ID (optional)</param>
    /// <returns>The kernel builder for chaining</returns>
    public static IKernelBuilder AddOllamaChatCompletion(
        this IKernelBuilder builder,
        string? modelName = null,
        string? endpoint = null,
        string? serviceId = null)
    {
        return builder.AddOllamaChatCompletion(new OllamaOptions
        {
            ChatModel = modelName ?? "llama3.2",
            Endpoint = endpoint ?? "http://localhost:11434"
        }, serviceId);
    }

    /// <summary>
    /// Adds Ollama chat completion service to the kernel builder using options
    /// </summary>
    /// <param name="builder">The kernel builder</param>
    /// <param name="options">Ollama configuration options</param>
    /// <param name="serviceId">The service ID (optional)</param>
    /// <returns>The kernel builder for chaining</returns>
    public static IKernelBuilder AddOllamaChatCompletion(
        this IKernelBuilder builder,
        OllamaOptions options,
        string? serviceId = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        builder.Services.AddHttpClient();
        
        builder.Services.AddKeyedSingleton<IChatCompletionService>(serviceId, (serviceProvider, _) =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            
            return new OllamaChatCompletionService(httpClient, options.ChatModel, options.Endpoint, options.TimeoutMinutes);
        });

        return builder;
    }

    /// <summary>
    /// Adds Ollama text embedding service to the kernel builder
    /// </summary>
    /// <param name="builder">The kernel builder</param>
    /// <param name="modelName">The model name to use (optional, defaults to llama3.2)</param>
    /// <param name="endpoint">The Ollama endpoint (optional, defaults to http://localhost:11434)</param>
    /// <param name="serviceId">The service ID (optional)</param>
    /// <returns>The kernel builder for chaining</returns>
    [Experimental("SKEXP0001")]
    public static IKernelBuilder AddOllamaTextEmbeddingGeneration(
        this IKernelBuilder builder,
        string? modelName = null,
        string? endpoint = null,
        string? serviceId = null)
    {
        return builder.AddOllamaTextEmbeddingGeneration(new OllamaOptions
        {
            EmbeddingModel = modelName ?? "llama3.2",
            Endpoint = endpoint ?? "http://localhost:11434"
        }, serviceId);
    }

    /// <summary>
    /// Adds Ollama text embedding service to the kernel builder using options
    /// </summary>
    /// <param name="builder">The kernel builder</param>
    /// <param name="options">Ollama configuration options</param>
    /// <param name="serviceId">The service ID (optional)</param>
    /// <returns>The kernel builder for chaining</returns>
    [Experimental("SKEXP0001")]
    public static IKernelBuilder AddOllamaTextEmbeddingGeneration(
        this IKernelBuilder builder,
        OllamaOptions options,
        string? serviceId = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        builder.Services.AddHttpClient();
        
        builder.Services.AddKeyedSingleton<ITextEmbeddingGenerationService>(serviceId, (serviceProvider, _) =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            
            return new OllamaTextEmbeddingGenerationService(httpClient, options.EmbeddingModel, options.Endpoint, options.TimeoutMinutes);
        });

        return builder;
    }

    /// <summary>
    /// Adds both Ollama chat completion and text embedding services to the kernel builder
    /// </summary>
    /// <param name="builder">The kernel builder</param>
    /// <param name="options">Ollama configuration options</param>
    /// <param name="chatServiceId">The chat service ID (optional)</param>
    /// <param name="embeddingServiceId">The embedding service ID (optional)</param>
    /// <returns>The kernel builder for chaining</returns>
    [Experimental("SKEXP0001")]
    public static IKernelBuilder AddOllamaServices(
        this IKernelBuilder builder,
        OllamaOptions? options = null,
        string? chatServiceId = null,
        string? embeddingServiceId = null)
    {
        options ??= new OllamaOptions();

        return builder
            .AddOllamaChatCompletion(options, chatServiceId)
            .AddOllamaTextEmbeddingGeneration(options, embeddingServiceId);
    }
}
