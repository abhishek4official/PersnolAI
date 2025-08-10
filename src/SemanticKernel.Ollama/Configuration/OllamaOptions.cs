namespace SemanticKernel.Ollama.Configuration;

/// <summary>
/// Configuration options for Ollama services
/// </summary>
public class OllamaOptions
{
    /// <summary>
    /// The Ollama endpoint URL (default: http://localhost:11434)
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// The default model name for chat completion (default: llama3.2)
    /// </summary>
    public string ChatModel { get; set; } = "llama3.2";

    /// <summary>
    /// The default model name for text embeddings (default: llama3.2)
    /// </summary>
    public string EmbeddingModel { get; set; } = "llama3.2";

    /// <summary>
    /// HTTP client timeout in seconds (default: 30) - Deprecated, use TimeoutMinutes instead
    /// </summary>
    [Obsolete("Use TimeoutMinutes instead")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// HTTP client timeout in minutes (default: 20 minutes)
    /// </summary>
    public double TimeoutMinutes { get; set; } = 20.0;
}
