using Microsoft.Extensions.Logging;
using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;
using Yarp.AiGateway.Core.Configuration;

namespace Yarp.AiGateway.Core.Pipeline;

public sealed class AiGatewayPipeline : IAiGatewayPipeline
{
    private readonly IEnumerable<IInputGuardrail> _inputGuardrails;
    private readonly IEnumerable<IOutputGuardrail> _outputGuardrails;
    private readonly IPolicyEvaluator _policyEvaluator;
    private readonly IModelRouter _router;
    private readonly IProviderFactory _providerFactory;
    private readonly IQuotaManager _quotaManager;
    private readonly IAuditSink _auditSink;
    private readonly AiGatewayConfiguration _config;
    private readonly ILogger<AiGatewayPipeline> _logger;

    public AiGatewayPipeline(
        IEnumerable<IInputGuardrail> inputGuardrails,
        IEnumerable<IOutputGuardrail> outputGuardrails,
        IPolicyEvaluator policyEvaluator,
        IModelRouter router,
        IProviderFactory providerFactory,
        IQuotaManager quotaManager,
        IAuditSink auditSink,
        AiGatewayConfiguration config,
        ILogger<AiGatewayPipeline> logger)
    {
        _inputGuardrails = inputGuardrails;
        _outputGuardrails = outputGuardrails;
        _policyEvaluator = policyEvaluator;
        _router = router;
        _providerFactory = providerFactory;
        _quotaManager = quotaManager;
        _auditSink = auditSink;
        _config = config;
        _logger = logger;
    }

    public async Task<AiGatewayResponse> ExecuteAsync(AiGatewayRequest request, CancellationToken ct)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var startedAt = DateTimeOffset.UtcNow;
        var flags = new List<string>();

        try
        {
            // 1. Quota check
            await _quotaManager.EnsureAllowedAsync(request, ct);

            // 2. Input guardrails
            var currentRequest = request;
            foreach (var guardrail in _inputGuardrails.OrderBy(g => g.Order))
            {
                var decision = await guardrail.EvaluateAsync(currentRequest, ct);
                if (!decision.Allowed)
                {
                    _logger.LogWarning("Input guardrail '{Guardrail}' blocked request: {Reason}",
                        guardrail.Name, decision.Reason);

                    return await AuditAndReturn(Blocked(decision.Reason ?? "Blocked by input guardrail", correlationId),
                        request, correlationId, startedAt, flags, ct);
                }
                currentRequest = decision.UpdatedRequest ?? currentRequest;
            }

            // 3. Policy evaluation
            var policyContext = await _policyEvaluator.EvaluateAsync(currentRequest, ct);

            // 4. Routing
            var route = await _router.RouteAsync(currentRequest, policyContext, ct);
            _logger.LogInformation("Routed to provider={Provider} model={Model}", route.Provider, route.Model);

            // 5. Provider execution with fallback
            AiGatewayResponse rawResponse;
            try
            {
                var provider = _providerFactory.Create(route.Provider);
                rawResponse = await provider.ExecuteAsync(currentRequest, route, ct);
            }
            catch (Exception ex) when (TryFallback(route.Provider, out var fallback))
            {
                _logger.LogWarning(ex, "Provider '{Provider}' failed, falling back to '{Fallback}'",
                    route.Provider, fallback.FallbackProvider);
                flags.Add($"fallback:{route.Provider}->{fallback.FallbackProvider}");

                var fallbackProvider = _providerFactory.Create(fallback.FallbackProvider);
                var fallbackRoute = new RouteDecision(fallback.FallbackProvider, fallback.FallbackModel, UsedFallback: true);
                rawResponse = await fallbackProvider.ExecuteAsync(currentRequest, fallbackRoute, ct);
            }

            // 6. Output guardrails
            var currentResponse = rawResponse;
            foreach (var guardrail in _outputGuardrails.OrderBy(g => g.Order))
            {
                var decision = await guardrail.EvaluateAsync(currentResponse, ct);
                if (!decision.Allowed)
                {
                    _logger.LogWarning("Output guardrail '{Guardrail}' blocked response: {Reason}",
                        guardrail.Name, decision.Reason);

                    return await AuditAndReturn(Blocked(decision.Reason ?? "Blocked by output guardrail", correlationId),
                        request, correlationId, startedAt, flags, ct);
                }
                currentResponse = decision.UpdatedResponse ?? currentResponse;
            }

            // 7. Record usage and return
            var finalResponse = currentResponse with { CorrelationId = correlationId, Flags = flags };
            await _quotaManager.RecordUsageAsync(request, finalResponse, ct);

            return await AuditAndReturn(finalResponse, request, correlationId, startedAt, flags, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Gateway pipeline error");
            return await AuditAndReturn(
                Blocked($"Gateway error: {ex.Message}", correlationId),
                request, correlationId, startedAt, flags, ct);
        }
    }

    private bool TryFallback(string providerName, out FallbackRuleSection fallback)
    {
        fallback = _config.Routing.Fallbacks.FirstOrDefault(f =>
            f.ForProvider.Equals(providerName, StringComparison.OrdinalIgnoreCase))!;
        return fallback is not null;
    }

    private async Task<AiGatewayResponse> AuditAndReturn(
        AiGatewayResponse response,
        AiGatewayRequest request,
        string correlationId,
        DateTimeOffset startedAt,
        List<string> flags,
        CancellationToken ct)
    {
        if (_config.Telemetry.EmitAuditEvents)
        {
            await _auditSink.WriteAsync(new AuditEvent
            {
                CorrelationId = correlationId,
                UserId = request.UserId,
                TenantId = request.TenantId,
                ApplicationId = request.ApplicationId,
                Provider = response.Provider,
                Model = response.Model,
                PromptTokens = response.PromptTokens,
                CompletionTokens = response.CompletionTokens,
                EstimatedCost = response.EstimatedCost,
                Blocked = response.Blocked,
                BlockReason = response.BlockReason,
                Flags = flags,
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.UtcNow
            }, ct);
        }
        return response;
    }

    private static AiGatewayResponse Blocked(string reason, string correlationId) => new()
    {
        Blocked = true,
        BlockReason = reason,
        CorrelationId = correlationId
    };
}
