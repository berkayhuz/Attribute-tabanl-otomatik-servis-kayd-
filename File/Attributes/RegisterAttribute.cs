namespace ServiceDefaults.DependencyInjection.Attributes;
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RegisterAttribute : Attribute
{
    public ServiceLifetime Lifetime { get; }

    public RegisterAttribute(ServiceLifetime lifetime)
    {
        Lifetime = lifetime;
    }

    public bool RegisterSelf { get; set; } = false;
}

