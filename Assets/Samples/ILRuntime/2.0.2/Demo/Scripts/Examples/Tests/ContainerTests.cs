using System;
using System.Collections.Generic;
using NUnit.Framework;


public class ContainerTests
{
    private MinIoC MinIoC { get; set; }

    [Test]
    public void Initialize()
    {
        MinIoC = new MinIoC();
    }

    [Test]
    public void SimpleReflectionConstruction()
    {
        MinIoC.Register<IFoo>(typeof(Foo));

        object instance = MinIoC.Resolve<IFoo>();

        // Instance should be of the registered type 
        Assert.IsInstanceOf<Foo>(instance);
    }

    [Test]
    public void RecursiveReflectionConstruction()
    {
        MinIoC.Register<IFoo>(typeof(Foo));
        MinIoC.Register<IBar>(typeof(Bar));
        MinIoC.Register<IBaz>(typeof(Baz));

        var instance = MinIoC.Resolve<IBaz>();

        // Test that the correct types were created
        Assert.IsInstanceOf(typeof(Baz), instance);

        var baz = instance as Baz;
        Assert.IsInstanceOf(typeof(Bar), baz.Bar);
        Assert.IsInstanceOf(typeof(Foo), baz.Foo);
    }

    [Test]
    public void SimpleFactoryConstruction()
    {
        MinIoC.Register<IFoo>(() => new Foo());

        object instance = MinIoC.Resolve<IFoo>();

        // Instance should be of the registered type 
        Assert.IsInstanceOf(typeof(Foo), instance);
    }

    [Test]
    public void MixedConstruction()
    {
        MinIoC.Register<IFoo>(() => new Foo());
        MinIoC.Register<IBar>(typeof(Bar));
        MinIoC.Register<IBaz>(typeof(Baz));

        var instance = MinIoC.Resolve<IBaz>();

        // Test that the correct types were created
        Assert.IsInstanceOf(typeof(Baz), instance);

        var baz = instance as Baz;
        Assert.IsInstanceOf(typeof(Bar), baz.Bar);
        Assert.IsInstanceOf(typeof(Foo), baz.Foo);
    }

    [Test]
    public void InstanceResolution()
    {
        MinIoC.Register<IFoo>(typeof(Foo));

        object instance1 = MinIoC.Resolve<IFoo>();
        object instance2 = MinIoC.Resolve<IFoo>();

        // Instances should be different between calls to Resolve
        Assert.AreNotEqual(instance1, instance2);
    }

    [Test]
    public void SingletonResolution()
    {
        MinIoC.Register<IFoo>(typeof(Foo)).AsSingleton();

        object instance1 = MinIoC.Resolve<IFoo>();
        object instance2 = MinIoC.Resolve<IFoo>();

        // Instances should be identic between calls to Resolve
        Assert.AreEqual(instance1, instance2);
    }

    [Test]
    public void PerScopeResolution()
    {
        MinIoC.Register<IFoo>(typeof(Foo)).PerScope();

        object instance1 = MinIoC.Resolve<IFoo>();
        object instance2 = MinIoC.Resolve<IFoo>();

        // Instances should be same as the container is itself a scope
        Assert.AreEqual(instance1, instance2);

        using (var scope = MinIoC.CreateScope())
        {
            object instance3 = scope.Resolve<IFoo>();
            object instance4 = scope.Resolve<IFoo>();

            // Instances should be equal inside a scope
            Assert.AreEqual(instance3, instance4);

            // Instances should not be equal between scopes
            Assert.AreNotEqual(instance1, instance3);
        }
    }

    [Test]
    public void MixedScopeResolution()
    {
        MinIoC.Register<IFoo>(typeof(Foo)).PerScope();
        MinIoC.Register<IBar>(typeof(Bar)).AsSingleton();
        MinIoC.Register<IBaz>(typeof(Baz));

        using (var scope = MinIoC.CreateScope())
        {
            Baz instance1 = scope.Resolve<IBaz>() as Baz;
            Baz instance2 = scope.Resolve<IBaz>() as Baz;

            // Ensure resolutions worked as expected
            Assert.AreNotEqual(instance1, instance2);

            // Singleton should be same
            Assert.AreEqual(instance1.Bar, instance2.Bar);
            Assert.AreEqual((instance1.Bar as Bar).Foo, (instance2.Bar as Bar).Foo);

            // Scoped types should be the same
            Assert.AreEqual(instance1.Foo, instance2.Foo);

            // Singleton should not hold scoped object
            Assert.AreNotEqual(instance1.Foo, (instance1.Bar as Bar).Foo);
            Assert.AreNotEqual(instance2.Foo, (instance2.Bar as Bar).Foo);
        }
    }

