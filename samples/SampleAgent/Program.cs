using ElsaWorkflowAgent.Agents;
using ElsaWorkflowAgent.Decisions;
using ElsaWorkflowAgent.Extensions;
using ElsaWorkflowAgent.Kernel;
using ElsaWorkflowAgent.Workflows;
using ElsaWorkflowAgent.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SemanticKernel.Ollama.Configuration;
using SemanticKernel.Ollama.Extensions;

#pragma warning disable SKEXP0001 // Suppress experimental API warnings for Ollama integration

namespace SampleAgent
{

    class Program
    {
        static async Task Main(string[] args)
        {
            // Prerequisites:
            // 1. Install Ollama: https://ollama.ai/
            // 2. Pull required models:
            //    ollama pull llama3.2
            //    ollama pull nomic-embed-text
            // 3. Start Ollama server (usually runs automatically)

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // Configure ElsaWorkflowAgent (without automatic Semantic Kernel setup)
                    services.AddElsaWorkflowAgent();

                    // Configure Semantic Kernel with our new SemanticKernel.Ollama package
                    services.AddCustomSemanticKernel(provider =>
                    {
                        var ollamaOptions = new OllamaOptions
                        {
                            Endpoint = "http://localhost:11434",
                            ChatModel = "llama3.1:latest",           // Make sure this model is pulled in Ollama
                            EmbeddingModel = "mxbai-embed-large:latest" // Make sure this model is pulled in Ollama
                        };

                        var kernel = Kernel.CreateBuilder()
                            .AddOllamaServices(ollamaOptions)
                            .Build();

                        return kernel;
                    });

                    // Register conversation agents and decisions
                    services.AddAgent<GreetingAgent, string>();
                    services.AddAgent<NameAgent, string>();
                    services.AddAgent<LLMAgent, string>();
                    services.AddDecision<ConversationDecision>();
                })
                .Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("🤖 Interactive Conversation Agent Started!");
            logger.LogInformation("💬 Type your messages below. Type 'exit' to quit.");
            Console.WriteLine();
            Console.WriteLine("🤖 Interactive Conversation Agent");
            Console.WriteLine("💬 Type your messages below. Type 'exit' to quit.");
            Console.WriteLine();
            Console.WriteLine("📋 Conversation Rules:");
            Console.WriteLine("   • 'hi' → 'Hello'");
            Console.WriteLine("   • 'hello' → 'Hi'");
            Console.WriteLine("   • 'what is your name?' → 'Bag bosdike'");
            Console.WriteLine("   • Everything else → Uses LLM (Ollama required)");
            Console.WriteLine();
            Console.WriteLine("🔧 Ollama Setup (for LLM responses):");
            Console.WriteLine("   1. Install Ollama: https://ollama.ai/");
            Console.WriteLine("   2. Run: ollama pull llama3.2");
            Console.WriteLine("   3. Ensure Ollama is running (ollama serve)");
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════");

            // Start interactive console loop
            await RunInteractiveConversationAsync(host, logger);

