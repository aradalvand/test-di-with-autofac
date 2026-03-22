using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
// services.AddScoped(typeof(IFoo<>), typeof(FooForEverything<>));
services.AddScoped(typeof(IFoo<>), typeof(FooForSpecials<>));
services.AddKeyedSingleton<IServiceProviderIsService, StrictServiceProviderIsService>("Sane");

var sp = services.BuildServiceProvider(validateScopes: true);

var isService = sp.GetRequiredKeyedService<IServiceProviderIsService>("Sane");
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

public sealed class StrictServiceProviderIsService(
    IServiceProvider sp,
    IServiceProviderIsService builtIn
) : IServiceProviderIsService
{
    public bool IsService(Type serviceType)
    {
        if (!builtIn.IsService(serviceType))
            return false;
        try
        {
            _ = sp.GetRequiredService(serviceType);
            return true;
        }
        catch (Exception ex)
        {
            if (ex is InvalidOperationException invalid && invalid.Message.StartsWith("Cannot resolve scoped service"))
                return true; // NOTE: We special the scoping exception.
            return false;
        }
    }
}
