using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var services = new ServiceCollection();
// services.AddScoped(typeof(IFuck<>), typeof(FuckForEverything<>));
services.AddScoped(typeof(IFuck<>), typeof(FuckForOmissibles<>));

var sp = services.BuildServiceProvider();

var isService = sp.GetRequiredService<IServiceProviderIsService>();
Console.WriteLine($"IFuck<Omissible> has services? {isService.IsService(typeof(IFuck<Omissible>))}");
Console.WriteLine($"IFuck<string> has services? {isService.IsService(typeof(IFuck<string>))}");

using var scope = sp.CreateScope();

var one = scope.ServiceProvider.GetServices<IFuck<Omissible>>();
Console.WriteLine($"For IFuck<Omissible>: ({one.Count()}) {string.Join(", ", one)}");

var two = scope.ServiceProvider.GetServices<IFuck<string>>();
Console.WriteLine($"For IFuck<string>: ({two.Count()}) {string.Join(", ", two)}");

public interface IFuck<T>;

public sealed class FuckForEverything<T> : IFuck<T>;
public sealed class FuckForOmissibles<T> : IFuck<T>
    where T : IOmissible;

public interface IOmissible;
public sealed class Omissible : IOmissible;
