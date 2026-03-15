using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Guardrails.Output;

/// <summary>
/// Detects and redacts PII in LLM responses before returning to the client.
/// Mirrors input PII detection but applied on the output side.
/// </summary>
public sealed partial class PiiOutputGuardrail : IOutputGuardrail
{
    private readonly ILogger<PiiOutputGuardrail> _logger;

    public int Order => 200;
    public string Name => "output-pii-redaction";

    public PiiOutputGuardrail(ILogger<PiiOutputGuardrail> logger) => _logger = logger;

    public Task<OutputGuardrailDecision> EvaluateAsync(AiGatewayResponse response, CancellationToken ct)
    {
        var content = response.Content;
        var originalLength = content.Length;

        content = EmailRegex().Replace(content, "[REDACTED_EMAIL]");
        content = CreditCardRegex().Replace(content, "[REDACTED_CARD]");
        content = SsnRegex().Replace(content, "[REDACTED_SSN]");
        content = SpanishDniRegex().Replace(content, "[REDACTED_DNI]");

        if (content.Length != originalLength)
        {
            _logger.LogWarning("PII detected and redacted in LLM response for correlation {CorrelationId}",
                response.CorrelationId);

            var updated = response with { Content = content };
            return Task.FromResult(new OutputGuardrailDecision(true, UpdatedResponse: updated));
        }

        return Task.FromResult(new OutputGuardrailDecision(true, UpdatedResponse: response));
    }

    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b(?:\d[ \-]*?){13,16}\b", RegexOptions.Compiled)]
    private static partial Regex CreditCardRegex();

    [GeneratedRegex(@"\b\d{3}[\-\s]?\d{2}[\-\s]?\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex SsnRegex();

    [GeneratedRegex(@"\b\d{8}[A-Z]\b|\b[XYZ]\d{7}[A-Z]\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SpanishDniRegex();
}
