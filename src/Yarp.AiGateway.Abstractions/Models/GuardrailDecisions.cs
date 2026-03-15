namespace Yarp.AiGateway.Abstractions.Models;

public sealed record InputGuardrailDecision(
    bool Allowed,
    string? Reason = null,
    AiGatewayRequest? UpdatedRequest = null);

public sealed record OutputGuardrailDecision(
    bool Allowed,
    string? Reason = null,
    AiGatewayResponse? UpdatedResponse = null);
