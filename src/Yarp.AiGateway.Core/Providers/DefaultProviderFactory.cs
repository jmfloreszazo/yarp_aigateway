using Yarp.AiGateway.Abstractions;

namespace Yarp.AiGateway.Core.Providers;

public sealed class DefaultProviderFactory : IProviderFactory
{
    private readonly IEnumerable<IAiProvider> _providers;

    public DefaultProviderFactory(IEnumerable<IAiProvider> providers) => _providers = providers;

    public IAiProvider Create(string providerName)
    {
        return _providers.FirstOrDefault(p =>
                   p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"AI provider '{providerName}' is not registered.");
    }
}
