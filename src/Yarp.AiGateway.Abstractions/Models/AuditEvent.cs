namespace Yarp.AiGateway.Abstractions.Models;

public sealed class AuditEvent
{
    public string CorrelationId { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string? TenantId { get; init; }
    public string? ApplicationId { get; init; }
    public string? Provider { get; init; }
    public string? Model { get; init; }
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public decimal EstimatedCost { get; init; }
    public bool Blocked { get; init; }
    public string? BlockReason { get; init; }
    public List<string> Flags { get; init; } = [];
    public string? Error { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset FinishedAt { get; init; }
    public double LatencyMs => (FinishedAt - StartedAt).TotalMilliseconds;
}
