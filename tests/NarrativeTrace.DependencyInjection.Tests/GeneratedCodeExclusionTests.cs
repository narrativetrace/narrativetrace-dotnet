// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace NarrativeTrace.DependencyInjection.Tests;

[CompilerGenerated]
public sealed class SynthesisedGreeter : IGreeter
{
    public string Greet(string name) => $"Synth {name}";

    public sealed class Inner
    {
    }
}

public abstract class GreeterProxyBase : DispatchProxy, IGreeter
{
    public string Greet(string name) => name;
}

/// <summary>List 1: structurally generated targets are never traced, whatever the include list says.</summary>
public class GeneratedCodeExclusionTests
{
    private const string TestNamespace = "NarrativeTrace.DependencyInjection.Tests";

    private static async Task<int> AsyncMethod()
    {
        await Task.Yield();
        return 1;
    }

    [Fact]
    public void Compiler_generated_attribute_marks_a_type_generated()
    {
        Assert.True(GeneratedTypes.IsGenerated(typeof(SynthesisedGreeter)));
    }

    [Fact]
    public void Closure_display_class_is_generated()
    {
        var captured = 3;
        Func<int> closure = () => captured;

        Assert.True(GeneratedTypes.IsGenerated(closure.Target!.GetType()));
    }

    [Fact]
    public void Async_state_machine_is_generated()
    {
        var machine = typeof(GeneratedCodeExclusionTests)
            .GetMethod(nameof(AsyncMethod), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;

        Assert.True(GeneratedTypes.IsGenerated(machine));
    }

    [Fact]
    public void Anonymous_type_is_generated()
    {
        Assert.True(GeneratedTypes.IsGenerated(new { A = 1 }.GetType()));
    }

    [Fact]
    public void Nested_type_inside_a_generated_type_is_generated()
    {
        var nested = typeof(SynthesisedGreeter.Inner);

        Assert.True(GeneratedTypes.IsGenerated(nested));
    }

    [Fact]
    public void Type_from_a_dynamic_assembly_is_generated()
    {
        Assert.True(GeneratedTypes.IsGenerated(EmitGreeter().GetType()));
    }

    [Fact]
    public void Dispatch_proxy_subclass_is_generated()
    {
        Assert.True(GeneratedTypes.IsGenerated(typeof(GreeterProxyBase)));
    }

    [Fact]
    public void Hand_written_class_is_not_generated()
    {
        Assert.False(GeneratedTypes.IsGenerated(typeof(Greeter)));
        Assert.False(GeneratedTypes.IsGenerated(typeof(List<Greeter>)));
    }

    [Fact]
    public void Compiler_generated_implementation_under_an_included_namespace_is_not_wrapped()
    {
        var provider = Provider(s => s.AddSingleton<IGreeter, SynthesisedGreeter>());

        Assert.Equal(typeof(SynthesisedGreeter), provider.GetRequiredService<IGreeter>().GetType());
    }

    [Fact]
    public void Dynamic_assembly_instance_under_an_included_namespace_is_not_wrapped()
    {
        var emitted = EmitGreeter();
        var provider = Provider(s => s.AddSingleton<IGreeter>(emitted));

        Assert.Same(emitted, provider.GetRequiredService<IGreeter>());
    }

    [Fact]
    public void Factory_registration_resolving_a_generated_instance_is_not_wrapped()
    {
        var emitted = EmitGreeter();
        var provider = Provider(s => s.AddSingleton<IGreeter>(_ => emitted));

        Assert.Same(emitted, provider.GetRequiredService<IGreeter>());
    }

    [Fact]
    public void Hand_written_neighbour_of_generated_code_is_still_wrapped()
    {
        var provider = Provider(s => s.AddSingleton<IGreeter, Greeter>());

        Assert.NotEqual(typeof(Greeter), provider.GetRequiredService<IGreeter>().GetType());
    }

    private static ServiceProvider Provider(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        services.AddNarrativeTracing(o => o.Namespaces(TestNamespace));
        return services.BuildServiceProvider();
    }

    private static IGreeter EmitGreeter()
    {
        var module = AssemblyBuilder
            .DefineDynamicAssembly(new AssemblyName("EmittedGreeters" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run)
            .DefineDynamicModule("m");
        var type = module.DefineType(TestNamespace + ".EmittedGreeter", TypeAttributes.Public, typeof(object), [typeof(IGreeter)]);
        var method = type.DefineMethod(
            nameof(IGreeter.Greet), MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            typeof(string), [typeof(string)]);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ret);
        type.DefineMethodOverride(method, typeof(IGreeter).GetMethod(nameof(IGreeter.Greet))!);
        type.DefineDefaultConstructor(MethodAttributes.Public);
        return (IGreeter)Activator.CreateInstance(type.CreateType())!;
    }
}
