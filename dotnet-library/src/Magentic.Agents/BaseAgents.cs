using Magentic.Core.Abstractions;
using Magentic.Core.Models;
using Microsoft.Extensions.Logging;

namespace Magentic.Agents;

/// <summary>
/// Base agent implementation with common functionality
/// </summary>
public abstract class BaseAgent : IAgent
{
    protected readonly IChatCompletionClient ChatClient;
    protected readonly ILogger Logger;

    protected BaseAgent(IChatCompletionClient chatClient, ILogger logger)
    {
        ChatClient = chatClient;
        Logger = logger;
    }

    /// <summary>
    /// Execute a task
    /// </summary>
    public abstract Task<AgentResponse> ExecuteAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get agent capabilities
    /// </summary>
    public virtual AgentCapabilities GetCapabilities()
    {
        return new AgentCapabilities
        {
            Name = GetType().Name,
            Description = "Base agent implementation",
            SupportedActions = new[] { "general" }
        };
    }

    /// <summary>
    /// Create a conversation context with system message
    /// </summary>
    protected ConversationContext CreateContext(string systemPrompt, string userInput)
    {
        var context = new ConversationContext();
        
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            context.AddMessage(new SystemMessage { Content = systemPrompt });
        }
        
        context.AddMessage(new UserMessage { Content = userInput });
        
        return context;
    }

    /// <summary>
    /// Create successful response
    /// </summary>
    protected AgentResponse CreateSuccessResponse(string content, Dictionary<string, object>? metadata = null)
    {
        return new AgentResponse
        {
            IsSuccess = true,
            Content = content,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Create error response
    /// </summary>
    protected AgentResponse CreateErrorResponse(string error, Exception? exception = null)
    {
        var response = new AgentResponse
        {
            IsSuccess = false,
            Error = error
        };

        if (exception != null)
        {
            response.Metadata["exception"] = exception.ToString();
        }

        return response;
    }
}

/// <summary>
/// Simple chat agent that processes user input using LLM
/// </summary>
public class ChatAgent : BaseAgent
{
    private readonly string _systemPrompt;

    public ChatAgent(
        IChatCompletionClient chatClient,
        ILogger<ChatAgent> logger,
        string? systemPrompt = null)
        : base(chatClient, logger)
    {
        _systemPrompt = systemPrompt ?? "You are a helpful AI assistant. Respond to user requests clearly and concisely.";
    }

    public override async Task<AgentResponse> ExecuteAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(input))
        {
            return CreateErrorResponse("Input cannot be null or empty");
        }

        try
        {
            Logger.LogDebug("ChatAgent processing input: {Input}", input);

            var context = CreateContext(_systemPrompt, input);
            var response = await ChatClient.GetChatCompletionAsync(context, cancellationToken);

            if (response.IsSuccess)
            {
                Logger.LogDebug("ChatAgent completed successfully");
                return CreateSuccessResponse(response.Content ?? "No response content");
            }
            else
            {
                Logger.LogWarning("ChatAgent LLM call failed: {Error}", response.Error);
                return CreateErrorResponse($"LLM call failed: {response.Error}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in ChatAgent execution");
            return CreateErrorResponse("An error occurred during execution", ex);
        }
    }

    public override AgentCapabilities GetCapabilities()
    {
        return new AgentCapabilities
        {
            Name = "ChatAgent",
            Description = "General-purpose chat agent that processes user input using LLM",
            SupportedActions = new[] { "chat", "general", "question-answering" }
        };
    }
}

/// <summary>
/// Task agent that breaks down and executes specific tasks
/// </summary>
public class TaskAgent : BaseAgent
{
    private const string DefaultSystemPrompt = @"
You are a task execution agent. Your role is to:
1. Understand the task provided by the user
2. Break it down into actionable steps if needed
3. Execute or provide guidance on how to complete the task
4. Provide clear, detailed responses about task completion

Be specific, actionable, and helpful in your responses.";

