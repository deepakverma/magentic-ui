using Azure;
using Azure.AI.OpenAI;
using Magentic.Core.Abstractions;
using Magentic.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Magentic.Samples.Console.LLM;

/// <summary>
/// Configuration for Azure OpenAI
/// </summary>
public class AzureOpenAIConfig
{
    /// <summary>
    /// Azure OpenAI endpoint URL
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// API key for authentication
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Deployment name (model deployment name in Azure)
    /// </summary>
    public string DeploymentName { get; set; } = string.Empty;
    
    /// <summary>
    /// Chat deployment name (alias for DeploymentName for compatibility)
    /// </summary>
    public string ChatDeploymentName => DeploymentName;

    /// <summary>
    /// Maximum tokens for completion
    /// </summary>
    public int MaxTokens { get; set; } = 2000;

    /// <summary>
    /// Temperature for response creativity (0.0-2.0)
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Azure OpenAI implementation of IChatCompletionClient
/// </summary>
public class AzureOpenAIChatClient : IChatCompletionClient
{
    private readonly OpenAIClient _client;
    private readonly AzureOpenAIConfig _config;
    private readonly ILogger<AzureOpenAIChatClient> _logger;

    public AzureOpenAIChatClient(
        IOptions<AzureOpenAIConfig> config,
        ILogger<AzureOpenAIChatClient> logger)
    {
        _config = config.Value;
        _logger = logger;

        if (string.IsNullOrEmpty(_config.Endpoint) || string.IsNullOrEmpty(_config.ApiKey))
        {
            throw new InvalidOperationException("Azure OpenAI endpoint and API key must be configured");
        }

        _client = new OpenAIClient(
            new Uri(_config.Endpoint),
            new AzureKeyCredential(_config.ApiKey));
    }

    public async Task<ChatResponse> GetChatCompletionAsync(
        ConversationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Sending chat completion request to Azure OpenAI");

            // Convert messages to OpenAI format
            var chatMessages = context.Messages.Select(ConvertMessage).ToList();

            var chatCompletionsOptions = new ChatCompletionsOptions(_config.ChatDeploymentName, chatMessages)
            {
                MaxTokens = _config.MaxTokens,
                Temperature = _config.Temperature,
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            var response = await _client.GetChatCompletionsAsync(chatCompletionsOptions, linkedCts.Token);
            
            if (response?.Value?.Choices?.Count > 0)
            {
                var choice = response.Value.Choices[0];
                var content = choice.Message?.Content ?? "";

                var usage = response.Value.Usage != null ? new ChatUsage
                {
                    PromptTokens = response.Value.Usage.PromptTokens,
                    CompletionTokens = response.Value.Usage.CompletionTokens
                } : null;

                _logger.LogDebug("Successfully received response from Azure OpenAI. Tokens: {PromptTokens}/{CompletionTokens}", 
                    usage?.PromptTokens, usage?.CompletionTokens);

                return new ChatResponse
                {
                    IsSuccess = true,
                    Content = content,
                    Usage = usage
                };
            }
            else
            {
                _logger.LogWarning("No choices returned from Azure OpenAI");
                return new ChatResponse
                {
                    IsSuccess = false,
                    Error = "No response choices returned from Azure OpenAI"
                };
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Chat completion request was cancelled");
            return new ChatResponse
            {
                IsSuccess = false,
                Error = "Request was cancelled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Azure OpenAI chat completion");
            return new ChatResponse
            {
                IsSuccess = false,
                Error = $"Azure OpenAI error: {ex.Message}"
            };
        }
    }

    public async IAsyncEnumerable<ChatStreamResponse> GetChatCompletionStreamAsync(
        ConversationContext context, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // For simplicity, simulate streaming with a regular chat completion
        var response = await GetChatCompletionAsync(context, cancellationToken);
        
        if (!response.IsSuccess)
        {
            yield return new ChatStreamResponse
            {
                IsFinal = true,
                Error = response.Error
            };
            yield break;
        }

        // Simulate streaming by chunking the response
        var content = response.Content ?? "";
        var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentContent = "";

        foreach (var word in words)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            currentContent += (currentContent.Length > 0 ? " " : "") + word;
            
            // Simulate delay
            await Task.Delay(50, cancellationToken);
            
            yield return new ChatStreamResponse
            {
                IsFinal = false,
                Delta = (currentContent.Length > word.Length ? " " : "") + word,
                Content = currentContent
            };
        }

        yield return new ChatStreamResponse
        {
            IsFinal = true,
            Content = currentContent
        };
    }

    private static Azure.AI.OpenAI.ChatRequestMessage ConvertMessage(ChatMessage message)
    {
        return message.Role.ToLowerInvariant() switch
        {
            "system" => new ChatRequestSystemMessage(message.Content),
            "user" => new ChatRequestUserMessage(message.Content),
            "assistant" => new ChatRequestAssistantMessage(message.Content),
            _ => throw new ArgumentException($"Unknown message role: {message.Role}")
        };
    }
}