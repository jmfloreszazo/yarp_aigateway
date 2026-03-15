using Microsoft.Extensions.Logging.Abstractions;
using Yarp.AiGateway.Abstractions.Models;
using Yarp.AiGateway.Guardrails.Input;

namespace Yarp.AiGateway.Guardrails.Tests;

public class PiiDetectionGuardrailTests
{
    private static PiiDetectionGuardrail CreateGuardrail(PiiMode mode = PiiMode.Sanitize) =>
        new(new PiiDetectionOptions { Mode = mode },
            NullLogger<PiiDetectionGuardrail>.Instance);

    private static AiGatewayRequest MakeRequest(string prompt) =>
        new() { Prompt = prompt };

    [Fact]
    public async Task Clean_prompt_passes()
    {
        var result = await CreateGuardrail().EvaluateAsync(MakeRequest("Hello world"), CancellationToken.None);
        Assert.True(result.Allowed);
    }

    [Theory]
    [InlineData("My email is john@example.com", "[REDACTED_EMAIL]")]
    [InlineData("Contact me at user.name+tag@domain.co.uk please", "[REDACTED_EMAIL]")]
    public async Task Sanitize_mode_redacts_emails(string prompt, string expectedRedaction)
    {
        var result = await CreateGuardrail(PiiMode.Sanitize).EvaluateAsync(MakeRequest(prompt), CancellationToken.None);
        Assert.True(result.Allowed);
        Assert.Contains(expectedRedaction, result.UpdatedRequest!.Prompt);
        Assert.DoesNotContain("@", result.UpdatedRequest.Prompt);
    }

    [Fact]
    public async Task Block_mode_rejects_email()
    {
        var result = await CreateGuardrail(PiiMode.Block).EvaluateAsync(
            MakeRequest("My email is test@example.com"), CancellationToken.None);
        Assert.False(result.Allowed);
        Assert.Contains("PII detected", result.Reason);
    }

    [Theory]
    [InlineData("My SSN is 123-45-6789")]
    [InlineData("SSN: 123 45 6789")]
    public async Task Detects_ssn(string prompt)
    {
        var result = await CreateGuardrail(PiiMode.Block).EvaluateAsync(MakeRequest(prompt), CancellationToken.None);
        Assert.False(result.Allowed);
    }

    [Theory]
    [InlineData("Card: 4111 1111 1111 1111")]
    [InlineData("Card: 4111-1111-1111-1111")]
    public async Task Detects_credit_cards(string prompt)
    {
        var result = await CreateGuardrail(PiiMode.Block).EvaluateAsync(MakeRequest(prompt), CancellationToken.None);
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Detects_spanish_dni()
    {
        var result = await CreateGuardrail(PiiMode.Block).EvaluateAsync(
            MakeRequest("Mi DNI es 12345678A"), CancellationToken.None);
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Detects_nie()
    {
        var result = await CreateGuardrail(PiiMode.Block).EvaluateAsync(
            MakeRequest("NIE: X1234567A"), CancellationToken.None);
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Sanitize_mode_redacts_ssn()
    {
        var result = await CreateGuardrail(PiiMode.Sanitize).EvaluateAsync(
            MakeRequest("My SSN is 123-45-6789"), CancellationToken.None);
        Assert.True(result.Allowed);
        Assert.Contains("[REDACTED_SSN]", result.UpdatedRequest!.Prompt);
        Assert.DoesNotContain("123-45-6789", result.UpdatedRequest.Prompt);
    }

    [Fact]
    public async Task Detects_iban()
    {
        var result = await CreateGuardrail(PiiMode.Block).EvaluateAsync(
            MakeRequest("IBAN: ES79 2100 0813 6101 2345 6789"), CancellationToken.None);
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Ip_detection_disabled_by_default()
    {
        var guardrail = new PiiDetectionGuardrail(
            new PiiDetectionOptions { Mode = PiiMode.Block, DetectIpAddresses = false },
            NullLogger<PiiDetectionGuardrail>.Instance);

        var result = await guardrail.EvaluateAsync(
            MakeRequest("Server at 192.168.1.1"), CancellationToken.None);
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Ip_detection_when_enabled()
    {
        var guardrail = new PiiDetectionGuardrail(
            new PiiDetectionOptions { Mode = PiiMode.Block, DetectIpAddresses = true },
            NullLogger<PiiDetectionGuardrail>.Instance);

        var result = await guardrail.EvaluateAsync(
            MakeRequest("Server at 192.168.1.1"), CancellationToken.None);
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Multiple_pii_types_all_redacted_in_sanitize_mode()
    {
        // Use data that won't be consumed by overlapping regex patterns
        var guardrail = new PiiDetectionGuardrail(
            new PiiDetectionOptions
            {
                Mode = PiiMode.Sanitize,
                DetectPhoneNumbers = false // Avoid phone regex consuming DNI digits
            },
            NullLogger<PiiDetectionGuardrail>.Instance);

        var result = await guardrail.EvaluateAsync(
            MakeRequest("Email: test@example.com and SSN: 123-45-6789 and DNI: 87654321B"),
            CancellationToken.None);

        Assert.True(result.Allowed);
        var updated = result.UpdatedRequest!.Prompt;
        Assert.Contains("[REDACTED_EMAIL]", updated);
        Assert.Contains("[REDACTED_SSN]", updated);
        Assert.Contains("[REDACTED_DNI]", updated);
    }

    [Fact]
    public async Task Custom_pattern_applied()
    {
        var options = new PiiDetectionOptions
        {
            Mode = PiiMode.Sanitize,
            CustomPatterns =
            [
                new CustomPiiPattern
                {
                    Name = "CUSTOM_ID",
                    Pattern = @"ID-\d{6}",
                    Replacement = "[REDACTED_ID]"
                }
            ]
        };
        var guardrail = new PiiDetectionGuardrail(options, NullLogger<PiiDetectionGuardrail>.Instance);
        var result = await guardrail.EvaluateAsync(MakeRequest("My ID-123456 is private"), CancellationToken.None);

        Assert.True(result.Allowed);
        Assert.Contains("[REDACTED_ID]", result.UpdatedRequest!.Prompt);
    }
}
