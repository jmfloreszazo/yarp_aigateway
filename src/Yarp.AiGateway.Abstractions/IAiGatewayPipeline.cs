using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Abstractions;

public interface IAiGatewayPipeline
{
    Task<AiGatewayResponse> ExecuteAsync(AiGatewayRequest request, CancellationToken ct);
}
