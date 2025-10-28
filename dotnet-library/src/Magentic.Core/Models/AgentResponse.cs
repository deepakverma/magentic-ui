using System.Text.Json.Serialization;

namespace Magentic.Core.Models;

/// <summary>
/// Response from an agent execution
/// </summary>
public class AgentResponse
{
    /// <summary>
    /// Whether the execution was successful
    /// </summary>
    [JsonPropertyName("is_success")]
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Response content from the agent
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// Error message if execution failed
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// When the response was generated
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata from execution
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Execution duration
    /// </summary>
    [JsonPropertyName("duration")]
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Agent that generated this response
    /// </summary>
    [JsonPropertyName("agent_name")]
    public string? AgentName { get; set; }
}