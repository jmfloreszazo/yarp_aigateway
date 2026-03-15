namespace Yarp.AiGateway.Core.Configuration;

public static class AiGatewayConfigValidator
{
    public static void ValidateAndThrow(AiGatewayConfiguration config)
    {
        if (config.Providers.Count == 0)
            throw new InvalidOperationException("At least one provider must be configured.");

        foreach (var (name, provider) in config.Providers)
        {
            if (string.IsNullOrWhiteSpace(provider.Endpoint))
                throw new InvalidOperationException($"Provider '{name}' must have an endpoint.");

            if (string.IsNullOrWhiteSpace(provider.Type))
                throw new InvalidOperationException($"Provider '{name}' must have a type.");

            if (string.IsNullOrWhiteSpace(provider.ApiKey))
                throw new InvalidOperationException($"Provider '{name}' must have an API key (use env: prefix for environment variables).");
        }

        if (string.IsNullOrWhiteSpace(config.Routing.DefaultProvider))
            throw new InvalidOperationException("Routing must specify a defaultProvider.");

        if (string.IsNullOrWhiteSpace(config.Routing.DefaultModel))
            throw new InvalidOperationException("Routing must specify a defaultModel.");

        if (!config.Providers.ContainsKey(config.Routing.DefaultProvider))
            throw new InvalidOperationException($"Default provider '{config.Routing.DefaultProvider}' not found in providers.");

        foreach (var rule in config.Routing.Rules)
        {
            if (!config.Providers.ContainsKey(rule.Provider))
                throw new InvalidOperationException($"Route rule '{rule.Name}' references unknown provider '{rule.Provider}'.");
        }

        foreach (var fb in config.Routing.Fallbacks)
        {
            if (!config.Providers.ContainsKey(fb.FallbackProvider))
                throw new InvalidOperationException($"Fallback rule references unknown provider '{fb.FallbackProvider}'.");
        }
    }
}
