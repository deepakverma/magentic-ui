using Magentic.Core.Abstractions;
using Magentic.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Magentic.Planning;

/// <summary>
/// Configuration for sentinel execution
/// </summary>
public class SentinelExecutorConfig
{
    /// <summary>
    /// Maximum number of iterations for iteration-based conditions
    /// </summary>
    public int MaxIterations { get; set; } = 1000;

    /// <summary>
    /// Maximum execution time for a sentinel step
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Maximum number of errors before giving up
    /// </summary>
    public int MaxErrors { get; set; } = 10;

    /// <summary>
    /// Minimum sleep duration between checks
    /// </summary>
    public TimeSpan MinSleepDuration { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum sleep duration between checks
    /// </summary>
    public TimeSpan MaxSleepDuration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Whether to use adaptive sleep (adjust sleep time based on errors)
    /// </summary>
    public bool UseAdaptiveSleep { get; set; } = true;
}

/// <summary>
/// Executes sentinel steps that monitor conditions over time
/// </summary>
public class SentinelExecutor : ISentinelExecutor
{
    private readonly IChatCompletionClient _chatClient;
    private readonly ILogger<SentinelExecutor> _logger;
    private readonly SentinelExecutorConfig _config;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningSentinels;

    private const string ConditionEvaluationPrompt = @"
You are evaluating whether a condition has been met for a monitoring task.

Condition to check: {condition}
Context: {context}

Evaluate whether the condition is satisfied. Respond with only 'true' if the condition is met, or 'false' if it is not met.

Your response must be exactly 'true' or 'false' with no additional text.";

    public SentinelExecutor(
        IChatCompletionClient chatClient,
        ILogger<SentinelExecutor> logger,
        SentinelExecutorConfig? config = null)
    {
        _chatClient = chatClient;
        _logger = logger;
        _config = config ?? new SentinelExecutorConfig();
        _runningSentinels = new ConcurrentDictionary<string, CancellationTokenSource>();
    }

    /// <summary>
    /// Execute a sentinel step
    /// </summary>
    public async Task<StepExecutionResult> ExecuteSentinelStepAsync(
        SentinelPlanStep sentinelStep,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting sentinel step execution: {StepTitle}", sentinelStep.Title);

        // Create a combined cancellation token
        using var sentinelCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sentinelToken = sentinelCts.Token;

        // Register this sentinel
        _runningSentinels.TryAdd(sentinelStep.SentinelId, sentinelCts);

        try
        {
            // Set execution timeout
            sentinelCts.CancelAfter(_config.MaxExecutionTime);

            var startTime = DateTime.UtcNow;
            sentinelStep.CurrentIteration = 0;
            sentinelStep.ErrorCount = 0;

            while (!sentinelToken.IsCancellationRequested)
            {
                try
                {
                    sentinelStep.CurrentIteration++;
                    sentinelStep.LastCheckTime = DateTime.UtcNow;

                    _logger.LogDebug("Sentinel check iteration {Iteration} for step: {StepTitle}",
                        sentinelStep.CurrentIteration, sentinelStep.Title);

                    // Check if condition is met
                    var conditionMet = await EvaluateConditionAsync(sentinelStep, sentinelToken);

                    if (conditionMet)
                    {
                        var duration = DateTime.UtcNow - startTime;
                        _logger.LogInformation("Sentinel condition met after {Duration} and {Iterations} iterations",
                            duration, sentinelStep.CurrentIteration);

                        return new StepExecutionResult
                        {
                            Success = true,
                            Result = $"Condition met after {sentinelStep.CurrentIteration} iterations in {duration:hh\\:mm\\:ss}"
                        };
                    }

                    // Check iteration limit for iteration-based conditions
                    if (sentinelStep.IsIterationBased && 
                        sentinelStep.CurrentIteration >= sentinelStep.ConditionAsInt)
                    {
                        _logger.LogInformation("Sentinel reached iteration limit: {Iterations}", 
                            sentinelStep.CurrentIteration);

                        return new StepExecutionResult
                        {
                            Success = true,
                            Result = $"Completed {sentinelStep.CurrentIteration} iterations"
                        };
                    }

                    // Check global iteration limit
                    if (sentinelStep.CurrentIteration >= _config.MaxIterations)
                    {
                        _logger.LogWarning("Sentinel exceeded maximum iterations: {MaxIterations}", 
                            _config.MaxIterations);

                        return new StepExecutionResult
                        {
                            Success = false,
                            Error = $"Exceeded maximum iterations ({_config.MaxIterations})"
                        };
                    }

                    // Sleep before next check
                    var sleepDuration = CalculateSleepDuration(sentinelStep);
                    await Task.Delay(sleepDuration, sentinelToken);
                }
                catch (OperationCanceledException) when (sentinelToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Sentinel step execution was cancelled: {StepTitle}", sentinelStep.Title);
                    break;
                }
                catch (Exception ex)
                {
                    sentinelStep.ErrorCount++;
                    _logger.LogError(ex, "Error in sentinel iteration {Iteration} for step: {StepTitle}",
                        sentinelStep.CurrentIteration, sentinelStep.Title);

                    if (sentinelStep.ErrorCount >= _config.MaxErrors)
                    {
                        _logger.LogError("Sentinel exceeded maximum errors: {MaxErrors}", _config.MaxErrors);

                        return new StepExecutionResult
                        {
                            Success = false,
                            Error = $"Exceeded maximum errors ({_config.MaxErrors}). Last error: {ex.Message}"
                        };
                    }

                    // Continue with adaptive sleep on errors
                    if (_config.UseAdaptiveSleep)
                    {
                        var errorSleepDuration = TimeSpan.FromSeconds(Math.Min(
                            sentinelStep.ErrorCount * 5, 
                            _config.MaxSleepDuration.TotalSeconds));
                        
                        await Task.Delay(errorSleepDuration, sentinelToken);
                    }
                }
            }

            return new StepExecutionResult
            {
                Success = false,
                Error = "Sentinel execution was cancelled or stopped"
            };
        }
        finally
        {
            // Clean up
            _runningSentinels.TryRemove(sentinelStep.SentinelId, out _);
        }
    }

