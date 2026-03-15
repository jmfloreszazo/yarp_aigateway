using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Guardrails.Input;

/// <summary>
/// Blocks requests where the prompt exceeds a configurable maximum length.
/// </summary>
public sealed class MaxLengthGuardrail(int maxLength) : IInputGuardrail
{
    public int Order => 100;
    public string Name => "max-length";

    public Task<InputGuardrailDecision> EvaluateAsync(AiGatewayRequest request, CancellationToken ct)
    {
        if (request.Prompt.Length > maxLength)
            return Task.FromResult(new InputGuardrailDecision(false,
                $"Prompt exceeds maximum length of {maxLength} characters ({request.Prompt.Length})."));

        return Task.FromResult(new InputGuardrailDecision(true, UpdatedRequest: request));
    }
}
