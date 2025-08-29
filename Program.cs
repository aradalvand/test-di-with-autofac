using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var services = new ServiceCollection();
services.AddSingleton<IFoo, Foo1>();
services.AddScoped<IBar, Bar1>();
services.AddHostedService<Worker>();

var sp = new TestServiceProvider(services);
var host1 = new TestHost(sp);
await host1.StartAsync();
using (var scope = host1.Services.CreateScope())
{
    var scopedFoo = scope.ServiceProvider.GetRequiredService<IFoo>();
    var scopedBar1 = scope.ServiceProvider.GetRequiredService<IBar>();
    var scopedBar2 = scope.ServiceProvider.GetRequiredService<IBar>();
    Console.WriteLine($"SCOPED IFoo: {scopedFoo.GetHashCode()}");
    Console.WriteLine($"SCOPED IBar: {scopedBar1.GetHashCode()} - {scopedBar2.GetHashCode()}");

    using (var scope2 = scope.ServiceProvider.CreateScope())
    {
        var scopedFoo2 = scope2.ServiceProvider.GetRequiredService<IFoo>();
        var scopedBar3 = scope2.ServiceProvider.GetRequiredService<IBar>();
        Console.WriteLine($"SCOPED IFoo: {scopedFoo2.GetHashCode()}");
        Console.WriteLine($"SCOPED IBar: {scopedBar3.GetHashCode()}");
    }
}
await host1.StopAsync();

Console.WriteLine("---");

// var host2 = new TestHost(sp);
// await host2.StartAsync();
// using (var scope = sp.CreateScope())
// {
//     var scopedFoo = scope.ServiceProvider.GetRequiredService<IFoo>();
//     var scopedBar = scope.ServiceProvider.GetRequiredService<IBar>();
//     Console.WriteLine($"SCOPED IFoo: {scopedFoo.GetHashCode()}");
//     Console.WriteLine($"SCOPED IBar: {scopedBar.GetHashCode()}");

//     using (var scope2 = scope.ServiceProvider.CreateScope())
//     {
//         var scopedFoo2 = scope2.ServiceProvider.GetRequiredService<IFoo>();
//         var scopedBar2 = scope2.ServiceProvider.GetRequiredService<IBar>();
//         Console.WriteLine($"SCOPED IFoo: {scopedFoo2.GetHashCode()}");
//         Console.WriteLine($"SCOPED IBar: {scopedBar2.GetHashCode()}");
//     }
// }
// await host2.StopAsync();

public sealed class Worker(
    IServiceProvider serviceProvider
) : BackgroundService
{
    public void Do()
    {
        using var scope = serviceProvider.CreateScope();
        var foo = scope.ServiceProvider.GetRequiredService<IFoo>();
        Console.WriteLine($"Worker — serviceProvider {serviceProvider.GetType()} / {serviceProvider.GetHashCode()} — IFoo {foo.GetHashCode()}"); // TODO: the `IFoo`'s GetHashCode here will be different than the ones in the Console.WriteLines above
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Do();
        return Task.CompletedTask;
    }
}

public interface IFoo;
public class Foo1 : IFoo;

public interface IBar;
public class Bar1 : IBar;

public sealed class TestServiceProvider : IServiceProvider, IServiceScopeFactory
{
    private const string LifetimeScopeTag = "SCOPED_SINGLETON";
    private readonly IContainer _container;
    public TestServiceProvider(IServiceCollection services)
    {
        var builder = new ContainerBuilder();
        builder.Populate(services, lifetimeScopeTagForSingletons: LifetimeScopeTag);
        builder.RegisterInstance<IServiceProvider>(this);
        builder.RegisterInstance<IServiceScopeFactory>(this);
        _container = builder.Build();
    }

    object? IServiceProvider.GetService(Type serviceType)
    {
        return _container.Resolve(serviceType);
    }

    IServiceScope IServiceScopeFactory.CreateScope() =>
        new Scope(this);

    private sealed class Scope : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        private readonly ILifetimeScope _scope;

        public Scope(TestServiceProvider parent) =>
            _scope = parent._container.BeginLifetimeScope(LifetimeScopeTag, cb =>
            {
                cb.RegisterInstance<IServiceProvider>(this);
                cb.RegisterInstance<IServiceScopeFactory>(this);
            });

        public Scope(ILifetimeScope scope) =>
            _scope = scope;

        IServiceProvider IServiceScope.ServiceProvider => this;
        IServiceScope IServiceScopeFactory.CreateScope() => new Scope(_scope.BeginLifetimeScope());
        object? IServiceProvider.GetService(Type serviceType)
        {
            return _scope.Resolve(serviceType);
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
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
