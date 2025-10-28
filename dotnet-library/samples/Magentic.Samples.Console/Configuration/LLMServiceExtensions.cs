using Magentic.Core.Abstractions;
using Magentic.Samples.Console.LLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Magentic.Samples.Console.Configuration;

/// <summary>
/// Configuration for LLM provider selection
/// </summary>
public class LLMConfig
{
    /// <summary>
    /// Provider to use: "OpenAI", "AzureOpenAI", or "Mock"
    /// </summary>
    public string Provider { get; set; } = "Mock";

    /// <summary>
    /// OpenAI configuration
    /// </summary>
    public OpenAIConfig OpenAI { get; set; } = new();

    /// <summary>
    /// Azure OpenAI configuration
    /// </summary>
    public AzureOpenAIConfig AzureOpenAI { get; set; } = new();
}

/// <summary>
/// Factory for creating LLM chat clients based on configuration
/// </summary>
public class ChatClientFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LLMConfig _config;
    private readonly ILogger<ChatClientFactory> _logger;

    public ChatClientFactory(
        IServiceProvider serviceProvider,
        IOptions<LLMConfig> config,
        ILogger<ChatClientFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates the appropriate chat client based on configuration
    /// </summary>
    public IChatCompletionClient CreateChatClient()
    {
        _logger.LogInformation("Creating chat client for provider: {Provider}", _config.Provider);

        return _config.Provider.ToLowerInvariant() switch
        {
            "openai" => CreateOpenAIChatClient(),
            "azureopenai" => CreateAzureOpenAIChatClient(),
            "mock" => CreateMockChatClient(),
            _ => throw new InvalidOperationException($"Unknown LLM provider: {_config.Provider}")
        };
    }

    private IChatCompletionClient CreateOpenAIChatClient()
    {
        if (string.IsNullOrEmpty(_config.OpenAI.ApiKey) || _config.OpenAI.ApiKey == "your-openai-api-key-here")
        {
            _logger.LogWarning("OpenAI API key not configured, falling back to mock client");
            return CreateMockChatClient();
        }

        var httpClient = new HttpClient();
        var options = Options.Create(_config.OpenAI);
        var logger = _serviceProvider.GetRequiredService<ILogger<OpenAIChatClient>>();
        
        return new OpenAIChatClient(httpClient, options, logger);
    }

    private IChatCompletionClient CreateAzureOpenAIChatClient()
    {
        if (string.IsNullOrEmpty(_config.AzureOpenAI.ApiKey) || 
            _config.AzureOpenAI.ApiKey == "your-azure-openai-key-here" ||
            string.IsNullOrEmpty(_config.AzureOpenAI.Endpoint) ||
            _config.AzureOpenAI.Endpoint == "https://your-resource.openai.azure.com")
        {
            _logger.LogWarning("Azure OpenAI configuration not complete, falling back to mock client");
            return CreateMockChatClient();
        }

        var options = Options.Create(_config.AzureOpenAI);
        var logger = _serviceProvider.GetRequiredService<ILogger<AzureOpenAIChatClient>>();
        
        return new AzureOpenAIChatClient(options, logger);
    }

    private IChatCompletionClient CreateMockChatClient()
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<MockChatCompletionClient>>();
        return new MockChatCompletionClient(logger);
    }
}

/// <summary>
/// Extension methods for service registration
/// </summary>
public static class LLMServiceExtensions
{
    /// <summary>
    /// Adds LLM services to the service collection
    /// </summary>
    public static IServiceCollection AddLLMServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure LLM settings
        services.Configure<LLMConfig>(configuration.GetSection("LLM"));
        services.Configure<OpenAIConfig>(configuration.GetSection("LLM:OpenAI"));
        services.Configure<AzureOpenAIConfig>(configuration.GetSection("LLM:AzureOpenAI"));

        // Register factory and client
        services.AddTransient<ChatClientFactory>();
        services.AddTransient<IChatCompletionClient>(provider =>
        {
            var factory = provider.GetRequiredService<ChatClientFactory>();
            return factory.CreateChatClient();
        });

        return services;
    }
}