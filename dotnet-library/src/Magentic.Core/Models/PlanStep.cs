using System.Text.Json;
using System.Text.Json.Serialization;

namespace Magentic.Core.Models;

/// <summary>
/// Represents a single step in a plan
/// </summary>
public class PlanStep
{
    /// <summary>
    /// Title or name of the step
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of what to do
    /// </summary>
    [JsonPropertyName("details")]
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// Name of the agent responsible for this step
    /// </summary>
    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this step has been completed
    /// </summary>
    [JsonPropertyName("is_completed")]
    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// Result of executing this step
    /// </summary>
    [JsonPropertyName("result")]
    public string? Result { get; set; }

    /// <summary>
    /// Error message if step failed
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Step execution metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// When this step was started
    /// </summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When this step was completed
    /// </summary>
    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Represents a sentinel step that monitors conditions over time
/// </summary>
public class SentinelPlanStep : PlanStep
{
    /// <summary>
    /// Step type identifier
    /// </summary>
    [JsonPropertyName("step_type")]
    public string StepType { get; set; } = "SentinelPlanStep";

    /// <summary>
    /// Number of seconds to sleep between checks
    /// </summary>
    [JsonPropertyName("sleep_duration")]
    public int SleepDuration { get; set; }

    /// <summary>
    /// Condition to check (integer for iteration count, string for LLM evaluation)
    /// </summary>
    [JsonPropertyName("condition")]
    public object Condition { get; set; } = "";

    /// <summary>
    /// Current iteration number
    /// </summary>
    [JsonIgnore]
    public int CurrentIteration { get; set; } = 0;

    /// <summary>
    /// Number of errors encountered
    /// </summary>
    [JsonIgnore]
    public int ErrorCount { get; set; } = 0;

    /// <summary>
    /// Last time a check was performed
    /// </summary>
    [JsonIgnore]
    public DateTime? LastCheckTime { get; set; }

    /// <summary>
    /// Unique identifier for this sentinel execution
    /// </summary>
    [JsonIgnore]
    public string SentinelId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Check if condition is an integer (iteration-based)
    /// </summary>
    [JsonIgnore]
    public bool IsIterationBased => Condition is int or long;

    /// <summary>
    /// Get condition as integer (for iteration-based conditions)
    /// </summary>
    [JsonIgnore]
    public int ConditionAsInt => Condition is int intValue ? intValue : 
                                Condition is long longValue ? (int)longValue : 0;

    /// <summary>
    /// Get condition as string (for LLM evaluation)
    /// </summary>
    [JsonIgnore]
    public string ConditionAsString => Condition?.ToString() ?? "";
}

/// <summary>
/// Custom JSON converter for PlanStep that handles SentinelPlanStep
/// </summary>
public class PlanStepJsonConverter : JsonConverter<PlanStep>
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(PlanStep).IsAssignableFrom(typeToConvert);
    }

    public override PlanStep Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Check if it's a SentinelPlanStep
        if (root.TryGetProperty("step_type", out var stepType) && 
            stepType.GetString() == "SentinelPlanStep")
        {
            return JsonSerializer.Deserialize<SentinelPlanStep>(root.GetRawText(), options)!;
        }

        return JsonSerializer.Deserialize<PlanStep>(root.GetRawText(), options)!;
    }

    public override void Write(Utf8JsonWriter writer, PlanStep value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}