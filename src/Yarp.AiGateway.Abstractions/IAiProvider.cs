using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Abstractions;

public interface IAiProvider
{
    string Name { get; }
    Task<AiGatewayResponse> ExecuteAsync(AiGatewayRequest request, RouteDecision route, CancellationToken ct);
}
