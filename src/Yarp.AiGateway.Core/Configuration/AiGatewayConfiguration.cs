using System.Text.Json;

namespace Yarp.AiGateway.Core.Configuration;

public sealed class AiGatewayConfiguration
{
    public GatewaySection Gateway { get; init; } = new();
    public Dictionary<string, ProviderSection> Providers { get; init; } = new();
    public RoutingSection Routing { get; init; } = new();
    public GuardrailsSection Guardrails { get; init; } = new();
    public QuotasSection Quotas { get; init; } = new();
    public TelemetrySection Telemetry { get; init; } = new();
}

public sealed class GatewaySection
{
    public string Endpoint { get; init; } = "/ai/chat";
    public int TimeoutSeconds { get; init; } = 60;
    public bool IncludePromptInAudit { get; init; }
    public string DefaultOperation { get; init; } = "chat";
}

public sealed class ProviderSection
{
    public string Type { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string? ApiVersion { get; init; }
    public Dictionary<string, string>? DeploymentMap { get; init; }
    public List<string> Models { get; init; } = [];
    public int Priority { get; init; }
    public bool Enabled { get; init; } = true;
}

public sealed class RoutingSection
{
    public string? DefaultProvider { get; init; }
    public string? DefaultModel { get; init; }
    public List<RouteRuleSection> Rules { get; init; } = [];
    public List<FallbackRuleSection> Fallbacks { get; init; } = [];
}

public sealed class RouteRuleSection
{
    public string Name { get; init; } = string.Empty;
    public List<ConditionSection> Conditions { get; init; } = [];
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
}

public sealed class FallbackRuleSection
{
    public string ForProvider { get; init; } = string.Empty;
    public string FallbackProvider { get; init; } = string.Empty;
    public string FallbackModel { get; init; } = string.Empty;
}

public sealed class ConditionSection
{
    public string Field { get; init; } = string.Empty;
    public string Operator { get; init; } = string.Empty;
    public JsonElement Value { get; init; }
}

public sealed class GuardrailsSection
{
    public List<GuardrailDefinition> Input { get; init; } = [];
    public List<GuardrailDefinition> Output { get; init; } = [];
}

public sealed class GuardrailDefinition
{
    public string Type { get; init; } = string.Empty;
    public string Mode { get; init; } = "block";
    public Dictionary<string, JsonElement> Parameters { get; init; } = new();
}

public sealed class QuotasSection
{
    public bool Enabled { get; init; }
    public List<QuotaRule> Rules { get; init; } = [];
}

public sealed class QuotaRule
{
    public string Scope { get; init; } = string.Empty;
    public int? RequestsPerMinute { get; init; }
    public decimal? MonthlyBudgetUsd { get; init; }
}

public sealed class TelemetrySection
{
    public bool Enabled { get; init; } = true;
    public string LogLevel { get; init; } = "Information";
    public bool EmitMetrics { get; init; } = true;
    public bool EmitTraces { get; init; } = true;
    public bool EmitAuditEvents { get; init; } = true;
}
