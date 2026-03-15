using System.Text.Json;
using Yarp.AiGateway.Abstractions.Models;
using Yarp.AiGateway.Core.Configuration;
using Yarp.AiGateway.Core.Routing;

namespace Yarp.AiGateway.Core.Tests;

public class ConditionEvaluatorTests
{
    private static readonly PolicyContext EmptyPolicy = new();

    private static AiGatewayRequest MakeRequest(
        string prompt = "test",
        string? tenantId = null,
        string? userId = null,
        Dictionary<string, object?>? metadata = null) =>
        new()
        {
            Prompt = prompt,
            TenantId = tenantId,
            UserId = userId,
            Metadata = metadata ?? new()
        };

    private static ConditionSection MakeCondition(string field, string op, object value)
    {
        var jsonValue = JsonSerializer.SerializeToElement(value);
        return new ConditionSection { Field = field, Operator = op, Value = jsonValue };
    }

    [Fact]
    public void Empty_conditions_always_match()
    {
        Assert.True(ConditionEvaluator.Matches([], MakeRequest(), EmptyPolicy));
    }

    [Fact]
    public void Eq_operator_matches()
    {
        var conditions = new[] { MakeCondition("tenantId", "eq", "acme") };
        Assert.True(ConditionEvaluator.Matches(conditions, MakeRequest(tenantId: "acme"), EmptyPolicy));
    }

    [Fact]
    public void Eq_operator_case_insensitive()
    {
        var conditions = new[] { MakeCondition("tenantId", "eq", "ACME") };
        Assert.True(ConditionEvaluator.Matches(conditions, MakeRequest(tenantId: "acme"), EmptyPolicy));
    }

    [Fact]
    public void Eq_operator_fails_on_mismatch()
    {
        var conditions = new[] { MakeCondition("tenantId", "eq", "other") };
        Assert.False(ConditionEvaluator.Matches(conditions, MakeRequest(tenantId: "acme"), EmptyPolicy));
    }

    [Fact]
    public void Neq_operator()
    {
        var conditions = new[] { MakeCondition("tenantId", "neq", "other") };
        Assert.True(ConditionEvaluator.Matches(conditions, MakeRequest(tenantId: "acme"), EmptyPolicy));
    }

    [Fact]
    public void Gt_operator_on_prompt_length()
    {
        var conditions = new[] { MakeCondition("prompt.length", "gt", 3) };
        Assert.True(ConditionEvaluator.Matches(conditions, MakeRequest(prompt: "Hello"), EmptyPolicy));
    }

    [Fact]
    public void Gt_operator_fails()
    {
        var conditions = new[] { MakeCondition("prompt.length", "gt", 100) };
        Assert.False(ConditionEvaluator.Matches(conditions, MakeRequest(prompt: "Hello"), EmptyPolicy));
    }

    [Fact]
    public void Gte_operator()
    {
        var conditions = new[] { MakeCondition("prompt.length", "gte", 5) };
        Assert.True(ConditionEvaluator.Matches(conditions, MakeRequest(prompt: "Hello"), EmptyPolicy));
    }

    [Fact]
    public void Lt_operator()
    {
        var conditions = new[] { MakeCondition("prompt.length", "lt", 100) };
        Assert.True(ConditionEvaluator.Matches(conditions, MakeRequest(prompt: "Hello"), EmptyPolicy));
    }

    [Fact]
    public void Contains_operator()
    {
        var conditions = new[] { MakeCondition("tenantId", "contains", "cm") };
        Assert.True(ConditionEvaluator.Matches(conditions, MakeRequest(tenantId: "acme"), EmptyPolicy));
    }

    [Fact]
    public void Startswith_operator()
    {
        var conditions = new[] { MakeCondition("tenantId", "startswith", "ac") };
        Assert.True(ConditionEvaluator.Matches(conditions, MakeRequest(tenantId: "acme"), EmptyPolicy));
    }

    [Fact]
    public void Exists_operator_true()
    {
        var conditions = new[] { MakeCondition("tenantId", "exists", true) };
        Assert.True(ConditionEvaluator.Matches(conditions, MakeRequest(tenantId: "acme"), EmptyPolicy));
    }

    [Fact]
    public void Exists_operator_false_when_null()
    {
        var conditions = new[] { MakeCondition("tenantId", "exists", true) };
        Assert.False(ConditionEvaluator.Matches(conditions, MakeRequest(tenantId: null), EmptyPolicy));
    }

    [Fact]
    public void Metadata_field_resolved()
    {
        var metadata = new Dictionary<string, object?> { ["priority"] = "high" };
        var conditions = new[] { MakeCondition("metadata.priority", "eq", "high") };
        Assert.True(ConditionEvaluator.Matches(conditions, MakeRequest(metadata: metadata), EmptyPolicy));
    }

    [Fact]
    public void Multiple_conditions_all_must_match()
    {
        var conditions = new[]
        {
            MakeCondition("tenantId", "eq", "acme"),
            MakeCondition("prompt.length", "gt", 2)
        };
        Assert.True(ConditionEvaluator.Matches(conditions, MakeRequest(prompt: "Hello", tenantId: "acme"), EmptyPolicy));
    }

    [Fact]
    public void Multiple_conditions_one_fails()
    {
        var conditions = new[]
        {
            MakeCondition("tenantId", "eq", "acme"),
            MakeCondition("prompt.length", "gt", 100)
        };
        Assert.False(ConditionEvaluator.Matches(conditions, MakeRequest(prompt: "Hello", tenantId: "acme"), EmptyPolicy));
    }

    [Fact]
    public void Unknown_operator_returns_false()
    {
        var conditions = new[] { MakeCondition("tenantId", "match_regex", ".*") };
        Assert.False(ConditionEvaluator.Matches(conditions, MakeRequest(tenantId: "acme"), EmptyPolicy));
    }

    [Fact]
    public void Unknown_field_returns_null()
    {
        var conditions = new[] { MakeCondition("nonexistent", "exists", true) };
        Assert.False(ConditionEvaluator.Matches(conditions, MakeRequest(), EmptyPolicy));
    }
}
