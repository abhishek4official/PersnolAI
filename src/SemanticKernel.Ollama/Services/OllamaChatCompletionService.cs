using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.TextGeneration;
using Microsoft.SemanticKernel.Services;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticKernel.Ollama.Services;

/// <summary>
/// Ollama chat completion service implementation for Semantic Kernel
/// </summary>
public class OllamaChatCompletionService : IChatCompletionService, ITextGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;
    private readonly string _endpoint;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the OllamaChatCompletionService
    /// </summary>
    /// <param name="httpClient">HTTP client for making requests</param>
    /// <param name="modelName">Name of the Ollama model to use (default: llama3.2)</param>
    /// <param name="endpoint">Ollama endpoint URL (default: http://localhost:11434)</param>
    /// <param name="timeoutMinutes">Request timeout in minutes (default: 20 minutes)</param>
    public OllamaChatCompletionService(
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
    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory, 
        PromptExecutionSettings? executionSettings = null, 
        Kernel? kernel = null, 
        CancellationToken cancellationToken = default)
    {
        var response = new StringBuilder();
        
        await foreach (var content in GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken))
        {
            response.Append(content.Content);
        }

        return new List<ChatMessageContent>
        {
            new ChatMessageContent(AuthorRole.Assistant, response.ToString())
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory, 
        PromptExecutionSettings? executionSettings = null, 
        Kernel? kernel = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = chatHistory.Select(msg => new
        {
            role = msg.Role.Label.ToLowerInvariant(),
            content = msg.Content
        }).ToList();

        var requestBody = new
        {
            model = _modelName,
            messages = messages,
            stream = true
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            // Create a timeout token that respects both the cancellation token and the configured timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);
            
            response = await _httpClient.PostAsync($"{_endpoint}/api/chat", content, timeoutCts.Token);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Ollama request timed out after {_timeout.TotalMinutes} minutes. Consider increasing the timeout configuration.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new KernelException($"Error calling Ollama API at {_endpoint}: {ex.Message}", ex);
        }

        // Process the streaming response outside of try-catch to allow yield
        await foreach (var streamContent in ProcessStreamResponseAsync(response, cancellationToken))
        {
            yield return streamContent;
        }
    }

    private async IAsyncEnumerable<StreamingChatMessageContent> ProcessStreamResponseAsync(
        HttpResponseMessage response, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            OllamaStreamResponse? streamResponse = null;
            try
            {
                streamResponse = JsonSerializer.Deserialize<OllamaStreamResponse>(line, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                // Log the problematic line for debugging if needed
                // Consider using ILogger here in a future version
                System.Diagnostics.Debug.WriteLine($"Failed to deserialize Ollama response: {line} - Error: {ex.Message}");
                continue;
            }

            if (streamResponse?.Message?.Content != null)
            {
                yield return new StreamingChatMessageContent(AuthorRole.Assistant, streamResponse.Message.Content);
            }

            if (streamResponse?.Done == true)
                break;
        }
    }

    // ITextGenerationService implementation
    /// <inheritdoc/>
    public async Task<IReadOnlyList<TextContent>> GetTextContentsAsync(
        string prompt, 
        PromptExecutionSettings? executionSettings = null, 
        Kernel? kernel = null, 
        CancellationToken cancellationToken = default)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);
        
        var chatResults = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        
        return chatResults.Select(result => new TextContent(result.Content)).ToList();
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<StreamingTextContent> GetStreamingTextContentsAsync(
        string prompt, 
        PromptExecutionSettings? executionSettings = null, 
        Kernel? kernel = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);
        
        await foreach (var streamContent in GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken))
        {
            yield return new StreamingTextContent(streamContent.Content);
        }
    }

    private class OllamaStreamResponse
    {
        public string? Model { get; set; }
        
        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }
        
        public OllamaMessage? Message { get; set; }
        
        public bool Done { get; set; }
    }

    private class OllamaMessage
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
    }
}
