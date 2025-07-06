using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ServiceDefaults.DependencyInjection.Helpers;

public static class StartupInjectionHelper
{
    public static void RegisterAttributedServices(this IServiceCollection services, string serviceName)
    {
        using var tempProvider = services.BuildServiceProvider();

        var loggerFactory = tempProvider.GetRequiredService<ILoggerFactory>();
        var options = tempProvider.GetRequiredService<IOptions<RegistrationOptions>>();

        services.RegisterAllAttributedServices(serviceName, loggerFactory, options);
    }
}
