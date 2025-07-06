using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceDefaults.DependencyInjection.Attributes;
using System.Collections.Concurrent;
using System.Reflection;
using MSLogging = Microsoft.Extensions.Logging;

namespace ServiceDefaults.DependencyInjection;

public class RegistrationOptions
{
    public int SpecialInterfaceThreshold { get; set; } = 3;
    public string[] ExcludedAssemblyPrefixes { get; set; } = new[] { "Microsoft.", "System.", "netstandard" };
    public long CacheSizeLimit { get; set; } = 1024;
     public int CacheEntryExpirationSeconds { get; set; } = 3600;
}

public static class ServiceCollectionExtensions
{
    private static readonly MemoryCache _typesCache = new(new MemoryCacheOptions { SizeLimit = 1024 });
    private static readonly ConcurrentDictionary<Type, int> _genericInterfaceUsageCount = new();

    public static IServiceCollection RegisterAllAttributedServices(
        this IServiceCollection services,
        string currentServiceName,
        MSLogging.ILoggerFactory loggerFactory,
        IOptions<RegistrationOptions> optionsAccessor,
        IAssemblyProvider? assemblyProvider = null)
    {
        var logger = loggerFactory.CreateLogger(typeof(ServiceCollectionExtensions).FullName!);
        var options = optionsAccessor.Value;

        assemblyProvider ??= new DefaultAssemblyProvider(GetFilteredAssemblies(options));
        var assemblies = assemblyProvider.GetAssemblies();

        CountGenericInterfaceUsage(assemblies, logger);

        foreach (var assembly in assemblies)
        {
            RegisterFromAssembly(services, assembly, currentServiceName, logger, options);
        }

        return services;
    }

