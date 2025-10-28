using System.Text.Json.Serialization;

namespace Magentic.Core.Models;

/// <summary>
/// Represents a conversation context with messages
/// </summary>
public class ConversationContext
{
    /// <summary>
    /// List of messages in the conversation
    /// </summary>
    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// Add a message to the conversation
    /// </summary>
    public void AddMessage(ChatMessage message)
    {
        Messages.Add(message);
    }

    /// <summary>
    /// Get the most recent message
    /// </summary>
    [JsonIgnore]
    public ChatMessage? LastMessage => Messages.LastOrDefault();
}

/// <summary>
/// Base class for chat messages
/// </summary>
public abstract class ChatMessage
{
    /// <summary>
    /// Message content
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Message role (system, user, assistant)
    /// </summary>
    [JsonPropertyName("role")]
    public abstract string Role { get; }

    /// <summary>
    /// Timestamp when message was created
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata for the message
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// System message for providing context/instructions
/// </summary>
public class SystemMessage : ChatMessage
{
    /// <summary>
    /// Message role
    /// </summary>
    public override string Role => "system";

    /// <summary>
    /// Create a new system message
    /// </summary>
    public SystemMessage(string content = "")
    {
        Content = content;
    }
}

/// <summary>
/// User message from human user
/// </summary>
public class UserMessage : ChatMessage
{
    /// <summary>
    /// Message role
    /// </summary>
    public override string Role => "user";

    /// <summary>
    /// Create a new user message
    /// </summary>
    public UserMessage(string content = "")
    {
        Content = content;
    }
}

/// <summary>
/// Assistant message from AI assistant
/// </summary>
public class AssistantMessage : ChatMessage
{
    /// <summary>
    /// Message role
    /// </summary>
    public override string Role => "assistant";

    /// <summary>
    /// Create a new assistant message
    /// </summary>
    public AssistantMessage(string content = "")
    {
        Content = content;
    }
}