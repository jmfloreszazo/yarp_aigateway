using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Yarp.AiGateway.Abstractions;
using Yarp.AiGateway.Core.Configuration;
using Yarp.AiGateway.Core.Middleware;
using Yarp.AiGateway.Core.Pipeline;
using Yarp.AiGateway.Core.Providers;
using Yarp.AiGateway.Core.Quotas;
using Yarp.AiGateway.Core.Routing;
using Yarp.AiGateway.Core.Telemetry;

namespace Yarp.AiGateway.Core.Extensions;

public static class AiGatewayServiceCollectionExtensions
{
    /// <summary>
    /// Registers all AI Gateway services from a JSON configuration file.
    /// </summary>
    public static IServiceCollection AddAiGatewayFromJson(
        this IServiceCollection services,
        string configPath)
    {
        var configuration = AiGatewayConfigLoader.Load(configPath);
        AiGatewayConfigValidator.ValidateAndThrow(configuration);
        return services.AddAiGateway(configuration);
    }

    /// <summary>
    /// Registers all AI Gateway services using a pre-built configuration object.
    /// </summary>
    public static IServiceCollection AddAiGateway(
        this IServiceCollection services,
        AiGatewayConfiguration configuration)
    {
        services.AddSingleton(configuration);

        // Core pipeline
        services.AddSingleton<IAiGatewayPipeline, AiGatewayPipeline>();
        services.AddSingleton<IModelRouter, DefaultModelRouter>();
        services.AddSingleton<IPolicyEvaluator, DefaultPolicyEvaluator>();
        services.AddSingleton<IProviderFactory, DefaultProviderFactory>();
        services.AddSingleton<IQuotaManager, InMemoryQuotaManager>();
        services.AddSingleton<IAuditSink, DefaultAuditSink>();

        // Guardrails from config
        services.AddAiGatewayGuardrails(configuration);

        // Providers from config
        services.AddAiGatewayProviders(configuration);

        return services;
    }
}

public static class AiGatewayApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the AI Gateway middleware to the HTTP pipeline.
    /// </summary>
    public static IApplicationBuilder UseAiGateway(this IApplicationBuilder app)
    {
        app.UseMiddleware<AiGatewayMiddleware>();
        return app;
    }
}
