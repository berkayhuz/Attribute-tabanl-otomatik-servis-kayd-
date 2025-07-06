namespace ServiceDefaults.DependencyInjection.Diagnostics;
public interface IServiceCollectionExtensionsDiagnostics
{
    void LogServiceRegistration(Type serviceType, Type implementationType, ServiceLifetime lifetime, string sourceAttribute);
}