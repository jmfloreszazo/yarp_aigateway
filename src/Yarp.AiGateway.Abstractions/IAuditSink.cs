using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Abstractions;

public interface IAuditSink
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken ct);
}
