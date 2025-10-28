using Magentic.Agents;
using Magentic.Core.Abstractions;
using Magentic.Core.Models;
using Magentic.Extensions.DependencyInjection;
using Magentic.Planning;
using Magentic.Samples.Console.Configuration;
using Magentic.Samples.Console.LLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Magentic.Samples.Console;

class Program
{
    static async Task Main(string[] args)
    {
        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        // Setup dependency injection
        var services = new ServiceCollection();
        
        // Add configuration
        services.AddSingleton<IConfiguration>(configuration);
        
        // Add logging with configuration
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
        });

        // Add LLM services with real providers
        services.AddLLMServices(configuration);

        // Add Magentic services
        services.AddMagentic(options =>
        {
            options.PlanningEngineConfig.MaxSteps = 10;
            options.PlanningEngineConfig.AvailableAgents = new List<string> { "chat", "task", "research" };
        });

        // Add agents
        services.AddAgent<ChatAgent>("chat");
        services.AddAgent<TaskAgent>("task");  
        services.AddAgent<ResearchAgent>("research");

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();

        // Configure agent registry
        var agentRegistry = serviceProvider.GetRequiredService<IAgentRegistry>();
        var configurator = serviceProvider.GetService<IAgentRegistryConfigurator>();
        configurator?.Configure(agentRegistry, serviceProvider);

        // Get orchestrator
        var orchestrator = serviceProvider.GetRequiredService<IOrchestrator>();

        // Show configuration info
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var llmConfig = configuration.GetSection("LLM").Get<LLMConfig>();
        
        logger.LogInformation("=== Magentic .NET Library Demo with Real LLM Integration ===");
        logger.LogInformation("LLM Provider: {Provider}", llmConfig?.Provider ?? "Unknown");

        System.Console.WriteLine("=== Magentic .NET Library Demo ===\n");

        // Demo 1: Simple agent execution
        await DemoSimpleAgent(serviceProvider);

        // Demo 2: Plan generation and execution
        await DemoPlanExecution(orchestrator);

        // Demo 3: Sentinel step demonstration
        await DemoSentinelStep(serviceProvider);

        System.Console.WriteLine("\nDemo completed!");
    }

    static async Task DemoSimpleAgent(IServiceProvider serviceProvider)
    {
        System.Console.WriteLine("--- Demo 1: Simple Agent Execution ---");

        var chatAgent = serviceProvider.GetRequiredService<ChatAgent>();
        
        var response = await chatAgent.ExecuteAsync("Hello, can you help me understand what you can do?");
        
        System.Console.WriteLine($"Agent Response: {response.Content}");
        System.Console.WriteLine($"Success: {response.IsSuccess}\n");
    }

    static async Task DemoPlanExecution(IOrchestrator orchestrator)
    {
        System.Console.WriteLine("--- Demo 2: Plan Generation and Execution ---");

        var userTask = "Help me research and write a brief summary about artificial intelligence";
        
        try
        {
            var result = await orchestrator.ExecuteTaskAsync(userTask);
            
            System.Console.WriteLine($"Plan Execution Result: {result.Success}");
            if (result.Plan != null)
            {
                System.Console.WriteLine($"Plan Title: {result.Plan.Title}");
                System.Console.WriteLine($"Steps: {result.Plan.Steps.Count}");
                System.Console.WriteLine($"Completed Steps: {result.Plan.CompletedSteps.Count()}");
            }
            if (!string.IsNullOrEmpty(result.Error))
            {
                System.Console.WriteLine($"Error: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error: {ex.Message}");
        }
        
        System.Console.WriteLine();
    }

    static async Task DemoSentinelStep(IServiceProvider serviceProvider)
    {
        System.Console.WriteLine("--- Demo 3: Sentinel Step Demonstration ---");

        var sentinelExecutor = serviceProvider.GetRequiredService<ISentinelExecutor>();
        
        // Create a simple iteration-based sentinel step
        var sentinelStep = new SentinelPlanStep
        {
            Title = "Monitor for 5 iterations",
            Details = "This is a demo sentinel that runs for 5 iterations",
            AgentName = "sentinel",
            SleepDuration = 1, // 1 second between checks
            Condition = 5 // Run for 5 iterations
        };

        System.Console.WriteLine("Starting sentinel step (will run for 5 seconds)...");
        
        var result = await sentinelExecutor.ExecuteSentinelStepAsync(sentinelStep);
        
        System.Console.WriteLine($"Sentinel Result: {result.Success}");
        System.Console.WriteLine($"Result: {result.Result}");
        if (!string.IsNullOrEmpty(result.Error))
        {
            System.Console.WriteLine($"Error: {result.Error}");
        }
        
        System.Console.WriteLine();
    }
}

