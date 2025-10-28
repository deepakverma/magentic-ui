using Magentic.Agents;
using Magentic.Core.Abstractions;
using Magentic.Planning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Magentic.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Magentic services with dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add core Magentic services to the service collection
    /// </summary>
    public static IServiceCollection AddMagentic(this IServiceCollection services)
    {
        // Core services
        services.TryAddSingleton<IAgentRegistry, AgentRegistry>();
        services.TryAddScoped<IPlanExecutor, PlanExecutor>();
        services.TryAddScoped<ISentinelExecutor, SentinelExecutor>();
        services.TryAddScoped<IPlanningEngine, PlanningEngine>();
        services.TryAddScoped<IOrchestrator, Orchestrator>();

        return services;
    }

    /// <summary>
    /// Add Magentic services with custom configurations
    /// </summary>
    public static IServiceCollection AddMagentic(
        this IServiceCollection services,
        Action<MagenticOptions> configure)
    {
        var options = new MagenticOptions();
        configure(options);

        // Register configurations
        services.TryAddSingleton(options.PlanningEngineConfig);
        services.TryAddSingleton(options.OrchestratorConfig);
        services.TryAddSingleton(options.PlanExecutorConfig);
        services.TryAddSingleton(options.SentinelExecutorConfig);

        // Core services
        services.TryAddSingleton<IAgentRegistry, AgentRegistry>();
        services.TryAddScoped<IPlanExecutor, PlanExecutor>();
        services.TryAddScoped<ISentinelExecutor, SentinelExecutor>();
        services.TryAddScoped<IPlanningEngine, PlanningEngine>();
        services.TryAddScoped<IOrchestrator, Orchestrator>();

        return services;
    }

    /// <summary>
    /// Register a chat completion client
    /// </summary>
    public static IServiceCollection AddChatCompletionClient<T>(this IServiceCollection services)
        where T : class, IChatCompletionClient
    {
        services.TryAddScoped<IChatCompletionClient, T>();
        return services;
    }

    /// <summary>
    /// Register a chat completion client with factory
    /// </summary>
    public static IServiceCollection AddChatCompletionClient<T>(
        this IServiceCollection services,
        Func<IServiceProvider, T> factory)
        where T : class, IChatCompletionClient
    {
        services.TryAddScoped<IChatCompletionClient>(factory);
        return services;
    }

    /// <summary>
    /// Register basic agents
    /// </summary>
    public static IServiceCollection AddBasicAgents(this IServiceCollection services)
    {
        services.TryAddTransient<ChatAgent>();
        services.TryAddTransient<TaskAgent>();
        services.TryAddTransient<ResearchAgent>();

        return services;
    }

    /// <summary>
    /// Register an agent with the agent registry
    /// </summary>
    public static IServiceCollection AddAgent<T>(
        this IServiceCollection services,
        string agentName)
        where T : class, IAgent
    {
        services.TryAddTransient<T>();
        
        services.AddSingleton<IAgentRegistration>(provider =>
            new AgentRegistration(agentName, typeof(T)));

        return services;
    }

    /// <summary>
    /// Register an agent instance with the agent registry
    /// </summary>
    public static IServiceCollection AddAgent<T>(
        this IServiceCollection services,
        string agentName,
        T agent)
        where T : class, IAgent
    {
        services.AddSingleton<IAgentRegistration>(
            new AgentRegistration(agentName, agent));

        return services;
    }

    /// <summary>
    /// Register an agent with factory
    /// </summary>
    public static IServiceCollection AddAgent<T>(
        this IServiceCollection services,
        string agentName,
        Func<IServiceProvider, T> factory)
        where T : class, IAgent
    {
        services.AddSingleton<IAgentRegistration>(provider =>
            new AgentRegistration(agentName, factory(provider)));

        return services;
    }

    /// <summary>
    /// Configure agent registry with registered agents
    /// </summary>
    public static IServiceCollection ConfigureAgentRegistry(this IServiceCollection services)
    {
        services.AddSingleton<IAgentRegistryConfigurator, AgentRegistryConfigurator>();
        return services;
    }
}

/// <summary>
/// Configuration options for Magentic services
/// </summary>
public class MagenticOptions
{
    /// <summary>
    /// Planning engine configuration
    /// </summary>
    public PlanningEngineConfig PlanningEngineConfig { get; set; } = new();

    /// <summary>
    /// Orchestrator configuration
    /// </summary>
    public OrchestratorConfig OrchestratorConfig { get; set; } = new();

    /// <summary>
    /// Plan executor configuration
    /// </summary>
    public PlanExecutorConfig PlanExecutorConfig { get; set; } = new();

    /// <summary>
    /// Sentinel executor configuration
    /// </summary>
    public SentinelExecutorConfig SentinelExecutorConfig { get; set; } = new();
}

/// <summary>
/// Represents an agent registration
/// </summary>
public interface IAgentRegistration
{
    string Name { get; }
    IAgent GetAgent(IServiceProvider serviceProvider);
}

/// <summary>
/// Agent registration implementation
/// </summary>
public class AgentRegistration : IAgentRegistration
{
    private readonly Type? _agentType;
    private readonly IAgent? _agentInstance;
    private readonly Func<IServiceProvider, IAgent>? _factory;

    public string Name { get; }

    public AgentRegistration(string name, Type agentType)
    {
        Name = name;
        _agentType = agentType;
    }

    public AgentRegistration(string name, IAgent agentInstance)
    {
        Name = name;
        _agentInstance = agentInstance;
    }

    public AgentRegistration(string name, Func<IServiceProvider, IAgent> factory)
    {
        Name = name;
        _factory = factory;
    }

    public IAgent GetAgent(IServiceProvider serviceProvider)
    {
        if (_agentInstance != null)
            return _agentInstance;

        if (_factory != null)
            return _factory(serviceProvider);

        if (_agentType != null)
            return (IAgent)serviceProvider.GetRequiredService(_agentType);

        throw new InvalidOperationException("No agent type, instance, or factory configured");
    }
}

/// <summary>
/// Configures the agent registry with registered agents
/// </summary>
public interface IAgentRegistryConfigurator
{
    void Configure(IAgentRegistry registry, IServiceProvider serviceProvider);
}

/// <summary>
/// Default implementation of agent registry configurator
/// </summary>
public class AgentRegistryConfigurator : IAgentRegistryConfigurator
{
    public void Configure(IAgentRegistry registry, IServiceProvider serviceProvider)
    {
        var registrations = serviceProvider.GetServices<IAgentRegistration>();
        
        foreach (var registration in registrations)
        {
            try
            {
                var agent = registration.GetAgent(serviceProvider);
                registry.RegisterAgent(registration.Name, agent);
            }
            catch (Exception ex)
            {
                // Log error but continue with other registrations
                var logger = serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger<AgentRegistryConfigurator>>();
                logger?.LogError(ex, "Failed to register agent: {AgentName}", registration.Name);
            }
        }
    }
}