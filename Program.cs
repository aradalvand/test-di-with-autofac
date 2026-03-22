using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
// services.AddScoped(typeof(IFoo<>), typeof(FooForEverything<>));
services.AddScoped(typeof(IFoo<>), typeof(FooForSpecials<>));

var sp = services.BuildServiceProvider();

var isService = sp.GetRequiredService<IServiceProviderIsService>();
Console.WriteLine($"IFoo<Special> has services? {isService.IsService(typeof(IFoo<Special>))}");
Console.WriteLine($"IFoo<object> has services? {isService.IsService(typeof(IFoo<object>))}");

using var scope = sp.CreateScope();

var one = scope.ServiceProvider.GetServices<IFoo<Special>>();
Console.WriteLine($"For IFoo<Special>: ({one.Count()}) {string.Join(", ", one)}");

var two = scope.ServiceProvider.GetServices<IFoo<object>>();
Console.WriteLine($"For IFoo<object>: ({two.Count()}) {string.Join(", ", two)}");

public interface IFoo<T>;

public sealed class FooForEverything<T> : IFoo<T>;
public sealed class FooForSpecials<T> : IFoo<T>
    where T : ISpecial;

public interface ISpecial;
public sealed class Special : ISpecial;
