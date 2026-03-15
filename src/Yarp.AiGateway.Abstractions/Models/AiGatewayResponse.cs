namespace Yarp.AiGateway.Abstractions.Models;

/// <summary>
/// Normalized AI Gateway response — provider agnostic.
/// </summary>
public sealed record AiGatewayResponse
{
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public decimal EstimatedCost { get; init; }
    public bool Blocked { get; init; }
    public string? BlockReason { get; init; }
    public List<string> Flags { get; init; } = [];
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
    public Dictionary<string, object?> Metadata { get; init; } = new();
}
