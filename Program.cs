using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddSingleton(typeof(IBar<>), typeof(Bar<>));
services.AddSingleton<IFoo, Bar<int>>();
services.AddSingleton<IFoo, Bar<string>>();
var sp = services.BuildServiceProvider();
var foos = sp.GetServices<IFoo>().ToList();
Console.WriteLine($"Built: {foos.Count}");

public interface IFoo
{

}
public interface IBar<T> : IFoo
{

}
public class Bar<T> : IBar<T>
{

}

public class Test(
    IEnumerable<IFoo> foos
)
{
    public void Print() => Console.WriteLine(string.Join(", ", foos));
}
