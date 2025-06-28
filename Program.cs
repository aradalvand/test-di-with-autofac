using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddSingleton(typeof(IBar<>), typeof(FooBar<>));
services.AddSingleton(typeof(IBar<>), typeof(GeneralBar<>));

var sp = services.BuildServiceProvider();

var bar1 = sp.GetServices<IBar<Foo>>();
Console.WriteLine($"IBar<Foo>s: {string.Join(", ", bar1.Select(e => e.GetType().Name))}");

var bar2 = sp.GetServices<IBar<int>>();
Console.WriteLine($"IBar<int>s: {string.Join(", ", bar2.Select(e => e.GetType().Name))}");

public interface IBar<T> : IFoo;
public class FooBar<T> : IBar<T> where T : IFoo;
public class GeneralBar<T> : IBar<T>;

public interface IFoo;
public class Foo : IFoo;
