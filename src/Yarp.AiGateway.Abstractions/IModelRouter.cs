using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Abstractions;

public interface IModelRouter
{
    Task<RouteDecision> RouteAsync(AiGatewayRequest request, PolicyContext policyContext, CancellationToken ct);
}
