using Magentic.Core.Abstractions;
using Magentic.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Magentic.Planning;

/// <summary>
/// Configuration for the planning engine
/// </summary>
public class PlanningEngineConfig
{
    /// <summary>
    /// Maximum number of steps in a plan
    /// </summary>
    public int MaxSteps { get; set; } = 20;

    /// <summary>
    /// Maximum number of retries for plan generation
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Whether to enable plan validation
    /// </summary>
    public bool EnablePlanValidation { get; set; } = true;

    /// <summary>
    /// Available agent names for planning
    /// </summary>
    public List<string> AvailableAgents { get; set; } = new();

    /// <summary>
    /// Custom planning prompt template
    /// </summary>
    public string? PlanningPromptTemplate { get; set; }
}

/// <summary>
/// Main planning engine that generates plans using LLM
/// </summary>
public class PlanningEngine : IPlanningEngine
{
    private readonly IChatCompletionClient _chatClient;
    private readonly ILogger<PlanningEngine> _logger;
    private readonly PlanningEngineConfig _config;

    private const string DefaultPlanningPrompt = @"
You are an AI assistant that creates detailed plans to accomplish user tasks. 
Create a step-by-step plan in JSON format with the following structure:

{
  ""title"": ""Plan Title"",
  ""description"": ""Detailed description of what this plan will accomplish"",
  ""steps"": [
    {
      ""title"": ""Step Title"",
      ""details"": ""Detailed description of what to do in this step"",
      ""agent_name"": ""agent_name_responsible_for_this_step""
    }
  ]
}

Available agents: {available_agents}

Guidelines:
1. Break down the task into logical, sequential steps
2. Assign each step to the most appropriate agent
3. Be specific and detailed in step descriptions
4. Ensure steps are actionable and measurable
5. Keep the plan focused and achievable
6. Maximum {max_steps} steps

User Task: {user_input}

Respond ONLY with the JSON plan, no additional text.";

    public PlanningEngine(
        IChatCompletionClient chatClient,
        ILogger<PlanningEngine> logger,
        PlanningEngineConfig? config = null)
    {
        _chatClient = chatClient;
        _logger = logger;
        _config = config ?? new PlanningEngineConfig();
    }

