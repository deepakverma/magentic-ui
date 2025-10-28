using Magentic.Core.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Magentic.Planning;

/// <summary>
/// Registry for managing available agents
/// </summary>
public class AgentRegistry : IAgentRegistry
{
    private readonly ConcurrentDictionary<string, IAgent> _agents;
    private readonly ILogger<AgentRegistry> _logger;

    public AgentRegistry(ILogger<AgentRegistry> logger)
    {
        _agents = new ConcurrentDictionary<string, IAgent>();
        _logger = logger;
    }

    /// <summary>
    /// Register an agent
    /// </summary>
    public void RegisterAgent(string name, IAgent agent)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Agent name cannot be null or empty", nameof(name));
        
        if (agent == null)
            throw new ArgumentNullException(nameof(agent));

        _agents.AddOrUpdate(name, agent, (key, existing) =>
        {
            _logger.LogInformation("Updating existing agent registration: {AgentName}", name);
            return agent;
        });

        _logger.LogInformation("Registered agent: {AgentName} of type {AgentType}", 
            name, agent.GetType().Name);
    }

    /// <summary>
    /// Unregister an agent
    /// </summary>
    public bool UnregisterAgent(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        var removed = _agents.TryRemove(name, out var agent);
        
        if (removed)
        {
            _logger.LogInformation("Unregistered agent: {AgentName}", name);
        }
        else
        {
            _logger.LogWarning("Failed to unregister agent (not found): {AgentName}", name);
        }

        return removed;
    }

    /// <summary>
    /// Get an agent by name
    /// </summary>
    public IAgent? GetAgent(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        _agents.TryGetValue(name, out var agent);
        return agent;
    }

    /// <summary>
    /// Get all registered agent names
    /// </summary>
    public IEnumerable<string> GetAgentNames()
    {
        return _agents.Keys.ToList();
    }

    /// <summary>
    /// Get all registered agents
    /// </summary>
    public IEnumerable<IAgent> GetAllAgents()
    {
        return _agents.Values.ToList();
    }

    /// <summary>
    /// Check if an agent is registered
    /// </summary>
    public bool IsAgentRegistered(string name)
    {
        return !string.IsNullOrEmpty(name) && _agents.ContainsKey(name);
    }

    /// <summary>
    /// Get count of registered agents
    /// </summary>
    public int AgentCount => _agents.Count;

    /// <summary>
    /// Clear all registered agents
    /// </summary>
    public void Clear()
    {
        var count = _agents.Count;
        _agents.Clear();
        _logger.LogInformation("Cleared all {Count} registered agents", count);
    }
}

/// <summary>
/// Interface for agent registry
/// </summary>
public interface IAgentRegistry
{
    /// <summary>
    /// Register an agent with a given name
    /// </summary>
    void RegisterAgent(string name, IAgent agent);

    /// <summary>
    /// Unregister an agent
    /// </summary>
    bool UnregisterAgent(string name);

    /// <summary>
    /// Get an agent by name
    /// </summary>
    IAgent? GetAgent(string name);

    /// <summary>
    /// Get all registered agent names
    /// </summary>
    IEnumerable<string> GetAgentNames();

    /// <summary>
    /// Get all registered agents
    /// </summary>
    IEnumerable<IAgent> GetAllAgents();

    /// <summary>
    /// Check if an agent is registered
    /// </summary>
    bool IsAgentRegistered(string name);

    /// <summary>
    /// Get count of registered agents
    /// </summary>
    int AgentCount { get; }

    /// <summary>
    /// Clear all registered agents
    /// </summary>
    void Clear();
}