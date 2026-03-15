using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Abstractions;

public interface IQuotaManager
{
    Task EnsureAllowedAsync(AiGatewayRequest request, CancellationToken ct);
    Task RecordUsageAsync(AiGatewayRequest request, AiGatewayResponse response, CancellationToken ct);
}
