using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Core.Middleware;

/// <summary>
/// YARP pipeline middleware that applies AI Gateway guardrails to requests
/// before YARP forwards them to the backend LLM cluster, and inspects
/// responses for output guardrail violations.
///
/// Flow: Client → YARP → [this middleware] → Backend → [output guardrails] → Client
/// </summary>
public sealed class YarpGuardrailMiddleware
{
    private readonly RequestDelegate _next;

    public YarpGuardrailMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var logger = context.RequestServices.GetService<ILogger<YarpGuardrailMiddleware>>();

        // ── Input guardrails ────────────────────────────────────────
        if (HasBody(context.Request.Method))
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            var prompt = ExtractPrompt(body);

            if (!string.IsNullOrEmpty(prompt))
            {
                var request = new AiGatewayRequest
                {
                    Prompt = prompt,
                    UserId = context.Request.Headers["X-User-Id"].FirstOrDefault()
                };

                var inputGuardrails = context.RequestServices.GetServices<IInputGuardrail>();

                foreach (var guardrail in inputGuardrails.OrderBy(g => g.Order))
                {
                    var decision = await guardrail.EvaluateAsync(request, context.RequestAborted);

                    if (!decision.Allowed)
                    {
                        logger?.LogWarning(
                            "Input guardrail '{Guardrail}' blocked request: {Reason}",
                            guardrail.Name, decision.Reason);

                        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new
                        {
                            blocked = true,
                            blockReason = decision.Reason ?? "Blocked by input guardrail"
                        }, SerializerOptions);
                        return;
                    }

                    request = decision.UpdatedRequest ?? request;
                }

                // If the prompt was sanitized (e.g., PII redacted), rewrite the body
                if (request.Prompt != prompt)
                {
                    logger?.LogInformation("Prompt sanitized by guardrails — rewriting request body");
                    var sanitizedBody = body.Replace(prompt, request.Prompt);
                    var bytes = Encoding.UTF8.GetBytes(sanitizedBody);
                    context.Request.Body = new MemoryStream(bytes);
                    context.Request.ContentLength = bytes.Length;
                }
            }
        }

        // ── Forward via YARP (with response buffering) ──────────────
        var originalResponseBody = context.Response.Body;
        using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        await _next(context);

        // ── Output guardrails ───────────────────────────────────────
        responseBuffer.Position = 0;
        var responseText = await new StreamReader(responseBuffer, Encoding.UTF8).ReadToEndAsync();

        if (!string.IsNullOrEmpty(responseText))
        {
            var responseContent = ExtractResponseContent(responseText);

            if (!string.IsNullOrEmpty(responseContent))
            {
                var outputGuardrails = context.RequestServices.GetServices<IOutputGuardrail>();
                var aiResponse = new AiGatewayResponse { Content = responseContent };

                foreach (var guardrail in outputGuardrails.OrderBy(g => g.Order))
                {
                    var decision = await guardrail.EvaluateAsync(aiResponse, context.RequestAborted);

                    if (!decision.Allowed)
                    {
                        logger?.LogWarning(
                            "Output guardrail '{Guardrail}' blocked response: {Reason}",
                            guardrail.Name, decision.Reason);

                        context.Response.Body = originalResponseBody;
                        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                        context.Response.ContentType = "application/json";
                        context.Response.Headers.ContentLength = null;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            blocked = true,
                            blockReason = decision.Reason ?? "Blocked by output guardrail"
                        }, SerializerOptions);
                        return;
                    }
                }
            }
        }

        // ── Pass through ────────────────────────────────────────────
        responseBuffer.Position = 0;
        context.Response.Body = originalResponseBody;
        context.Response.Headers.ContentLength = null;
        await responseBuffer.CopyToAsync(originalResponseBody);
    }

    // ────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────

    private static bool HasBody(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method);

    /// <summary>
    /// Extracts user prompt text from an OpenAI-format chat body or a
    /// simple <c>{ "prompt": "..." }</c> body.
    /// </summary>
    private static string? ExtractPrompt(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // OpenAI chat format: {"messages": [{"role": "user", "content": "…"}]}
            if (root.TryGetProperty("messages", out var messages) &&
                messages.ValueKind == JsonValueKind.Array)
            {
                var parts = new List<string>();
                foreach (var msg in messages.EnumerateArray())
                {
                    if (msg.TryGetProperty("role", out var role) &&
                        role.GetString() is "user" or "system" &&
                        msg.TryGetProperty("content", out var content) &&
                        content.GetString() is { } text)
                    {
                        parts.Add(text);
                    }
                }
                return parts.Count > 0 ? string.Join("\n", parts) : null;
            }

            // Simple format: {"prompt": "…"}
            if (root.TryGetProperty("prompt", out var promptProp))
                return promptProp.GetString();
        }
        catch (JsonException)
        {
            // Non-JSON body — nothing to inspect
        }

        return null;
    }

    /// <summary>
    /// Extracts assistant content from an OpenAI chat completion response.
    /// </summary>
    private static string? ExtractResponseContent(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Standard: {"choices": [{"message": {"content": "…"}}]}
            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("content", out var content))
                {
                    return content.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // Not a JSON response
        }

        return null;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
