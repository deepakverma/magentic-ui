using Magentic.Core.Abstractions;
using Magentic.Core.Models;
using Microsoft.Extensions.Logging;

namespace Magentic.Planning;

/// <summary>
/// Configuration for plan executor
/// </summary>
public class PlanExecutorConfig
{
    /// <summary>
    /// Default timeout for step execution
    /// </summary>
    public TimeSpan DefaultStepTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to log detailed step execution information
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;

    /// <summary>
    /// Maximum number of retries for failed steps
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Executes individual plan steps using registered agents
/// </summary>
public class PlanExecutor : IPlanExecutor
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<PlanExecutor> _logger;
    private readonly PlanExecutorConfig _config;

    public PlanExecutor(
        IAgentRegistry agentRegistry,
        ILogger<PlanExecutor> logger,
        PlanExecutorConfig? config = null)
    {
        _agentRegistry = agentRegistry;
        _logger = logger;
        _config = config ?? new PlanExecutorConfig();
    }

    /// <summary>
    /// Execute a single plan step
    /// </summary>
    public async Task<StepExecutionResult> ExecuteStepAsync(
        PlanStep step,
        CancellationToken cancellationToken = default)
    {
        if (step == null)
            throw new ArgumentNullException(nameof(step));

        _logger.LogInformation("Executing step: {StepTitle} with agent: {AgentName}", 
            step.Title, step.AgentName);

        step.StartedAt = DateTime.UtcNow;

        var agent = _agentRegistry.GetAgent(step.AgentName);
        if (agent == null)
        {
            var error = $"Agent '{step.AgentName}' not found in registry";
            _logger.LogError(error);
            
            return new StepExecutionResult
            {
                Success = false,
                Error = error
            };
        }

        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _config.MaxRetries)
        {
            try
            {
                attempt++;
                
                if (_config.EnableDetailedLogging)
                {
                    _logger.LogDebug("Executing step attempt {Attempt}/{MaxRetries}: {StepTitle}", 
                        attempt, _config.MaxRetries, step.Title);
                }

                // Create timeout for step execution
                using var timeoutCts = new CancellationTokenSource(_config.DefaultStepTimeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                // Execute the step
                var response = await agent.ExecuteAsync(step.Details, combinedCts.Token);

                if (response.IsSuccess)
                {
                    step.CompletedAt = DateTime.UtcNow;
                    
                    if (_config.EnableDetailedLogging)
                    {
                        var duration = step.CompletedAt.Value - step.StartedAt!.Value;
                        _logger.LogInformation("Step completed successfully in {Duration}: {StepTitle}",
                            duration, step.Title);
                    }

                    return new StepExecutionResult
                    {
                        Success = true,
                        Result = response.Content,
                        Metadata = response.Metadata
                    };
                }
                else
                {
                    var error = $"Agent execution failed: {response.Error}";
                    _logger.LogWarning("Step execution failed on attempt {Attempt}: {Error}", 
                        attempt, error);

                    if (attempt >= _config.MaxRetries)
                    {
                        return new StepExecutionResult
                        {
                            Success = false,
                            Error = error
                        };
                    }

                    // Wait before retry
                    if (attempt < _config.MaxRetries)
                    {
                        await Task.Delay(_config.RetryDelay, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Step execution was cancelled: {StepTitle}", step.Title);
                
                return new StepExecutionResult
                {
                    Success = false,
                    Error = "Step execution was cancelled"
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Step execution timed out after {Timeout}: {StepTitle}", 
                    _config.DefaultStepTimeout, step.Title);
                
                return new StepExecutionResult
                {
                    Success = false,
                    Error = $"Step execution timed out after {_config.DefaultStepTimeout}"
                };
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogError(ex, "Error executing step on attempt {Attempt}: {StepTitle}", 
                    attempt, step.Title);

                if (attempt >= _config.MaxRetries)
                {
                    return new StepExecutionResult
                    {
                        Success = false,
                        Error = $"Step execution failed after {_config.MaxRetries} attempts: {ex.Message}"
                    };
                }

                // Wait before retry
                if (attempt < _config.MaxRetries)
                {
                    await Task.Delay(_config.RetryDelay, cancellationToken);
                }
            }
        }

        return new StepExecutionResult
        {
            Success = false,
            Error = $"Step execution failed after {_config.MaxRetries} attempts. Last error: {lastException?.Message}"
        };
    }

    /// <summary>
    /// Execute multiple steps in sequence
    /// </summary>
    public async Task<PlanExecutionResult> ExecuteStepsAsync(
        IEnumerable<PlanStep> steps,
        CancellationToken cancellationToken = default)
    {
        var stepList = steps.ToList();
        var results = new List<StepExecutionResult>();
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("Executing {StepCount} steps in sequence", stepList.Count);

        foreach (var step in stepList)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var result = await ExecuteStepAsync(step, cancellationToken);
            results.Add(result);

            if (!result.Success)
            {
                _logger.LogWarning("Step execution failed, stopping sequence: {StepTitle}", step.Title);
                break;
            }
        }

        var endTime = DateTime.UtcNow;
        var success = results.All(r => r.Success) && results.Count == stepList.Count;

        return new PlanExecutionResult
        {
            Success = success,
            StepResults = results,
            StartTime = startTime,
            EndTime = endTime,
            Duration = endTime - startTime,
            ExecutedStepsCount = results.Count,
            TotalStepsCount = stepList.Count
        };
    }

    /// <summary>
    /// Execute multiple steps in parallel
    /// </summary>
    public async Task<PlanExecutionResult> ExecuteStepsParallelAsync(
        IEnumerable<PlanStep> steps,
        int maxConcurrency = 4,
        CancellationToken cancellationToken = default)
    {
        var stepList = steps.ToList();
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("Executing {StepCount} steps in parallel with max concurrency {MaxConcurrency}", 
            stepList.Count, maxConcurrency);

        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        
        var tasks = stepList.Select(async step =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ExecuteStepAsync(step, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        var endTime = DateTime.UtcNow;
        var success = results.All(r => r.Success);

        return new PlanExecutionResult
        {
            Success = success,
            StepResults = results.ToList(),
            StartTime = startTime,
            EndTime = endTime,
            Duration = endTime - startTime,
            ExecutedStepsCount = results.Length,
            TotalStepsCount = stepList.Count
        };
    }
}



/// <summary>
/// Result of executing multiple plan steps
/// </summary>
public class PlanExecutionResult
{
    /// <summary>
    /// Whether all steps executed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Results from individual step executions
    /// </summary>
    public List<StepExecutionResult> StepResults { get; set; } = new();

    /// <summary>
    /// When execution started
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// When execution ended
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Total execution duration
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Number of steps that were executed
    /// </summary>
    public int ExecutedStepsCount { get; set; }

    /// <summary>
    /// Total number of steps that were supposed to be executed
    /// </summary>
    public int TotalStepsCount { get; set; }

    /// <summary>
    /// Whether all steps were executed
    /// </summary>
    public bool AllStepsExecuted => ExecutedStepsCount == TotalStepsCount;

    /// <summary>
    /// Number of successful steps
    /// </summary>
    public int SuccessfulStepsCount => StepResults.Count(r => r.Success);

    /// <summary>
    /// Number of failed steps
    /// </summary>
    public int FailedStepsCount => StepResults.Count(r => !r.Success);
}