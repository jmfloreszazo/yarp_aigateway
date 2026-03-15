namespace Yarp.AiGateway.Abstractions.Models;

/// <summary>
/// Normalized AI Gateway request — provider agnostic.
/// </summary>
public sealed record AiGatewayRequest
{
    public string Operation { get; init; } = "chat";
    public string Prompt { get; init; } = string.Empty;
    public string? SystemPrompt { get; init; }
    public string? UserId { get; init; }
    public string? TenantId { get; init; }
    public string? ApplicationId { get; init; }
    public string? ConversationId { get; init; }
    public string? RequestedProvider { get; init; }
    public string? RequestedModel { get; init; }
    public int? MaxTokens { get; init; }
    public double? Temperature { get; init; }
    public Dictionary<string, object?> Metadata { get; init; } = new();
}
