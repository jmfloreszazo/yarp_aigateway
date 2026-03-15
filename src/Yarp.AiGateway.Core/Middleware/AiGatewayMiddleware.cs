using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;
using Yarp.AiGateway.Core.Configuration;

namespace Yarp.AiGateway.Core.Middleware;

public sealed class AiGatewayMiddleware
{
    private readonly RequestDelegate _next;

    public AiGatewayMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        AiGatewayConfiguration config,
        IAiGatewayPipeline pipeline)
    {
        if (!context.Request.Path.Equals(config.Gateway.Endpoint, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!HttpMethods.IsPost(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        AiGatewayRequest request;
        try
        {
            request = await ParseRequestAsync(context);
        }
        catch (JsonException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteJsonAsync(context, new { error = "Invalid JSON payload" });
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        cts.CancelAfter(TimeSpan.FromSeconds(config.Gateway.TimeoutSeconds));

        var response = await pipeline.ExecuteAsync(request, cts.Token);

        context.Response.StatusCode = response.Blocked
            ? StatusCodes.Status422UnprocessableEntity
            : StatusCodes.Status200OK;

        await WriteJsonAsync(context, response);
    }

    private static async Task<AiGatewayRequest> ParseRequestAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        return JsonSerializer.Deserialize<AiGatewayRequest>(body, JsonOptions)
               ?? throw new JsonException("Empty request body");
    }

    private static Task WriteJsonAsync<T>(HttpContext context, T value)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(value, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