    [Test]
    public void SingletonScopedResolution()
    {
        MinIoC.Register<IFoo>(typeof(Foo)).AsSingleton();
        MinIoC.Register<IBar>(typeof(Bar)).PerScope();

        var instance1 = MinIoC.Resolve<IBar>();

        using (var scope = MinIoC.CreateScope())
        {
            var instance2 = MinIoC.Resolve<IBar>();

            // Singleton should resolve to the same instance
            Assert.AreEqual((instance1 as Bar).Foo, (instance2 as Bar).Foo);
        }
    }

    [Test]
    public void MixedNoScopeResolution()
    {
        MinIoC.Register<IFoo>(typeof(Foo)).PerScope();
        MinIoC.Register<IBar>(typeof(Bar)).AsSingleton();
        MinIoC.Register<IBaz>(typeof(Baz));

        Baz instance1 = MinIoC.Resolve<IBaz>() as Baz;
        Baz instance2 = MinIoC.Resolve<IBaz>() as Baz;

        // Ensure resolutions worked as expected
        Assert.AreNotEqual(instance1, instance2);

        // Singleton should be same
        Assert.AreEqual(instance1.Bar, instance2.Bar);

        // Scoped types should not be different outside a scope
        Assert.AreEqual(instance1.Foo, instance2.Foo);
        Assert.AreEqual(instance1.Foo, (instance1.Bar as Bar).Foo);
        Assert.AreEqual(instance2.Foo, (instance2.Bar as Bar).Foo);
    }

    [Test]
    public void MixedReversedRegistration()
    {
        MinIoC.Register<IBaz>(typeof(Baz));
        MinIoC.Register<IBar>(typeof(Bar));
        MinIoC.Register<IFoo>(() => new Foo());

        IBaz instance = MinIoC.Resolve<IBaz>();

        // Test that the correct types were created
        Assert.IsInstanceOf(typeof(Baz), instance);

        var baz = instance as Baz;
        Assert.IsInstanceOf(typeof(Bar), baz.Bar);
        Assert.IsInstanceOf(typeof(Foo), baz.Foo);
    }

    [Test]
    public void ScopeDisposesOfCachedInstances()
    {
        MinIoC.Register<SpyDisposable>(typeof(SpyDisposable)).PerScope();
        SpyDisposable spy;

        using (var scope = MinIoC.CreateScope())
        {
            spy = scope.Resolve<SpyDisposable>();
        }

        Assert.IsTrue(spy.Disposed);
    }

    [Test]
    public void ContainerDisposesOfSingletons()
    {
        SpyDisposable spy;
        using (var container = new MinIoC())
        {
            container.Register<SpyDisposable>().AsSingleton();
            spy = container.Resolve<SpyDisposable>();
        }

        Assert.IsTrue(spy.Disposed);
    }

    [Test]
    public void SingletonsAreDifferentAcrossContainers()
    {
        var container1 = new MinIoC();
        container1.Register<IFoo>(typeof(Foo)).AsSingleton();

        var container2 = new MinIoC();
        container2.Register<IFoo>(typeof(Foo)).AsSingleton();

        Assert.AreNotEqual(container1.Resolve<IFoo>(), container2.Resolve<IFoo>());
    }

    [Test]
    public void GetServiceUnregisteredTypeReturnsNull()
    {
        using (var container = new MinIoC())
        {
            object value = container.GetService(typeof(Foo));

            Assert.IsNull(value);
        }
    }

    [Test]
    public void GetServiceMissingDependencyThrows()
    {
        using (var container = new MinIoC())
        {
            container.Register<Bar>();

            Assert.Throws<KeyNotFoundException>(() => container.GetService(typeof(Bar)));
        }
    }

    #region Types used for tests

    private interface IFoo
    {
    }

    private class Foo : IFoo
    {
    }

    private interface IBar
    {
    }

    private class Bar : IBar
    {
        public IFoo Foo { get; set; }

        public Bar(IFoo foo)
        {
            Foo = foo;
        }
    }

    private interface IBaz
    {
    }

    private class Baz : IBaz
    {
        public IFoo Foo { get; set; }
        public IBar Bar { get; set; }

        public Baz(IFoo foo, IBar bar)
        {
            Foo = foo;
            Bar = bar;
        }
    }

    private class SpyDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    #endregion
}