using LocalChatApi.Models;
using LocalChatApi.Core;
using LocalChatApi.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LocalChatApi.Agents;

/// <summary>
/// Intent detection agent that analyzes user input to determine the appropriate workflow
/// </summary>
public class IntentDetectionAgent : BaseAgent<IntentResult>
{
    public IntentDetectionAgent(ILogger<IntentDetectionAgent> logger, Kernel kernel)
        : base(logger, kernel)
    {
    }

    public override AgentMetadata Metadata => new AgentMetadata
    {
        Id = "intent-detection-agent",
        Name = "Intent Detection Agent",
        Description = "Analyzes user input to determine intent and route to appropriate workflow",
        Version = "1.0.0",
        InputType = typeof(UserRequest),
        OutputType = typeof(IntentResult)
    };

    protected override async Task<IntentResult> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
    {
        if (input is not UserRequest userRequest)
        {
            throw new ArgumentException("Input must be a UserRequest", nameof(input));
        }

        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        var prompt = $@"
Analyze this user input and determine the intent. Respond with just one word:
- 'chat' for normal conversation
- 'file_upload' if asking about uploading files
- 'file_chat' if asking questions about a specific file

User input: ""{userRequest.Input}""

Intent:";

        var response = await chatCompletionService.GetChatMessageContentAsync(prompt, cancellationToken: cancellationToken);

        var intent = response.Content?.Trim().ToLowerInvariant() ?? "chat";

        // Simple intent detection logic
        if (userRequest.Input.ToLowerInvariant().Contains("upload"))
        {
            intent = "file_upload";
        }
        else if (userRequest.Input.ToLowerInvariant().Contains("file") && 
                 (userRequest.Input.ToLowerInvariant().Contains("what") || 
                  userRequest.Input.ToLowerInvariant().Contains("about")))
        {
            intent = "file_chat";
        }
        else
        {
            intent = "chat";
        }

        return new IntentResult
        {
            Intent = intent,
            Confidence = 0.8,
            Entities = new Dictionary<string, object>(),
            OriginalInput = userRequest.Input
        };
    }
}

/// <summary>
/// Chat agent that handles normal conversation using Semantic Kernel with Ollama
/// </summary>
public class ChatAgent : BaseAgent<string>
{
    private readonly IChatHistoryService _chatHistoryService;

    public ChatAgent(ILogger<ChatAgent> logger, Kernel kernel, IChatHistoryService chatHistoryService)
        : base(logger, kernel)
    {
        _chatHistoryService = chatHistoryService;
    }

    public override AgentMetadata Metadata => new AgentMetadata
    {
        Id = "chat-agent",
        Name = "Chat Agent",
        Description = "Handles normal conversation using Semantic Kernel with Ollama",
        Version = "1.0.0",
        InputType = typeof(UserRequest),
        OutputType = typeof(string)
    };

    protected override async Task<string> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
    {
        if (input is not UserRequest userRequest)
        {
            throw new ArgumentException("Input must be a UserRequest", nameof(input));
        }

        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        // Get recent chat history for context
        var recentMessages = await _chatHistoryService.GetSessionMessagesAsync(userRequest.SessionId, 10);
        var conversationContext = BuildConversationContext(recentMessages.ToList(), userRequest.Input);

        var response = await chatCompletionService.GetChatMessageContentAsync(
            conversationContext, 
            cancellationToken: cancellationToken);

        var responseText = response.Content ?? "I'm sorry, I couldn't generate a response.";

        // Save chat history will be handled by the orchestration service
        return responseText;
    }

    private string BuildConversationContext(List<ChatMessage> recentMessages, string currentInput)
    {
        var context = "You are a helpful AI assistant. Have a natural conversation with the user.\n\n";
        
        if (recentMessages.Any())
        {
            context += "Recent conversation:\n";
            foreach (var message in recentMessages.TakeLast(5))
            {
                context += $"{message.Role}: {message.Content}\n";
            }
            context += "\n";
        }

        context += $"User: {currentInput}\nAssistant:";
        return context;
    }
}
