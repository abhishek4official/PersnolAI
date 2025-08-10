using ElsaWorkflowAgent.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using LocalChatApi.Models;
using System.Text.Json;

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

    public override string Id => "intent-detection-agent";
    public override string Name => "Intent Detection Agent";
    public override string Description => "Analyzes user input to determine intent and route to appropriate workflow";

    public override AgentMetadata Metadata => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        InputType = typeof(UserRequest),
        OutputType = typeof(IntentResult)
    };

    protected override async Task<IntentResult> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
    {
        var userRequest = input as UserRequest ?? throw new ArgumentException("Input must be UserRequest");
        
        Logger.LogInformation("Analyzing intent for input: {Input}", userRequest.Input);

        // Create prompt for intent detection
        var prompt = $$"""
            Analyze the following user input and determine the intent. Return only the intent classification:

            User Input: "{{userRequest.Input}}"

            Intent Categories:
            1. "chat" - Normal conversation, questions, general queries
            2. "file_upload" - User wants to upload a file, mentions uploading, attaching, or sharing files
            3. "file_chat" - User is asking questions about a previously uploaded file, mentions file ID, or asks about file content

            Additional Context:
            - Look for keywords like "upload", "attach", "file", "document"
            - Look for file ID references (like "XXXXX" or alphanumeric IDs)
            - Look for questions about file content or analysis

            Return a JSON response with this exact format:
            {
                "intent": "chat|file_upload|file_chat",
                "confidence": 0.0-1.0,
                "entities": {
                    "file_id": "extracted_file_id_if_any",
                    "keywords": ["relevant", "keywords"]
                }
            }
            """;

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
            var responseText = result.GetValue<string>() ?? "";

            Logger.LogInformation("LLM Response: {Response}", responseText);

            // Parse the JSON response
            var intentData = JsonSerializer.Deserialize<JsonElement>(responseText);
            
            var intent = intentData.GetProperty("intent").GetString() ?? "chat";
            var confidence = intentData.GetProperty("confidence").GetDouble();
            
            var entities = new Dictionary<string, object>();
            if (intentData.TryGetProperty("entities", out var entitiesElement))
            {
                if (entitiesElement.TryGetProperty("file_id", out var fileIdElement))
                {
                    var fileId = fileIdElement.GetString();
                    if (!string.IsNullOrEmpty(fileId))
                        entities["file_id"] = fileId;
                }
                
                if (entitiesElement.TryGetProperty("keywords", out var keywordsElement))
                {
                    var keywords = keywordsElement.EnumerateArray()
                        .Select(k => k.GetString() ?? "")
                        .Where(k => !string.IsNullOrEmpty(k))
                        .ToList();
                    entities["keywords"] = keywords;
                }
            }

            var intentResult = new IntentResult
            {
                Intent = intent,
                Confidence = confidence,
                Entities = entities,
                OriginalInput = userRequest.Input
            };

            Logger.LogInformation("Intent detected: {Intent} with confidence {Confidence}", intent, confidence);
            return intentResult;
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse LLM response as JSON, falling back to simple classification");
            
            // Fallback: Simple keyword-based classification
            var inputLower = userRequest.Input.ToLowerInvariant();
            
            if (inputLower.Contains("upload") || inputLower.Contains("attach") || inputLower.Contains("file"))
            {
                return new IntentResult
                {
                    Intent = "file_upload",
                    Confidence = 0.7,
                    Entities = new Dictionary<string, object> { ["keywords"] = new[] { "file", "upload" } },
                    OriginalInput = userRequest.Input
                };
            }
            
            if (inputLower.Contains("file id") || System.Text.RegularExpressions.Regex.IsMatch(inputLower, @"\b[a-f0-9]{8,}\b"))
            {
                return new IntentResult
                {
                    Intent = "file_chat",
                    Confidence = 0.6,
                    Entities = new Dictionary<string, object>(),
                    OriginalInput = userRequest.Input
                };
            }

            return new IntentResult
            {
                Intent = "chat",
                Confidence = 0.8,
                Entities = new Dictionary<string, object>(),
                OriginalInput = userRequest.Input
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during intent detection");
            
            // Default to chat intent on error
            return new IntentResult
            {
                Intent = "chat",
                Confidence = 0.5,
                Entities = new Dictionary<string, object>(),
                OriginalInput = userRequest.Input
            };
        }
    }
}
