using Yarp.AiGateway.Core.Configuration;

namespace Yarp.AiGateway.Core.Tests;

public class AiGatewayConfigLoaderTests
{
    [Fact]
    public void Load_valid_config_file()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
            {
                "gateway": { "timeoutSeconds": 30 },
                "providers": {
                    "openai": {
                        "type": "openai",
                        "endpoint": "https://api.openai.com",
                        "apiKey": "sk-test"
                    }
                },
                "routing": {
                    "rules": [
                        {
                            "name": "default",
                            "provider": "openai",
                            "model": "gpt-4o",
                            "conditions": []
                        }
                    ]
                },
                "guardrails": { "input": [], "output": [] }
            }
            """);

            var config = AiGatewayConfigLoader.Load(tempFile);

            Assert.Equal(30, config.Gateway.TimeoutSeconds);
            Assert.Single(config.Providers);
            Assert.Equal("openai", config.Providers["openai"].Type);
            Assert.Single(config.Routing.Rules);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_resolves_environment_variables()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            Environment.SetEnvironmentVariable("TEST_API_KEY_YARP", "my-secret-key");

            File.WriteAllText(tempFile, """
            {
                "providers": {
                    "openai": {
                        "type": "openai",
                        "endpoint": "https://api.openai.com",
                        "apiKey": "env:TEST_API_KEY_YARP"
                    }
                }
            }
            """);

            var config = AiGatewayConfigLoader.Load(tempFile);
            Assert.Equal("my-secret-key", config.Providers["openai"].ApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_API_KEY_YARP", null);
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_throws_on_missing_file()
    {
        Assert.Throws<FileNotFoundException>(() =>
            AiGatewayConfigLoader.Load("/nonexistent/config.json"));
    }

    [Fact]
    public void Load_parses_guardrail_definitions()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
            {
                "guardrails": {
                    "input": [
                        {
                            "type": "pii",
                            "mode": "sanitize",
                            "parameters": { "detectEmails": true }
                        },
                        {
                            "type": "semantic",
                            "mode": "block",
                            "parameters": {
                                "thresholds": { "violence": 0.7 }
                            }
                        }
                    ],
                    "output": [
                        {
                            "type": "blocked-patterns",
                            "mode": "block",
                            "parameters": {
                                "patterns": ["stack trace:"]
                            }
                        }
                    ]
                }
            }
            """);

            var config = AiGatewayConfigLoader.Load(tempFile);
            Assert.Equal(2, config.Guardrails.Input.Count);
            Assert.Single(config.Guardrails.Output);
            Assert.Equal("pii", config.Guardrails.Input[0].Type);
            Assert.Equal("sanitize", config.Guardrails.Input[0].Mode);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_parses_routing_with_conditions()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
            {
                "routing": {
                    "rules": [
                        {
                            "name": "long-prompts",
                            "provider": "azure-openai",
                            "model": "gpt-4o",
                            "conditions": [
                                {
                                    "field": "prompt.length",
                                    "operator": "gt",
                                    "value": 8000
                                }
                            ]
                        }
                    ],
                    "fallbacks": [
                        {
                            "forProvider": "openai",
                            "fallbackProvider": "azure-openai",
                            "fallbackModel": "gpt-4o"
                        }
                    ]
                }
            }
            """);

            var config = AiGatewayConfigLoader.Load(tempFile);
            Assert.Single(config.Routing.Rules);
            Assert.Equal("prompt.length", config.Routing.Rules[0].Conditions[0].Field);
            Assert.Equal("gt", config.Routing.Rules[0].Conditions[0].Operator);
            Assert.Single(config.Routing.Fallbacks);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
