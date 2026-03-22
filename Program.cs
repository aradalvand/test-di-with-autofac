using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var services = new ServiceCollection();
services.AddSingleton(typeof(IFuck<>), typeof(FuckForEverything<>));
services.AddSingleton(typeof(IFuck<>), typeof(FuckForOmissibles<>));

var sp = services.BuildServiceProvider();

var one = sp.GetServices<IFuck<Omissible>>();
Console.WriteLine($"{string.Join(", ", one)}");

var two = sp.GetServices<IFuck<string>>();
Console.WriteLine($"{string.Join(", ", two)}");

public interface IFuck<T>;

public sealed class FuckForEverything<T> : IFuck<T>;
public sealed class FuckForOmissibles<T> : IFuck<T>
    where T : IOmissible;

public interface IOmissible;
public sealed class Omissible : IOmissible;