    /// <summary>
    /// Stop a running sentinel
    /// </summary>
    public Task StopSentinelAsync(string sentinelId)
    {
        _logger.LogInformation("Stopping sentinel: {SentinelId}", sentinelId);

        if (_runningSentinels.TryRemove(sentinelId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop all running sentinels
    /// </summary>
    public Task StopAllSentinelsAsync()
    {
        _logger.LogInformation("Stopping all running sentinels");

        var sentinelIds = _runningSentinels.Keys.ToList();
        var tasks = sentinelIds.Select(StopSentinelAsync);

        return Task.WhenAll(tasks);
    }

    /// <summary>
    /// Get information about running sentinels
    /// </summary>
    public Task<SentinelInfo[]> GetRunningSentinelsAsync()
    {
        var sentinels = _runningSentinels.Keys
            .Select(id => new SentinelInfo { SentinelId = id, IsRunning = true })
            .ToArray();

        return Task.FromResult(sentinels);
    }

    private async Task<bool> EvaluateConditionAsync(SentinelPlanStep sentinelStep, CancellationToken cancellationToken)
    {
        // For iteration-based conditions, always return false until iteration count is reached
        if (sentinelStep.IsIterationBased)
        {
            return sentinelStep.CurrentIteration >= sentinelStep.ConditionAsInt;
        }

        // For string conditions, use LLM evaluation
        var condition = sentinelStep.ConditionAsString;
        if (string.IsNullOrEmpty(condition))
        {
            _logger.LogWarning("Empty condition for sentinel step: {StepTitle}", sentinelStep.Title);
            return false;
        }

        try
        {
            var prompt = ConditionEvaluationPrompt
                .Replace("{condition}", condition)
                .Replace("{context}", $"Step: {sentinelStep.Title}, Iteration: {sentinelStep.CurrentIteration}");

            var context = new ConversationContext();
            context.AddMessage(new SystemMessage { Content = prompt });

            var response = await _chatClient.GetChatCompletionAsync(context, cancellationToken);

            if (response.IsSuccess && !string.IsNullOrEmpty(response.Content))
            {
                var result = response.Content.Trim().ToLowerInvariant();
                
                if (result == "true")
                {
                    return true;
                }
                else if (result == "false")
                {
                    return false;
                }
                else
                {
                    _logger.LogWarning("Invalid condition evaluation response: {Response}", response.Content);
                    return false;
                }
            }
            else
            {
                _logger.LogWarning("Failed to get condition evaluation response: {Error}", response.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating sentinel condition: {Condition}", condition);
            throw;
        }
    }

    private TimeSpan CalculateSleepDuration(SentinelPlanStep sentinelStep)
    {
        var baseDuration = TimeSpan.FromSeconds(sentinelStep.SleepDuration);

        // Ensure within bounds
        if (baseDuration < _config.MinSleepDuration)
            baseDuration = _config.MinSleepDuration;
        else if (baseDuration > _config.MaxSleepDuration)
            baseDuration = _config.MaxSleepDuration;

        // Apply adaptive sleep based on errors
        if (_config.UseAdaptiveSleep && sentinelStep.ErrorCount > 0)
        {
            var multiplier = 1.0 + (sentinelStep.ErrorCount * 0.5);
            var adaptedDuration = TimeSpan.FromMilliseconds(baseDuration.TotalMilliseconds * multiplier);
            
            // Still respect maximum
            if (adaptedDuration > _config.MaxSleepDuration)
                adaptedDuration = _config.MaxSleepDuration;

            return adaptedDuration;
        }

        return baseDuration;
    }
}

/// <summary>
/// Information about a running sentinel
/// </summary>
public class SentinelInfo
{
    /// <summary>
    /// Unique identifier for the sentinel
    /// </summary>
    public string SentinelId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the sentinel is currently running
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// When the sentinel was started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Current iteration number
    /// </summary>
    public int CurrentIteration { get; set; }

    /// <summary>
    /// Number of errors encountered
    /// </summary>
    public int ErrorCount { get; set; }
}