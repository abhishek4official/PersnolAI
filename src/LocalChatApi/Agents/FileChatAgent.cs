using ElsaWorkflowAgent.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using LocalChatApi.Models;
using LocalChatApi.Services;

#pragma warning disable SKEXP0001 // Suppress experimental API warnings

namespace LocalChatApi.Agents;

/// <summary>
/// File chat agent that handles RAG-based queries about uploaded files
/// </summary>
public class FileChatAgent : BaseAgent<string>
{
    private readonly IFileStorageService _fileStorageService;
    private readonly IChatHistoryService _chatHistoryService;
    private readonly ITextEmbeddingGenerationService _embeddingService;

    public FileChatAgent(
        ILogger<FileChatAgent> logger, 
        Kernel kernel, 
        IFileStorageService fileStorageService,
        IChatHistoryService chatHistoryService,
        ITextEmbeddingGenerationService embeddingService)
        : base(logger, kernel)
    {
        _fileStorageService = fileStorageService;
        _chatHistoryService = chatHistoryService;
        _embeddingService = embeddingService;
    }

    public override string Id => "file-chat-agent";
    public override string Name => "File Chat Agent";
    public override string Description => "Answers questions about uploaded files using RAG";

    public override AgentMetadata Metadata => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        InputType = typeof(FileChatRequest),
        OutputType = typeof(string)
    };

    protected override async Task<string> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
    {
        var chatRequest = input as FileChatRequest ?? throw new ArgumentException("Input must be FileChatRequest");
        
        Logger.LogInformation("Processing file chat request for file: {FileId}", chatRequest.FileId);

        try
        {
            // Check if file exists
            var fileDoc = await _fileStorageService.GetFileAsync(chatRequest.FileId);
            if (fileDoc == null)
            {
                return $"File with ID '{chatRequest.FileId}' not found. Please provide a valid File ID.";
            }

            if (!fileDoc.Chunks.Any())
            {
                return $"File '{fileDoc.FileName}' has not been processed for chat yet. Please try again later.";
            }

            // Generate embedding for the question
            var questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(chatRequest.Question, cancellationToken: cancellationToken);
            
            // Find relevant chunks using similarity search
            var relevantChunks = await _fileStorageService.SearchSimilarChunksAsync(
                chatRequest.FileId, 
                questionEmbedding.ToArray(), 
                topK: 3);

            if (!relevantChunks.Any())
            {
                return $"I couldn't find relevant information in the file '{fileDoc.FileName}' to answer your question.";
            }

            // Build context from relevant chunks
            var fileContext = string.Join("\n\n", relevantChunks.Select(chunk => $"[Chunk {chunk.ChunkIndex + 1}]: {chunk.Content}"));

            // Get recent chat history for this session
            var recentMessages = await _chatHistoryService.GetRecentMessagesAsync(chatRequest.SessionId, 5);
            var conversationHistory = BuildConversationHistory(recentMessages);

            // Create RAG prompt
            var prompt = $"""
                You are an AI assistant that answers questions about uploaded documents. Use the provided context from the document to answer the user's question accurately and helpfully.

                Document Information:
                - File Name: {fileDoc.FileName}
                - File ID: {fileDoc.FileId}

                {conversationHistory}

                Relevant Context from Document:
                {fileContext}

                User Question: {chatRequest.Question}

                Instructions:
                1. Answer the question based on the provided context from the document
                2. Be specific and cite relevant information from the document
                3. If the context doesn't contain enough information to fully answer the question, say so
                4. Be conversational and helpful
                5. Reference the file name when appropriate

                Answer:
                """;

            var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
            var response = result.GetValue<string>() ?? "I apologize, but I couldn't generate a response.";

            // Save chat history
            await SaveFileChatHistory(chatRequest, response, fileDoc.FileName);

            Logger.LogInformation("File chat response generated for file: {FileId}", chatRequest.FileId);
            return response;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing file chat request for file: {FileId}", chatRequest.FileId);
            return "I apologize, but I encountered an error while processing your question about the file. Please try again.";
        }
    }

    private string BuildConversationHistory(List<ChatMessage> recentMessages)
    {
        if (!recentMessages.Any())
        {
            return "";
        }

        var historyLines = new List<string> { "Recent conversation history:" };
        
        var messagesInOrder = recentMessages.OrderBy(m => m.Timestamp).ToList();
        
        foreach (var message in messagesInOrder)
        {
            var role = message.Role == "user" ? "User" : "Assistant";
            historyLines.Add($"{role}: {message.Content}");
        }

        return string.Join("\n", historyLines) + "\n";
    }

    private async Task SaveFileChatHistory(FileChatRequest chatRequest, string response, string fileName)
    {
        try
        {
            // Save user message
            var userMessage = new ChatMessage
            {
                SessionId = chatRequest.SessionId,
                Role = "user",
                Content = chatRequest.Question,
                Intent = "file_chat",
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object> 
                { 
                    ["file_id"] = chatRequest.FileId,
                    ["file_name"] = fileName
                }
            };
            await _chatHistoryService.SaveMessageAsync(userMessage);

            // Save assistant response
            var assistantMessage = new ChatMessage
            {
                SessionId = chatRequest.SessionId,
                Role = "assistant",
                Content = response,
                Intent = "file_chat",
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object> 
                { 
                    ["agent"] = Id,
                    ["file_id"] = chatRequest.FileId,
                    ["file_name"] = fileName
                }
            };
            await _chatHistoryService.SaveMessageAsync(assistantMessage);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving file chat history for session: {SessionId}", chatRequest.SessionId);
        }
    }
}
