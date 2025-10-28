using Magentic.Core.Abstractions;
using Magentic.Core.Models;
using Microsoft.Extensions.Logging;

namespace Magentic.Planning;

/// <summary>
/// Configuration for the orchestrator
/// </summary>
public class OrchestratorConfig
{
    /// <summary>
    /// Whether to enable automatic plan revision when steps fail
    /// </summary>
    public bool EnableAutoRevision { get; set; } = true;

    /// <summary>
    /// Maximum number of revision attempts
    /// </summary>
    public int MaxRevisionAttempts { get; set; } = 3;

    /// <summary>
    /// Whether to continue execution when a step fails
    /// </summary>
    public bool ContinueOnFailure { get; set; } = false;

    /// <summary>
    /// Timeout for step execution
    /// </summary>
    public TimeSpan StepTimeout { get; set; } = TimeSpan.FromMinutes(10);
}

/// <summary>
/// Main orchestrator that manages plan execution using multiple agents
/// </summary>
public class Orchestrator : IOrchestrator
{
    private readonly IPlanningEngine _planningEngine;
    private readonly IPlanExecutor _planExecutor;
    private readonly ISentinelExecutor _sentinelExecutor;
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<Orchestrator> _logger;
    private readonly OrchestratorConfig _config;

    public Orchestrator(
        IPlanningEngine planningEngine,
        IPlanExecutor planExecutor,
        ISentinelExecutor sentinelExecutor,
        IAgentRegistry agentRegistry,
        ILogger<Orchestrator> logger,
        OrchestratorConfig? config = null)
    {
        _planningEngine = planningEngine;
        _planExecutor = planExecutor;
        _sentinelExecutor = sentinelExecutor;
        _agentRegistry = agentRegistry;
        _logger = logger;
        _config = config ?? new OrchestratorConfig();
    }

