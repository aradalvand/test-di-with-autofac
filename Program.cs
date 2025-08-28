using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddScoped<IFoo, Foo1>();
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
        _underlyingProvider = services.BuildServiceProvider();
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceScopeFactory))
            return new S();
        return _underlyingProvider.GetService(serviceType);
    }
}
