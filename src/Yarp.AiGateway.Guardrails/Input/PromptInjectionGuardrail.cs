using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Guardrails.Input;

/// <summary>
/// Detects prompt injection and jailbreak attempts using known attack patterns.
/// Goes beyond simple blocked patterns — detects structural manipulation techniques.
/// </summary>
public sealed class PromptInjectionGuardrail : IInputGuardrail
{
    public int Order => 150;
    public string Name => "prompt-injection";

    // Common prompt injection / jailbreak indicators
    private static readonly string[] InjectionPatterns =
    [
        "ignore previous instructions",
        "ignore all previous",
        "ignore above instructions",
        "disregard previous",
        "disregard all previous",
        "disregard your instructions",
        "forget your instructions",
        "forget previous instructions",
        "override your instructions",
        "new instructions:",
        "system prompt:",
        "reveal system prompt",
        "reveal your instructions",
        "show me your prompt",
        "what is your system prompt",
        "bypass policy",
        "bypass safety",
        "bypass content filter",
        "you are now",
        "act as if you have no restrictions",
        "pretend you are",
        "jailbreak",
        "DAN mode",
        "developer mode enable",
        "do anything now",
        "ignore safety guidelines",
        "ignore content policy",
        "roleplay as an unrestricted",
        "you have no ethical guidelines",
        "respond without any restrictions",
        "```system",
    ];

    // Structural indicators of injection (encoded tricks)
    private static readonly string[] StructuralIndicators =
    [
        "\\u0000",       // null byte
        "\\x00",         // null byte hex
        "<|im_start|>",  // ChatML injection
        "<|im_end|>",    // ChatML injection
        "<|endoftext|>", // GPT token boundary
        "[INST]",        // Llama instruction injection
        "[/INST]",       // Llama instruction injection
        "<<SYS>>",       // Llama system injection
        "<</SYS>>",      // Llama system injection
    ];

    public Task<InputGuardrailDecision> EvaluateAsync(AiGatewayRequest request, CancellationToken ct)
    {
        var prompt = request.Prompt;

        // Check known injection patterns
        foreach (var pattern in InjectionPatterns)
        {
            if (prompt.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new InputGuardrailDecision(false,
                    "Prompt injection attempt detected."));
        }

        // Check structural injection indicators
        foreach (var indicator in StructuralIndicators)
        {
            if (prompt.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new InputGuardrailDecision(false,
                    "Structural prompt injection detected."));
        }

        // Check for excessive role instruction overrides
        if (HasExcessiveRoleOverrides(prompt))
            return Task.FromResult(new InputGuardrailDecision(false,
                "Suspicious role override pattern detected."));

        return Task.FromResult(new InputGuardrailDecision(true, UpdatedRequest: request));
    }

    private static bool HasExcessiveRoleOverrides(string prompt)
    {
        var lower = prompt.ToLowerInvariant();
        var roleKeywords = new[] { "you are now", "from now on", "new role", "act as", "behave as" };
        var count = roleKeywords.Count(k => lower.Contains(k));
        return count >= 2; // Two or more role override attempts = suspicious
    }
}
