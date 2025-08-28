using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var services = new ServiceCollection();
services.AddSingleton<IFoo, Foo1>();
services.AddScoped<IBar, Bar1>();
services.Configure<Fuck>(f => f.Count = 123);
services.AddHostedService<Worker>();
// services.AddSingleton<IServiceScopeFactory, S>();

var sp = new TestServiceProvider(services);
var host1 = new TestHost(sp);
await host1.StartAsync();

using (var scope = sp.CreateScope())
{
    var scopedFoo = scope.ServiceProvider.GetRequiredService<IFoo>();
    var scopedBar = scope.ServiceProvider.GetRequiredService<IBar>();
    Console.WriteLine($"SCOPED IFoo: {scopedFoo.GetHashCode()}");
    Console.WriteLine($"SCOPED IBar: {scopedBar.GetHashCode()}");

    using (var scope2 = scope.ServiceProvider.CreateScope())
    {
        var scopedFoo2 = scope2.ServiceProvider.GetRequiredService<IFoo>();
        var scopedBar2 = scope2.ServiceProvider.GetRequiredService<IBar>();
        Console.WriteLine($"SCOPED IFoo: {scopedFoo2.GetHashCode()}");
        Console.WriteLine($"SCOPED IBar: {scopedBar2.GetHashCode()}");
    }
}
await host1.StopAsync();

Console.WriteLine("---");

var host2 = new TestHost(new(services));
await host2.StartAsync();
using (var scope = sp.CreateScope())
{
    var scopedFoo = scope.ServiceProvider.GetRequiredService<IFoo>();
    var scopedBar = scope.ServiceProvider.GetRequiredService<IBar>();
    Console.WriteLine($"SCOPED IFoo: {scopedFoo.GetHashCode()}");
    Console.WriteLine($"SCOPED IBar: {scopedBar.GetHashCode()}");

    using (var scope2 = scope.ServiceProvider.CreateScope())
    {
        var scopedFoo2 = scope2.ServiceProvider.GetRequiredService<IFoo>();
        var scopedBar2 = scope2.ServiceProvider.GetRequiredService<IBar>();
        Console.WriteLine($"SCOPED IFoo: {scopedFoo2.GetHashCode()}");
        Console.WriteLine($"SCOPED IBar: {scopedBar2.GetHashCode()}");
    }
}
await host2.StopAsync();

public sealed class Worker(
    IServiceProvider serviceProvider
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var foo = scope.ServiceProvider.GetRequiredService<IFoo>();
            Console.WriteLine($"Worker — serviceProvider {serviceProvider.GetType()} / {serviceProvider.GetHashCode()} — IFoo {foo.GetHashCode()}"); // TODO: FUCKt's
            await Task.Delay(100_000, stoppingToken);
        }
        finally
        {
            Console.WriteLine("Worker over");
        }
    }
}

// public sealed class Dependent(
// )
// {
//     public void Do() => Console.WriteLine(foo.GetHashCode());
// }

public interface IFoo;
public class Foo1 : IFoo;

public interface IBar;
public class Bar1 : IBar;

public sealed class TestServiceProvider : IServiceProvider, IServiceScopeFactory
{
    private readonly List<Type> _singletonMadeScopedServiceTypes = [];
    private readonly ServiceProvider _underlyingProvider;
    private readonly Lazy<IServiceScope> _singletonScope;
    public TestServiceProvider(IServiceCollection services)
    {
        foreach (var singletonService in services.Where(s => s.Lifetime is ServiceLifetime.Singleton).ToList())
        {
            var scopedEquivalent = singletonService.WithLifetime(ServiceLifetime.Scoped, skipSingletonsWithInstances: true);
            services.Remove(singletonService);
            services.Add(scopedEquivalent);

            _singletonMadeScopedServiceTypes.Add(singletonService.ServiceType);
        }
        _underlyingProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _singletonScope = new(_underlyingProvider.CreateScope);
    }

    object? IServiceProvider.GetService(Type serviceType) =>
        serviceType == typeof(IServiceScopeFactory)
            ? this
            : _underlyingProvider.GetService(serviceType);

    IServiceScope IServiceScopeFactory.CreateScope() =>
        new Scope(this);

    private sealed class Scope(
        TestServiceProvider provider
    ) : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        IServiceProvider IServiceScope.ServiceProvider => this;
        IServiceScope IServiceScopeFactory.CreateScope() => new Scope(provider);
        object? IServiceProvider.GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceScopeFactory))
                return this;

            return provider._singletonScope.Value.ServiceProvider.GetService(serviceType);
        }

        public void Dispose() { }
    }
}

public sealed class TestHost(
    TestServiceProvider serviceProvider
) : IHost
{
    public IServiceProvider Services => _scope.ServiceProvider;

    private readonly IServiceScope _scope = serviceProvider.CreateScope();
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var hostedServices = _scope.ServiceProvider.GetServices<IHostedService>();
        foreach (var hostedService in hostedServices) // NOTE: We run the hosted services' `StartAsync` in order and sequentially because that's the standard behavior of the built-in hosts.
            await hostedService.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var hostedServices = _scope.ServiceProvider.GetServices<IHostedService>();
        foreach (var hostedService in hostedServices)
            await hostedService.StopAsync(cancellationToken);

        Dispose();
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}

public static class ServiceDescriptorExtensions
{
    public static ServiceDescriptor WithLifetime(
        this ServiceDescriptor service,
        ServiceLifetime newLifetime,
        bool skipSingletonsWithInstances = false
    )
    {
        if (service.Lifetime == newLifetime)
            return service;

        if (service.IsKeyedService)
        {
            if (service.KeyedImplementationInstance is not null) // NOTE: This entails `service` is singleton.
                return skipSingletonsWithInstances ? service : throw new InvalidOperationException($"Keyed singleton service {service} has implementation instance {service.KeyedImplementationInstance} and cannot be turned into {newLifetime}");

            if (service.KeyedImplementationFactory is not null)
                return new ServiceDescriptor(service.ServiceType, service.ServiceKey, service.KeyedImplementationFactory, newLifetime);

            return new ServiceDescriptor(service.ServiceType, service.ServiceKey, service.KeyedImplementationType!, newLifetime);
        }

        if (service.ImplementationInstance is not null) // NOTE: This entails `service` is singleton.
            return skipSingletonsWithInstances ? service : throw new InvalidOperationException($"Singleton service {service} has implementation instance {service.KeyedImplementationInstance} and cannot be turned into {newLifetime}");

        if (service.ImplementationFactory is not null)
            return new ServiceDescriptor(service.ServiceType, service.ImplementationFactory, newLifetime);

        return new ServiceDescriptor(service.ServiceType, service.ImplementationType!, newLifetime);
    }
}

public record Fuck
{
    public int Count { get; set; }
}
