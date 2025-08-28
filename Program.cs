using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

var services = new ServiceCollection();
services.AddSingleton<IFoo, Foo1>();
// services.AddSingleton<IServiceScopeFactory, S>();

// var sp = services.BuildServiceProvider();
var sp = new CustomServiceProvider(services);

using (var scope = sp.CreateScope())
{
    var scopedFoo = scope.ServiceProvider.GetRequiredService<IFoo>();
    Console.WriteLine($"SCOPED: {scopedFoo.GetHashCode()}");

    using (var scope2 = scope.ServiceProvider.CreateScope())
    {
        var scopedFoo2 = scope2.ServiceProvider.GetRequiredService<IFoo>();
        Console.WriteLine($"SCOPED: {scopedFoo2.GetHashCode()}");
    }
}

public interface IFoo;
public class Foo1 : IFoo;

public class S : IServiceScopeFactory
{
    public IServiceScope CreateScope()
    {
        Console.WriteLine("HERE");
        throw new NotImplementedException();
    }
}

public sealed class CustomServiceProvider : IServiceProvider
{
    private readonly IServiceProvider _underlyingProvider;
    public CustomServiceProvider(IServiceCollection services)
    {
        foreach (var singletonService in services.Where(s => s.Lifetime is ServiceLifetime.Singleton).ToList())
        {
            var scopedEquivalent = singletonService.WithLifetime(ServiceLifetime.Scoped);
            services.Remove(singletonService);
            services.Add(scopedEquivalent);
        }
        _underlyingProvider = services.BuildServiceProvider();
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceScopeFactory))
            return new S();
        return _underlyingProvider.GetService(serviceType);
    }
}

public static class ServiceDescriptorExtensions
{
    public static ServiceDescriptor WithLifetime(
        this ServiceDescriptor service,
        ServiceLifetime newLifetime,
        bool tolerateSingleInstanceRemoval = false
    )
    {
        if (service.Lifetime == newLifetime)
            return service;

        if (service.IsKeyedService)
        {
            if (!tolerateSingleInstanceRemoval && service.KeyedImplementationInstance is not null) // NOTE: This entails `service` is singleton.
                throw new InvalidOperationException();

            if (service.KeyedImplementationFactory is not null)
                return new ServiceDescriptor(service.ServiceType, service.ServiceKey, service.KeyedImplementationFactory, newLifetime);

            return new ServiceDescriptor(service.ServiceType, service.ServiceKey, service.KeyedImplementationType!, newLifetime);
        }

        if (!tolerateSingleInstanceRemoval && service.ImplementationInstance is not null) // NOTE: This entails `service` is singleton.
            throw new InvalidOperationException();

        if (service.ImplementationFactory is not null)
            return new ServiceDescriptor(service.ServiceType, service.ImplementationFactory, newLifetime);

        return new ServiceDescriptor(service.ServiceType, service.ImplementationType!, newLifetime);
    }
}
