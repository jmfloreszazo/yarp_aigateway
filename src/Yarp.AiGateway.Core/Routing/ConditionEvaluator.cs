using System.Globalization;
using Yarp.AiGateway.Abstractions.Models;
using Yarp.AiGateway.Core.Configuration;

namespace Yarp.AiGateway.Core.Routing;

public static class ConditionEvaluator
{
    public static bool Matches(
        IReadOnlyCollection<ConditionSection> conditions,
        AiGatewayRequest request,
        PolicyContext policyContext)
    {
        if (conditions.Count == 0) return true;

        foreach (var condition in conditions)
        {
            if (!Evaluate(condition, request, policyContext))
                return false;
        }
        return true;
    }

    private static bool Evaluate(
        ConditionSection condition,
        AiGatewayRequest request,
        PolicyContext policyContext)
    {
        var left = ResolveField(condition.Field, request, policyContext);

        return condition.Operator.ToLowerInvariant() switch
        {
            "eq" => string.Equals(left?.ToString(), condition.Value.ToString(), StringComparison.OrdinalIgnoreCase),
            "neq" => !string.Equals(left?.ToString(), condition.Value.ToString(), StringComparison.OrdinalIgnoreCase),
            "gt" => ToDecimal(left) > condition.Value.GetDecimal(),
            "gte" => ToDecimal(left) >= condition.Value.GetDecimal(),
            "lt" => ToDecimal(left) < condition.Value.GetDecimal(),
            "lte" => ToDecimal(left) <= condition.Value.GetDecimal(),
            "contains" => left?.ToString()?.Contains(condition.Value.GetString()!, StringComparison.OrdinalIgnoreCase) == true,
            "startswith" => left?.ToString()?.StartsWith(condition.Value.GetString()!, StringComparison.OrdinalIgnoreCase) == true,
            "exists" => left is not null,
            _ => false
        };
    }

    private static object? ResolveField(string field, AiGatewayRequest request, PolicyContext policyContext)
    {
        return field.ToLowerInvariant() switch
        {
            "prompt.length" => request.Prompt.Length,
            "tenantid" => request.TenantId,
            "userid" => request.UserId,
            "applicationid" => request.ApplicationId,
            "requestedprovider" => request.RequestedProvider,
            "requestedmodel" => request.RequestedModel,
            "operation" => request.Operation,
            "maxtokens" => request.MaxTokens,
            "temperature" => request.Temperature,
            _ when field.StartsWith("metadata.", StringComparison.OrdinalIgnoreCase)
                => request.Metadata.TryGetValue(field["metadata.".Length..], out var val) ? val : null,
            _ => null
        };
    }

    private static decimal ToDecimal(object? value) =>
        value is not null
            ? Convert.ToDecimal(value, CultureInfo.InvariantCulture)
            : 0m;
}
