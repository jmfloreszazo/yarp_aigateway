using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Providers.AzureOpenAI;

public sealed class AzureOpenAiProvider(
    HttpClient httpClient,
    string apiKey,
    string apiVersion,
    Dictionary<string, string>? deploymentMap) : IAiProvider
{
    public string Name => "azure-openai";

    public async Task<AiGatewayResponse> ExecuteAsync(AiGatewayRequest request, RouteDecision route, CancellationToken ct)
    {
        var deploymentName = ResolveDeployment(route.Model);
        var requestUri = $"openai/deployments/{deploymentName}/chat/completions?api-version={apiVersion}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);
        httpRequest.Headers.Add("api-key", apiKey);

        var payload = new
        {
            messages = BuildMessages(request),
            max_tokens = request.MaxTokens ?? 1000,
            temperature = request.Temperature ?? 0.2
        };

        httpRequest.Content = JsonContent.Create(payload);

        var response = await httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        return ParseResponse(json, route);
    }

    private string ResolveDeployment(string model)
    {
        if (deploymentMap is not null && deploymentMap.TryGetValue(model, out var deployment))
            return deployment;
        return model; // Use model name as deployment name if no map
    }

    private static object[] BuildMessages(AiGatewayRequest request)
    {
        var messages = new List<object>();

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            messages.Add(new { role = "system", content = request.SystemPrompt });

        messages.Add(new { role = "user", content = request.Prompt });
        return [.. messages];
    }

    private static AiGatewayResponse ParseResponse(JsonDocument json, RouteDecision route)
    {
        var root = json.RootElement;
        var content = root.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        int promptTokens = 0, completionTokens = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            promptTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
            completionTokens = usage.TryGetProperty("completion_tokens", out var ctk) ? ctk.GetInt32() : 0;
        }

        return new AiGatewayResponse
        {
            Provider = "azure-openai",
            Model = route.Model,
            Content = content,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens
        };
    }
}
