using Magentic.Core.Abstractions;
using Magentic.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Magentic.Samples.Console.LLM;

/// <summary>
/// Mock chat completion client for demonstration purposes
/// </summary>
public class MockChatCompletionClient : IChatCompletionClient
{
    private readonly ILogger<MockChatCompletionClient> _logger;

    public MockChatCompletionClient(ILogger<MockChatCompletionClient> logger)
    {
        _logger = logger;
    }

    public Task<ChatResponse> GetChatCompletionAsync(ConversationContext context, CancellationToken cancellationToken = default)
    {
        // Simulate processing delay
        Task.Delay(100, cancellationToken);

        var lastMessage = context.Messages.LastOrDefault();
        var userInput = lastMessage?.Content ?? "No input";

        // Generate mock responses based on input
        string response = GenerateMockResponse(userInput);

        _logger.LogDebug("Mock LLM generated response for input: {Input}", userInput.Substring(0, Math.Min(50, userInput.Length)));

        return Task.FromResult(new ChatResponse
        {
            IsSuccess = true,
            Content = response,
            Usage = new ChatUsage
            {
                PromptTokens = userInput.Length / 4, // Rough token estimate
                CompletionTokens = response.Length / 4
            }
        });
    }

    public async IAsyncEnumerable<ChatStreamResponse> GetChatCompletionStreamAsync(
        ConversationContext context, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var lastMessage = context.Messages.LastOrDefault();
        var userInput = lastMessage?.Content ?? "No input";
        var response = GenerateMockResponse(userInput);

        // Simulate streaming by yielding chunks
        var words = response.Split(' ');
        var currentContent = string.Empty;

        for (int i = 0; i < words.Length; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            var word = words[i];
            currentContent += (i > 0 ? " " : "") + word;

            await Task.Delay(50, cancellationToken); // Simulate network delay

            yield return new ChatStreamResponse
            {
                IsFinal = i == words.Length - 1,
                Delta = (i > 0 ? " " : "") + word,
                Content = currentContent
            };
        }
    }

    private string GenerateMockResponse(string input)
    {
        var lowerInput = input.ToLowerInvariant();

        if (lowerInput.Contains("help") || lowerInput.Contains("what") || lowerInput.Contains("can"))
        {
            return "I'm a mock AI assistant. I can help with various tasks including research, analysis, and general questions. How can I assist you today?";
        }

        if (lowerInput.Contains("research") || lowerInput.Contains("artificial intelligence") || lowerInput.Contains("ai"))
        {
            return @"Based on the research task, here's a structured approach:

**Research Plan for AI Summary:**

1. **Definition**: Artificial Intelligence (AI) refers to computer systems that can perform tasks typically requiring human intelligence.

2. **Key Areas**: 
   - Machine Learning
   - Natural Language Processing
   - Computer Vision
   - Robotics

3. **Current Applications**:
   - Virtual assistants (Siri, Alexa)
   - Recommendation systems
   - Autonomous vehicles
   - Medical diagnosis

4. **Future Implications**: AI continues to advance rapidly, with potential impacts on employment, healthcare, education, and society.

This provides a comprehensive overview of artificial intelligence covering its definition, key areas, current applications, and future implications.";
        }

        // If the input contains planning-related system prompt, return a plan in JSON format
        if (lowerInput.Contains("creates detailed plan") || lowerInput.Contains("respond only with the json") || 
            (lowerInput.Contains("json format") && lowerInput.Contains("steps")) ||
            lowerInput.Contains("available agents") || 
            (lowerInput.Contains("ai assistant") && lowerInput.Contains("plan")))
        {
            return JsonSerializer.Serialize(new
            {
                title = "AI Research and Summary Plan",
                description = "A comprehensive plan to research and write a summary about artificial intelligence",
                steps = new[]
                {
                    new
                    {
                        title = "Gather AI Definitions",
                        details = "Research and collect various definitions of artificial intelligence from reliable sources",
                        agent_name = "research"
                    },
                    new
                    {
                        title = "Identify Key AI Areas", 
                        details = "List and describe the main areas and types of AI (ML, NLP, Computer Vision, etc.)",
                        agent_name = "research"
                    },
                    new
                    {
                        title = "Document Current Applications",
                        details = "Research current real-world applications and use cases of AI technology",
                        agent_name = "research"
                    },
                    new
                    {
                        title = "Write Summary Report",
                        details = "Compile research into a coherent, well-structured summary document",
                        agent_name = "task"
                    }
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        return $"I understand you're asking about: '{input}'. This is a mock response demonstrating the AI agent system. In a real implementation, this would be handled by an actual language model.";
    }
}