# SemanticKernel.Ollama

A .NET library that provides Ollama integration for Microsoft Semantic Kernel, enabling you to use local LLM models through Ollama with the Semantic Kernel framework.

## Features

- **Chat Completion**: Full support for chat completions using Ollama models
- **Text Embeddings**: Generate text embeddings using Ollama embedding models
- **Streaming Support**: Real-time streaming responses for chat completions
- **Easy Integration**: Simple extension methods for Semantic Kernel registration
- **Configurable**: Flexible configuration options for endpoints, models, and timeouts

## Installation

```bash
dotnet add package SemanticKernel.Ollama
```

## Prerequisites

1. Install Ollama: https://ollama.ai/
2. Pull a model: `ollama pull llama3.2`
3. Ensure Ollama is running: `ollama serve`

## Quick Start

### Basic Usage

```csharp
using Microsoft.SemanticKernel;
using SemanticKernel.Ollama.Extensions;

// Create a kernel with Ollama services
var kernel = Kernel.CreateBuilder()
    .AddOllamaServices() // Uses default settings
    .Build();

// Use chat completion
var response = await kernel.InvokePromptAsync("What is the capital of France?");
Console.WriteLine(response);
```

### Custom Configuration

```csharp
using Microsoft.SemanticKernel;
using SemanticKernel.Ollama.Configuration;
using SemanticKernel.Ollama.Extensions;

// Configure Ollama options
var options = new OllamaOptions
{
    Endpoint = "http://localhost:11434",
    ChatModel = "llama3.1",
    EmbeddingModel = "llama3.1",
    TimeoutSeconds = 60
};

var kernel = Kernel.CreateBuilder()
    .AddOllamaServices(options)
    .Build();
```

### Individual Service Registration

```csharp
using Microsoft.SemanticKernel;
using SemanticKernel.Ollama.Extensions;

var kernel = Kernel.CreateBuilder()
    .AddOllamaChatCompletion(
        modelName: "llama3.1", 
        endpoint: "http://localhost:11434")
    .AddOllamaTextEmbeddingGeneration(
        modelName: "llama3.1", 
        endpoint: "http://localhost:11434")
    .Build();
```

### Streaming Chat

```csharp
using Microsoft.SemanticKernel.ChatCompletion;

var chatService = kernel.GetRequiredService<IChatCompletionService>();
var chatHistory = new ChatHistory();
chatHistory.AddUserMessage("Tell me a story");

await foreach (var update in chatService.GetStreamingChatMessageContentsAsync(chatHistory))
{
    Console.Write(update.Content);
}
```

## Configuration Options

| Property | Default | Description |
|----------|---------|-------------|
| `Endpoint` | `http://localhost:11434` | Ollama server endpoint |
| `ChatModel` | `llama3.2` | Model name for chat completion |
| `EmbeddingModel` | `llama3.2` | Model name for text embeddings |
| `TimeoutSeconds` | `30` | HTTP request timeout in seconds |

## Error Handling

The library provides proper error handling and will throw `KernelException` for:
- Network connectivity issues
- Invalid responses from Ollama
- Model not found errors
- JSON parsing errors

## Supported Models

This library works with any Ollama-compatible model. Popular choices include:
- `llama3.2` (recommended)
- `llama3.1`
- `codellama`
- `mistral`
- `phi3`

## Requirements

- .NET 8.0 or later
- Microsoft.SemanticKernel 1.20.0 or later
- Running Ollama instance

## License

MIT License - see LICENSE file for details.
