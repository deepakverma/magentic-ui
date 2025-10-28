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
/// Configuration for OpenAI
/// </summary>
public class OpenAIConfig
{
    /// <summary>
    /// OpenAI API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model name (e.g., gpt-4, gpt-3.5-turbo)
    /// </summary>
    public string Model { get; set; } = "gpt-4";

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

    /// <summary>
    /// Base URL for OpenAI API (defaults to official API)
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
}

/// <summary>
/// OpenAI implementation of IChatCompletionClient
/// </summary>
public class OpenAIChatClient : IChatCompletionClient
{
    private readonly HttpClient _httpClient;
    private readonly OpenAIConfig _config;
    private readonly ILogger<OpenAIChatClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpenAIChatClient(
        HttpClient httpClient,
        IOptions<OpenAIConfig> config,
        ILogger<OpenAIChatClient> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;

        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            throw new InvalidOperationException("OpenAI API key must be configured");
        }

        // Configure HTTP client
        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<ChatResponse> GetChatCompletionAsync(
        ConversationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Sending chat completion request to OpenAI");

            var request = new OpenAIChatRequest
            {
                Model = _config.Model,
                Messages = context.Messages.Select(ConvertMessage).ToArray(),
                MaxTokens = _config.MaxTokens,
                Temperature = _config.Temperature
            };

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/chat/completions", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<OpenAIChatResponse>(responseJson, _jsonOptions);

                if (apiResponse?.Choices?.Length > 0)
                {
                    var choice = apiResponse.Choices[0];
                    var responseContent = choice.Message?.Content ?? "";

                    var usage = apiResponse.Usage != null ? new ChatUsage
                    {
                        PromptTokens = apiResponse.Usage.PromptTokens,
                        CompletionTokens = apiResponse.Usage.CompletionTokens
                    } : null;

                    _logger.LogDebug("Successfully received response from OpenAI. Tokens: {PromptTokens}/{CompletionTokens}", 
                        usage?.PromptTokens, usage?.CompletionTokens);

                    return new ChatResponse
                    {
                        IsSuccess = true,
                        Content = responseContent,
                        Usage = usage
                    };
                }
                else
                {
                    _logger.LogWarning("No choices returned from OpenAI");
                    return new ChatResponse
                    {
                        IsSuccess = false,
                        Error = "No response choices returned from OpenAI"
                    };
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                
                return new ChatResponse
                {
                    IsSuccess = false,
                    Error = $"OpenAI API error: {response.StatusCode} - {errorContent}"
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
            _logger.LogError(ex, "Error calling OpenAI chat completion");
            return new ChatResponse
            {
                IsSuccess = false,
                Error = $"OpenAI error: {ex.Message}"
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

    private static OpenAIMessage ConvertMessage(ChatMessage message)
    {
        return new OpenAIMessage
        {
            Role = message.Role.ToLowerInvariant(),
            Content = message.Content
        };
    }
}

#region OpenAI API Models

public class OpenAIChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public OpenAIMessage[] Messages { get; set; } = Array.Empty<OpenAIMessage>();

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("temperature")]
    public float Temperature { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

public class OpenAIMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class OpenAIChatResponse
{
    [JsonPropertyName("choices")]
    public OpenAIChoice[]? Choices { get; set; }

    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; set; }
}

public class OpenAIChoice
{
    [JsonPropertyName("message")]
    public OpenAIMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class OpenAIUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
}

public class OpenAIStreamResponse
{
    [JsonPropertyName("choices")]
    public OpenAIStreamChoice[]? Choices { get; set; }
}

public class OpenAIStreamChoice
{
    [JsonPropertyName("delta")]
    public OpenAIDelta? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class OpenAIDelta
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

#endregion