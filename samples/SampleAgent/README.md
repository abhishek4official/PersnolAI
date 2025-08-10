# Ollama Integration for ElsaWorkflowAgent

This sample demonstrates how to configure Semantic Kernel with local Ollama for both chat completion and text embeddings.

## Prerequisites

1. **Install Ollama**: Download and install from [https://ollama.ai/](https://ollama.ai/)

2. **Pull Required Models**:
   ```bash
   ollama pull llama3.2
   ollama pull nomic-embed-text
   ```

3. **Start Ollama Server**: The server usually starts automatically after installation, or run:
   ```bash
   ollama serve
   ```

## Configuration

The sample is configured to use:
- **Chat Model**: `llama3.2` (you can change this to any chat model you have pulled)
- **Embedding Model**: `nomic-embed-text` (you can change this to any embedding model)
- **Endpoint**: `http://localhost:11434` (default Ollama endpoint)

## Usage

### Basic Configuration
```csharp
services.AddCustomSemanticKernel(provider =>
{
    var ollamaOptions = new OllamaOptions
    {
        Endpoint = "http://localhost:11434",
        ChatModel = "llama3.2",
        EmbeddingModel = "nomic-embed-text"
    };

    var kernel = Kernel.CreateBuilder()
        .AddOllama(ollamaOptions)
        .Build();

    return kernel;
});
```

### Individual Service Configuration
```csharp
services.AddCustomSemanticKernel(provider =>
{
    var kernel = Kernel.CreateBuilder()
        .AddOllamaChatCompletion("llama3.2", "http://localhost:11434")
        .AddOllamaTextEmbeddingGeneration("nomic-embed-text", "http://localhost:11434")
        .Build();
    return kernel;
});
```

## Available Models

### Popular Chat Models
- `llama3.2` (3B, 1B) - Latest Llama model
- `llama3.1` (8B, 70B, 405B) - Previous Llama version
- `codellama` - Code-specialized model
- `mistral` - Efficient 7B model
- `phi3` - Microsoft's small language model

### Popular Embedding Models
- `nomic-embed-text` - High-quality text embeddings
- `all-minilm` - Fast and efficient embeddings
- `mxbai-embed-large` - High-dimensional embeddings

## Running the Sample

```bash
dotnet run --project samples/SampleAgent/
```

The sample will:
1. Configure Semantic Kernel with Ollama services
2. Create sample agents that use the local LLM
3. Build a dynamic workflow
4. Execute agents and demonstrate functionality

## Troubleshooting

- **Connection Issues**: Ensure Ollama is running on `http://localhost:11434`
- **Model Not Found**: Make sure you've pulled the required models with `ollama pull <model-name>`
- **Slow Responses**: Local models may be slower than cloud APIs, especially on CPU-only systems
- **Memory Issues**: Large models require significant RAM; consider smaller models if needed

## Customization

You can customize the configuration by:
- Changing model names in `OllamaOptions`
- Modifying the endpoint if running Ollama on a different host/port
- Adjusting timeout settings for longer-running models
- Adding custom prompt templates or functions to the kernel
