using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Guardrails.Input;

/// <summary>
/// Detects and redacts Personally Identifiable Information (PII) in prompts.
/// Supports emails, phone numbers, credit cards, SSN, IBAN, IP addresses,
/// Spanish DNI/NIE, passport numbers, and custom patterns.
/// Can operate in 'sanitize' mode (redact and pass) or 'block' mode (reject).
/// </summary>
public sealed partial class PiiDetectionGuardrail : IInputGuardrail
{
    private readonly PiiDetectionOptions _options;
    private readonly ILogger<PiiDetectionGuardrail> _logger;

    public int Order => 300;
    public string Name => "pii-detection";

    public PiiDetectionGuardrail(PiiDetectionOptions options, ILogger<PiiDetectionGuardrail> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<InputGuardrailDecision> EvaluateAsync(AiGatewayRequest request, CancellationToken ct)
    {
        var detections = new List<PiiDetection>();
        var sanitized = request.Prompt;

        if (_options.DetectEmails)
            sanitized = DetectAndRedact(sanitized, EmailRegex(), "EMAIL", "[REDACTED_EMAIL]", detections);

        if (_options.DetectPhoneNumbers)
            sanitized = DetectAndRedact(sanitized, PhoneRegex(), "PHONE", "[REDACTED_PHONE]", detections);

        if (_options.DetectCreditCards)
            sanitized = DetectAndRedact(sanitized, CreditCardRegex(), "CREDIT_CARD", "[REDACTED_CARD]", detections);

        if (_options.DetectSsn)
            sanitized = DetectAndRedact(sanitized, SsnRegex(), "SSN", "[REDACTED_SSN]", detections);

        if (_options.DetectIban)
            sanitized = DetectAndRedact(sanitized, IbanRegex(), "IBAN", "[REDACTED_IBAN]", detections);

        if (_options.DetectIpAddresses)
            sanitized = DetectAndRedact(sanitized, IpAddressRegex(), "IP_ADDRESS", "[REDACTED_IP]", detections);

        if (_options.DetectSpanishDni)
            sanitized = DetectAndRedact(sanitized, SpanishDniRegex(), "DNI_NIE", "[REDACTED_DNI]", detections);

        if (_options.DetectPassportNumbers)
            sanitized = DetectAndRedact(sanitized, PassportRegex(), "PASSPORT", "[REDACTED_PASSPORT]", detections);

        if (_options.DetectDatesOfBirth)
            sanitized = DetectAndRedact(sanitized, DateOfBirthRegex(), "DOB", "[REDACTED_DOB]", detections);

        // Custom patterns
        foreach (var custom in _options.CustomPatterns)
        {
            var regex = new Regex(custom.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled,
                TimeSpan.FromMilliseconds(200));
            sanitized = DetectAndRedact(sanitized, regex, custom.Name, custom.Replacement, detections);
        }

        if (detections.Count == 0)
            return Task.FromResult(new InputGuardrailDecision(true, UpdatedRequest: request));

        _logger.LogWarning("PII detected: {Types} ({Count} occurrences)",
            string.Join(", ", detections.Select(d => d.Type).Distinct()),
            detections.Count);

        if (_options.Mode == PiiMode.Block)
        {
            return Task.FromResult(new InputGuardrailDecision(false,
                $"PII detected in prompt: {string.Join(", ", detections.Select(d => d.Type).Distinct())}. " +
                "Remove personal information before sending."));
        }

        // Sanitize mode: redact and continue
        var updatedRequest = request with { Prompt = sanitized };
        return Task.FromResult(new InputGuardrailDecision(true, UpdatedRequest: updatedRequest));
    }

    private static string DetectAndRedact(
        string text,
        Regex regex,
        string type,
        string replacement,
        List<PiiDetection> detections)
    {
        var matches = regex.Matches(text);
        foreach (Match match in matches)
        {
            detections.Add(new PiiDetection(type, match.Index, match.Length));
        }
        return regex.Replace(text, replacement);
    }

    // --- Regex patterns ---

    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?<!\d)(?:\+?\d{1,3}[\s\-.]?)?\(?\d{2,4}\)?[\s\-.]?\d{3,4}[\s\-.]?\d{3,4}(?!\d)", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b(?:\d[ \-]*?){13,16}\b", RegexOptions.Compiled)]
    private static partial Regex CreditCardRegex();

    [GeneratedRegex(@"\b\d{3}[\-\s]?\d{2}[\-\s]?\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex SsnRegex();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}[\s\-]?[\dA-Z]{4}[\s\-]?[\dA-Z]{4}[\s\-]?[\dA-Z]{4}[\s\-]?[\dA-Z]{0,4}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex IbanRegex();

    [GeneratedRegex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled)]
    private static partial Regex IpAddressRegex();

    [GeneratedRegex(@"\b\d{8}[A-Z]\b|\b[XYZ]\d{7}[A-Z]\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SpanishDniRegex();

    [GeneratedRegex(@"\b[A-Z]{1,2}\d{6,9}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PassportRegex();

    [GeneratedRegex(@"\b(?:0[1-9]|[12]\d|3[01])[/\-.](?:0[1-9]|1[0-2])[/\-.](?:19|20)\d{2}\b", RegexOptions.Compiled)]
    private static partial Regex DateOfBirthRegex();
}

public sealed record PiiDetection(string Type, int Position, int Length);

public sealed class PiiDetectionOptions
{
    public PiiMode Mode { get; init; } = PiiMode.Sanitize;
    public bool DetectEmails { get; init; } = true;
    public bool DetectPhoneNumbers { get; init; } = true;
    public bool DetectCreditCards { get; init; } = true;
    public bool DetectSsn { get; init; } = true;
    public bool DetectIban { get; init; } = true;
    public bool DetectIpAddresses { get; init; }
    public bool DetectSpanishDni { get; init; } = true;
    public bool DetectPassportNumbers { get; init; }
    public bool DetectDatesOfBirth { get; init; }
    public List<CustomPiiPattern> CustomPatterns { get; init; } = [];
}

public sealed class CustomPiiPattern
{
    public string Name { get; init; } = string.Empty;
    public string Pattern { get; init; } = string.Empty;
    public string Replacement { get; init; } = "[REDACTED]";
}

public enum PiiMode
{
    Sanitize,
    Block
}