    /// <summary>
    /// Generate a plan based on user input
    /// </summary>
    public async Task<Plan> GeneratePlanAsync(string userInput, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating plan for user input: {UserInput}", userInput);

        var prompt = BuildPlanningPrompt(userInput);
        var context = new ConversationContext();
        context.AddMessage(new SystemMessage { Content = prompt });

        var attempt = 0;
        while (attempt < _config.MaxRetries)
        {
            try
            {
                _logger.LogDebug("Plan generation attempt {Attempt}", attempt + 1);

                var response = await _chatClient.GetChatCompletionAsync(context, cancellationToken);
                
                if (response.IsSuccess && !string.IsNullOrEmpty(response.Content))
                {
                    var plan = ParsePlanFromResponse(response.Content);
                    
                    if (_config.EnablePlanValidation && !ValidatePlan(plan))
                    {
                        _logger.LogWarning("Generated plan failed validation on attempt {Attempt}", attempt + 1);
                        attempt++;
                        continue;
                    }

                    _logger.LogInformation("Successfully generated plan with {StepCount} steps", plan.Steps.Count);
                    return plan;
                }
                else
                {
                    _logger.LogWarning("LLM response was not successful: {Error}", response.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating plan on attempt {Attempt}", attempt + 1);
            }

            attempt++;
        }

        throw new InvalidOperationException($"Failed to generate plan after {_config.MaxRetries} attempts");
    }

    /// <summary>
    /// Revise an existing plan based on feedback
    /// </summary>
    public async Task<Plan> RevisePlanAsync(Plan currentPlan, string feedback, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Revising plan based on feedback: {Feedback}", feedback);

        var prompt = BuildRevisionPrompt(currentPlan, feedback);
        var context = new ConversationContext();
        context.AddMessage(new SystemMessage { Content = prompt });

        try
        {
            var response = await _chatClient.GetChatCompletionAsync(context, cancellationToken);
            
            if (response.IsSuccess && !string.IsNullOrEmpty(response.Content))
            {
                var revisedPlan = ParsePlanFromResponse(response.Content);
                
                // Preserve some metadata from original plan
                revisedPlan.CreatedAt = currentPlan.CreatedAt;
                revisedPlan.UpdatedAt = DateTime.UtcNow;
                
                _logger.LogInformation("Successfully revised plan with {StepCount} steps", revisedPlan.Steps.Count);
                return revisedPlan;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revising plan");
        }

        throw new InvalidOperationException("Failed to revise plan");
    }

    /// <summary>
    /// Validate that a plan is well-formed and reasonable
    /// </summary>
    public bool ValidatePlan(Plan plan)
    {
        if (plan == null)
        {
            _logger.LogWarning("Plan validation failed: Plan is null");
            return false;
        }

        if (string.IsNullOrEmpty(plan.Title))
        {
            _logger.LogWarning("Plan validation failed: Title is empty");
            return false;
        }

        if (plan.Steps.Count == 0)
        {
            _logger.LogWarning("Plan validation failed: No steps defined");
            return false;
        }

        if (plan.Steps.Count > _config.MaxSteps)
        {
            _logger.LogWarning("Plan validation failed: Too many steps ({Count} > {Max})", 
                plan.Steps.Count, _config.MaxSteps);
            return false;
        }

        // Validate each step
        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            
            if (string.IsNullOrEmpty(step.Title))
            {
                _logger.LogWarning("Plan validation failed: Step {Index} has empty title", i);
                return false;
            }

            if (string.IsNullOrEmpty(step.Details))
            {
                _logger.LogWarning("Plan validation failed: Step {Index} has empty details", i);
                return false;
            }

            if (string.IsNullOrEmpty(step.AgentName))
            {
                _logger.LogWarning("Plan validation failed: Step {Index} has no assigned agent", i);
                return false;
            }

            // Validate agent exists if we have a list of available agents
            if (_config.AvailableAgents.Count > 0 && !_config.AvailableAgents.Contains(step.AgentName))
            {
                _logger.LogWarning("Plan validation failed: Step {Index} assigned to unknown agent {Agent}", 
                    i, step.AgentName);
                return false;
            }
        }

        return true;
    }

    private string BuildPlanningPrompt(string userInput)
    {
        var prompt = _config.PlanningPromptTemplate ?? DefaultPlanningPrompt;
        
        return prompt
            .Replace("{available_agents}", string.Join(", ", _config.AvailableAgents))
            .Replace("{max_steps}", _config.MaxSteps.ToString())
            .Replace("{user_input}", userInput);
    }

    private string BuildRevisionPrompt(Plan currentPlan, string feedback)
    {
        var currentPlanJson = JsonSerializer.Serialize(currentPlan, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        return $@"
You are revising an existing plan based on user feedback. Here is the current plan:

{currentPlanJson}

User Feedback: {feedback}

Please revise the plan to address the feedback. Respond ONLY with the updated JSON plan.

Available agents: {string.Join(", ", _config.AvailableAgents)}
Maximum {_config.MaxSteps} steps.";
    }

    private Plan ParsePlanFromResponse(string response)
    {
        try
        {
            // Clean up the response - sometimes LLM adds markdown or extra text
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new PlanStepJsonConverter() }
                };

                return JsonSerializer.Deserialize<Plan>(jsonText, options)!;
            }
            else
            {
                throw new InvalidOperationException("No valid JSON found in response");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse plan from JSON: {Response}", response);
            throw new InvalidOperationException("Failed to parse plan from LLM response", ex);
        }
    }
}

/// <summary>
/// Interface for planning engines
/// </summary>
public interface IPlanningEngine
{
    /// <summary>
    /// Generate a new plan based on user input
    /// </summary>
    Task<Plan> GeneratePlanAsync(string userInput, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revise an existing plan based on feedback
    /// </summary>
    Task<Plan> RevisePlanAsync(Plan currentPlan, string feedback, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate that a plan is well-formed
    /// </summary>
    bool ValidatePlan(Plan plan);
}