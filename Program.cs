using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var services = new ServiceCollection();
services.AddSingleton<IFoo, Foo1>();
services.AddScoped<IBar, Bar1>();
services.AddHostedService<Worker>();
// services.AddSingleton<IServiceScopeFactory, S>();

var sp = new TestServiceProvider(services);
// var host1 = new TestHost(sp);
// await host1.StartAsync();
using (var scope = sp.CreateScope())
{
    var hostedServices = scope.ServiceProvider.GetServices<IHostedService>();
    foreach (var hostedService in hostedServices) // NOTE: We run the hosted services' `StartAsync` in order and sequentially because that's the standard behavior of the built-in hosts.
        await hostedService.StartAsync(default);

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

    foreach (var hostedService in hostedServices)
        await hostedService.StopAsync(default);
}
// await host1.StopAsync();

Console.WriteLine("---");

// var host2 = new TestHost(new(services));
// await host2.StartAsync();
using (var scope = sp.CreateScope())
{
    var hostedServices = scope.ServiceProvider.GetServices<IHostedService>();
    foreach (var hostedService in hostedServices) // NOTE: We run the hosted services' `StartAsync` in order and sequentially because that's the standard behavior of the built-in hosts.
        await hostedService.StartAsync(default);

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

    foreach (var hostedService in hostedServices)
        await hostedService.StopAsync(default);
}
// await host2.StopAsync();

public sealed class Worker(
    IFoo foo
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            Console.WriteLine($"Worker — IFoo {foo.GetHashCode()}");
            await Task.Delay(100_000, stoppingToken);
        }
        finally
        {
            Console.WriteLine("Worker over");
        }
    }
}

public interface IFoo;
public class Foo1 : IFoo;

public interface IBar;
public class Bar1 : IBar;

public sealed class TestServiceProvider : IServiceProvider, IServiceScopeFactory
{
    private readonly ServiceProvider _underlyingProvider;
    public TestServiceProvider(IServiceCollection services)
    {
        foreach (var singletonService in services.Where(s => s.Lifetime is ServiceLifetime.Singleton).ToList())
        {
            var scopedEquivalent = singletonService.WithLifetime(ServiceLifetime.Scoped);
            services.Remove(singletonService);
            services.Add(scopedEquivalent);
        }
        _underlyingProvider = services.BuildServiceProvider();
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
        private readonly Lazy<IServiceScope> _singletonScope = new(provider._underlyingProvider.CreateScope);

        IServiceProvider IServiceScope.ServiceProvider => this;
        IServiceScope IServiceScopeFactory.CreateScope() => this;
        object? IServiceProvider.GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceScopeFactory))
                return this;

            return _singletonScope.Value.ServiceProvider.GetService(serviceType);
        }

        public void Dispose() { }
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

        _scope.Dispose();
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}
