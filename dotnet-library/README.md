# Magentic .NET Library

.Net implementation of Magentic UI
## Features

- **Multi-Agent System**: Support for different types of AI agents (Chat, Task, Research, etc.)
- **Strategic Planning**: LLM-powered plan generation and execution
-  **Orchestration**: Coordinate multiple agents to complete complex tasks
-  **Sentinel Monitoring**: Long-running monitoring with condition evaluation
-  **Dependency Injection**: Full DI support with Microsoft.Extensions.DependencyInjection
-  **Extensible**: Easy to add custom agents and extend functionality

## Architecture

The library is organized into several NuGet packages:

- **Magentic.Core**: Core abstractions and models
- **Magentic.Planning**: Planning engine and orchestrator
- **Magentic.Agents**: Basic agent implementations
- **Magentic.Extensions.DependencyInjection**: DI extensions and configuration

## Quick Start

### 1. Install NuGet Packages

```bash
dotnet add package Magentic.Core
dotnet add package Magentic.Planning
dotnet add package Magentic.Agents
dotnet add package Magentic.Extensions.DependencyInjection
```

### 2. Setup Dependency Injection

```csharp
using Magentic.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();

// Add logging
services.AddLogging(builder => builder.AddConsole());

// Add Magentic services
services.AddMagentic(options =>
{
    options.PlanningEngineConfig.MaxSteps = 10;
    options.PlanningEngineConfig.AvailableAgents = new List<string> { "chat", "task", "research" };
});

// Add your chat completion client implementation
services.AddChatCompletionClient<YourChatCompletionClient>();

// Add agents
services.AddAgent<ChatAgent>("chat");
services.AddAgent<TaskAgent>("task");
services.AddAgent<ResearchAgent>("research");

var serviceProvider = services.BuildServiceProvider();
```

### 3. Execute Tasks with Orchestrator

```csharp
var orchestrator = serviceProvider.GetRequiredService<IOrchestrator>();

var result = await orchestrator.ExecuteTaskAsync(
    "Research and summarize the latest developments in AI");

if (result.Success)
{
    Console.WriteLine($"Task completed with {result.Plan.Steps.Count} steps");
    foreach (var step in result.Plan.CompletedSteps)
    {
        Console.WriteLine($"✅ {step.Title}: {step.Result}");
    }
}
```

### 4. Use Individual Agents

```csharp
var chatAgent = serviceProvider.GetRequiredService<ChatAgent>();
var response = await chatAgent.ExecuteAsync("Hello, how can you help me?");

Console.WriteLine($"Agent: {response.Content}");
```

## Core Concepts

### Agents

Agents are the core execution units. The library provides several built-in agents:

- **ChatAgent**: General-purpose conversational agent
- **TaskAgent**: Specialized for executing specific tasks
- **ResearchAgent**: Focused on information gathering and analysis

Create custom agents by implementing `IAgent`:

```csharp
public class CustomAgent : IAgent
{
    public async Task<AgentResponse> ExecuteAsync(string input, CancellationToken cancellationToken = default)
    {
        // Your custom agent logic here
        return new AgentResponse 
        { 
            IsSuccess = true, 
            Content = "Custom response" 
        };
    }
}
```

### Plans and Planning

Plans are structured approaches to completing complex tasks:

```csharp
var planningEngine = serviceProvider.GetRequiredService<IPlanningEngine>();

var plan = await planningEngine.GeneratePlanAsync(
    "Create a marketing campaign for a new product");

// Execute the plan
var orchestrator = serviceProvider.GetRequiredService<IOrchestrator>();
var result = await orchestrator.ExecutePlanAsync(plan);
```

### Sentinel Steps

Sentinel steps provide long-running monitoring capabilities:

```csharp
var sentinelStep = new SentinelPlanStep
{
    Title = "Monitor website availability",
    Details = "Check if website is responding every 30 seconds",
    SleepDuration = 30,
    Condition = "website is accessible", // LLM will evaluate this condition
    AgentName = "monitoring"
};

var sentinelExecutor = serviceProvider.GetRequiredService<ISentinelExecutor>();
var result = await sentinelExecutor.ExecuteSentinelStepAsync(sentinelStep);
```

## Advanced Usage

### Custom Chat Completion Client

Implement `IChatCompletionClient` to integrate with your preferred LLM service:

```csharp
public class OpenAIChatClient : IChatCompletionClient
{
    public async Task<ChatResponse> GetChatCompletionAsync(
        ConversationContext context, 
        CancellationToken cancellationToken = default)
    {
        // Integrate with OpenAI API, Azure OpenAI, etc.
        // Return ChatResponse with success/failure and content
    }

    public IAsyncEnumerable<ChatStreamResponse> GetChatCompletionStreamAsync(
        ConversationContext context, 
        CancellationToken cancellationToken = default)
    {
        // Implement streaming responses
    }
}
```

### Configuration Options

Configure the behavior of different components:

```csharp
services.AddMagentic(options =>
{
    // Planning Engine
    options.PlanningEngineConfig.MaxSteps = 15;
    options.PlanningEngineConfig.MaxRetries = 5;
    options.PlanningEngineConfig.EnablePlanValidation = true;

    // Orchestrator
    options.OrchestratorConfig.EnableAutoRevision = true;
    options.OrchestratorConfig.MaxRevisionAttempts = 3;
    options.OrchestratorConfig.ContinueOnFailure = false;

    // Plan Executor
    options.PlanExecutorConfig.DefaultStepTimeout = TimeSpan.FromMinutes(10);
    options.PlanExecutorConfig.MaxRetries = 3;

    // Sentinel Executor
    options.SentinelExecutorConfig.MaxIterations = 1000;
    options.SentinelExecutorConfig.MaxExecutionTime = TimeSpan.FromHours(2);
    options.SentinelExecutorConfig.UseAdaptiveSleep = true;
});
```

## Examples

See the `samples/` directory for complete examples:

- **Console Sample**: Basic usage demonstration
- **Web API Sample**: RESTful API for agent interactions
- **Custom Agents**: Examples of creating specialized agents

## Comparison with Python magentic-ui

This .NET library provides equivalent functionality to the Python magentic-ui project:

| Python magentic-ui | .NET Library |
|-------------------|--------------|
| WebSurfer Agent | Planned for future versions |
| Orchestrator | `IOrchestrator` and `Orchestrator` |
| Planning System | `IPlanningEngine` and `PlanningEngine` |
| Sentinel Steps | `ISentinelExecutor` and `SentinelExecutor` |
| LLM Integration | `IChatCompletionClient` abstraction |
| Multi-Agent Teams | `IAgentRegistry` and agent coordination |

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Roadmap

- [ ] WebSurfer agent implementation
- [ ] Playwright integration for web automation
- [ ] Additional agent types (File, Email, etc.)
- [ ] Enhanced monitoring and logging
- [ ] Performance optimizations
- [ ] More comprehensive examples

## Support

For questions, issues, or contributions, please visit the project repository or create an issue.