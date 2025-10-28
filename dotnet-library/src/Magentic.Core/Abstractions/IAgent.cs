using Magentic.Core.Models;

namespace Magentic.Core.Abstractions;

/// <summary>
/// Core interface for all AI agents
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Execute a task with the given input
    /// </summary>
    /// <param name="input">The task input</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent response</returns>
    Task<AgentResponse> ExecuteAsync(string input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for agents that can save and restore their state
/// </summary>
public interface IStatefulAgent : IAgent
{
    /// <summary>
    /// Save the current agent state
    /// </summary>
    /// <returns>Serializable state object</returns>
    Task<object> SaveStateAsync();

    /// <summary>
    /// Load a previously saved state
    /// </summary>
    /// <param name="state">The state to restore</param>
    Task LoadStateAsync(object state);

    /// <summary>
    /// Clear the current state
    /// </summary>
    Task ClearStateAsync();
}

/// <summary>
/// Interface for agents that support streaming responses
/// </summary>
public interface IStreamingAgent : IAgent
{
    /// <summary>
    /// Process a conversation with streaming response
    /// </summary>
    /// <param name="context">The conversation context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of response chunks</returns>
    IAsyncEnumerable<AgentResponse> ProcessStreamAsync(ConversationContext context, CancellationToken cancellationToken = default);
}