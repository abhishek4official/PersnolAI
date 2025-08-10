using ElsaWorkflowAgent.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using LocalChatApi.Models;
using LocalChatApi.Services;

namespace LocalChatApi.Agents;

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

    public override string Id => "chat-agent";
    public override string Name => "Chat Agent";
    public override string Description => "Handles normal conversation and queries using LLM";

    public override AgentMetadata Metadata => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        InputType = typeof(UserRequest),
        OutputType = typeof(string)
    };

    protected override async Task<string> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
    {
        var userRequest = input as UserRequest ?? throw new ArgumentException("Input must be UserRequest");
        
        Logger.LogInformation("Processing chat request for session: {SessionId}", userRequest.SessionId);

        // Get recent chat history for context
        var recentMessages = await _chatHistoryService.GetRecentMessagesAsync(userRequest.SessionId, 10);
        
        // Build conversation context
        var conversationContext = BuildConversationContext(recentMessages, userRequest.Input);

        // Create prompt with context
        var prompt = $"""
            You are a helpful AI assistant. Respond to the user's message naturally and helpfully.
            
            {conversationContext}
            
            User: {userRequest.Input}
            Assistant:
            """;

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
            var response = result.GetValue<string>() ?? "I apologize, but I couldn't generate a response.";

            // Save user message and assistant response to history
            await SaveChatHistory(userRequest, response);

            Logger.LogInformation("Chat response generated for session: {SessionId}", userRequest.SessionId);
            return response;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating chat response for session: {SessionId}", userRequest.SessionId);
            return "I apologize, but I encountered an error while processing your request. Please try again.";
        }
    }

    private string BuildConversationContext(List<ChatMessage> recentMessages, string currentInput)
    {
        if (!recentMessages.Any())
        {
            return "This is the start of a new conversation.";
        }

        var contextLines = new List<string> { "Previous conversation:" };
        
        // Reverse to get chronological order (oldest first)
        var messagesInOrder = recentMessages.OrderBy(m => m.Timestamp).ToList();
        
        foreach (var message in messagesInOrder)
        {
            var role = message.Role == "user" ? "User" : "Assistant";
            contextLines.Add($"{role}: {message.Content}");
        }

        return string.Join("\n", contextLines);
    }

    private async Task SaveChatHistory(UserRequest userRequest, string response)
    {
        try
        {
            // Save user message
            var userMessage = new ChatMessage
            {
                SessionId = userRequest.SessionId,
                Role = "user",
                Content = userRequest.Input,
                Intent = "chat",
                Timestamp = userRequest.Timestamp,
                Metadata = userRequest.Context
            };
            await _chatHistoryService.SaveMessageAsync(userMessage);

            // Save assistant response
            var assistantMessage = new ChatMessage
            {
                SessionId = userRequest.SessionId,
                Role = "assistant",
                Content = response,
                Intent = "chat",
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object> { ["agent"] = Id }
            };
            await _chatHistoryService.SaveMessageAsync(assistantMessage);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving chat history for session: {SessionId}", userRequest.SessionId);
        }
    }
}
