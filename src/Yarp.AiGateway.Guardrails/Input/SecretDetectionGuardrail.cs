using System.Text.RegularExpressions;
using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Guardrails.Input;

/// <summary>
/// Detects secrets, tokens, and connection strings in prompts and blocks them.
/// </summary>
public sealed partial class SecretDetectionGuardrail : IInputGuardrail
{
    public int Order => 350;
    public string Name => "secret-detection";

    public Task<InputGuardrailDecision> EvaluateAsync(AiGatewayRequest request, CancellationToken ct)
    {
        var prompt = request.Prompt;

        if (BearerTokenRegex().IsMatch(prompt))
            return Blocked("Bearer token detected in prompt.");

        if (ConnectionStringRegex().IsMatch(prompt))
            return Blocked("Connection string detected in prompt.");

        if (AwsKeyRegex().IsMatch(prompt))
            return Blocked("AWS access key detected in prompt.");

        if (AzureStorageKeyRegex().IsMatch(prompt))
            return Blocked("Azure storage key detected in prompt.");

        if (GitHubTokenRegex().IsMatch(prompt))
            return Blocked("GitHub token detected in prompt.");

        if (PrivateKeyRegex().IsMatch(prompt))
            return Blocked("Private key detected in prompt.");

        if (JwtRegex().IsMatch(prompt))
            return Blocked("JWT token detected in prompt.");

        return Task.FromResult(new InputGuardrailDecision(true, UpdatedRequest: request));
    }

    private static Task<InputGuardrailDecision> Blocked(string reason) =>
        Task.FromResult(new InputGuardrailDecision(false, reason));

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.Compiled)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(?:Server|Data Source|Host)=[^;]+;.*(?:Password|Pwd)=[^;]+", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringRegex();

    [GeneratedRegex(@"\bAKIA[0-9A-Z]{16}\b", RegexOptions.Compiled)]
    private static partial Regex AwsKeyRegex();

    [GeneratedRegex(@"DefaultEndpointsProtocol=https?;AccountName=", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex AzureStorageKeyRegex();

    [GeneratedRegex(@"\b(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9_]{36,255}\b", RegexOptions.Compiled)]
    private static partial Regex GitHubTokenRegex();

    [GeneratedRegex(@"-----BEGIN (?:RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----", RegexOptions.Compiled)]
    private static partial Regex PrivateKeyRegex();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9\-_]+\.eyJ[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\b", RegexOptions.Compiled)]
    private static partial Regex JwtRegex();
}