    /// <summary>
    /// Execute a task by generating and executing a plan
    /// </summary>
    public async Task<OrchestratorResult> ExecuteTaskAsync(
        string userInput, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting task execution: {UserInput}", userInput);

        try
        {
            // Generate initial plan
            var plan = await _planningEngine.GeneratePlanAsync(userInput, cancellationToken);
            
            return await ExecutePlanAsync(plan, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute task");
            
            return new OrchestratorResult
            {
                Success = false,
                Error = ex.Message,
                CompletedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Execute a pre-generated plan
    /// </summary>
    public async Task<OrchestratorResult> ExecutePlanAsync(
        Plan plan, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing plan: {PlanTitle} with {StepCount} steps", 
            plan.Title, plan.Steps.Count);

        var result = new OrchestratorResult
        {
            Plan = plan,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            plan.Start();
            
            while (!plan.IsCompleted && !cancellationToken.IsCancellationRequested)
            {
                var currentStep = plan.CurrentStep;
                if (currentStep == null)
                    break;

                _logger.LogInformation("Executing step {Index}: {Title}", 
                    plan.CurrentStepIndex + 1, currentStep.Title);

                // Handle sentinel steps differently
                if (currentStep is SentinelPlanStep sentinelStep)
                {
                    var sentinelResult = await ExecuteSentinelStepAsync(sentinelStep, cancellationToken);
                    
                    if (sentinelResult.Success)
                    {
                        plan.CompleteCurrentStep(sentinelResult.Result);
                    }
                    else
                    {
                        await HandleStepFailureAsync(plan, sentinelResult.Error ?? "Sentinel step failed");
                        if (!_config.ContinueOnFailure)
                            break;
                    }
                }
                else
                {
                    // Execute regular step
                    var stepResult = await _planExecutor.ExecuteStepAsync(currentStep, cancellationToken);
                    
                    if (stepResult.Success)
                    {
                        plan.CompleteCurrentStep(stepResult.Result);
                    }
                    else
                    {
                        await HandleStepFailureAsync(plan, stepResult.Error ?? "Step execution failed");
                        if (!_config.ContinueOnFailure)
                            break;
                    }
                }
            }

            result.Success = plan.IsCompleted && !plan.HasErrors;
            result.CompletedAt = DateTime.UtcNow;

            if (result.Success)
            {
                _logger.LogInformation("Plan execution completed successfully");
            }
            else
            {
                _logger.LogWarning("Plan execution completed with errors or incomplete steps");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plan execution");
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Pause plan execution
    /// </summary>
    public async Task PausePlanAsync(Plan plan)
    {
        _logger.LogInformation("Pausing plan execution: {PlanTitle}", plan.Title);
        
        plan.Pause();
        
        // Stop any running sentinel steps
        foreach (var step in plan.Steps.OfType<SentinelPlanStep>())
        {
            await _sentinelExecutor.StopSentinelAsync(step.SentinelId);
        }
    }

    /// <summary>
    /// Resume paused plan execution
    /// </summary>
    public async Task<OrchestratorResult> ResumePlanAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resuming plan execution: {PlanTitle}", plan.Title);
        
        plan.Status = PlanStatus.InProgress;
        return await ExecutePlanAsync(plan, cancellationToken);
    }

    /// <summary>
    /// Cancel plan execution
    /// </summary>
    public async Task CancelPlanAsync(Plan plan)
    {
        _logger.LogInformation("Cancelling plan execution: {PlanTitle}", plan.Title);
        
        plan.Cancel();
        
        // Stop any running sentinel steps
        foreach (var step in plan.Steps.OfType<SentinelPlanStep>())
        {
            await _sentinelExecutor.StopSentinelAsync(step.SentinelId);
        }
    }

    private async Task<StepExecutionResult> ExecuteSentinelStepAsync(
        SentinelPlanStep sentinelStep, 
        CancellationToken cancellationToken)
    {
        try
        {
            return await _sentinelExecutor.ExecuteSentinelStepAsync(sentinelStep, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing sentinel step: {StepTitle}", sentinelStep.Title);
            
            return new StepExecutionResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task HandleStepFailureAsync(Plan plan, string error)
    {
        _logger.LogWarning("Step failed: {Error}", error);
        
        plan.FailCurrentStep(error);

        if (_config.EnableAutoRevision && plan.Metadata.ContainsKey("revision_attempts"))
        {
            var revisionAttempts = (int)plan.Metadata["revision_attempts"];
            
            if (revisionAttempts < _config.MaxRevisionAttempts)
            {
                _logger.LogInformation("Attempting to revise plan due to failure (attempt {Attempt})", 
                    revisionAttempts + 1);

                try
                {
                    var revisedPlan = await _planningEngine.RevisePlanAsync(plan, 
                        $"The following error occurred: {error}. Please revise the plan to address this issue.");
                    
                    // Copy revised steps back to current plan
                    plan.Steps.Clear();
                    plan.Steps.AddRange(revisedPlan.Steps);
                    plan.CurrentStepIndex = 0;
                    plan.Status = PlanStatus.InProgress;
                    plan.Metadata["revision_attempts"] = revisionAttempts + 1;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to revise plan");
                }
            }
        }
    }
}

/// <summary>
/// Interface for orchestrators
/// </summary>
public interface IOrchestrator
{
    /// <summary>
    /// Execute a task by generating and executing a plan
    /// </summary>
    Task<OrchestratorResult> ExecuteTaskAsync(string userInput, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a pre-generated plan
    /// </summary>
    Task<OrchestratorResult> ExecutePlanAsync(Plan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pause plan execution
    /// </summary>
    Task PausePlanAsync(Plan plan);

    /// <summary>
    /// Resume paused plan execution
    /// </summary>
    Task<OrchestratorResult> ResumePlanAsync(Plan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel plan execution
    /// </summary>
    Task CancelPlanAsync(Plan plan);
}

/// <summary>
/// Result of orchestrator execution
/// </summary>
public class OrchestratorResult
{
    /// <summary>
    /// Whether execution was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if execution failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// The plan that was executed
    /// </summary>
    public Plan? Plan { get; set; }

    /// <summary>
    /// When execution started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When execution completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Execution duration
    /// </summary>
    public TimeSpan? Duration => CompletedAt?.Subtract(StartedAt);

    /// <summary>
    /// Additional result data
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}