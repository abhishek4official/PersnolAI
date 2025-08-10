using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticKernel.Ollama.Services;

/// <summary>
/// Ollama text embedding generation service implementation for Semantic Kernel
/// </summary>
[Experimental("SKEXP0001")]
public class OllamaTextEmbeddingGenerationService : ITextEmbeddingGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;
    private readonly string _endpoint;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the OllamaTextEmbeddingGenerationService
    /// </summary>
    /// <param name="httpClient">HTTP client for making requests</param>
    /// <param name="modelName">Name of the Ollama embedding model to use (default: llama3.2)</param>
    /// <param name="endpoint">Ollama endpoint URL (default: http://localhost:11434)</param>
    /// <param name="timeoutMinutes">Request timeout in minutes (default: 20 minutes)</param>
    public OllamaTextEmbeddingGenerationService(
        HttpClient httpClient,
        string modelName = "llama3.2",
        string endpoint = "http://localhost:11434",
        double timeoutMinutes = 20.0)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _modelName = modelName ?? throw new ArgumentNullException(nameof(modelName));
        _endpoint = endpoint?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(endpoint));
        _timeout = TimeSpan.FromMinutes(timeoutMinutes);
        
        // Set the timeout on the HttpClient
        _httpClient.Timeout = _timeout;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>
    {
        ["ModelName"] = _modelName,
        ["Endpoint"] = _endpoint,
        ["Provider"] = "Ollama",
        ["TimeoutMinutes"] = _timeout.TotalMinutes
    };

    /// <inheritdoc/>
    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data, 
        Kernel? kernel = null, 
        CancellationToken cancellationToken = default)
    {
        var results = new List<ReadOnlyMemory<float>>();

        foreach (var text in data)
        {
            var embedding = await GenerateEmbeddingInternalAsync(text, cancellationToken);
            results.Add(embedding);
        }

        return results;
    }

    /// <summary>
    /// Generates an embedding for a single text input
    /// </summary>
    /// <param name="text">The text to generate an embedding for</param>
    /// <param name="kernel">Optional kernel parameter (not used)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The embedding as a ReadOnlyMemory of floats</returns>
    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text, 
        Kernel? kernel = null, 
        CancellationToken cancellationToken = default)
    {
        return await GenerateEmbeddingInternalAsync(text, cancellationToken);
    }

    private async Task<ReadOnlyMemory<float>> GenerateEmbeddingInternalAsync(string text, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = _modelName,
            prompt = text
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            // Create a timeout token that respects both the cancellation token and the configured timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);
            
            var response = await _httpClient.PostAsync($"{_endpoint}/api/embeddings", content, timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            
            // Configure JsonSerializer options for case-insensitive property matching
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var embeddingResponse = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseJson, options);

            if (embeddingResponse?.Embedding != null)
            {
                return new ReadOnlyMemory<float>(embeddingResponse.Embedding);
            }

            throw new KernelException("Empty embedding response from Ollama");
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Ollama embedding request timed out after {_timeout.TotalMinutes} minutes. Consider increasing the timeout configuration.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new KernelException($"Error calling Ollama embeddings API at {_endpoint}: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new KernelException($"Error parsing Ollama embeddings response: {ex.Message}", ex);
        }
    }

    private class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
