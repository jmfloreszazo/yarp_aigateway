using Microsoft.Extensions.DependencyInjection;
using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Core.Configuration;
using Yarp.AiGateway.Providers.AzureOpenAI;
using Yarp.AiGateway.Providers.Mistral;
using Yarp.AiGateway.Providers.OpenAI;

namespace Yarp.AiGateway.Core.Extensions;

public static class ProviderRegistrationExtensions
{
    /// <summary>
    /// Registers provider HttpClients and IAiProvider implementations from configuration.
    /// </summary>
    public static IServiceCollection AddAiGatewayProviders(
        this IServiceCollection services,
        AiGatewayConfiguration config)
    {
        foreach (var (name, provider) in config.Providers)
        {
            if (!provider.Enabled) continue;

            switch (provider.Type.ToLowerInvariant())
            {
                case "openai":
                    services.AddHttpClient($"ai-provider-{name}",
                        client => client.BaseAddress = new Uri(provider.Endpoint));

                    services.AddSingleton<IAiProvider>(sp =>
                    {
                        var factory = sp.GetRequiredService<IHttpClientFactory>();
                        return new OpenAiProvider(factory.CreateClient($"ai-provider-{name}"), provider.ApiKey);
                    });
                    break;

                case "azure-openai":
                    services.AddHttpClient($"ai-provider-{name}",
                        client => client.BaseAddress = new Uri(provider.Endpoint));

                    services.AddSingleton<IAiProvider>(sp =>
                    {
                        var factory = sp.GetRequiredService<IHttpClientFactory>();
                        return new AzureOpenAiProvider(
                            factory.CreateClient($"ai-provider-{name}"),
                            provider.ApiKey,
                            provider.ApiVersion ?? "2025-01-01-preview",
                            provider.DeploymentMap);
                    });
                    break;

                case "mistral":
                    services.AddHttpClient($"ai-provider-{name}",
                        client => client.BaseAddress = new Uri(provider.Endpoint));

                    services.AddSingleton<IAiProvider>(sp =>
                    {
                        var factory = sp.GetRequiredService<IHttpClientFactory>();
                        return new MistralProvider(factory.CreateClient($"ai-provider-{name}"), provider.ApiKey);
                    });
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported provider type '{provider.Type}' for '{name}'.");
            }
        }

        return services;
    }
}
