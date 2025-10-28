using Magentic.Core.Models;

namespace Magentic.Core.Abstractions;

/// <summary>
/// Interface for chat completion clients that interact with language models
/// </summary>
public interface IChatCompletionClient
{
    /// <summary>
    /// Get a chat completion response
    /// </summary>
    /// <param name="context">The conversation context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Chat completion response</returns>
    Task<ChatResponse> GetChatCompletionAsync(ConversationContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a streaming chat completion response
    /// </summary>
    /// <param name="context">The conversation context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of streaming responses</returns>
    IAsyncEnumerable<ChatStreamResponse> GetChatCompletionStreamAsync(ConversationContext context, CancellationToken cancellationToken = default);
}