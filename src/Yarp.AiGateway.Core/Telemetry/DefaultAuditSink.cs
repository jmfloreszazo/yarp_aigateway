using Microsoft.Extensions.Logging;
using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Core.Telemetry;

public sealed class DefaultAuditSink(ILogger<DefaultAuditSink> logger) : IAuditSink
{
    public Task WriteAsync(AuditEvent auditEvent, CancellationToken ct)
    {
        logger.LogInformation(
            "AI_GATEWAY correlation={CorrelationId} user={UserId} tenant={TenantId} " +
            "provider={Provider} model={Model} blocked={Blocked} " +
            "promptTokens={PromptTokens} completionTokens={CompletionTokens} " +
            "cost={Cost} latencyMs={LatencyMs} flags={Flags}",
            auditEvent.CorrelationId,
            auditEvent.UserId,
            auditEvent.TenantId,
            auditEvent.Provider,
            auditEvent.Model,
            auditEvent.Blocked,
            auditEvent.PromptTokens,
            auditEvent.CompletionTokens,
            auditEvent.EstimatedCost,
            auditEvent.LatencyMs,
            string.Join(",", auditEvent.Flags));

        return Task.CompletedTask;
    }
}
