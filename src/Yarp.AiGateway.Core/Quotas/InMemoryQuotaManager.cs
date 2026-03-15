using System.Collections.Concurrent;
using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;
using Yarp.AiGateway.Core.Configuration;

namespace Yarp.AiGateway.Core.Quotas;

/// <summary>
/// In-memory quota manager. For production, replace with Redis-based implementation.
/// </summary>
public sealed class InMemoryQuotaManager : IQuotaManager
{
    private readonly AiGatewayConfiguration _config;
    private readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _rateLimits = new();

    public InMemoryQuotaManager(AiGatewayConfiguration config) => _config = config;

    public Task EnsureAllowedAsync(AiGatewayRequest request, CancellationToken ct)
    {
        if (!_config.Quotas.Enabled) return Task.CompletedTask;

        foreach (var rule in _config.Quotas.Rules)
        {
            if (rule.RequestsPerMinute is null) continue;

            var key = BuildRateLimitKey(rule.Scope, request);
            var now = DateTime.UtcNow;

            var entry = _rateLimits.AddOrUpdate(
                key,
                _ => (1, now),
                (_, existing) =>
                {
                    if ((now - existing.WindowStart).TotalMinutes >= 1)
                        return (1, now);
                    return (existing.Count + 1, existing.WindowStart);
                });

            if (entry.Count > rule.RequestsPerMinute.Value)
                throw new InvalidOperationException(
                    $"Rate limit exceeded for {rule.Scope}: {rule.RequestsPerMinute} requests/min");
        }

        return Task.CompletedTask;
    }

    public Task RecordUsageAsync(AiGatewayRequest request, AiGatewayResponse response, CancellationToken ct)
    {
        // In-memory: no persistent tracking — reserved for Redis/SQL implementations
        return Task.CompletedTask;
    }

    private static string BuildRateLimitKey(string scope, AiGatewayRequest request) => scope.ToLowerInvariant() switch
    {
        "user" => $"rate:user:{request.UserId ?? "anonymous"}",
        "tenant" => $"rate:tenant:{request.TenantId ?? "default"}",
        "application" => $"rate:app:{request.ApplicationId ?? "default"}",
        _ => $"rate:global"
    };
}
