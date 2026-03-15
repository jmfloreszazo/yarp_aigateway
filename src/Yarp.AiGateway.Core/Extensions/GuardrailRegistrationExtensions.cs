using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Core.Configuration;
using Yarp.AiGateway.Guardrails.Input;
using Yarp.AiGateway.Guardrails.Output;

namespace Yarp.AiGateway.Core.Extensions;

public static class GuardrailRegistrationExtensions
{
    /// <summary>
    /// Registers all guardrails defined in the AI Gateway configuration.
    /// </summary>
    public static IServiceCollection AddAiGatewayGuardrails(
        this IServiceCollection services,
        AiGatewayConfiguration config)
    {
        // Always register prompt injection detection
        services.AddSingleton<IInputGuardrail, PromptInjectionGuardrail>();

        foreach (var definition in config.Guardrails.Input)
        {
            switch (definition.Type.ToLowerInvariant())
            {
                case "max-length":
                    var maxLen = GetInt(definition, "maxLength", 20000);
                    services.AddSingleton<IInputGuardrail>(new MaxLengthGuardrail(maxLen));
                    break;

                case "blocked-patterns":
                    var patterns = GetStringArray(definition, "patterns");
                    services.AddSingleton<IInputGuardrail>(new BlockedPatternsGuardrail(patterns));
                    break;

                case "pii":
                case "pii-detection":
                case "pii-redaction":
                    var piiOptions = BuildPiiOptions(definition);
                    services.AddSingleton(piiOptions);
                    services.AddSingleton<IInputGuardrail, PiiDetectionGuardrail>();
                    break;

                case "secret-detection":
                    services.AddSingleton<IInputGuardrail, SecretDetectionGuardrail>();
                    break;

                case "semantic":
                    var semanticOptions = BuildSemanticOptions(definition);
                    services.AddSingleton(semanticOptions);
                    services.AddSingleton<IInputGuardrail, SemanticGuardrail>();
                    break;
            }
        }

        foreach (var definition in config.Guardrails.Output)
        {
            switch (definition.Type.ToLowerInvariant())
            {
                case "blocked-patterns":
                    var patterns = GetStringArray(definition, "patterns");
                    services.AddSingleton<IOutputGuardrail>(new BlockedPatternsOutputGuardrail(patterns));
                    break;

                case "pii":
                case "pii-redaction":
                    services.AddSingleton<IOutputGuardrail, PiiOutputGuardrail>();
                    break;
            }
        }

        return services;
    }

    private static PiiDetectionOptions BuildPiiOptions(GuardrailDefinition def)
    {
        return new PiiDetectionOptions
        {
            Mode = def.Mode.Equals("block", StringComparison.OrdinalIgnoreCase)
                ? PiiMode.Block
                : PiiMode.Sanitize,
            DetectEmails = GetBool(def, "detectEmails", true),
            DetectPhoneNumbers = GetBool(def, "detectPhones", true),
            DetectCreditCards = GetBool(def, "detectCreditCards", true),
            DetectSsn = GetBool(def, "detectSsn", true),
            DetectIban = GetBool(def, "detectIban", true),
            DetectIpAddresses = GetBool(def, "detectIpAddresses", false),
            DetectSpanishDni = GetBool(def, "detectSpanishDni", true),
            DetectPassportNumbers = GetBool(def, "detectPassports", false),
            DetectDatesOfBirth = GetBool(def, "detectDatesOfBirth", false),
            DetectMedicalRecordNumbers = GetBool(def, "detectMedicalRecordNumbers", false),
            DetectPatientNames = GetBool(def, "detectPatientNames", false),
        };
    }

    private static SemanticGuardrailOptions BuildSemanticOptions(GuardrailDefinition def)
    {
        var options = new SemanticGuardrailOptions
        {
            Mode = def.Mode.Equals("flag", StringComparison.OrdinalIgnoreCase)
                ? SemanticMode.Flag
                : SemanticMode.Block,
            DefaultThreshold = GetDouble(def, "defaultThreshold", 0.4),
        };

        if ((def.Parameters.TryGetValue("thresholds", out var thresholdsElem) ||
             def.Parameters.TryGetValue("categoryThresholds", out thresholdsElem)) &&
            thresholdsElem.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in thresholdsElem.EnumerateObject())
            {
                if (prop.Value.TryGetDouble(out var val))
                    options.CategoryThresholds[prop.Name] = val;
            }
        }

        return options;
    }

    // --- Helpers ---

    private static int GetInt(GuardrailDefinition def, string key, int defaultValue) =>
        def.Parameters.TryGetValue(key, out var elem) && elem.TryGetInt32(out var val) ? val : defaultValue;

    private static double GetDouble(GuardrailDefinition def, string key, double defaultValue) =>
        def.Parameters.TryGetValue(key, out var elem) && elem.TryGetDouble(out var val) ? val : defaultValue;

    private static bool GetBool(GuardrailDefinition def, string key, bool defaultValue) =>
        def.Parameters.TryGetValue(key, out var elem) && elem.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? elem.GetBoolean()
            : defaultValue;

    private static string GetString(GuardrailDefinition def, string key, string defaultValue) =>
        def.Parameters.TryGetValue(key, out var elem) && elem.ValueKind == JsonValueKind.String
            ? elem.GetString() ?? defaultValue
            : defaultValue;

    private static string[] GetStringArray(GuardrailDefinition def, string key) =>
        def.Parameters.TryGetValue(key, out var elem) && elem.ValueKind == JsonValueKind.Array
            ? elem.EnumerateArray().Select(e => e.GetString()!).ToArray()
            : [];
}
