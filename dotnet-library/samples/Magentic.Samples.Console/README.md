# Magentic.NET - Real LLM Integration

This sample demonstrates how to integrate the Magentic.NET library with real Language Model providers (OpenAI, Azure OpenAI).

## Configuration

The application uses `appsettings.json` for configuration. You can choose between different LLM providers:

### 1. OpenAI Integration

Update `appsettings.json`:

```json
{
  "LLM": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "sk-your-actual-openai-api-key-here",
      "Model": "gpt-4",
      "MaxTokens": 2000,
      "Temperature": 0.7,
      "TimeoutSeconds": 60,
      "BaseUrl": "https://api.openai.com/v1"
    }
  }
}
```

### 2. Azure OpenAI Integration

Update `appsettings.json`:

```json
{
  "LLM": {
    "Provider": "AzureOpenAI",
    "AzureOpenAI": {
      "Endpoint": "https://your-resource.openai.azure.com",
      "ApiKey": "your-azure-openai-key-here",
      "DeploymentName": "gpt-4",
      "MaxTokens": 2000,
      "Temperature": 0.7,
      "TimeoutSeconds": 60
    }
  }
}
```

### 3. Mock Client (Default)

For testing without real API keys:

```json
{
  "LLM": {
    "Provider": "Mock"
  }
}
```

## Environment Variables

You can also configure via environment variables:

```bash
# For OpenAI
export LLM__Provider="OpenAI"
export LLM__OpenAI__ApiKey="sk-your-key-here"
export LLM__OpenAI__Model="gpt-4"

# For Azure OpenAI  
export LLM__Provider="AzureOpenAI"
export LLM__AzureOpenAI__Endpoint="https://your-resource.openai.azure.com"
export LLM__AzureOpenAI__ApiKey="your-key-here"
export LLM__AzureOpenAI__DeploymentName="gpt-4"
```

## Command Line Arguments

Override configuration via command line:

```bash
dotnet run --LLM:Provider=OpenAI --LLM:OpenAI:Model=gpt-3.5-turbo
```

## Security Best Practices

1. **Never commit API keys to version control**
2. Use environment variables or Azure Key Vault for production
3. Use the minimum required permissions for API keys
4. Rotate API keys regularly

## Running the Sample

1. **Install dependencies:**
   ```bash
   dotnet restore
   ```

2. **Configure your API keys** (see sections above)

3. **Build and run:**
   ```bash
   dotnet build
   dotnet run
   ```

## Sample Features

The sample demonstrates:

1. **Simple Agent Execution** - Basic chat interactions with your chosen LLM
2. **Plan Generation** - Multi-step planning powered by real AI
3. **Sentinel Monitoring** - Long-running tasks with AI oversight
4. **Token Usage Tracking** - Monitor API costs and usage
5. **Error Handling** - Robust error handling for API failures
6. **Streaming Responses** - Real-time streaming from LLM providers

## Supported Providers

| Provider | Status | Features |
|----------|--------|----------|
| OpenAI | ✅ Complete | Chat completion, streaming, error handling |
| Azure OpenAI | ✅ Complete | Chat completion, streaming, error handling |
| Mock Client | ✅ Complete | Testing, development, CI/CD |

## Troubleshooting

### "API key not configured" Error
- Ensure your API key is properly set in `appsettings.json` or environment variables
- Verify the key hasn't expired or been revoked

### "Endpoint not found" Error (Azure OpenAI)
- Check your Azure OpenAI endpoint URL
- Verify the deployment name matches your Azure configuration

### Build Errors
- Run `dotnet restore` to ensure all packages are installed
- Check that you're using .NET 6.0 or later

## Next Steps

- Integrate with your own agents by implementing `IAgent`
- Create custom planning strategies with `IPlanExecutor`
- Build web applications using the library with dependency injection
- Add custom LLM providers by implementing `IChatCompletionClient`