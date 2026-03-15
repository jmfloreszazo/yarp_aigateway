using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Core.Pipeline;

public sealed class DefaultPolicyEvaluator : IPolicyEvaluator
{
    public Task<PolicyContext> EvaluateAsync(AiGatewayRequest request, CancellationToken ct)
    {
        return Task.FromResult(new PolicyContext
        {
            ResolvedProvider = request.RequestedProvider,
            ResolvedModel = request.RequestedModel
        });
    }
}
