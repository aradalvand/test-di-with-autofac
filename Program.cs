using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var services = new ServiceCollection();
services.AddSingleton(typeof(IFuck<>), typeof(FuckForEverything<>));
services.AddSingleton(typeof(IFuck<>), typeof(FuckForValueTypes<>));

var sp = services.BuildServiceProvider();

var one = sp.GetServices<IFuck<string>>();
Console.WriteLine($"{string.Join(", ", one)}");

var two = sp.GetServices<IFuck<int>>();
Console.WriteLine($"{string.Join(", ", two)}");

public interface IFuck<T>
{

}

public sealed class FuckForEverything<T> : IFuck<T>
{

}
public sealed class FuckForValueTypes<T> : IFuck<T>
    where T : struct
{

}
