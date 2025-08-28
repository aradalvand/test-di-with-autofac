using System.Collections.Immutable;
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

public sealed class CustomServiceProvider : IServiceProvider, IDisposable
{
    private readonly List<Type> _singletonMadeScopedServiceTypes = [];
    private readonly ServiceProvider _underlyingProvider;
    private readonly IServiceScope _singletonScope;
    private readonly ScopeFactory _scopeFactory;
    public CustomServiceProvider(IServiceCollection services)
    {
        foreach (var singletonService in services.Where(s => s.Lifetime is ServiceLifetime.Singleton).ToList())
        {
            var scopedEquivalent = singletonService.WithLifetime(ServiceLifetime.Scoped);
            services.Remove(singletonService);
            services.Add(scopedEquivalent);

            _singletonMadeScopedServiceTypes.Add(singletonService.ServiceType);
        }
        _underlyingProvider = services.BuildServiceProvider();
        _singletonScope = _underlyingProvider.CreateScope();
        _scopeFactory = new(this);
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceScopeFactory))
            return _scopeFactory;

        return _underlyingProvider.GetService(serviceType);
    }

    public void Dispose() =>
        _singletonScope.Dispose();

    private sealed class ScopeFactory(
        CustomServiceProvider parent
    ) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            new Scope(parent, this);
    }

    private sealed class Scope(
        CustomServiceProvider provider,
        ScopeFactory scopeFactory
    ) : IServiceScope, IServiceProvider
    {
        private readonly IServiceScope _scope = provider._underlyingProvider.CreateScope();
        public IServiceProvider ServiceProvider => this;
        public void Dispose() { }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceScopeFactory))
                return scopeFactory;

            if (provider._singletonMadeScopedServiceTypes.Contains(serviceType))
                return provider._singletonScope.ServiceProvider.GetService(serviceType);
            return _scope.ServiceProvider.GetService(serviceType);
        }
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
