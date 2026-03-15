using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Guardrails.Input;

/// <summary>
/// Blocks requests that contain known prompt injection or jailbreak patterns.
/// </summary>
public sealed class BlockedPatternsGuardrail(IEnumerable<string> patterns) : IInputGuardrail
{
    private readonly string[] _patterns = patterns.ToArray();

    public int Order => 200;
    public string Name => "blocked-patterns";

    public Task<InputGuardrailDecision> EvaluateAsync(AiGatewayRequest request, CancellationToken ct)
    {
        foreach (var pattern in _patterns)
        {
            if (request.Prompt.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new InputGuardrailDecision(false,
                    $"Prompt blocked: matches forbidden pattern."));
        }

        return Task.FromResult(new InputGuardrailDecision(true, UpdatedRequest: request));
    }
}
