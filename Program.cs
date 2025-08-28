using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddScoped<IFoo, Foo1>();
// services.AddSingleton<IServiceScopeFactory, S>();

var sp = services.BuildServiceProvider();

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
