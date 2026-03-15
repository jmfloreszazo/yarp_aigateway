using System.Net.Http.Json;
using System.Text.Json;
using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Providers.Ollama;

/// <summary>
/// AI provider for Ollama — local LLM inference.
/// Ollama exposes an OpenAI-compatible API at /v1/chat/completions.
/// No API key is required for local deployments.
/// 
/// Key use-case: PHI (Protected Health Information) and sensitive data
/// that must NEVER leave the organization's infrastructure.
/// </summary>
public sealed class OllamaProvider(HttpClient httpClient) : IAiProvider
{
    public string Name => "ollama";

    public async Task<AiGatewayResponse> ExecuteAsync(AiGatewayRequest request, RouteDecision route, CancellationToken ct)
    {
        // Ollama supports the OpenAI-compatible endpoint /v1/chat/completions
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");

        var payload = new
        {
            model = route.Model,
            messages = BuildMessages(request),
            max_tokens = request.MaxTokens ?? 1000,
            temperature = request.Temperature ?? 0.2,
            stream = false
        };

        httpRequest.Content = JsonContent.Create(payload);

        var response = await httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        return ParseResponse(json, route);
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
            Provider = "ollama",
            Model = route.Model,
            Content = content,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens
        };
    }
}
