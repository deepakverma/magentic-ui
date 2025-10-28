using System.Text.Json;
using System.Text.Json.Serialization;

namespace Magentic.Core.Models;

/// <summary>
/// Represents a complete plan with multiple steps
/// </summary>
public class Plan
{
    /// <summary>
    /// Title of the plan
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the plan
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// List of steps to execute
    /// </summary>
    [JsonPropertyName("steps")]
    [JsonConverter(typeof(PlanStepsJsonConverter))]
    public List<PlanStep> Steps { get; set; } = new();

    /// <summary>
    /// When this plan was created
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this plan was last updated
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current status of the plan
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlanStatus Status { get; set; } = PlanStatus.NotStarted;

    /// <summary>
    /// Plan execution metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Current step being executed (0-based index)
    /// </summary>
    [JsonPropertyName("current_step_index")]
    public int CurrentStepIndex { get; set; } = 0;

    /// <summary>
    /// Get the current step being executed
    /// </summary>
    [JsonIgnore]
    public PlanStep? CurrentStep => CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count 
        ? Steps[CurrentStepIndex] 
        : null;

    /// <summary>
    /// Get completed steps
    /// </summary>
    [JsonIgnore]
    public IEnumerable<PlanStep> CompletedSteps => Steps.Where(s => s.IsCompleted);

    /// <summary>
    /// Get pending steps
    /// </summary>
    [JsonIgnore]
    public IEnumerable<PlanStep> PendingSteps => Steps.Where(s => !s.IsCompleted);

    /// <summary>
    /// Check if all steps are completed
    /// </summary>
    [JsonIgnore]
    public bool IsCompleted => Steps.All(s => s.IsCompleted);

    /// <summary>
    /// Check if any step has an error
    /// </summary>
    [JsonIgnore]
    public bool HasErrors => Steps.Any(s => !string.IsNullOrEmpty(s.Error));

    /// <summary>
    /// Get progress percentage (0-100)
    /// </summary>
    [JsonIgnore]
    public double ProgressPercentage => Steps.Count == 0 ? 0 : 
        (double)CompletedSteps.Count() / Steps.Count * 100;

    /// <summary>
    /// Add a new step to the plan
    /// </summary>
    public void AddStep(PlanStep step)
    {
        Steps.Add(step);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Mark the current step as completed and move to next
    /// </summary>
    public bool CompleteCurrentStep(string? result = null)
    {
        if (CurrentStep == null) return false;

        CurrentStep.IsCompleted = true;
        CurrentStep.CompletedAt = DateTime.UtcNow;
        CurrentStep.Result = result;

        // Move to next step
        CurrentStepIndex++;

        // Update plan status
        if (IsCompleted)
        {
            Status = PlanStatus.Completed;
        }
        else if (HasErrors)
        {
            Status = PlanStatus.Failed;
        }

        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Mark the current step as failed
    /// </summary>
    public void FailCurrentStep(string error)
    {
        if (CurrentStep == null) return;

        CurrentStep.Error = error;
        CurrentStep.CompletedAt = DateTime.UtcNow;
        Status = PlanStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Start executing the plan
    /// </summary>
    public void Start()
    {
        Status = PlanStatus.InProgress;
        CurrentStepIndex = 0;
        UpdatedAt = DateTime.UtcNow;

        // Mark first step as started
        if (CurrentStep != null && CurrentStep.StartedAt == null)
        {
            CurrentStep.StartedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Pause plan execution
    /// </summary>
    public void Pause()
    {
        Status = PlanStatus.Paused;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Cancel plan execution
    /// </summary>
    public void Cancel()
    {
        Status = PlanStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Create a copy of this plan
    /// </summary>
    public Plan Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<Plan>(json)!;
    }
}

/// <summary>
/// Plan execution status
/// </summary>
public enum PlanStatus
{
    NotStarted,
    InProgress,
    Paused,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Custom JSON converter for plan steps to handle polymorphic serialization
/// </summary>
public class PlanStepsJsonConverter : JsonConverter<List<PlanStep>>
{
    public override List<PlanStep> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var steps = new List<PlanStep>();
        
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected StartArray token");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            var converter = new PlanStepJsonConverter();
            var step = converter.Read(ref reader, typeof(PlanStep), options);
            steps.Add(step);
        }

        return steps;
    }

    public override void Write(Utf8JsonWriter writer, List<PlanStep> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        
        var converter = new PlanStepJsonConverter();
        foreach (var step in value)
        {
            converter.Write(writer, step, options);
        }
        
        writer.WriteEndArray();
    }
}