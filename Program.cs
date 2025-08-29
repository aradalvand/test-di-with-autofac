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

var host2 = new TestHost(sp);
await host2.StartAsync();
using (var scope = host2.Services.CreateScope())
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

public sealed class TestServiceProvider : IKeyedServiceProvider, ISupportRequiredService, IServiceProviderIsKeyedService, IServiceScopeFactory, IDisposable, IAsyncDisposable
{
    private const string LifetimeScopeTag = "SCOPED_SINGLETON";
    private readonly IContainer _container;
    public TestServiceProvider(IServiceCollection services)
    {
        var builder = new ContainerBuilder();
        // NOTE: Fascinatingly, the "lifetime scope tag for singletons" of Autofac feature is precisely what we need in this context — it transforms singleton services to "scoped to lifetime scopes that have this tag" and the any nested scopes.
        // NOTE: That is exactly the behavior we need here; we need to isolate singletons to each scope (since we want to create a scope for each test case — rather than build a whole container for each test case), and it wouldn't be enough to just update the Microsoft DI service descriptors' lifetimes from singleton to scoped, because in the case of nested scopes, if an earlier scope resolve a scoped service, and then a nested scope does the same for the same service, they won't share the service instance. That is a breaking departure from the normal singleton behavior, we want this "singleton->scoped" transformation to be transparent.
        builder.Populate(services, lifetimeScopeTagForSingletons: LifetimeScopeTag);
        // NOTE: This one of the main two reasons for using Autofac here; you cannot override `IServiceProvider` and `IServiceScopeFactory` using Microsoft's `ServiceCollection` implementation, whereas you CAN do that with the Autofac container; which is necessary for our implementation here.
        builder.RegisterInstance<IServiceProvider>(this);
        builder.RegisterInstance<IServiceScopeFactory>(this);
        // NOTE: We couldn't just use Autofac's built-in `AutofacServiceProvider` because you can't create tagged scopes through that. So, we have to have our own custom implementation that forwards calls to the underlying containers/scopes in specific ways.
        _container = builder.Build();
    }

    public object? GetService(Type serviceType) =>
        _container.ResolveOptional(serviceType);
    public object GetRequiredService(Type serviceType) =>
        _container.Resolve(serviceType);

    public object? GetKeyedService(Type serviceType, object? serviceKey) =>
        _container.ResolveKeyedOptional(serviceKey!, serviceType);
    public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
         _container.ResolveKeyed(serviceKey!, serviceType);

    public bool IsService(Type serviceType) =>
        _container.IsRegistered(serviceType);
    public bool IsKeyedService(Type serviceType, object? serviceKey) =>
        _container.IsRegisteredWithKey(serviceKey!, serviceType);

    public IServiceScope CreateScope() =>
        new Scope(_container);

    public void Dispose() =>
        _container.Dispose();
    public ValueTask DisposeAsync() =>
        _container.DisposeAsync();

    private sealed class Scope : IServiceScopeFactory, ISupportRequiredService, IServiceProviderIsKeyedService, IServiceScope, IKeyedServiceProvider, IAsyncDisposable
    {
        private readonly ILifetimeScope _scope;
        public Scope(IContainer topLevelContainer) =>
            _scope = topLevelContainer.BeginLifetimeScope(LifetimeScopeTag, AddToContainer);
        public Scope(ILifetimeScope scope) =>
            _scope = scope.BeginLifetimeScope(AddToContainer);

        private void AddToContainer(ContainerBuilder builder)
        {
            builder.RegisterInstance<IServiceProvider>(this);
            builder.RegisterInstance<IServiceScopeFactory>(this);
        }

        // NOTE: We create a new actual scope here rather than e.g. returning `this` because we want normal scoped services to have their normal behavior (e.g. different instance across nested scopes); against, transparency is paramount here.
        // NOTE: This tells you that scoped and singleton will still differ in behavior in `TestServiceProvider`. Services originally registered as scoped would have a different instance across each scope (even if the scopes are nested, just like the default behavior), whereas services originally registered as singleton would resolve to the same instance across inner scopes.
        public IServiceScope CreateScope() => new Scope(_scope);

        public IServiceProvider ServiceProvider => this;

        public object? GetService(Type serviceType) =>
            _scope.ResolveOptional(serviceType);
        public object GetRequiredService(Type serviceType) =>
            _scope.Resolve(serviceType);

        public object? GetKeyedService(Type serviceType, object? serviceKey) =>
            _scope.ResolveKeyedOptional(serviceKey!, serviceType);
        public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
            _scope.ResolveKeyed(serviceKey!, serviceType);

        public void Dispose() =>
            _scope.Dispose();
        public ValueTask DisposeAsync() =>
            _scope.DisposeAsync();

        public bool IsService(Type serviceType) =>
            _scope.IsRegistered(serviceType);
        public bool IsKeyedService(Type serviceType, object? serviceKey) =>
            _scope.IsRegisteredWithKey(serviceKey!, serviceType);
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
public static class AutofacExtensions
{
    // NOTE: This particular overload doesn't exist built-uin for `ResolveKeyedOptional` in Autofac, for some reason. So, we write it ourselves.
    public static object? ResolveKeyedOptional(
        this IComponentContext context,
        object? serviceKey,
        Type serviceType
    ) =>
        context.TryResolveKeyed(serviceKey!, serviceType, out var resolved)
            ? resolved
            : null;
}
