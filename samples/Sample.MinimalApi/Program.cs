using Yarp.AiGateway.Core.Extensions;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// ─── YARP reverse proxy ────────────────────────────────────────
// Routes and clusters loaded from appsettings.json define WHICH
// backends to proxy to and HOW to transform requests.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context =>
    {
        // Inject the API key from an environment variable into every
        // proxied request. The key never appears in config files.
        context.AddRequestTransform(transformContext =>
        {
            var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? "";
            transformContext.ProxyRequest.Headers.TryAddWithoutValidation("api-key", apiKey);
            return ValueTask.CompletedTask;
        });
    });

// ─── AI Gateway guardrails ─────────────────────────────────────
// Loads guardrail configuration from aigateway.json.
// Registers input guardrails (PII, injection, patterns, secrets)
// and output guardrails (blocked patterns, PII in responses).
builder.Services.AddAiGatewayFromJson("aigateway.json");

var app = builder.Build();

// ─── YARP pipeline with AI Gateway guardrails ──────────────────
// YARP handles the reverse proxying (routing, forwarding, transforms).
// AI Gateway guardrails sit INSIDE the YARP pipeline, inspecting
// every request/response before YARP forwards to the backend.
//
// Flow: Client → YARP → [Input Guardrails] → Backend (Azure OpenAI)
//                                           → [Output Guardrails] → Client
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseAiGatewayGuardrails();
});

app.Run();
