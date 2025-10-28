using Magentic.Core.Models;

namespace Magentic.Core.Abstractions;

/// <summary>
/// Interface for executing plan steps
/// </summary>
public interface IPlanExecutor
{
    /// <summary>
    /// Execute a single plan step
    /// </summary>
    Task<StepExecutionResult> ExecuteStepAsync(PlanStep step, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for executing sentinel steps
/// </summary>
public interface ISentinelExecutor
{
    /// <summary>
    /// Execute a sentinel step with monitoring
    /// </summary>
    Task<StepExecutionResult> ExecuteSentinelStepAsync(SentinelPlanStep step, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop a running sentinel by ID
    /// </summary>
    Task StopSentinelAsync(string sentinelId);
}

/// <summary>
/// Result of executing a single step
/// </summary>
public class StepExecutionResult
{
    /// <summary>
    /// Whether execution was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Result content from the agent
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Error message if execution failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Additional metadata from execution
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Execution duration
    /// </summary>
    public TimeSpan? Duration { get; set; }
}