using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;
using Yarp.AiGateway.Core.Configuration;

namespace Yarp.AiGateway.Core.Routing;

public sealed class DefaultModelRouter : IModelRouter
{
    private readonly AiGatewayConfiguration _configuration;

    public DefaultModelRouter(AiGatewayConfiguration configuration) => _configuration = configuration;

    public Task<RouteDecision> RouteAsync(AiGatewayRequest request, PolicyContext policyContext, CancellationToken ct)
    {
        // If client explicitly requested a provider/model, respect it
        if (!string.IsNullOrWhiteSpace(request.RequestedProvider) &&
            !string.IsNullOrWhiteSpace(request.RequestedModel) &&
            _configuration.Providers.ContainsKey(request.RequestedProvider))
        {
            return Task.FromResult(new RouteDecision(request.RequestedProvider, request.RequestedModel));
        }

        // Evaluate routing rules — first match wins
        foreach (var rule in _configuration.Routing.Rules)
        {
            if (ConditionEvaluator.Matches(rule.Conditions, request, policyContext))
            {
                return Task.FromResult(new RouteDecision(rule.Provider, rule.Model));
            }
        }

        return Task.FromResult(new RouteDecision(
            _configuration.Routing.DefaultProvider!,
            _configuration.Routing.DefaultModel!));
    }
}
