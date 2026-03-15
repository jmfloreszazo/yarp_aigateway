using Microsoft.Extensions.Logging;
using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Abstractions.Models;

namespace Yarp.AiGateway.Guardrails.Input;

/// <summary>
/// Semantic guardrail that classifies prompt intent across risk categories.
/// Uses a weighted scoring system with configurable thresholds per category.
/// Categories: violence, self-harm, sexual, hate, illegal, data-exfiltration, manipulation.
/// </summary>
public sealed class SemanticGuardrail : IInputGuardrail
{
    private readonly SemanticGuardrailOptions _options;
    private readonly ILogger<SemanticGuardrail> _logger;

    public int Order => 250;
    public string Name => "semantic";

    public SemanticGuardrail(SemanticGuardrailOptions options, ILogger<SemanticGuardrail> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<InputGuardrailDecision> EvaluateAsync(AiGatewayRequest request, CancellationToken ct)
    {
        var prompt = request.Prompt;
        var scores = new Dictionary<string, double>();
        var flags = new List<string>();

        foreach (var category in AllCategories)
        {
            var score = ScoreCategory(prompt, category);
            if (score > 0)
                scores[category.Name] = score;

            var threshold = _options.GetThreshold(category.Name);
            if (score >= threshold)
                flags.Add($"{category.Name}:{score:F2}");
        }

        if (flags.Count > 0)
        {
            _logger.LogWarning("Semantic guardrail triggered: {Flags}", string.Join(", ", flags));

            if (_options.Mode == SemanticMode.Block)
            {
                return Task.FromResult(new InputGuardrailDecision(false,
                    $"Content policy violation detected: {string.Join(", ", flags.Select(f => f.Split(':')[0]))}"));
            }

            // Flag mode — annotate but allow
            var metadata = new Dictionary<string, object?>(request.Metadata)
            {
                ["semantic_flags"] = flags,
                ["semantic_scores"] = scores
            };
            var updated = request with { Metadata = metadata };
            return Task.FromResult(new InputGuardrailDecision(true, UpdatedRequest: updated));
        }

        return Task.FromResult(new InputGuardrailDecision(true, UpdatedRequest: request));
    }

    private static double ScoreCategory(string prompt, SemanticCategory category)
    {
        var lower = prompt.ToLowerInvariant();
        double totalScore = 0;
        int hits = 0;

        foreach (var (keyword, weight) in category.Keywords)
        {
            if (lower.Contains(keyword))
            {
                totalScore += weight;
                hits++;
            }
        }

        // Bonus for clustering — multiple indicators increase confidence
        if (hits >= 3) totalScore *= 1.5;
        if (hits >= 5) totalScore *= 2.0;

        return Math.Min(totalScore, 1.0);
    }

    // --- Semantic categories with weighted keywords ---

    private static readonly SemanticCategory[] AllCategories =
    [
        new("violence",
        [
            ("kill", 0.4), ("murder", 0.5), ("attack", 0.2), ("weapon", 0.3),
            ("bomb", 0.5), ("shoot", 0.4), ("stab", 0.4), ("assault", 0.3),
            ("hurt someone", 0.4), ("make a weapon", 0.5), ("how to harm", 0.5),
            ("destroy", 0.15), ("explosive", 0.5), ("terroris", 0.5),
        ]),

        new("self-harm",
        [
            ("kill myself", 0.6), ("suicide", 0.5), ("self harm", 0.5),
            ("end my life", 0.6), ("hurt myself", 0.5), ("cut myself", 0.5),
            ("want to die", 0.5), ("no reason to live", 0.5),
        ]),

        new("sexual",
        [
            ("explicit sexual", 0.5), ("pornograph", 0.5), ("nsfw", 0.3),
            ("sexual content", 0.4), ("erotic", 0.3), ("nude", 0.2),
            ("underage", 0.8), ("minor", 0.15),
        ]),

        new("hate",
        [
            ("racial slur", 0.5), ("hate speech", 0.5), ("discriminat", 0.2),
            ("supremac", 0.5), ("inferior race", 0.6), ("ethnic cleansing", 0.7),
            ("genocide", 0.5), ("nazi", 0.3), ("xenophob", 0.3),
        ]),

        new("illegal",
        [
            ("how to hack", 0.4), ("how to steal", 0.4), ("make drugs", 0.5),
            ("phishing", 0.4), ("ransomware", 0.4), ("malware", 0.3),
            ("counterfeit", 0.4), ("money laundering", 0.5), ("fraud", 0.2),
            ("illegal", 0.1), ("exploit vulnerability", 0.4),
        ]),

        new("data-exfiltration",
        [
            ("give me all data", 0.3), ("dump database", 0.5), ("export all records", 0.3),
            ("show all users", 0.2), ("list all passwords", 0.6), ("api keys", 0.3),
            ("connection string", 0.3), ("secret key", 0.3), ("private key", 0.3),
            ("access token", 0.2), ("credentials", 0.2),
        ]),

        new("manipulation",
        [
            ("pretend you are", 0.2), ("act as if", 0.15), ("roleplay as", 0.15),
            ("you are now", 0.2), ("from now on you", 0.25), ("new persona", 0.3),
            ("hypothetically", 0.1), ("in a fictional world", 0.15),
            ("if you were evil", 0.3), ("no ethical constraints", 0.5),
            ("without restrictions", 0.4), ("unfiltered", 0.25),
        ]),
    ];
}

public sealed record SemanticCategory(string Name, (string Keyword, double Weight)[] Keywords);

public sealed class SemanticGuardrailOptions
{
    public SemanticMode Mode { get; init; } = SemanticMode.Block;

    /// <summary>
    /// Threshold per category (0.0 to 1.0). Default threshold applies if not specified.
    /// </summary>
    public Dictionary<string, double> CategoryThresholds { get; init; } = new();

    /// <summary>
    /// Default threshold when a category-specific one is not set.
    /// </summary>
    public double DefaultThreshold { get; init; } = 0.4;

    public double GetThreshold(string category) =>
        CategoryThresholds.TryGetValue(category, out var threshold)
            ? threshold
            : DefaultThreshold;
}

public enum SemanticMode
{
    Block,
    Flag // Annotate metadata but allow through
}
