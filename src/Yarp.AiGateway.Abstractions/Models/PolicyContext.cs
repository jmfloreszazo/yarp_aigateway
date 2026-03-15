namespace Yarp.AiGateway.Abstractions.Models;

public sealed record PolicyContext
{
    public string? ResolvedProvider { get; init; }
    public string? ResolvedModel { get; init; }
    public Dictionary<string, object?> Properties { get; init; } = new();
}
