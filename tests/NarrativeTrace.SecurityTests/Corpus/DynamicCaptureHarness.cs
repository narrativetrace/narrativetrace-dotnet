// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using System.Reflection.Emit;

namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// Synthesizes, at runtime, a fresh one-method interface (plus a trivial implementation) whose
/// single parameter carries a caller-chosen name — the same mechanism
/// <see cref="RedactionCapturePathConformanceTests"/> needs to replay a corpus row whose field
/// name <em>is</em> the test data.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: <c>NarrativeInterceptor</c> reads a parameter's name off a real
/// <see cref="ParameterInfo"/>, recovered by reflection the same way production code's own
/// interfaces are — there is no seam to hand it a name directly. A C# interface's parameter names
/// are fixed at compile time, but the corpus's names are DATA (accented, NFD-decomposed, and CJK
/// spellings among them), so the only way to drive a real name through the real capture path is to
/// generate the interface (and a class implementing it) at runtime, the way a mocking framework or
/// an ORM's proxy generator would.
/// </para>
/// <para>
/// <b>@edgeCase</b> IL parameter names are just metadata-heap strings — the CLR does not enforce
/// C# identifier grammar on them — so in practice every corpus row's name is expected to build
/// cleanly. <see cref="Build"/> still cannot rule out every possible failure (a name containing an
/// embedded NUL, for instance), so it is never called unguarded: see
/// <see cref="RedactionCapturePathConformanceTests"/>'s loud, failing skip for the row a build
/// throws for.
/// </para>
/// </remarks>
internal static class DynamicCaptureHarness
{
    private const string MethodName = "Invoke";

    private static readonly ModuleBuilder Module = CreateModule();

    /// <summary>
    /// Builds a public interface <c>void Invoke(string &lt;parameterName&gt;)</c>, a public class
    /// implementing it with an empty body, and an instance of that class — everything
    /// <see cref="NarrativeTrace.Proxy.NarrativeTraceProxy.Create(Type, object, NarrativeTrace.Core.INarrativeContext, NarrativeTrace.Proxy.ProxyOptions?)"/>
    /// needs to wrap a real traced call whose one parameter is named <paramref name="parameterName"/>.
    /// </summary>
    /// <param name="caseId">The corpus row id, folded into the generated type names for a readable dump/ILDasm trace.</param>
    /// <param name="parameterName">The exact parameter name to emit — corpus data, not validated here.</param>
    public static DynamicCaptureCase Build(string caseId, string parameterName)
    {
        var suffix = "_" + Guid.NewGuid().ToString("N");
        var ifaceType = BuildInterface(SafeTypeName(caseId) + "_I" + suffix, parameterName);
        var implType = BuildImplementation(SafeTypeName(caseId) + "_C" + suffix, ifaceType);
        var instance = Activator.CreateInstance(implType)
            ?? throw new InvalidOperationException($"could not instantiate generated type for '{caseId}'");
        var method = ifaceType.GetMethod(MethodName)
            ?? throw new InvalidOperationException($"generated interface for '{caseId}' lost its own method");
        return new DynamicCaptureCase(ifaceType, instance, method);
    }

    private static Type BuildInterface(string typeName, string parameterName)
    {
        var builder = Module.DefineType(
            typeName,
            TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        var method = builder.DefineMethod(
            MethodName,
            MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual
                | MethodAttributes.NewSlot,
            typeof(void),
            [typeof(string)]);
        method.DefineParameter(1, ParameterAttributes.None, parameterName);
        return builder.CreateType();
    }

    private static Type BuildImplementation(string typeName, Type ifaceType)
    {
        var builder = Module.DefineType(
            typeName, TypeAttributes.Public, typeof(object), [ifaceType]);
        var method = builder.DefineMethod(
            MethodName,
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final,
            typeof(void),
            [typeof(string)]);
        method.GetILGenerator().Emit(OpCodes.Ret);
        builder.DefineMethodOverride(method, ifaceType.GetMethod(MethodName)!);
        return builder.CreateType();
    }

    // Generated type names never reach a C# compiler either, but keeping them readable (ASCII,
    // no punctuation beyond underscore) is worth the trivial cost for anyone reading a dump.
    private static string SafeTypeName(string caseId)
    {
        var chars = caseId.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return "Case_" + new string(chars);
    }

    private static ModuleBuilder CreateModule()
    {
        var name = new AssemblyName("NarrativeTrace.SecurityTests.DynamicRedactionCorpus");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        return assembly.DefineDynamicModule("MainModule");
    }
}

/// <summary>The generated pieces <see cref="DynamicCaptureHarness.Build"/> hands back for one corpus row.</summary>
/// <param name="InterfaceType">The generated one-method interface, ready for <c>NarrativeTraceProxy.Create</c>.</param>
/// <param name="Instance">A live instance of the generated implementation, the proxy's forwarding target.</param>
/// <param name="Method">The interface's own <see cref="MethodInfo"/>, for invoking through the proxy reflectively.</param>
internal sealed record DynamicCaptureCase(Type InterfaceType, object Instance, MethodInfo Method);
