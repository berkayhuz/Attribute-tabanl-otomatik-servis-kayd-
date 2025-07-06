namespace ServiceDefaults.DependencyInjection.Attributes;
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class TargetServiceAttribute : Attribute
{
    public string[] Services { get; }

    public TargetServiceAttribute(params string[] services)
    {
        Services = services;
    }
}