    private static IEnumerable<Assembly> GetFilteredAssemblies(RegistrationOptions options)
        => AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.FullName)
                        && !options.ExcludedAssemblyPrefixes.Any(pref => a.FullName.StartsWith(pref, StringComparison.OrdinalIgnoreCase)))
            .Distinct();

    private static void CountGenericInterfaceUsage(IEnumerable<Assembly> assemblies, MSLogging.ILogger logger)
    {
        _genericInterfaceUsageCount.Clear();
        foreach (var assembly in assemblies)
        {
            foreach (var type in SafeGetTypes(assembly, logger).Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition))
            {
                foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType))
                {
                    var def = iface.GetGenericTypeDefinition();
                    _genericInterfaceUsageCount.AddOrUpdate(def, 1, (_, count) => count + 1);
                }
            }
        }
    }

    private static void RegisterFromAssembly(
        IServiceCollection services,
        Assembly assembly,
        string currentServiceName,
        MSLogging.ILogger logger,
        RegistrationOptions options)
    {
        foreach (var impl in SafeGetTypes(assembly, logger).Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition))
        {
            TryRegisterService(services, impl, currentServiceName, logger, options);
        }
    }

    private static void TryRegisterService(
        IServiceCollection services,
        Type impl,
        string currentServiceName,
        MSLogging.ILogger logger,
        RegistrationOptions options)
    {
        var attr = impl.GetCustomAttribute<RegisterAttribute>();
        if (attr is null || !IsTargeted(impl, currentServiceName))
            return;

        var source = new LifetimeSource(attr.Lifetime, nameof(RegisterAttribute));
        var interfaces = impl.GetInterfaces()
                             .Except(new[] { typeof(IDisposable), typeof(IAsyncDisposable) })
                             .ToArray();

        if (RegisterHostedService(services, impl, source, logger))
            return;

        var special = interfaces
            .Where(i => i.IsGenericType
                        && _genericInterfaceUsageCount.TryGetValue(i.GetGenericTypeDefinition(), out var count)
                        && count >= options.SpecialInterfaceThreshold)
            .ToArray();

        if (special.Any())
        {
            RegisterInterfaces(services, special, impl, source, logger);
            if (attr.RegisterSelf)
                AddWithLogging(services, impl, impl, source, logger);
            return;
        }

        if (!interfaces.Any())
        {
            AddWithLogging(services, impl, impl, source, logger);
            return;
        }

        RegisterInterfaces(services, interfaces, impl, source, logger);
        if (attr.RegisterSelf)
            AddWithLogging(services, impl, impl, source, logger);
    }

    private static bool RegisterHostedService(
        IServiceCollection services,
        Type impl,
        LifetimeSource source,
        MSLogging.ILogger logger)
    {
        if (typeof(BackgroundService).IsAssignableFrom(impl) || typeof(IHostedService).IsAssignableFrom(impl))
        {
            AddWithLogging(services, impl, impl, source, logger);
            return true;
        }
        return false;
    }

    private static void RegisterInterfaces(
        IServiceCollection services,
        IEnumerable<Type> interfaces,
        Type impl,
        LifetimeSource source,
        MSLogging.ILogger logger)
    {
        foreach (var svc in interfaces)
        {
            var (serviceType, implType) = svc.ResolveFor(impl);
            AddWithLogging(services, serviceType, implType, source, logger);
        }
    }

    private static List<Type> SafeGetTypes(Assembly assembly, MSLogging.ILogger logger)
    {
        if (_typesCache.TryGetValue(assembly, out List<Type> list))
            return list;

        try
        {
            list = assembly.DefinedTypes.Select(t => t.AsType()).ToList();
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSize(1)
                .SetSlidingExpiration(TimeSpan.FromSeconds(3600));
            _typesCache.Set(assembly, list, cacheEntryOptions);
            return list;
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var inner in ex.LoaderExceptions ?? Array.Empty<Exception>())
                logger.LogError(inner, "Error loading type from assembly {Assembly}", assembly.FullName);

            list = ex.Types.Where(t => t != null).Cast<Type>().ToList();
            _typesCache.Set(assembly, list, new MemoryCacheEntryOptions().SetSize(1));
            return list;
        }
    }

    private static (Type ServiceType, Type ImplementationType) ResolveFor(this Type service, Type impl)
    {
        if (service.IsGenericType && impl.IsGenericType)
            return (service.GetGenericTypeDefinition(), impl.GetGenericTypeDefinition());
        return (service, impl);
    }

    private static bool IsTargeted(Type type, string currentServiceName)
    {
        var attr = type.GetCustomAttribute<TargetServiceAttribute>();
        return attr is null || attr.Services.Any(s => s.Equals(currentServiceName, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddWithLogging(
        IServiceCollection services,
        Type serviceType,
        Type implementationType,
        LifetimeSource source,
        MSLogging.ILogger logger)
    {
        var registrar = ServiceRegistrarFactory.Create();
        registrar.Register(services, source.Lifetime, serviceType, implementationType);
        logger.LogInformation(
            "Registered {Service} -> {Impl} as {Lifetime} via {Source}",
            serviceType.FullName,
            implementationType.FullName,
            source.Lifetime,
            source.AttributeName);
    }

    private record LifetimeSource(ServiceLifetime Lifetime, string AttributeName);

    public interface IAssemblyProvider
    {
        IEnumerable<Assembly> GetAssemblies();
    }

    internal class DefaultAssemblyProvider : IAssemblyProvider
    {
        private readonly IEnumerable<Assembly> _assemblies;
        public DefaultAssemblyProvider(IEnumerable<Assembly> assemblies) => _assemblies = assemblies;
        public IEnumerable<Assembly> GetAssemblies() => _assemblies;
    }

    public interface IServiceRegistrar
    {
        void Register(IServiceCollection services, ServiceLifetime lifetime, Type serviceType, Type implementationType);
    }

    public class DefaultServiceRegistrar : IServiceRegistrar
    {
        private static readonly IReadOnlyDictionary<ServiceLifetime, Action<IServiceCollection, Type, Type>> _registrars =
            new Dictionary<ServiceLifetime, Action<IServiceCollection, Type, Type>>
            {
                [ServiceLifetime.Singleton] = (s, svc, impl) => s.AddSingleton(svc, impl),
                [ServiceLifetime.Scoped] = (s, svc, impl) => s.AddScoped(svc, impl),
                [ServiceLifetime.Transient] = (s, svc, impl) => s.AddTransient(svc, impl),
            };

        public void Register(IServiceCollection services, ServiceLifetime lifetime, Type serviceType, Type implementationType)
        {
            if (!_registrars.TryGetValue(lifetime, out var action))
                throw new InvalidOperationException($"No registrar for lifetime {lifetime}");
            action(services, serviceType, implementationType);
        }
    }

    internal static class ServiceRegistrarFactory
    {
        public static IServiceRegistrar Create() => new DefaultServiceRegistrar();
    }
}