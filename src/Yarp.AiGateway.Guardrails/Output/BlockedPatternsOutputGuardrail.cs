using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Guardrails.Output;

/// <summary>
/// Blocks LLM responses that contain forbidden patterns (data leakage, system prompt leaks, etc.).
/// </summary>
public sealed class BlockedPatternsOutputGuardrail(IEnumerable<string> patterns) : IOutputGuardrail
{
    private readonly string[] _patterns = patterns.ToArray();

    public int Order => 100;
    public string Name => "output-blocked-patterns";

    public Task<OutputGuardrailDecision> EvaluateAsync(AiGatewayResponse response, CancellationToken ct)
    {
        foreach (var pattern in _patterns)
        {
            if (response.Content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new OutputGuardrailDecision(false,
                    "Response blocked: contains forbidden content."));
        }

        return Task.FromResult(new OutputGuardrailDecision(true, UpdatedResponse: response));
    }
}