            logger.LogInformation("👋 Conversation Agent Application completed");
        }

        private static async Task RunInteractiveConversationAsync(IHost host, ILogger logger)
        {
            // Get services
            var greetingAgent = host.Services.GetRequiredService<GreetingAgent>();
            var nameAgent = host.Services.GetRequiredService<NameAgent>();
            var llmAgent = host.Services.GetRequiredService<LLMAgent>();
            var conversationDecision = host.Services.GetRequiredService<ConversationDecision>();
            var workflowEngine = host.Services.GetRequiredService<IWorkflowEngine>();

            while (true)
            {
                Console.Write("You: ");
                var userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput) || userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Bot: 👋 Goodbye!");
                    break;
                }

                try
                {
                    // Create dynamic workflow based on user input
                    var workflow = ElsaWorkflowAgent.Workflows.WorkflowBuilderFactory
                        .CreateBuilder("ConversationWorkflow", "Interactive conversation workflow")
                        .WithVariable("userInput", userInput)
                        .WithVariable("timestamp", DateTime.Now.ToString())
                        .AddDecision(conversationDecision, new { input = userInput })
                        
                        .AddAgent(greetingAgent, new { input = userInput })
                        .AddAgent(nameAgent, new { input = userInput })
                        .AddAgent(llmAgent, new { input = userInput })
                        .Build();

                    // Execute the workflow
                    var workflowResult = await workflowEngine.ExecuteAsync(workflow, new { input = userInput });

                    if (workflowResult.Success)
                    {
                        Console.WriteLine($"Bot: {workflowResult.FinalResult}");
                    }
                    else
                    {
                        Console.WriteLine("Bot: 😅 Sorry, I encountered an error. Please try again.");
                        logger.LogError("Workflow execution failed: {ErrorMessage}", workflowResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Bot: 😅 Sorry, something went wrong. Please try again.");
                    logger.LogError(ex, "Error during conversation workflow execution");
                }

                Console.WriteLine();
            }
        }
    }

    // Greeting Agent - Handles "hi" and "hello"
    public class GreetingAgent : BaseAgent<string>
    {
        public GreetingAgent(ILogger<GreetingAgent> logger, Microsoft.SemanticKernel.Kernel kernel)
            : base(logger, kernel)
        {
        }

        public override string Id => "greeting_agent";
        public override string Name => "Greeting Agent";
        public override string Description => "Handles greeting messages like hi and hello";

        public override AgentMetadata Metadata => new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            InputType = typeof(object),
            OutputType = typeof(string)
        };

        protected override async Task<string> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(50, cancellationToken); // Simulate processing

            // Extract the actual input string from the anonymous object
            var inputStr = "";
            if (input != null)
            {
                var inputType = input.GetType();
                var inputProperty = inputType.GetProperty("input");
                if (inputProperty != null)
                {
                    inputStr = inputProperty.GetValue(input)?.ToString()?.ToLowerInvariant().Trim() ?? "";
                }
                else
                {
                    inputStr = input.ToString()?.ToLowerInvariant().Trim() ?? "";
                }
            }

            return inputStr switch
            {
                "hi" => "Hello",
                "hello" => "Hi",
                _ => "" // Return empty for non-greeting inputs
            };
        }
    }

    // Name Agent - Handles "what is your name?" question
    public class NameAgent : BaseAgent<string>
    {
        public NameAgent(ILogger<NameAgent> logger, Microsoft.SemanticKernel.Kernel kernel)
            : base(logger, kernel)
        {
        }

        public override string Id => "name_agent";
        public override string Name => "Name Agent";
        public override string Description => "Handles name-related questions";

        public override AgentMetadata Metadata => new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            InputType = typeof(object),
            OutputType = typeof(string)
        };

        protected override async Task<string> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(50, cancellationToken); // Simulate processing

            // Extract the actual input string from the anonymous object
            var inputStr = "";
            if (input != null)
            {
                var inputType = input.GetType();
                var inputProperty = inputType.GetProperty("input");
                if (inputProperty != null)
                {
                    inputStr = inputProperty.GetValue(input)?.ToString()?.ToLowerInvariant().Trim() ?? "";
                }
                else
                {
                    inputStr = input.ToString()?.ToLowerInvariant().Trim() ?? "";
                }
            }

            if (inputStr.Contains("what is your name") || inputStr.Contains("what's your name"))
            {
                return "Bag bosdike";
            }

            return ""; // Return empty for non-name questions
        }
    }

    // LLM Agent - Handles all other inputs using Semantic Kernel
    public class LLMAgent : BaseAgent<string>
    {
        public LLMAgent(ILogger<LLMAgent> logger, Microsoft.SemanticKernel.Kernel kernel)
            : base(logger, kernel)
        {
        }

        public override string Id => "llm_agent";
        public override string Name => "LLM Agent";
        public override string Description => "Handles general conversation using LLM";

        public override AgentMetadata Metadata => new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            InputType = typeof(object),
            OutputType = typeof(string)
        };

        protected override async Task<string> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
        {
            // Extract the actual input string from the anonymous object
            var inputStr = "";
            if (input != null)
            {
                var inputType = input.GetType();
                var inputProperty = inputType.GetProperty("input");
                if (inputProperty != null)
                {
                    inputStr = inputProperty.GetValue(input)?.ToString() ?? "";
                }
                else
                {
                    inputStr = input.ToString() ?? "";
                }
            }

            try
            {
                // Use Semantic Kernel to generate response
                var prompt = $"""
                    You are a helpful and friendly conversational AI assistant. 
                    Respond to the user's message in a natural and engaging way.
                    Keep your responses concise but helpful.
                    
                    User message: {inputStr}
                    
                    Response:
                    """;

                var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
                return result.ToString();
            }
            catch (Exception ex) when (ex.Message.Contains("404") || ex.Message.Contains("Ollama"))
            {
                Logger.LogWarning("Ollama service not available, providing fallback response for: {Input}", inputStr);
                
                // Provide intelligent fallback responses when Ollama is not available
                return GenerateFallbackResponse(inputStr);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error calling LLM for input: {Input}", input);
                return "I'm having trouble processing that right now. Could you please try again?";
            }
        }

        private string GenerateFallbackResponse(string input)
        {
            var lowerInput = input.ToLowerInvariant();

            // Provide contextual responses based on keywords
            if (lowerInput.Contains("how are you") || lowerInput.Contains("how do you do"))
                return "I'm doing well, thank you for asking! How can I help you today?";
            
            if (lowerInput.Contains("thank") || lowerInput.Contains("thanks"))
                return "You're welcome! Is there anything else I can help you with?";
            
            if (lowerInput.Contains("help") || lowerInput.Contains("assist"))
                return "I'd be happy to help! What do you need assistance with?";
            
            if (lowerInput.Contains("weather"))
                return "I don't have access to current weather data, but you can check a weather app or website for accurate information.";
            
            if (lowerInput.Contains("time") || lowerInput.Contains("date"))
                return $"The current time is {DateTime.Now:HH:mm} and today's date is {DateTime.Now:yyyy-MM-dd}.";
            
            if (lowerInput.Contains("goodbye") || lowerInput.Contains("bye"))
                return "Goodbye! It was nice talking with you. Take care!";
            
            if (lowerInput.Contains("?"))
                return "That's an interesting question! I'd need my full capabilities to give you a detailed answer. Please make sure Ollama is running for better responses.";
            
            // Generic fallback for statements
            return "I understand you're trying to communicate with me. For more intelligent responses, please ensure Ollama is running with the llama3.2 model.";
        }
    }

    // Conversation Decision - Routes to appropriate agent based on input
    public class ConversationDecision : BaseDecision
    {
        public ConversationDecision(ILogger<ConversationDecision> logger) : base(logger)
        {
        }

        public override string Id => "conversation_decision";
        public override string Name => "Conversation Decision";
        public override string Description => "Routes conversation to appropriate handler";

        public override DecisionMetadata Metadata => new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            InputType = typeof(object),
            PossibleOutcomes = new List<string> { "greeting", "name", "llm" }
        };

        protected override async Task<DecisionResult> ExecuteInternalAsync(object input, DecisionContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(25, cancellationToken);

            // Extract the actual input string from the anonymous object
            var inputStr = "";
            if (input != null)
            {
                var inputType = input.GetType();
                var inputProperty = inputType.GetProperty("input");
                if (inputProperty != null)
                {
                    inputStr = inputProperty.GetValue(input)?.ToString()?.ToLowerInvariant().Trim() ?? "";
                }
                else
                {
                    inputStr = input.ToString()?.ToLowerInvariant().Trim() ?? "";
                }
            }

            // Check for greetings
            if (inputStr == "hi" || inputStr == "hello")
            {
                return DecisionResult.CreateSuccess("greeting", new { route = "greeting", handler = "greeting_agent" });
            }

            // Check for name question
            if (inputStr.Contains("what is your name") || inputStr.Contains("what's your name"))
            {
                return DecisionResult.CreateSuccess("name", new { route = "name", handler = "name_agent" });
            }

            // Default to LLM for everything else
            return DecisionResult.CreateSuccess("llm", new { route = "llm", handler = "llm_agent" });
        }
    }
}