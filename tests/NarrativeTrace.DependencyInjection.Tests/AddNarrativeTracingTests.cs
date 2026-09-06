// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.DependencyInjection;
using NarrativeTrace.Core;
using NarrativeTrace.DependencyInjection;
using Xunit;

namespace NarrativeTrace.DependencyInjection.Tests;

public interface IGreeter
{
    string Greet(string name);
}

public sealed class Greeter : IGreeter
{
    public string Greet(string name) => $"Hello {name}";
}

public class AddNarrativeTracingTests
{
    private const string TestNamespace =
        "NarrativeTrace.DependencyInjection.Tests";

    [Fact]
    public void Matched_interface_service_is_wrapped_in_a_proxy()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGreeter, Greeter>();
        services.AddNarrativeTracing(o => o.Namespaces(TestNamespace));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var greeter = scope.ServiceProvider
            .GetRequiredService<IGreeter>();

        // The resolved instance is a proxy, not the raw implementation.
        Assert.NotEqual(typeof(Greeter), greeter.GetType());
        Assert.Equal("Hello Ada", greeter.Greet("Ada"));
    }

    [Fact]
    public void Wrapped_call_is_recorded_in_the_scoped_context()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGreeter, Greeter>();
        services.AddNarrativeTracing(o => o.Namespaces(TestNamespace));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<IGreeter>().Greet("Ada");

        var trace = sp.GetRequiredService<INarrativeContext>()
            .CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Greet", trace.Roots[0].Signature.MethodName);
    }

    [Fact]
    public void Service_outside_configured_namespace_is_not_wrapped()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGreeter, Greeter>();
        services.AddNarrativeTracing(o => o.Namespaces("Some.Other.App"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var greeter = scope.ServiceProvider
            .GetRequiredService<IGreeter>();

        Assert.Equal(typeof(Greeter), greeter.GetType());
    }

    [Fact]
    public void Factory_registered_service_is_wrapped()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGreeter>(_ => new Greeter());
        services.AddNarrativeTracing(o => o.Namespaces(TestNamespace));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var greeter = scope.ServiceProvider
            .GetRequiredService<IGreeter>();

        Assert.NotEqual(typeof(Greeter), greeter.GetType());
        Assert.Equal("Hello Bo", greeter.Greet("Bo"));
    }

    [Fact]
    public void Keyed_service_is_tolerated_and_left_unwrapped()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IGreeter, Greeter>("k");

        var ex = Record.Exception(() =>
            services.AddNarrativeTracing(o => o.Namespaces(TestNamespace)));

        Assert.Null(ex);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var greeter = scope.ServiceProvider
            .GetRequiredKeyedService<IGreeter>("k");
        Assert.Equal(typeof(Greeter), greeter.GetType());
    }

    [Fact]
    public void Interface_in_an_excluded_namespace_is_left_unwrapped()
    {
        var services = new ServiceCollection();
        services.AddScoped<Infra.Contracts.IInfra, InfraImpl>();
        services.AddNarrativeTracing(o => o
            .Namespaces(TestNamespace)
            .ExcludeNamespaces("Infra.Contracts"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var infra = scope.ServiceProvider
            .GetRequiredService<Infra.Contracts.IInfra>();

        Assert.Equal(typeof(InfraImpl), infra.GetType());
    }

    [Fact]
    public void Open_generic_interface_is_not_wrapped()
    {
        var services = new ServiceCollection();
        services.AddScoped(typeof(IRepo<>), typeof(Repo<>));
        services.AddNarrativeTracing(o => o.Namespaces(TestNamespace));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var repo = scope.ServiceProvider
            .GetRequiredService<IRepo<int>>();

        Assert.Equal(typeof(Repo<int>), repo.GetType());
    }

    [Fact]
    public void Multi_interface_singleton_wraps_each_interface_independently()
    {
        var impl = new MultiService();
        var services = new ServiceCollection();
        services.AddSingleton<IAlpha>(impl);
        services.AddSingleton<IBeta>(impl);
        services.AddNarrativeTracing(o => o.Namespaces(TestNamespace));

        using var provider = services.BuildServiceProvider();
        var alpha = provider.GetRequiredService<IAlpha>();
        var beta = provider.GetRequiredService<IBeta>();

        // Both interfaces are wrapped and traced...
        Assert.NotEqual(typeof(MultiService), alpha.GetType());
        Assert.NotEqual(typeof(MultiService), beta.GetType());
        Assert.Equal("a", alpha.A());
        Assert.Equal("b", beta.B());
        // ...but DispatchProxy is one-interface-per-proxy, so the two proxies
        // are distinct objects — a documented divergence from Java's single
        // multi-interface proxy (the underlying target instance is shared).
        Assert.NotSame(alpha, (object)beta);
    }

    [Fact]
    public void Singleton_lifetime_is_preserved_after_wrapping()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGreeter, Greeter>();
        services.AddNarrativeTracing(o => o.Namespaces(TestNamespace));

        using var provider = services.BuildServiceProvider();
        var a = provider.GetRequiredService<IGreeter>();
        var b = provider.GetRequiredService<IGreeter>();

        Assert.Same(a, b);
    }
}

public interface IRepo<T>
{
    T? Get();
}

public sealed class Repo<T> : IRepo<T>
{
    public T? Get() => default;
}

// Implementation lives in the matched namespace; the interface is in a
// separately excludable namespace so DI-2 exclusion can be exercised.
public sealed class InfraImpl : Infra.Contracts.IInfra
{
    public string Ping() => "pong";
}

public interface IAlpha
{
    string A();
}

public interface IBeta
{
    string B();
}

public sealed class MultiService : IAlpha, IBeta
{
    public string A() => "a";
    public string B() => "b";
}
