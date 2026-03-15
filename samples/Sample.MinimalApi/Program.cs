using Yarp.AiGateway.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ─── YARP reverse proxy ────────────────────────────────────────
// Standard YARP setup: routes and clusters loaded from appsettings.json.
// Requests matching a YARP route are forwarded to the upstream cluster
// with NO guardrails — pure reverse proxy behavior.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ─── AI Gateway extension ──────────────────────────────────────
// Extends YARP with AI-specific guardrails, intelligent routing,
// PII redaction, prompt injection detection, blocked patterns,
// quota management, and audit logging.
// Everything is declared in aigateway.json.
builder.Services.AddAiGatewayFromJson("aigateway.json");

var app = builder.Build();

// AI Gateway middleware intercepts POST /ai/chat and runs the
// full guardrail pipeline before forwarding to the LLM provider.
app.UseAiGateway();

// YARP reverse proxy handles all other matched routes.
app.MapReverseProxy();

app.Run();
