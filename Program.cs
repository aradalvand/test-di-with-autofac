using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddSingleton<IFoo, Foo1>();
services.AddScoped<IFoo, Foo2>();

var sp = services.BuildServiceProvider();
var singletonFoo = sp.GetRequiredService<IFoo>();

Console.WriteLine($"SINGLETON: {sp.GetRequiredService<IFoo>().GetType()}");

using (var scope = sp.CreateScope())
{
    var scopedFoo = scope.ServiceProvider.GetRequiredService<IFoo>();
    Console.WriteLine($"SCOPED: {scope.ServiceProvider.GetRequiredService<IFoo>().GetType()}");
}

public interface IFoo;
public class Foo1 : IFoo;
public class Foo2 : IFoo;
