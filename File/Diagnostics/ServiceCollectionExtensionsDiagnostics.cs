using SharedKernel.Logging.Abstractions;

namespace ServiceDefaults.DependencyInjection.Diagnostics;
public class ServiceCollectionExtensionsDiagnostics : IServiceCollectionExtensionsDiagnostics
{
    private readonly ILogHelper<ServiceCollectionExtensionsDiagnostics> _log;

    public ServiceCollectionExtensionsDiagnostics(ILogHelper<ServiceCollectionExtensionsDiagnostics> log)
    {
        _log = log;
    }

    public void LogServiceRegistration(Type serviceType, Type implementationType, ServiceLifetime lifetime, string sourceAttribute)
    {
        _log.LogInfo("📦 Registered [{Lifetime}] {ServiceType} => {ImplementationType} via [{Attribute}]",
            lifetime, serviceType.Name, implementationType.Name, sourceAttribute);
    }
}