    public TaskAgent(
        IChatCompletionClient chatClient,
        ILogger<TaskAgent> logger)
        : base(chatClient, logger)
    {
    }

    public override async Task<AgentResponse> ExecuteAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(input))
        {
            return CreateErrorResponse("Task input cannot be null or empty");
        }

        try
        {
            Logger.LogInformation("TaskAgent executing task: {Task}", input);

            var context = CreateContext(DefaultSystemPrompt, input);
            var response = await ChatClient.GetChatCompletionAsync(context, cancellationToken);

            if (response.IsSuccess)
            {
                var metadata = new Dictionary<string, object>
                {
                    ["task"] = input,
                    ["completed_at"] = DateTime.UtcNow,
                    ["agent_type"] = "TaskAgent"
                };

                Logger.LogInformation("TaskAgent completed task successfully");
                return CreateSuccessResponse(response.Content ?? "Task completed", metadata);
            }
            else
            {
                Logger.LogWarning("TaskAgent LLM call failed: {Error}", response.Error);
                return CreateErrorResponse($"Failed to process task: {response.Error}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in TaskAgent execution");
            return CreateErrorResponse("An error occurred during task execution", ex);
        }
    }

    public override AgentCapabilities GetCapabilities()
    {
        return new AgentCapabilities
        {
            Name = "TaskAgent",
            Description = "Specialized agent for executing specific tasks and providing step-by-step guidance",
            SupportedActions = new[] { "task-execution", "planning", "step-by-step-guidance" }
        };
    }
}

/// <summary>
/// Research agent that helps with information gathering and analysis
/// </summary>
public class ResearchAgent : BaseAgent
{
    private const string DefaultSystemPrompt = @"
You are a research agent. Your role is to:
1. Help users gather and analyze information
2. Provide comprehensive research on topics
3. Synthesize information from multiple perspectives
4. Offer insights and recommendations based on research

Be thorough, analytical, and provide well-structured responses with clear reasoning.";

    public ResearchAgent(
        IChatCompletionClient chatClient,
        ILogger<ResearchAgent> logger)
        : base(chatClient, logger)
    {
    }

    public override async Task<AgentResponse> ExecuteAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(input))
        {
            return CreateErrorResponse("Research query cannot be null or empty");
        }

        try
        {
            Logger.LogInformation("ResearchAgent processing query: {Query}", input);

            var context = CreateContext(DefaultSystemPrompt, input);
            var response = await ChatClient.GetChatCompletionAsync(context, cancellationToken);

            if (response.IsSuccess)
            {
                var metadata = new Dictionary<string, object>
                {
                    ["research_query"] = input,
                    ["research_completed_at"] = DateTime.UtcNow,
                    ["agent_type"] = "ResearchAgent",
                    ["research_depth"] = "comprehensive"
                };

                Logger.LogInformation("ResearchAgent completed research successfully");
                return CreateSuccessResponse(response.Content ?? "Research completed", metadata);
            }
            else
            {
                Logger.LogWarning("ResearchAgent LLM call failed: {Error}", response.Error);
                return CreateErrorResponse($"Failed to complete research: {response.Error}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in ResearchAgent execution");
            return CreateErrorResponse("An error occurred during research", ex);
        }
    }

    public override AgentCapabilities GetCapabilities()
    {
        return new AgentCapabilities
        {
            Name = "ResearchAgent",
            Description = "Specialized agent for information gathering, analysis, and research tasks",
            SupportedActions = new[] { "research", "analysis", "information-gathering", "synthesis" }
        };
    }
}

/// <summary>
/// Agent capabilities information
/// </summary>
public class AgentCapabilities
{
    /// <summary>
    /// Name of the agent
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what the agent does
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// List of actions this agent can perform
    /// </summary>
    public string[] SupportedActions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Version of the agent
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Additional metadata about capabilities
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}