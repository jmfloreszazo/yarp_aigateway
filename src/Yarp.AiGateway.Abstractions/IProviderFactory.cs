namespace Yarp.AiGateway.Abstractions;

public interface IProviderFactory
{
    IAiProvider Create(string providerName);
}
