using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Abstractions;

public interface IInputGuardrail
{
    int Order { get; }
    string Name { get; }
    Task<InputGuardrailDecision> EvaluateAsync(AiGatewayRequest request, CancellationToken ct);
}
