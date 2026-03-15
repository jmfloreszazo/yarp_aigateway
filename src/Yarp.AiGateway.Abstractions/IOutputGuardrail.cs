using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Abstractions;

public interface IOutputGuardrail
{
    int Order { get; }
    string Name { get; }
    Task<OutputGuardrailDecision> EvaluateAsync(AiGatewayResponse response, CancellationToken ct);
}
