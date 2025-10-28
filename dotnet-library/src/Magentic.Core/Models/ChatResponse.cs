using System.Text.Json.Serialization;

namespace Magentic.Core.Models;

/// <summary>
/// Response from chat completion
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// Whether the response was successful
    /// </summary>
    [JsonPropertyName("is_success")]
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Response content
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// Error message if not successful
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Usage statistics
    /// </summary>
    [JsonPropertyName("usage")]
    public ChatUsage? Usage { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Streaming response from chat completion
/// </summary>
public class ChatStreamResponse
{
    /// <summary>
    /// Whether this is the final response
    /// </summary>
    [JsonPropertyName("is_final")]
    public bool IsFinal { get; set; }

    /// <summary>
    /// Content delta (incremental content)
    /// </summary>
    [JsonPropertyName("delta")]
    public string? Delta { get; set; }

    /// <summary>
    /// Complete content so far
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// Error message if something went wrong
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Usage statistics for chat completion
/// </summary>
public class ChatUsage
{
    /// <summary>
    /// Number of tokens in the prompt
    /// </summary>
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    /// <summary>
    /// Number of tokens in the completion
    /// </summary>
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Total number of tokens
    /// </summary>
    [JsonPropertyName("total_tokens")]
    public int TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>
    /// Estimated cost (if available)
    /// </summary>
    [JsonPropertyName("estimated_cost")]
    public decimal? EstimatedCost { get; set; }
}