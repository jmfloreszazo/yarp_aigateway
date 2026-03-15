using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Abstractions;

public interface IPolicyEvaluator
{
    Task<PolicyContext> EvaluateAsync(AiGatewayRequest request, CancellationToken ct);
}
